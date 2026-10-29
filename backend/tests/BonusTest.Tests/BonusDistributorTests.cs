using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Tests;

public class BonusDistributorTests
{
    private static EmployeeProjectHours Project(int id, decimal hours) => new()
        { EmployeeProjectHoursId = id, ProjectName = $"Proyecto {id}", Hours = hours };

    [Fact]
    public void SplitsByHoursAndUsesStableProjectOrder()
    {
        var rows = BonusDistributor.Distribute(500, [Project(2, 40), Project(1, 120)]);
        Assert.Equal(new decimal[] { 375, 125 }, rows.Select(x => x.GrossAmount));
        Assert.Equal(new[] { 1, 2 }, rows.Select(x => x.EmployeeProjectHoursId));
        Assert.Equal(500, rows.Sum(x => x.GrossAmount));
    }

    [Fact]
    public void LastProjectReceivesRoundingDifference()
    {
        var rows = BonusDistributor.Distribute(1, [Project(1, 1), Project(2, 1), Project(3, 1)]);
        Assert.Equal(new decimal[] { .33m, .33m, .34m }, rows.Select(x => x.GrossAmount));
    }

    [Fact]
    public void RoundsHalfUpAndKeepsExactTotal()
    {
        var rows = BonusDistributor.Distribute(.05m, [Project(1, 1), Project(2, 1)]);
        Assert.Equal(new decimal[] { .03m, .02m }, rows.Select(x => x.GrossAmount));
    }

    [Fact]
    public void SingleProjectGetsEverything()
    {
        var row = Assert.Single(BonusDistributor.Distribute(123.45m, [Project(1, 160)]));
        Assert.Equal(123.45m, row.GrossAmount);
        Assert.Equal(160, row.Hours);
    }

    [Fact]
    public void ZeroHourProjectGetsZero()
    {
        var rows = BonusDistributor.Distribute(100, [Project(1, 0), Project(2, 160)]);
        Assert.Equal(new decimal[] { 0, 100 }, rows.Select(x => x.GrossAmount));
    }

    [Fact]
    public void RejectsMissingZeroOrNegativeHours()
    {
        Assert.Throws<BusinessException>(() => BonusDistributor.Distribute(100, []));
        Assert.Throws<BusinessException>(() => BonusDistributor.Distribute(100, [Project(1, 0)]));
        Assert.Throws<BusinessException>(() => BonusDistributor.Distribute(100, [Project(1, -1), Project(2, 10)]));
    }

    [Fact]
    public void RejectsNegativeRoundingAdjustmentWithoutChangingState()
    {
        var bonus = new Bonus { GrossAmount = .03m };
        var ex = Assert.Throws<BusinessException>(() => bonus.Pay(
            Enumerable.Range(1, 5).Select(i => Project(i, 1)).ToList(), "test", DateTimeOffset.UtcNow));
        Assert.Equal("PROJECT_ROUNDING", ex.Code);
        Assert.Equal("0201", bonus.StateCode);
        Assert.Empty(bonus.ProjectAllocations);
    }

    [Theory]
    [InlineData("0202", false)]
    [InlineData("0203", false)]
    [InlineData("0201", true)]
    public void CannotPayPaidCancelledOrDeletedBonus(string state, bool deleted)
    {
        var bonus = new Bonus { StateCode = state, IsDeleted = deleted, GrossAmount = 100 };
        Assert.Throws<BusinessException>(() => bonus.Pay([Project(1, 10)], "test", DateTimeOffset.UtcNow));
        Assert.Empty(bonus.ProjectAllocations);
    }
}
