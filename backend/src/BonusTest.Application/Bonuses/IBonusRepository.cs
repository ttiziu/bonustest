using BonusTest.Application.Common;
using BonusTest.Domain.Entities;

namespace BonusTest.Application.Bonuses;

public interface IBonusRepository
{
    Task<PagedResponse<BonusDto>> ListAsync(BonusFilter filter, DateOnly? period, CancellationToken ct);
    Task<Employee?> FindEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken ct);
    Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken ct);
    Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, DateOnly period, CancellationToken ct);
    Task<Bonus?> FindAsync(int bonusId, CancellationToken ct);
    Task AddAsync(Bonus bonus, CancellationToken ct);
    Task<IReadOnlyList<EmployeeProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken ct);
    Task SavePaymentAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
