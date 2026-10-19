namespace CRM.BuildingBlocks.Domain;

/// <summary>Base type for persisted entities with an integer identity key.</summary>
public abstract class Entity
{
    public int Id { get; set; }
}

/// <summary>
/// Entities whose creation/modification time and user are filled in automatically on save.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }

    int? CreatedBy { get; set; }

    DateTime UpdatedAt { get; set; }

    int? UpdatedBy { get; set; }
}

/// <summary>
/// Entities that are never physically deleted: removing them sets <see cref="IsDeleted"/> and they
/// disappear from every query through a global filter.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }

    int? DeletedBy { get; set; }
}

/// <summary>Convenience base for entities that are auditable.</summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
