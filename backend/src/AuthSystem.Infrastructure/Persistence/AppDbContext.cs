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
    public DbSet<VaultProfile> VaultProfiles => Set<VaultProfile>();
    public DbSet<VaultItem> VaultItems => Set<VaultItem>();
    public DbSet<VaultItemAttachment> VaultItemAttachments => Set<VaultItemAttachment>();
    public DbSet<VaultKeyShare> VaultKeyShares => Set<VaultKeyShare>();

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

        builder.Entity<VaultProfile>(e =>
        {
            e.ToTable("VaultProfiles");
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.UserId).IsUnique();
            e.Property(v => v.PublicKey).IsRequired();
            e.Property(v => v.KdfSalt).IsRequired();
            e.Property(v => v.PrivateKeyWrappedByMasterKey).IsRequired();
            e.Property(v => v.PrivateKeyWrappedByRecoveryKey).IsRequired();
            e.Property(v => v.KeySchemeVersion).HasDefaultValue(1);
            e.HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VaultItem>(e =>
        {
            e.ToTable("VaultItems");
            e.HasKey(i => i.Id);
            e.Property(i => i.CiphertextBlob).IsRequired();
            e.HasIndex(i => i.OwnerUserId);
            e.HasOne(i => i.Owner)
                .WithMany()
                .HasForeignKey(i => i.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VaultItemAttachment>(e =>
        {
            e.ToTable("VaultItemAttachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(255).IsRequired();
            e.Property(a => a.Iv).IsRequired();
            e.Property(a => a.CiphertextBytes).HasColumnType("varbinary(max)").IsRequired();
            e.HasIndex(a => a.VaultItemId);
            e.HasOne(a => a.VaultItem)
                .WithMany()
                .HasForeignKey(a => a.VaultItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VaultKeyShare>(e =>
        {
            e.ToTable("VaultKeyShares");
            e.HasKey(s => s.Id);
            e.Property(s => s.WrappedUvk).IsRequired();
            // At most one active grant per (owner, holder) pair, including the self-share row.
            e.HasIndex(s => new { s.VaultOwnerUserId, s.HolderUserId })
                .IsUnique()
                .HasFilter("[RevokedAt] IS NULL");
            e.HasOne(s => s.VaultOwner)
                .WithMany()
                .HasForeignKey(s => s.VaultOwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Must be Restrict, not Cascade: two FKs from this table into Users would otherwise
            // give SQL Server multiple cascade paths and the migration would fail to apply.
            e.HasOne(s => s.Holder)
                .WithMany()
                .HasForeignKey(s => s.HolderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
