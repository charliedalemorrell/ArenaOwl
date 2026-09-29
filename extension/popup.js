// The toolbar popup: pick a monitor, how many screens, and a platform for each, then launch.
import { PLATFORMS } from "./platforms.js";
import { layout } from "./layout.js";

const MAX_SCREENS = 4;
const SETTINGS_KEY = "settings";

const monitorSelect = document.getElementById("monitor");
const countSelect = document.getElementById("count");
const slotsDiv = document.getElementById("slots");
const preview = document.getElementById("preview");

let displays = [];
const slotSelects = [];

async function init() {
  const saved = (await chrome.storage.local.get(SETTINGS_KEY))[SETTINGS_KEY] ?? {};

  // Monitors, numbered left to right.
  displays = (await chrome.system.display.getInfo()).sort(
    (a, b) => a.bounds.left - b.bounds.left || a.bounds.top - b.bounds.top
  );
  displays.forEach((d, i) => {
    const option = document.createElement("option");
    option.value = d.id;
    option.textContent = `${i + 1}: ${d.bounds.width}x${d.bounds.height}${d.isPrimary ? " (primary)" : ""}`;
    monitorSelect.appendChild(option);
  });
  const wanted = displays.find((d) => d.id === saved.monitor) ?? displays.find((d) => d.isPrimary) ?? displays[0];
  monitorSelect.value = wanted.id;

  // One platform dropdown per possible screen.
  for (let i = 0; i < MAX_SCREENS; i++) {
    const label = document.createElement("label");
    label.append(`Screen ${i + 1}`);
    const select = document.createElement("select");
    PLATFORMS.forEach((p) => {
      const option = document.createElement("option");
      option.value = p.key;
      option.textContent = p.name;
      select.appendChild(option);
    });
    select.value = saved.slots?.[i] ?? PLATFORMS[i % PLATFORMS.length].key;
    select.addEventListener("change", update);
    label.appendChild(select);
    slotsDiv.appendChild(label);
    slotSelects.push(select);
  }

  countSelect.value = String(saved.count ?? MAX_SCREENS);
  monitorSelect.addEventListener("change", update);
  countSelect.addEventListener("change", update);
  update();
}

const currentDisplay = () => displays.find((d) => d.id === monitorSelect.value);
const currentCount = () => Number(countSelect.value);
const currentPlatforms = () => slotSelects.slice(0, currentCount()).map((s) => PLATFORMS.find((p) => p.key === s.value));

// Refreshes which dropdowns are enabled and redraws the layout preview.
function update() {
  slotSelects.forEach((s, i) => (s.disabled = i >= currentCount()));

  const area = currentDisplay().workArea;
  preview.style.aspectRatio = `${area.width} / ${area.height}`;
  preview.replaceChildren();

  const platforms = currentPlatforms();
  layout({ left: 0, top: 0, width: 1000, height: 1000 }, platforms.length).forEach((tile, i) => {
    const box = document.createElement("div");
    box.className = "tile";
    box.style.cssText = `left:${tile.left / 10}%; top:${tile.top / 10}%; width:${tile.width / 10}%; height:${tile.height / 10}%;`;
    const inner = document.createElement("div");
    inner.textContent = `${i + 1}\n${platforms[i].name}`;
    inner.style.whiteSpace = "pre-line";
    box.appendChild(inner);
    preview.appendChild(box);
  });
}

document.getElementById("launch").addEventListener("click", async () => {
  await chrome.storage.local.set({
    [SETTINGS_KEY]: {
      monitor: monitorSelect.value,
      count: currentCount(),
      slots: slotSelects.map((s) => s.value),
    },
  });

  const wa = currentDisplay().workArea;
  await chrome.runtime.sendMessage({
    type: "launch",
    area: { left: wa.left, top: wa.top, width: wa.width, height: wa.height },
    slots: currentPlatforms().map((p) => ({ name: p.name, url: p.url })),
  });
  window.close();
});

document.getElementById("closeAll").addEventListener("click", async () => {
  await chrome.runtime.sendMessage({ type: "closeAll" });
  window.close();
});

init();
