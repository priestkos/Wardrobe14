// Sample data + the Wardrobe14 main window (Windows/MainWindow.cs), shared by every scene and the video.
// Rows are filtered by a JS copy of Logic/ModGrid.cs, so counts always agree with the plugin's own rules.

// ModRow(Directory, Name, Path); Penumbra sorts by Path, ignoring case (Ipc/PenumbraIpc.cs Mods()).
const MODS = [
  "Body/Sample Body Base",
  "Gear/Outfits/Sample Outfit",
  "Gear/Outfits/Sample Outfit Alt",
  "Gear/Shoes/Example Boots",
  "Hair/Example Hair Pack",
  "Hair/Short/Example Bob",
  "Poses/Example Idle Pose",
  "UI/Example Hotbar Theme",
  "VFX/Sample Glow Effect",
  "Weapons/Example Sword Recolor",
].map((path) => {
  const name = path.slice(path.lastIndexOf("/") + 1);
  return { dir: name, name, path };
});

// Penumbra GetCollections(); "Main" is the player's (GetCollectionForObject(0)), the rest may be pinned.
const COLLECTIONS = ["Alt", "Default", "Gpose", "Main", "Testing"];

// GetAllModSettings per collection: dir -> [enabled, priority, inherited, temporary]. Missing = no settings = off.
const SETTINGS = {
  Main: {
    "Sample Body Base": [true, 0, false, false],
    "Sample Outfit": [true, 10, false, false],
    "Sample Outfit Alt": [false, 0, false, false],
    "Example Boots": [true, 0, false, false],
    "Example Hair Pack": [true, 5, false, true],
    "Example Idle Pose": [false, 0, false, false],
    "Example Hotbar Theme": [true, 0, true, false],
    "Sample Glow Effect": [false, 0, false, false],
    "Example Sword Recolor": [true, 0, false, false],
  },
  Gpose: {
    "Sample Body Base": [true, 0, true, false],
    "Sample Outfit": [false, 0, false, false],
    "Sample Outfit Alt": [true, 10, false, false],
    "Example Boots": [true, 0, true, false],
    "Example Hair Pack": [false, 0, false, false],
    "Example Bob": [true, 5, false, false],
    "Example Idle Pose": [true, 0, false, false],
    "Example Hotbar Theme": [true, 0, true, false],
    "Example Sword Recolor": [false, 0, false, false],
  },
  Alt: {
    "Sample Body Base": [true, 0, true, false],
    "Sample Outfit": [false, 0, true, false],
    "Example Boots": [true, 0, true, false],
    "Example Hair Pack": [true, 0, false, false],
    "Example Idle Pose": [false, 0, false, false],
    "Example Hotbar Theme": [true, 0, false, false],
    "Sample Glow Effect": [false, 0, false, false],
    "Example Sword Recolor": [true, 0, true, false],
  },
  Default: {
    "Sample Body Base": [true, 0, false, false],
    "Example Boots": [true, 0, false, false],
    "Example Hotbar Theme": [true, 0, false, false],
  },
  Testing: {},
};

const PLAYER = "Main";
const PINNED = ["Gpose", "Alt"]; // Configuration.PinnedCollections
const PENUMBRA_API = "5.13"; // illustrative: the running Penumbra reports this, not the bundled package
const GLAMOURER_API = "1.7"; // illustrative, same reason

// MainWindow.Reload(): "(you)" column first, then each pinned collection.
const COLUMNS = [{ id: PLAYER, label: `${PLAYER} (you)` }, ...PINNED.map((id) => ({ id, label: id }))];

const isOn = (col, dir) => !!(SETTINGS[col][dir] && SETTINGS[col][dir][0]);

// ModGrid.Filter
function filterRows(s) {
  const prefix = s.folder.trim().replace(/^\/+|\/+$/g, "").toLowerCase();
  const text = s.text.toLowerCase();
  return MODS.filter((r) => {
    if (text && !r.name.toLowerCase().includes(text) && !r.path.toLowerCase().includes(text)) return false;
    if (prefix && !r.path.toLowerCase().startsWith(prefix + "/")) return false;
    if (s.onIn >= 0 && s.onIn < COLUMNS.length && !isOn(COLUMNS[s.onIn].id, r.dir)) return false;
    if (s.differs && new Set(COLUMNS.map((c) => isOn(c.id, r.dir))).size < 2) return false;
    return true;
  });
}

// ModGrid.Folders: every folder holding a mod, parents included, sorted ignoring case.
function folders() {
  const set = new Set();
  for (const r of MODS) for (let i = r.path.indexOf("/"); i > 0; i = r.path.indexOf("/", i + 1)) set.add(r.path.slice(0, i));
  return [...set].sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase()));
}

