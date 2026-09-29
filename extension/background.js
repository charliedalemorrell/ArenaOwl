// ArenaOwl background service worker: opens the tiled windows, decides which one has sound,
// and closes them. State lives in chrome.storage.session because the worker can be stopped and
// restarted by Chrome at any time.
import { layout } from "./layout.js";

const STATE_KEY = "arenaowl";
const EMPTY = { windows: [], active: -1 };

async function getState() {
  const stored = await chrome.storage.session.get(STATE_KEY);
  return stored[STATE_KEY] ?? structuredClone(EMPTY);
}

async function setState(state) {
  await chrome.storage.session.set({ [STATE_KEY]: state });
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  handle(message, sender).then(sendResponse);
  return true; // keep the channel open for the async response
});

async function handle(message, sender) {
  switch (message.type) {
    case "launch":
      await launch(message.area, message.slots);
      return {};
    case "setActive":
      await setActive(message.index);
      return {};
    case "closeAll":
      await closeAll();
      return {};
    case "isArenaOwl": {
      // A page asking whether it's one of our windows (see fullscreen-bridge.js).
      const state = await getState();
      return state.windows.some((w) => w.tabId === sender.tab?.id && !w.closed);
    }
    case "hello": {
      // A page overlay asking what it should show.
      const state = await getState();
      const index = state.windows.findIndex((w) => w.tabId === sender.tab?.id);
      if (index < 0) return null;
      return {
        index,
        active: state.active,
        windows: state.windows.map((w) => ({ label: w.label, closed: w.closed })),
      };
    }
    default:
      return {};
  }
}

// Opens one window per slot, tiled inside area. slots is [{ name, url }].
async function launch(area, slots) {
  await closeAll();

  const tiles = layout(area, slots.length);
  const seen = {};
  const state = { windows: [], active: 0 };
  await setState(state);

  for (let i = 0; i < slots.length; i++) {
    const { name, url } = slots[i];
    seen[name] = (seen[name] ?? 0) + 1;
    const label = seen[name] === 1 ? name : `${name} #${seen[name]}`;
    const tile = tiles[i];

    const win = await chrome.windows.create({
      url,
      type: "popup",
      focused: false,
      left: tile.left,
      top: tile.top,
      width: tile.width,
      height: tile.height,
    });

    // Save as we go so page overlays can find themselves as soon as they load.
    state.windows.push({ windowId: win.id, tabId: win.tabs[0].id, label, tile, closed: false });
    await setState(state);
  }

  // Chrome can adjust a new window's size or position, so place each one again.
  for (const w of state.windows) {
    await chrome.windows
      .update(w.windowId, {
        left: w.tile.left,
        top: w.tile.top,
        width: w.tile.width,
        height: w.tile.height,
      })
      .catch(() => {});
  }

  await applyActive();
}

// Makes one window the one with sound (index), or mutes all of them (-1).
async function setActive(index) {
  const state = await getState();
  state.active = index;
  await setState(state);
  await applyActive();
}

// Mutes every tab except the active one, then tells each page overlay to redraw.
async function applyActive() {
  const state = await getState();
  await Promise.all(
    state.windows.map(async (w, i) => {
      if (w.closed) return;
      await chrome.tabs.update(w.tabId, { muted: i !== state.active }).catch(() => {});
      await inject(w.tabId);
    })
  );
}

// (Re)draws the on-page controls. Safe to call repeatedly; the script refreshes itself if it
// is already on the page.
async function inject(tabId) {
  await chrome.scripting.executeScript({ target: { tabId }, files: ["overlay.js"] }).catch(() => {});
}

async function closeAll() {
  const state = await getState();
  // Clear first, so the "window closed" handler below finds nothing to update.
  await setState(structuredClone(EMPTY));
  await Promise.all(
    state.windows.filter((w) => !w.closed).map((w) => chrome.windows.remove(w.windowId).catch(() => {}))
  );
}

// Pages reload and navigate: put the overlay and the mute state back each time.
chrome.tabs.onUpdated.addListener(async (tabId, info) => {
  if (info.status !== "complete") return;
  const state = await getState();
  const index = state.windows.findIndex((w) => w.tabId === tabId && !w.closed);
  if (index < 0) return;
  await chrome.tabs.update(tabId, { muted: index !== state.active }).catch(() => {});
  await inject(tabId);
});

// If someone closes a window by hand, remember it so the numbers on the others don't shift.
chrome.windows.onRemoved.addListener(async (windowId) => {
  const state = await getState();
  const w = state.windows.find((x) => x.windowId === windowId);
  if (!w) return;
  w.closed = true;
  if (state.windows.every((x) => x.closed)) {
    await setState(structuredClone(EMPTY));
    return;
  }
  if (state.windows[state.active]?.closed) state.active = -1;
  await setState(state);
  await applyActive();
});
