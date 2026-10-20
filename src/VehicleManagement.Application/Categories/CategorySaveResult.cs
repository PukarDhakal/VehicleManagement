using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Application.Categories;

public sealed record CategorySaveResult(
    CategoryConfiguration? SavedConfiguration,
    int AffectedVehicleCount,
    Guid? ConfirmationToken,
    bool ConfigurationChanged)
{
    public bool RequiresConfirmation => ConfirmationToken.HasValue;
}
