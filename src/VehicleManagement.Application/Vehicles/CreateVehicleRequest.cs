namespace VehicleManagement.Application.Vehicles;

public sealed class CreateVehicleRequest
{
    public CreateVehicleRequest(
        string ownerName,
        int manufacturerId,
        int yearOfManufacture,
        decimal weightKg)
    {
        OwnerName = ownerName;
        ManufacturerId = manufacturerId;
        YearOfManufacture = yearOfManufacture;
        WeightKg = weightKg;
    }

    public string OwnerName { get; }
    public int ManufacturerId { get; }
    public int YearOfManufacture { get; }
    public decimal WeightKg { get; }
}
