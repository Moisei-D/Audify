// Audify dashboard - vanilla JS + Chart.js.
// Flow: user drags/drops (or browses to) their Spotify export JSON files ->
// we POST them to /api/upload -> server parses & stores them in memory ->
// we fetch the dataset's real date range and default the dashboard to "All time"
// (since exported history is historical - "last 6 months" from today would
// usually show nothing) -> user can then narrow down via presets/slider/custom range.

// `upload` is the ID used in the HTML; fall back to a sensible selector if it's missing.
const uploadScreen = document.getElementById("upload") || document.querySelector(".upload-screen") || null;
const dashboard = document.getElementById("dashboard");
const dropzone = document.getElementById("dropzone");
const fileInput = document.getElementById("fileInput");
const uploadStatus = document.getElementById("uploadStatus");
const uploadDifferentBtn = document.getElementById("uploadDifferentBtn");

const presetButtons = Array.from(document.querySelectorAll(".preset-btn"));
const monthsSlider = document.getElementById("monthsSlider");
const monthsValue = document.getElementById("monthsValue");
const fromDateInput = document.getElementById("fromDate");
const toDateInput = document.getElementById("toDate");
const applyCustomRangeBtn = document.getElementById("applyCustomRange");
const datasetHint = document.getElementById("datasetHint");
const activeRangeLabel = document.getElementById("activeRangeLabel");
const loginBtn = document.getElementById("loginBtn");

const charts = {}; // name -> Chart.js instance, so we can update() instead of recreating

// The single source of truth for "what range are we currently viewing".
// mode: "alltime" | "months" | "custom"
let currentFilter = { mode: "alltime" };
let datasetRange = null; // { earliestUtc, latestUtc, totalTrackEvents }

// --- Upload / drag & drop ---------------------------------------------------

function setUploadStatus(message, kind) {
  uploadStatus.textContent = message;
  uploadStatus.className = `upload-status ${kind ?? ""}`.trim();
}

async function uploadFiles(fileList) {
  const jsonFiles = Array.from(fileList).filter(f => f.name.toLowerCase().endsWith(".json"));

  if (jsonFiles.length === 0) {
    setUploadStatus("Please drop .json files from your Spotify data export.", "error");
    return;
  }

  const formData = new FormData();
  jsonFiles.forEach(f => formData.append("files", f));

  setUploadStatus(`Uploading ${jsonFiles.length} file(s)...`);

  try {
    const response = await fetch("/api/upload", {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        Accept: "application/json"
      }
    });

    if (!response.ok) {
      let errorText;
      try {
        errorText = await response.text();
      } catch (e) {
        errorText = response.statusText || `Upload failed with status ${response.status}`;
      }
      console.error("Upload failed:", response.status, errorText);
      setUploadStatus(errorText || "Upload failed.", "error");
      return;
    }

    const result = await response.json();
    // Support both camelCase (client expectation) and PascalCase (server default) responses.
    const filesProcessed = result.filesProcessed ?? result.FilesProcessed ?? 0;
    const trackEventsLoaded = result.trackEventsLoaded ?? result.TrackEventsLoaded ?? result.TrackEventsLoaded ?? 0;

    setUploadStatus(
      `Loaded ${trackEventsLoaded.toLocaleString()} track plays from ${filesProcessed} file(s). Building your dashboard...`,
      "success"
    );

    await enterDashboard();
  } catch (err) {
    console.error("Upload request failed:", err);
    setUploadStatus(err?.message ?? "Something went wrong while uploading. Check the console for details.", "error");
  }
}

function showDashboard() {
  if (uploadScreen) uploadScreen.classList.add("hidden");
  if (dashboard) {
    dashboard.classList.remove("hidden");
    dashboard.scrollIntoView({ behavior: "smooth", block: "start" });
  }
}

function showUploadScreen() {
  if (dashboard) dashboard.classList.add("hidden");
  if (uploadScreen) uploadScreen.classList.remove("hidden");
  if (uploadStatus) setUploadStatus("");
}

dropzone.addEventListener("click", () => fileInput.click());

fileInput.addEventListener("change", () => {
  if (fileInput.files.length > 0) uploadFiles(fileInput.files);
});

