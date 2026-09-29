// Runs inside the page itself (MAIN world) on the streaming sites.
//
// Real fullscreen makes Chrome cover the whole monitor, which would hide the other three
// windows. In ArenaOwl windows this replaces it with "fullscreen within the window": the
// player element is stretched over the window's own viewport instead. It only does that once
// fullscreen-bridge.js has confirmed this is an ArenaOwl window; everywhere else it hands the
// call straight to the browser's real fullscreen.
(() => {
  if (window.__arenaOwlFullscreen) return;
  window.__arenaOwlFullscreen = true;

  let enabled = false;
  window.addEventListener("arenaowl-enable", (e) => {
    enabled = Boolean(e.detail);
    if (!enabled && fsElement) exit();
  });

  const nativeRequest = Element.prototype.requestFullscreen;
  const nativeWebkitRequest = Element.prototype.webkitRequestFullscreen;
  const nativeExit = Document.prototype.exitFullscreen;
  const nativeWebkitExit = Document.prototype.webkitExitFullscreen;
  const nativeElementGetter = Object.getOwnPropertyDescriptor(Document.prototype, "fullscreenElement")?.get;

  // The element we've stretched, and the inline styles to put back afterwards.
  let fsElement = null;
  let savedStyles = [];

  const ELEMENT_STYLES = {
    position: "fixed",
    top: "0",
    left: "0",
    width: "100vw",
    height: "100vh",
    "max-width": "none",
    "max-height": "none",
    margin: "0",
    transform: "none",
    background: "#000",
    "z-index": "2147483646",
  };

  // Ancestors with a transform, filter or containment would trap a fixed-position child inside
  // themselves instead of letting it cover the viewport, so switch those off while stretched.
  const ANCESTOR_STYLES = {
    transform: "none",
    filter: "none",
    "backdrop-filter": "none",
    contain: "none",
    perspective: "none",
    "will-change": "auto",
    overflow: "visible",
  };

  function applyStyles(node, styles) {
    for (const [prop, value] of Object.entries(styles)) node.style.setProperty(prop, value, "important");
  }

  function announce(target) {
    for (const name of ["fullscreenchange", "webkitfullscreenchange"]) {
      target.dispatchEvent(new Event(name, { bubbles: true }));
    }
    // Players lay themselves out on resize, so nudge them.
    window.dispatchEvent(new Event("resize"));
  }

  function enter(el) {
    if (fsElement) exit();
    fsElement = el;
    savedStyles = [];

    for (let node = el; node; node = node.parentElement) {
      savedStyles.push([node, node.getAttribute("style")]);
      if (node === el) applyStyles(node, ELEMENT_STYLES);
      else if (node === document.documentElement) applyStyles(node, { ...ANCESTOR_STYLES, overflow: "hidden" });
      else applyStyles(node, ANCESTOR_STYLES);
    }
    announce(el);
  }

  function exit() {
    const el = fsElement;
    if (!el) return;
    for (const [node, style] of savedStyles) {
      if (style === null) node.removeAttribute("style");
      else node.setAttribute("style", style);
    }
    fsElement = null;
    savedStyles = [];
    announce(el);
  }

  Element.prototype.requestFullscreen = function (options) {
    if (!enabled) return nativeRequest.call(this, options);
    enter(this);
    return Promise.resolve();
  };

  if (nativeWebkitRequest) {
    Element.prototype.webkitRequestFullscreen = function (options) {
      if (!enabled) return nativeWebkitRequest.call(this, options);
      enter(this);
    };
  }

  Document.prototype.exitFullscreen = function () {
    if (!fsElement) return nativeExit.call(this);
    exit();
    return Promise.resolve();
  };

  if (nativeWebkitExit) {
    Document.prototype.webkitExitFullscreen = function () {
      if (!fsElement) return nativeWebkitExit.call(this);
      exit();
    };
  }

  // Sites check these to decide which icon to show and what the button does.
  for (const name of ["fullscreenElement", "webkitFullscreenElement"]) {
    Object.defineProperty(Document.prototype, name, {
      configurable: true,
      get() {
        return fsElement ?? (nativeElementGetter ? nativeElementGetter.call(this) : null);
      },
    });
  }

  // Escape leaves fullscreen, as it does in the real thing.
  window.addEventListener(
    "keydown",
    (e) => {
      if (fsElement && e.key === "Escape") {
        e.stopPropagation();
        exit();
      }
    },
    true
  );
})();
