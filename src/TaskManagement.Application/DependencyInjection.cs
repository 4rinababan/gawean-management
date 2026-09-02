using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Common;
using TaskManagement.Application.Services;
using TaskManagement.Application.Validation;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateIssueRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddScoped<PermissionGuard>();
        services.AddScoped<IssueChangeProcessor>();

        services.AddScoped<OrganizationService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<IssueService>();
        services.AddScoped<BoardService>();
        services.AddScoped<SprintService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AttachmentService>();

        return services;
    }
}
