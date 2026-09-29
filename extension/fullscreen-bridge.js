// Runs in the extension's isolated world at the start of each page load. It tells
// fullscreen-main.js (which runs in the page and can't talk to the extension) whether this tab is
// one of the ArenaOwl windows.
//
// The window is only recorded by the background script once Chrome has finished creating it,
// which can be after the page has started loading, so ask a few times.
(() => {
  let confirmed = false;

  async function check() {
    if (confirmed) return;
    try {
      const isArenaOwl = await chrome.runtime.sendMessage({ type: "isArenaOwl" });
      if (isArenaOwl) {
        confirmed = true;
        window.dispatchEvent(new CustomEvent("arenaowl-enable", { detail: true }));
      }
    } catch {
      // Extension was reloaded; nothing to do on this page.
    }
  }

  check();
  for (const delay of [300, 1000, 2500, 5000]) setTimeout(check, delay);
  document.addEventListener("DOMContentLoaded", check);
  window.addEventListener("load", check);
})();
