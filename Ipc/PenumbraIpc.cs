using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Wardrobe14.Logic;

namespace Wardrobe14.Ipc;

/// <summary> Read-only Penumbra IPC (M0/M1). Every call is caught: Penumbra can unload between any two frames. </summary>
public sealed class PenumbraIpc : IDisposable
{
    public const int BreakingVersion = 5;

    private readonly ApiVersion apiVersion;
    private readonly GetModList getModList;
    private readonly GetCollections getCollections;
    private readonly GetCollectionForObject getCollectionForObject;
    private readonly GetAllModSettings getAllModSettings;
    private readonly GetModPath getModPath;
    private readonly RedrawObject redrawObject;
    private readonly IDisposable[] events;

    /// <summary> Null when usable, otherwise why not. </summary>
    public string? Problem { get; private set; } = "not checked";
    public (int Breaking, int Feature) Version { get; private set; }

    // shortcut: plain flags, not per-collection invalidation; the window reloads its few visible columns. Upgrade if reloads show up in the probe timings.
    // Penumbra may raise events off the framework thread, so these are only ever set here and cleared by the window.
    public volatile bool ModsStale = true;
    public volatile bool SettingsStale = true;

    public PenumbraIpc(IDalamudPluginInterface pi)
    {
        apiVersion = new ApiVersion(pi);
        getModList = new GetModList(pi);
        getCollections = new GetCollections(pi);
        getCollectionForObject = new GetCollectionForObject(pi);
        getAllModSettings = new GetAllModSettings(pi);
        getModPath = new GetModPath(pi);
        redrawObject = new RedrawObject(pi);
        events =
        [
            Initialized.Subscriber(pi, () => { Check(); ModsStale = SettingsStale = true; }),
            Disposed.Subscriber(pi, () => Problem = "Penumbra unloaded"),
            ModSettingChanged.Subscriber(pi, (_, _, _, _) => SettingsStale = true),
            ModAdded.Subscriber(pi, _ => ModsStale = SettingsStale = true),
            ModDeleted.Subscriber(pi, _ => ModsStale = SettingsStale = true),
            ModMoved.Subscriber(pi, (_, _) => ModsStale = SettingsStale = true),
        ];
        Check();
    }

    public void Check()
    {
        try
        {
            Version = apiVersion.Invoke();
            Problem = Version.Breaking == BreakingVersion ? null : $"Penumbra API {Version.Breaking}.{Version.Feature}, need {BreakingVersion}.x";
        }
        catch (Exception)
        {
            Problem = "Penumbra not loaded";
        }
    }

    public bool Ok => Problem == null;

    /// <summary> Runs an IPC read; on failure records the reason and returns the fallback. </summary>
    private T Try<T>(Func<T> call, T fallback)
    {
        if (!Ok)
            return fallback;
        try
        {
            return call();
        }
        catch (Exception e)
        {
            Problem = $"Penumbra IPC failed: {e.Message}";
            return fallback;
        }
    }

    public Dictionary<Guid, string> Collections()
        => Try(() => getCollections.Invoke(), new Dictionary<Guid, string>());

    /// <summary> The collection the local player (object 0) actually uses, or null. </summary>
    public (Guid Id, string Name)? PlayerCollection()
        => Try<(Guid, string)?>(() =>
        {
            var (valid, _, effective) = getCollectionForObject.Invoke(0);
            return valid ? effective : null;
        }, null);

    /// <summary> Every mod with its sort path. One GetModPath call per mod; the probe times it. </summary>
    public List<ModRow> Mods()
        => Try(() => getModList.Invoke()
            .Select(kv =>
            {
                var (ec, path, _, _) = getModPath.Invoke(kv.Key, kv.Value);
                return new ModRow(kv.Key, kv.Value, ec == PenumbraApiEc.Success ? path : kv.Value);
            })
            .OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToList(), []);

    /// <summary> Effective settings (inheritance and our-visible temporary settings included) for one collection. </summary>
    public Dictionary<string, ModCell> Settings(Guid collection)
        => Try(() =>
        {
            var (ec, map) = getAllModSettings.Invoke(collection, false, false, 0);
            if (ec != PenumbraApiEc.Success || map == null)
                return new Dictionary<string, ModCell>();
            return map.ToDictionary(kv => kv.Key, kv => new ModCell(kv.Value.Item1, kv.Value.Item2, kv.Value.Item4, kv.Value.Item5));
        }, new Dictionary<string, ModCell>());

    public void RedrawPlayer()
        => Try(() => { redrawObject.Invoke(0, RedrawType.Redraw); return 0; }, 0);

    public void Dispose()
    {
        foreach (var e in events)
            e.Dispose();
    }
}
