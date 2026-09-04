using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Projects;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Title).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(20000);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.ReporterUserId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.AssigneeUserId).HasMaxLength(450);
        builder.Property(i => i.BoardRank).HasMaxLength(64).IsRequired();

        builder.HasIndex(i => new { i.ProjectId, i.Number }).IsUnique();
        builder.HasIndex(i => new { i.OrganizationId, i.Status });
        // Dashboard reads "my open work" by assignee and orders/filters on the due date.
        builder.HasIndex(i => new { i.AssigneeUserId, i.Status, i.DueDate });
        builder.HasIndex(i => i.SprintId);
        builder.HasIndex(i => i.AssigneeUserId);

        builder.HasOne<Project>().WithMany().HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Sprint>().WithMany().HasForeignKey(i => i.SprintId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Issue>().WithMany().HasForeignKey(i => i.ParentIssueId).OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(i => i.Comments).WithOne().HasForeignKey(c => c.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Attachments).WithOne().HasForeignKey(a => a.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Viewers).WithOne().HasForeignKey(v => v.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.AiNotes).WithOne().HasForeignKey(n => n.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Viewers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.AiNotes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Body).HasMaxLength(10000).IsRequired();
        builder.Property(c => c.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(c => c.IssueId);
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(a => a.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(a => a.IssueId);
    }
}

public sealed class IssueViewerConfiguration : IEntityTypeConfiguration<IssueViewer>
{
    public void Configure(EntityTypeBuilder<IssueViewer> builder)
    {
        builder.ToTable("issue_viewers");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(v => new { v.IssueId, v.UserId }).IsUnique();
    }
}

public sealed class IssueAiNoteConfiguration : IEntityTypeConfiguration<IssueAiNote>
{
    public void Configure(EntityTypeBuilder<IssueAiNote> builder)
    {
        builder.ToTable("issue_ai_notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.AskedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Question).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Answer).HasMaxLength(8000).IsRequired();
        builder.HasIndex(n => n.IssueId);
    }
}

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.Field).HasMaxLength(64).IsRequired();
        builder.Property(a => a.OldValue).HasMaxLength(4000);
        builder.Property(a => a.NewValue).HasMaxLength(4000);
        builder.HasIndex(a => new { a.IssueId, a.CreatedAt });
        builder.HasOne<Issue>().WithMany().HasForeignKey(a => a.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}
