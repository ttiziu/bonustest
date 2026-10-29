using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Domain.Entities;

public class Bonus
{
    public int BonusId { get; set; }
    public string DepartmentCode { get; set; } = null!;
    public int EmployeeNumber { get; set; }
    public DateOnly Period { get; set; }
    public int BonusTypeId { get; set; }
    public decimal Value { get; set; }
    public string ReasonCode { get; set; } = null!;
    public string StateCode { get; set; } = BonusStates.Registered;
    public decimal MonthlySalary { get; set; }
    public decimal TaxRate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public bool IsDeleted { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string ModifiedBy { get; set; } = null!;
    public DateTimeOffset ModifiedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Employee Employee { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public State State { get; set; } = null!;
    public BonusType BonusType { get; set; } = null!;
    public BonusReason Reason { get; set; } = null!;

    public List<BonusProjectAllocation> ProjectAllocations { get; set; } = [];

    public void Pay(IReadOnlyList<EmployeeProjectHours> projects, string userCode, DateTimeOffset now)
    {
        EnsureRegistered();
        ProjectAllocations = BonusDistributor.Distribute(GrossAmount, projects);
        StateCode = BonusStates.Paid;
        Touch(userCode, now);
    }

    public void Cancel(string userCode, DateTimeOffset now)
    {
        EnsureRegistered();
        StateCode = BonusStates.Cancelled;
        Touch(userCode, now);
    }

    public void Delete(string userCode, DateTimeOffset now)
    {
        EnsureRegistered();
        IsDeleted = true;
        Touch(userCode, now);
    }

    private void EnsureRegistered()
    {
        if (IsDeleted || StateCode != BonusStates.Registered)
            throw new BusinessException("BONUS_STATE", "Solo se puede pagar, anular o eliminar un bono registrado.");
    }

    private void Touch(string userCode, DateTimeOffset now)
    {
        ModifiedBy = userCode;
        ModifiedAt = now;
    }
}
