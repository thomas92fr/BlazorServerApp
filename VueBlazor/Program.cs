using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Model.Services;
using ViewModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register Services
builder.Services.AddSingleton<IWeatherForecastService, WeatherForecastService>();

// Register ViewModels
builder.Services.AddTransient<CounterViewModel>();
builder.Services.AddTransient<WeatherForecastViewModel>();

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
