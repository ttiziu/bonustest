using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Tests;

public class BonusCalculatorTests
{
    [Theory]
    [InlineData(2, 10, 5000, 8, 500, 40, 460)]
    [InlineData(1, 100, 2500, 0, 100, 0, 100)]
    [InlineData(2, 100, 8000, 10, 8000, 800, 7200)]
    [InlineData(1, 0.125, 5000, 0, 0.13, 0, 0.13)]
    [InlineData(1, 1.25, 5000, 10, 1.25, 0.13, 1.12)]
    [InlineData(2, 0.0025, 5000, 8, 0.13, 0.01, 0.12)]
    [InlineData(1, 5000, 5000, 8, 5000, 400, 4600)]
    [InlineData(1, 0.0001, 5000, 8, 0, 0, 0)]
    public void CalculatesAndRoundsHalfUp(int type, decimal value, decimal salary, decimal tax,
        decimal gross, decimal withheld, decimal net)
    {
        Assert.Equal(new BonusAmounts(gross, withheld, net), BonusCalculator.Calculate(type, value, salary, tax));
    }

    [Theory]
    [InlineData(1, 0, 5000, 8)]
    [InlineData(1, -1, 5000, 8)]
    [InlineData(1, 5000.01, 5000, 8)]
    [InlineData(2, 100.0001, 5000, 8)]
    [InlineData(3, 10, 5000, 8)]
    [InlineData(1, 10, 0, 8)]
    [InlineData(1, 10, 5000, -1)]
    [InlineData(1, 10, 5000, 101)]
    [InlineData(1, 1.00001, 5000, 8)]
    public void RejectsInvalidAmounts(int type, decimal value, decimal salary, decimal tax) =>
        Assert.Throws<BusinessException>(() => BonusCalculator.Calculate(type, value, salary, tax));

    [Theory]
    [InlineData("0202", false)]
    [InlineData("0203", false)]
    [InlineData("0201", true)]
    public void OnlyRegisteredVisibleBonusesCanChange(string state, bool deleted)
    {
        var bonus = new Bonus { StateCode = state, IsDeleted = deleted };
        Assert.Throws<BusinessException>(() => bonus.Cancel("test", DateTimeOffset.UtcNow));
        Assert.Throws<BusinessException>(() => bonus.Delete("test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CancellationKeepsOriginalAmountsAndUpdatesAudit()
    {
        var now = DateTimeOffset.Parse("2026-10-29T17:00:00Z");
        var bonus = new Bonus { GrossAmount = 500, TaxAmount = 40, NetAmount = 460, CreatedBy = "creator" };
        bonus.Cancel("editor", now);
        Assert.Equal("0203", bonus.StateCode);
        Assert.Equal("creator", bonus.CreatedBy);
        Assert.Equal("editor", bonus.ModifiedBy);
        Assert.Equal(now, bonus.ModifiedAt);
        Assert.Equal(460, bonus.NetAmount);
        Assert.False(bonus.IsDeleted);
    }
}
