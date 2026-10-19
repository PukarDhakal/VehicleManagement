using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Application.Vehicles;

public interface IVehicleRepository
{
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
}
