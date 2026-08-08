using AuthSystem.Application.Dtos;
using AuthSystem.Application.Services;
using AuthSystem.Application.Tests.Fakes;
using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthSystem.Application.Tests;

/// <summary>
/// Covers the authorization-sensitive paths of VaultService specifically - not full coverage.
/// A bug here means one user's encrypted vault (bank credentials, etc.) leaking to another user,
/// so these are the cases that matter most: cross-user access, self-targeting, and revocation.
/// </summary>
public class VaultServiceTests
{
    private static (FakeVaultRepository Repo, UserManager<ApplicationUser> Users, VaultService Service) CreateService()
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
        return (repo, userManager, new VaultService(repo, userManager));
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
        var request = new VaultSetupRequest(publicKey, "salt", 600_000, "wrappedPriv", "wrappedPrivRecovery", "selfWrappedUvk");
        var result = await service.SetupAsync(userId, request, CancellationToken.None);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SetupAsync_Fails_WhenAlreadySetUp()
    {
        var (_, _, service) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var second = await service.SetupAsync(userId, new VaultSetupRequest("pub2", "salt2", 600_000, "a", "b", "c"), CancellationToken.None);

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task GetItemAsync_Fails_ForNonOwner()
    {
        var (repo, _, service) = CreateService();
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
        var (repo, _, service) = CreateService();
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
        var (repo, _, service) = CreateService();
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
        var (_, users, service) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("owner@example.com", "wrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenTargetHasNoVault()
    {
        var (_, users, service) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await CreateUserAsync(users, "novault@example.com");

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("novault@example.com", "wrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_Fails_WhenOwnerHasNoVault()
    {
        var (_, users, service) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, holder.Id);

        var result = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateShareAsync_ReplacesExistingActiveShare()
    {
        var (repo, users, service) = CreateService();
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
        var (repo, users, service) = CreateService();
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
        var (repo, users, service) = CreateService();
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
        var (_, users, service) = CreateService();
        var owner = await CreateUserAsync(users, "owner@example.com");
        var holder = await CreateUserAsync(users, "holder@example.com");
        await SetUpVaultAsync(service, owner.Id);
        await SetUpVaultAsync(service, holder.Id);
        var share = await service.CreateShareAsync(owner.Id, new CreateVaultShareRequest("holder@example.com", "wrapped"), CancellationToken.None);
        await service.CreateItemAsync(owner.Id, new CreateVaultItemRequest("ciphertext"), CancellationToken.None);

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
        var (_, _, service) = CreateService();

        var result = await service.RewrapMasterKeyAsync(Guid.NewGuid(), new VaultRewrapDto("newSalt", 700_000, "newWrapped"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RewrapMasterKeyAsync_UpdatesProfile_WhenVaultExists()
    {
        var (repo, _, service) = CreateService();
        var userId = Guid.NewGuid();
        await SetUpVaultAsync(service, userId);

        var result = await service.RewrapMasterKeyAsync(userId, new VaultRewrapDto("newSalt", 700_000, "newWrapped"), CancellationToken.None);

        Assert.True(result.Succeeded);
        var profile = repo.Profiles.Single(p => p.UserId == userId);
        Assert.Equal("newSalt", profile.KdfSalt);
        Assert.Equal(700_000, profile.KdfIterations);
        Assert.Equal("newWrapped", profile.PrivateKeyWrappedByMasterKey);
    }
}
