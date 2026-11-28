# Changelog

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
