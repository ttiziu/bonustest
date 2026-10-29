using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public static class BonusDistributor
{
    public static List<BonusProjectAllocation> Distribute(decimal grossAmount, IReadOnlyList<EmployeeProjectHours> projects)
    {
        if (projects.Count == 0 || projects.Any(x => x.Hours < 0) || projects.Sum(x => x.Hours) <= 0)
            throw new BusinessException("PROJECT_HOURS", "El empleado necesita proyectos con horas válidas y un total mayor que cero para pagar el bono.");

        var ordered = projects.OrderBy(x => x.EmployeeProjectHoursId).ToList();
        var totalHours = ordered.Sum(x => x.Hours);
        var remaining = grossAmount;
        var allocations = new List<BonusProjectAllocation>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var project = ordered[i];
            var amount = i == ordered.Count - 1
                ? remaining
                : decimal.Round(grossAmount * project.Hours / totalHours, 2, MidpointRounding.AwayFromZero);
            // No se persiste un ajuste negativo si las mitades acumuladas superan el bruto.
            if (amount < 0)
                throw new BusinessException("PROJECT_ROUNDING", "El redondeo del reparto produce un ajuste negativo. Revisa las horas de los proyectos antes de pagar.");
            allocations.Add(new BonusProjectAllocation
            {
                EmployeeProjectHoursId = project.EmployeeProjectHoursId,
                ProjectName = project.ProjectName, Hours = project.Hours, GrossAmount = amount
            });
            remaining -= amount;
        }
        return allocations;
    }
}
