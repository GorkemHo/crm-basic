namespace CRM.BuildingBlocks.Security;

/// <summary>
/// Claim names used in access tokens. Short, standard JWT names (RFC 7519 / OIDC) so the token stays
/// small and clients can read it without .NET-specific mappings.
/// </summary>
public static class CrmClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string GivenName = "given_name";
    public const string FamilyName = "family_name";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string Locale = "locale";
    public const string TokenId = "jti";
}
