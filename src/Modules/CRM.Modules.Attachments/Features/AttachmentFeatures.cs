using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Attachments.Contracts;
using CRM.Modules.Attachments.Domain;
using CRM.Modules.Attachments.Infrastructure.Persistence;
using CRM.Modules.Attachments.Infrastructure.Storage;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRM.Modules.Attachments.Features;

/// <param name="CreatedByName">The uploader; null for files added by the system (demo data).</param>
internal sealed record AttachmentDto(
    int Id, string RecordType, int RecordId, string FileName, string ContentType, long Size,
    int? CreatedBy, string? CreatedByName, DateTime CreatedAt);

// ---------------------------------------------------------------- List

/// <summary>Files of one record, newest first.</summary>
internal sealed record GetAttachments(string RecordType, int RecordId) : IQuery<IReadOnlyList<AttachmentDto>>;

internal sealed class GetAttachmentsValidator : AbstractValidator<GetAttachments>
{
    public GetAttachmentsValidator()
    {
        RuleFor(x => x.RecordType).Must(RecordTypes.All.Contains).WithMessage($"Supported record types: {string.Join(", ", RecordTypes.All)}.");
        RuleFor(x => x.RecordId).GreaterThan(0);
    }
}

internal sealed class GetAttachmentsHandler : IRequestHandler<GetAttachments, IReadOnlyList<AttachmentDto>>
{
    private readonly AttachmentsDbContext _db;
    private readonly IUserDirectory _users;

