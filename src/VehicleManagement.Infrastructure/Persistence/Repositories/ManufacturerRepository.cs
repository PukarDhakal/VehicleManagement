using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Vehicles;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Infrastructure.Persistence.Repositories;

public sealed class ManufacturerRepository : IManufacturerRepository
{
    private readonly IDbContextFactory<VehicleManagementDbContext> _contextFactory;

    public ManufacturerRepository(IDbContextFactory<VehicleManagementDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Manufacturer>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Manufacturers
            .AsNoTracking()
            .OrderBy(manufacturer => manufacturer.Name)
            .ThenBy(manufacturer => manufacturer.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        int manufacturerId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Manufacturers
            .AsNoTracking()
            .AnyAsync(manufacturer => manufacturer.Id == manufacturerId, cancellationToken);
    }
}
