using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Application.Categories;

public interface ICategoryConfigurationRepository
{
    Task<CategoryConfiguration?> GetAsync(CancellationToken cancellationToken = default);
}
