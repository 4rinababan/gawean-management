using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Issues;

/// <summary>Metadata for a file uploaded against an issue. The bytes live in <see cref="StorageKey"/> via the app's file store.</summary>
public class Attachment : Entity, ITenantScoped
{
    public const long MaxSizeBytes = 25 * 1024 * 1024;

    private Attachment() { }

    internal Attachment(Guid issueId, Guid organizationId, string uploadedByUserId, string fileName, string contentType, long sizeBytes, string storageKey)
    {
        if (sizeBytes is <= 0 or > MaxSizeBytes)
            throw new DomainException($"Attachment size must be between 1 byte and {MaxSizeBytes / (1024 * 1024)} MB.");

        IssueId = issueId;
        OrganizationId = organizationId;
        UploadedByUserId = Guard.NotBlank(uploadedByUserId, nameof(uploadedByUserId));
        FileName = Guard.NotBlank(fileName, nameof(fileName));
        ContentType = Guard.NotBlank(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        StorageKey = Guard.NotBlank(storageKey, nameof(storageKey));
    }

    public Guid OrganizationId { get; private set; }

    public Guid IssueId { get; private set; }

    public string UploadedByUserId { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;
}
