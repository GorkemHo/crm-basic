using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(
        User user,
        IEnumerable<string> roles);
    }
}
