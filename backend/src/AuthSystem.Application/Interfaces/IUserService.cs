using AuthSystem.Application.Dtos;

namespace AuthSystem.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserProfileDto>> GetAllAsync(CancellationToken ct);
}
