using VehicleManagement.Domain;
using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Domain.Tests.Categories;

public sealed class CategoryConfigurationTests
{
    [Theory]
    [InlineData("0.01", "Light")]
    [InlineData("499.99", "Light")]
    [InlineData("500.00", "Medium")]
    [InlineData("500.01", "Medium")]
    [InlineData("2499.99", "Medium")]
    [InlineData("2500.00", "Heavy")]
    [InlineData("2500.01", "Heavy")]
    [InlineData("99999999.99", "Heavy")]
    public void GetCategoryForWeightUsesInclusiveMinimum(string weight, string expectedName)
    {
        var configuration = CategoryConfiguration.Create(InitialChanges());

        var category = configuration.GetCategoryForWeight(ParseWeight(weight));

        Assert.Equal(expectedName, category.Name);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("100000000")]
    public void GetCategoryForWeightRejectsInvalidWeight(string weight)
    {
        var configuration = CategoryConfiguration.Create(InitialChanges());

        Assert.Throws<DomainValidationException>(() =>
            configuration.GetCategoryForWeight(ParseWeight(weight)));
    }

    [Fact]
    public void CreateOrdersBySortOrderRatherThanInputPosition()
    {
        var configuration = CategoryConfiguration.Create(InitialChanges().Reverse());

        Assert.Equal(new[] { "Light", "Medium", "Heavy" },
            configuration.Categories.Select(category => category.Name));
    }

    [Fact]
    public void OneCategoryCoversEveryValidWeight()
    {
        var configuration = CategoryConfiguration.Create(
            [new CategoryChange(null, "All vehicles", "vehicle", 1, 0)]);

        Assert.Same(configuration.Categories.Single(), configuration.GetCategoryForWeight(0.01m));
        Assert.Same(configuration.Categories.Single(), configuration.GetCategoryForWeight(99_999_999.99m));
    }

    [Fact]
    public void CreateTrimsNamesAndIconKeys()
    {
        var configuration = CategoryConfiguration.Create(
            [new CategoryChange(null, "  Light  ", "  car  ", 1, 0)]);

        var category = configuration.Categories.Single();
        Assert.Equal("Light", category.Name);
        Assert.Equal("car", category.IconKey);
    }

