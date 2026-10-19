using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VehicleManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Manufacturers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IconKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MinWeightKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CategoryConfigurationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleCategories", x => x.Id);
                    table.CheckConstraint("CK_VehicleCategories_MinWeightKg", "[MinWeightKg] >= 0");
                    table.CheckConstraint("CK_VehicleCategories_SortOrder", "[SortOrder] > 0");
                    table.ForeignKey(
                        name: "FK_VehicleCategories_CategoryConfigurations_CategoryConfigurationId",
                        column: x => x.CategoryConfigurationId,
                        principalTable: "CategoryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ManufacturerId = table.Column<int>(type: "int", nullable: false),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_OwnerName", "LEN(LTRIM(RTRIM([OwnerName]))) > 0");
                    table.CheckConstraint("CK_Vehicles_WeightKg", "[WeightKg] > 0");
                    table.CheckConstraint("CK_Vehicles_YearOfManufacture", "[YearOfManufacture] >= 1800");
                    table.ForeignKey(
                        name: "FK_Vehicles_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CategoryConfigurations",
                column: "Id",
                value: 1);

            migrationBuilder.InsertData(
                table: "Manufacturers",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Mazda" },
                    { 2, "Mercedes" },
                    { 3, "Honda" },
                    { 4, "Ferrari" },
                    { 5, "Toyota" }
                });

            migrationBuilder.InsertData(
                table: "VehicleCategories",
                columns: new[] { "Id", "CategoryConfigurationId", "IconKey", "MinWeightKg", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "car", 0m, "Light", 1 },
                    { 2, 1, "van", 500m, "Medium", 2 },
                    { 3, 1, "truck", 2500m, "Heavy", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_Name",
                table: "Manufacturers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_CategoryConfigurationId_MinWeightKg",
                table: "VehicleCategories",
                columns: new[] { "CategoryConfigurationId", "MinWeightKg" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_CategoryConfigurationId_Name",
                table: "VehicleCategories",
                columns: new[] { "CategoryConfigurationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_CategoryConfigurationId_SortOrder",
                table: "VehicleCategories",
                columns: new[] { "CategoryConfigurationId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ManufacturerId",
                table: "Vehicles",
                column: "ManufacturerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleCategories");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "CategoryConfigurations");

            migrationBuilder.DropTable(
                name: "Manufacturers");
        }
    }
}
