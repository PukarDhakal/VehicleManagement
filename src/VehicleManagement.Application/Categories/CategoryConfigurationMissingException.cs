namespace VehicleManagement.Application.Categories;

public sealed class CategoryConfigurationMissingException : Exception
{
    public CategoryConfigurationMissingException()
        : base("The category configuration has not been created.")
    {
    }
}