    public GetAttachmentsHandler(AttachmentsDbContext db, IUserDirectory users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IReadOnlyList<AttachmentDto>> Handle(GetAttachments request, CancellationToken cancellationToken)
    {
        var items = await _db.Attachments.AsNoTracking()
            .Where(x => x.RecordType == request.RecordType && x.RecordId == request.RecordId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new AttachmentDto(x.Id, x.RecordType, x.RecordId, x.FileName, x.ContentType, x.Size, x.CreatedBy, null, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var names = await _users.GetNamesAsync(items.Where(x => x.CreatedBy.HasValue).Select(x => x.CreatedBy!.Value), cancellationToken);

        return items.Select(x => x with { CreatedByName = x.CreatedBy is { } id ? names.GetValueOrDefault(id) : null }).ToList();
    }
}

// ---------------------------------------------------------------- Upload

/// <param name="FileName">As sent by the client; only its last path segment is kept.</param>
/// <param name="Content">Read once, by the handler.</param>
internal sealed record UploadAttachment(string RecordType, int RecordId, string FileName, long Size, Stream Content) : ICommand<AttachmentDto>;

internal sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachment>
{
    // Type and size limits are business rules in the handler (422 with their own codes).
    public UploadAttachmentValidator()
    {
        RuleFor(x => x.RecordType).Must(RecordTypes.All.Contains).WithMessage($"Supported record types: {string.Join(", ", RecordTypes.All)}.");
        RuleFor(x => x.RecordId).GreaterThan(0);
        RuleFor(x => x.FileName).NotEmpty();
    }
}

internal sealed class UploadAttachmentHandler : IRequestHandler<UploadAttachment, AttachmentDto>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly AttachmentsDbContext _db;
    private readonly IFileStorage _storage;
    private readonly AttachmentRules _rules;
    private readonly AttachmentOptions _options;
    private readonly IUserDirectory _users;

    public UploadAttachmentHandler(
        AttachmentsDbContext db, IFileStorage storage, AttachmentRules rules, IOptions<AttachmentOptions> options, IUserDirectory users)
    {
        _db = db;
        _storage = storage;
        _rules = rules;
        _options = options.Value;
        _users = users;
    }

    public async Task<AttachmentDto> Handle(UploadAttachment request, CancellationToken cancellationToken)
    {
        var fileName = AttachmentRules.CleanFileName(request.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if(!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException($"Files of type '{extension}' cannot be uploaded.", "attachment.type_not_allowed");
        }

        if(request.Size <= 0)
        {
            throw new BusinessRuleException("The file is empty.", "attachment.empty");
        }

        if(request.Size > _options.MaxFileSizeBytes)
        {
            throw new BusinessRuleException($"Files can be at most {_options.MaxFileSizeMb} MB.", "attachment.too_large");
        }

        await _rules.EnsureRecordExistsAsync(request.RecordType, request.RecordId, cancellationToken);

        var key = await _storage.SaveAsync(request.Content, cancellationToken);
        var attachment = new Attachment
        {
            RecordType = request.RecordType,
            RecordId = request.RecordId,
            FileName = fileName,
            ContentType = ContentTypes.TryGetContentType(fileName, out var type) ? type : "application/octet-stream",
            Size = request.Size,
            StorageKey = key
        };

        try
        {
            _db.Attachments.Add(attachment);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Don't leave a file nobody can reach.
            await _storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        var uploader = attachment.CreatedBy is { } id ? (await _users.GetNamesAsync([id], cancellationToken)).GetValueOrDefault(id) : null;
        return new AttachmentDto(attachment.Id, attachment.RecordType, attachment.RecordId, attachment.FileName, attachment.ContentType,
            attachment.Size, attachment.CreatedBy, uploader, attachment.CreatedAt);
    }
}

// ---------------------------------------------------------------- Download / delete

internal sealed record AttachmentFile(string FileName, string ContentType, Stream Content);

internal sealed record DownloadAttachment(int Id) : IQuery<AttachmentFile>;

internal sealed class DownloadAttachmentHandler : IRequestHandler<DownloadAttachment, AttachmentFile>
{
    private readonly AttachmentsDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadAttachmentHandler(AttachmentsDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<AttachmentFile> Handle(DownloadAttachment request, CancellationToken cancellationToken)
    {
        var attachment = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Attachment", request.Id);

        try
        {
            return new AttachmentFile(attachment.FileName, attachment.ContentType, _storage.OpenRead(attachment.StorageKey));
        }
        catch(FileNotFoundException)
        {
            throw new NotFoundException("The file is missing from the file store.", "attachment.file_missing");
        }
        catch(DirectoryNotFoundException)
        {
            throw new NotFoundException("The file is missing from the file store.", "attachment.file_missing");
        }
    }
}

/// <summary>Only the uploader, or someone with <see cref="AttachmentsPermissions.Manage"/>, may delete.</summary>
internal sealed record DeleteAttachment(int Id) : ICommand;

internal sealed class DeleteAttachmentHandler : IRequestHandler<DeleteAttachment, Unit>
{
    private readonly AttachmentsDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _currentUser;

    public DeleteAttachmentHandler(AttachmentsDbContext db, IFileStorage storage, ICurrentUser currentUser)
    {
        _db = db;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteAttachment request, CancellationToken cancellationToken)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Attachment", request.Id);

        // No signed-in user means the system itself (startup seeding); every endpoint requires a permission.
        var system = !_currentUser.IsAuthenticated;
        if(!system && attachment.CreatedBy != _currentUser.Id && !_currentUser.HasPermission(AttachmentsPermissions.Manage))
        {
            throw new ForbiddenException("Only the person who uploaded this file can delete it.", "attachment.not_uploader");
        }

        // The row is soft-deleted (its name stays in the audit log); the bytes are removed.
        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync(cancellationToken);
        await _storage.DeleteAsync(attachment.StorageKey, cancellationToken);

        return Unit.Value;
    }
}

internal sealed class AttachmentRules
{
    private readonly ICrmDirectory _crm;
    private readonly ISalesDirectory _sales;

    public AttachmentRules(ICrmDirectory crm, ISalesDirectory sales)
    {
        _crm = crm;
        _sales = sales;
    }

    /// <summary>The last path segment, without control characters, at most 255 characters (extension kept).</summary>
    public static string CleanFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if(string.IsNullOrEmpty(name) || name is "." or "..")
        {
            throw new BusinessRuleException("The file has no name.", "attachment.no_name");
        }

        if(name.Length > 255)
        {
            var extension = Path.GetExtension(name);
            name = name[..(255 - extension.Length)] + extension;
        }

        return name;
    }

    public async Task EnsureRecordExistsAsync(string recordType, int recordId, CancellationToken cancellationToken)
    {
        var exists = recordType switch
        {
            RecordTypes.Company => (await _crm.GetCompaniesAsync([recordId], cancellationToken)).ContainsKey(recordId),
            RecordTypes.Customer => (await _crm.GetCustomersAsync([recordId], cancellationToken)).ContainsKey(recordId),
            RecordTypes.Deal => (await _sales.GetDealsAsync([recordId], cancellationToken)).ContainsKey(recordId),
            _ => false
        };

        if(!exists)
        {
            throw new NotFoundException(char.ToUpperInvariant(recordType[0]) + recordType[1..], recordId);
        }
    }
}
