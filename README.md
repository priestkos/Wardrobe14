# Wardrobe14

<img src="images/icon.png" width="96">

A cross-collection view of your Penumbra mods: every mod against your own collection and any others you pin, with search and folder filters. Bulk toggles, mod sets, undo and one-click Glamourer looks are planned.

`/pmm` · Dalamud API 15 · .NET 10

> **No support will be provided. This software is to be used as is, without warranty of any kind.**

> [!NOTE]
> **Early build.** This release only **reads** from Penumbra and Glamourer. It cannot change a mod setting yet; the only thing it can trigger is a redraw of your own character. Writing features arrive in later releases, each with an automatic snapshot and undo.

## What it does

- **Mod grid across collections.** One row per installed mod, showing its name and Penumbra folder. There is one column for the collection your character is using, marked `(you)`, plus one column for each collection you pin. Penumbra's own window shows one collection at a time. Here you can see the same mod in several collections side by side.
- **Cell states.** Each cell shows whether the mod is `on` or `off` in that collection. `(inh)` means the setting is inherited from a parent collection. `(tmp)` means it is a temporary setting placed by a plugin. Hover a cell to see the mod's priority. A mod with no settings in a collection shows as `off`.
- **Filters.** You can filter by:
  - text, matched against the mod name or the full Penumbra sort path;
  - a Penumbra folder (picked from every folder that holds a mod, parents included);
  - **On in \<collection\>**, to show only mods enabled in one column;
  - **Differs between columns**, to show only mods that are on in one column and off in another.

  A counter shows how many of the installed mods are listed.
- **Pinned columns.** The **+ Add collection** dropdown pins a collection as a column, and the `x` on its button removes it. Pinned columns are saved and come back on the next launch.
- **Status line.** Shows whether Penumbra and Glamourer are loaded and which IPC API version each reports. A **Recheck** button runs the checks again and re-reads everything. A **Redraw me** button redraws your character.
- **`/pmm probe`.** Prints the API versions, how many mods and collections exist, which collection your character uses, and how long the reads took in milliseconds. Those timings decide how the later writing features refresh the grid.

### What it is not meant to do

- **It does not delete, install, move or rename mods.** Penumbra's IPC offers these calls, but deleting a mod cannot be undone, so Wardrobe14 leaves file management to Penumbra.
- **It does not apply anything on its own.** There are no zone, job, gearset or timer triggers. Glamourer's Automation tab already does that. Every future change will come from a button press or a typed command.
- **It does not edit Glamourer designs, Penumbra mod files or either plugin's config files.** Everything goes through their public IPC.
- **It does not replace Penumbra's window.** Mod options, conflicts, file editing and the changed-items view stay in Penumbra.

## How it works

All data comes from Penumbra's and Glamourer's official IPC, through their own NuGet subscriber classes. There are no hooks, no signatures and no native memory reads.

| Read | Penumbra IPC |
| --- | --- |
| Mod list and sort paths | `GetModList`, then `GetModPath` once per mod |
| Collections and your character's collection | `GetCollections`, `GetCollectionForObject(0)` |
| Each column's state | `GetAllModSettings(collection, ignoreInheritance: false, ignoreTemporary: false, key: 0)` |
| Redraw | `RedrawObject(0, Redraw)` |

- **Refreshing.** The grid reloads when Penumbra raises `ModSettingChanged`, `ModAdded`, `ModDeleted`, `ModMoved` or `Initialized`, and when the window opens. It never polls. Only the visible columns are re-read.
- **Version checks.** Penumbra must report breaking API version 5. Glamourer must report major version 1. If a plugin is missing, unloads, or reports another version, the status line turns red and that plugin's features stay idle.
- **Failed reads.** Every IPC call is wrapped. A failure (for example, Penumbra unloading mid-frame) records its reason on the status line and the call returns nothing. It never throws out of the draw loop.
- **Bundled libraries.** The plugin ships `Penumbra.Api` 5.19.2 and `Glamourer.Api` 2.8.2.

## Commands

| Command | Effect |
| --- | --- |
| `/pmm` | Toggle the window (also the installer's main UI button) |
| `/pmm probe` | Print API versions, counts and read timings to chat (read-only) |
| `/pmm redraw` | Redraw your character |

## Settings

There is no settings window. The pinned grid columns are the only saved state, written to `pluginConfigs\Wardrobe14.json` whenever you pin or unpin a collection.

## Build

Requires the .NET 10 SDK and the Dalamud dev distribution (`%APPDATA%\XIVLauncher\addon\Hooks\dev`, or `DALAMUD_HOME`).

```
dotnet build -c Debug      # load bin/Debug/Wardrobe14.dll as a dev plugin
dotnet build -c Release    # zip in bin/Release/Wardrobe14/latest.zip
```

Releases are published automatically on each push to `main` that changes the plugin binary. The version is the next four-part number above the latest GitHub release, or the csproj version when that is already higher. The release workflow runs the tests before it builds.

## Install (dev plugin)

1. `/xlsettings` → Experimental → Dev Plugin Locations → add `bin\Debug\Wardrobe14.dll`. Point at the build folder rather than copying the DLL: `Penumbra.Api.dll`, `Glamourer.Api.dll` and their dependencies must sit beside it.
2. `/xlplugins` → Dev Tools → enable **Wardrobe14**.
3. `/pmm` opens the window.

Penumbra must be installed and loaded. Glamourer is optional in this build.

## Tests

The grid filtering (`Logic/ModGrid.cs`) has no Dalamud dependency, so the xUnit project compiles it directly. The tests cover:

- text matching on name or path, ignoring case;
- the folder filter, which matches a path prefix rather than a substring;
- a mod missing from a collection counting as off;
- **On in \<collection\>** and **Differs between columns**, including that a single column never differs and that an out-of-range column means no filter;
- the folder list including parent folders.

```
dotnet test Wardrobe14.Tests/Wardrobe14.Tests.csproj    # 6 cases
```

## Limits

- **Not verified in game.** Behaviour is taken from the code and the offline tests, not from a live client.
- **Read-only.** Bulk enable/disable, mod sets, snapshots and undo, Glamourer looks and the quick bar are planned, not built.
- **Switching collections.** The plugin re-reads your character's collection when the window opens and after a Penumbra event. A switch that raises no event (a zone change, a collection assignment) shows after **Recheck** or reopening the window.
- **Hidden temporary settings.** Temporary settings locked with another plugin's key (Glamourer's, for example) are not shown, because the grid reads with key `0`.
- **Load cost.** Loading the mod list makes one `GetModPath` IPC call per mod. `/pmm probe` reports what that costs on your install.

## Layout

```
Plugin.cs               entry point, /pmm commands, probe, window wiring
Configuration.cs        pinned collection columns
Ipc/PenumbraIpc.cs      Penumbra IPC reads, version check, change events
Ipc/GlamourerIpc.cs     Glamourer availability and version check
Logic/ModGrid.cs        pure row model, filters and folder list
Windows/MainWindow.cs   status line, column picker, filters, mod grid
Wardrobe14.Tests/       xUnit tests for Logic/ModGrid.cs
```
