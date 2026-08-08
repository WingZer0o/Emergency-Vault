using Microsoft.AspNetCore.Identity;

namespace AuthSystem.Domain.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