// MainWindow.DrawCell label + tooltip
function cellLabel(col, dir) {
  const c = SETTINGS[col][dir];
  if (!c) return { text: "off", on: false, tip: null };
  return {
    text: (c[0] ? "on" : "off") + (c[2] ? " (inh)" : "") + (c[3] ? " (tmp)" : ""),
    on: c[0],
    tip: `Priority ${c[1]}${c[2] ? "\nInherited from a parent collection" : ""}${c[3] ? "\nTemporary setting" : ""}`,
  };
}

// Plugin.Probe() second line, from the same data.
function probeLines() {
  const entries = COLLECTIONS.reduce((n, c) => n + Object.keys(SETTINGS[c]).length, 0);
  return [
    `[Wardrobe14] Penumbra: OK (API ${PENUMBRA_API}); Glamourer: OK (API ${GLAMOURER_API})`,
    `[Wardrobe14] ${MODS.length} mods listed with paths in 3.4 ms; ${COLLECTIONS.length} collections, settings read in 1.2 ms total (slowest 0.4 ms, ${entries} mod entries); your collection: ${PLAYER}.`,
  ];
}

const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;");

// s: { penumbra: true|problem string, text, folder, onIn, differs, popup: null|"folder"|"onin"|"addcol", hover, tip: [rowIndex, colIndex] }
function windowHtml(s) {
  const status = (name, problem, ver) =>
    problem ? `<span class="bad">${name}: ${esc(problem)}</span>` : `<span>${name}: OK (API ${ver})</span>`;
  const pOk = s.penumbra === true;
  let h = `<div class="line">${status("Penumbra", pOk ? null : s.penumbra, PENUMBRA_API)}${status("Glamourer", null, GLAMOURER_API)}<span class="sbtn" id="b-recheck">Recheck</span>${pOk ? '<span class="sbtn" id="b-redraw">Redraw me</span>' : ""}</div>`;
  if (!pOk) return h; // MainWindow.Draw returns after the status line

  const pop = (items) =>
    `<div class="popup">${items.map((i) => `<div class="${i.sel ? "sel" : ""} ${i.label === s.hover ? "hover" : ""}" data-item="${esc(i.label)}">${esc(i.label)}</div>`).join("")}</div>`;

  // DrawColumnPicker
  h += `<div class="line"><span>Columns:</span>${PINNED.map((id) => `<span class="sbtn">${id}&nbsp; x</span>`).join("")}`;
  h += `<span class="combo w200" id="c-addcol">+ Add collection${
    s.popup === "addcol" ? pop(COLLECTIONS.filter((c) => !COLUMNS.some((k) => k.id === c)).map((label) => ({ label }))) : ""
  }</span></div>`;

  // DrawFilters
  const onLabel = s.onIn < 0 ? "Any state" : `On in ${COLUMNS[s.onIn].label}`;
  h += `<div class="line">`;
  h += `<span class="combo input w180" id="c-text">${s.text ? esc(s.text) : '<span class="dis">Search name or path</span>'}</span>`;
  h += `<span class="combo w180" id="c-folder">${esc(s.folder || "All folders")}${
    s.popup === "folder" ? pop([{ label: "All folders", sel: !s.folder }, ...folders().map((f) => ({ label: f, sel: f === s.folder }))]) : ""
  }</span>`;
  h += `<span class="combo w160" id="c-onin">${esc(onLabel)}${
    s.popup === "onin" ? pop([{ label: "Any state", sel: s.onIn < 0 }, ...COLUMNS.map((c, i) => ({ label: `On in ${c.label}`, sel: s.onIn === i }))]) : ""
  }</span>`;
  h += `<span class="checkbox-box" id="cb-differs">${s.differs ? "✓" : ""}</span><span>Differs between columns</span></div>`;

  // DrawGrid
  const rows = filterRows(s);
  h += `<div class="dis">${rows.length} of ${MODS.length} mods</div>`;
  h += `<table class="grid"><tr><th class="st">Mod</th><th class="st">Folder</th>${COLUMNS.map((c) => `<th class="fx">${esc(c.label)}</th>`).join("")}</tr>`;
  rows.forEach((r, ri) => {
    const slash = r.path.lastIndexOf("/");
    h += `<tr><td>${esc(r.name)}</td><td class="dis">${esc(slash > 0 ? r.path.slice(0, slash) : "")}</td>`;
    COLUMNS.forEach((c, ci) => {
      const l = cellLabel(c.id, r.dir);
      const tip = s.tip && s.tip[0] === ri && s.tip[1] === ci && l.tip ? `<div class="tooltip">${esc(l.tip).replace(/\n/g, "<br>")}</div>` : "";
      h += `<td class="${l.on ? "" : "dis"}" id="cell-${ri}-${ci}">${l.text}${tip}</td>`;
    });
    h += `</tr>`;
  });
  return h + `</table>`;
}

function renderWindow(el, s) {
  el.querySelector(".imgui-body").innerHTML = windowHtml({ penumbra: true, text: "", folder: "", onIn: -1, differs: false, popup: null, ...s });
}
