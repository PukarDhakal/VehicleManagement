using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Vehicles;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly IDbContextFactory<VehicleManagementDbContext> _contextFactory;

    public VehicleRepository(IDbContextFactory<VehicleManagementDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Joins vehicles to manufacturers in one query and only accepts known sort fields.
    /// The ID tie breaker keeps rows in a stable order when primary values match.
    /// </summary>
    public async Task<IReadOnlyList<VehicleListRecord>> ListAsync(
        VehicleSortField sortField,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var vehicleRows =
            from vehicle in context.Vehicles.AsNoTracking()
            join manufacturer in context.Manufacturers.AsNoTracking()
                on vehicle.ManufacturerId equals manufacturer.Id
            select new VehicleQueryRow
            {
                Id = vehicle.Id,
                OwnerName = vehicle.OwnerName,
                ManufacturerName = manufacturer.Name,
                YearOfManufacture = vehicle.YearOfManufacture,
                WeightKg = vehicle.WeightKg
            };

        IOrderedQueryable<VehicleQueryRow> orderedRows;

        switch (sortField)
        {
            case VehicleSortField.OwnerName:
                orderedRows = descending
                    ? vehicleRows.OrderByDescending(row => row.OwnerName)
                    : vehicleRows.OrderBy(row => row.OwnerName);
                break;

            case VehicleSortField.Manufacturer:
                orderedRows = descending
                    ? vehicleRows.OrderByDescending(row => row.ManufacturerName)
                    : vehicleRows.OrderBy(row => row.ManufacturerName);
                break;

            case VehicleSortField.YearOfManufacture:
                orderedRows = descending
                    ? vehicleRows.OrderByDescending(row => row.YearOfManufacture)
                    : vehicleRows.OrderBy(row => row.YearOfManufacture);
                break;

            case VehicleSortField.WeightKg:
                orderedRows = descending
                    ? vehicleRows.OrderByDescending(row => row.WeightKg)
                    : vehicleRows.OrderBy(row => row.WeightKg);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(sortField), sortField, "Unknown vehicle sort field.");
        }

        var sortedRows = await orderedRows
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

        var records = new List<VehicleListRecord>(sortedRows.Count);

        foreach (var row in sortedRows)
        {
            records.Add(new VehicleListRecord(
                row.Id,
                row.OwnerName,
                row.ManufacturerName,
                row.YearOfManufacture,
                row.WeightKg));
        }

        return records;
    }

    private sealed class VehicleQueryRow
    {
        public int Id { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public int YearOfManufacture { get; set; }
        public decimal WeightKg { get; set; }
    }
}
