namespace AuthSystem.Application.Options;

public class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Base URL used to build email confirmation / password reset links.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
