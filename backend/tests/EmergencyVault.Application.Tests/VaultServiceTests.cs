using EmergencyVault.Application.Dtos;
using EmergencyVault.Application.Options;
using EmergencyVault.Application.Services;
using EmergencyVault.Application.Tests.Fakes;
using EmergencyVault.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmergencyVault.Application.Tests;

/// <summary>
/// Covers the authorization-sensitive paths of VaultService specifically - not full coverage.
/// A bug here means one user's encrypted vault (bank credentials, etc.) leaking to another user,
/// so these are the cases that matter most: cross-user access, self-targeting, revocation, item
/// scoping (an item-scoped share holder must never reach another item or the owner's whole list),
/// and the two email-code gates layered on sharing - accepting an invite (VerifyShareAsync) and,
/// separately, the per-session access challenge required on every subsequent view.
/// </summary>
public class VaultServiceTests
{
    private static (FakeVaultRepository Repo, UserManager<ApplicationUser> Users, VaultService Service, FakeEmailSender Email) CreateService(
        VaultShareCodesOptions? shareCodesOptions = null)
    {
        var store = new FakeUserStore();
        var userManager = new UserManager<ApplicationUser>(
            store,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var repo = new FakeVaultRepository();
        var email = new FakeEmailSender();
        var service = new VaultService(
            repo,
            new FakeVaultAttachmentBlobStore(),
            userManager,
            new FakeHotpService(),
            email,
            new FakeSharedVaultAccessChallengeCache(),
            new FakeSharedVaultAccessGrantCache(),
            Microsoft.Extensions.Options.Options.Create(shareCodesOptions ?? new VaultShareCodesOptions()),
            NullLogger<VaultService>.Instance);
        return (repo, userManager, service, email);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string first = "Test", string last = "User")
    {
        // EF Core's SQL Server provider auto-generates a sequential Guid for a default-valued Id
        // on save; the in-memory FakeUserStore has no such behavior, so every test user needs an
        // explicit Id or they'd all collide on Guid.Empty.
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded);
        return user;
    }

    private static async Task SetUpVaultAsync(VaultService service, Guid userId, string publicKey = "pub")
    {
        var request = new VaultSetupRequest(
            publicKey, "salt", 600_000, "wrappedPriv", "wrappedPrivRecovery", "selfWrappedUvk",
            "mlKemPub", "mlKemSecretMaster", "mlKemSecretRecovery");
        var result = await service.SetupAsync(userId, request, CancellationToken.None);
        Assert.True(result.Succeeded);
    }

    /// <summary>Creates a share and immediately accepts the invite (VerifyShareAsync) with the code that was emailed for it.</summary>
    private static async Task<VaultShareDto> CreateAndAcceptShareAsync(
        VaultService service, FakeEmailSender email, Guid ownerId, Guid holderId, string holderEmail,
        Guid? vaultItemId = null, bool canEdit = false)
    {
        var share = await service.CreateShareAsync(ownerId, new CreateVaultShareRequest(holderEmail, "wrapped", vaultItemId, canEdit), CancellationToken.None);
        Assert.True(share.Succeeded);
        var code = email.VaultShareCodesSent.Last(c => c.ToEmail == holderEmail).Code;
        var verify = await service.VerifyShareAsync(holderId, share.Data!.Id, code, CancellationToken.None);
        Assert.True(verify.Succeeded);
        return share.Data!;
    }

    /// <summary>Passes the separate per-session access challenge (RequestSharedVaultAccessAsync/VerifySharedVaultAccessAsync) required before an accepted share can actually be viewed.</summary>
    private static async Task GrantSessionAccessAsync(VaultService service, FakeEmailSender email, Guid holderId, Guid ownerId)
    {
        var challenge = await service.RequestSharedVaultAccessAsync(holderId, ownerId, CancellationToken.None);
        Assert.True(challenge.Succeeded);
        if (!challenge.Data!.RequiresCode)
        {
            return;
        }

        var code = email.VaultShareCodesSent.Last().Code;
        var verify = await service.VerifySharedVaultAccessAsync(holderId, challenge.Data.ChallengeToken!, code, CancellationToken.None);
        Assert.True(verify.Succeeded);
    }