["dragenter", "dragover"].forEach(evt =>
  dropzone.addEventListener(evt, e => {
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
    dropzone.classList.add("dragover");
  })
);

["dragleave", "drop"].forEach(evt =>
  dropzone.addEventListener(evt, e => {
    e.preventDefault();
    dropzone.classList.remove("dragover");
  })
);

dropzone.addEventListener("drop", e => {
  if (e.dataTransfer?.files?.length) {
    uploadFiles(e.dataTransfer.files);
  }
});

// Prevent the browser from navigating to the file when dropping outside the dropzone.
window.addEventListener("dragover", e => e.preventDefault());
window.addEventListener("drop", e => e.preventDefault());

uploadDifferentBtn.addEventListener("click", async () => {
  await fetch("/api/upload/clear", { method: "POST" });
  fileInput.value = "";
  datasetRange = null;
  showUploadScreen();
});

// --- Entering the dashboard: fetch dataset range, default to "All time" -----

async function enterDashboard() {
  showDashboard();

  try {
    const res = await fetch("/api/stats/data-range");
    if (res.ok) {
      const range = await res.json();
      datasetRange = range;
      const earliest = new Date(range.earliestUtc);
      const latest = new Date(range.latestUtc);

      datasetHint.textContent = `Your data covers ${formatDate(earliest)} - ${formatDate(latest)}`;
      fromDateInput.min = toInputDate(earliest);
      fromDateInput.max = toInputDate(latest);
      toDateInput.min = toInputDate(earliest);
      toDateInput.max = toInputDate(latest);
    }
  } catch (err) {
    console.warn("Could not load dataset range:", err);
  }

  setFilterMode("alltime");
}

async function checkExistingData() {
  try {
    const res = await fetch("/api/upload/status");
    const status = await res.json();
    if (status.hasData) {
      await enterDashboard();
    }
  } catch (err) {
    console.warn("Could not check upload status:", err);
  }
}

// --- Filter state -----------------------------------------------------------

function setFilterMode(mode, extra) {
  currentFilter = { mode, ...extra };

  presetButtons.forEach(btn => {
    const isMatch =
      (mode === "alltime" && btn.dataset.alltime) ||
      (mode === "months" && btn.dataset.months === String(extra?.months));
    btn.classList.toggle("active", !!isMatch);
  });

  if (mode === "months") {
    monthsSlider.value = extra.months;
    monthsValue.textContent = extra.months;
  }

  refreshDashboard();
}

presetButtons.forEach(btn => {
  btn.addEventListener("click", () => {
    if (btn.dataset.alltime) {
      setFilterMode("alltime");
    } else {
      setFilterMode("months", { months: Number(btn.dataset.months) });
    }
  });
});

monthsSlider.addEventListener("input", () => {
  monthsValue.textContent = monthsSlider.value;
});
monthsSlider.addEventListener("change", () => {
  setFilterMode("months", { months: Number(monthsSlider.value) });
});

applyCustomRangeBtn.addEventListener("click", () => {
  if (!fromDateInput.value || !toDateInput.value) return;
  setFilterMode("custom", { from: fromDateInput.value, to: toDateInput.value });
});

function buildRangeQuery() {
  if (currentFilter.mode === "custom") {
    return `from=${currentFilter.from}&to=${currentFilter.to}`;
  }
  if (currentFilter.mode === "months") {
    return `months=${currentFilter.months}`;
  }
  // "alltime": pass the dataset's own bounds as an explicit range.
  if (datasetRange) {
    return `from=${toInputDate(new Date(datasetRange.earliestUtc))}&to=${toInputDate(new Date(datasetRange.latestUtc))}`;
  }
  return "months=12";
}

// --- Formatting helpers ------------------------------------------------------

function toInputDate(date) {
  return date.toISOString().split("T")[0];
}

