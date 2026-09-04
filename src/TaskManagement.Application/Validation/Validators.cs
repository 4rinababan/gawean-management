using FluentValidation;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Automation;

namespace TaskManagement.Application.Validation;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(40)
            .Matches("^[A-Za-z0-9 _-]+$").WithMessage("The workspace URL may only contain letters, numbers, spaces, hyphens and underscores.");
    }
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches("^[A-Za-z][A-Za-z0-9]{1,9}$")
            .WithMessage("The project key must be 2–10 letters or digits and start with a letter.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateIssueRequestValidator : AbstractValidator<CreateIssueRequest>
{
    public CreateIssueRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints is not null);
    }
}

public sealed class UpdateIssueRequestValidator : AbstractValidator<UpdateIssueRequest>
{
    public UpdateIssueRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints is not null);
    }
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.IssueId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

public sealed class CreateSprintRequestValidator : AbstractValidator<CreateSprintRequest>
{
    public CreateSprintRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Goal).MaximumLength(500);
    }
}

public sealed class StartSprintRequestValidator : AbstractValidator<StartSprintRequest>
{
    public StartSprintRequestValidator()
    {
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("The sprint must end after it starts.");
    }
}

public sealed class AutomationActionDtoValidator : AbstractValidator<AutomationActionDto>
{
    public AutomationActionDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value)
            .Must(v => Enum.TryParse<IssueStatus>(v, out _)).WithMessage("Choose a status.")
            .When(x => x.Type == AutomationActionType.SetStatus);
        RuleFor(x => x.Value)
            .Must(v => Enum.TryParse<IssuePriority>(v, out _)).WithMessage("Choose a priority.")
            .When(x => x.Type == AutomationActionType.SetPriority);
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("This action needs a value.")
            .When(x => x.Type is AutomationActionType.AddComment or AutomationActionType.Notify);
    }
}

public sealed class CreateAutomationRuleRequestValidator : AbstractValidator<CreateAutomationRuleRequest>
{
    public CreateAutomationRuleRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.TriggerValue)
            .Must(v => Enum.TryParse<IssueStatus>(v, out _)).WithMessage("Choose a target status.")
            .When(x => x.TriggerType == AutomationTriggerType.StatusChanged);
        RuleFor(x => x.TriggerValue)
            .Must(v => Enum.TryParse<IssuePriority>(v, out _)).WithMessage("Choose a target priority.")
            .When(x => x.TriggerType == AutomationTriggerType.PriorityChanged);
        RuleFor(x => x.Actions).NotEmpty().WithMessage("Add at least one action.");
        RuleForEach(x => x.Actions).SetValidator(new AutomationActionDtoValidator());
    }
}

public sealed class UpdateAutomationRuleRequestValidator : AbstractValidator<UpdateAutomationRuleRequest>
{
    public UpdateAutomationRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.TriggerValue)
            .Must(v => Enum.TryParse<IssueStatus>(v, out _)).WithMessage("Choose a target status.")
            .When(x => x.TriggerType == AutomationTriggerType.StatusChanged);
        RuleFor(x => x.TriggerValue)
            .Must(v => Enum.TryParse<IssuePriority>(v, out _)).WithMessage("Choose a target priority.")
            .When(x => x.TriggerType == AutomationTriggerType.PriorityChanged);
        RuleFor(x => x.Actions).NotEmpty().WithMessage("Add at least one action.");
        RuleForEach(x => x.Actions).SetValidator(new AutomationActionDtoValidator());
    }
}
