namespace AuthSystem.Domain.Entities
{
    public class UserFailedLogin
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? AttemptedByIp { get; set; }
    }
}