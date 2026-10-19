using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VehicleManagement.Domain.Categories;
using VehicleManagement.Domain.Vehicles;
using VehicleManagement.Infrastructure.Persistence;

namespace VehicleManagement.Integration.Tests.Persistence;

public sealed class PersistenceModelTests
{
    [Fact]
    public void CategoryConfigurationUsesDatabaseRowVersion()
    {
        using var context = CreateContext();
        var configuration = context.Model.FindEntityType(typeof(CategoryConfiguration));
        var rowVersion = configuration!.FindProperty(nameof(CategoryConfiguration.RowVersion));

        Assert.NotNull(rowVersion);
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Equal("rowversion", rowVersion.GetColumnType());
    }

    [Fact]
    public void VehicleAndCategoryWeightsUseDecimalTenTwo()
    {
        using var context = CreateContext();
        var vehicle = context.Model.FindEntityType(typeof(Vehicle));
        var category = context.Model.FindEntityType(typeof(VehicleCategory));

        Assert.Equal("decimal(10,2)", vehicle!.FindProperty(nameof(Vehicle.WeightKg))!.GetColumnType());
        Assert.Equal("decimal(10,2)", category!.FindProperty(nameof(VehicleCategory.MinWeightKg))!.GetColumnType());
        Assert.Null(vehicle.FindProperty("CategoryId"));
    }

    [Fact]
    public void VehicleReferencesManufacturer()
    {
        using var context = CreateContext();
        var vehicle = context.Model.FindEntityType(typeof(Vehicle));
        var manufacturerForeignKey = Assert.Single(vehicle!.GetForeignKeys());

        Assert.Equal(typeof(Manufacturer), manufacturerForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Vehicle.ManufacturerId), Assert.Single(manufacturerForeignKey.Properties).Name);
    }

    [Fact]
    public void InitialCategoriesAndManufacturersAreSeeded()
    {
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var configuration = designTimeModel.FindEntityType(typeof(CategoryConfiguration));
        var category = designTimeModel.FindEntityType(typeof(VehicleCategory));
        var manufacturer = designTimeModel.FindEntityType(typeof(Manufacturer));

        Assert.Single(configuration!.GetSeedData());
        Assert.Equal(new[] { "Light", "Medium", "Heavy" },
            category!.GetSeedData().OrderBy(row => (int)row["SortOrder"]!).Select(row => (string)row["Name"]!));
        Assert.Equal(new[] { "Mazda", "Mercedes", "Honda", "Ferrari", "Toyota" },
            manufacturer!.GetSeedData().OrderBy(row => (int)row["Id"]!).Select(row => (string)row["Name"]!));
    }

    private static VehicleManagementDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VehicleManagementDbContext>()
            .UseSqlServer("Server=localhost;Database=VehicleManagement;Integrated Security=True")
            .Options;

        return new VehicleManagementDbContext(options);
    }
}
