using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

public class OverridingClassifierTests
{
    private static AutostartEntry Entry(string exe, string? target = null) =>
        new("id", "Item", ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            exe, null, AutostartState.Enabled) { TargetExecutable = target };

    private static Classification Base(Purpose p, SafetyRating s) =>
        new(p, s, ClassificationSource.KnowledgeBase, "base", false);

    [Fact]
    public void Never_recommend_override_forces_do_not_touch_without_changing_purpose()
    {
        var store = new FakeOverrideStore();
        store.Set(new UserOverride("Spotify.exe", Purpose: null, NeverRecommend: true));

        var c = new OverridingClassifier(new FakeClassifier(Base(Purpose.Media, SafetyRating.Safe)), store)
            .Classify(Entry(@"C:\x\Spotify.exe"));

        Assert.Equal(SafetyRating.DoNotTouch, c.Safety);
        Assert.Equal(Purpose.Media, c.Purpose);
        Assert.Equal(ClassificationSource.Override, c.Source);
    }

    [Fact]
    public void Purpose_override_relabels_but_keeps_the_base_safety()
    {
        var store = new FakeOverrideStore();
        store.Set(new UserOverride("mystery.exe", Purpose.Gaming, NeverRecommend: false));

        var c = new OverridingClassifier(new FakeClassifier(Base(Purpose.Unknown, SafetyRating.Caution)), store)
            .Classify(Entry(@"C:\x\mystery.exe"));

        Assert.Equal(Purpose.Gaming, c.Purpose);
        Assert.Equal(SafetyRating.Caution, c.Safety);
        Assert.Equal(ClassificationSource.Override, c.Source);
    }

    [Fact]
    public void No_override_passes_the_base_classification_through_unchanged()
    {
        var c = new OverridingClassifier(new FakeClassifier(Base(Purpose.Updater, SafetyRating.Safe)),
            new FakeOverrideStore()).Classify(Entry(@"C:\x\whatever.exe"));

        Assert.Equal(SafetyRating.Safe, c.Safety);
        Assert.Equal(ClassificationSource.KnowledgeBase, c.Source);
    }

    [Fact]
    public void Override_matches_by_the_squirrel_target_executable()
    {
        var store = new FakeOverrideStore();
        store.Set(new UserOverride("Discord.exe", Purpose: null, NeverRecommend: true));

        // The launcher is Update.exe; the item's identity is its target, Discord.exe.
        var c = new OverridingClassifier(
                new FakeClassifier(Base(Purpose.Communication, SafetyRating.Safe)), store)
            .Classify(Entry(@"C:\x\Update.exe", target: "Discord.exe"));

        Assert.Equal(SafetyRating.DoNotTouch, c.Safety);
        Assert.Equal(ClassificationSource.Override, c.Source);
    }

    private sealed class FakeClassifier(Classification result) : IClassifier
    {
        public Classification Classify(AutostartEntry entry) => result;
    }

    private sealed class FakeOverrideStore : IOverrideStore
    {
        private readonly Dictionary<string, UserOverride> _d = new();
        public UserOverride? Get(string n) => _d.GetValueOrDefault(OverrideKey.Normalize(n));
        public void Set(UserOverride ov) => _d[OverrideKey.Normalize(ov.ExecutableName)] = ov;
        public void Remove(string n) => _d.Remove(OverrideKey.Normalize(n));
        public IReadOnlyList<UserOverride> GetAll() => _d.Values.ToList();
    }
}
