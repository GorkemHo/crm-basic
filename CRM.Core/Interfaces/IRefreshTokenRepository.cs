using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Finds a token by its hash, including the owning user and the user's roles.
        /// </summary>
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

        Task AddAsync(RefreshToken refreshToken);

        Task RevokeAllForUserAsync(int userId);

        Task SaveChangesAsync();
    }
}
