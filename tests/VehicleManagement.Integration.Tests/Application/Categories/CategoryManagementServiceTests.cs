using VehicleManagement.Application.Categories;
using VehicleManagement.Application.Vehicles;
using VehicleManagement.Domain.Categories;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Integration.Tests.Application.Categories;

public sealed class CategoryManagementServiceTests
{
    [Fact]
    public async Task SavingUnchangedCategoriesReportsNoChange()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository([550m]);
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;

        var result = await service.SaveAsync(original.RowVersion, Changes(original));

        Assert.False(result.ConfigurationChanged);
        Assert.False(result.RequiresConfirmation);
        Assert.Equal(0, result.AffectedVehicleCount);
        Assert.Equal(0, vehicles.ListWeightsCallCount);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public async Task SaveRequiresConfirmationOnlyAboveOneHundredAffectedVehicles(
        int vehicleCount,
        bool requiresConfirmation)
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository(Enumerable.Repeat(550m, vehicleCount));
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var changes = MoveMediumMinimum(original, 600m);

        var result = await service.SaveAsync(original.RowVersion, changes);

        Assert.Equal(vehicleCount, result.AffectedVehicleCount);
        Assert.True(result.ConfigurationChanged);
        Assert.Equal(requiresConfirmation, result.RequiresConfirmation);
        Assert.Equal(requiresConfirmation ? 0 : 1, categories.SaveCallCount);

