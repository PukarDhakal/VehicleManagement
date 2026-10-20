using VehicleManagement.Application.Categories;
using VehicleManagement.Application.Vehicles;
using VehicleManagement.Domain;
using VehicleManagement.Domain.Categories;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Integration.Tests.Application.Vehicles;

public sealed class VehicleApplicationServiceTests
{
    [Fact]
    public async Task CreateVehicleAsyncValidatesManufacturerAndSavesVehicle()
    {
        var vehicleRepository = new FakeVehicleRepository();
        var manufacturerRepository = new FakeManufacturerRepository(existingManufacturerId: 2);
        var service = CreateService(vehicleRepository, manufacturerRepository);
        var request = new CreateVehicleRequest("  Alex Driver  ", 2, 2020, 1200.50m);

        var vehicleId = await service.CreateVehicleAsync(request);

        Assert.Equal(42, vehicleId);
        Assert.Equal(2, manufacturerRepository.CheckedManufacturerId);
        Assert.NotNull(vehicleRepository.AddedVehicle);
        Assert.Equal("Alex Driver", vehicleRepository.AddedVehicle.OwnerName);
        Assert.Equal(2020, vehicleRepository.AddedVehicle.YearOfManufacture);
        Assert.Equal(1200.50m, vehicleRepository.AddedVehicle.WeightKg);
    }

    [Fact]
    public async Task CreateVehicleAsyncRejectsUnknownManufacturer()
    {
        var vehicleRepository = new FakeVehicleRepository();
        var manufacturerRepository = new FakeManufacturerRepository(existingManufacturerId: null);
        var service = CreateService(vehicleRepository, manufacturerRepository);
        var request = new CreateVehicleRequest("Alex Driver", 99, 2020, 1200m);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateVehicleAsync(request));

        Assert.Equal("The selected manufacturer does not exist.", exception.Message);
        Assert.Null(vehicleRepository.AddedVehicle);
    }

    [Fact]
    public async Task CreateVehicleAsyncUsesTheSuppliedTimeProvider()
    {
        var vehicleRepository = new FakeVehicleRepository();
        var manufacturerRepository = new FakeManufacturerRepository(existingManufacturerId: 1);
        var service = CreateService(vehicleRepository, manufacturerRepository, currentYear: 2026);
        var request = new CreateVehicleRequest("Alex Driver", 1, 2028, 1200m);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateVehicleAsync(request));

        Assert.Null(manufacturerRepository.CheckedManufacturerId);
        Assert.Null(vehicleRepository.AddedVehicle);
    }

    [Fact]
    public async Task GetVehiclesAsyncClassifiesEveryVehicleUsingOneConfiguration()
    {
        var records = new List<VehicleListRecord>
        {
            new VehicleListRecord(1, "Alex", "Mazda", 2020, 499.99m),
            new VehicleListRecord(2, "Blair", "Honda", 2021, 500m),
            new VehicleListRecord(3, "Casey", "Toyota", 2022, 2500m)
        };
        var vehicleRepository = new FakeVehicleRepository(records);
        var manufacturerRepository = new FakeManufacturerRepository(existingManufacturerId: null);
        var categoryRepository = new FakeCategoryConfigurationRepository(CreateCategoryConfiguration());
        var service = CreateService(vehicleRepository, manufacturerRepository, categoryRepository);

        var vehicles = await service.GetVehiclesAsync(VehicleSortField.WeightKg, descending: true);

        Assert.Equal(new[] { "Light", "Medium", "Heavy" }, vehicles.Select(vehicle => vehicle.CategoryName));
        Assert.Equal(new[] { "car", "van", "truck" }, vehicles.Select(vehicle => vehicle.CategoryIconKey));
        Assert.Equal(1, categoryRepository.GetCallCount);
        Assert.Equal(VehicleSortField.WeightKg, vehicleRepository.RequestedSortField);
        Assert.True(vehicleRepository.RequestedDescending);
    }

    [Fact]
    public async Task GetVehiclesAsyncRequiresCategoryConfiguration()
    {
        var vehicleRepository = new FakeVehicleRepository();
        var manufacturerRepository = new FakeManufacturerRepository(existingManufacturerId: null);
        var categoryRepository = new FakeCategoryConfigurationRepository(configuration: null);
        var service = CreateService(vehicleRepository, manufacturerRepository, categoryRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetVehiclesAsync(VehicleSortField.OwnerName, descending: false));

        Assert.Equal("The category configuration has not been created.", exception.Message);
        Assert.Equal(0, vehicleRepository.ListCallCount);
    }

    private static VehicleApplicationService CreateService(
        FakeVehicleRepository vehicleRepository,
        FakeManufacturerRepository manufacturerRepository,
        FakeCategoryConfigurationRepository? categoryRepository = null,
        int currentYear = 2026)
    {
        categoryRepository ??= new FakeCategoryConfigurationRepository(CreateCategoryConfiguration());

        return new VehicleApplicationService(
            vehicleRepository,
            manufacturerRepository,
            categoryRepository,
            new FixedTimeProvider(currentYear));
    }

    private static CategoryConfiguration CreateCategoryConfiguration()
    {
        return CategoryConfiguration.Create(new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0m),
            new CategoryChange(null, "Medium", "van", 2, 500m),
            new CategoryChange(null, "Heavy", "truck", 3, 2500m)
        });
    }

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        private readonly IReadOnlyList<VehicleListRecord> _records;

        public FakeVehicleRepository(IReadOnlyList<VehicleListRecord>? records = null)
        {
            _records = records ?? Array.Empty<VehicleListRecord>();
        }

        public Vehicle? AddedVehicle { get; private set; }
        public VehicleSortField? RequestedSortField { get; private set; }
        public bool RequestedDescending { get; private set; }
        public int ListCallCount { get; private set; }

        public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            typeof(Vehicle).GetProperty(nameof(Vehicle.Id))!.SetValue(vehicle, 42);
            AddedVehicle = vehicle;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VehicleListRecord>> ListAsync(
            VehicleSortField sortField,
            bool descending,
            CancellationToken cancellationToken = default)
        {
            RequestedSortField = sortField;
            RequestedDescending = descending;
            ListCallCount++;
            return Task.FromResult(_records);
        }
    }

    private sealed class FakeManufacturerRepository : IManufacturerRepository
    {
        private readonly int? _existingManufacturerId;

        public FakeManufacturerRepository(int? existingManufacturerId)
        {
            _existingManufacturerId = existingManufacturerId;
        }

        public int? CheckedManufacturerId { get; private set; }

        public Task<IReadOnlyList<Manufacturer>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Manufacturer> manufacturers = Array.Empty<Manufacturer>();
            return Task.FromResult(manufacturers);
        }

        public Task<bool> ExistsAsync(
            int manufacturerId,
            CancellationToken cancellationToken = default)
        {
            CheckedManufacturerId = manufacturerId;
            return Task.FromResult(_existingManufacturerId == manufacturerId);
        }
    }

    private sealed class FakeCategoryConfigurationRepository : ICategoryConfigurationRepository
    {
        private readonly CategoryConfiguration? _configuration;

        public FakeCategoryConfigurationRepository(CategoryConfiguration? configuration)
        {
            _configuration = configuration;
        }

        public int GetCallCount { get; private set; }

        public Task<CategoryConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(_configuration);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(int currentYear)
        {
            _utcNow = new DateTimeOffset(currentYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
