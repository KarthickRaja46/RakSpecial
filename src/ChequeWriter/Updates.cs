using System.Diagnostics;
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

    static readonly HttpClient Downloader = new() { Timeout = TimeSpan.FromMinutes(15) };

    /// <summary>
    /// Downloads the release's setup file to %TEMP%\RAK Special update and returns its path.
    /// The link comes from github.com (checked in <see cref="UpdateCheck.ParseLatestRelease"/>).
    /// </summary>
    public static async Task<string> DownloadSetupAsync(ReleaseInfo r, IProgress<(long Done, long? Total)> progress, CancellationToken ct)
    {
        var url = r.SetupUrl ?? throw new InvalidOperationException("This release has no setup file.");
        var dir = Path.Combine(Path.GetTempPath(), "RAK Special update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"RAKSpecial-Setup-{r.Version}.exe");
        var part = file + ".part";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("RAKSpecial", Current.ToString()));
        using var resp = await Downloader.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(part))
        {
            var buffer = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                done += n;
                progress.Report((done, total));
            }
        }

        // a real setup is a Windows program ("MZ" header) of several MB, and complete
        var info = new FileInfo(part);
        var head = new byte[2];
        await using (var fs = File.OpenRead(part)) _ = await fs.ReadAsync(head, ct);
        if (head[0] != 'M' || head[1] != 'Z' || info.Length < 1_000_000 || (total is long t && info.Length != t))
        {
            File.Delete(part);
            throw new InvalidDataException("The downloaded file is not a complete RAK Special setup.");
        }
        File.Move(part, file, overwrite: true);
        return file;
    }

    /// <summary>Starts the setup in silent mode (it shows only a progress window) and closes RAK Special.
    /// The setup reopens RAK Special when it has finished.</summary>
    public static void InstallAndExit(string setupFile)
    {
        Process.Start(new ProcessStartInfo(setupFile, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });
        Application.Exit();
    }
}

public enum UpdateChoice { Later, Skip, Install }

