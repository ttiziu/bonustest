namespace BonusTest.Domain.Entities;

public class BonusProjectAllocation
{
    public int BonusProjectAllocationId { get; set; }
    public int BonusId { get; set; }
    public int EmployeeProjectHoursId { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal Hours { get; set; }
    public decimal GrossAmount { get; set; }
}
