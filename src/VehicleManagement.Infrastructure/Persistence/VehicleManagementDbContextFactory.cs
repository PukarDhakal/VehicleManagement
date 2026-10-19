using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VehicleManagement.Infrastructure.Persistence;

public sealed class VehicleManagementDbContextFactory : IDesignTimeDbContextFactory<VehicleManagementDbContext>
{
    public VehicleManagementDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__VehicleManagement");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Migration generation needs a provider configuration but does not open a connection.
            connectionString = "Server=localhost;Database=VehicleManagement;Integrated Security=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<VehicleManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new VehicleManagementDbContext(options);
    }
}
