using Dapper;
using SysGreen.Core.Usage;

namespace SysGreen.Data;

/// <summary>
/// Clears the user's accumulated data in place (ADR-0017 in-app reset). Truncates every mutable
/// table but leaves the schema, so the running app keeps working against an empty store.
/// </summary>
public sealed class DataStoreReset : IDataStoreReset
{
    private readonly IConnectionFactory _factory;

    public DataStoreReset(IConnectionFactory factory) => _factory = factory;

    public void Reset()
    {
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            DELETE FROM usage;
            DELETE FROM change_record;
            DELETE FROM override;
            DELETE FROM setting;
            """);
    }
}
