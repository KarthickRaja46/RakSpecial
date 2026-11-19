using System.Globalization;

namespace ChequeWriter.Core;

/// <summary>Small helpers for changing the cheque register.</summary>
public static class ChequeRecords
{
    /// <summary>Reads an amount typed by the user: "1,250.50", "1250.5" or "AED 1250" all work.</summary>
    public static bool TryParseAmount(string text, out decimal amount)
    {
        var t = (text ?? "").Trim().Replace("AED", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    /// <summary>Removes every Void cheque (spoilt or cancelled leaves) from the register. Returns how many were removed.</summary>
    public static int RemoveVoid(List<ChequeRecord> cheques) => cheques.RemoveAll(c => c.Status == ChequeStatus.Void);

    /// <summary>Puts Void cheques back to Printed. Returns how many changed.</summary>
    public static int UndoVoid(IEnumerable<ChequeRecord> cheques)
    {
        var n = 0;
        foreach (var c in cheques.Where(c => c.Status == ChequeStatus.Void))
        {
            c.Status = ChequeStatus.Printed;
            n++;
        }
        return n;
    }
}
