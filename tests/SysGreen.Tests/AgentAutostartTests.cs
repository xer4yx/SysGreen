using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class AgentAutostartTests
{
    private const string Value = "SysGreen Agent";
    private const string Command = @"C:\Program Files\SysGreen\SysGreen.Agent.exe";

    [Fact]
    public void Registers_the_agent_when_tracking_is_enabled_and_not_present()
    {
        var reg = new FakeReg();

        new AgentAutostart(reg).Sync(trackingEnabled: true, Value, Command);

        Assert.Equal(Command, reg.Get(Value));
    }

    [Fact]
    public void Leaves_an_existing_registration_untouched()
    {
        // The user may have disabled it via StartupApproved — the Run value is still present.
        // Re-running must not overwrite or re-enable it (ADR-0014: listed + user-disableable).
        var reg = new FakeReg();
        reg.Set(Value, "old-path");

        new AgentAutostart(reg).Sync(trackingEnabled: true, Value, Command);

        Assert.Equal("old-path", reg.Get(Value));
    }

    [Fact]
    public void Removes_the_registration_when_tracking_is_turned_off()
    {
        var reg = new FakeReg();
        reg.Set(Value, Command);

        new AgentAutostart(reg).Sync(trackingEnabled: false, Value, Command);

        Assert.False(reg.Exists(Value));
    }

    [Fact]
    public void Does_nothing_when_disabled_and_already_absent()
    {
        var reg = new FakeReg();

        new AgentAutostart(reg).Sync(trackingEnabled: false, Value, Command);

        Assert.Equal(0, reg.RemoveCount);
    }

    private sealed class FakeReg : IRunKeyRegistration
    {
        private readonly Dictionary<string, string> _d = new();
        public int RemoveCount { get; private set; }
        public bool Exists(string valueName) => _d.ContainsKey(valueName);
        public void Set(string valueName, string command) => _d[valueName] = command;
        public void Remove(string valueName) { if (_d.Remove(valueName)) RemoveCount++; }
        public string? Get(string valueName) => _d.GetValueOrDefault(valueName);
    }
}
