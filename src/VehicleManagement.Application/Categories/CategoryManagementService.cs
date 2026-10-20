using VehicleManagement.Application.Vehicles;
using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Application.Categories;

public sealed class CategoryManagementService
{
    private const int ConfirmationThreshold = 100;

    private readonly ICategoryConfigurationRepository _categoryRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private PendingConfirmation? _pendingConfirmation;

    public CategoryManagementService(
        ICategoryConfigurationRepository categoryRepository,
        IVehicleRepository vehicleRepository)
    {
        _categoryRepository = categoryRepository;
        _vehicleRepository = vehicleRepository;
    }

    public Task<CategoryConfiguration?> GetAsync(CancellationToken cancellationToken = default) =>
        _categoryRepository.GetAsync(cancellationToken);

    public void CancelPendingConfirmation() => _pendingConfirmation = null;

    public async Task<CategorySaveResult> SaveAsync(
        byte[] expectedRowVersion,
        IReadOnlyList<CategoryChange> changes,
        Guid? confirmationToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);
        ArgumentNullException.ThrowIfNull(changes);

        var proposal = changes.ToArray();
        var current = await _categoryRepository.GetAsync(cancellationToken)
            ?? throw new CategoryConfigurationMissingException();

        if (!current.RowVersion.SequenceEqual(expectedRowVersion))
        {
            _pendingConfirmation = null;
            throw new CategoryConfigurationConflictException();
        }

        var before = GetBoundaries(current);
        var changed = current.ApplyChanges(proposal);
        var affectedCount = 0;

        if (changed)
        {
            var after = GetBoundaries(current);
            if (!before.SequenceEqual(after))
            {
                var weights = await _vehicleRepository.ListWeightsAsync(cancellationToken);
                affectedCount = weights.Count(weight =>
                    GetCategoryId(before, weight) != GetCategoryId(after, weight));
            }
        }

        if (affectedCount > ConfirmationThreshold &&
            !MatchesPendingConfirmation(confirmationToken, expectedRowVersion, proposal))
        {
            var token = Guid.NewGuid();
            _pendingConfirmation = new PendingConfirmation(
                token,
                expectedRowVersion.ToArray(),
                proposal);
            return new CategorySaveResult(null, affectedCount, token, changed);
        }

        _pendingConfirmation = null;
        var saved = await _categoryRepository.SaveAsync(expectedRowVersion, proposal, cancellationToken);
        return new CategorySaveResult(saved, affectedCount, null, changed);
    }

    private bool MatchesPendingConfirmation(
        Guid? token,
        byte[] expectedRowVersion,
        IReadOnlyList<CategoryChange> proposal) =>
        token.HasValue &&
        _pendingConfirmation is { } pending &&
        token.Value == pending.Token &&
        expectedRowVersion.SequenceEqual(pending.RowVersion) &&
        proposal.SequenceEqual(pending.Proposal);

    private static CategoryBoundary[] GetBoundaries(CategoryConfiguration configuration) =>
        configuration.Categories
            .OrderBy(category => category.MinWeightKg)
            .Select(category => new CategoryBoundary(category.Id, category.MinWeightKg))
            .ToArray();

    private static int GetCategoryId(IReadOnlyList<CategoryBoundary> boundaries, decimal weightKg)
    {
        for (var index = boundaries.Count - 1; index >= 0; index--)
        {
            if (weightKg >= boundaries[index].MinimumWeightKg)
            {
                return boundaries[index].Id;
            }
        }

        throw new InvalidOperationException("The category configuration does not cover a vehicle weight.");
    }

    private sealed record CategoryBoundary(int Id, decimal MinimumWeightKg);

    private sealed record PendingConfirmation(
        Guid Token,
        byte[] RowVersion,
        CategoryChange[] Proposal);
}
