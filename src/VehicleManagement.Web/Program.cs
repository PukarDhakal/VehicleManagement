using Microsoft.EntityFrameworkCore;
using VehicleManagement.Application.Categories;
using VehicleManagement.Application.Vehicles;
using VehicleManagement.Infrastructure.Persistence;
using VehicleManagement.Infrastructure.Persistence.Repositories;
using VehicleManagement.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("VehicleManagement")
    ?? throw new InvalidOperationException(
        "Connection string 'VehicleManagement' has not been configured.");

builder.Services.AddPooledDbContextFactory<VehicleManagementDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ICategoryConfigurationRepository, CategoryConfigurationRepository>();
builder.Services.AddScoped<IManufacturerRepository, ManufacturerRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<VehicleApplicationService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
