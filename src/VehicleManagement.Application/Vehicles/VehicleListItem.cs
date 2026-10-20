namespace VehicleManagement.Application.Vehicles;

public sealed class VehicleListItem
{
    public VehicleListItem(
        int id,
        string ownerName,
        string manufacturerName,
        int yearOfManufacture,
        decimal weightKg,
        string categoryName,
        string categoryIconKey)
    {
        Id = id;
        OwnerName = ownerName;
        ManufacturerName = manufacturerName;
        YearOfManufacture = yearOfManufacture;
        WeightKg = weightKg;
        CategoryName = categoryName;
        CategoryIconKey = categoryIconKey;
    }

    public int Id { get; }
    public string OwnerName { get; }
    public string ManufacturerName { get; }
    public int YearOfManufacture { get; }
    public decimal WeightKg { get; }
    public string CategoryName { get; }
    public string CategoryIconKey { get; }
}
