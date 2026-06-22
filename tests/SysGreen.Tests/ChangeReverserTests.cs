using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public class ChangeReverserTests
{
    private static readonly DateTime When = new(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

    private static ChangeRecord DisabledHkcu(string name) =>
        new(Guid.NewGuid().ToString("n"), $"HKCU:{name}", name, ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved", When, true, null)
        { Location = AutostartLocation.RegistryRunCurrentUser };

    [Fact]
    public void Reversing_a_disable_applies_an_enable_for_the_same_item()
    {
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([DisabledHkcu("Spotify")]);

        var change = Assert.Single(apply.LastBatch);
        Assert.Equal(ChangeAction.Enable, change.Action);
        Assert.Equal("HKCU:Spotify", change.Entry.Id);
        Assert.Equal("Spotify", change.Entry.DisplayName);
        Assert.Equal(AutostartLocation.RegistryRunCurrentUser, change.Entry.Location);
    }

    [Fact]
    public void Reversing_an_enable_applies_a_disable()
    {
        var record = new ChangeRecord("id", "HKCU:Spotify", "Spotify", ChangeAction.Enable,
            "Disabled", "Enabled", "StartupApproved", When, true, null)
            { Location = AutostartLocation.RegistryRunCurrentUser };
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([record]);

        Assert.Equal(ChangeAction.Disable, Assert.Single(apply.LastBatch).Action);
    }

    [Fact]
    public void Reversing_targets_the_recorded_mechanism_key_not_the_display_name()
    {
        // A Startup-folder item disables under its shortcut file name; the undo must use the same key.
        var record = new ChangeRecord("id", "Folder:Spotify", "Spotify", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved", When, true, null)
            { Location = AutostartLocation.StartupFolderCurrentUser, MechanismKey = "Spotify.lnk" };
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([record]);

        Assert.Equal("Spotify.lnk", Assert.Single(apply.LastBatch).Entry.MechanismKey);
    }

    [Fact]
    public void Reversing_an_admin_change_carries_the_elevation_requirement()
    {
        var record = new ChangeRecord("id", "HKLM:Updater", "Updater", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved", When, true, null)
            { Location = AutostartLocation.RegistryRunLocalMachine };
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([record]);

        // The inverse change keeps the admin location, so RoutingApplyService elevates it (ADR-0004).
        Assert.True(Assert.Single(apply.LastBatch).Entry.RequiresElevation);
    }

    [Fact]
    public void Reversing_a_batch_inverts_every_reversible_record_in_one_apply()
    {
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([DisabledHkcu("Spotify"), DisabledHkcu("Discord")]);

        Assert.Equal(1, apply.CallCount); // a single batch, so one restore point / one UAC prompt
        Assert.Equal(2, apply.LastBatch.Count);
        Assert.All(apply.LastBatch, c => Assert.Equal(ChangeAction.Enable, c.Action));
    }

    [Fact]
    public void An_end_task_record_is_not_reversible_and_is_skipped()
    {
        var endTask = new ChangeRecord("id", "4242", "Spotify", ChangeAction.EndTask,
            "Running", "Ended", "ProcessKill", When, true, null);
        var apply = new CapturingApplyService();

        var result = new ChangeReverser(apply).Reverse([endTask]);

        Assert.Equal(0, apply.CallCount); // nothing to apply
        Assert.Empty(result.Records);
    }

    [Fact]
    public void A_failed_change_is_skipped_because_nothing_actually_changed()
    {
        var failed = new ChangeRecord("id", "HKCU:Spotify", "Spotify", ChangeAction.Disable,
            "Enabled", "Unknown", "StartupApproved", When, false, "access denied")
            { Location = AutostartLocation.RegistryRunCurrentUser };
        var apply = new CapturingApplyService();

        new ChangeReverser(apply).Reverse([failed]);

        Assert.Equal(0, apply.CallCount);
    }

    private sealed class CapturingApplyService : IApplyService
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<PendingChange> LastBatch { get; private set; } = [];

        public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
        {
            CallCount++;
            LastBatch = changes;
            return new ApplyResult(false, false, changes.Select(c =>
                new ChangeRecord("r", c.Entry.Id, c.Entry.DisplayName, c.Action,
                    "x", "y", "StartupApproved", When, true, null)).ToList());
        }
    }
}
