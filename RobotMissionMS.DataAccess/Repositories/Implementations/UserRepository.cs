using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class UserRepository(RobotMissionDbContext db) : IUserRepository
{
    public async Task<UserListPage> GetUsersAsync(
        int skip,
        int take,
        string? role,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(user => user.Role.Name == role);
        if (isActive.HasValue)
            query = query.Where(user => user.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(take)
            .Select(user => new UserListRow(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role.Name,
                user.IsActive,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return new UserListPage(items, total);
    }
}
