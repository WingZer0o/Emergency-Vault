namespace EmergencyVault.Infrastructure.Email
{
    public class EmailRequestBody
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string From { get; set; }
        public List<string> To { get; set; }
        public string Subject { get; set; }
        public string Html { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}