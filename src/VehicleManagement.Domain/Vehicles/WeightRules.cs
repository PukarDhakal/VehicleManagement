namespace VehicleManagement.Domain.Vehicles;

internal static class WeightRules
{
    internal const decimal MaximumWeightKg = 99_999_999.99m;

    internal static void Validate(decimal weightKg, bool allowZero)
    {
        if (weightKg < 0 || (!allowZero && weightKg == 0))
        {
            throw new DomainValidationException(allowZero
                ? "A category minimum cannot be negative."
                : "Vehicle weight must be greater than zero.");
        }

        if (weightKg > MaximumWeightKg)
        {
            throw new DomainValidationException("Weight cannot exceed 99,999,999.99 kg.");
        }

        if (decimal.Round(weightKg, 2) != weightKg)
        {
            throw new DomainValidationException("Weight must have no more than two decimal places.");
        }
    }
}
