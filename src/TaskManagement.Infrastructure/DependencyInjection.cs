using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure.Ai;
using TaskManagement.Infrastructure.Email;
using TaskManagement.Infrastructure.Identity;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Storage;
using TaskManagement.Infrastructure.Time;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // Scoped factory: each CreateDbContext() is a fresh unit of work bound to the current circuit's tenant,
        // so concurrent Blazor component renders never share one EF context. A scoped AppDbContext is also
        // registered (from the same factory) for ASP.NET Core Identity and the startup migration.
        services.AddDbContextFactory<AppDbContext>((_, options) =>
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure();
                })
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning)),
            ServiceLifetime.Scoped);

        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        services.AddScoped<IAppDbContextFactory, AppDbContextFactoryAdapter>();

        services.AddScoped<IUserDirectory, UserDirectory>();

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddSingleton<IHtmlSanitizer, Content.RichTextSanitizer>();

        // Email is queued and dispatched in the background; SMTP round trips must not sit inside a
        // request the user is waiting on. Bounded + DropOldest so a mail outage can't grow unbounded.
        services.AddSingleton(_ => Channel.CreateBounded<OutgoingEmail>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }));
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender, QueuedEmailSender>();
        services.AddHostedService<EmailDispatcher>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IClock, SystemClock>();

        AddAiAssistant(services, configuration);

        return services;
    }

    /// <summary>
    /// The assistant is optional: with no key configured we register a disabled stand-in rather than a
    /// client that would fail on first use, so a deployment without a model is a supported state.
    /// </summary>
    private static void AddAiAssistant(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);
        services.AddOptions<AiOptions>().Bind(section);

        var options = section.Get<AiOptions>() ?? new AiOptions();
        if (!options.IsConfigured)
        {
            services.AddSingleton<IAiAssistant, DisabledAiAssistant>();
            return;
        }

        services.AddHttpClient<IAiAssistant, ChatAiAssistant>(http =>
        {
            // A trailing slash matters: without it BaseAddress drops the /v1 segment when combined
            // with the relative request path.
            http.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            http.DefaultRequestHeaders.Authorization = new("Bearer", options.ApiKey);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    }
}
