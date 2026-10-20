using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Categories;
using VehicleManagement.Domain.Categories;
using VehicleManagement.Infrastructure.Persistence;
using VehicleManagement.Infrastructure.Persistence.Repositories;

namespace VehicleManagement.Integration.Tests.Persistence;

public sealed class CategoryConfigurationRepositorySqlServerTests
{
    [SqlServerFact]
    public async Task SavePreservesIdsAndDoesNotUpdateUnchangedRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE CategoryUpdateAudit (CategoryId int NOT NULL);
                """);
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER AuditCategoryUpdates ON VehicleCategories AFTER UPDATE AS
                BEGIN
                    INSERT INTO CategoryUpdateAudit (CategoryId) SELECT Id FROM inserted;
                END
                """);
        }

        var original = (await database.Repository.GetAsync())!;
        var categories = ByOrder(original);
        var saved = await database.Repository.SaveAsync(original.RowVersion, new[]
        {
            Change(categories[0]),
            Change(categories[1]) with { IconKey = "bus" },
            Change(categories[2])
        });

        Assert.Equal(categories.Select(category => category.Id), ByOrder(saved).Select(category => category.Id));
        Assert.False(original.RowVersion.SequenceEqual(saved.RowVersion));

        await using var verification = database.CreateContext();
        var updatedIds = await verification.Database
            .SqlQueryRaw<int>("SELECT CategoryId AS Value FROM CategoryUpdateAudit")
            .ToListAsync();
        Assert.Equal(new[] { categories[1].Id }, updatedIds);
    }

    [SqlServerFact]
    public async Task SaveSupportsNameOrderAndMinimumSwaps()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = (await database.Repository.GetAsync())!;
        var categories = ByOrder(original);

        var saved = await database.Repository.SaveAsync(original.RowVersion, new[]
        {
            Change(categories[0]),
            Change(categories[1]) with
            {
                Name = categories[2].Name,
                SortOrder = categories[2].SortOrder,
                MinWeightKg = categories[2].MinWeightKg
            },
            Change(categories[2]) with
            {
                Name = categories[1].Name,
                SortOrder = categories[1].SortOrder,
                MinWeightKg = categories[1].MinWeightKg
            }
        });

        var reloaded = (await database.Repository.GetAsync())!;
        Assert.Equal(categories[2].Id, ByOrder(reloaded)[1].Id);
        Assert.Equal(categories[1].Id, ByOrder(reloaded)[2].Id);
        Assert.Equal(saved.RowVersion, reloaded.RowVersion);
        Assert.Equal(categories[2].Id, reloaded.GetCategoryForWeight(500m).Id);
    }

    [SqlServerFact]
    public async Task SaveSupportsBoundaryMovesDeletionAndAddition()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = (await database.Repository.GetAsync())!;
        var categories = ByOrder(original);

        await database.Repository.SaveAsync(original.RowVersion, new[]
        {
            Change(categories[0]),
            Change(categories[1]) with { MinWeightKg = 600m },
            new CategoryChange(null, "Large", "truck", 3, 2600m)
        });

        var reloaded = (await database.Repository.GetAsync())!;
        var result = ByOrder(reloaded);
        Assert.Equal(3, result.Length);
        Assert.Equal(categories[0].Id, result[0].Id);
        Assert.Equal(categories[1].Id, result[1].Id);
        Assert.DoesNotContain(result, category => category.Id == categories[2].Id);
        Assert.True(result[2].Id > 0);
        Assert.Equal("Large", reloaded.GetCategoryForWeight(2600m).Name);
    }

    [SqlServerFact]
    public async Task InvalidAndOutdatedProposalsLeaveDatabaseUnchanged()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = (await database.Repository.GetAsync())!;
        var categories = ByOrder(original);

        await Assert.ThrowsAsync<VehicleManagement.Domain.DomainValidationException>(() =>
            database.Repository.SaveAsync(original.RowVersion, new[]
            {
                Change(categories[0]),
                Change(categories[1]) with { MinWeightKg = 0m },
                Change(categories[2])
            }));

        var afterInvalid = (await database.Repository.GetAsync())!;
        Assert.Equal(original.RowVersion, afterInvalid.RowVersion);

        await database.Repository.SaveAsync(original.RowVersion, new[]
        {
            Change(categories[0]),
            Change(categories[1]) with { Name = "Midweight" },
            Change(categories[2])
        });

        await Assert.ThrowsAsync<CategoryConfigurationConflictException>(() =>
            database.Repository.SaveAsync(original.RowVersion, new[]
            {
                Change(categories[0]),
                Change(categories[1]) with { Name = "Other" },
                Change(categories[2])
            }));

        var reloaded = (await database.Repository.GetAsync())!;
        Assert.Equal("Midweight", ByOrder(reloaded)[1].Name);
    }

    [SqlServerFact]
    public async Task DatabaseFailureRollsBackParentAndCategoryChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectCategoryUpdates ON VehicleCategories AFTER UPDATE AS
                BEGIN
                    THROW 51000, 'Injected category update failure', 1;
                END
                """);
        }

        var original = (await database.Repository.GetAsync())!;
        var categories = ByOrder(original);
        await Assert.ThrowsAsync<SqlException>(() =>
            database.Repository.SaveAsync(original.RowVersion, new[]
            {
                Change(categories[0]),
                Change(categories[1]) with { Name = "Midweight" },
                Change(categories[2])
            }));

        var reloaded = (await database.Repository.GetAsync())!;
        Assert.Equal(original.RowVersion, reloaded.RowVersion);
        Assert.Equal(original.Revision, reloaded.Revision);
        Assert.Equal("Medium", ByOrder(reloaded)[1].Name);
    }

    [SqlServerFact]
    public async Task NoChangeSaveDoesNotAdvanceVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = (await database.Repository.GetAsync())!;

        var saved = await database.Repository.SaveAsync(
            original.RowVersion,
            ByOrder(original).Select(Change).ToArray());

        Assert.Equal(original.RowVersion, saved.RowVersion);
        Assert.Equal(original.Revision, saved.Revision);
    }

    private static VehicleCategory[] ByOrder(CategoryConfiguration configuration) =>
        configuration.Categories.OrderBy(category => category.SortOrder).ToArray();

    private static CategoryChange Change(VehicleCategory category) =>
        new(category.Id, category.Name, category.IconKey, category.SortOrder, category.MinWeightKg);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<VehicleManagementDbContext> _options;

        private TestDatabase(DbContextOptions<VehicleManagementDbContext> options)
        {
            _options = options;
            Repository = new CategoryConfigurationRepository(new TestContextFactory(options));
        }

        public CategoryConfigurationRepository Repository { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var baseConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__VehicleManagement");
            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                throw new InvalidOperationException("A SQL Server test connection is required.");
            }

            var connection = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "VehicleManagement_Test_" + Guid.NewGuid().ToString("N"),
                Pooling = false
            };
            var options = new DbContextOptionsBuilder<VehicleManagementDbContext>()
                .UseSqlServer(connection.ConnectionString)
                .Options;
            var database = new TestDatabase(options);
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }

        public VehicleManagementDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestContextFactory : IDbContextFactory<VehicleManagementDbContext>
    {
        private readonly DbContextOptions<VehicleManagementDbContext> _options;

        public TestContextFactory(DbContextOptions<VehicleManagementDbContext> options)
        {
            _options = options;
        }

        public VehicleManagementDbContext CreateDbContext() => new(_options);

        public Task<VehicleManagementDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__VehicleManagement")))
        {
            Skip = "Set ConnectionStrings__VehicleManagement to run SQL Server tests.";
        }
    }
}