    [Fact]
    public async Task SetupAsync_Fails_WhenAlreadySetUp()
    {
        var (_, _, service, _) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var second = await service.SetupAsync(
            userId, new VaultSetupRequest("pub2", "salt2", 600_000, "a", "b", "c", "d", "e", "f"), CancellationToken.None);

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task GetItemAsync_Fails_ForNonOwner()
    {
        var (repo, _, service, _) = CreateService();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var item = new VaultItem { Id = Guid.NewGuid(), OwnerUserId = ownerId, CiphertextBlob = "secret" };
        repo.Items.Add(item);

        var result = await service.GetItemAsync(attackerId, item.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateItemAsync_Fails_ForNonOwner()
    {
        var (repo, _, service, _) = CreateService();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var item = new VaultItem { Id = Guid.NewGuid(), OwnerUserId = ownerId, CiphertextBlob = "secret" };
        repo.Items.Add(item);

        var result = await service.UpdateItemAsync(attackerId, item.Id, new UpdateVaultItemRequest("tampered", null), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("secret", item.CiphertextBlob);
    }

    [Fact]
    public async Task UpdateItemAsync_RecordsRevision_WithPreviousCiphertextAndEditor()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        var created = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("original", "wrappedItemKey"), CancellationToken.None);

        var result = await service.UpdateItemAsync(owner.Id, created.Data!.Id, new UpdateVaultItemRequest("updated", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        var revision = Assert.Single(repo.ItemRevisions);
        Assert.Equal(created.Data.Id, revision.VaultItemId);
        Assert.Equal("original", revision.PreviousCiphertextBlob);
        Assert.Equal(owner.Id, revision.ChangedByUserId);
    }

    [Fact]
    public async Task GetItemRevisionsAsync_ReturnsHistory_ForOwner()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        var created = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("v1", "wrappedItemKey"), CancellationToken.None);
        await service.UpdateItemAsync(owner.Id, created.Data!.Id, new UpdateVaultItemRequest("v2", null), CancellationToken.None);
        // The fake repo doesn't simulate EF's Include(r => r.ChangedBy) - populate it directly, as a real query would.
        Assert.Single(repo.ItemRevisions).ChangedBy = owner;

        var result = await service.GetItemRevisionsAsync(owner.Id, created.Data.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var revision = Assert.Single(result.Data!);
        Assert.Equal("v1", revision.PreviousCiphertextBlob);
        Assert.Equal(owner.Id, revision.ChangedByUserId);
    }

    [Fact]
    public async Task GetItemRevisionsAsync_Fails_ForNonOwner()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var attacker = await CreateUserAsync(users, "attacker@example.com");
        await SetUpVaultAsync(service, owner.Id);
        var created = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("v1", "wrappedItemKey"), CancellationToken.None);
        await service.UpdateItemAsync(owner.Id, created.Data!.Id, new UpdateVaultItemRequest("v2", null), CancellationToken.None);

        var result = await service.GetItemRevisionsAsync(attacker.Id, created.Data.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteItemAsync_Fails_ForNonOwner()
    {
        var (repo, _, service, _) = CreateService();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var item = new VaultItem { Id = Guid.NewGuid(), OwnerUserId = ownerId, CiphertextBlob = "secret" };
        repo.Items.Add(item);

        var result = await service.DeleteItemAsync(attackerId, item.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenTargetIsSelf()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("owner@example.com", "wrapped", null, false), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenTargetHasNoVault()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await CreateUserAsync(users, "novault@example.com");

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("novault@example.com", "wrapped", null, false), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenOwnerHasNoVault()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, holder.Id);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenItemScoped_AndItemNotYetMigratedToPerItemKey()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var legacyItem = new VaultItem { Id = Guid.NewGuid(), OwnerUserId = owner.Id, CiphertextBlob = "secret", WrappedItemKey = null };
        repo.Items.Add(legacyItem);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", legacyItem.Id, false), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_ReplacesExistingActiveShare()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);

        var first = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped-v1", null, false), CancellationToken.None);
        var second = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped-v2", null, false), CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var activeShares = repo.Shares.Where(s => s.VaultOwnerUserId == owner.Id && s.HolderUserId == holder.Id && s.RevokedAt is null);
        Assert.Single(activeShares);
        Assert.Equal("wrapped-v2", activeShares.Single().WrappedKey);
    }

    [Fact]
    public async Task RevokeShareAsync_Fails_WhenCallerIsNotTheGrantingOwner()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        var attacker = await CreateUserAsync(users, "attacker@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);

        var result = await service.RevokeShareAsync(attacker.Id, share.Data!.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Shares.Single(s => s.Id == share.Data.Id).RevokedAt);
    }

    [Fact]
    public async Task RevokeShareAsync_Fails_ForSelfShareRow()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        var selfShare = repo.Shares.Single(s => s.VaultOwnerUserId == owner.Id && s.HolderUserId == owner.Id);

        var result = await service.RevokeShareAsync(owner.Id, selfShare.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(selfShare.RevokedAt);
    }

    [Fact]
    public async Task GetSharedItemsAsync_Fails_AfterRevocation()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com");
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var beforeRevoke = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);
        await service.RevokeShareAsync(owner.Id, share.Id, CancellationToken.None);
        var afterRevoke = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.True(beforeRevoke.Succeeded);
        Assert.Single(beforeRevoke.Data!);
        Assert.False(afterRevoke.Succeeded);
    }

    [Fact]
    public async Task RewrapMasterKeyAsync_Fails_WhenNoVault()
    {
        var (_, _, service, _) = CreateService();

        var result = await service.RewrapMasterKeyAsync(Guid.NewGuid(), new VaultRewrapDto("newSalt", 700_000, "newWrapped", null), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RewrapMasterKeyAsync_UpdatesProfile_WhenVaultExists()
    {
        var (repo, _, service, _) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var result = await service.RewrapMasterKeyAsync(userId, new VaultRewrapDto("newSalt", 700_000, "newWrapped", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        var profile = repo.Profiles.Single(p => p.UserId == userId);
        Assert.Equal("newSalt", profile.KdfSalt);
        Assert.Equal(700_000, profile.KdfIterations);
        Assert.Equal("newWrapped", profile.PrivateKeyWrappedByMasterKey);
    }

    [Fact]
    public async Task RewrapMasterKeyAsync_LeavesMlKemSecretKeyUntouched_WhenNotProvided()
    {
        // A legacy (KeySchemeVersion 1) account's reset/recover flow never sends this field - make
        // sure a null here doesn't accidentally null out (or otherwise touch) a hybrid account's
        // already-stored ML-KEM secret key wrapping.
        var (repo, _, service, _) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);
        var originalMlKemSecret = repo.Profiles.Single(p => p.UserId == userId).MlKemSecretKeyWrappedByMasterKey;

        var result = await service.RewrapMasterKeyAsync(userId, new VaultRewrapDto("newSalt", 700_000, "newWrapped", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(originalMlKemSecret, repo.Profiles.Single(p => p.UserId == userId).MlKemSecretKeyWrappedByMasterKey);
    }

    [Fact]
    public async Task RewrapMasterKeyAsync_UpdatesMlKemSecretKey_WhenProvided()
    {
        var (repo, _, service, _) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var result = await service.RewrapMasterKeyAsync(
            userId, new VaultRewrapDto("newSalt", 700_000, "newWrapped", "newMlKemSecretMaster"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("newMlKemSecretMaster", repo.Profiles.Single(p => p.UserId == userId).MlKemSecretKeyWrappedByMasterKey);
    }

    [Fact]
    public async Task GetSharedItemsAsync_Fails_WhenShareNotYetVerified()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);

        var result = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetSharedItemsAsync_Fails_WhenInviteAccepted_ButSessionAccessNotYetVerified()
    {
        // Accepting the invite (VerifyShareAsync) only proves the holder owns that inbox once - it
        // must not by itself grant standing access; every viewing session needs its own passed
        // challenge via RequestSharedVaultAccessAsync/VerifySharedVaultAccessAsync.
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com");

        var result = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetSharedWithMeAsync_HidesWrappedUvk_UntilVerified()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);

        var result = await service.GetSharedWithMeAsync(holder.Id, CancellationToken.None);

        var dto = Assert.Single(result.Data!);
        Assert.Null(dto.WrappedUvk);
        Assert.Equal(VaultShareStatus.Pending, dto.Status);
    }

    [Fact]
    public async Task VerifyShareAsync_GrantsAccess_WithCorrectCode()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com");
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var items = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);
        var status = (await service.GetSharesAsync(owner.Id, CancellationToken.None)).Data!.Single(s => s.Id == share.Id).Status;

        Assert.True(items.Succeeded);
        Assert.Single(items.Data!);
        Assert.Equal(VaultShareStatus.Active, status);
    }

    [Fact]
    public async Task VerifyShareAsync_Fails_WithWrongCode()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);

        var result = await service.VerifyShareAsync(holder.Id, share.Data!.Id, "000000", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task VerifyShareAsync_Fails_ForNonHolder()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        var attacker = await CreateUserAsync(users, "attacker@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);
        var sentCode = email.VaultShareCodesSent.Single().Code;

        var result = await service.VerifyShareAsync(attacker.Id, share.Data!.Id, sentCode, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task VerifyShareAsync_PermanentlyFails_AfterMaxAttempts()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);

        for (var i = 0; i < new VaultShareCodesOptions().MaxAttempts; i++)
        {
            await service.VerifyShareAsync(holder.Id, share.Data!.Id, "000000", CancellationToken.None);
        }

        var status = (await service.GetSharesAsync(owner.Id, CancellationToken.None)).Data!.Single(s => s.Id == share.Data!.Id).Status;
        Assert.Equal(VaultShareStatus.Failed, status);
    }

    [Fact]
    public async Task ResendShareCodeAsync_IssuesNewCode_AndResetsAttempts()
    {
        var (_, users, service, email) = CreateService(new VaultShareCodesOptions { ResendCooldownSeconds = 0 });
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped", null, false), CancellationToken.None);
        var firstCode = email.VaultShareCodesSent.Single().Code;
        await service.VerifyShareAsync(holder.Id, share.Data!.Id, "000000", CancellationToken.None);

        var resend = await service.ResendShareCodeAsync(holder.Id, share.Data!.Id, CancellationToken.None);
        var secondCode = email.VaultShareCodesSent.Last().Code;
        var verify = await service.VerifyShareAsync(holder.Id, share.Data!.Id, secondCode, CancellationToken.None);

        Assert.True(resend.Succeeded);
        Assert.NotEqual(firstCode, secondCode);
        Assert.True(verify.Succeeded);
    }

    [Fact]
    public async Task RequestSharedVaultAccessAsync_NeverRequiresCode_ForOwnVault()
    {
        var (_, _, service, _) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var result = await service.RequestSharedVaultAccessAsync(userId, userId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Data!.RequiresCode);
    }

    [Fact]
    public async Task VerifySharedVaultAccessAsync_Fails_WithWrongCode()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com");
        var challenge = await service.RequestSharedVaultAccessAsync(holder.Id, owner.Id, CancellationToken.None);

        var result = await service.VerifySharedVaultAccessAsync(holder.Id, challenge.Data!.ChallengeToken!, "000000", CancellationToken.None);
        var items = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(items.Succeeded);
    }

    [Fact]
    public async Task GetSharedItemAsync_Fails_ForItemScopedHolder_OnADifferentItem()
    {
        // The core promise of a per-item share: a holder scoped to one item must never be able to
        // reach a different item in the same owner's vault, even one they know the id of.
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var sharedItem = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("shared", "wrappedItemKey1"), CancellationToken.None);
        var otherItem = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("other", "wrappedItemKey2"), CancellationToken.None);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com", sharedItem.Data!.Id);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var allowed = await service.GetSharedItemAsync(holder.Id, owner.Id, sharedItem.Data!.Id, CancellationToken.None);
        var denied = await service.GetSharedItemAsync(holder.Id, owner.Id, otherItem.Data!.Id, CancellationToken.None);

        Assert.True(allowed.Succeeded);
        Assert.False(denied.Succeeded);
    }

    [Fact]
    public async Task GetSharedItemsAsync_Fails_ForItemScopedOnlyHolder()
    {
        // Whole-vault listing must stay unreachable via an item-scoped-only share - otherwise a
        // holder granted just one item could learn the existence/count of the owner's other items.
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var item = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com", item.Data!.Id);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var result = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateItemAsync_Succeeds_ForItemScopedHolder_WithCanEditAndSessionGrant()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var item = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com", item.Data!.Id, canEdit: true);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var result = await service.UpdateItemAsync(holder.Id, item.Data!.Id, new UpdateVaultItemRequest("updated", null), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpdateItemAsync_Fails_ForItemScopedHolder_WithoutCanEdit()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var item = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com", item.Data!.Id, canEdit: false);
        await GrantSessionAccessAsync(service, email, holder.Id, owner.Id);

        var result = await service.UpdateItemAsync(holder.Id, item.Data!.Id, new UpdateVaultItemRequest("updated", null), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateItemAsync_Fails_ForItemScopedHolder_WithCanEdit_ButNoSessionGrant()
    {
        // CanWriteItemAsync requires both an active+verified+CanEdit share AND a live session grant -
        // accepting the share invite alone must not be enough to write.
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var item = await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext", "wrappedItemKey"), CancellationToken.None);
        await CreateAndAcceptShareAsync(service, email, owner.Id, holder.Id, "holder@example.com", item.Data!.Id, canEdit: true);

        var result = await service.UpdateItemAsync(holder.Id, item.Data!.Id, new UpdateVaultItemRequest("updated", null), CancellationToken.None);

        Assert.False(result.Succeeded);
    }
}
