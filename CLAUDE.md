# ArenaOwl

## Goal
Watch up to 4 football games from different streaming services (Prime, ESPN, Fox One, Paramount+/CBS, Netflix) on one screen at the same time, without flipping between apps.

## Why it's built this way
- Xbox and TV streaming apps can only show one app full screen, so there's no in-app way to combine Netflix + Prime + ESPN + Fox.
- Embedding the sites in iframes doesn't work (the sites block framing).
- A WinForms/WPF app using WebView2 was ruled out: WebView2 supports PlayReady DRM but not Widevine, so some services won't play.
- The solution: launch real Microsoft Edge (supports both Widevine and PlayReady) in 4 separate app-mode windows, one per quadrant.

## How it works
- .NET 9 WinForms app, Windows only. Files: `Program.cs` (entry), `MainForm.cs` (the UI), `Launcher.cs` (layout + launching), `Platforms.cs` (service list), `Settings.cs` (remembered choices).
- The UI lets the user pick the monitor (numbered left to right), the browser (Edge or Chrome), the number of screens (1-4), and a platform for each screen. The same platform can be picked more than once (e.g. 3 ESPN + 1 Prime). A live preview shows the layout.
- Layouts: 1 = full, 2 = side by side, 3 = one big on the left + two stacked on the right, 4 = 2x2 grid. All use the monitor's work area (excludes taskbar).
- Launches `msedge` (or Chrome) with `--app=URL --user-data-dir=<per-window profile>` for each screen. Separate profiles = separate browser processes, so each window can be positioned and each keeps its own login.
- Profiles live in %LOCALAPPDATA%\ArenaOwl\<Browser>\<Name>. A repeated platform gets its own profile (`ESPN`, `ESPN-2`, ...), so each copy needs its own login once.
- After launch, grabs each process's MainWindowHandle and positions it with SetWindowPos (twice, in case the browser resizes after crossing to a monitor with different scaling).
- After launch, a small always-on-top control strip (`ControlOverlay.cs`) sits at the center of the tiles: one toggle per window picks which one has sound (checking one unchecks the others; a green frame is drawn around it), plus a Close all button.
- Sound switching (`AudioRouter.cs`) uses Windows per-app audio sessions via NAudio. Browser audio comes from a child process of the browser we started, so each session is traced up its parent chain to find its window. Verified with two Edge windows playing tones. It re-applies every 1.5s because sessions only exist once a page plays audio.
- Edge/Chrome draw their own title bar, so it can't be hidden. Windows are enlarged by the invisible DWM border so tiles have no gaps.
- Logins (`ProfileSeeder.cs`): the "Copy my browser logins" option seeds each brand-new ArenaOwl profile with the cookies and local storage (plus Local State, which holds the decryption key) from the user's normal browser profile (the last-used one). Only done once per profile, before any window opens. The browser locks its cookie file while running, so the user must close Chrome/Edge first; the app stops with a message if it can't copy. Not yet verified that Chrome (app-bound encryption) accepts the copied cookies; test this. Rejected alternative: using the real profile directly, which would put all windows in one process and break per-window sound switching.
- Last choices are saved to %LOCALAPPDATA%\ArenaOwl\settings.json.
- The service list is `Platform.All` in Platforms.cs.

## Chrome extension version (`extension/`)
Runs inside the user's real Chrome profile, so every site is already signed in (the C# app couldn't get this: Chrome discards cookies copied into a new profile). Plain JS, Manifest V3, no build step.
- Install: chrome://extensions -> turn on Developer mode -> Load unpacked -> pick the `extension` folder. Works in Edge the same way (edge://extensions).
- `popup.html/js/css`: toolbar popup with monitor, screen count, platform per screen, layout preview, Launch and Close all.
- `background.js`: creates the windows (`chrome.windows.create`, type popup) tiled by `layout.js`, mutes all tabs but the active one (`chrome.tabs.update muted`), closes all. State is in `chrome.storage.session` because the service worker gets stopped.
- `overlay.js`: injected into each window; a strip with numbered sound buttons, Mute all and Close all, and a green frame when that window has sound. The strip is draggable by its grip (double-click resets to top-right); its position is saved per site in chrome.storage.local because players put controls in different places. Extensions can't make an always-on-top window, hence the in-page strip.
- Fullscreen: real fullscreen would cover the whole monitor, so `fullscreen-main.js` (page world, document_start) overrides `requestFullscreen`/`exitFullscreen` and `document.fullscreenElement` to stretch the player over its own window instead (best-effort, per-site behavior varies; players inside cross-origin iframes only fill the iframe). It only activates once `fullscreen-bridge.js` confirms via the background script that the tab is an ArenaOwl window. Sites are listed in the manifest `content_scripts` matches; add new services there too.
- `platforms.js`: the service list. Untested so far; the C# app in the repo root is kept as a backup.

## Known caveats
- Every window plays audio; user mutes all but one in the players themselves.
- Some GPUs limit the number of simultaneous protected (DRM) streams; a window may go black.
- If a profile's browser window is already open, the new launch hands off and the window can't be positioned; close all the windows and relaunch.

## Ideas not yet built
- Per-window audio switching.

## About the user
Works in C# but isn't an expert. Prefers full code files over snippets (unless it's a one-line change).

