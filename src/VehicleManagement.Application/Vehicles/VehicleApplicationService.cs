using VehicleManagement.Application.Categories;
using VehicleManagement.Domain;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Application.Vehicles;

public sealed class VehicleApplicationService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IManufacturerRepository _manufacturerRepository;
    private readonly ICategoryConfigurationRepository _categoryConfigurationRepository;
    private readonly TimeProvider _timeProvider;

    public VehicleApplicationService(
        IVehicleRepository vehicleRepository,
        IManufacturerRepository manufacturerRepository,
        ICategoryConfigurationRepository categoryConfigurationRepository,
        TimeProvider timeProvider)
    {
        _vehicleRepository = vehicleRepository;
        _manufacturerRepository = manufacturerRepository;
        _categoryConfigurationRepository = categoryConfigurationRepository;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<Manufacturer>> GetManufacturersAsync(
        CancellationToken cancellationToken = default)
    {
        return _manufacturerRepository.ListAsync(cancellationToken);
    }

    public async Task<int> CreateVehicleAsync(
        CreateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentYear = _timeProvider.GetUtcNow().Year;
        var vehicle = Vehicle.Create(
            request.OwnerName,
            request.ManufacturerId,
            request.YearOfManufacture,
            request.WeightKg,
            currentYear);

        var manufacturerExists = await _manufacturerRepository.ExistsAsync(
            vehicle.ManufacturerId,
            cancellationToken);

        if (!manufacturerExists)
        {
            throw new DomainValidationException("The selected manufacturer does not exist.");
        }

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        return vehicle.Id;
    }

    /// <summary>
    /// Loads one category configuration for the complete list so every vehicle is
    /// classified consistently without running a category query for each row.
    /// </summary>
    public async Task<IReadOnlyList<VehicleListItem>> GetVehiclesAsync(
        VehicleSortField sortField,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        var categoryConfiguration = await _categoryConfigurationRepository.GetAsync(cancellationToken);
        if (categoryConfiguration is null)
        {
            throw new CategoryConfigurationMissingException();
        }

        var records = await _vehicleRepository.ListAsync(sortField, descending, cancellationToken);
        var vehicles = new List<VehicleListItem>(records.Count);

        foreach (var record in records)
        {
            var category = categoryConfiguration.GetCategoryForWeight(record.WeightKg);
            vehicles.Add(new VehicleListItem(
                record.Id,
                record.OwnerName,
                record.ManufacturerName,
                record.YearOfManufacture,
                record.WeightKg,
                category.Name,
                category.IconKey));
        }

        return vehicles;
    }
}
