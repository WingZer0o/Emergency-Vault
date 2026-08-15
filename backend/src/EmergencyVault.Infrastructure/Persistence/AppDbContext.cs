using EmergencyVault.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmergencyVault.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserFailedLogin> UserFailedLogins => Set<UserFailedLogin>();
    public DbSet<VaultProfile> VaultProfiles => Set<VaultProfile>();
    public DbSet<VaultItem> VaultItems => Set<VaultItem>();
    public DbSet<VaultItemAttachment> VaultItemAttachments => Set<VaultItemAttachment>();
    public DbSet<VaultKeyShare> VaultKeyShares => Set<VaultKeyShare>();
    public DbSet<VaultItemView> VaultItemViews => Set<VaultItemView>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<BillingSetting> BillingSettings => Set<BillingSetting>();

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
            e.Property(s => s.WrappedKey).IsRequired();

            // At most one active whole-vault share per pair (covers the self-share row too). A
            // single index on the raw nullable VaultItemId column would NOT enforce this - SQL
            // Server treats every NULL as distinct, so these two invariants need separate filtered
            // indexes rather than one index over all three columns.
            e.HasIndex(s => new { s.VaultOwnerUserId, s.HolderUserId })
                .IsUnique()
                .HasFilter("[RevokedAt] IS NULL AND [VaultItemId] IS NULL")
                .HasDatabaseName("IX_VaultKeyShares_Owner_Holder_WholeVault");
            // At most one active share of a given item per pair. A holder may still separately hold
            // both a whole-vault share and a redundant item-scoped share to the same owner.
            e.HasIndex(s => new { s.VaultOwnerUserId, s.HolderUserId, s.VaultItemId })
                .IsUnique()
                .HasFilter("[RevokedAt] IS NULL AND [VaultItemId] IS NOT NULL")
                .HasDatabaseName("IX_VaultKeyShares_Owner_Holder_Item");

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
            // Restrict, not Cascade: VaultItem already has a Cascade path into Users via Owner, so a
            // second cascade path from this table into Users (via VaultItem) would hit the same
            // multi-cascade-path problem as Holder above. Because share rows are only ever
            // soft-revoked (never hard-deleted) elsewhere, VaultService.DeleteItemAsync must
            // explicitly hard-delete an item's share rows before removing the item, or the item
            // would become permanently undeletable once it had ever been shared.
            e.HasOne(s => s.VaultItem)
                .WithMany()
                .HasForeignKey(s => s.VaultItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VaultItemView>(e =>
        {
            e.ToTable("VaultItemViews");
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.VaultItemId);
            e.HasOne(v => v.VaultItem)
                .WithMany()
                .HasForeignKey(v => v.VaultItemId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict, not Cascade: VaultItem already has a Cascade path into Users via Owner, so a
            // second cascade path from this table into Users (via Viewer) would give SQL Server
            // multiple cascade paths and the migration would fail to apply - same rationale as
            // VaultKeyShare.Holder above.
            e.HasOne(v => v.Viewer)
                .WithMany()
                .HasForeignKey(v => v.ViewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Subscription>(e =>
        {
            e.ToTable("Subscriptions");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.StripeCustomerId).HasMaxLength(255);
            e.Property(s => s.StripeSubscriptionId).HasMaxLength(255);
            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BillingSetting>(e =>
        {
            e.ToTable("BillingSettings");
            e.HasKey(b => b.Id);
            e.Property(b => b.Currency).HasMaxLength(8);
            e.HasData(new BillingSetting
            {
                Id = BillingSetting.SingletonId,
                YearlyPriceCents = 5000,
                Currency = "usd",
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
        });
    }
}
