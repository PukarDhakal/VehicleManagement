using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Categories;
using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Infrastructure.Persistence.Repositories;

public sealed class CategoryConfigurationRepository : ICategoryConfigurationRepository
{
    private const decimal MaximumWeightKg = 99_999_999.99m;

    private readonly IDbContextFactory<VehicleManagementDbContext> _contextFactory;

    public CategoryConfigurationRepository(
        IDbContextFactory<VehicleManagementDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CategoryConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.CategoryConfigurations
            .AsNoTracking()
            .Include(configuration => configuration.Categories)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Validates a complete proposal, then saves it under the parent row version lock.
    /// Temporary unique values allow category names, positions and boundaries to swap
    /// without exposing an incomplete configuration outside the transaction.
    /// </summary>
    public async Task<CategoryConfiguration> SaveAsync(
        byte[] expectedRowVersion,
        IReadOnlyList<CategoryChange> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);
        ArgumentNullException.ThrowIfNull(changes);

        if (expectedRowVersion.Length != 8)
        {
            throw new ArgumentException("A valid category row version is required.", nameof(expectedRowVersion));
        }

        var proposedChanges = changes.ToArray();
        var originalRowVersion = expectedRowVersion.ToArray();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var currentConfiguration = await context.CategoryConfigurations
            .AsNoTracking()
            .Include(configuration => configuration.Categories)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The category configuration has not been created.");

        if (!currentConfiguration.RowVersion.SequenceEqual(originalRowVersion))
        {
            throw new CategoryConfigurationConflictException();
        }

        if (!currentConfiguration.ApplyChanges(proposedChanges))
        {
            return currentConfiguration;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var configuration = await context.CategoryConfigurations
            .Include(item => item.Categories)
            .SingleAsync(cancellationToken);

        if (!configuration.RowVersion.SequenceEqual(originalRowVersion))
        {
            throw new CategoryConfigurationConflictException();
        }

        var originalCategories = configuration.Categories
            .Select(category => new CategoryChange(
                category.Id,
                category.Name,
                category.IconKey,
                category.SortOrder,
                category.MinWeightKg))
            .ToArray();

        var entry = context.Entry(configuration);
        entry.Property(item => item.RowVersion).OriginalValue = originalRowVersion;
        entry.Property(item => item.Revision).CurrentValue = checked(configuration.Revision + 1);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CategoryConfigurationConflictException(exception);
        }

        if (!configuration.ApplyChanges(proposedChanges))
        {
            throw new CategoryConfigurationConflictException();
        }

        await StageChangedCategoriesAsync(
            context,
            configuration.Id,
            originalCategories,
            configuration.Categories,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return configuration;
    }

    /// <summary>
    /// Frees unique values held by edited or removed rows before EF writes the final
    /// values. Every staged value is absent from both the old and proposed states.
    /// </summary>
    private static async Task StageChangedCategoriesAsync(
        VehicleManagementDbContext context,
        int configurationId,
        IReadOnlyList<CategoryChange> originalCategories,
        IReadOnlyCollection<VehicleCategory> proposedCategories,
        CancellationToken cancellationToken)
    {
        var proposedById = proposedCategories
            .Where(category => category.Id > 0)
            .ToDictionary(category => category.Id);

        var usedNames = new HashSet<string>(
            originalCategories.Select(category => category.Name)
                .Concat(proposedCategories.Select(category => category.Name)),
            StringComparer.OrdinalIgnoreCase);
        var usedOrders = new HashSet<int>(
            originalCategories.Select(category => category.SortOrder)
                .Concat(proposedCategories.Select(category => category.SortOrder)));
        var usedMinimums = new HashSet<decimal>(
            originalCategories.Select(category => category.MinWeightKg)
                .Concat(proposedCategories.Select(category => category.MinWeightKg)));

        foreach (var original in originalCategories)
        {
            var existsInProposal = proposedById.TryGetValue(original.Id!.Value, out var proposed);
            var nameChanged = !existsInProposal || original.Name != proposed!.Name;
            var orderChanged = !existsInProposal || original.SortOrder != proposed!.SortOrder;
            var minimumChanged = !existsInProposal || original.MinWeightKg != proposed!.MinWeightKg;

            if (!nameChanged && !orderChanged && !minimumChanged)
            {
                continue;
            }

            var stagedName = nameChanged ? GetUnusedName(usedNames) : original.Name;
            var stagedOrder = orderChanged ? GetUnusedOrder(usedOrders) : original.SortOrder;
            var stagedMinimum = minimumChanged ? GetUnusedMinimum(usedMinimums) : original.MinWeightKg;

            var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [VehicleCategories] SET [Name] = {stagedName}, [SortOrder] = {stagedOrder}, [MinWeightKg] = {stagedMinimum} WHERE [Id] = {original.Id.Value} AND [CategoryConfigurationId] = {configurationId}",
                cancellationToken);

            if (affectedRows != 1)
            {
                throw new CategoryConfigurationConflictException();
            }

            // Raw SQL changed the database row, but EF still sees the old values.
            // Give EF the staged values so unique-index swaps have no dependency cycle.
            var tracked = context.ChangeTracker.Entries<VehicleCategory>()
                .Single(entry => entry.Entity.Id == original.Id.Value);
            if (nameChanged)
            {
                tracked.Property(category => category.Name).OriginalValue = stagedName;
            }

            if (orderChanged)
            {
                tracked.Property(category => category.SortOrder).OriginalValue = stagedOrder;
            }

            if (minimumChanged)
            {
                tracked.Property(category => category.MinWeightKg).OriginalValue = stagedMinimum;
            }
        }
    }

    private static string GetUnusedName(HashSet<string> usedNames)
    {
        while (true)
        {
            var name = "category_stage_" + Guid.NewGuid().ToString("N");
            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static int GetUnusedOrder(HashSet<int> usedOrders)
    {
        for (var order = 1; order < int.MaxValue; order++)
        {
            if (usedOrders.Add(order))
            {
                return order;
            }
        }

        throw new InvalidOperationException("No temporary category sort order is available.");
    }

    private static decimal GetUnusedMinimum(HashSet<decimal> usedMinimums)
    {
        for (var minimum = MaximumWeightKg; minimum >= 0; minimum -= 0.01m)
        {
            if (usedMinimums.Add(minimum))
            {
                return minimum;
            }
        }

        throw new InvalidOperationException("No temporary category minimum weight is available.");
    }
}
