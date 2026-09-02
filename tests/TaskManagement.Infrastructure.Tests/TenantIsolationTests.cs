using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

public class TenantIsolationTests : IDisposable
{
    private readonly SqliteHarness _harness = new();

    [Fact]
    public async Task Query_filters_hide_other_tenants_projects()
    {
        Guid orgA, orgB;
        await using (var seed = _harness.CreateContext())
        {
            var a = new Organization("Alpha", "alpha", "owner-a");
            var b = new Organization("Beta", "beta", "owner-b");
            seed.Organizations.AddRange(a, b);
            orgA = a.Id;
            orgB = b.Id;
            seed.Projects.Add(new Project(orgA, "AAA", "A project"));
            seed.Projects.Add(new Project(orgB, "BBB", "B project"));
            await seed.SaveChangesAsync();
        }

        _harness.Tenant.Set(orgA, "alpha", OrgRole.Admin);
        await using var ctx = _harness.CreateContext();

        var visible = await ctx.Projects.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].Key.Should().Be("AAA");
    }

    [Fact]
    public async Task IgnoringTenantFilter_sees_all_tenants()
    {
        await using (var seed = _harness.CreateContext())
        {
            seed.Organizations.Add(new Organization("Alpha", "alpha", "owner-a"));
            seed.Organizations.Add(new Organization("Beta", "beta", "owner-b"));
            await seed.SaveChangesAsync();
        }

        _harness.Tenant.Set(Guid.NewGuid(), "nobody", OrgRole.Admin);
        await using var ctx = _harness.CreateContext();

        (await ctx.IgnoringTenantFilter<Organization>().CountAsync()).Should().Be(2);
        (await ctx.Organizations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Unique_index_prevents_duplicate_project_key_within_an_org()
    {
        var org = new Organization("Alpha", "alpha", "owner-a");
        _harness.Tenant.Set(org.Id, "alpha", OrgRole.Admin);

        await using var ctx = _harness.CreateContext();
        ctx.Organizations.Add(org);
        ctx.Projects.Add(new Project(org.Id, "DUP", "First"));
        await ctx.SaveChangesAsync();

        ctx.Projects.Add(new Project(org.Id, "DUP", "Second"));
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_a_project_cascades_to_its_issues()
    {
        var org = new Organization("Alpha", "alpha", "owner-a");
        var project = new Project(org.Id, "CAS", "Cascade");
        var issue = project.CreateIssue("child", IssueType.Task, "owner-a");
        _harness.Tenant.Set(org.Id, "alpha", OrgRole.Admin);

        await using var ctx = _harness.CreateContext();
        ctx.Organizations.Add(org);
        ctx.Projects.Add(project);
        ctx.Issues.Add(issue);
        await ctx.SaveChangesAsync();
        (await ctx.Issues.CountAsync()).Should().Be(1);

        ctx.Projects.Remove(project);
        await ctx.SaveChangesAsync();

        (await ctx.Issues.CountAsync()).Should().Be(0);
    }

    public void Dispose() => _harness.Dispose();
}
