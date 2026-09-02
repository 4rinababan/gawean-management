using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Application.Services;

public sealed class ProjectService(IAppDbContextFactory dbf, IUserDirectory users, PermissionGuard guard)
{
    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        var projects = await db.Projects
            .Where(p => p.OrganizationId == guard.OrganizationId)
            .OrderBy(p => p.Key)
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.Description,
                p.LeadUserId,
                OpenIssues = db.Issues.Count(i => i.ProjectId == p.Id && i.Status != IssueStatus.Done),
            })
            .ToListAsync(ct);

        var leads = await users.GetManyAsync(projects.Where(p => p.LeadUserId is not null).Select(p => p.LeadUserId!), ct);

        return projects
            .Select(p => new ProjectDto(
                p.Id, p.Key, p.Name, p.Description, p.LeadUserId,
                p.LeadUserId is not null && leads.TryGetValue(p.LeadUserId, out var u) ? u.DisplayName : null,
                p.OpenIssues))
            .ToList();
    }

    public async Task<ProjectDto> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        key = key.ToUpperInvariant();
        await using var db = dbf.CreateDbContext();

        var project = await db.Projects.FirstOrDefaultAsync(p => p.OrganizationId == guard.OrganizationId && p.Key == key, ct)
            ?? throw NotFoundException.For<Project>(key);

        var open = await db.Issues.CountAsync(i => i.ProjectId == project.Id && i.Status != IssueStatus.Done, ct);
        var lead = project.LeadUserId is null ? null : await users.GetAsync(project.LeadUserId, ct);

        return new ProjectDto(project.Id, project.Key, project.Name, project.Description, project.LeadUserId, lead?.DisplayName, open);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageProjects);
        var key = Project.ValidateKey(request.Key);
        await using var db = dbf.CreateDbContext();

        if (await db.Projects.AnyAsync(p => p.OrganizationId == guard.OrganizationId && p.Key == key, ct))
            throw new ConflictException($"A project with key '{key}' already exists in this workspace.");

        // The creator owns the project unless they nominate someone else as lead.
        var owner = string.IsNullOrEmpty(request.LeadUserId) ? guard.UserId : request.LeadUserId;
        var project = new Project(guard.OrganizationId, key, request.Name, request.Description, owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return new ProjectDto(project.Id, project.Key, project.Name, project.Description, project.LeadUserId, null, 0);
    }

    public async Task UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default)
    {
        await using var db = dbf.CreateDbContext();
        var project = await RequireOwnedProjectAsync(db, projectId, ct);

        project.Update(request.Name, request.Description, request.LeadUserId);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = dbf.CreateDbContext();
        var project = await RequireOwnedProjectAsync(db, projectId, ct);

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>True when the caller may edit this project: its owner, or a workspace admin.</summary>
    public async Task<bool> CanEditAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!guard.Allows(OrgPermission.ViewContent))
            return false;

        await using var db = dbf.CreateDbContext();
        var ownerId = await db.Projects
            .Where(p => p.Id == projectId && p.OrganizationId == guard.OrganizationId)
            .Select(p => p.LeadUserId)
            .FirstOrDefaultAsync(ct);

        return ownerId == guard.UserId || guard.Allows(OrgPermission.ManageOrganization);
    }

    /// <summary>
    /// Project details are owner-managed: only the project's lead may change them. Workspace admins keep
    /// access so a workspace is never left with an unmaintainable project (e.g. after the owner leaves).
    /// </summary>
    private async Task<Project> RequireOwnedProjectAsync(IAppDbContext db, Guid projectId, CancellationToken ct)
    {
        guard.Require(OrgPermission.ViewContent);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<Project>(projectId);

        if (!project.IsOwnedBy(guard.UserId) && !guard.Allows(OrgPermission.ManageOrganization))
            throw new ForbiddenException("Only the project owner or a workspace admin can change this project.");

        return project;
    }
}
