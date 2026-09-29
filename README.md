<p align="center">
  <img src="extension/icons/icon128.png" width="96" alt="ArenaOwl logo">
</p>

<h1 align="center">ArenaOwl</h1>

<p align="center">Watch up to 4 games from different streaming services on one screen, at the same time.</p>

## What it does

On a big game day, your games are scattered across ESPN, Prime, Fox One, Paramount+, and
whatever else — and a TV or Xbox app can only show one of them full screen at a time. ArenaOwl
is a Chrome extension that opens several of those sites as separate windows and tiles them
across one monitor, so you can watch them all at once.

- Pick a monitor, how many windows (1–4), and which streaming service goes in each. The same
  service can be used more than once — 3 ESPN windows and 1 Prime, say.
- Every window plays sound by default, so mute is a click away: a small on-page control strip
  lets you pick which one window has sound (checking one unchecks the rest), mute everything, or
  close everything.
- It runs inside your normal Chrome profile, so every site is already signed in — no separate
  logins to manage.
- Going fullscreen on a video keeps it inside its own window instead of taking over the whole
  monitor, so the other games stay visible.
- The control strip can be dragged out of the way of a player's own controls, and it remembers
  where you put it for each site.

Supported out of the box: Prime Video, ESPN, Fox One, Paramount+, Netflix, Peacock, YouTube TV,
Disney+, Hulu, Tubi, and HBO Max. Edit `extension/platforms.js` to add or change services (see
[CLAUDE.md](CLAUDE.md) for what else needs updating alongside it).

## Install it (Developer mode)

ArenaOwl isn't published to the Chrome Web Store, so it's loaded as an unpacked extension —
this only takes a minute and is a normal, supported way to run an extension you trust.

1. **Download this repository** — click the green **Code** button on GitHub, then **Download
   ZIP**, and unzip it somewhere you'll keep it (don't delete the folder afterward; Chrome loads
   the extension from it every time it starts). If you're comfortable with git, `git clone` works
   too.
2. Open Chrome and go to `chrome://extensions` (paste that into the address bar).
3. Turn on **Developer mode**, the toggle in the top-right corner of that page.
4. Click **Load unpacked**, and select the `extension` folder inside the ArenaOwl folder you
   unzipped (not the repo's top-level folder — the one named `extension`).
5. ArenaOwl should now appear in your extensions list and in the toolbar. Click the puzzle-piece
   icon in Chrome's toolbar and pin it if you'd like it always visible.

It also works in Microsoft Edge, the same way, via `edge://extensions`.

Because it's unpacked rather than from the Web Store, Chrome may occasionally show a "disable
developer mode extensions" reminder — you can dismiss it, or click **Keep it on** if offered.

## Use it

1. Click the ArenaOwl icon in the toolbar.
2. Choose your monitor, how many windows you want (1–4), and a streaming service for each.
   The preview shows how they'll be arranged.
3. Click **Launch**. The windows open on your chosen monitor, already signed in.
4. Each window has a small strip in the corner (drag it by the `⋮⋮` grip if a player's own
   controls are in the way):
   - Numbered buttons choose which window has sound. A green frame marks the active one.
   - 🔇 mutes every window.
   - ✕ Close all closes every ArenaOwl window at once.

## Updating it

After pulling or downloading a newer version of this repo, go back to `chrome://extensions`
and click the reload icon (a circular arrow) on the ArenaOwl card. If the update changed which
sites it works with, Chrome may ask you to re-approve its permissions.

## More detail

[CLAUDE.md](CLAUDE.md) has the technical notes: how the extension is put together, and details
on the earlier Windows app (`ArenaOwl.csproj` and the other `.cs` files) that's kept in this repo
as a backup.
