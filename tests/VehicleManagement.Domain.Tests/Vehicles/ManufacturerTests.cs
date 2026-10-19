using VehicleManagement.Domain;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Domain.Tests.Vehicles;

public sealed class ManufacturerTests
{
    [Fact]
    public void CreateTrimsName()
    {
        var manufacturer = Manufacturer.Create("  Mazda  ");

        Assert.Equal("Mazda", manufacturer.Name);
        Assert.Equal(0, manufacturer.Id);
    }

    [Fact]
    public void CreateAcceptsMaximumNameLength()
    {
        var name = new string('a', 100);

        var manufacturer = Manufacturer.Create(name);

        Assert.Equal(name, manufacturer.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsMissingName(string? name)
    {
        Assert.Throws<DomainValidationException>(() => Manufacturer.Create(name!));
    }

    [Fact]
    public void CreateRejectsNameOverMaximumLength()
    {
        var name = new string('a', 101);

        Assert.Throws<DomainValidationException>(() => Manufacturer.Create(name));
    }
}
