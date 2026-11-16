using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class AnalyticsTests
{
    static readonly DateTime Today = new(2026, 11, 16);

    static ChequeRecord C(string payee, decimal amount, DateTime date, ChequeStatus status = ChequeStatus.Printed, string bank = "b1") =>
        new() { Payee = payee, Amount = amount, ChequeDate = date, Status = status, BankId = bank, PrintedAt = date };

    [Fact]
    public void Periods_cover_whole_months()
    {
        Assert.Equal((new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)), ToTuple(Analytics.Range(AnalyticsPeriod.ThisMonth, Today)));
        Assert.Equal((new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)), ToTuple(Analytics.Range(AnalyticsPeriod.LastMonth, Today)));
        Assert.Equal((new DateTime(2025, 12, 1), new DateTime(2026, 11, 30)), ToTuple(Analytics.Range(AnalyticsPeriod.Last12Months, Today)));
        Assert.Equal((new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), ToTuple(Analytics.Range(AnalyticsPeriod.LastYear, Today)));
        Assert.Equal((null, null), Analytics.Range(AnalyticsPeriod.AllTime, Today));
    }

    static (DateTime, DateTime) ToTuple((DateTime? From, DateTime? To) r) => (r.From!.Value, r.To!.Value);

    [Fact]
    public void Summary_counts_only_paid_cheques_in_totals()
    {
        var rows = new[]
        {
            C("A", 1000, new DateTime(2026, 11, 1), ChequeStatus.Cleared),
            C("A", 500, new DateTime(2026, 11, 10)),                       // printed, due, not cleared
            C("B", 2000, new DateTime(2026, 11, 20)),                      // post-dated: not "awaiting clearing"
            C("B", 700, new DateTime(2026, 11, 5), ChequeStatus.Bounced),
            C("C", 300, new DateTime(2026, 11, 6), ChequeStatus.Stopped),
            C("C", 999, new DateTime(2026, 11, 7), ChequeStatus.Void),
        };
        var s = Analytics.Summarize(rows, Today);
        Assert.Equal(3, s.Cheques);
        Assert.Equal(3500m, s.Total);
        Assert.Equal(1166.67m, s.Average);
        Assert.Equal(2000m, s.Largest);
        Assert.Equal((1, 700m), (s.Bounced, s.BouncedAmount));
        Assert.Equal((1, 300m), (s.Stopped, s.StoppedAmount));
        Assert.Equal(1, s.Void);
        Assert.Equal((1, 500m), (s.Uncleared, s.UnclearedAmount));
    }

    [Fact]
    public void ByMonth_fills_empty_months_and_skips_unpaid()
    {
        var rows = new[]
        {
            C("A", 100, new DateTime(2026, 1, 5)),
            C("A", 50, new DateTime(2026, 1, 25)),
            C("A", 999, new DateTime(2026, 2, 1), ChequeStatus.Bounced),
            C("A", 200, new DateTime(2026, 3, 31)),
        };
        var m = Analytics.ByMonth(rows);
        Assert.Equal(new[] { "Jan 2026", "Feb 2026", "Mar 2026" }, m.Select(x => x.Label));
        Assert.Equal(new[] { 150m, 0m, 200m }, m.Select(x => x.Total));
        Assert.Equal(new[] { 2, 0, 1 }, m.Select(x => x.Count));
    }

    [Fact]
    public void ByMonth_uses_the_period_bounds()
    {
        var m = Analytics.ByMonth(new[] { C("A", 100, new DateTime(2026, 5, 5)) }, new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        Assert.Equal(new[] { 0m, 100m, 0m }, m.Select(x => x.Total));
        Assert.Empty(Analytics.ByMonth(Array.Empty<ChequeRecord>()));
    }

    [Fact]
    public void ByPayee_groups_names_ignoring_case_and_spaces()
    {
        var rows = new[]
        {
            C("acme  trading", 100, new DateTime(2026, 1, 1)),
            C("ACME TRADING", 300, new DateTime(2026, 2, 1)),
            C(" Acme Trading LLC", 50, new DateTime(2026, 2, 2)),
            C("Landlord", 1200, new DateTime(2026, 1, 1)),
            C("Landlord", 1200, new DateTime(2026, 2, 1), ChequeStatus.Bounced),
        };
        var p = Analytics.ByPayee(rows);
        Assert.Equal(new[] { "Landlord", "ACME TRADING", "Acme Trading LLC" }, p.Select(x => x.Payee));
        var acme = p[1];
        Assert.Equal((2, 400m, 200m, 300m), (acme.Count, acme.Total, acme.Average, acme.Largest));
        Assert.Equal(400m / 1650m, acme.Share);
        Assert.Equal(1, p[0].Bounced);
        Assert.Equal(1200m, p[0].Total);
    }

    [Fact]
    public void Upcoming_lists_printed_post_dated_cheques_soonest_first()
    {
        var rows = new[]
        {
            C("A", 1, Today),                                               // due today: not upcoming
            C("A", 2, Today.AddDays(40)),
            C("A", 3, Today.AddDays(5)),
            C("A", 4, Today.AddDays(6), ChequeStatus.Void),
        };
        Assert.Equal(new[] { 3m, 2m }, Analytics.Upcoming(rows, Today).Select(r => r.Amount));
        Assert.Equal(new[] { 3m }, Analytics.Upcoming(rows, Today, 30).Select(r => r.Amount));
    }

    [Fact]
    public void Filter_by_dates_and_book()
    {
        var rows = new[] { C("A", 1, new DateTime(2026, 1, 1)), C("A", 2, new DateTime(2026, 2, 1), bank: "b2"), C("A", 3, new DateTime(2026, 3, 1)) };
        Assert.Equal(new[] { 2m, 3m }, Analytics.Filter(rows, new DateTime(2026, 2, 1), null).Select(r => r.Amount));
        Assert.Equal(new[] { 1m, 3m }, Analytics.Filter(rows, null, null, "b1").Select(r => r.Amount));
    }

    [Fact]
    public void Statuses_saved_by_name_still_load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rs-status-{Guid.NewGuid():N}.json");
        try
        {
            var store = new DataStore(path);
            store.Data.Cheques.Add(C("A", 1, Today, ChequeStatus.Bounced));
            store.Save();
            Assert.Contains("\"Bounced\"", File.ReadAllText(path));
            Assert.Equal(ChequeStatus.Bounced, new DataStore(path).Data.Cheques[0].Status);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
        }
    }
}

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("2.1", "2.1.0")]
    [InlineData("V3", "3.0.0")]
    [InlineData("2.1.0-beta", "2.1.0")]
    [InlineData("2.0.0+abc123", "2.0.0")]
    public void Parses_versions(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateCheck.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void Rejects_non_versions(string? tag) => Assert.Null(UpdateCheck.ParseVersion(tag));

    [Fact]
    public void Compares_exe_version_with_tag()
    {
        Assert.False(UpdateCheck.IsNewer(new Version(2, 0, 0), new Version(2, 0, 0, 0)));
        Assert.True(UpdateCheck.IsNewer(new Version(2, 0, 1), new Version(2, 0, 0, 0)));
        Assert.False(UpdateCheck.IsNewer(new Version(1, 9, 9), new Version(2, 0, 0)));
    }

    const string Release = """
        {
          "tag_name": "v2.1.0", "html_url": "https://github.com/owner/rak-special/releases/tag/v2.1.0",
          "draft": false, "prerelease": false, "body": "Analytics",
          "assets": [
            { "name": "RAKSpecial-Portable-2.1.0.zip", "browser_download_url": "https://github.com/owner/rak-special/releases/download/v2.1.0/RAKSpecial-Portable-2.1.0.zip" },
            { "name": "RAKSpecial-Setup-2.1.0.exe", "browser_download_url": "https://github.com/owner/rak-special/releases/download/v2.1.0/RAKSpecial-Setup-2.1.0.exe" }
          ]
        }
        """;

    [Fact]
    public void Reads_release_and_finds_the_setup_file()
    {
        var r = UpdateCheck.ParseLatestRelease(Release)!;
        Assert.Equal(new Version(2, 1, 0), r.Version);
        Assert.EndsWith("/RAKSpecial-Setup-2.1.0.exe", r.SetupUrl);
        Assert.Equal("Analytics", r.Notes);
    }

    [Fact]
    public void Ignores_prereleases_and_links_outside_github()
    {
        Assert.Null(UpdateCheck.ParseLatestRelease(Release.Replace("\"prerelease\": false", "\"prerelease\": true")));
        Assert.Null(UpdateCheck.ParseLatestRelease(Release.Replace("\"html_url\": \"https://github.com", "\"html_url\": \"https://evil.example")));
        var r = UpdateCheck.ParseLatestRelease(Release.Replace("download_url\": \"https://github.com/owner/rak-special/releases/download/v2.1.0/RAKSpecial-Setup",
                                                                  "download_url\": \"http://github.com/x/RAKSpecial-Setup"))!;
        Assert.Null(r.SetupUrl);
    }
}