/// <summary>"Update available" window: what's new, then download and install in one click.</summary>
public class UpdateDialog : Form
{
    readonly ReleaseInfo _release;
    readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 8, Visible = false, Style = ProgressBarStyle.Continuous };
    readonly Label _status = new() { Dock = DockStyle.Top, Height = 22, ForeColor = Theme.Muted, Visible = false };
    readonly RoundButton _update = new() { Text = "Update now", Glyph = "", Kind = ButtonKind.Primary, Font = Theme.Semibold(10.5f) };
    readonly RoundButton _later = new() { Text = "Later" };
    readonly LinkLabel _skip = new() { Text = "Skip this version", AutoSize = true, LinkColor = Theme.Muted };
    CancellationTokenSource? _cts;

    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;
    public string SetupFile { get; private set; } = "";

    public UpdateDialog(ReleaseInfo release)
    {
        _release = release;
        Text = $"{AppInfo.Name} update";
        Font = Theme.Ui(9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Card;
        ClientSize = new Size(S(520), S(430));
        AppIcon.Apply(this);

        // header
        var header = new Panel { Dock = DockStyle.Top, Height = S(92), BackColor = Theme.Navy };
        using (var ico = new Icon(AppIcon.Get(), 56, 56))
            header.Controls.Add(new PictureBox { Image = ico.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(S(20), S(18), S(56), S(56)) });
        header.Controls.Add(new Label { Text = "Update available", AutoSize = true, ForeColor = Theme.Gold, Font = Theme.Ui(16f, FontStyle.Bold), Location = new Point(S(90), S(16)) });
        header.Controls.Add(new Label
        {
            Text = $"{AppInfo.Name} {release.Version} is ready  ·  you have {Updates.Current}", AutoSize = true,
            ForeColor = Color.White, Font = Theme.Ui(10f), Location = new Point(S(92), S(54)),
        });

        // body
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(22), S(14), S(22), S(8)), BackColor = Theme.Card };
        var notes = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Theme.Canvas, ForeColor = Theme.Text, Font = Theme.Ui(9.5f), Text = CleanNotes(release.Notes), TabStop = false,
        };
        var notesFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(10)), BackColor = Theme.Canvas };
        notesFrame.Controls.Add(notes);
        var whatsNew = new Label { Text = "WHAT'S NEW", Dock = DockStyle.Top, Height = S(22), ForeColor = Theme.Muted, Font = Theme.Semibold(8f) };
        var info = new Label
        {
            Dock = DockStyle.Bottom, Height = S(56), ForeColor = Theme.Muted, Padding = new Padding(0, S(8), 0, 0),
            Text = "Update now downloads the new version, installs it and reopens RAK Special. Your cheques, payees and settings are kept.",
        };
        body.Controls.Add(notesFrame);
        body.Controls.Add(whatsNew);
        body.Controls.Add(_status);
        body.Controls.Add(_progress);
        body.Controls.Add(info);

        // buttons
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(64), FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(S(18), S(10), S(18), S(10)), BackColor = Theme.Card };
        _update.Size = new Size(S(160), S(42));
        _later.Size = new Size(S(100), S(42));
        _update.Margin = new Padding(S(8), 0, 0, 0);
        _later.Margin = new Padding(S(8), 0, 0, 0);
        _skip.Margin = new Padding(0, S(13), S(4), 0);
        var github = new LinkLabel { Text = "Release page", AutoSize = true, LinkColor = Theme.Muted, Margin = new Padding(0, S(13), S(16), 0) };
        github.LinkClicked += (_, _) => Updates.Open(release.PageUrl);
        bar.Controls.Add(_update);
        bar.Controls.Add(_later);
        bar.Controls.Add(_skip);
        bar.Controls.Add(github);

        _update.Click += async (_, _) => await UpdateNow();
        _later.Click += (_, _) => { _cts?.Cancel(); Choice = UpdateChoice.Later; Close(); };
        _skip.LinkClicked += (_, _) => { Choice = UpdateChoice.Skip; Close(); };
        FormClosing += (_, _) => _cts?.Cancel();
        AcceptButton = _update;
        Shown += (_, _) => { ActiveControl = _update; notes.Select(0, 0); };

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(bar);
    }

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    async Task UpdateNow()
    {
        if (_release.SetupUrl == null)
        {
            Updates.Open(_release.PageUrl);
            Close();
            return;
        }
        _update.Enabled = false;
        _skip.Enabled = false;
        _later.Text = "Cancel";
        _progress.Visible = _status.Visible = true;
        _status.Text = "Downloading...";
        _cts = new CancellationTokenSource();
        var progress = new Progress<(long Done, long? Total)>(p =>
        {
            if (p.Total is long t && t > 0) _progress.Value = (int)Math.Min(100, p.Done * 100 / t);
            _status.Text = $"Downloading...  {p.Done / 1048576.0:0.0} MB" + (p.Total is long tt ? $" of {tt / 1048576.0:0.0} MB" : "");
        });
        try
        {
            SetupFile = await Updates.DownloadSetupAsync(_release, progress, _cts.Token);
            _status.Text = "Installing... RAK Special will reopen by itself.";
            Choice = UpdateChoice.Install;
            Close();
        }
        catch (OperationCanceledException) { /* Cancel pressed or window closed */ }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            _progress.Visible = _status.Visible = false;
            _update.Enabled = _skip.Enabled = true;
            _later.Text = "Later";
            if (MessageBox.Show(this, "The update could not be downloaded:\n" + ex.Message +
                    "\n\nOpen the download in the web browser instead?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Updates.Open(_release.SetupUrl ?? _release.PageUrl);
        }
    }

    /// <summary>GitHub release notes without Markdown symbols.</summary>
    static string CleanNotes(string notes)
    {
        const string fallback = "Improvements and fixes.\r\n\r\nClick 'Release page' below for the details.";
        if (string.IsNullOrWhiteSpace(notes)) return fallback;
        var lines = notes.Replace("\r", "").Split('\n')
            .Select(l => l.Trim().TrimStart('#').Trim().Replace("**", "").Replace("`", ""))
            .Where(l => !l.StartsWith("What's Changed", StringComparison.OrdinalIgnoreCase) &&
                        !l.StartsWith("Full Changelog", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.StartsWith("* ") || l.StartsWith("- ") ? "•  " + l[2..] : l)
            .Select(l => l.Replace("*", ""));
        var text = string.Join(Environment.NewLine, lines).Trim();
        return text.Length > 0 ? text : fallback;   // GitHub's automatic notes can be only a "Full Changelog" link
    }
}

/// <summary>Yellow bar at the top of the main window: "RAK Special 2.1.0 is available".</summary>
public class UpdateBar : Panel
{
    public UpdateBar(ReleaseInfo r, Action update, Action skip)
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
        flow.Controls.Add(Link("Update now", update));
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
