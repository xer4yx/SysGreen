using SysGreen.Core;
using SysGreen.Core.Abstractions;

namespace SysGreen.Tests;

public class RestorePointServiceTests
{
    [Fact]
    public void A_newly_created_point_is_success()
    {
        var service = new RestorePointService(new FakeApi(RestorePointStatus.Created));

        Assert.True(service.TryCreateRestorePoint("before apply"));
    }

    [Fact]
    public void A_recent_existing_point_counts_as_success()
    {
        // Windows throttles to ~one point per 24h; a recent one is still a valid lifeline,
        // so a second Apply in the same day must not be blocked.
        var service = new RestorePointService(new FakeApi(RestorePointStatus.AlreadyExistsRecently));

        Assert.True(service.TryCreateRestorePoint("before apply"));
    }

    [Fact]
    public void A_failed_attempt_is_not_success()
    {
        var service = new RestorePointService(new FakeApi(RestorePointStatus.Failed));

        Assert.False(service.TryCreateRestorePoint("before apply"));
    }

    [Fact]
    public void An_api_exception_is_swallowed_as_failure()
    {
        // e.g. System Restore disabled, process not elevated, or WMI unavailable.
        var service = new RestorePointService(new ThrowingApi());

        Assert.False(service.TryCreateRestorePoint("before apply"));
    }

    private sealed class FakeApi(RestorePointStatus status) : IRestorePointApi
    {
        public RestorePointStatus CreateRestorePoint(string description) => status;
    }

    private sealed class ThrowingApi : IRestorePointApi
    {
        public RestorePointStatus CreateRestorePoint(string description) =>
            throw new InvalidOperationException("System Restore is disabled");
    }
}
