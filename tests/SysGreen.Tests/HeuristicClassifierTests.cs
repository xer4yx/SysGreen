using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

/// <summary>
/// The heuristic fallback (ADR-0002): infer a rough Purpose/Safety from signer/path/name when the
/// Knowledge Base has no match. Conservative on Safety — only clear background updaters become Safe,
/// security software is protected, everything else stays Caution.
/// </summary>
public class HeuristicClassifierTests
{
    private static AutostartEntry E(
        string? exe, string? publisher = null, string display = "Item", string? target = null) =>
        new("id", display, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            exe, publisher, AutostartState.Enabled) { TargetExecutable = target };

    [Fact]
    public void A_background_updater_is_safe_to_disable()
    {
        var c = HeuristicClassifier.Classify(E(@"C:\Program Files (x86)\Google\Update\GoogleUpdate.exe"));

        Assert.NotNull(c);
        Assert.Equal(Purpose.Updater, c!.Purpose);
        Assert.Equal(SafetyRating.Safe, c.Safety);
        Assert.Equal(ClassificationSource.Heuristic, c.Source);
    }

    [Fact]
    public void An_updater_is_recognised_by_its_display_name_too()
    {
        // A logon scheduled task's display name carries the "update" signal even when the exe doesn't.
        var c = HeuristicClassifier.Classify(E(
            @"C:\Program Files\Common Files\Microsoft Shared\ClickToRun\OfficeC2RClient.exe",
            display: "Office Automatic Updates 2.0"));

        Assert.Equal(Purpose.Updater, c!.Purpose);
        Assert.Equal(SafetyRating.Safe, c.Safety);
    }

    [Fact]
    public void Security_software_is_protected_as_do_not_touch()
    {
        var c = HeuristicClassifier.Classify(E(@"C:\Program Files\McAfee\agent.exe", publisher: "McAfee, LLC"));

        Assert.Equal(Purpose.Security, c!.Purpose);
        Assert.Equal(SafetyRating.DoNotTouch, c.Safety);
    }

    [Fact]
    public void A_security_updater_stays_protected_not_marked_safe()
    {
        var c = HeuristicClassifier.Classify(E(
            @"C:\Program Files\Malwarebytes\Anti-Malware\mbupdate.exe", publisher: "Malwarebytes Inc"));

        Assert.Equal(Purpose.Security, c!.Purpose);
        Assert.Equal(SafetyRating.DoNotTouch, c.Safety);
    }

    [Fact]
    public void A_windows_component_is_labelled_but_left_at_caution()
    {
        var c = HeuristicClassifier.Classify(E(@"C:\Windows\System32\sihost.exe"));

        Assert.Equal(Purpose.WindowsCore, c!.Purpose);
        Assert.Equal(SafetyRating.Caution, c.Safety);
    }

    [Fact]
    public void A_windows_item_named_update_stays_caution_not_safe()
    {
        // A Windows-Update component must never be auto-recommended just because it says "update".
        var c = HeuristicClassifier.Classify(E(
            @"C:\Windows\System32\MoUsoCoreWorker.exe", display: "Update Orchestrator"));

        Assert.Equal(Purpose.WindowsCore, c!.Purpose);
        Assert.Equal(SafetyRating.Caution, c.Safety);
    }

    [Fact]
    public void Device_maker_software_is_labelled_oem_at_caution()
    {
        var c = HeuristicClassifier.Classify(E(
            @"C:\Program Files (x86)\Lenovo\LenovoNow\LenovoNow.Task.exe", publisher: "Lenovo"));

        Assert.Equal(Purpose.OemDriver, c!.Purpose);
        Assert.Equal(SafetyRating.Caution, c.Safety);
    }

    [Fact]
    public void A_squirrel_launcher_is_not_mistaken_for_an_updater()
    {
        // Update.exe fronts a real app (TargetExecutable set) — don't call the whole app an updater.
        var c = HeuristicClassifier.Classify(E(
            @"C:\Users\me\AppData\Local\Discord\Update.exe", display: "Discord", target: "Discord.exe"));

        Assert.Null(c);
    }

    [Fact]
    public void An_ordinary_unknown_app_yields_no_heuristic()
    {
        Assert.Null(HeuristicClassifier.Classify(E(@"C:\Program Files\Acme\Acme.exe", publisher: "Acme Corp")));
    }

    [Fact]
    public void An_entry_with_no_metadata_yields_no_heuristic()
    {
        Assert.Null(HeuristicClassifier.Classify(E(null)));
    }
}
