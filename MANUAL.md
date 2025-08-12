## AppSwitcher Manual

This manual covers installing/running AppSwitcher, configuring layout and slots, per-slot actions, hotkeys, screenshots, multi‑instance targeting, persistence, packaging, and troubleshooting.

### What is AppSwitcher?
AppSwitcher is a small, always‑on‑top toolbar that lets you assign frequently used apps or specific windows to slots. Clicking a slot brings the assigned app/window to the foreground (or launches it). You can customize labels, icons, colors, layout, and quickly assign from currently running windows.

Works on Windows 10/11 x64. Built with WPF (.NET 8).

---

## Install and Run

### Prerequisites
- .NET 8 Runtime to run; .NET 8 SDK to build from source
- Windows x64

### Run from source (dev)
From the repo root:

```bat
run.bat
```

This will build (if needed) and launch `AppSwitcher.exe` from `apps/AppSwitcher/bin/.../publish/`.

Alternatively, from the app folder:

```bat
apps\AppSwitcher\run.bat
```

### Build only
```bat
apps\AppSwitcher\build.bat
```
Outputs a single‑file, self‑contained `AppSwitcher.exe` under `apps/AppSwitcher/bin/x64/Release/net8.0-windows/win-x64/publish/`.

### Install via Inno Setup installer (local)
Build the installer then run it:

```bat
package.bat
```

The script compiles `installer/appswitcher.iss` with Inno Setup 6 and writes an installer like `installer/Output/AppSwitcher-<version>-Setup.exe`.

### Chocolatey package (local build)
To create a local Chocolatey package (not published, for local testing):

```bat
packaging\package.bat
```

This packs `packaging/appswitcher.nuspec` and includes the published `AppSwitcher.exe` in `tools/` for shim execution (`appswitcher`). Requires the `choco` CLI.

---

## Basics and UI

- The window is borderless and semi‑transparent with rounded corners.
- Drag the top bar to move it. Resize via the window grip.
- Toggle always‑on‑top from the menu (☰).
- A system tray icon is available with quick assignment actions and Exit.

---

## Layout Presets

Open the menu (☰) → Layout:
- 1 × 4
- 2 × 2
- 2 × 4
- 1 × 6
- Configure rows/columns… (custom up to 8 × 8; visible slots capped at 64)

Icon sizes auto‑adjust to fit the grid. Hidden slots are preserved when shrinking layout.

---

## Assigning Apps/Windows to Slots

You can assign a slot in multiple ways:

- Drag‑and‑drop: Drop a `.exe` or `.lnk` file onto a slot.
- Click an empty slot: Assigns the last external or current foreground window.
- Tray menu: Right‑click the tray icon and choose “Add active window to Slot N”.
- From the menu (☰):
  - “Assign from running (pick window)…” to pick from a list of top‑level windows.
  - “Assign this slot from running… (Slot N)” for a specific slot.

When assigning from a running window, AppSwitcher records:
- Executable path (or shortcut target)
- Window title
- Window handle (for the current session) and approximate bounds (for heuristics)
- For Chrome: best‑effort current tab URL and favicon

Tip: If you have a custom image in the clipboard when assigning, it will be used as the slot icon.

---

## Per‑Slot Actions

- Left‑click:
  - If the assigned app/window is running, brings the best‑matching window to the front.
  - Otherwise, launches the target executable with stored arguments/working directory (if any).
- Middle‑click: Clears the slot immediately.
- Double‑click: Captures a screenshot of the assigned app’s window group to the clipboard.
- Right‑click: Slot context menu with:
  - Rename: Set a custom label (overrides auto‑label).
  - Clear slot.
  - Change icon…: Pick a custom image file.
  - Change background color…

Notes on window activation:
- The app prioritizes a saved window handle (if still valid), then title matches, then size/position heuristics, and finally the process’s main window.

---

## Hotkeys

