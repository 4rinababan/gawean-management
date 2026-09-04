using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Automation;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("automation_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(120).IsRequired();
        builder.Property(r => r.TriggerType).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.TriggerValue).HasMaxLength(30);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450).IsRequired();

        // A rule's action list is small, always loaded whole, and never queried into — a single jsonb
        // column is a better fit than a second child table.
        builder.Property(r => r.Actions)
            .HasColumnName("actions_json")
            .HasColumnType("jsonb")
            .HasConversion(
                actions => JsonSerializer.Serialize(actions, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<AutomationAction>>(json, (JsonSerializerOptions?)null) ?? new List<AutomationAction>())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<AutomationAction>>(
                (a, b) => (a ?? Array.Empty<AutomationAction>()).SequenceEqual(b ?? Array.Empty<AutomationAction>()),
                a => a.Aggregate(0, (hash, x) => HashCode.Combine(hash, x)),
                a => a.ToList()));

        builder.HasIndex(r => new { r.ProjectId, r.Enabled });
        builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
