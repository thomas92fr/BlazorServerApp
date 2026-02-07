using BlazorServerApp.Model;
using Radzen;
using BlazorServerApp.ViewModel;
using BlazorServerApp.ViewMCP;
using BlazorServerApp.ViewBlazor.Endpoints;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });
builder.Services.AddRadzenComponents();

// Add ViewModel layer (automatically includes Model layer)
builder.Services.AddViewModels(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Add MCP server
builder.Services.AddViewMcp();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
    });

// Logging
builder.Services.AddLogging();

var app = builder.Build();

// Apply pending migrations on startup
await app.Services.MigrateDatabaseAsync();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
//app.MapUploadEndpoints(); //Endpoints for file uploads
app.MapViewMcp();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
