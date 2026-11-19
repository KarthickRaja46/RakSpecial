using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class ChequeRecordsTests
{
    [Theory]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("1250.5", 1250.5)]
    [InlineData(" AED 15000 ", 15000)]
    [InlineData("aed 7", 7)]
    public void Amounts_are_read_with_or_without_commas_and_AED(string text, double expected)
    {
        Assert.True(ChequeRecords.TryParseAmount(text, out var a));
        Assert.Equal((decimal)expected, a);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12.3.4")]
    public void Bad_amounts_are_refused(string text) => Assert.False(ChequeRecords.TryParseAmount(text, out _));

    [Fact]
    public void Remove_void_keeps_every_other_status()
    {
        var list = new List<ChequeRecord>
        {
            new() { ChequeNo = "1", Status = ChequeStatus.Printed },
            new() { ChequeNo = "2", Status = ChequeStatus.Void },
            new() { ChequeNo = "3", Status = ChequeStatus.Cleared },
            new() { ChequeNo = "4", Status = ChequeStatus.Void },
            new() { ChequeNo = "5", Status = ChequeStatus.Bounced },
        };
        Assert.Equal(2, ChequeRecords.RemoveVoid(list));
        Assert.Equal(new[] { "1", "3", "5" }, list.Select(c => c.ChequeNo));
    }

    [Fact]
    public void Undo_void_only_changes_void_cheques()
    {
        var list = new[]
        {
            new ChequeRecord { ChequeNo = "1", Status = ChequeStatus.Void },
            new ChequeRecord { ChequeNo = "2", Status = ChequeStatus.Cleared },
        };
        Assert.Equal(1, ChequeRecords.UndoVoid(list));
        Assert.Equal(ChequeStatus.Printed, list[0].Status);
        Assert.Equal(ChequeStatus.Cleared, list[1].Status);
    }
}