function formatDate(date) {
  return date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

const DAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

// --- Stats fetching & rendering ----------------------------------------------

async function getJson(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Request to ${url} failed with status ${response.status}`);
  }
  return response.json();
}

function upsertChart(canvasId, config) {
  if (charts[canvasId]) {
    charts[canvasId].data = config.data;
    charts[canvasId].update();
  } else {
    charts[canvasId] = new Chart(document.getElementById(canvasId), config);
  }
}

const axisColor = "#a3a3ad";
const gridColor = "#2c2c33";

// Accent palette mirrors css/site.css tokens (--purple, --pink, --teal, --blue, --amber, --green).
const PALETTE = {
  green: "#1db954",
  purple: "#b18cff",
  pink: "#ff6fb0",
  teal: "#33d6c0",
  blue: "#6ea8fe",
  amber: "#ffc857"
};
const DOUGHNUT_COLORS = [PALETTE.purple, PALETTE.pink, PALETTE.teal, PALETTE.blue, PALETTE.amber, PALETTE.green, "#535353"];

async function loadSummary() {
  const summary = await getJson(`/api/stats/summary?${buildRangeQuery()}`);
  document.getElementById("statMinutes").textContent = Math.round(summary.totalMinutesListened).toLocaleString();
  document.getElementById("statTracks").textContent = summary.totalTracksPlayed.toLocaleString();
  document.getElementById("statArtists").textContent = summary.uniqueArtists.toLocaleString();
  document.getElementById("statUniqueTracks").textContent = summary.uniqueTracks.toLocaleString();

  activeRangeLabel.textContent = `Showing ${formatDate(new Date(summary.rangeStartUtc))} - ${formatDate(new Date(summary.rangeEndUtc))}`;

  // Derived stat: average minutes listened per calendar day in the active range.
  const rangeStart = new Date(summary.rangeStartUtc);
  const rangeEnd = new Date(summary.rangeEndUtc);
  const dayCount = Math.max(1, Math.round((rangeEnd - rangeStart) / (1000 * 60 * 60 * 24)) + 1);
  const avgPerDay = summary.totalMinutesListened / dayCount;
  document.getElementById("statAvgPerDay").textContent = avgPerDay >= 1
    ? Math.round(avgPerDay).toLocaleString()
    : avgPerDay.toFixed(1);
}

async function loadHabits() {
  const habits = await getJson(`/api/stats/habits?${buildRangeQuery()}`);
  document.getElementById("statSkipRate").textContent = `${habits.skipRatePercent}%`;
  document.getElementById("statStreak").textContent = habits.longestStreakDays.toLocaleString();

  const mostActiveEl = document.getElementById("mostActiveDay");
  if (habits.mostActiveDayUtc) {
    const date = formatDate(new Date(habits.mostActiveDayUtc));
    mostActiveEl.innerHTML = `<span class="highlight">${date}</span> - ${Math.round(habits.mostActiveDayMinutes)} minutes listened`;
  } else {
    mostActiveEl.textContent = "No data in this range yet.";
  }
}

async function loadTopArtists() {
  const artists = await getJson(`/api/stats/top-artists?${buildRangeQuery()}&take=8`);
  upsertChart("topArtistsChart", {
    type: "bar",
    data: {
      labels: artists.map(a => a.artistName),
      datasets: [{ label: "Minutes listened", data: artists.map(a => a.minutesListened), backgroundColor: PALETTE.pink, borderRadius: 6 }]
    },
    options: {
      indexAxis: "y",
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: axisColor }, grid: { color: gridColor } },
        y: { ticks: { color: axisColor }, grid: { display: false } }
      }
    }
  });
}

async function loadTrend() {
  const trend = await getJson(`/api/stats/trend?${buildRangeQuery()}`);
  upsertChart("trendChart", {
    type: "line",
    data: {
      labels: trend.map(t => t.month),
      datasets: [{
        label: "Minutes listened", data: trend.map(t => t.minutesListened),
        borderColor: PALETTE.purple, backgroundColor: "rgba(177, 140, 255, 0.18)", fill: true, tension: 0.3
      }]
    },
    options: {
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: axisColor }, grid: { color: gridColor } },
        y: { ticks: { color: axisColor }, grid: { color: gridColor } }
      }
    }
  });
}

async function loadDayOfWeek() {
  const days = await getJson(`/api/stats/by-day-of-week?${buildRangeQuery()}`);
  upsertChart("dayOfWeekChart", {
    type: "bar",
    data: {
      labels: days.map(d => DAY_LABELS[d.dayIndex]),
      datasets: [{ label: "Minutes listened", data: days.map(d => d.minutesListened), backgroundColor: PALETTE.teal, borderRadius: 6 }]
    },
    options: {
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: axisColor }, grid: { display: false } },
        y: { ticks: { color: axisColor }, grid: { color: gridColor } }
      }
    }
  });
}

async function loadHourOfDay() {
  const hours = await getJson(`/api/stats/by-hour?${buildRangeQuery()}`);

  // Derived stat: the single hour (UTC) with the most listening.
  const peakHourEl = document.getElementById("statPeakHour");
  if (hours.length > 0) {
    const peak = hours.reduce((best, h) => (h.minutesListened > best.minutesListened ? h : best), hours[0]);
    peakHourEl.textContent = `${String(peak.hour).padStart(2, "0")}:00`;
  } else {
    peakHourEl.textContent = "-";
  }

  upsertChart("hourOfDayChart", {
    type: "bar",
    data: {
      labels: hours.map(h => `${h.hour}:00`),
      datasets: [{ label: "Minutes listened", data: hours.map(h => h.minutesListened), backgroundColor: PALETTE.blue, borderRadius: 4 }]
    },
    options: {
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: axisColor, maxRotation: 0, autoSkip: true, maxTicksLimit: 12 }, grid: { display: false } },
        y: { ticks: { color: axisColor }, grid: { color: gridColor } }
      }
    }
  });
}

async function loadPlatforms() {
  const platforms = await getJson(`/api/stats/platforms?${buildRangeQuery()}`);
  upsertChart("platformChart", {
    type: "doughnut",
    data: {
      labels: platforms.map(p => p.platform),
      datasets: [{ data: platforms.map(p => p.minutesListened), backgroundColor: DOUGHNUT_COLORS }]
    },
    options: { plugins: { legend: { position: "bottom", labels: { color: axisColor } } } }
  });
}

async function loadContentTypes() {
  const types = await getJson(`/api/stats/content-types?${buildRangeQuery()}`);
  upsertChart("contentTypeChart", {
    type: "doughnut",
    data: {
      labels: types.map(t => t.contentType),
      datasets: [{ data: types.map(t => t.minutesListened), backgroundColor: DOUGHNUT_COLORS }]
    },
    options: { plugins: { legend: { position: "bottom", labels: { color: axisColor } } } }
  });
}

async function loadTopTracks() {
  const tracks = await getJson(`/api/stats/top-tracks?${buildRangeQuery()}&take=8`);
  document.getElementById("topTracksList").innerHTML = tracks.map(t => `
    <li>${t.trackName}<br/><span class="secondary">${t.artistName} - ${Math.round(t.minutesListened)} min</span></li>
  `).join("") || "<li class=\"secondary\">No plays in this range.</li>";
}

async function loadTopAlbums() {
  const albums = await getJson(`/api/stats/top-albums?${buildRangeQuery()}&take=8`);
  document.getElementById("topAlbumsList").innerHTML = albums.map(a => `
    <li>${a.albumName}<br/><span class="secondary">${a.artistName} - ${Math.round(a.minutesListened)} min</span></li>
  `).join("") || "<li class=\"secondary\">No plays in this range.</li>";
}

async function refreshDashboard() {
  try {
    await Promise.all([
      loadSummary(),
      loadHabits(),
      loadTopArtists(),
      loadTrend(),
      loadDayOfWeek(),
      loadHourOfDay(),
      loadPlatforms(),
      loadContentTypes(),
      loadTopTracks(),
      loadTopAlbums()
    ]);
  } catch (err) {
    console.error("Failed to refresh dashboard:", err);
  }
}

// Placeholder for the future "log in with Spotify" OAuth flow (see README roadmap).
loginBtn.addEventListener("click", () => {
  alert("Spotify login isn't wired up yet - for now, drag & drop your exported data instead.");
});

// Initial load: see if there's already data in memory, otherwise show the dropzone.
// Do not auto-load any example or seeded data on first load. The app
// should remain on the upload screen until the user provides their
// own Spotify JSON files via drag & drop or the file picker.
// If you need to re-enable checking server-side uploads, call
// `checkExistingData()` from a debug console.