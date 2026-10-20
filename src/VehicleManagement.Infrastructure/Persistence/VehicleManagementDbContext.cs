using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleManagement.Domain.Categories;
using VehicleManagement.Domain.Vehicles;

namespace VehicleManagement.Infrastructure.Persistence;

public sealed class VehicleManagementDbContext : DbContext
{
    public VehicleManagementDbContext(DbContextOptions<VehicleManagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<CategoryConfiguration> CategoryConfigurations => Set<CategoryConfiguration>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>
    /// Defines how the domain objects are stored in SQL Server and adds the starting data.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCategoryConfiguration(modelBuilder.Entity<CategoryConfiguration>());
        ConfigureVehicleCategory(modelBuilder.Entity<VehicleCategory>());
        ConfigureManufacturer(modelBuilder.Entity<Manufacturer>());
        ConfigureVehicle(modelBuilder.Entity<Vehicle>());
    }

    /// <summary>
    /// Keeps categories under one configuration and uses RowVersion to detect conflicting saves.
    /// </summary>
    private static void ConfigureCategoryConfiguration(EntityTypeBuilder<CategoryConfiguration> builder)
    {
        builder.ToTable("CategoryConfigurations");
        builder.HasKey(configuration => configuration.Id);

        builder.Property(configuration => configuration.RowVersion)
            .IsRowVersion();

        builder.Property(configuration => configuration.Revision)
            .HasDefaultValue(0L);

        builder.HasMany(configuration => configuration.Categories)
            .WithOne()
            .HasForeignKey("CategoryConfigurationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(configuration => configuration.Categories)
            .HasField("_categories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(new { Id = 1 });
    }

    /// <summary>
    /// Stores category boundaries and prevents duplicate names, positions, or minimum weights.
    /// </summary>
    private static void ConfigureVehicleCategory(EntityTypeBuilder<VehicleCategory> builder)
    {
        builder.ToTable("VehicleCategories", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_VehicleCategories_SortOrder", "[SortOrder] > 0");
            table.HasCheckConstraint("CK_VehicleCategories_MinWeightKg", "[MinWeightKg] >= 0");
        });

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();

        builder.Property(category => category.IconKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(category => category.MinWeightKg)
            .HasColumnType("decimal(10,2)");

        builder.HasIndex("CategoryConfigurationId", nameof(VehicleCategory.Name))
            .IsUnique();

        builder.HasIndex("CategoryConfigurationId", nameof(VehicleCategory.SortOrder))
            .IsUnique();

        builder.HasIndex("CategoryConfigurationId", nameof(VehicleCategory.MinWeightKg))
            .IsUnique();

        builder.HasData(
            new { Id = 1, CategoryConfigurationId = 1, Name = "Light", IconKey = "car", SortOrder = 1, MinWeightKg = 0m },
            new { Id = 2, CategoryConfigurationId = 1, Name = "Medium", IconKey = "van", SortOrder = 2, MinWeightKg = 500m },
            new { Id = 3, CategoryConfigurationId = 1, Name = "Heavy", IconKey = "truck", SortOrder = 3, MinWeightKg = 2500m });
    }

    private static void ConfigureManufacturer(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("Manufacturers");
        builder.HasKey(manufacturer => manufacturer.Id);

        builder.Property(manufacturer => manufacturer.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(manufacturer => manufacturer.Name)
            .IsUnique();

        builder.HasData(
            new { Id = 1, Name = "Mazda" },
            new { Id = 2, Name = "Mercedes" },
            new { Id = 3, Name = "Honda" },
            new { Id = 4, Name = "Ferrari" },
            new { Id = 5, Name = "Toyota" });
    }

    private static void ConfigureVehicle(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", table =>
        {
            table.HasCheckConstraint("CK_Vehicles_OwnerName", "LEN(LTRIM(RTRIM([OwnerName]))) > 0");
            table.HasCheckConstraint("CK_Vehicles_YearOfManufacture", "[YearOfManufacture] >= 1800");
            table.HasCheckConstraint("CK_Vehicles_WeightKg", "[WeightKg] > 0");
        });

        builder.HasKey(vehicle => vehicle.Id);

        builder.Property(vehicle => vehicle.OwnerName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(vehicle => vehicle.WeightKg)
            .HasColumnType("decimal(10,2)");

        builder.HasOne<Manufacturer>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.ManufacturerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
