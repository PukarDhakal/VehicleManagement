using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Application.Vehicles;

public interface IManufacturerRepository
{
    Task<IReadOnlyList<Manufacturer>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int manufacturerId, CancellationToken cancellationToken = default);
}
