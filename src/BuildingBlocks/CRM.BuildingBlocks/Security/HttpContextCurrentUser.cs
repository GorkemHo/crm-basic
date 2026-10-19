using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CRM.BuildingBlocks.Security;

/// <summary><see cref="ICurrentUser"/> backed by the claims of the current HTTP request's access token.</summary>
internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int? Id => int.TryParse(Principal?.FindFirstValue(CrmClaimTypes.UserId), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(CrmClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(CrmClaimTypes.Role).Select(x => x.Value).ToList() ?? [];

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(CrmClaimTypes.Permission).Select(x => x.Value).ToHashSet() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(CrmClaimTypes.Permission, permission) == true;
}
