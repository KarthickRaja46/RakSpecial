using System.Net;
using System.Net.Http.Headers;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Asks GitHub for the latest published release of <see cref="AppInfo.GitHubRepo"/>.
/// Only a plain HTTPS GET of the release information; nothing about the user is sent.
/// </summary>
public static class Updates
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static bool Configured => AppInfo.GitHubRepo.Contains('/');

    public static Version Current =>
        UpdateCheck.Normalize(typeof(Updates).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static string ReleasesPage => $"https://github.com/{AppInfo.GitHubRepo}/releases";

    /// <summary>The latest release, or null when the repository has no published release yet. Throws on network errors.</summary>
    public static async Task<ReleaseInfo?> GetLatestAsync()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, UpdateCheck.LatestReleaseApi(AppInfo.GitHubRepo));
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("RAKSpecial", Current.ToString()));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var resp = await Http.SendAsync(req);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return UpdateCheck.ParseLatestRelease(await resp.Content.ReadAsStringAsync());
    }

    public static void Open(string url) => Support.Open(url);
}

/// <summary>Yellow bar at the top of the main window: "RAK Special 2.1.0 is available".</summary>
public class UpdateBar : Panel
{
    public UpdateBar(ReleaseInfo r, Action skip)
    {
        Dock = DockStyle.Top;
        Height = 34;
        BackColor = Color.FromArgb(255, 243, 205);
        Padding = new Padding(10, 0, 6, 0);

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = false };
        var text = new Label
        {
            AutoSize = true, Margin = new Padding(0, 8, 12, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Text = $"{AppInfo.Name} {r.Version} is available (you have {Updates.Current}).",
        };
        LinkLabel Link(string t, Action a)
        {
            var l = new LinkLabel { Text = t, AutoSize = true, Margin = new Padding(0, 8, 14, 0), Font = new Font("Segoe UI", 9.5f) };
            l.LinkClicked += (_, _) => a();
            return l;
        }
        flow.Controls.Add(text);
        flow.Controls.Add(Link("Download the setup", () => Updates.Open(r.SetupUrl ?? r.PageUrl)));
        flow.Controls.Add(Link("What's new", () => Updates.Open(r.PageUrl)));
        flow.Controls.Add(Link("Skip this version", () => { skip(); Dispose(); }));

        var close = new Button
        {
            Text = "✕", Dock = DockStyle.Right, Width = 30, FlatStyle = FlatStyle.Flat, TabStop = false,
            BackColor = BackColor, Cursor = Cursors.Hand,
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Dispose();
        new ToolTip().SetToolTip(close, "Hide until the next start");

        Controls.Add(flow);
        Controls.Add(close);
    }
}
