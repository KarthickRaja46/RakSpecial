using System.Text.Json;
using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class RakBooksTests
{
    [Fact]
    public void Business_and_old_books_have_their_measured_sizes_and_codes()
    {
        var biz = DefaultLayouts.RakBusiness();
        var old = DefaultLayouts.RakOld();
        Assert.Equal(("RAK-001", 202.0, 99.0), (biz.Code, biz.ChequeWidthMm, biz.ChequeHeightMm));
        Assert.Equal(("RAK-002", 206.0, 100.0), (old.Code, old.ChequeWidthMm, old.ChequeHeightMm));
        Assert.Equal("rak-business", biz.BuiltInPicture);
        Assert.Equal("rak-old", old.BuiltInPicture);
    }

    [Fact]
    public void Old_book_has_its_amount_box_level_with_the_second_words_line()
    {
        var old = DefaultLayouts.RakOld();
        var figures = old.Field(FieldKind.AmountFigures)!;
        var words2 = old.Field(FieldKind.AmountWordsLine2)!;
        Assert.True(Math.Abs(figures.Y - words2.Y) < 1.0);
        var biz = DefaultLayouts.RakBusiness();
        Assert.True(biz.Field(FieldKind.AmountFigures)!.Y < biz.Field(FieldKind.AmountWordsLine2)!.Y - 5);
    }

    [Fact]
    public void Every_field_lies_on_its_cheque()
    {
        foreach (var bank in DefaultLayouts.All())
        foreach (var f in bank.Fields.Where(f => f.Rotation == 0))
        {
            Assert.InRange(f.X, 0, bank.ChequeWidthMm);
            Assert.InRange(f.X + f.Width, 0, bank.ChequeWidthMm);
            Assert.InRange(f.Y + f.Height, 0, bank.ChequeHeightMm);
        }
    }

    [Fact]
    public void Version_1_data_is_upgraded_without_losing_calibration()
    {
        // what version 1 ("Cheque Writer") saved: one bank, generic outline, user-moved amount field
        var v1 = new AppData();
        var bank = DefaultLayouts.RakBusiness();
        bank.Name = "RAK Bank (Business)"; bank.Code = ""; bank.BuiltInPicture = "";
        bank.ChequeWidthMm = 204; bank.ChequeHeightMm = 94; bank.OffsetXmm = 1.2;
        bank.Field(FieldKind.AmountFigures)!.Y = 44.4;
        v1.Banks.Add(bank);
        v1.Settings.LastBankId = bank.Id;
        var json = JsonSerializer.Serialize(v1, DataStore.JsonOptions);
        var loaded = JsonSerializer.Deserialize<AppData>(json, DataStore.JsonOptions)!;

        Assert.True(DefaultLayouts.EnsureBuiltIns(loaded));
        Assert.Equal(2, loaded.Banks.Count);
        var b = loaded.Banks.Single(x => x.Id == DefaultLayouts.RakBusinessId);
        Assert.Equal(("RAK Business", "RAK-001", "rak-business"), (b.Name, b.Code, b.BuiltInPicture));
        Assert.Equal((202.0, 99.0), (b.ChequeWidthMm, b.ChequeHeightMm));
        Assert.Equal(1.2, b.OffsetXmm);
        Assert.Equal(44.4, b.Field(FieldKind.AmountFigures)!.Y);
        Assert.False(DefaultLayouts.EnsureBuiltIns(loaded));   // second run changes nothing
    }
}

public class MigrationTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "rs-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Old_data_is_copied_once_and_left_in_place()
    {
        var oldPath = Path.Combine(_dir, "old", "chequewriter-data.json");
        var newPath = Path.Combine(_dir, "new", DataStore.DataFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(oldPath)!);
        var old = new DataStore(oldPath);
        old.RememberPayee("Kept Payee LLC");
        old.Save();

        Assert.True(DataStore.MigrateFrom(oldPath, newPath));
        Assert.True(File.Exists(oldPath));
        var store = new DataStore(newPath);
        Assert.Equal("Kept Payee LLC", Assert.Single(store.Data.Payees).Name);
        Assert.False(DataStore.MigrateFrom(oldPath, newPath));   // never overwrites RAK Special data
    }

    [Fact]
    public void Nothing_to_migrate_starts_fresh()
    {
        Assert.False(DataStore.MigrateFrom(Path.Combine(_dir, "missing.json"), Path.Combine(_dir, "new.json")));
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
}

public class PdcScheduleTests
{
    [Fact]
    public void Twelve_monthly_cheques_with_numbers_counting_up()
    {
        var s = PdcSchedule.Build(new DateTime(2027, 1, 5), 12, 1, "000998", 4500m);
        Assert.Equal(12, s.Count);
        Assert.Equal(new DateTime(2027, 12, 5), s[^1].Date);
        Assert.Equal(new[] { "000998", "000999", "001000" }, s.Take(3).Select(i => i.ChequeNo));
        Assert.All(s, i => Assert.Equal(4500m, i.Amount));
    }

    [Fact]
    public void Month_end_start_does_not_drift()
    {
        var s = PdcSchedule.Build(new DateTime(2027, 1, 31), 4, 1, "1", 100m);
        Assert.Equal(new[] { new DateTime(2027, 1, 31), new DateTime(2027, 2, 28), new DateTime(2027, 3, 31), new DateTime(2027, 4, 30) },
            s.Select(i => i.Date));
    }

    [Fact]
    public void Quarterly_interval()
    {
        var s = PdcSchedule.Build(new DateTime(2027, 1, 1), 4, 3, "", 100m);
        Assert.Equal(new DateTime(2027, 10, 1), s[^1].Date);
        Assert.All(s, i => Assert.Equal("", i.ChequeNo));
    }

    [Theory]
    [InlineData(2027, 5, 1, WeekendRule.MoveToMonday, 2027, 5, 3)]   // Saturday
    [InlineData(2027, 5, 2, WeekendRule.MoveToMonday, 2027, 5, 3)]   // Sunday
    [InlineData(2027, 5, 1, WeekendRule.MoveToFriday, 2027, 4, 30)]
    [InlineData(2027, 5, 2, WeekendRule.MoveToFriday, 2027, 4, 30)]
    [InlineData(2027, 5, 3, WeekendRule.MoveToMonday, 2027, 5, 3)]   // Monday stays
    [InlineData(2027, 5, 1, WeekendRule.KeepDate, 2027, 5, 1)]
    public void Weekend_rule(int y, int m, int d, WeekendRule rule, int ey, int em, int ed) =>
        Assert.Equal(new DateTime(ey, em, ed), PdcSchedule.AdjustForWeekend(new DateTime(y, m, d), rule));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(121, 1)]
    [InlineData(12, 0)]
    [InlineData(12, 13)]
    public void Rejects_bad_counts(int count, int every) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PdcSchedule.Build(DateTime.Today, count, every, "1", 1m));
}
