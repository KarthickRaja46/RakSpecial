namespace ChequeWriter;

/// <summary>Program name, project links and support contacts. Change them here only.</summary>
public static class AppInfo
{
    public const string Name = "RAK Special";
    public const string Author = "Karthick Raja";

    /// <summary>
    /// GitHub "owner/repository" that publishes the releases, used by the update check and the Help links.
    /// Leave empty to turn the update check off in this build.
    /// </summary>
    public const string GitHubRepo = "KarthickRaja46/RakSpecial";

    public const string SupportEmail = "karthickraja232205@gmail.com";

    /// <summary>Support phone numbers (also on WhatsApp). Display form, and digits only for wa.me links.</summary>
    public static readonly SupportPhone[] Phones =
    {
        new("UAE (Dubai)", "+971 50 256 7444", "971502567444"),
        new("India", "+91 63832 68445", "916383268445"),
    };

    public static string ProjectPage => $"https://github.com/{GitHubRepo}";
    public static string NewIssuePage => $"https://github.com/{GitHubRepo}/issues/new/choose";
}

public record SupportPhone(string Country, string Display, string Digits)
{
    public string WhatsAppLink(string message) => $"https://wa.me/{Digits}?text={Uri.EscapeDataString(message)}";
    public string CallLink => "tel:+" + Digits;
}
