using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthSystem.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserFailedLogin> UserFailedLogins => Set<UserFailedLogin>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e => e.ToTable("Users"));
        builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(e => e.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(e => e.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(e => e.ToTable("UserLogins"));
        builder.Entity<IdentityUserToken<Guid>>(e => e.ToTable("UserTokens"));
        builder.Entity<IdentityRoleClaim<Guid>>(e => e.ToTable("RoleClaims"));

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserFailedLogin>(e =>
        {
            e.ToTable("UserFailedLogins");
            e.HasKey(t => t.Id);
            e.Property(t => t.Email).HasMaxLength(256).IsRequired();
            e.Property(t => t.Reason).HasMaxLength(64).IsRequired();
            e.Property(t => t.AttemptedByIp).HasMaxLength(45);
            e.HasOne(t => t.User)
                .WithMany(u => u.FailedLogins)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
