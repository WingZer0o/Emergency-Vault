using System.Security.Cryptography;
using AuthSystem.Application;
using AuthSystem.Application.Options;
using AuthSystem.Domain.Constant;
using AuthSystem.Infrastructure;
using AuthSystem.Infrastructure.Persistence;
using AuthSystem.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
await InfisicalSetup.InjectEmailAPISecrets();

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
    .SetApplicationName("AuthSystem");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

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

builder.Services.AddAuthorization();

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AuthSystem API", Version = "v1" });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login.",
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", null, null), [] }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services);

app.Run();
