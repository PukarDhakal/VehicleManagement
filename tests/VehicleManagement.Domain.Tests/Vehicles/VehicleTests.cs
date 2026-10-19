using VehicleManagement.Domain;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Domain.Tests.Vehicles;

public sealed class VehicleTests
{
    [Fact]
    public void CreateStoresValidDetailsAndTrimsOwnerName()
    {
        var vehicle = Vehicle.Create("  Alex Driver  ", 2, 2020, 1200.50m, 2026);

        Assert.Equal("Alex Driver", vehicle.OwnerName);
        Assert.Equal(2, vehicle.ManufacturerId);
        Assert.Equal(2020, vehicle.YearOfManufacture);
        Assert.Equal(1200.50m, vehicle.WeightKg);
        Assert.Equal(0, vehicle.Id);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(1900)]
    [InlineData(2026)]
    [InlineData(2027)]
    public void CreateAcceptsManufactureYearBoundaries(int yearOfManufacture)
    {
        var vehicle = Vehicle.Create("Alex Driver", 1, yearOfManufacture, 1m, 2026);

        Assert.Equal(yearOfManufacture, vehicle.YearOfManufacture);
    }

    [Theory]
    [InlineData(1799)]
    [InlineData(2028)]
    public void CreateRejectsOutOfRangeManufactureYear(int yearOfManufacture)
    {
        Assert.Throws<DomainValidationException>(() =>
            Vehicle.Create("Alex Driver", 1, yearOfManufacture, 1m, 2026));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRejectsInvalidManufacturerId(int manufacturerId)
    {
        Assert.Throws<DomainValidationException>(() =>
            Vehicle.Create("Alex Driver", manufacturerId, 2020, 1m, 2026));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsMissingOwnerName(string? ownerName)
    {
        Assert.Throws<DomainValidationException>(() =>
            Vehicle.Create(ownerName!, 1, 2020, 1m, 2026));
    }

    [Fact]
    public void CreateAcceptsMaximumOwnerNameLengthAfterTrimming()
    {
        var ownerName = new string('a', 150);

        var vehicle = Vehicle.Create(" " + ownerName + " ", 1, 2020, 1m, 2026);

        Assert.Equal(ownerName, vehicle.OwnerName);
    }

    [Fact]
    public void CreateRejectsOwnerNameOverMaximumLength()
    {
        var ownerName = new string('a', 151);

        Assert.Throws<DomainValidationException>(() =>
            Vehicle.Create(ownerName, 1, 2020, 1m, 2026));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("100000000")]
    public void CreateRejectsInvalidWeight(string weight)
    {
        var weightKg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<DomainValidationException>(() =>
            Vehicle.Create("Alex Driver", 1, 2020, weightKg, 2026));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("99999999.99")]
    public void CreateAcceptsValidWeightBoundaries(string weight)
    {
        var weightKg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        var vehicle = Vehicle.Create("Alex Driver", 1, 2020, weightKg, 2026);

        Assert.Equal(weightKg, vehicle.WeightKg);
    }

    [Fact]
    public void CreateDoesNotStoreCategoryIdentity()
    {
        var vehicle = Vehicle.Create("Alex Driver", 1, 2020, 1200m, 2026);

        Assert.DoesNotContain(typeof(Vehicle).GetProperties(), property => property.Name == "CategoryId");
        Assert.Equal(1200m, vehicle.WeightKg);
    }
}
