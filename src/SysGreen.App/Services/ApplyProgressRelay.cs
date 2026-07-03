using SysGreen.Core.Apply;

namespace SysGreen.App.Services;

/// <summary>
/// App-side progress fan-out (Topic B / Phase 6): the elevated Helper client reports polled phases
/// here and the <c>MainViewModel</c> listens to update the header strip. A shared singleton bridges the
/// two — the process-launching client can't see the view-model. Updates arrive on the Apply background
/// thread; the view-model only writes scalar bound properties, which WPF's binding engine marshals to
/// the UI thread, so no explicit dispatching is needed here.
/// </summary>
public sealed class ApplyProgressRelay : IApplyProgressSink
{
    public event Action<ApplyProgress>? ProgressReported;

    public void Report(ApplyProgress progress) => ProgressReported?.Invoke(progress);
}
