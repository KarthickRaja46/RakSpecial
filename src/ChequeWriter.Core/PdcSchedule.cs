namespace ChequeWriter.Core;

/// <summary>What to do when a post-dated cheque falls on a weekend (UAE weekend: Saturday and Sunday).</summary>
public enum WeekendRule
{
    KeepDate,
    MoveToMonday,
    MoveToFriday,
}

/// <summary>One cheque in a post-dated (PDC) series.</summary>
public record PdcItem(int Index, string ChequeNo, DateTime Date, decimal Amount);

/// <summary>
/// Builds a series of post-dated cheques, e.g. 12 monthly rent cheques.
/// Every date is counted from the first date (not from the previous one), so a series starting
/// on the 31st gives 31 Jan, 28/29 Feb, 31 Mar, 30 Apr ... instead of drifting to the 28th.
/// </summary>
public static class PdcSchedule
{
    public const int MaxCheques = 120;

    public static IReadOnlyList<PdcItem> Build(DateTime firstDate, int count, int everyMonths,
        string firstChequeNo, decimal amount, WeekendRule weekend = WeekendRule.KeepDate)
    {
        if (count < 1 || count > MaxCheques) throw new ArgumentOutOfRangeException(nameof(count), $"Number of cheques must be 1 to {MaxCheques}.");
        if (everyMonths < 1 || everyMonths > 12) throw new ArgumentOutOfRangeException(nameof(everyMonths), "Interval must be 1 to 12 months.");

        var list = new List<PdcItem>(count);
        var no = (firstChequeNo ?? "").Trim();
        for (var i = 0; i < count; i++)
        {
            var date = AdjustForWeekend(firstDate.Date.AddMonths(i * everyMonths), weekend);
            list.Add(new PdcItem(i + 1, no, date, amount));
            no = DataStore.NextNumber(no);
        }
        return list;
    }

    public static bool IsWeekend(DateTime d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static DateTime AdjustForWeekend(DateTime d, WeekendRule rule) => rule switch
    {
        WeekendRule.MoveToMonday => d.DayOfWeek switch
        {
            DayOfWeek.Saturday => d.AddDays(2),
            DayOfWeek.Sunday => d.AddDays(1),
            _ => d,
        },
        WeekendRule.MoveToFriday => d.DayOfWeek switch
        {
            DayOfWeek.Saturday => d.AddDays(-1),
            DayOfWeek.Sunday => d.AddDays(-2),
            _ => d,
        },
        _ => d,
    };
}
