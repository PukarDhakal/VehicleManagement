using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Domain.Categories;

public sealed class CategoryConfiguration
{
    private readonly List<VehicleCategory> _categories = [];

    private CategoryConfiguration()
    {
    }

    public int Id { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<VehicleCategory> Categories => _categories.AsReadOnly();

    public static CategoryConfiguration Create(IEnumerable<CategoryChange> changes)
    {
        var configuration = new CategoryConfiguration();
        configuration.ApplyChanges(changes);
        return configuration;
    }

    /// <summary>
    /// Validates the entire proposal before changing any category, allowing several
    /// boundaries to move together. Existing identities and instances are retained
    /// so persistence can update only the rows that changed.
    /// </summary>
    public bool ApplyChanges(IEnumerable<CategoryChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var proposedCategories = ValidateChanges(changes);
        var existingCategoriesById = _categories
            .Where(category => category.Id > 0)
            .ToDictionary(category => category.Id);

        foreach (var proposedCategory in proposedCategories)
        {
            if (proposedCategory.Id is int id && !existingCategoriesById.ContainsKey(id))
            {
                throw new DomainValidationException("A category being edited no longer exists in this configuration.");
            }
        }

        var nextCategories = new List<VehicleCategory>();
        var changed = false;

        foreach (var proposedCategory in proposedCategories)
        {
            if (proposedCategory.Id is int id)
            {
                var existingCategory = existingCategoriesById[id];
                if (!existingCategory.Matches(proposedCategory))
                {
                    existingCategory.Update(proposedCategory);
                    changed = true;
                }

                nextCategories.Add(existingCategory);
            }
            else
            {
                nextCategories.Add(new VehicleCategory(proposedCategory));
                changed = true;
            }
        }

        if (_categories.Count != nextCategories.Count)
        {
            changed = true;
        }

        if (changed)
        {
            _categories.Clear();
            _categories.AddRange(nextCategories);
        }

        return changed;
    }

    /// <summary>
    /// Uses inclusive minimum weights and an exclusive inferred upper boundary.
    /// Selecting by minimum rather than collection position also works when a
    /// database loads the categories in a different order.
    /// </summary>
    public VehicleCategory GetCategoryForWeight(decimal weightKg)
    {
        WeightRules.Validate(weightKg, allowZero: false);

        return _categories
            .Where(category => category.MinWeightKg <= weightKg)
            .MaxBy(category => category.MinWeightKg)
            ?? throw new DomainValidationException("The category configuration does not cover this weight.");
    }

    private static List<CategoryChange> ValidateChanges(IEnumerable<CategoryChange> changes)
    {
        var proposedCategories = new List<CategoryChange>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingIds = new HashSet<int>();

        foreach (var change in changes)
        {
            var proposedCategory = ValidateCategory(change);

            if (!names.Add(proposedCategory.Name))
            {
                throw new DomainValidationException("Category names must be unique.");
            }

            if (proposedCategory.Id is int id && !existingIds.Add(id))
            {
                throw new DomainValidationException("An existing category can appear only once.");
            }

            proposedCategories.Add(proposedCategory);
        }

        if (proposedCategories.Count == 0)
        {
            throw new DomainValidationException("At least one category is required.");
        }

        proposedCategories = proposedCategories.OrderBy(category => category.SortOrder).ToList();
        ValidateCategoryOrder(proposedCategories);
        return proposedCategories;
    }

    private static CategoryChange ValidateCategory(CategoryChange change)
    {
        if (change is null)
        {
            throw new DomainValidationException("A category change cannot be empty.");
        }

        if (change.Id is <= 0)
        {
            throw new DomainValidationException("An existing category ID must be greater than zero.");
        }

        var name = change.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            throw new DomainValidationException("Category names must contain between 1 and 100 characters.");
        }

        var iconKey = change.IconKey?.Trim();
        if (string.IsNullOrEmpty(iconKey) || iconKey.Length > 50)
        {
            throw new DomainValidationException("Category icon keys must contain between 1 and 50 characters.");
        }

        if (change.SortOrder <= 0)
        {
            throw new DomainValidationException("Category sort order must be greater than zero.");
        }

        WeightRules.Validate(change.MinWeightKg, allowZero: true);
        return change with { Name = name, IconKey = iconKey };
    }

    private static void ValidateCategoryOrder(IReadOnlyList<CategoryChange> proposedCategories)
    {
        if (proposedCategories[0].MinWeightKg != 0)
        {
            throw new DomainValidationException("The first category must start at zero to cover every valid weight.");
        }

        var previousCategory = proposedCategories[0];
        for (var index = 1; index < proposedCategories.Count; index++)
        {
            var currentCategory = proposedCategories[index];

            if (currentCategory.SortOrder == previousCategory.SortOrder)
            {
                throw new DomainValidationException("Category sort orders must be unique.");
            }

            if (currentCategory.MinWeightKg <= previousCategory.MinWeightKg)
            {
                throw new DomainValidationException("Minimum weights must strictly increase in category sort order.");
            }

            previousCategory = currentCategory;
        }
    }
}
