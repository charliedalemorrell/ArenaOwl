// Injected into each ArenaOwl window. Shows a small control strip (one numbered button per
// window to choose which one has sound, Mute all, and Close all) and draws a green frame around
// the page when this window is the one with sound.
//
// The strip can be dragged by its grip (double-click the grip to put it back in the top-right
// corner). Each site remembers its own spot, since players put their controls in different places.
(() => {
  // Already on the page (e.g. injected again after a state change): just redraw.
  if (window.__arenaOwlRefresh) {
    window.__arenaOwlRefresh();
    return;
  }

  const ON = "#28a046";
  const MARGIN = 8;
  const POSITION_KEY = `arenaowl-strip:${location.hostname}`;

  const host = document.createElement("div");
  host.style.cssText = "all: initial; position: fixed; inset: 0; z-index: 2147483647; pointer-events: none;";
  const shadow = host.attachShadow({ mode: "closed" });

  shadow.innerHTML = `
    <style>
      .frame { position: fixed; inset: 0; border: 5px solid ${ON}; box-sizing: border-box;
               pointer-events: none; display: none; }
      .frame.on { display: block; }
      .strip { position: fixed; top: ${MARGIN}px; right: ${MARGIN}px; display: flex; gap: 4px; padding: 4px;
               background: rgba(30,30,30,0.85); border-radius: 8px; pointer-events: auto;
               opacity: 0.35; transition: opacity 0.15s; font: 700 13px "Segoe UI", sans-serif;
               user-select: none; }
      .strip:hover, .strip.dragging { opacity: 1; }
      .buttons { display: flex; gap: 4px; }
      .grip { width: 16px; display: flex; align-items: center; justify-content: center;
              color: #aaa; cursor: grab; touch-action: none; letter-spacing: -2px; }
      .strip.dragging .grip { cursor: grabbing; }
      button { all: unset; box-sizing: border-box; width: 28px; height: 28px; display: flex;
               align-items: center; justify-content: center; border-radius: 6px; cursor: pointer;
               color: #fff; background: #464646; }
      button:hover { filter: brightness(1.25); }
      button.active { background: ${ON}; }
      button.muted { background: #c8a010; }
      button.here { outline: 2px solid #fff; outline-offset: -2px; }
      button:disabled { opacity: 0.3; cursor: default; }
      button.close { width: auto; padding: 0 9px; background: #b42828; }
    </style>
    <div class="frame"></div>
    <div class="strip">
      <div class="grip" title="Drag to move (double-click to reset)">⋮⋮</div>
      <div class="buttons"></div>
    </div>`;

  const frame = shadow.querySelector(".frame");
  const strip = shadow.querySelector(".strip");
  const grip = shadow.querySelector(".grip");
  const buttons = shadow.querySelector(".buttons");

  // ---- position: saved as fractions (0-1) of the room available, so it still fits if the
  // window is resized ----
  let position = null; // { fx, fy } or null for the default corner

  function applyPosition() {
    if (!position) {
      strip.style.left = "";
      strip.style.right = `${MARGIN}px`;
      strip.style.top = `${MARGIN}px`;
      return;
    }
    const rect = strip.getBoundingClientRect();
    const roomX = Math.max(0, window.innerWidth - rect.width);
    const roomY = Math.max(0, window.innerHeight - rect.height);
    strip.style.right = "";
    strip.style.left = `${Math.round(position.fx * roomX)}px`;
    strip.style.top = `${Math.round(position.fy * roomY)}px`;
  }

  async function loadPosition() {
    try {
      const saved = (await chrome.storage.local.get(POSITION_KEY))[POSITION_KEY];
      if (saved) {
        position = saved;
        applyPosition();
      }
    } catch {
      // Storage unavailable: the default corner is fine.
    }
  }

  function savePosition() {
    try {
      if (position) chrome.storage.local.set({ [POSITION_KEY]: position });
      else chrome.storage.local.remove(POSITION_KEY);
    } catch {
      // Not worth interrupting anything over.
    }
  }

  let dragOffset = null;

  grip.addEventListener("pointerdown", (e) => {
    const rect = strip.getBoundingClientRect();
    dragOffset = { x: e.clientX - rect.left, y: e.clientY - rect.top };
    grip.setPointerCapture(e.pointerId);
    strip.classList.add("dragging");
    e.preventDefault();
  });

  grip.addEventListener("pointermove", (e) => {
    if (!dragOffset) return;
    const rect = strip.getBoundingClientRect();
    const roomX = Math.max(1, window.innerWidth - rect.width);
    const roomY = Math.max(1, window.innerHeight - rect.height);
    position = {
      fx: Math.min(1, Math.max(0, (e.clientX - dragOffset.x) / roomX)),
      fy: Math.min(1, Math.max(0, (e.clientY - dragOffset.y) / roomY)),
    };
    applyPosition();
  });

  const endDrag = () => {
    if (!dragOffset) return;
    dragOffset = null;
    strip.classList.remove("dragging");
    savePosition();
  };
  grip.addEventListener("pointerup", endDrag);
  grip.addEventListener("pointercancel", endDrag);

  grip.addEventListener("dblclick", () => {
    position = null;
    applyPosition();
    savePosition();
  });

  window.addEventListener("resize", applyPosition);

  // ---- content ----
  async function refresh() {
    let info;
    try {
      info = await chrome.runtime.sendMessage({ type: "hello" });
    } catch {
      return; // extension was reloaded; this page's script is orphaned
    }
    if (!info) {
      host.remove();
      return;
    }
    if (!host.isConnected) document.documentElement.appendChild(host);

    frame.classList.toggle("on", info.active === info.index);

    buttons.replaceChildren();
    info.windows.forEach((w, i) => {
      const b = document.createElement("button");
      b.textContent = String(i + 1);
      b.title = `Sound: ${w.label}`;
      b.disabled = w.closed;
      b.classList.toggle("active", i === info.active);
      b.classList.toggle("here", i === info.index);
      // Clicking the one that has sound turns it off (everything muted).
      b.onclick = () => chrome.runtime.sendMessage({ type: "setActive", index: i === info.active ? -1 : i });
      buttons.appendChild(b);
    });

    // Mutes every window. Lit up while everything is muted.
    const mute = document.createElement("button");
    mute.textContent = "🔇";
    mute.title = "Mute all";
    mute.classList.toggle("muted", info.active === -1);
    mute.onclick = () => chrome.runtime.sendMessage({ type: "setActive", index: -1 });
    buttons.appendChild(mute);

    const close = document.createElement("button");
    close.className = "close";
    close.textContent = "✕ Close all";
    close.onclick = () => chrome.runtime.sendMessage({ type: "closeAll" });
    buttons.appendChild(close);

    // The strip's width can change with the number of windows, so re-fit it.
    applyPosition();
  }

  window.__arenaOwlRefresh = refresh;
  loadPosition();
  refresh();
})();
