using Microsoft.EntityFrameworkCore;

namespace SelfHostLlm.Persistence;

public static class DatabaseStatus
{
    /// <summary>
    /// DB kết nối được và không còn migration chờ áp dụng. Không tự migrate — việc đó do người
    /// vận hành chạy <c>dotnet ef database update</c>.
    /// </summary>
    public static async Task<bool> IsSchemaUpToDateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        return !pending.Any();
    }
}
