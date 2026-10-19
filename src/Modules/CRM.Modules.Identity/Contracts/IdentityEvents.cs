using CRM.BuildingBlocks.Messaging;

namespace CRM.Modules.Identity.Contracts;

/// <summary>A user account was created (self-registration or by an admin).</summary>
public sealed record UserRegistered(int UserId, string Email, string FullName, string PreferredLanguage) : IntegrationEvent;

public sealed record UserDeleted(int UserId) : IntegrationEvent;
