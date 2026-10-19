namespace VehicleManagement.Domain.Categories;

public sealed record CategoryChange(
    int? Id,
    string Name,
    string IconKey,
    int SortOrder,
    decimal MinWeightKg);
