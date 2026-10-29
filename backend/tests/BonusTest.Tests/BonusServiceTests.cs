using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Tests;

public class BonusServiceTests
{
    private readonly FakeRepository repository = new();
    private BonusService Service => new(repository, new FixedClock());
    private static RegisterBonusRequest Request(string period = "2026-10") => new("TECH1", 101, period, 2, 10, "000001");

    [Theory]
    [InlineData("2026-11", "FUTURE_PERIOD")]
    [InlineData("2026-13", "BONUS_PERIOD")]
    [InlineData("2026-1", "BONUS_PERIOD")]
    [InlineData("2026-00", "BONUS_PERIOD")]
    [InlineData("", "BONUS_PERIOD")]
    public async Task RejectsInvalidPeriods(string period, string expectedCode)
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.RegisterAsync(Request(period), "test", default));
        Assert.Equal(expectedCode, ex.Code);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task UsesLimaMonthAtUtcMonthBoundary()
    {
        // 1 de noviembre UTC todavía es 31 de octubre en Lima.
        var service = new BonusService(repository, new FixedClock(DateTimeOffset.Parse("2026-11-01T02:00:00Z")));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RegisterAsync(Request("2026-11"), "test", default));
        Assert.Equal("FUTURE_PERIOD", ex.Code);
        await service.RegisterAsync(Request("2026-10"), "test", default);
        Assert.NotNull(repository.Added);
    }

    [Fact]
    public async Task RequiresActiveEmployeeAndReason()
    {
        repository.Employee.EmployeeStateCode = "0102";
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.RegisterAsync(Request(), "test", default));
        Assert.Equal("EMPLOYEE_INACTIVE", ex.Code);
        repository.Employee.EmployeeStateCode = "0101";
        repository.ReasonActive = false;
        ex = await Assert.ThrowsAsync<BusinessException>(() => Service.RegisterAsync(Request(), "test", default));
        Assert.Equal("BONUS_REASON", ex.Code);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task RejectsDuplicateAndMissingOperator()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.RegisterAsync(Request(), " ", default));
        Assert.Equal("USER_CODE", ex.Code);
        repository.Duplicate = true;
        ex = await Assert.ThrowsAsync<BusinessException>(() => Service.RegisterAsync(Request(), "test", default));
        Assert.Equal("DUPLICATE_BONUS", ex.Code);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task SavesCalculationSnapshotAndAudit()
    {
        await Service.RegisterAsync(Request(), " tester ", default);
        var bonus = Assert.IsType<Bonus>(repository.Added);
        Assert.Equal(5000, bonus.MonthlySalary);
        Assert.Equal(8, bonus.TaxRate);
        Assert.Equal(500, bonus.GrossAmount);
        Assert.Equal(40, bonus.TaxAmount);
        Assert.Equal(460, bonus.NetAmount);
        Assert.Equal("0201", bonus.StateCode);
        Assert.Equal("tester", bonus.CreatedBy);
        Assert.Equal(bonus.CreatedBy, bonus.ModifiedBy);
        Assert.Equal(bonus.CreatedAt, bonus.ModifiedAt);
    }

    [Fact]
    public async Task PaymentSavesProjectsAndPaidStateTogether()
    {
        repository.Loaded = new Bonus { BonusId = 7, DepartmentCode = "TECH1", EmployeeNumber = 101, GrossAmount = 500 };
        repository.Projects = [new() { EmployeeProjectHoursId = 1, ProjectName = "Portal", Hours = 120 },
            new() { EmployeeProjectHoursId = 2, ProjectName = "App", Hours = 40 }];
        var result = await Service.PayAsync(7, "payer", default);
        Assert.Equal("0202", result.StateCode);
        Assert.Equal(new decimal[] { 375, 125 }, result.Projects.Select(x => x.GrossAmount));
        Assert.Equal("payer", repository.Loaded.ModifiedBy);
        Assert.True(repository.PaymentSaved);
        await Assert.ThrowsAsync<BusinessException>(() => Service.PayAsync(7, "payer", default));
    }

    [Fact]
    public async Task PaymentDoesNotSaveWhenProjectsAreMissing()
    {
        repository.Loaded = new Bonus { GrossAmount = 500 };
        await Assert.ThrowsAsync<BusinessException>(() => Service.PayAsync(7, "payer", default));
        Assert.False(repository.PaymentSaved);
        Assert.Equal("0201", repository.Loaded.StateCode);
    }

    [Fact]
    public async Task PaymentRequiresOperatorAndExistingBonus()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.PayAsync(7, null, default));
        Assert.Equal("USER_CODE", ex.Code);
        ex = await Assert.ThrowsAsync<BusinessException>(() => Service.PayAsync(7, "payer", default));
        Assert.Equal("BONUS_NOT_FOUND", ex.Code);
        Assert.False(repository.PaymentSaved);
    }

    private class FixedClock(DateTimeOffset? instant = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant ?? DateTimeOffset.Parse("2026-10-29T17:00:00Z");
    }

    private class FakeRepository : IBonusRepository
    {
        public Employee Employee { get; } = new() { DepartmentCode = "TECH1", EmployeeNumber = 101,
            FullName = "Andrea Torres Vega", MonthlySalary = 5000, TaxRate = 8, EmployeeStateCode = "0101" };
        public bool ReasonActive { get; set; } = true;
        public bool Duplicate { get; set; }
        public Bonus? Added { get; private set; }
        public Bonus? Loaded { get; set; }
        public IReadOnlyList<EmployeeProjectHours> Projects { get; set; } = [];
        public bool PaymentSaved { get; private set; }
        public Task<IReadOnlyList<EmployeeProjectHours>> GetProjectHoursAsync(string d, int n, CancellationToken ct) => Task.FromResult(Projects);
        public Task SavePaymentAsync(CancellationToken ct) { PaymentSaved = true; return Task.CompletedTask; }
        public Task<Employee?> FindEmployeeAsync(string d, int n, CancellationToken ct) => Task.FromResult<Employee?>(Employee);
        public Task<bool> IsReasonActiveAsync(string r, CancellationToken ct) => Task.FromResult(ReasonActive);
        public Task<bool> HasActiveBonusAsync(string d, int n, DateOnly p, CancellationToken ct) => Task.FromResult(Duplicate);
        public Task AddAsync(Bonus bonus, CancellationToken ct) { Added = bonus; return Task.CompletedTask; }
        public Task<Bonus?> FindAsync(int id, CancellationToken ct) => Task.FromResult(Loaded);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<PagedResponse<BonusDto>> ListAsync(BonusFilter f, DateOnly? p, CancellationToken ct) =>
            Task.FromResult(PagedResponse<BonusDto>.Create([], f.PageIndex, f.PageSize, 0));
    }
}
