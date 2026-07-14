// Audify dashboard - vanilla JS + Chart.js.
// Flow: user drags/drops (or browses to) their Spotify export JSON files ->
// we POST them to /api/upload -> server parses & stores them in memory ->
// we fetch the dataset's real date range and default the dashboard to "All time"
// (since exported history is historical - "last 6 months" from today would
// usually show nothing) -> user can then narrow down via presets/slider/custom range.

const uploadScreen = document.getElementById("uploadScreen");
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
    const response = await fetch("/api/upload", { method: "POST", body: formData });

    if (!response.ok) {
      const errorText = await response.text();
      setUploadStatus(errorText || "Upload failed.", "error");
      return;
    }

    const result = await response.json();
    setUploadStatus(
      `Loaded ${result.trackEventsLoaded.toLocaleString()} track plays from ${result.filesProcessed} file(s). Building your dashboard...`,
      "success"
    );

    await enterDashboard();
  } catch (err) {
    console.error(err);
    setUploadStatus("Something went wrong while uploading. Check the console for details.", "error");
  }
}

function showDashboard() {
  uploadScreen.classList.add("hidden");
  dashboard.classList.remove("hidden");
}

function showUploadScreen() {
  dashboard.classList.add("hidden");
  uploadScreen.classList.remove("hidden");
  setUploadStatus("");
}

dropzone.addEventListener("click", () => fileInput.click());

fileInput.addEventListener("change", () => {
  if (fileInput.files.length > 0) uploadFiles(fileInput.files);
});

["dragenter", "dragover"].forEach(evt =>
  dropzone.addEventListener(evt, e => {
    e.preventDefault();
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

const axisColor = "#b3b3b3";
const gridColor = "#2a2a2a";

async function loadSummary() {
  const summary = await getJson(`/api/stats/summary?${buildRangeQuery()}`);
  document.getElementById("statMinutes").textContent = Math.round(summary.totalMinutesListened).toLocaleString();
  document.getElementById("statTracks").textContent = summary.totalTracksPlayed.toLocaleString();
  document.getElementById("statArtists").textContent = summary.uniqueArtists.toLocaleString();
  document.getElementById("statUniqueTracks").textContent = summary.uniqueTracks.toLocaleString();

  activeRangeLabel.textContent = `Showing ${formatDate(new Date(summary.rangeStartUtc))} - ${formatDate(new Date(summary.rangeEndUtc))}`;
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
      datasets: [{ label: "Minutes listened", data: artists.map(a => a.minutesListened), backgroundColor: "#1db954", borderRadius: 6 }]
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
        borderColor: "#1db954", backgroundColor: "rgba(29, 185, 84, 0.2)", fill: true, tension: 0.3
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
      datasets: [{ label: "Minutes listened", data: days.map(d => d.minutesListened), backgroundColor: "#1ed760", borderRadius: 6 }]
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
  upsertChart("hourOfDayChart", {
    type: "bar",
    data: {
      labels: hours.map(h => `${h.hour}:00`),
      datasets: [{ label: "Minutes listened", data: hours.map(h => h.minutesListened), backgroundColor: "#3fd68c", borderRadius: 4 }]
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

const DOUGHNUT_COLORS = ["#1db954", "#1ed760", "#3fd68c", "#7fe0b0", "#b3b3b3", "#535353", "#2a2a2a"];

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
checkExistingData();
