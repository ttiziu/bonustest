using System.Globalization;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Application.Bonuses;

public class BonusService(IBonusRepository repository, TimeProvider clock)
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public Task<PagedResponse<BonusDto>> ListAsync(BonusFilter filter, CancellationToken ct)
    {
        DateOnly? period = string.IsNullOrWhiteSpace(filter.Period) ? null : ParsePeriod(filter.Period);
        if (filter.EmployeeNumber is <= 0)
            throw new BusinessException("EMPLOYEE_NUMBER", "El número de empleado debe ser mayor que cero.");
        if (!string.IsNullOrWhiteSpace(filter.StateCode) && filter.StateCode is not
            (BonusStates.Registered or BonusStates.Cancelled or BonusStates.Paid))
            throw new BusinessException("BONUS_STATE", "El estado de bono no es válido.");
        if ((long)(filter.PageIndex - 1) * filter.PageSize > int.MaxValue)
            throw new BusinessException("PAGE", "El número de página es demasiado grande.");
        return repository.ListAsync(filter, period, ct);
    }

    public async Task<EmployeeDto> FindEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken ct)
    {
        var employee = await RequireEmployeeAsync(departmentCode, employeeNumber, ct);
        return new(employee.DepartmentCode, employee.EmployeeNumber, employee.FullName,
            employee.HireDate, employee.MonthlySalary, employee.TaxRate);
    }

    public async Task<int> RegisterAsync(RegisterBonusRequest request, string? userCode, CancellationToken ct)
    {
        var user = RequireUser(userCode);
        var period = ParsePeriod(request.Period);
        var now = clock.GetUtcNow();
        var today = TimeZoneInfo.ConvertTime(now, BusinessTimeZone);
        if (period > new DateOnly(today.Year, today.Month, 1))
            throw new BusinessException("FUTURE_PERIOD", "El periodo no puede ser posterior al mes actual.");
        var employee = await RequireEmployeeAsync(request.DepartmentCode, request.EmployeeNumber, ct);
        if (string.IsNullOrWhiteSpace(request.ReasonCode) ||
            !await repository.IsReasonActiveAsync(request.ReasonCode.Trim(), ct))
            throw new BusinessException("BONUS_REASON", "Selecciona un motivo activo.");
        var amounts = BonusCalculator.Calculate(request.BonusTypeId, request.Value, employee.MonthlySalary, employee.TaxRate);
        if (await repository.HasActiveBonusAsync(employee.DepartmentCode, employee.EmployeeNumber, period, ct))
            throw new BusinessException("DUPLICATE_BONUS", "El empleado ya tiene un bono activo en ese periodo.");
        var bonus = new Bonus
        {
            DepartmentCode = employee.DepartmentCode, EmployeeNumber = employee.EmployeeNumber,
            Period = period, BonusTypeId = request.BonusTypeId, Value = request.Value,
            ReasonCode = request.ReasonCode.Trim(), MonthlySalary = employee.MonthlySalary,
            TaxRate = employee.TaxRate, GrossAmount = amounts.GrossAmount,
            TaxAmount = amounts.TaxAmount, NetAmount = amounts.NetAmount,
            CreatedBy = user, ModifiedBy = user, CreatedAt = now, ModifiedAt = now
        };
        await repository.AddAsync(bonus, ct);
        return bonus.BonusId;
    }

    public async Task CancelAsync(int bonusId, string? userCode, CancellationToken ct)
    {
        var user = RequireUser(userCode);
        var bonus = await RequireBonusAsync(bonusId, ct);
        bonus.Cancel(user, clock.GetUtcNow());
        await repository.SaveAsync(ct);
    }

    public async Task DeleteAsync(int bonusId, string? userCode, CancellationToken ct)
    {
        var user = RequireUser(userCode);
        var bonus = await RequireBonusAsync(bonusId, ct);
        bonus.Delete(user, clock.GetUtcNow());
        await repository.SaveAsync(ct);
    }

    private async Task<Employee> RequireEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(departmentCode) || departmentCode.Trim().Length > 5 || employeeNumber <= 0)
            throw new BusinessException("EMPLOYEE_KEY", "Indica el área y un número de empleado válido.");
        var employee = await repository.FindEmployeeAsync(departmentCode.Trim(), employeeNumber, ct)
            ?? throw new BusinessException("EMPLOYEE_NOT_FOUND", "No se encontró un empleado con esa área y número.");
        if (employee.EmployeeStateCode != EmployeeStates.Active)
            throw new BusinessException("EMPLOYEE_INACTIVE", "El empleado está cesado y no puede recibir bonos.");
        return employee;
    }

    private async Task<Bonus> RequireBonusAsync(int id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) ?? throw new BusinessException("BONUS_NOT_FOUND", "El bono ya no está disponible. Actualiza el listado.");

    private static string RequireUser(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode) || userCode.Trim().Length > 100)
            throw new BusinessException("USER_CODE", "El header UserCode es obligatorio y admite hasta 100 caracteres.");
        return userCode.Trim();
    }

    private static DateOnly ParsePeriod(string? period)
    {
        if (period is null || period.Length != 7 || !DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new BusinessException("BONUS_PERIOD", "El periodo debe tener el formato YYYY-MM.");
        return parsed;
    }
}
