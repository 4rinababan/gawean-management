using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Absolute path to the directory attachments are written to (a mounted volume in production).</summary>
    public string RootPath { get; set; } = "uploads";
}

/// <summary>Stores attachment bytes on the local filesystem under a sharded, opaque key. Swap for an S3 implementation without touching callers.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.CreateVersion7():n}{extension}";
        var target = ResolvePath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var file = File.Create(target);
        await content.CopyToAsync(file, ct);

        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException("Attachment blob not found.", storageKey);

        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    // Guards against path traversal in the stored key.
    private string ResolvePath(string storageKey)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Storage key escapes the storage root.");
        return full;
    }
}
