namespace VehicleManagement.Domain.Categories;

public sealed class VehicleCategory
{
    private VehicleCategory()
    {
    }

    internal VehicleCategory(CategoryChange change)
    {
        Update(change);
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string IconKey { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public decimal MinWeightKg { get; private set; }

    internal void Update(CategoryChange change)
    {
        Name = change.Name;
        IconKey = change.IconKey;
        SortOrder = change.SortOrder;
        MinWeightKg = change.MinWeightKg;
    }

    internal bool Matches(CategoryChange change) =>
        Name == change.Name &&
        IconKey == change.IconKey &&
        SortOrder == change.SortOrder &&
        MinWeightKg == change.MinWeightKg;
}
