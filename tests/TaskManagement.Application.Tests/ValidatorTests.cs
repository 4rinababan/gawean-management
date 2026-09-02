using FluentValidation;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Validation;
using TaskManagement.Domain;

namespace TaskManagement.Application.Tests;

public class ValidatorTests
{
    [Theory]
    [InlineData("", "web", false)]
    [InlineData("Acme", "", false)]
    [InlineData("Acme", "bad/slug", false)]
    [InlineData("Acme", "acme-1", true)]
    public void CreateOrganization_validates_name_and_slug(string name, string slug, bool valid)
    {
        var result = new CreateOrganizationRequestValidator()
            .Validate(new CreateOrganizationRequest { Name = name, Slug = slug });

        result.IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("W", false)]
    [InlineData("1W", false)]
    [InlineData("WEB", true)]
    [InlineData("PROJECT123", true)]
    [InlineData("TOOLONGKEYXX", false)]
    public void CreateProject_validates_key_shape(string key, bool valid)
    {
        var result = new CreateProjectRequestValidator()
            .Validate(new CreateProjectRequest { Key = key, Name = "Name" });

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void CreateIssue_requires_a_title_and_project()
    {
        var result = new CreateIssueRequestValidator()
            .Validate(new CreateIssueRequest { ProjectId = Guid.Empty, Title = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain(["ProjectId", "Title"]);
    }

    [Fact]
    public void CreateIssue_rejects_story_points_over_100()
    {
        var result = new CreateIssueRequestValidator()
            .Validate(new CreateIssueRequest { ProjectId = Guid.NewGuid(), Title = "T", StoryPoints = 200 });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void StartSprint_requires_end_after_start()
    {
        var start = new DateOnly(2026, 3, 1);
        var result = new StartSprintRequestValidator()
            .Validate(new StartSprintRequest { StartDate = start, EndDate = start });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-an-email", false)]
    [InlineData("person@example.com", true)]
    public void InviteMember_validates_email(string email, bool valid)
    {
        var result = new InviteMemberRequestValidator()
            .Validate(new InviteMemberRequest { Email = email, Role = OrgRole.Member });

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void AddComment_rejects_empty_body()
    {
        var result = new AddCommentRequestValidator()
            .Validate(new AddCommentRequest(Guid.NewGuid(), "   "));

        result.IsValid.Should().BeFalse();
    }
}
