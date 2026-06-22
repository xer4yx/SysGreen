using Dapper;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Data;

/// <summary>
/// Persists user Overrides in the local <c>override</c> table (ADR-0006). Loaded into memory once so
/// the classifier can consult it per item without a round-trip; writes update both the DB and the
/// in-memory snapshot. Keyed by the normalized executable name.
/// </summary>
public sealed class OverrideRepository : IOverrideStore
{
    private readonly IConnectionFactory _factory;
    private readonly Dictionary<string, UserOverride> _cache;

    public OverrideRepository(IConnectionFactory factory)
    {
        _factory = factory;
        _cache = LoadAll();
    }

    public UserOverride? Get(string executableName) =>
        _cache.GetValueOrDefault(OverrideKey.Normalize(executableName));

    public IReadOnlyList<UserOverride> GetAll() => _cache.Values.ToList();

    public void Set(UserOverride ov)
    {
        var key = OverrideKey.Normalize(ov.ExecutableName);
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            INSERT INTO override (executable_path, purpose, never_recommend)
            VALUES (@Key, @Purpose, @Never)
            ON CONFLICT(executable_path) DO UPDATE SET
                purpose = @Purpose, never_recommend = @Never;
            """,
            new { Key = key, Purpose = ov.Purpose?.ToString(), Never = ov.NeverRecommend ? 1 : 0 });
        _cache[key] = ov with { ExecutableName = key };
    }

    public void Remove(string executableName)
    {
        var key = OverrideKey.Normalize(executableName);
        using var c = _factory.OpenConnection();
        c.Execute("DELETE FROM override WHERE executable_path = @Key;", new { Key = key });
        _cache.Remove(key);
    }

    private Dictionary<string, UserOverride> LoadAll()
    {
        using var c = _factory.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT executable_path, purpose, never_recommend FROM override;";
        using var reader = cmd.ExecuteReader();

        var cache = new Dictionary<string, UserOverride>();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            Purpose? purpose = reader.IsDBNull(1) ? null : Enum.Parse<Purpose>(reader.GetString(1));
            cache[key] = new UserOverride(key, purpose, reader.GetInt64(2) != 0);
        }
        return cache;
    }
}
