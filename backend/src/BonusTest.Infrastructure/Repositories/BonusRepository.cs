using System.Globalization;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BonusTest.Infrastructure.Repositories;

public class BonusRepository(AppDbContext context) : IBonusRepository
{
    public async Task<PagedResponse<BonusDto>> ListAsync(BonusFilter filter, DateOnly? period, CancellationToken ct)
    {
        var query = context.Bonuses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
            query = query.Where(x => x.DepartmentCode == filter.DepartmentCode.Trim());
        if (filter.EmployeeNumber.HasValue)
            query = query.Where(x => x.EmployeeNumber == filter.EmployeeNumber.Value);
        if (period.HasValue) query = query.Where(x => x.Period == period.Value);
        if (!string.IsNullOrWhiteSpace(filter.StateCode))
            query = query.Where(x => x.StateCode == filter.StateCode);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.BonusId)
            .Skip((filter.PageIndex - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new
            {
                x.BonusId, x.DepartmentCode, x.Department.DepartmentName, x.EmployeeNumber,
                x.Employee.FullName, x.Period, x.BonusTypeId, x.BonusType.BonusTypeDescription,
                x.Value, x.ReasonCode, x.Reason.ReasonDescription, x.StateCode, x.State.StateDescription,
                x.GrossAmount, x.TaxAmount, x.NetAmount
            }).ToListAsync(ct);
        var data = rows.Select(x => new BonusDto(x.BonusId, x.DepartmentCode.Trim(), x.DepartmentName,
            x.EmployeeNumber, x.FullName, x.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            x.BonusTypeId, x.BonusTypeDescription, x.Value, x.ReasonCode.Trim(), x.ReasonDescription,
            x.StateCode.Trim(), x.StateDescription, x.GrossAmount, x.TaxAmount, x.NetAmount)).ToList();
        return PagedResponse<BonusDto>.Create(data, filter.PageIndex, filter.PageSize, total);
    }

    public Task<Employee?> FindEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken ct) =>
        context.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber, ct);

    public Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken ct) =>
        context.BonusReasons.AnyAsync(x => x.ReasonCode == reasonCode && x.State, ct);

    public Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, DateOnly period, CancellationToken ct) =>
        context.Bonuses.AnyAsync(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber &&
            x.Period == period && x.StateCode != BonusStates.Cancelled, ct);

    public Task<Bonus?> FindAsync(int bonusId, CancellationToken ct) =>
        context.Bonuses.SingleOrDefaultAsync(x => x.BonusId == bonusId, ct);

    public async Task AddAsync(Bonus bonus, CancellationToken ct)
    {
        context.Bonuses.Add(bonus);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("BONUS_CHANGED", "Otro usuario modificó el bono. Actualiza el listado.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException("DUPLICATE_BONUS", "El empleado ya tiene un bono activo en ese periodo.");
        }
    }
}
