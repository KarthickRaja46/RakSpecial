using System.Text.Json;

namespace ChequeWriter.Core;

/// <summary>A published GitHub release. Links are always on https://github.com/.</summary>
public record ReleaseInfo(Version Version, string Tag, string PageUrl, string? SetupUrl, string Notes);

/// <summary>Reads GitHub's "latest release" answer and compares versions. No network code here.</summary>
public static class UpdateCheck
{
    public static string LatestReleaseApi(string repo) => $"https://api.github.com/repos/{repo}/releases/latest";

    /// <summary>"v2.1.0", "2.1", "2.1.0-beta" or "2.1.0+abc" → 2.1.0. Null when it is not a version.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().TrimStart('v', 'V');
        var cut = t.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) t = t[..cut];
        if (!t.Contains('.')) t += ".0";
        return Version.TryParse(t, out var v) ? Normalize(v) : null;
    }

    /// <summary>Keeps major.minor.patch, so 2.0.0.0 (the exe's version) equals 2.0.0 (the tag).</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    /// <summary>
    /// Reads the JSON of GET /repos/{repo}/releases/latest. Returns null for drafts, pre-releases,
    /// unreadable versions or links that are not on github.com.
    /// </summary>
    public static ReleaseInfo? ParseLatestRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (Bool(root, "draft") || Bool(root, "prerelease")) return null;

        var tag = Str(root, "tag_name");
        var version = ParseVersion(tag);
        var page = Str(root, "html_url");
        if (version == null || !IsGitHub(page)) return null;

        string? setup = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = Str(a, "name");
                var url = Str(a, "browser_download_url");
                if (name.StartsWith("RAKSpecial-Setup", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsGitHub(url))
                {
                    setup = url;
                    break;
                }
            }
        }
        return new ReleaseInfo(version, tag, page, setup, Str(root, "body"));
    }

    static bool IsGitHub(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com";

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
