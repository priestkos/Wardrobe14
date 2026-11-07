using Dalamud.Plugin;
using Glamourer.Api.IpcSubscribers;

namespace Wardrobe14.Ipc;

/// <summary> Glamourer availability gate only (M0). Looks (M4) add design calls here. </summary>
public sealed class GlamourerIpc : IDisposable
{
    // Glamourer's own ApiVersion major (GlamourerApi.CurrentApiVersionMajor), not the Glamourer.Api package version.
    public const int MajorVersion = 1;

    private readonly ApiVersion apiVersion;
    private readonly IDisposable[] events;

    public string? Problem { get; private set; } = "not checked";
    public (int Major, int Minor) Version { get; private set; }

    public GlamourerIpc(IDalamudPluginInterface pi)
    {
        apiVersion = new ApiVersion(pi);
        events =
        [
            Initialized.Subscriber(pi, Check),
            Disposed.Subscriber(pi, () => Problem = "Glamourer unloaded"),
        ];
        Check();
    }

    public void Check()
    {
        try
        {
            Version = apiVersion.Invoke();
            Problem = Version.Major == MajorVersion ? null : $"Glamourer API {Version.Major}.{Version.Minor}, need {MajorVersion}.x";
        }
        catch (Exception)
        {
            Problem = "Glamourer not loaded";
        }
    }

    public void Dispose()
    {
        foreach (var e in events)
            e.Dispose();
    }
}
