namespace VehicleManagement.Domain.Vehicles;

public sealed class Manufacturer
{
    private Manufacturer()
    {
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static Manufacturer Create(string name)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 100)
        {
            throw new DomainValidationException("Manufacturer names must contain between 1 and 100 characters.");
        }

        return new Manufacturer { Name = trimmedName };
    }
}
