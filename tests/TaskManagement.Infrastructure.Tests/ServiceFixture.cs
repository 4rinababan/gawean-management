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
    public IFileStorage Storage { get; } = Substitute.For<IFileStorage>();
    public IAiAssistant Ai { get; } = Substitute.For<IAiAssistant>();
    public IHtmlSanitizer Sanitizer { get; } = new TaskManagement.Infrastructure.Content.RichTextSanitizer();
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
        Urls.InvitationAccept(Arg.Any<string>()).Returns(ci => $"https://test/invitations/accept?token={ci.Arg<string>()}");
    }

    public AppDbContext Db() => _harness.CreateContext();

    public IAppDbContextFactory Factory => _harness.Factory;

    public T Build<T>() where T : class
    {
        var guard = new PermissionGuard(Tenant, CurrentUser);
        var automation = new AutomationEngine(Urls, Tenant);
        var changeProcessor = new IssueChangeProcessor(Users, Email, Realtime, Tenant, Urls, automation);

        object svc = typeof(T).Name switch
        {
            nameof(IssueService) => new IssueService(Factory, Users, guard, changeProcessor, Sanitizer, Ai),
            nameof(BoardService) => new BoardService(Factory, Users, guard, changeProcessor),
            nameof(SprintService) => new SprintService(Factory, guard, Realtime),
            nameof(ProjectService) => new ProjectService(Factory, Users, guard),
            nameof(OrganizationService) => new OrganizationService(Factory, CurrentUser, Users, guard, Storage),
            nameof(NotificationService) => new NotificationService(Factory, CurrentUser),
            nameof(InvitationService) => new InvitationService(Factory, CurrentUser, Users, Email, guard, Clock, Urls),
            nameof(AttachmentService) => new AttachmentService(Factory, Storage, guard),
            nameof(DashboardService) => new DashboardService(Factory, CurrentUser, Clock),
            nameof(AutomationRuleService) => new AutomationRuleService(Factory, Users, guard),
            _ => throw new NotSupportedException(typeof(T).Name),
        };
        return (T)svc;
    }

    public void Dispose() => _harness.Dispose();
}
