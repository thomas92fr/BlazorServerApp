using Model.Repositories;
using Model.Services;
using ViewModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// BLAZOR KEY: Repository is Scoped (per user circuit), not Singleton
// Each user connection gets their own Repository instance with isolated cache
builder.Services.AddScoped<IRepository, InMemoryRepository>();

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
