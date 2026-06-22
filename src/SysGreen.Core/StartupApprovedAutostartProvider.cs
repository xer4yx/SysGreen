using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Startup;

/// <summary>
/// Decorates a raw autostart provider, resolving each entry's real enable/disable state from the
/// Windows StartupApproved flags (ADR-0005). Without this, every enumerated item reads as Enabled,
/// so a disabled item would still be recommended for disabling. Pure logic over the injected store.
/// </summary>
public sealed class StartupApprovedAutostartProvider : IAutostartProvider
{
    private readonly IAutostartProvider _inner;
    private readonly IStartupApprovedStore _store;

    public StartupApprovedAutostartProvider(IAutostartProvider inner, IStartupApprovedStore store)
    {
        _inner = inner;
        _store = store;
    }

    public IReadOnlyList<AutostartEntry> Enumerate() =>
        _inner.Enumerate().Select(ResolveState).ToList();

    private AutostartEntry ResolveState(AutostartEntry entry)
    {
        // Only Run-key and Startup-folder entries carry StartupApproved flags; others (e.g. scheduled
        // tasks) keep whatever state their own provider reported.
        if (!HasStartupApprovedFlag(entry.Location)) return entry;

        var flag = _store.ReadFlag(entry.Location, entry.StartupApprovedValueName);
        var state = StartupApprovedFlag.IsEnabled(flag) ? AutostartState.Enabled : AutostartState.Disabled;
        return entry with { State = state };
    }

    private static bool HasStartupApprovedFlag(AutostartLocation location) => location is
        AutostartLocation.RegistryRunCurrentUser or
        AutostartLocation.RegistryRunLocalMachine or
        AutostartLocation.StartupFolderCurrentUser or
        AutostartLocation.StartupFolderCommon;
}
