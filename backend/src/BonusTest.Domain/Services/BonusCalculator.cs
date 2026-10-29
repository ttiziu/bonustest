using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public record BonusAmounts(decimal GrossAmount, decimal TaxAmount, decimal NetAmount);

public static class BonusCalculator
{
    public static BonusAmounts Calculate(int type, decimal value, decimal salary, decimal taxRate)
    {
        if (type is not (BonusTypes.FixedAmount or BonusTypes.SalaryPercentage))
            throw new BusinessException("BONUS_TYPE", "El tipo de bono no es válido.");
        if (value <= 0 || decimal.Round(value, 4) != value)
            throw new BusinessException("BONUS_VALUE", "Ingresa un valor mayor que cero con un máximo de cuatro decimales.");
        if (salary <= 0 || taxRate < 0 || taxRate > 100)
            throw new BusinessException("EMPLOYEE_PAY", "El sueldo o la retención del empleado no son válidos.");
        if (type == BonusTypes.FixedAmount && value > salary)
            throw new BusinessException("BONUS_VALUE", "El monto fijo no puede superar el sueldo mensual.");
        if (type == BonusTypes.SalaryPercentage && value > 100)
            throw new BusinessException("BONUS_VALUE", "El porcentaje no puede superar el 100 %.");

        var gross = Round(type == BonusTypes.FixedAmount ? value : salary * value / 100);
        if (gross == 0)
            throw new BusinessException("BONUS_VALUE", "El bono debe ser de al menos 0.01 después del redondeo.");
        var tax = Round(gross * taxRate / 100);
        return new(gross, tax, Round(gross - tax));
    }

    private static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
