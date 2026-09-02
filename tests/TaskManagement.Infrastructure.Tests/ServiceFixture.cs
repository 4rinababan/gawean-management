using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Services;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>Wires the real application services against a SQLite-backed context with substituted external dependencies.</summary>
public sealed class ServiceFixture : IDisposable
{
    private readonly SqliteHarness _harness = new();

    public FakeTenant Tenant => _harness.Tenant;
    public FakeCurrentUser CurrentUser { get; } = new();
    public IUserDirectory Users { get; } = Substitute.For<IUserDirectory>();
    public IEmailSender Email { get; } = Substitute.For<IEmailSender>();
    public INotificationRealtime Realtime { get; } = Substitute.For<INotificationRealtime>();
    public IAppUrls Urls { get; } = Substitute.For<IAppUrls>();
    public IClock Clock { get; } = new FakeClock();

    public ServiceFixture()
    {
        Users.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new UserSummary(ci.Arg<string>(), $"User {ci.Arg<string>()}", $"{ci.Arg<string>()}@x.com", ci.Arg<string>(), "#333"));
        Users.GetManyAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => (IReadOnlyDictionary<string, UserSummary>)ci.Arg<IEnumerable<string>>()
                .Distinct()
                .ToDictionary(id => id, id => new UserSummary(id, $"User {id}", $"{id}@x.com", id, "#333")));
        Urls.Issue(Arg.Any<string>(), Arg.Any<Guid>()).Returns("https://test/issue");
    }

    public AppDbContext Db() => _harness.CreateContext();

    public T Build<T>(AppDbContext db) where T : class
    {
        var guard = new PermissionGuard(Tenant, CurrentUser);
        var changeProcessor = new IssueChangeProcessor(db, Users, Email, Realtime, Tenant, Urls);
        var issues = new IssueService(db, Users, guard, changeProcessor);

        object svc = typeof(T).Name switch
        {
            nameof(IssueService) => issues,
            nameof(BoardService) => new BoardService(db, guard, issues, changeProcessor),
            nameof(SprintService) => new SprintService(db, guard, issues, Realtime),
            nameof(ProjectService) => new ProjectService(db, Users, guard),
            nameof(OrganizationService) => new OrganizationService(db, CurrentUser, Users, guard),
            nameof(NotificationService) => new NotificationService(db, CurrentUser),
            _ => throw new NotSupportedException(typeof(T).Name),
        };
        return (T)svc;
    }

    public void Dispose() => _harness.Dispose();
}
