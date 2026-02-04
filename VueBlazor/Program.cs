using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Services;
using Model.UnitOfWork;
using ViewModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure Entity Framework Core with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// BLAZOR KEY: UnitOfWork is Scoped (per user circuit)
// Each user connection gets their own UnitOfWork instance with isolated cache
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register services
builder.Services.AddSingleton<IWeatherForecastService, WeatherForecastService>();

// Register ViewModels as Scoped (shared per circuit, not per component)
// MIGRATION NOTE: Previously were Transient, now Scoped matches Repository lifetime
builder.Services.AddScoped<CounterViewModel>();
builder.Services.AddScoped<WeatherForecastViewModel>();
builder.Services.AddScoped<PersonListViewModel>();

// Logging
builder.Services.AddLogging();

var app = builder.Build();

// Apply migrations automatically in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
