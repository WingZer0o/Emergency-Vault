using AuthSystem.Application.Dtos;
using AuthSystem.Application.Options;
using AuthSystem.Application.Services;
using AuthSystem.Application.Tests.Fakes;
using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthSystem.Application.Tests;

/// <summary>
/// Covers the authorization-sensitive paths of VaultService specifically - not full coverage.
/// A bug here means one user's encrypted vault (bank credentials, etc.) leaking to another user,
/// so these are the cases that matter most: cross-user access, self-targeting, revocation, and
/// (since sharing added an email verification gate) whether an unverified share is truly inert.
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
            repo, userManager, new FakeHotpService(), email,
            Microsoft.Extensions.Options.Options.Create(shareCodesOptions ?? new VaultShareCodesOptions()));
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

        var result = await service.UpdateItemAsync(attackerId, item.Id, new UpdateVaultItemRequest("tampered"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("secret", item.CiphertextBlob);
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

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("owner@example.com", "wrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenTargetHasNoVault()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await CreateUserAsync(users, "novault@example.com");

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("novault@example.com", "wrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenOwnerHasNoVault()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, holder.Id);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

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

        var first = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped-v1"), CancellationToken.None);
        var second = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped-v2"), CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var activeShares = repo.Shares.Where(s => s.VaultOwnerUserId == owner.Id && s.HolderUserId == holder.Id && s.RevokedAt is null);
        Assert.Single(activeShares);
        Assert.Equal("wrapped-v2", activeShares.Single().WrappedUvk);
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
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

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
    public async Task ReleaseHeldSharesAsync_RemovesEveryShareHeldByUser_IncludingSelfShare()
    {
        var (repo, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

        await service.ReleaseHeldSharesAsync(holder.Id, CancellationToken.None);

        // Every row where holder is the Holder - their own self-share and the cross-user grant - is gone.
        Assert.DoesNotContain(repo.Shares, s => s.HolderUserId == holder.Id);
        // Rows where holder is only the VaultOwner (irrelevant here) or another user's self-share are untouched.
        Assert.Contains(repo.Shares, s => s.VaultOwnerUserId == owner.Id && s.HolderUserId == owner.Id);
    }

    [Fact]
    public async Task GetSharedItemsAsync_Fails_AfterRevocation()
    {
        var (_, users, service, email) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext"), CancellationToken.None);
        var sentCode = email.VaultShareCodesSent.Single(c => c.ToEmail == "holder@example.com").Code;
        await service.VerifyShareAsync(holder.Id, share.Data!.Id, sentCode, CancellationToken.None);

        var beforeRevoke = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);
        await service.RevokeShareAsync(owner.Id, share.Data!.Id, CancellationToken.None);
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
        await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext"), CancellationToken.None);

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
        await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

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
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext"), CancellationToken.None);
        var sentCode = email.VaultShareCodesSent.Single().Code;

        var verify = await service.VerifyShareAsync(holder.Id, share.Data!.Id, sentCode, CancellationToken.None);
        var items = await service.GetSharedItemsAsync(holder.Id, owner.Id, CancellationToken.None);

        Assert.True(verify.Succeeded);
        Assert.True(items.Succeeded);
        Assert.Single(items.Data!);
    }

    [Fact]
    public async Task VerifyShareAsync_Fails_WithWrongCode()
    {
        var (_, users, service, _) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

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
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
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
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

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
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
        var firstCode = email.VaultShareCodesSent.Single().Code;
        await service.VerifyShareAsync(holder.Id, share.Data!.Id, "000000", CancellationToken.None);

        var resend = await service.ResendShareCodeAsync(holder.Id, share.Data!.Id, CancellationToken.None);
        var secondCode = email.VaultShareCodesSent.Last().Code;
        var verify = await service.VerifyShareAsync(holder.Id, share.Data!.Id, secondCode, CancellationToken.None);

        Assert.True(resend.Succeeded);
        Assert.NotEqual(firstCode, secondCode);
        Assert.True(verify.Succeeded);
    }
}
