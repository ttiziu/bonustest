using BonusTest.Application.Common;

namespace BonusTest.Application.Bonuses;

public class BonusFilter : PaginationRequest
{
    public string? DepartmentCode { get; set; }
    public int? EmployeeNumber { get; set; }
    public string? Period { get; set; }
    public string? StateCode { get; set; }
}

public record RegisterBonusRequest(string DepartmentCode, int EmployeeNumber, string Period,
    int BonusTypeId, decimal Value, string ReasonCode);

public record EmployeeDto(string DepartmentCode, int EmployeeNumber, string FullName,
    DateTime HireDate, decimal MonthlySalary, decimal TaxRate);

public record BonusDto(int BonusId, string DepartmentCode, string DepartmentName, int EmployeeNumber,
    string FullName, string Period, int BonusTypeId, string BonusTypeDescription, decimal Value,
    string ReasonCode, string ReasonDescription, string StateCode, string StateDescription,
    decimal GrossAmount, decimal TaxAmount, decimal NetAmount);

public record BonusProjectDto(string ProjectName, decimal Hours, decimal GrossAmount);
public record BonusPaymentDto(int BonusId, string StateCode, IReadOnlyList<BonusProjectDto> Projects);
