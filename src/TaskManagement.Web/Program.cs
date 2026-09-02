using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Identity;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Web.Components;
using TaskManagement.Web.Components.Account;
using TaskManagement.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// --- Application + infrastructure -------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Web-layer implementations of application abstractions.
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<IAppUrls, AppUrls>();
builder.Services.AddSingleton<INotificationRealtime, SignalRNotificationRealtime>();
builder.Services.AddScoped<TaskManagement.Web.Components.Ui.ToastService>();

// --- Authentication --------------------------------------------------------------
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

var google = builder.Configuration.GetSection("Authentication:Google");
if (google.GetValue<string>("ClientId") is { Length: > 0 } googleId)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleId;
        options.ClientSecret = google.GetValue<string>("ClientSecret")!;
    });
}

var github = builder.Configuration.GetSection("Authentication:GitHub");
if (github.GetValue<string>("ClientId") is { Length: > 0 } githubId)
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubId;
        options.ClientSecret = github.GetValue<string>("ClientSecret")!;
        options.Scope.Add("user:email");
    });
}

authBuilder.AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IEmailSender<ApplicationUser>, IdentityEmailSender>();

// Persist Data Protection keys so auth cookies survive restarts / redeploys.
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    Directory.CreateDirectory(keyPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        .SetApplicationName("TaskManagement");
}

// --- SignalR / health / misc ----------------------------------------------------
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, NameIdentifierUserIdProvider>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")
              ?? builder.Configuration.GetConnectionString("DefaultConnection")!, name: "database");

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

// --- Migrate on startup (guarded) ----------------------------------------------
if (app.Configuration.GetValue("RunMigrationsOnStartup", app.Environment.IsDevelopment()))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseForwardedHeaders();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapHub<NotificationHub>(NotificationHub.HubUrl);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();
app.MapAttachmentEndpoints();

app.Run();

public partial class Program;

/// <summary>Maps SignalR's per-connection user id to the Identity NameIdentifier claim so groups key on the user id.</summary>
file sealed class NameIdentifierUserIdProvider : Microsoft.AspNetCore.SignalR.IUserIdProvider
{
    public string? GetUserId(Microsoft.AspNetCore.SignalR.HubConnectionContext connection)
        => connection.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
