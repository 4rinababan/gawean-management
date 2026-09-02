using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Application.Services;

public sealed record AttachmentContent(string FileName, string ContentType, Stream Stream);

public sealed class AttachmentService(IAppDbContext db, IFileStorage storage, PermissionGuard guard)
{
    public async Task<AttachmentDto> UploadAsync(Guid issueId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);

        var storageKey = await storage.SaveAsync(content, fileName, contentType, ct);
        var attachment = issue.AddAttachment(guard.UserId, fileName, contentType, sizeBytes, storageKey);
        await db.SaveChangesAsync(ct);

        return new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedByUserId, attachment.CreatedAt);
    }

    public async Task<AttachmentContent> DownloadAsync(Guid attachmentId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);

        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw NotFoundException.For<Attachment>(attachmentId);

        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return new AttachmentContent(attachment.FileName, attachment.ContentType, stream);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);

        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw NotFoundException.For<Attachment>(attachmentId);

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.StorageKey, ct);
    }
}
