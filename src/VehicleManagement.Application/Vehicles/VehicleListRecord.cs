namespace VehicleManagement.Application.Vehicles;

public sealed class VehicleListRecord
{
    public VehicleListRecord(
        int id,
        string ownerName,
        string manufacturerName,
        int yearOfManufacture,
        decimal weightKg)
    {
        Id = id;
        OwnerName = ownerName;
        ManufacturerName = manufacturerName;
        YearOfManufacture = yearOfManufacture;
        WeightKg = weightKg;
    }

    public int Id { get; }
    public string OwnerName { get; }
    public string ManufacturerName { get; }
    public int YearOfManufacture { get; }
    public decimal WeightKg { get; }
}
