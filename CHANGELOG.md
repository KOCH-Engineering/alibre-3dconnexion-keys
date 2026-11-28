# Changelog

## 0.7.9 – 2026-11-28
- Readable names for the few Alibre ribbon items that only have an internal id: `ACIS_DRAFT` → *Draft*, `CMD_DESIGN_CHECK_BODY` → *Design Check Body* (also for functions already on your keys).

## 0.7.8 – 2026-11-28
- Before clicking a ribbon command, the add-on checks that it is enabled and on a ribbon page that is shown. An Alibre command that is not available in the active window gives a beep and a short message (e.g. *Boss Extrude is not available in this window* in an assembly sketch) instead of doing nothing.

## 0.7.5 – 2026-11-28
- Clearer dialog: the context (Part, Assembly, Sketch, Drawing) is a row of tabs with a blue bar under the chosen one; the device (SpaceMouse Enterprise, Keyboard Pro, Radial menu) is a smaller dark/light switch labelled *Device*. The chosen tab is now always shown.

## 0.7.4 – 2026-11-28
- **Every function now goes through the add-on**, also those with a shortcut. The add-on clicks the command in Alibre's ribbon of the active window (found by command, name or shortcut); a command that is not in the ribbon (e.g. an Alibre Script shortcut) is typed as its shortcut.
- **Feedback instead of silence:** if the command is greyed out or not in this window (e.g. Boss Extrude in an assembly sketch), you hear a beep and see a short message at the mouse pointer.
- Works from the radial menu too: the invisible keys are caught even when 3Dconnexion's menu window has the focus.
- In the function lists, commands for the selected tab (and general ones) come first; commands for another window type are marked, e.g. *(Part)*. They can still be chosen.

## 0.7.3 – 2026-11-28
- The selected context tab (Part, Assembly, Sketch, Drawing) stays highlighted when you choose a device.

## 0.7.2 – 2026-11-28
- Alibre commands chosen before 0.7.1 are also run by the add-on (no false warning on Save when the dialog is opened from another window type).
- About shows the version with three digits.

## 0.7.1 – 2026-11-28
- **Every Alibre ribbon command works on a key or in the radial menu, also without a keyboard shortcut.** The add-on gives such a command an invisible key (F13–F24 with Ctrl/Shift/Alt), catches it inside Alibre and clicks the ribbon button. The list shows *run by add-on*; Save only warns about functions that are not in Alibre's ribbon.
- Radial menu: empty slots are shown as *(empty)*.

## 0.7.0 – 2026-11-28
- **Radial menu** for every SpaceMouse, also the SpaceMouse Compact (idea: HaroldL). A third choice next to SpaceMouse Enterprise and Keyboard Pro: 8 functions around the cursor, opened with the MENU button (left button on the Compact, MENU key on Pro/Enterprise). One menu per context (Part, Assembly, Sketch, Drawing), and it follows the active Alibre window like the keys.
- If no slot is filled, the MENU button keeps 3DxWare's own menu.

## 0.6.3 – 2026-11-27
- **About…** in the menu, the same in all KOCH Engineering add-ons.
- The version is shown with three digits.

## 0.6.2 – 2026-11-25
- Credits always visible at the top right of the dialog: **KOCH Engineering** (opens About) · Henrik J. Koch (LinkedIn) · Denmark © 2026.
- Faster dialog: Alibre's command list is read once in the background after start-up and reused; icons are only rendered when they are shown.

## 0.6.1 – 2026-11-25
- About window: click "KOCH Engineering" in the dialog (KOCH Engineering · Henrik J. Koch · Denmark · © 2026, links to LinkedIn and GitHub).
- Windows installer (`KOCH-3Dconnexion-Keys-Setup-0.6.1.exe`) with uninstall from Windows Settings.

## 0.6.0 – 2026-11-25
- Keyboard Pro support: 12 macro keys (3 groups of 4), with their own layout per context and icons in the preview.
- Device selector that shows only the 3Dconnexion devices 3DxWare knows about.
- "KOCH Engineering" link in the dialog (Henrik J. Koch, Denmark).
- Renamed to **KOCH 3Dconnexion Keys** (menu Add-on → 3Dconnexion), since it now covers both mouse and keyboard.
- New add-on icon (SpaceMouse Enterprise seen from above).
- `uninstall.cmd`.

## 0.5.0
- Searchable key picker: type part of a name, use the arrow keys and Enter.
- Functions already used on another key are highlighted.

## 0.4.x
- Reads all ~1,300 Alibre ribbon commands with their real (SVG) icons and shortcuts.
- Clicking a key in the preview opens its picker; red key numbers; two-column layout.
- Hot reload: "Reload add-on" loads a new core without restarting Alibre.

## 0.3.0
- First working version: writes names, icons and shortcut macros to the 3DxWare Alibre profile, and switches automatically between Part, Assembly, Sketch and Drawing.
