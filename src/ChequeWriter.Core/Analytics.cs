using System.Globalization;

namespace ChequeWriter.Core;

public enum AnalyticsPeriod { ThisMonth, LastMonth, Last3Months, Last12Months, ThisYear, LastYear, AllTime, Custom }

public record MonthTotal(int Year, int Month, int Count, decimal Total)
{
    public DateTime Start => new(Year, Month, 1);
    public string Label => Start.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    public string ShortLabel => Start.ToString("MMM yy", CultureInfo.InvariantCulture);
    public decimal Average => Count == 0 ? 0 : Math.Round(Total / Count, 2);
}

public record PayeeTotal(string Payee, int Count, decimal Total, decimal Share, decimal Average, decimal Largest,
                         DateTime LastDate, int Bounced);

public enum ChequeState { Cleared, AwaitingClearing, PostDated, Bounced, Stopped, Void }

public record StatusSlice(ChequeState State, int Count, decimal Amount);

public enum InsightKind { Info, Warning }

public record Insight(string Text, InsightKind Kind);

public record CategoryTotal(string Category, int Count, decimal Total, decimal Share, decimal Average);

public record AnalyticsSummary(int Cheques, decimal Total, decimal Average, decimal Largest,
                               int Void, int Bounced, decimal BouncedAmount, int Stopped, decimal StoppedAmount,
                               int Uncleared, decimal UnclearedAmount);

