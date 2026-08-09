using AuthSystem.Application.Interfaces;
using AuthSystem.Domain.Entities;
using AuthSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthSystem.Infrastructure.Repositories;

public class VaultRepository(AppDbContext db) : IVaultRepository
{
    public async Task<VaultProfile?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        return await db.VaultProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task AddProfileAsync(VaultProfile profile, CancellationToken ct)
    {
        await db.VaultProfiles.AddAsync(profile, ct);
    }

    public async Task<VaultKeyShare?> GetActiveShareAsync(Guid vaultOwnerUserId, Guid holderUserId, CancellationToken ct)
    {
        return await db.VaultKeyShares.FirstOrDefaultAsync(
            s => s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId == holderUserId && s.RevokedAt == null, ct);
    }

    public async Task<VaultKeyShare?> GetShareByIdAsync(Guid shareId, CancellationToken ct)
    {
        return await db.VaultKeyShares.FirstOrDefaultAsync(s => s.Id == shareId, ct);
    }

    public async Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesGrantedByAsync(Guid vaultOwnerUserId, CancellationToken ct)
    {
        return await db.VaultKeyShares
            .Include(s => s.Holder)
            .Where(s => s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId != vaultOwnerUserId && s.RevokedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesHeldByAsync(Guid holderUserId, CancellationToken ct)
    {
        return await db.VaultKeyShares
            .Include(s => s.VaultOwner)
            .Where(s => s.HolderUserId == holderUserId && s.VaultOwnerUserId != holderUserId && s.RevokedAt == null)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteHoldersSharesByIdAsync(Guid holderUserId, CancellationToken ct)
    {
        return await db.VaultKeyShares.Where(s => s.HolderUserId == holderUserId).ExecuteDeleteAsync(ct);
    }

    public async Task AddShareAsync(VaultKeyShare share, CancellationToken ct)
    {
        await db.VaultKeyShares.AddAsync(share, ct);
    }

    public void RemoveShare(VaultKeyShare share)
    {
        db.VaultKeyShares.Remove(share);
    }

    public async Task<IReadOnlyList<VaultItem>> GetItemsByOwnerAsync(Guid ownerUserId, CancellationToken ct)
    {
        return await db.VaultItems.Where(i => i.OwnerUserId == ownerUserId).ToListAsync(ct);
    }

    public async Task<VaultItem?> GetItemAsync(Guid itemId, Guid ownerUserId, CancellationToken ct)
    {
        return await db.VaultItems.FirstOrDefaultAsync(i => i.Id == itemId && i.OwnerUserId == ownerUserId, ct);
    }

    public async Task<VaultItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct)
    {
        return await db.VaultItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
    }

    public async Task AddItemAsync(VaultItem item, CancellationToken ct)
    {
        await db.VaultItems.AddAsync(item, ct);
    }

    public void RemoveItem(VaultItem item)
    {
        db.VaultItems.Remove(item);
    }

    public async Task<VaultItemAttachment?> GetAttachmentAsync(Guid attachmentId, Guid vaultItemId, CancellationToken ct)
    {
        return await db.VaultItemAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.VaultItemId == vaultItemId, ct);
    }

    public async Task<IReadOnlyList<VaultItemAttachment>> GetAttachmentsByItemAsync(Guid vaultItemId, CancellationToken ct)
    {
        // Excludes CiphertextBytes - listing metadata should never pull blob bytes through EF.
        return await db.VaultItemAttachments
            .Where(a => a.VaultItemId == vaultItemId)
            .Select(a => new VaultItemAttachment
            {
                Id = a.Id,
                VaultItemId = a.VaultItemId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                Iv = a.Iv,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task AddAttachmentAsync(VaultItemAttachment attachment, CancellationToken ct)
    {
        await db.VaultItemAttachments.AddAsync(attachment, ct);
    }

    public void RemoveAttachment(VaultItemAttachment attachment)
    {
        db.VaultItemAttachments.Remove(attachment);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }
}