        if (requiresConfirmation)
        {
            var confirmed = await service.SaveAsync(original.RowVersion, changes, result.ConfirmationToken);
            Assert.False(confirmed.RequiresConfirmation);
            Assert.Equal(1, categories.SaveCallCount);
        }
    }

    [Fact]
    public async Task RenameAndIconChangeDoNotCountAsReclassification()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository(Enumerable.Repeat(550m, 150));
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var changes = Changes(original);
        changes[1] = changes[1] with { Name = "Midweight", IconKey = "car" };

        var result = await service.SaveAsync(original.RowVersion, changes);

        Assert.False(result.RequiresConfirmation);
        Assert.Equal(0, result.AffectedVehicleCount);
        Assert.Equal(1, categories.SaveCallCount);
        Assert.Equal(0, vehicles.ListWeightsCallCount);
    }

    [Fact]
    public async Task MultipleBoundaryMovesAndDeletionCountEachVehicleOnce()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository([550m, 1000m, 2550m, 2700m]);
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var changes = Changes(original);
        changes[1] = changes[1] with { MinWeightKg = 600m };
        changes[2] = changes[2] with { MinWeightKg = 2600m };

        var moved = await service.SaveAsync(original.RowVersion, changes);

        Assert.Equal(2, moved.AffectedVehicleCount);
        var afterMove = moved.SavedConfiguration!;
        var withoutMedium = Changes(afterMove)
            .Where(change => change.Name != "Medium")
            .Select((change, index) => change with { SortOrder = index + 1 })
            .ToArray();

        var deleted = await service.SaveAsync(afterMove.RowVersion, withoutMedium);

        Assert.Equal(2, deleted.AffectedVehicleCount);
    }

    [Fact]
    public async Task AddingCategoryCountsVehiclesAssignedToIt()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository([1200m, 2600m]);
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var changes = Changes(original);

        var proposal = new[]
        {
            changes[0],
            changes[1],
            new CategoryChange(null, "Large", "truck", 3, 1000m),
            changes[2] with { SortOrder = 4 }
        };

        var result = await service.SaveAsync(original.RowVersion, proposal);

        Assert.Equal(1, result.AffectedVehicleCount);
        Assert.Equal("Large", result.SavedConfiguration!.GetCategoryForWeight(1200m).Name);
    }

    [Fact]
    public async Task ConfirmationIsBoundToProposalAndCanBeCancelled()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository(Enumerable.Repeat(550m, 101));
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var firstProposal = MoveMediumMinimum(original, 600m);
        var first = await service.SaveAsync(original.RowVersion, firstProposal);

        var changedProposal = MoveMediumMinimum(original, 700m);
        var changed = await service.SaveAsync(original.RowVersion, changedProposal, first.ConfirmationToken);

        Assert.True(changed.RequiresConfirmation);
        Assert.NotEqual(first.ConfirmationToken, changed.ConfirmationToken);
        Assert.Equal(0, categories.SaveCallCount);

        service.CancelPendingConfirmation();
        var cancelled = await service.SaveAsync(original.RowVersion, changedProposal, changed.ConfirmationToken);

        Assert.True(cancelled.RequiresConfirmation);
        Assert.NotEqual(changed.ConfirmationToken, cancelled.ConfirmationToken);
        Assert.Equal(0, categories.SaveCallCount);
    }

    [Fact]
    public async Task ConfirmationRechecksConcurrencyAndCurrentVehicleCount()
    {
        var categories = new FakeCategoryRepository();
        var vehicles = new FakeVehicleRepository(Enumerable.Repeat(550m, 101));
        var service = new CategoryManagementService(categories, vehicles);
        var original = (await service.GetAsync())!;
        var changes = MoveMediumMinimum(original, 600m);
        var preview = await service.SaveAsync(original.RowVersion, changes);

        vehicles.Weights.Add(550m);
        var confirmed = await service.SaveAsync(original.RowVersion, changes, preview.ConfirmationToken);
        Assert.Equal(102, confirmed.AffectedVehicleCount);

        var next = (await service.GetAsync())!;
        vehicles.Weights.Clear();
        vehicles.Weights.AddRange(Enumerable.Repeat(650m, 101));
        var secondProposal = MoveMediumMinimum(next, 700m);
        var secondPreview = await service.SaveAsync(next.RowVersion, secondProposal);
        Assert.True(secondPreview.RequiresConfirmation);
        categories.AdvanceVersion();

        await Assert.ThrowsAsync<CategoryConfigurationConflictException>(() =>
            service.SaveAsync(next.RowVersion, secondProposal, secondPreview.ConfirmationToken));
        Assert.Equal(1, categories.SaveCallCount);
    }

    private static CategoryChange[] MoveMediumMinimum(CategoryConfiguration configuration, decimal minimum)
    {
        var changes = Changes(configuration);
        changes[1] = changes[1] with { MinWeightKg = minimum };
        return changes;
    }

    private static CategoryChange[] Changes(CategoryConfiguration configuration) =>
        configuration.Categories
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategoryChange(
                category.Id, category.Name, category.IconKey, category.SortOrder, category.MinWeightKg))
            .ToArray();

    private sealed class FakeCategoryRepository : ICategoryConfigurationRepository
    {
        private CategoryChange[] _stored =
        [
            new(1, "Light", "car", 1, 0m),
            new(2, "Medium", "van", 2, 500m),
            new(3, "Heavy", "truck", 3, 2500m)
        ];
        private byte[] _version = [1, 0, 0, 0, 0, 0, 0, 0];
        private int _nextId = 4;

        public int SaveCallCount { get; private set; }

        public Task<CategoryConfiguration?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<CategoryConfiguration?>(CreateConfiguration());

        public Task<CategoryConfiguration> SaveAsync(
            byte[] expectedRowVersion,
            IReadOnlyList<CategoryChange> changes,
            CancellationToken cancellationToken = default)
        {
            if (!expectedRowVersion.SequenceEqual(_version))
            {
                throw new CategoryConfigurationConflictException();
            }

            var configuration = CreateConfiguration();
            var changed = configuration.ApplyChanges(changes);
            if (changed)
            {
                foreach (var category in configuration.Categories.Where(category => category.Id == 0))
                {
                    typeof(VehicleCategory).GetProperty(nameof(VehicleCategory.Id))!
                        .SetValue(category, _nextId++);
                }

                _stored = Changes(configuration);
                AdvanceVersion();
                typeof(CategoryConfiguration).GetProperty(nameof(CategoryConfiguration.RowVersion))!
                    .SetValue(configuration, _version.ToArray());
            }

            SaveCallCount++;
            return Task.FromResult(configuration);
        }

        public void AdvanceVersion() => _version[0]++;

        private CategoryConfiguration CreateConfiguration()
        {
            var configuration = CategoryConfiguration.Create(
                _stored.Select(change => change with { Id = null }));
            var categories = configuration.Categories.OrderBy(category => category.SortOrder).ToArray();
            var stored = _stored.OrderBy(change => change.SortOrder).ToArray();

            for (var index = 0; index < categories.Length; index++)
            {
                typeof(VehicleCategory).GetProperty(nameof(VehicleCategory.Id))!
                    .SetValue(categories[index], stored[index].Id);
            }

            typeof(CategoryConfiguration).GetProperty(nameof(CategoryConfiguration.RowVersion))!
                .SetValue(configuration, _version.ToArray());
            return configuration;
        }
    }

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        public FakeVehicleRepository(IEnumerable<decimal> weights)
        {
            Weights = weights.ToList();
        }

        public List<decimal> Weights { get; }
        public int ListWeightsCallCount { get; private set; }

        public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<VehicleListRecord>> ListAsync(
            VehicleSortField sortField,
            bool descending,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<decimal>> ListWeightsAsync(
            CancellationToken cancellationToken = default)
        {
            ListWeightsCallCount++;
            return Task.FromResult<IReadOnlyList<decimal>>(Weights.ToArray());
        }
    }
}