/// <summary>
/// Totals over the cheque register. Everything goes by the cheque date (when the money leaves),
/// and only cheques that count as paid (Printed or Cleared) are added to totals.
/// </summary>
public static class Analytics
{
    /// <summary>Calendar range for a period. Ranges cover whole months, so post-dated cheques in them count.</summary>
    public static (DateTime? From, DateTime? To) Range(AnalyticsPeriod p, DateTime today)
    {
        var month = new DateTime(today.Year, today.Month, 1);
        return p switch
        {
            AnalyticsPeriod.ThisMonth => (month, month.AddMonths(1).AddDays(-1)),
            AnalyticsPeriod.LastMonth => (month.AddMonths(-1), month.AddDays(-1)),
            AnalyticsPeriod.Last3Months => (month.AddMonths(-2), month.AddMonths(1).AddDays(-1)),
            AnalyticsPeriod.Last12Months => (month.AddMonths(-11), month.AddMonths(1).AddDays(-1)),
            AnalyticsPeriod.ThisYear => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            AnalyticsPeriod.LastYear => (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year - 1, 12, 31)),
            _ => (null, null),
        };
    }

    public static IEnumerable<ChequeRecord> Filter(IEnumerable<ChequeRecord> rows, DateTime? from, DateTime? to, string? bankId = null) =>
        rows.Where(r => (from == null || r.ChequeDate.Date >= from.Value.Date) &&
                        (to == null || r.ChequeDate.Date <= to.Value.Date) &&
                        (string.IsNullOrEmpty(bankId) || r.BankId == bankId));

    /// <summary>Cheques that take part in analytics: everything except those the user removed.</summary>
    public static IEnumerable<ChequeRecord> Counted(IEnumerable<ChequeRecord> rows) => rows.Where(r => !r.ExcludedFromAnalytics);

    public static AnalyticsSummary Summarize(IEnumerable<ChequeRecord> rows, DateTime today)
    {
        var all = Counted(rows).ToList();
        var paid = all.Where(r => r.Status.CountsAsPaid()).ToList();
        var total = paid.Sum(r => r.Amount);
        var bounced = all.Where(r => r.Status == ChequeStatus.Bounced).ToList();
        var stopped = all.Where(r => r.Status == ChequeStatus.Stopped).ToList();
        var uncleared = all.Where(r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date <= today.Date).ToList();
        return new AnalyticsSummary(
            paid.Count, total, paid.Count == 0 ? 0 : Math.Round(total / paid.Count, 2), paid.Count == 0 ? 0 : paid.Max(r => r.Amount),
            all.Count(r => r.Status == ChequeStatus.Void),
            bounced.Count, bounced.Sum(r => r.Amount), stopped.Count, stopped.Sum(r => r.Amount),
            uncleared.Count, uncleared.Sum(r => r.Amount));
    }

    /// <summary>
    /// Paid totals per month, oldest first, with empty months filled in. The range is from..to when given,
    /// otherwise the first and last cheque. At most 240 months.
    /// </summary>
    public static List<MonthTotal> ByMonth(IEnumerable<ChequeRecord> rows, DateTime? from = null, DateTime? to = null)
    {
        var paid = Counted(rows).Where(r => r.Status.CountsAsPaid()).ToList();
        if (paid.Count == 0 && (from == null || to == null)) return new();
        var first = from ?? paid.Min(r => r.ChequeDate);
        var last = to ?? paid.Max(r => r.ChequeDate);
        var byKey = paid.GroupBy(r => (r.ChequeDate.Year, r.ChequeDate.Month))
                        .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(r => r.Amount)));
        var list = new List<MonthTotal>();
        for (var m = new DateTime(first.Year, first.Month, 1); m <= last && list.Count < 240; m = m.AddMonths(1))
        {
            byKey.TryGetValue((m.Year, m.Month), out var v);
            list.Add(new MonthTotal(m.Year, m.Month, v.Count, v.Total));
        }
        return list;
    }

    /// <summary>
    /// Paid totals per payee, largest first. Names are matched ignoring case and extra spaces;
    /// the spelling on the newest cheque is shown.
    /// </summary>
    public static List<PayeeTotal> ByPayee(IEnumerable<ChequeRecord> rows)
    {
        var all = Counted(rows).ToList();
        var grandTotal = all.Where(r => r.Status.CountsAsPaid()).Sum(r => r.Amount);
        return all.GroupBy(r => PayeeKey(r.Payee))
            .Select(g =>
            {
                var paid = g.Where(r => r.Status.CountsAsPaid()).ToList();
                var total = paid.Sum(r => r.Amount);
                var newest = g.OrderByDescending(r => r.ChequeDate).ThenByDescending(r => r.PrintedAt).First();
                return new PayeeTotal(
                    newest.Payee.Trim(), paid.Count, total, grandTotal == 0 ? 0 : total / grandTotal,
                    paid.Count == 0 ? 0 : Math.Round(total / paid.Count, 2), paid.Count == 0 ? 0 : paid.Max(r => r.Amount),
                    newest.ChequeDate, g.Count(r => r.Status == ChequeStatus.Bounced));
            })
            .Where(p => p.Count > 0 || p.Bounced > 0)
            .OrderByDescending(p => p.Total).ThenBy(p => p.Payee, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool SamePayee(string a, string b) => PayeeKey(a) == PayeeKey(b);

    static string PayeeKey(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>Post-dated cheques still to be paid: Printed, dated after today (and within <paramref name="days"/>), soonest first.</summary>
    public static List<ChequeRecord> Upcoming(IEnumerable<ChequeRecord> rows, DateTime today, int? days = null) =>
        Counted(rows).Where(r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date > today.Date &&
                        (days == null || r.ChequeDate.Date <= today.Date.AddDays(days.Value)))
            .OrderBy(r => r.ChequeDate).ThenBy(r => r.ChequeNo, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The period just before from..to, of the same length: whole months when the range is whole months
    /// (e.g. the 12 months before "last 12 months"), otherwise the same number of days.
    /// </summary>
    public static (DateTime From, DateTime To) PreviousRange(DateTime from, DateTime to)
    {
        from = from.Date; to = to.Date;
        if (from.Day == 1 && to.AddDays(1).Day == 1)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
            return (from.AddMonths(-months), from.AddDays(-1));
        }
        var days = (to - from).Days + 1;
        return (from.AddDays(-days), from.AddDays(-1));
    }

    /// <summary>Change from <paramref name="before"/> to <paramref name="now"/> as a fraction (0.25 = 25% more), or null when there is nothing to compare with.</summary>
    public static decimal? Change(decimal now, decimal before) => before == 0 ? null : (now - before) / before;

    /// <summary>
    /// Where the cheques stand: cleared, waiting to clear (due), post-dated (not due yet), bounced, stopped, void.
    /// Removed cheques are left out.
    /// </summary>
    public static List<StatusSlice> ByStatus(IEnumerable<ChequeRecord> rows, DateTime today)
    {
        var all = Counted(rows).ToList();
        StatusSlice S(ChequeState st, Func<ChequeRecord, bool> f)
        {
            var g = all.Where(f).ToList();
            return new StatusSlice(st, g.Count, g.Sum(r => r.Amount));
        }
        return new()
        {
            S(ChequeState.Cleared, r => r.Status == ChequeStatus.Cleared),
            S(ChequeState.AwaitingClearing, r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date <= today.Date),
            S(ChequeState.PostDated, r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date > today.Date),
            S(ChequeState.Bounced, r => r.Status == ChequeStatus.Bounced),
            S(ChequeState.Stopped, r => r.Status == ChequeStatus.Stopped),
            S(ChequeState.Void, r => r.Status == ChequeStatus.Void),
        };
    }

    /// <summary>
    /// Short plain-language findings for the "Good to know" panel. <paramref name="scope"/> is every cheque that
    /// passes the cheque book / category filters (any date); from..to is the period (null = all time).
    /// </summary>
    public static List<Insight> Insights(IEnumerable<ChequeRecord> scope, DateTime? from, DateTime? to, DateTime today)
    {
        var all = Counted(scope).ToList();
        var period = Filter(all, from, to).ToList();
        var paid = period.Where(r => r.Status.CountsAsPaid()).ToList();
        var list = new List<Insight>();
        if (period.Count == 0)
            list.Add(new Insight("No cheques in this period. Try a longer period, or All time.", InsightKind.Info));

        var total = paid.Sum(r => r.Amount);
        if (from != null && to != null && period.Count > 0)
        {
            var (pf, pt) = PreviousRange(from.Value, to.Value);
            var before = Filter(all, pf, pt).Where(r => r.Status.CountsAsPaid()).Sum(r => r.Amount);
            if (Change(total, before) is { } ch && Math.Abs(ch) >= 0.005m)
                list.Add(new Insight(
                    $"You paid {Aed(total)}, {Math.Abs(ch):0%} {(ch > 0 ? "more" : "less")} than in the period before ({Aed(before)}).",
                    InsightKind.Info));
            else if (before > 0)
                list.Add(new Insight($"You paid {Aed(total)}, about the same as in the period before.", InsightKind.Info));
        }

        if (paid.Count > 0)
        {
            var big = paid.OrderByDescending(r => r.Amount).ThenBy(r => r.ChequeDate).First();
            list.Add(new Insight($"Biggest cheque: {Aed(big.Amount)} to {big.Payee.Trim()} on {big.ChequeDate:dd MMM yyyy}.", InsightKind.Info));

            var payees = ByPayee(period).Where(p => p.Total > 0).ToList();
            if (payees.Count > 1)
            {
                var top = payees[0];
                list.Add(new Insight($"{top.Payee} received {top.Share:0%} of all payments ({Aed(top.Total)} in {top.Count} {Plural(top.Count, "cheque")}).", InsightKind.Info));
            }

            var months = paid.GroupBy(r => new DateTime(r.ChequeDate.Year, r.ChequeDate.Month, 1)).ToList();
            if (months.Count >= 2)
            {
                var busiest = months.OrderByDescending(g => g.Sum(r => r.Amount)).First();
                list.Add(new Insight($"Busiest month: {busiest.Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture)} with {Aed(busiest.Sum(r => r.Amount))}.", InsightKind.Info));
            }

            var regular = paid.GroupBy(r => PayeeKey(r.Payee))
                .Select(g => (Name: g.OrderByDescending(r => r.ChequeDate).First().Payee.Trim(),
                              Months: g.Select(r => (r.ChequeDate.Year, r.ChequeDate.Month)).Distinct().Count()))
                .Where(x => x.Months >= 3).OrderByDescending(x => x.Months).Take(3).ToList();
            if (regular.Count > 0)
                list.Add(new Insight("Regular payments: " + string.Join(", ", regular.Select(x => $"{x.Name} ({x.Months} months)")) + ".", InsightKind.Info));
        }

        var bounced = period.Where(r => r.Status == ChequeStatus.Bounced).ToList();
        if (bounced.Count > 0)
            list.Add(new Insight($"{bounced.Count} {Plural(bounced.Count, "cheque")} bounced in this period ({Aed(bounced.Sum(r => r.Amount))}).", InsightKind.Warning));

        // these look at every date, not only the period: they are about what to do now
        var stale = all.Where(r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date <= today.Date.AddDays(-14)).ToList();
        if (stale.Count > 0)
            list.Add(new Insight(
                $"{stale.Count} {Plural(stale.Count, "cheque")} ({Aed(stale.Sum(r => r.Amount))}) dated over 2 weeks ago {(stale.Count == 1 ? "is" : "are")} not marked Cleared yet. Check the bank statement and set the status in the register.",
                InsightKind.Warning));

        var due = Upcoming(all, today, 30);
        if (due.Count > 0)
            list.Add(new Insight(
                $"{Aed(due.Sum(r => r.Amount))} in post-dated cheques falls due in the next 30 days. Next: {Aed(due[0].Amount)} to {due[0].Payee.Trim()} on {due[0].ChequeDate:dd MMM}.",
                InsightKind.Warning));

        var voids = period.Count(r => r.Status == ChequeStatus.Void);
        if (voids > 0)
            list.Add(new Insight($"{voids} {Plural(voids, "cheque")} voided in this period.", InsightKind.Info));
        return list;
    }

    static string Aed(decimal v) => "AED " + v.ToString("#,##0.00", CultureInfo.InvariantCulture);
    static string Plural(int n, string word) => n == 1 ? word : word + "s";

    /// <summary>Paid totals per payee category, largest first. <paramref name="categoryOf"/> maps a payee name to its category.</summary>
    public static List<CategoryTotal> ByCategory(IEnumerable<ChequeRecord> rows, Func<string, string> categoryOf)
    {
        var paid = Counted(rows).Where(r => r.Status.CountsAsPaid()).ToList();
        var grand = paid.Sum(r => r.Amount);
        return paid.GroupBy(r => categoryOf(r.Payee), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var total = g.Sum(r => r.Amount);
                return new CategoryTotal(g.Key, g.Count(), total, grand == 0 ? 0 : total / grand, Math.Round(total / g.Count(), 2));
            })
            .OrderByDescending(c => c.Total).ThenBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
