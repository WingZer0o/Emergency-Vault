using System.Threading.RateLimiting;
using EmergencyVault.Api.Authorization;
using EmergencyVault.Application;
using EmergencyVault.Application.Options;
using EmergencyVault.Domain.Constant;
using EmergencyVault.Infrastructure;
using EmergencyVault.Infrastructure.Persistence;
using EmergencyVault.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
await InfisicalSetup.InjectEmailAPISecrets();

StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable(EnvironmentVariableConst.StripeSecretKey)
    ?? throw new InvalidOperationException($"Missing {EnvironmentVariableConst.StripeSecretKey} environment variable.");

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");

// Loaded once here and shared as a singleton so the signing key (TokenService) and the
// JWT Bearer validation parameters below always agree on the exact same ECDSA P-521 key.
using var keyLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var signingKeyPem = Environment.GetEnvironmentVariable(EnvironmentVariableConst.JwtSigningKeyPem)
    ?? throw new InvalidOperationException($"Missing {EnvironmentVariableConst.JwtSigningKeyPem} environment variable.");
var signingKey = EcdsaKeyProvider.Load(signingKeyPem, keyLoggerFactory.CreateLogger("EcdsaSigningKey"));
builder.Services.AddSingleton(signingKey);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

// Raises Kestrel's ~30MB default so vault-item file attachments can approach SQL Server's
// varbinary(max) ceiling (2^31-1 bytes). This is a process-wide default, not per-route - Kestrel
// has no per-endpoint body size config - the upload action additionally uses
// [DisableRequestSizeLimit] to remove ASP.NET Core's separate request-size feature check.
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 2_147_483_648; // 2 GiB
});

builder.Services.AddDataProtection()
    .SetApplicationName("EmergencyVault");

// Kestrel never sees the browser directly. In the Docker prod overlay there's one hop (nginx).
// In Azure prod there are now two: the Static Web App edge (linked backend) fronting Container
// Apps' Envoy-based ingress. Each hop appends its peer's IP to X-Forwarded-For, so by the time it
// reaches Kestrel the header is "<real client IP>, <SWA edge IP>" - this rewrites
// HttpContext.Connection.RemoteIpAddress from that header so AuthController and the rate limiter
// below see the real client IP rather than the SWA edge's. KnownNetworks/KnownProxies are cleared
// because those hops' IPs aren't fixed, known values (SWA's edge IP and the ACA revision's IP both
// vary) - safe only because Kestrel itself is never reachable except through that exact proxy
// chain (nginx is the only published port in the Docker prod overlay; the Container App's ingress
// must be locked down to only accept traffic from the linked Static Web App, otherwise anyone can
// bypass SWA and spoof this header directly).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 2;
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();


builder.Services.AddRateLimiter(options =>
{
   options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromMinutes(1)
        })); 
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new ECDsaSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, ActiveSubscriptionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveSubscription", policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new ActiveSubscriptionRequirement()));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Must run before anything that reads HttpContext.Connection.RemoteIpAddress or the request
// scheme (CORS, auth, controllers) - it rewrites both from the forwarded headers set above.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services);

app.Run();
