using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Categories;
using VehicleManagement.Domain.Categories;

namespace VehicleManagement.Infrastructure.Persistence.Repositories;

public sealed class CategoryConfigurationRepository : ICategoryConfigurationRepository
{
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
}
