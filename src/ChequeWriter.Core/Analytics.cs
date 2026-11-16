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

    public static AnalyticsSummary Summarize(IEnumerable<ChequeRecord> rows, DateTime today)
    {
        var all = rows.ToList();
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
        var paid = rows.Where(r => r.Status.CountsAsPaid()).ToList();
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
        var all = rows.ToList();
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
        rows.Where(r => r.Status == ChequeStatus.Printed && r.ChequeDate.Date > today.Date &&
                        (days == null || r.ChequeDate.Date <= today.Date.AddDays(days.Value)))
            .OrderBy(r => r.ChequeDate).ThenBy(r => r.ChequeNo, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
