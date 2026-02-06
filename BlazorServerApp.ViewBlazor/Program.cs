using BlazorServerApp.Model;
using Radzen;
using BlazorServerApp.ViewModel;
using BlazorServerApp.ViewMCP;
using BlazorServerApp.ViewBlazor.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddRadzenComponents();

// Add ViewModel layer (automatically includes Model layer)
builder.Services.AddViewModels(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Add MCP server
builder.Services.AddViewMcp();

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
app.MapUploadEndpoints();
app.MapViewMcp();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
