namespace VehicleManagement.Domain.Vehicles;

public sealed class Vehicle
{
    private Vehicle()
    {
    }

    public int Id { get; private set; }
    public string OwnerName { get; private set; } = string.Empty;
    public int ManufacturerId { get; private set; }
    public int YearOfManufacture { get; private set; }
    public decimal WeightKg { get; private set; }

    /// <summary>
    /// Creates a vehicle using a supplied year so the rule is predictable in tests
    /// and does not depend on the machine clock.
    /// </summary>
    public static Vehicle Create(
        string ownerName,
        int manufacturerId,
        int yearOfManufacture,
        decimal weightKg,
        int currentYear)
    {
        var trimmedOwnerName = ownerName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedOwnerName) || trimmedOwnerName.Length > 150)
        {
            throw new DomainValidationException("Owner names must contain between 1 and 150 characters.");
        }

        if (manufacturerId <= 0)
        {
            throw new DomainValidationException("A manufacturer must be selected.");
        }

        if (yearOfManufacture < 1800 || yearOfManufacture > currentYear + 1)
        {
            throw new DomainValidationException("The manufacture year must be between 1800 and next year.");
        }

        WeightRules.Validate(weightKg, allowZero: false);

        return new Vehicle
        {
            OwnerName = trimmedOwnerName,
            ManufacturerId = manufacturerId,
            YearOfManufacture = yearOfManufacture,
            WeightKg = weightKg
        };
    }
}
