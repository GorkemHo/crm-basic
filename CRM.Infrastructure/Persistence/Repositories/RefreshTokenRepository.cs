using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly CrmDbContext _context;

        public RefreshTokenRepository(CrmDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            return await _context.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Token == tokenHash);
        }

        public async Task AddAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
        }

        public async Task RevokeAllForUserAsync(int userId)
        {
            await _context.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