- Global hotkeys: Ctrl + Alt + 1..9
  - Action: Assign the current foreground window to Slot 1..9 (respecting the current number of visible slots).
  - These hotkeys do not switch; they assign. To switch/activate, click the slot.
- Hotkeys rebind automatically when you change the number of visible slots (up to 9).

If a hotkey is already in use by another app, registration may silently fail for that number.

---

## Screenshots

- Double‑click a populated slot to capture a screenshot of the target window group.
- The capture is copied to the clipboard; paste it into an editor, chat, email, etc.
- “Window group” includes owner and popup/tool windows from the same process when possible.

---

## Multi‑Instance Targeting

When multiple instances/windows exist for the same app, AppSwitcher aims to bring back the specific one originally assigned by:
- Saving the window handle for the current session (if valid)
- Matching by last known window title (exact, then contains)
- Falling back to a best match by window bounds
- As a last resort, the process’s main window

For browsers (Chrome), the app attempts to read the active tab’s URL and fetch a favicon for the slot.

If the original window no longer exists or the title changes significantly, reassign the slot from the running window list or re‑click an empty slot to auto‑assign from the current foreground.

---

## Persistence

Configuration is saved to:
`%APPDATA%\AppSwitcher\config.json`

What’s stored:
- Toolbar position, size, always‑on‑top, layout (rows/columns)
- Per‑slot: target path, arguments, working directory, AppUserModelID (if any), process ID, window title, window handle (session), last bounds, custom label, custom icon path, background color, website URL

Downloaded/temporary icons (e.g., favicons, clipboard captures saved as files) are stored alongside the config in `%APPDATA%\AppSwitcher\` and referenced by path.

Resetting configuration: exit the app, then delete `%APPDATA%\AppSwitcher\config.json` to start fresh.

---

## Packaging

### Inno Setup installer
- Script: `installer/appswitcher.iss`
- Build: `package.bat` from repo root (requires Inno Setup 6; `ISCC.exe` must be installed)
- Output: `installer/Output/AppSwitcher-<version>-Setup.exe`

### Chocolatey package
- Spec: `packaging/appswitcher.nuspec`
- Build: `packaging/package.bat` (requires `choco` CLI)
- The package includes `tools/AppSwitcher.exe` and relies on Chocolatey’s shim to expose `appswitcher` on PATH.

---

## Troubleshooting

- App doesn’t come to foreground
  - Some windows may block programmatic activation; try clicking the slot again or ensure the target window isn’t minimized/hidden.
  - If you run AppSwitcher elevated and the target app is not (or vice versa), Windows focus rules can interfere.

- Hotkeys not working
  - Another app may already use Ctrl+Alt+<number>.
  - Only up to 9 hotkeys are registered. If you reduce visible slots, hotkeys above the new count are unregistered.

- Drag‑and‑drop doesn’t assign
  - Ensure you’re dropping a `.exe` or `.lnk`. For `.lnk`, the target is resolved automatically.

- Chrome favicon/URL not shown
  - Favicon retrieval is best‑effort (Google favicon API or `/favicon.ico`). Some sites block it or require internet access.
  - The URL is read via UI Automation when possible and may not work for all locales/versions.

- Custom icon not updating
  - Reopen the menu or resize the window to force a refresh. Verify the image path is still valid.

- Reset everything
  - Exit the app and delete `%APPDATA%\AppSwitcher\config.json`.

---

## Shortcuts Reference

- Global: Ctrl + Alt + 1..9 → Assign active window to Slot 1..9
- Slot: Left‑click → Activate/launch
- Slot: Middle‑click → Clear slot
- Slot: Double‑click → Copy screenshot to clipboard
- Window: Drag top bar to move; resize with the grip

---

## Notes and Limits

- Visible slots are capped at 64. Hotkeys are available for the first 9 slots.
- Window handles are valid only for the current Windows session.
- Always‑on‑top can also be enforced at the OS level by other apps; AppSwitcher provides a toggle within the app menu.


