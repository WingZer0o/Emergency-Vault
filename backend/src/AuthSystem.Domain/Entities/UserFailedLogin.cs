namespace AuthSystem.Domain.Entities
{
    public class UserFailedLogin
    {
        public Guid Id { get; set; }

        /// <summary>Null when the attempt was for an email that doesn't match any account.</summary>
        public Guid? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public string Email { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? AttemptedByIp { get; set; }
    }
}