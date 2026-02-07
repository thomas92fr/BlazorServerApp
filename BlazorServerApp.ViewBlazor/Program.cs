using BlazorServerApp.Model;
using Radzen;
using BlazorServerApp.ViewModel;
using BlazorServerApp.ViewMCP;
using BlazorServerApp.ViewBlazor.Endpoints;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using BlazorServerApp.Model.Data;
using BlazorServerApp.Model.Entities;

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
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
})
.AddOpenIdConnect(options =>
{
    var oidc = builder.Configuration.GetSection("OpenIdConnect");
    options.Authority = oidc["Authority"];
    options.ClientId = oidc["ClientId"];
    options.ClientSecret = oidc["ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.GetClaimsFromUserInfoEndpoint = true;
    options.CallbackPath = "/signin-oidc";
    options.Events = new OpenIdConnectEvents
    {
        OnTicketReceived = async context =>
        {
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return;

            var dbFactory = context.HttpContext.RequestServices
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();

            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == email);
            if (user == null)
            {
                user = new User { UserName = email, Password = string.Empty };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            var identity = context.Principal!.Identity as ClaimsIdentity;
            identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            // Ensure Identity.Name returns the email for display in the UI
            if (string.IsNullOrEmpty(context.Principal.Identity?.Name))
            {
                identity?.AddClaim(new Claim(ClaimTypes.Name, email));
            }
        }
    };
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
