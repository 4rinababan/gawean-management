using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Application.Services;

public sealed record AttachmentContent(string FileName, string ContentType, Stream Stream);

public sealed class AttachmentService(IAppDbContextFactory dbf, IFileStorage storage, PermissionGuard guard)
{
    public async Task<AttachmentDto> UploadAsync(Guid issueId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        await using var db = dbf.CreateDbContext();

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);

        var storageKey = await storage.SaveAsync(content, fileName, contentType, ct);
        var attachment = issue.AddAttachment(guard.UserId, fileName, contentType, sizeBytes, storageKey);
        db.Attachments.Add(attachment); // client-generated ids: force Added rather than EF's key-is-set heuristic
        await db.SaveChangesAsync(ct);

        return new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedByUserId, attachment.CreatedAt);
    }

    public async Task<AttachmentContent> DownloadAsync(Guid attachmentId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw NotFoundException.For<Attachment>(attachmentId);

        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return new AttachmentContent(attachment.FileName, attachment.ContentType, stream);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        await using var db = dbf.CreateDbContext();

        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw NotFoundException.For<Attachment>(attachmentId);

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.StorageKey, ct);
    }
}
