namespace VehicleManagement.Application.Categories;

public sealed class CategoryConfigurationConflictException : Exception
{
    private const string ConflictMessage =
        "The category configuration has changed. Reload it before saving your changes.";

    public CategoryConfigurationConflictException()
        : base(ConflictMessage)
    {
    }

    public CategoryConfigurationConflictException(Exception innerException)
        : base(ConflictMessage, innerException)
    {
    }
}
