using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;

internal static class PostgresAssertions
{
    /// <summary>Chạy <paramref name="action"/> và trả PostgresException gốc (kể cả khi bọc trong DbUpdateException).</summary>
    public static async Task<PostgresException> ThrowsPostgresAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            return pg;
        }
        catch (PostgresException pg)
        {
            return pg;
        }

        throw new Xunit.Sdk.XunitException("Mong đợi PostgresException nhưng thao tác đã thành công.");
    }
}
