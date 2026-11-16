namespace ChequeWriter.Core;

/// <summary>
/// Built-in RAK Bank cheque books. Positions are millimetres from the top-left of the page
/// (A4 landscape, cheque placed in the top-left corner against the paper guide).
/// </summary>
public static class DefaultLayouts
{
    public const string RakBusinessId = "rak-business";
    public const string RakOldId = "rak-old";

    /// <summary>
    /// RAK BUSINESS - new model (RAK-001), 202 x 99 mm.
    /// Calibrated with a test print on a real RAK Business cheque (Nov 2026).
    /// </summary>
    public static BankLayout RakBusiness() => new()
    {
        Id = RakBusinessId,
        Name = "RAK Business",
        Code = "RAK-001",
        BuiltInPicture = "rak-business",
        PaperWidthMm = 297,
        PaperHeightMm = 210,
        ChequeWidthMm = 202,
        ChequeHeightMm = 99,
        DateFormat = "dd-MM-yyyy",
        Fields =
        {
            new() { Kind = FieldKind.Date, Label = "Date", X = 156.23, Y = 18.83, Width = 32, Height = 6.0,
                    FontSize = 9.75, CenterVertically = true, CharPitch = 3.1575 },
            new() { Kind = FieldKind.Payee, Label = "Payee", X = 37.22, Y = 36.14, Width = 130.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountWordsLine1, Label = "Amount in words - line 1", X = 16.50, Y = 45.31, Width = 118.12, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountWordsLine2, Label = "Amount in words - line 2", X = 7.06, Y = 55.00, Width = 134.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountFigures, Label = "Amount in figures", X = 157.67, Y = 45.70, Width = 40.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.BearerStrike, Label = "Strike 'or Bearer'", X = 5.45, Y = 38.35, Width = 17.0, Height = 4.0, FontSize = 9, Text = "XXXXXXX" },
            new() { Kind = FieldKind.AcPayeeText, Label = "A/C Payee text", X = 3.70, Y = 1.30, Width = 34.0, Height = 26.0, FontSize = 8.25,
                    Rotation = 35, Align = TextAlign.Center, CenterVertically = true, Text = "A/C PAYEE ONLY" },
        },
        Lines =
        {
            new() { Label = "Crossing line 1", X1 = -0.50, Y1 = 18.90, X2 = 27.50, Y2 = -1.10 },
            new() { Label = "Crossing line 2", X1 = -0.30, Y1 = 26.10, X2 = 38.70, Y2 = -1.90 },
        },
    };

    /// <summary>
    /// RAK BANK - older, wider book (RAK-002), 206 x 100 mm.
    /// The amount box sits lower (level with the second words line) than on the Business book.
    /// Starting positions: do one plain-paper test print before the first real cheque.
    /// </summary>
    public static BankLayout RakOld() => new()
    {
        Id = RakOldId,
        Name = "RAK Old",
        Code = "RAK-002",
        BuiltInPicture = "rak-old",
        PaperWidthMm = 297,
        PaperHeightMm = 210,
        ChequeWidthMm = 206,
        ChequeHeightMm = 100,
        DateFormat = "dd-MM-yyyy",
        Fields =
        {
            new() { Kind = FieldKind.Date, Label = "Date", X = 156.56, Y = 18.56, Width = 32, Height = 6.0,
                    FontSize = 11.25, CenterVertically = true, CharPitch = 3.1575 },
            new() { Kind = FieldKind.Payee, Label = "Payee", X = 41.72, Y = 35.62, Width = 122.0, Height = 6.0, FontSize = 12 },
            new() { Kind = FieldKind.AmountWordsLine1, Label = "Amount in words - line 1", X = 18.88, Y = 45.04, Width = 118.12, Height = 6.0, FontSize = 12 },
            new() { Kind = FieldKind.AmountWordsLine2, Label = "Amount in words - line 2", X = 7.06, Y = 54.74, Width = 135.0, Height = 6.0, FontSize = 12 },
            new() { Kind = FieldKind.AmountFigures, Label = "Amount in figures", X = 155.61, Y = 54.82, Width = 41.0, Height = 7.0, FontSize = 12.75 },
            new() { Kind = FieldKind.BearerStrike, Label = "Strike 'or Bearer'", X = 5.72, Y = 37.82, Width = 20.0, Height = 5.0, FontSize = 11.25, Text = "XXXXXXX" },
            new() { Kind = FieldKind.AcPayeeText, Label = "A/C Payee text", X = 3.70, Y = 1.30, Width = 34.0, Height = 26.0, FontSize = 8.25,
                    Rotation = 35, Align = TextAlign.Center, CenterVertically = true, Text = "A/C PAYEE ONLY" },
        },
        Lines =
        {
            new() { Label = "Crossing line 1", X1 = -0.50, Y1 = 18.90, X2 = 27.50, Y2 = -1.10 },
            new() { Label = "Crossing line 2", X1 = -0.30, Y1 = 26.10, X2 = 38.70, Y2 = -1.90 },
        },
    };

    public static IReadOnlyList<BankLayout> All() => new[] { RakBusiness(), RakOld() };

    /// <summary>The factory layout for a built-in bank id, or null for a bank the user added.</summary>
    public static BankLayout? ById(string id) => All().FirstOrDefault(b => b.Id == id);

    public static AppData NewAppData()
    {
        var data = new AppData();
        data.Banks.AddRange(All());
        data.Settings.LastBankId = RakBusinessId;
        return data;
    }

    /// <summary>
    /// Brings data saved by an older version up to date: adds built-in books that are missing and
    /// fills in their code and picture. The user's own field positions and offsets are never changed.
    /// Returns true when something was changed.
    /// </summary>
    public static bool EnsureBuiltIns(AppData data)
    {
        var changed = false;
        foreach (var factory in All())
        {
            var bank = data.Banks.FirstOrDefault(b => b.Id == factory.Id);
            if (bank == null)
            {
                data.Banks.Add(factory);
                changed = true;
                continue;
            }
            if (bank.Code.Length == 0) { bank.Code = factory.Code; changed = true; }
            if (bank.BuiltInPicture.Length == 0) { bank.BuiltInPicture = factory.BuiltInPicture; changed = true; }
            // version 1 used a generic 204 x 94 mm outline for the Business book
            if (bank.Id == RakBusinessId && bank.ChequeWidthMm == 204 && bank.ChequeHeightMm == 94)
            {
                bank.ChequeWidthMm = factory.ChequeWidthMm; bank.ChequeHeightMm = factory.ChequeHeightMm;
                if (bank.Name == "RAK Bank (Business)") bank.Name = factory.Name;
                changed = true;
            }
        }
        if (data.Banks.All(b => b.Id != data.Settings.LastBankId)) data.Settings.LastBankId = data.Banks[0].Id;
        return changed;
    }
}
