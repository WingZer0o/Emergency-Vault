namespace EmergencyVault.Domain.Entities;

/// <summary>
/// Records a single instance of a non-owner viewing a vault item via the shared-vault flow.
/// Append-only audit log - never updated, only inserted (and cascade-deleted with the item).
/// </summary>
public class VaultItemView
{
    public Guid Id { get; set; }

    public Guid VaultItemId { get; set; }
    public VaultItem VaultItem { get; set; } = null!;

    public Guid ViewerUserId { get; set; }
    public ApplicationUser Viewer { get; set; } = null!;

    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;
}
