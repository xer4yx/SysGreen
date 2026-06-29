using Dapper;
using SysGreen.Core.Usage;

namespace SysGreen.Data;

/// <summary>
/// Local app settings in the <c>setting</c> table (ADR-0006). Currently the launch-tracking off
/// switch (ADR-0012), which is on by default until the user turns it off.
/// </summary>
public sealed class SettingsRepository
    : ITrackingSettings, IOnboardingState, IDataRetentionSettings, IPolicyAcceptance, IThresholdSettings
{
    private const string LaunchTrackingKey = "launch_tracking_enabled";
    private const string FirstRunCompleteKey = "first_run_complete";
    private const string KeepDataOnUninstallKey = "keep_data_on_uninstall";
    private const string AcceptedPolicyVersionKey = "accepted_policy_version";
    private const string AbandonedThresholdDaysKey = "abandoned_threshold_days";

    private readonly IConnectionFactory _factory;

    public SettingsRepository(IConnectionFactory factory) => _factory = factory;

    public bool LaunchTrackingEnabled => ReadBool(LaunchTrackingKey, defaultValue: true);

    public void SetLaunchTrackingEnabled(bool enabled) => WriteBool(LaunchTrackingKey, enabled);

    public bool FirstRunComplete => ReadBool(FirstRunCompleteKey, defaultValue: false);

    public void MarkFirstRunComplete() => WriteBool(FirstRunCompleteKey, true);

    /// <summary>Whether the Data Store is kept when SysGreen is uninstalled. Keep by default (ADR-0017).</summary>
    public bool KeepDataOnUninstall => ReadBool(KeepDataOnUninstallKey, defaultValue: true);

    public void SetKeepDataOnUninstall(bool keep) => WriteBool(KeepDataOnUninstallKey, keep);

    /// <summary>The accepted Privacy Policy &amp; Terms version (ADR-0018). 0 = nothing accepted yet.</summary>
    public int AcceptedPolicyVersion => ReadInt(AcceptedPolicyVersionKey, defaultValue: 0);

    public void SetAcceptedPolicyVersion(int version) => WriteInt(AcceptedPolicyVersionKey, version);

    /// <summary>Days without a launch before an app is "Abandoned" (CONTEXT.md). Default 30.</summary>
    public int AbandonedThresholdDays => ReadInt(AbandonedThresholdDaysKey, defaultValue: 30);

    public void SetAbandonedThresholdDays(int days) => WriteInt(AbandonedThresholdDaysKey, days);

    private bool ReadBool(string key, bool defaultValue)
    {
        using var c = _factory.OpenConnection();
        var value = c.QuerySingleOrDefault<string?>(
            "SELECT value FROM setting WHERE key = @Key;", new { Key = key });
        return value is null ? defaultValue : value == "1";
    }

    private void WriteBool(string key, bool value)
    {
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            INSERT INTO setting (key, value) VALUES (@Key, @Value)
            ON CONFLICT(key) DO UPDATE SET value = @Value;
            """,
            new { Key = key, Value = value ? "1" : "0" });
    }

    private int ReadInt(string key, int defaultValue)
    {
        using var c = _factory.OpenConnection();
        var value = c.QuerySingleOrDefault<string?>(
            "SELECT value FROM setting WHERE key = @Key;", new { Key = key });
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private void WriteInt(string key, int value)
    {
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            INSERT INTO setting (key, value) VALUES (@Key, @Value)
            ON CONFLICT(key) DO UPDATE SET value = @Value;
            """,
            new { Key = key, Value = value.ToString() });
    }
}