    [Fact]
    public void CreateAcceptsMaximumLengthsAndDecimalTrailingZeroes()
    {
        var configuration = CategoryConfiguration.Create(
        [
            new CategoryChange(null, new string('a', 100), new string('b', 50), 1, 0),
            new CategoryChange(null, "Second", "truck", 2, 500.000m)
        ]);

        Assert.Equal("Second", configuration.GetCategoryForWeight(500.000m).Name);
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        yield return [Array.Empty<CategoryChange>()];
        yield return [new CategoryChange[] { null! }];
        yield return [new[] { new CategoryChange(null, "Light", "car", 1, 1) }];
        yield return [new[] { new CategoryChange(null, "Light", "car", 1, -1) }];
        yield return [new[] { new CategoryChange(null, "", "car", 1, 0) }];
        yield return [new[] { new CategoryChange(null, " ", "car", 1, 0) }];
        yield return [new[] { new CategoryChange(null, null!, "car", 1, 0) }];
        yield return [new[] { new CategoryChange(null, new string('a', 101), "car", 1, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", "", 1, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", " ", 1, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", null!, 1, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", new string('a', 51), 1, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", "car", 0, 0) }];
        yield return [new[] { new CategoryChange(null, "Light", "car", -1, 0) }];
        yield return [new[] { new CategoryChange(0, "Light", "car", 1, 0) }];
        yield return [new[] { new CategoryChange(-1, "Light", "car", 1, 0) }];
        yield return [new[] { new CategoryChange(42, "Light", "car", 1, 0) }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, " light ", "truck", 2, 500)
        }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, "Medium", "truck", 1, 500)
        }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, "Medium", "truck", 2, 0)
        }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, "Medium", "truck", 2, 2500),
            new CategoryChange(null, "Heavy", "lorry", 3, 500)
        }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, "Medium", "truck", 2, 500.001m)
        }];
        yield return [new[]
        {
            new CategoryChange(null, "Light", "car", 1, 0),
            new CategoryChange(null, "Medium", "truck", 2, 100_000_000m)
        }];
    }

    [Theory]
    [MemberData(nameof(InvalidConfigurations))]
    public void CreateRejectsInvalidConfigurations(CategoryChange[] changes)
    {
        Assert.Throws<DomainValidationException>(() => CategoryConfiguration.Create(changes));
    }

    [Fact]
    public void ApplyChangesPreservesExistingIdentityAndReclassifiesWeight()
    {
        var configuration = CreateStoredConfiguration();
        var heavy = configuration.Categories.Single(category => category.Id == 3);
        var light = configuration.Categories.Single(category => category.Id == 1);
        var changes = CurrentChanges(configuration);
        changes[2] = changes[2] with { MinWeightKg = 2000 };

        var changed = configuration.ApplyChanges(changes);

        Assert.True(changed);
        Assert.Same(heavy, configuration.GetCategoryForWeight(2200));
        Assert.Equal(3, heavy.Id);
        Assert.Same(light, configuration.GetCategoryForWeight(400));
    }

    [Fact]
    public void ApplyChangesValidatesFinalStateWhenSeveralBoundariesMove()
    {
        var configuration = CreateStoredConfiguration();
        var changes = CurrentChanges(configuration);
        changes[1] = changes[1] with { MinWeightKg = 3000 };
        changes[2] = changes[2] with { MinWeightKg = 4000 };

        configuration.ApplyChanges(changes);

        Assert.Equal("Light", configuration.GetCategoryForWeight(2999.99m).Name);
        Assert.Equal("Medium", configuration.GetCategoryForWeight(3000).Name);
        Assert.Equal("Heavy", configuration.GetCategoryForWeight(4000).Name);
    }

    [Fact]
    public void InvalidEditDoesNotPartiallyChangeExistingObjects()
    {
        var configuration = CreateStoredConfiguration();
        var originalObjects = configuration.Categories.ToArray();
        var originalValues = CurrentChanges(configuration);
        var changes = CurrentChanges(configuration);
        changes[0] = changes[0] with { Name = "Renamed", IconKey = "new-icon" };
        changes[2] = changes[2] with { MinWeightKg = 400 };

        Assert.Throws<DomainValidationException>(() => configuration.ApplyChanges(changes));

        Assert.Equal(originalValues, CurrentChanges(configuration));
        Assert.Equal(originalObjects, configuration.Categories);
    }

    [Fact]
    public void UnknownIdentityDoesNotPartiallyApplyEarlierEdits()
    {
        var configuration = CreateStoredConfiguration();
        var original = CurrentChanges(configuration);
        var changes = CurrentChanges(configuration);
        changes[0] = changes[0] with { Name = "Renamed" };
        changes[2] = changes[2] with { Id = 99 };

        Assert.Throws<DomainValidationException>(() => configuration.ApplyChanges(changes));

        Assert.Equal(original, CurrentChanges(configuration));
    }

    [Fact]
    public void RepeatedIdentityIsRejectedWithoutChanges()
    {
        var configuration = CreateStoredConfiguration();
        var original = CurrentChanges(configuration);
        var changes = CurrentChanges(configuration);
        changes[2] = changes[2] with { Id = 2 };

        Assert.Throws<DomainValidationException>(() => configuration.ApplyChanges(changes));

        Assert.Equal(original, CurrentChanges(configuration));
    }

    [Fact]
    public void ApplyChangesAddsCategoryWithoutReplacingExistingObjects()
    {
        var configuration = CreateStoredConfiguration();
        var existing = configuration.Categories.ToArray();
        var changes = CurrentChanges(configuration).Append(
            new CategoryChange(null, "Very heavy", "large-truck", 4, 5000));

        Assert.True(configuration.ApplyChanges(changes));

        Assert.Equal(4, configuration.Categories.Count);
        Assert.All(existing, category => Assert.Contains(category, configuration.Categories));
        Assert.Equal("Very heavy", configuration.GetCategoryForWeight(5000).Name);
        Assert.Equal(0, configuration.GetCategoryForWeight(5000).Id);
    }

    [Fact]
    public void DeletingMiddleCategoryExtendsPreviousRangeWithoutAGap()
    {
        var configuration = CreateStoredConfiguration();

        configuration.ApplyChanges(CurrentChanges(configuration).Where(change => change.Id != 2));

        Assert.Equal(2, configuration.Categories.Count);
        Assert.Equal("Light", configuration.GetCategoryForWeight(2000).Name);
        Assert.Equal("Heavy", configuration.GetCategoryForWeight(2500).Name);
    }

    [Fact]
    public void DeletingFirstCategoryRequiresMovingNewFirstMinimumToZero()
    {
        var configuration = CreateStoredConfiguration();
        var changes = CurrentChanges(configuration).Where(change => change.Id != 1).ToArray();

        Assert.Throws<DomainValidationException>(() => configuration.ApplyChanges(changes));
        changes[0] = changes[0] with { MinWeightKg = 0 };
        configuration.ApplyChanges(changes);

        Assert.Equal("Medium", configuration.GetCategoryForWeight(0.01m).Name);
    }

    [Fact]
    public void DeletingAllCategoriesLeavesConfigurationUnchanged()
    {
        var configuration = CreateStoredConfiguration();
        var original = CurrentChanges(configuration);

        Assert.Throws<DomainValidationException>(() => configuration.ApplyChanges([]));

        Assert.Equal(original, CurrentChanges(configuration));
    }

    [Fact]
    public void UnchangedProposalReturnsFalseAndPreservesObjects()
    {
        var configuration = CreateStoredConfiguration();
        var originalObjects = configuration.Categories.ToArray();

        var changed = configuration.ApplyChanges(CurrentChanges(configuration).Reverse());

        Assert.False(changed);
        Assert.Equal(originalObjects, configuration.Categories);
    }

    [Fact]
    public void RenameAndIconChangePreserveCategoryIdentity()
    {
        var configuration = CreateStoredConfiguration();
        var original = configuration.GetCategoryForWeight(600);
        var changes = CurrentChanges(configuration);
        changes[1] = changes[1] with { Name = "Standard", IconKey = "van" };

        Assert.True(configuration.ApplyChanges(changes));

        Assert.Same(original, configuration.GetCategoryForWeight(600));
        Assert.Equal("Standard", original.Name);
        Assert.Equal("van", original.IconKey);
    }

    private static CategoryChange[] InitialChanges() =>
    [
        new(null, "Light", "car", 1, 0),
        new(null, "Medium", "van", 2, 500),
        new(null, "Heavy", "truck", 3, 2500)
    ];

    private static CategoryChange[] CurrentChanges(CategoryConfiguration configuration) =>
        configuration.Categories
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategoryChange(category.Id, category.Name, category.IconKey,
                category.SortOrder, category.MinWeightKg))
            .ToArray();

    private static CategoryConfiguration CreateStoredConfiguration()
    {
        var configuration = CategoryConfiguration.Create(InitialChanges());
        var nextId = 1;
        foreach (var category in configuration.Categories)
        {
            // Simulate database assigned identities without adding public setters to business objects.
            typeof(VehicleCategory).GetProperty(nameof(VehicleCategory.Id))!.SetValue(category, nextId++);
        }

        return configuration;
    }

    private static decimal ParseWeight(string weight) =>
        decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
}
