using EmergencyVault.Application.Dtos;

namespace EmergencyVault.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserProfileDto>> GetAllAsync(CancellationToken ct);
}
