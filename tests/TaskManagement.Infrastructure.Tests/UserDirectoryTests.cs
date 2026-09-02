using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Identity;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Tests;

public class UserDirectoryTests : IDisposable
{
    private readonly SqliteHarness _harness = new();

    private UserDirectory Directory() => new(new HarnessDbContextFactory(_harness));

    [Fact]
    public async Task GetManyAsync_returns_summaries_for_the_requested_ids()
    {
        await Seed(
            ("u1", "Ada Lovelace", "ada@x.com", "ada"),
            ("u2", "", "grace@x.com", "grace"),
            ("u3", "Alan Turing", "alan@x.com", "alan"));

        var result = await Directory().GetManyAsync(["u1", "u2", "missing"]);

        result.Should().HaveCount(2);
        result["u1"].DisplayName.Should().Be("Ada Lovelace");
        result["u2"].DisplayName.Should().Be("grace"); // falls back to username when DisplayName is blank
    }

    [Fact]
    public async Task FindByEmailAsync_is_case_insensitive()
    {
        await Seed(("u1", "Ada", "ADA@X.COM", "ada"));

        var found = await Directory().FindByEmailAsync("ada@x.com");

        found.Should().NotBeNull();
        found!.Id.Should().Be("u1");
    }

    [Fact]
    public async Task FindByUsernameAsync_resolves_mentions()
    {
        await Seed(("u1", "Ada", "ada@x.com", "ada.lovelace"));

        (await Directory().FindByUsernameAsync("ADA.LOVELACE"))!.Id.Should().Be("u1");
        (await Directory().FindByUsernameAsync("nobody")).Should().BeNull();
    }

    private async Task Seed(params (string Id, string Display, string Email, string UserName)[] users)
    {
        await using var db = _harness.CreateContext();
        foreach (var u in users)
        {
            db.Users.Add(new ApplicationUser
            {
                Id = u.Id,
                DisplayName = u.Display,
                Email = u.Email,
                NormalizedEmail = u.Email.ToUpperInvariant(),
                UserName = u.UserName,
                NormalizedUserName = u.UserName.ToUpperInvariant(),
                AvatarColor = "#123456",
            });
        }
        await db.SaveChangesAsync();
    }

    public void Dispose() => _harness.Dispose();

    private sealed class HarnessDbContextFactory(SqliteHarness harness) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => harness.CreateContext();
    }
}
