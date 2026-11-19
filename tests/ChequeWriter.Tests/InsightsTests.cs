using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class InsightsTests
{
    static readonly DateTime Today = new(2026, 11, 16);

    static ChequeRecord C(string payee, decimal amount, DateTime date, ChequeStatus status = ChequeStatus.Printed) =>
        new() { Payee = payee, Amount = amount, ChequeDate = date, Status = status, BankId = "b1", PrintedAt = date };

    [Fact]
    public void Previous_range_of_whole_months_is_the_same_number_of_months()
    {
        Assert.Equal((new DateTime(2024, 12, 1), new DateTime(2025, 11, 30)),
            Analytics.PreviousRange(new DateTime(2025, 12, 1), new DateTime(2026, 11, 30)));
        Assert.Equal((new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)),
            Analytics.PreviousRange(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));
        Assert.Equal((new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)),
            Analytics.PreviousRange(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31)));
    }

    [Fact]
    public void Previous_range_of_odd_dates_is_the_same_number_of_days()
    {
        Assert.Equal((new DateTime(2026, 11, 1), new DateTime(2026, 11, 10)),
            Analytics.PreviousRange(new DateTime(2026, 11, 11), new DateTime(2026, 11, 20)));
    }

    [Fact]
    public void Change_is_null_without_a_previous_amount()
    {
        Assert.Null(Analytics.Change(100, 0));
        Assert.Equal(0.25m, Analytics.Change(125, 100));
        Assert.Equal(-0.5m, Analytics.Change(50, 100));
    }

    [Fact]
    public void Status_breakdown_splits_printed_into_due_and_post_dated()
    {
        var rows = new[]
        {
            C("A", 100, new DateTime(2026, 11, 1), ChequeStatus.Cleared),
            C("A", 200, new DateTime(2026, 11, 10)),
            C("B", 300, new DateTime(2026, 12, 1)),
            C("B", 400, new DateTime(2026, 11, 5), ChequeStatus.Bounced),
            C("C", 500, new DateTime(2026, 11, 6), ChequeStatus.Void),
            new ChequeRecord { Payee = "X", Amount = 999, ChequeDate = Today, ExcludedFromAnalytics = true },
        };
        var s = Analytics.ByStatus(rows, Today).ToDictionary(x => x.State);
        Assert.Equal(100m, s[ChequeState.Cleared].Amount);
        Assert.Equal(200m, s[ChequeState.AwaitingClearing].Amount);
        Assert.Equal(300m, s[ChequeState.PostDated].Amount);
        Assert.Equal(1, s[ChequeState.Bounced].Count);
        Assert.Equal(0, s[ChequeState.Stopped].Count);
        Assert.Equal(1, s[ChequeState.Void].Count);
    }

    [Fact]
    public void Insights_compare_with_the_period_before_and_name_the_biggest_cheque()
    {
        var rows = new[]
        {
            C("Landlord", 1000, new DateTime(2026, 10, 1), ChequeStatus.Cleared),
            C("Landlord", 1000, new DateTime(2026, 11, 1), ChequeStatus.Cleared),
            C("Supplier", 250, new DateTime(2026, 11, 3), ChequeStatus.Cleared),
        };
        var text = Analytics.Insights(rows, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), Today).Select(i => i.Text).ToList();
        Assert.Contains(text, t => t.Contains("25% more") && t.Contains("AED 1,000.00"));
        Assert.Contains(text, t => t.StartsWith("Biggest cheque: AED 1,000.00 to Landlord"));
        Assert.Contains(text, t => t.StartsWith("Landlord received 80%"));
    }

    [Fact]
    public void Insights_warn_about_old_uncleared_and_upcoming_cheques()
    {
        var rows = new[]
        {
            C("Old", 100, new DateTime(2026, 10, 20)),           // printed 27 days ago, not cleared
            C("Soon", 700, new DateTime(2026, 11, 25)),          // post-dated, due in 9 days
        };
        var warnings = Analytics.Insights(rows, null, null, Today).Where(i => i.Kind == InsightKind.Warning).Select(i => i.Text).ToList();
        Assert.Contains(warnings, t => t.StartsWith("1 cheque (AED 100.00) dated over 2 weeks ago is not marked Cleared"));
        Assert.Contains(warnings, t => t.StartsWith("AED 700.00 in post-dated cheques falls due") && t.Contains("Soon"));
    }

    [Fact]
    public void Insights_say_so_when_the_period_is_empty()
    {
        var list = Analytics.Insights(Array.Empty<ChequeRecord>(), new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), Today);
        Assert.Single(list);
        Assert.StartsWith("No cheques in this period", list[0].Text);
    }
}
