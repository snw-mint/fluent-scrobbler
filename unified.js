/*
 * BubbleFM
 * Copyright (c) 2026 SnowMint
 * Licensed under the GNU General Public License v3.0 (GPL-3.0)
 * You should have received a copy of the GNU General Public License along with this program.
 * If not, see <https://www.gnu.org/licenses/>.
 */

const SUN_ICON = `<svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColor"><path d="M451.5-771.5Q440-783 440-800v-80q0-17 11.5-28.5T480-920t28.5 11.5T520-880v80q0 17-11.5 28.5T480-760t-28.5-11.5M678-678q-11-11-11-27.5t11-28.5l56-57q12-12 28.5-12t28.5 12q11 11 11 28t-11 28l-57 57q-11 11-28 11t-28-11m122 238q-17 0-28.5-11.5T760-480t11.5-28.5T800-520h80q17 0 28.5 11.5T920-480t-11.5 28.5T880-440zM451.5-51.5Q440-63 440-80v-80q0-17 11.5-28.5T480-200t28.5 11.5T520-160v80q0 17-11.5 28.5T480-40t-28.5-11.5M226-678l-57-56q-12-12-12-29t12-28q11-11 28-11t28 11l57 57q11 11 11 28t-11 28q-12 11-28 11t-28-11m508 509-56-57q-11-12-11-28.5t11-27.5 27.5-11 28.5 11l57 56q12 11 11.5 28T791-169q-12 12-29 12t-28-12M80-440q-17 0-28.5-11.5T40-480t11.5-28.5T80-520h80q17 0 28.5 11.5T200-480t-11.5 28.5T160-440zm89 271q-11-11-11-28t11-28l57-57q11-11 27.5-11t28.5 11q12 12 12 28.5T282-225l-56 56q-12 12-29 12t-28-12m141-141q-70-70-70-170t70-170 170-70 170 70 70 170-70 170-170 70-170-70m283-57q47-47 47-113t-47-113-113-47-113 47-47 113 47 113 113 47 113-47M480-480"/></svg>`;
const MOON_ICON = `<svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColo"><path d="M484-80q-84 0-157.5-32t-128-86.5-86.5-128T80-484q0-128 72-232t193-146q22-8 41 5.5t18 36.5q-3 85 27 162t90 137 137 90 162 27q26-1 38.5 17.5T863-345q-44 120-147.5 192.5T484-80m0-80q88 0 163-44t118-121q-86-8-163-43.5T464-465t-97-138-43-163q-77 43-120.5 118.5T160-484q0 135 94.5 229.5T484-160m-20-305"/></svg>`;

function escapeHTML(str) {
  if (!str) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

document.addEventListener("DOMContentLoaded", () => {
  const themeToggle = document.getElementById("theme-toggle");
  const savedTheme = localStorage.getItem("theme");
  const systemPrefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  const currentTheme = savedTheme || (systemPrefersDark ? "dark" : "light");
  setTheme(currentTheme);

  if (themeToggle) {
    themeToggle.addEventListener("click", () => {
      const isDark = document.documentElement.getAttribute("data-theme") === "dark";
      setTheme(isDark ? "light" : "dark");
    });
  }

  function setTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("theme", theme);
    if (themeToggle) {
      themeToggle.innerHTML = theme === "dark" ? SUN_ICON : MOON_ICON;
    }
  }

  initFaqModal();

  const pb = document.getElementById("promoBanner");
  if (pb) {
    const ua = navigator.userAgent || "";
    const pl = navigator.userAgentData?.platform || navigator.platform || "";
    const isWin = /Win/i.test(pl) || /Windows/i.test(ua);
    const isMob = Boolean(navigator.userAgentData?.mobile) || /Mobi|Android|iPhone|iPad|iPod|Windows Phone/i.test(ua);
    if (isWin && !isMob) {
      pb.style.display = "flex";
      pb.addEventListener("click", () => {
        if (typeof umami !== "undefined") {
          umami.track("Fluent Scrobbler Outbound Click");
        }
      });
    }
  }

  const formUsername = document.getElementById("form-username");
  if (formUsername) {
    formUsername.addEventListener("submit", (e) => {
      e.preventDefault();
      const userInput = document.getElementById("userInput").value.trim();
      if (userInput) {
        sessionStorage.setItem("lastfm_user", userInput);
        if (typeof umami !== "undefined") {
          umami.track("Search Initiated", { type: "single" });
        }
        window.location.href = "result.html";
      }
    });
  }

  const formMatch = document.getElementById("form-match");
  if (formMatch) {
    formMatch.addEventListener("submit", (e) => {
      e.preventDefault();
      const userInput1 = document.getElementById("userInput1").value.trim();
      const userInput2 = document.getElementById("userInput2").value.trim();
      if (userInput1 && userInput2) {
        sessionStorage.setItem("lastfm_user1", userInput1);
        sessionStorage.setItem("lastfm_user2", userInput2);
        if (typeof umami !== "undefined") {
          umami.track("Search Initiated", { type: "match" });
        }
        window.location.href = "result.html";
      }
    });
  }

  if (window.location.pathname.endsWith("result.html") || window.location.pathname.includes("result.html")) {
    const user = sessionStorage.getItem("lastfm_user");
    if (!user) {
      window.location.href = "index.html";
    } else {
      let currentPeriod = "month";
      let periodOffset = 0;
      updatePeriodNavigation(currentPeriod, periodOffset);

      const toggleBtns = document.querySelectorAll(".time-toggle-btn");
      if (toggleBtns.length > 0) {
        toggleBtns.forEach((btn) => {
          btn.addEventListener("click", () => {
            if (btn.classList.contains("active")) return;

            toggleBtns.forEach((b) => b.classList.remove("active"));
            btn.classList.add("active");

            currentPeriod = btn.getAttribute("data-period");
            periodOffset = 0;
            updatePeriodNavigation(currentPeriod, periodOffset);
            resetToSkeletons();
            fetchLastfmAndDeezerData(user, currentPeriod, periodOffset);
          });
        });
      }

      const prevPeriodBtn = document.getElementById("prevPeriodBtn");
      const nextPeriodBtn = document.getElementById("nextPeriodBtn");
      let isNavigating = false;

      const handlePeriodChange = async (newOffset) => {
        if (isNavigating) return;
        isNavigating = true;

        if (prevPeriodBtn) prevPeriodBtn.disabled = true;
        if (nextPeriodBtn) nextPeriodBtn.disabled = true;

        periodOffset = newOffset;
        updatePeriodNavigation(currentPeriod, periodOffset);
        resetToSkeletons();

        try {
          await fetchLastfmAndDeezerData(user, currentPeriod, periodOffset);
        } finally {
          isNavigating = false;
          updatePeriodNavigation(currentPeriod, periodOffset);
        }
      };

      if (prevPeriodBtn) {
        prevPeriodBtn.addEventListener("click", () => {
          handlePeriodChange(periodOffset - 1);
        });
      }

      if (nextPeriodBtn) {
        nextPeriodBtn.addEventListener("click", () => {
          if (periodOffset >= 0) return;
          handlePeriodChange(periodOffset + 1);
        });
      }

      fetchLastfmAndDeezerData(user, currentPeriod, periodOffset);
    }
  }
});

const CACHE_TTL_MS = 24 * 60 * 60 * 1000;
const periodCache = {};
let currentActiveData = null;

function getLocalStorageCache(key) {
  try {
    const raw = localStorage.getItem("bubblefm_cache_" + key);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (Date.now() - parsed.timestamp < CACHE_TTL_MS) {
      return parsed.data;
    } else {
      localStorage.removeItem("bubblefm_cache_" + key);
    }
  } catch (e) {}
  return null;
}

function setLocalStorageCache(key, data) {
  try {
    localStorage.setItem(
      "bubblefm_cache_" + key,
      JSON.stringify({
        timestamp: Date.now(),
        data: data,
      })
    );
  } catch (e) {}
}

function updatePeriodNavigation(period, offset) {
  const periodDisplayText = document.getElementById("periodDisplayText");
  const prevPeriodBtn = document.getElementById("prevPeriodBtn");
  const nextPeriodBtn = document.getElementById("nextPeriodBtn");

  if (nextPeriodBtn) {
    nextPeriodBtn.disabled = offset >= 0;
  }
  if (prevPeriodBtn) {
    prevPeriodBtn.disabled = false;
  }

  if (!periodDisplayText) return;

  const now = new Date();
  if (period === "month") {
    const targetMonthStart = new Date(now.getFullYear(), now.getMonth() + offset, 1);
    periodDisplayText.textContent = targetMonthStart.toLocaleString("en-US", { month: "long" });
  } else if (period === "week") {
    const dayOfWeek = now.getDay();
    const diffToMonday = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
    const currentMonday = new Date(now.getFullYear(), now.getMonth(), now.getDate() + diffToMonday);
    currentMonday.setHours(0, 0, 0, 0);

    const targetMonday = new Date(currentMonday.getFullYear(), currentMonday.getMonth(), currentMonday.getDate() + offset * 7);
    let targetEnd;
    if (offset === 0) {
      targetEnd = new Date(now);
    } else {
      targetEnd = new Date(targetMonday.getFullYear(), targetMonday.getMonth(), targetMonday.getDate() + 6);
    }

    const startDay = targetMonday.getDate().toString().padStart(2, "0");
    const endDay = targetEnd.getDate().toString().padStart(2, "0");
    periodDisplayText.textContent = `${startDay}-${endDay}`;
  }
}

function isPlaceholderImage(url) {
  if (!url) return true;
  return url.includes("d41d8cd98f00b204e9800998ecf8427e");
}

function selectBestArtist(items, targetName) {
  if (!items || items.length === 0) return null;
  const targetLower = (targetName || "").toLowerCase().trim();

  const validMatches = items.filter(
    (item) =>
      item.name &&
      item.name.toLowerCase().trim() === targetLower &&
      !isPlaceholderImage(item.picture_medium || item.picture),
  );

  if (validMatches.length > 0) {
    validMatches.sort((a, b) => (b.nb_fan || 0) - (a.nb_fan || 0));
    return validMatches[0];
  }

  const anyValid = items.filter((item) => !isPlaceholderImage(item.picture_medium || item.picture));
  if (anyValid.length > 0) {
    anyValid.sort((a, b) => (b.nb_fan || 0) - (a.nb_fan || 0));
    return anyValid[0];
  }

  return items[0];
}

function fetchDeezerJsonp(type, query) {
  return new Promise((resolve) => {
    const callbackName = "deezer_cb_" + Math.random().toString(36).substring(2);
    const script = document.createElement("script");

    const timer = setTimeout(() => {
      delete window[callbackName];
      if (script.parentNode) script.parentNode.removeChild(script);
      resolve(null);
    }, 5000);

    window[callbackName] = (data) => {
      clearTimeout(timer);
      if (script.parentNode) script.parentNode.removeChild(script);
      delete window[callbackName];
      resolve(data);
    };

    const cleanQuery = (query || "").replace(/["']/g, "").trim();
    script.src = `https://api.deezer.com/search/${type}?q=${encodeURIComponent(cleanQuery)}&output=jsonp&callback=${callbackName}`;
    script.onerror = () => {
      clearTimeout(timer);
      if (script.parentNode) script.parentNode.removeChild(script);
      delete window[callbackName];
      resolve(null);
    };
    document.body.appendChild(script);
  });
}

async function fetchAssetData(type, query) {
  const cleanQuery = (query || "").replace(/["']/g, "").trim();
  if (!cleanQuery) return null;
  try {
    const jsonpData = await fetchDeezerJsonp(type, cleanQuery);
    if (jsonpData && jsonpData.data && jsonpData.data.length > 0) return jsonpData;
  } catch (e) {
    console.warn("Deezer JSONP fetch warning:", e);
  }
  return null;
}

const IGNORED_TAGS = new Set([
  "seen live",
  "favorites",
  "favorite",
  "spotify",
  "albums i own",
  "female vocalists",
  "male vocalists",
  "american",
  "british",
  "canadian",
  "english",
  "swedish",
  "japanese",
  "german",
  "french",
  "australian",
  "under 2000 listeners",
  "scrobble",
  "all",
  "loved",
  "my favorites",
  "favorite artists",
  "check out",
  "tracks i own"
]);

function formatVibeTag(tag) {
  if (!tag) return "";
  const lower = tag.toLowerCase().trim();
  if (lower === "r&b" || lower === "rnb") return "R&B";
  if (lower === "edm") return "EDM";
  if (lower === "k-pop" || lower === "kpop") return "K-Pop";
  if (lower === "j-pop" || lower === "jpop") return "J-Pop";
  if (lower === "idm") return "IDM";
  return lower
    .split(/([\s\-])/)
    .map((part) => (part.length > 0 ? part.charAt(0).toUpperCase() + part.slice(1) : part))
    .join("");
}

async function fetchTopVibeTag(artists, lastfmBaseUrl) {
  if (!artists || artists.length === 0) return null;
  const topArtists = artists.slice(0, 5);
  const tagScores = {};

  const promises = topArtists.map(async (artist) => {
    try {
      const res = await fetch(
        `${lastfmBaseUrl}?method=artist.gettoptags&artist=${encodeURIComponent(artist.name)}&_t=${Date.now()}`
      );
      if (!res.ok) return;
      const json = await res.json();
      let rawTags = json.toptags?.tag || [];
      if (!Array.isArray(rawTags)) rawTags = [rawTags];

      rawTags.slice(0, 8).forEach((t) => {
        const tagName = (t.name || "").toLowerCase().trim();
        if (!tagName || IGNORED_TAGS.has(tagName)) return;
        const count = parseInt(t.count || 0, 10) || 1;
        const weight = count * (artist.playcount || 1);
        tagScores[tagName] = (tagScores[tagName] || 0) + weight;
      });
    } catch (e) {}
  });

  await Promise.all(promises);

  const sortedTags = Object.keys(tagScores).sort((a, b) => tagScores[b] - tagScores[a]);
  if (sortedTags.length > 0) {
    return formatVibeTag(sortedTags[0]);
  }
  return null;
}

function initFaqModal() {
  const faqToggle = document.getElementById("faq-toggle");
  if (!faqToggle) return;

  const isMatchMode = window.location.pathname.includes("match");

  let faqModal = document.getElementById("faqModal");
  if (!faqModal) {
    faqModal = document.createElement("div");
    faqModal.id = "faqModal";
    faqModal.className = "modal";
    faqModal.setAttribute("aria-hidden", "true");

    const singleFaqHtml = `
      <div class="modal-content faq-modal-content">
        <span class="close-button" id="faqCloseBtn">&times;</span>
        <div class="faq-header">
          <h2>Frequently Asked Questions</h2>
          <p class="modal-info">Quick answers about BubbleFM features, calculations, and feedback.</p>
        </div>
        <div class="faq-list">
          <details class="faq-item" open>
            <summary class="faq-question">How are monthly and weekly charts calculated?</summary>
            <div class="faq-answer">
              <p><strong>Monthly charts</strong> count scrobbles starting from the 1st day of the current month. <strong>Weekly charts</strong> count from Monday of the current week. Once these timeframes end, counts automatically reset for the next period.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">How can I view detailed stats for individual items?</summary>
            <div class="faq-answer">
              <p>Hover over (or tap on mobile) any artist, album, or song in your chart to view its total scrobble count and estimated listening time in minutes.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">How do I generate and download a shareable story card?</summary>
            <div class="faq-answer">
              <p>Click the green floating button at the bottom right labeled <strong>Generate card</strong>. Follow the quick step-by-step setup to pick your layout, colors, and format, then download your image.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">How do I switch my card's Light / Dark theme?</summary>
            <div class="faq-answer">
              <p>The generated card automatically matches the website's active theme. Click the <strong>Sun / Moon icon</strong> in the top header to toggle between light and dark mode before generating your card.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">Are my personal data or Last.fm credentials saved?</summary>
            <div class="faq-answer">
              <p>No login or account creation is required! All stats and images are fetched live on your device using public APIs (Last.fm, Deezer, MusicBrainz). Your data is never saved on servers.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">Feedback, Suggestions & Bug Reports</summary>
            <div class="faq-answer">
              <p>BubbleFM is open-source! We welcome community contributions and feedback on GitHub:</p>
              <ul class="faq-links-list">
                <li><strong>Design Feedback:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=feedback.yml" target="_blank" rel="noopener noreferrer">Propose a design or UI improvement</a></li>
                <li><strong>Feature Ideas:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=feature.yml" target="_blank" rel="noopener noreferrer">Suggest a new feature</a></li>
                <li><strong>Bug Reports:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=bug.yml" target="_blank" rel="noopener noreferrer">Report an issue or bug</a></li>
              </ul>
            </div>
          </details>
        </div>
      </div>
    `;

    const matchFaqHtml = `
      <div class="modal-content faq-modal-content">
        <span class="close-button" id="faqCloseBtn">&times;</span>
        <div class="faq-header">
          <h2>Match FAQ</h2>
          <p class="modal-info">Quick answers about BubbleFM Match calculations, compatibility score, and charts.</p>
        </div>
        <div class="faq-list">
          <details class="faq-item" open>
            <summary class="faq-question">What timeframe is calculated for Match?</summary>
            <div class="faq-answer">
              <p>Match compatibility and top charts are calculated based on listening history from the <strong>last 30 days</strong> for both Last.fm users.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">How is the compatibility percentage calculated?</summary>
            <div class="faq-answer">
              <p>Compatibility compares the top 100 artists of both users over the last 30 days. It measures shared artists relative to the maximum possible overlap:</p>
              <p style="margin-top: 0.4rem; background: var(--color-neutral-100); padding: 0.5rem; border-radius: 6px; font-family: monospace; font-size: 0.82rem;">(Shared Artists ÷ Minimum Total Artists) × 100</p>
              <p style="margin-top: 0.4rem;">For example, if both users have 100 top artists and share 45 of them, your match score is <strong>45%</strong>.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">What is the difference between User Vibe and Common Artists?</summary>
            <div class="faq-answer">
              <p><strong>User Vibe:</strong> Displays top individual artists listened to by each user over the last 30 days.</p>
              <p><strong>Common Artists:</strong> Ranks top artists listened to by both users, combining their shared scrobble counts.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">How do I switch the Match card's Light / Dark theme?</summary>
            <div class="faq-answer">
              <p>The generated card automatically matches the website's active theme. Click the <strong>Sun / Moon icon</strong> in the top header to toggle between light and dark mode before generating your card.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">Are my personal data or Last.fm credentials saved?</summary>
            <div class="faq-answer">
              <p>No login or account creation is required! All stats and images are fetched live on your device using public APIs (Last.fm, Deezer, MusicBrainz). Your data is never saved on servers.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">Feedback, Suggestions & Bug Reports</summary>
            <div class="faq-answer">
              <p>BubbleFM is open-source! We welcome community contributions and feedback on GitHub:</p>
              <ul class="faq-links-list">
                <li><strong>Design Feedback:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=feedback.yml" target="_blank" rel="noopener noreferrer">Propose a design or UI improvement</a></li>
                <li><strong>Feature Ideas:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=feature.yml" target="_blank" rel="noopener noreferrer">Suggest a new feature</a></li>
                <li><strong>Bug Reports:</strong> <a href="https://github.com/snw-mint/bubblefm/issues/new?template=bug.yml" target="_blank" rel="noopener noreferrer">Report an issue or bug</a></li>
              </ul>
            </div>
          </details>
        </div>
      </div>
    `;

    faqModal.innerHTML = isMatchMode ? matchFaqHtml : singleFaqHtml;
    document.body.appendChild(faqModal);
  }

  const faqCloseBtn = document.getElementById("faqCloseBtn");

  const openFaq = () => {
    faqModal.classList.add("show");
    faqModal.setAttribute("aria-hidden", "false");

    try {
      if (typeof umami !== "undefined") {
        umami.track("FAQ Opened");
      }
      const count = parseInt(localStorage.getItem("bubblefm_faq_open_count") || "0", 10) + 1;
      localStorage.setItem("bubblefm_faq_open_count", count.toString());
      console.log(`[Analytics] FAQ Opened. Total local opens: ${count}`);
    } catch (e) {}
  };

  const closeFaq = () => {
    faqModal.classList.remove("show");
    faqModal.setAttribute("aria-hidden", "true");
  };

  faqToggle.addEventListener("click", openFaq);
  if (faqCloseBtn) faqCloseBtn.addEventListener("click", closeFaq);
  faqModal.addEventListener("click", (e) => {
    if (e.target === faqModal) closeFaq();
  });
}

function initGlobalTooltip() {
  let globalTooltip = document.getElementById("globalTooltip");
  if (!globalTooltip) {
    globalTooltip = document.createElement("div");
    globalTooltip.id = "globalTooltip";
    globalTooltip.className = "global-tooltip";
    document.body.appendChild(globalTooltip);
  }

  document.addEventListener("mouseover", (e) => {
    const chartItem = e.target.closest(".chart-item");
    if (chartItem && !chartItem.closest("#storyCardContainer")) {
      const plays = chartItem.getAttribute("data-plays");
      const minutes = chartItem.getAttribute("data-minutes");
      if (plays && minutes) {
        globalTooltip.textContent = `${plays} streams / ${minutes} min`;
        globalTooltip.classList.add("show");

        const rect = chartItem.getBoundingClientRect();

        let top = rect.top + window.scrollY - 40;
        let left = rect.left + window.scrollX + rect.width / 2;

        globalTooltip.style.top = `${top}px`;
        globalTooltip.style.left = `${left}px`;
      }
    } else {
      globalTooltip.classList.remove("show");
    }
  });
}
initGlobalTooltip();

function resetToSkeletons() {
  const btnGerarRelatorio = document.getElementById("btnGerarRelatorio");
  if (btnGerarRelatorio) {
    btnGerarRelatorio.classList.add("hidden");
  }

  const textFields = ["userScrobbles", "userMinutes", "userDailyAvg", "userVibe"];
  textFields.forEach((id) => {
    const el = document.getElementById(id);
    if (el) {
      el.textContent = "";
      el.classList.add("skeleton", "skeleton-text");
      el.style.width = "80px";
    }
  });

  const chartContainers = ["listTopArtists", "listTopTracks", "listTopAlbums"];
  chartContainers.forEach((id) => {
    const el = document.getElementById(id);
    if (el) {
      el.innerHTML = `
        <div class="chart-item top-1">
            <div class="top1-image skeleton skeleton-icon">
                <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 -960 960 960" width="24px" fill="#e3e3e3"><path d="M480-300q75 0 127.5-52.5T660-480q0-75-52.5-127.5T480-660q-75 0-127.5 52.5T300-480q0 75 52.5 127.5T480-300Zm-28.5-151.5Q440-463 440-480t11.5-28.5Q463-520 480-520t28.5 11.5Q520-497 520-480t-11.5 28.5Q497-440 480-440t-28.5-11.5ZM480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z"/></svg>
            </div>
            <div class="text-content">
                <span class="skeleton skeleton-text" style="width: 120px; height: 18px;"></span>
                <span class="skeleton skeleton-text" style="width: 80px; height: 14px; margin-top: 4px;"></span>
            </div>
        </div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#2</span> <span class="skeleton skeleton-text" style="width: 150px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#3</span> <span class="skeleton skeleton-text" style="width: 130px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#4</span> <span class="skeleton skeleton-text" style="width: 160px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#5</span> <span class="skeleton skeleton-text" style="width: 140px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#6</span> <span class="skeleton skeleton-text" style="width: 170px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#7</span> <span class="skeleton skeleton-text" style="width: 120px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#8</span> <span class="skeleton skeleton-text" style="width: 145px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#9</span> <span class="skeleton skeleton-text" style="width: 155px; height: 16px;"></span></div>
        <div class="chart-item"><span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#10</span> <span class="skeleton skeleton-text" style="width: 135px; height: 16px;"></span></div>
      `;
    }
  });
}

async function fetchLastfmAndDeezerData(username, period = "month", offset = 0) {
  const cacheKey = `${username}_${period}_${offset}`;

  if (periodCache[cacheKey]) {
    currentActiveData = periodCache[cacheKey];
    renderData(username, currentActiveData);
    return;
  }

  const stored = getLocalStorageCache(cacheKey);
  if (stored) {
    periodCache[cacheKey] = stored;
    currentActiveData = stored;
    renderData(username, stored);
    return;
  }

  try {
    const lastfmBaseUrl = "https://bubblefm.snw-mint.workers.dev/data";

    let from, to;
    const now = new Date();
    let subtitleText = "";
    let reviewLabel = "Month Review";

    if (period === "month") {
      const targetMonthStart = new Date(now.getFullYear(), now.getMonth() + offset, 1, 0, 0, 0);
      from = Math.floor(targetMonthStart.getTime() / 1000);
      if (offset === 0) {
        to = Math.floor(now.getTime() / 1000);
      } else {
        const targetMonthEnd = new Date(now.getFullYear(), now.getMonth() + offset + 1, 0, 23, 59, 59);
        to = Math.floor(targetMonthEnd.getTime() / 1000);
      }
      subtitleText = targetMonthStart.toLocaleString("en-US", { month: "long" }).toUpperCase();
      reviewLabel = "Month Review";
    } else if (period === "week") {
      const dayOfWeek = now.getDay();
      const diffToMonday = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
      const currentMonday = new Date(now.getFullYear(), now.getMonth(), now.getDate() + diffToMonday);
      currentMonday.setHours(0, 0, 0, 0);

      const targetMonday = new Date(currentMonday.getFullYear(), currentMonday.getMonth(), currentMonday.getDate() + offset * 7, 0, 0, 0);
      from = Math.floor(targetMonday.getTime() / 1000);

      let targetEnd;
      if (offset === 0) {
        targetEnd = new Date(now);
        to = Math.floor(now.getTime() / 1000);
      } else {
        targetEnd = new Date(targetMonday.getFullYear(), targetMonday.getMonth(), targetMonday.getDate() + 6, 23, 59, 59);
        to = Math.floor(targetEnd.getTime() / 1000);
      }

      const startDay = targetMonday.getDate().toString().padStart(2, "0");
      const endDay = targetEnd.getDate().toString().padStart(2, "0");
      const monthShort = targetEnd.toLocaleString("en-US", { month: "short" }).toLowerCase();
      subtitleText = `${startDay}-${endDay} ${monthShort}`;
      reviewLabel = "Week Review";
    }

    const [userInfoRes, firstPageRes] = await Promise.all([
      fetch(`${lastfmBaseUrl}?method=user.getinfo&user=${username}&_t=${Date.now()}`),
      fetch(
        `${lastfmBaseUrl}?method=user.getrecenttracks&user=${username}&limit=200&from=${from}&to=${to}&_t=${Date.now()}`,
      ),
    ]);

    const userInfo = await userInfoRes.json();
    const firstPageData = await firstPageRes.json();

    let rawTracks = firstPageData.recenttracks?.track || [];
    if (!Array.isArray(rawTracks)) rawTracks = [rawTracks];

    const totalPages = parseInt(firstPageData.recenttracks?.["@attr"]?.totalPages || 0, 10);

    if (totalPages > 1) {
      const promises = [];
      for (let i = 2; i <= totalPages; i++) {
        promises.push(
          fetch(
            `${lastfmBaseUrl}?method=user.getrecenttracks&user=${username}&limit=200&page=${i}&from=${from}&to=${to}&_t=${Date.now()}`,
          ).then((r) => r.json()),
        );
      }
      const pagesData = await Promise.all(promises);
      pagesData.forEach((page) => {
        let pageTracks = page.recenttracks?.track || [];
        if (!Array.isArray(pageTracks)) pageTracks = [pageTracks];
        rawTracks = rawTracks.concat(pageTracks);
      });
    }

    const artistMap = {};
    const albumMap = {};
    const trackMap = {};

    rawTracks.forEach((track) => {
      const artistName = track.artist?.["#text"] || track.artist?.name;
      const albumName = track.album?.["#text"];
      const trackName = track.name;

      if (!artistName) return;

      if (!artistMap[artistName]) artistMap[artistName] = { name: artistName, playcount: 0 };
      artistMap[artistName].playcount++;

      if (albumName) {
        const albumKey = `${artistName} - ${albumName}`;
        if (!albumMap[albumKey]) albumMap[albumKey] = { name: albumName, artist: { name: artistName }, playcount: 0 };
        albumMap[albumKey].playcount++;
      }

      if (trackName) {
        const trackKey = `${artistName} - ${trackName}`;
        if (!trackMap[trackKey]) trackMap[trackKey] = { name: trackName, artist: { name: artistName }, playcount: 0 };
        trackMap[trackKey].playcount++;
      }
    });

    const artists = Object.values(artistMap).sort((a, b) => b.playcount - a.playcount);
    const albums = Object.values(albumMap).sort((a, b) => b.playcount - a.playcount);
    const tracks = Object.values(trackMap).sort((a, b) => b.playcount - a.playcount);

    const topArtistName = artists[0]?.name;
    const topAlbumName = albums[0]?.name;
    const topAlbumArtist = albums[0]?.artist?.name;
    const topTrackName = tracks[0]?.name;
    const topTrackArtist = tracks[0]?.artist?.name;

    let artistImage = null;
    let artistCoverImage = null;
    let albumImage = null;
    let trackImage = null;
    let vibeTag = null;

    const assetPromises = [];

    if (artists && artists.length > 0) {
      assetPromises.push(
        fetchTopVibeTag(artists, lastfmBaseUrl)
          .then((v) => {
            vibeTag = v;
          })
          .catch(() => {}),
      );
    }

    if (topArtistName) {
      assetPromises.push(
        fetchAssetData("artist", topArtistName)
          .then((data) => {
            if (data && data.data && data.data.length > 0) {
              const bestArtist = selectBestArtist(data.data, topArtistName);
              if (bestArtist) {
                artistImage = bestArtist.picture_medium || bestArtist.picture;
                artistCoverImage = bestArtist.picture_xl || bestArtist.picture_big || bestArtist.picture;
              }
            }
          })
          .catch((err) => console.warn("Artist asset fetch warning:", err)),
      );
    }

    if (topAlbumName) {
      const albumQuery = `${topAlbumName} ${topAlbumArtist || ""}`;
      assetPromises.push(
        fetchAssetData("album", albumQuery)
          .then((data) => {
            if (data && data.data && data.data.length > 0) {
              albumImage = data.data[0].cover_medium || data.data[0].cover;
            }
          })
          .catch((err) => console.warn("Album asset fetch warning:", err)),
      );
    }

    if (topTrackName) {
      const trackQuery = `${topTrackName} ${topTrackArtist || ""}`;
      assetPromises.push(
        fetchAssetData("track", trackQuery)
          .then((data) => {
            if (data && data.data && data.data.length > 0) {
              const item = data.data[0];
              trackImage = item.album ? item.album.cover_medium || item.album.cover : item.cover_medium || item.cover;
            }
          })
          .catch((err) => console.warn("Track asset fetch warning:", err)),
      );
    }

    await Promise.all(assetPromises);

    const data = {
      artists,
      albums,
      tracks,
      artistImage,
      artistCoverImage,
      albumImage,
      trackImage,
      userInfo,
      rawTracks,
      from,
      to,
      period,
      offset,
      subtitleText,
      reviewLabel,
      vibeTag,
    };

    periodCache[cacheKey] = data;
    setLocalStorageCache(cacheKey, data);
    currentActiveData = data;
    renderData(username, data);
  } catch (error) {
    console.error("Error fetching API data:", error);
  }
}

function renderData(username, data) {
  const {
    artists,
    albums,
    tracks,
    artistImage,
    artistCoverImage,
    albumImage,
    trackImage,
    userInfo,
    rawTracks,
    from,
    to,
    vibeTag,
  } = data;

  function renderList(listId, items, type) {
    const container = document.getElementById(listId);
    if (!container || !items || items.length === 0) return;

    let html = "";
    items.slice(0, 10).forEach((item, index) => {
      const rank = index + 1;
      const name = escapeHTML(item.name);
      const playcountNum = parseInt(item.playcount || 0, 10);
      const playcountStr = playcountNum.toLocaleString("en-US");
      const minutesStr = Math.round(playcountNum * 3.5).toLocaleString("en-US");

      if (rank === 1) {
        let subText = "";
        if (type === "artist") {
          subText = `${playcountStr} plays`;
        } else if (type === "track" || type === "album") {
          subText = `${escapeHTML(item.artist.name)} - ${playcountStr} plays`;
        }

        html += `
                    <div class="chart-item top-1" data-plays="${playcountStr}" data-minutes="${minutesStr}" style="cursor: pointer;">
                        <div class="top1-image skeleton skeleton-icon" id="${type}1Skeleton">
                            <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 -960 960 960" width="24px" fill="#e3e3e3"><path d="M480-300q75 0 127.5-52.5T660-480q0-75-52.5-127.5T480-660q-75 0-127.5 52.5T300-480q0 75 52.5 127.5T480-300Zm-28.5-151.5Q440-463 440-480t11.5-28.5Q463-520 480-520t28.5 11.5Q520-497 520-480t-11.5 28.5Q497-440 480-440t-28.5-11.5ZM480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z"/></svg>
                        </div>
                        <img class="top1-image" id="${type}1Img" alt="Top ${type}" style="display: none;" />
                        <div class="text-content">
                            <span>${name}</span>
                            <span style="font-size: 0.9rem; opacity: 0.8;">${subText}</span>
                        </div>
                    </div>
                `;
      } else {
        let text = "";
        if (type === "artist") {
          text = name;
        } else if (type === "track" || type === "album") {
          text = `${name} - ${escapeHTML(item.artist.name)}`;
        }
        html += `
                    <div class="chart-item" data-plays="${playcountStr}" data-minutes="${minutesStr}" style="cursor: pointer;">
                        <span style="font-weight: bold; margin-right: 15px; color: var(--color-neutral-500);">#${rank}</span> ${text}
                    </div>
                `;
      }
    });
    container.innerHTML = html;
  }

  renderList("listTopArtists", artists, "artist");
  renderList("listTopTracks", tracks, "track");
  renderList("listTopAlbums", albums, "album");

  function updateTop1Image(type, imageUrl) {
    const imgEl = document.getElementById(`${type}1Img`);
    const skeletonEl = document.getElementById(`${type}1Skeleton`);
    if (imgEl && skeletonEl && imageUrl) {
      imgEl.src = imageUrl;
      imgEl.onload = () => {
        imgEl.classList.add("fade-in");
        imgEl.style.display = "block";
        skeletonEl.style.display = "none";
      };
    }
  }

  updateTop1Image("artist", artistImage);
  updateTop1Image("album", albumImage);
  updateTop1Image("track", trackImage);

  const userAvatarEl = document.getElementById("userAvatar");
  const userAvatarContainer = document.getElementById("userAvatarContainer");
  const userDisplayNameEl = document.getElementById("userDisplayName");

  const artistCoverEl = document.getElementById("artistCover");
  const coverContainer = document.getElementById("coverContainer");

  if (userDisplayNameEl) {
    userDisplayNameEl.textContent = userInfo?.user?.realname || userInfo?.user?.name || username;
    userDisplayNameEl.classList.remove("skeleton", "skeleton-text");
    userDisplayNameEl.style.width = "auto";
  }

  if (userAvatarEl && userAvatarContainer) {
    const images = userInfo?.user?.image;
    const avatarUrl = Array.isArray(images)
      ? images.find((img) => img.size === "extralarge")?.["#text"] ||
        images.find((img) => img.size === "large")?.["#text"] ||
        images[images.length - 1]?.["#text"]
      : null;
    if (avatarUrl && avatarUrl.trim() !== "") {
      userAvatarEl.src = avatarUrl;
      userAvatarEl.onload = () => {
        userAvatarEl.classList.add("fade-in");
        userAvatarEl.style.display = "block";
        document.getElementById("userAvatarSkeletonIcon").style.display = "none";
        userAvatarContainer.classList.remove("skeleton", "skeleton-icon");
      };
    }
  }

  if (artistCoverEl && coverContainer && artistCoverImage) {
    artistCoverEl.src = artistCoverImage;
    artistCoverEl.onload = () => {
      artistCoverEl.classList.add("fade-in");
      artistCoverEl.style.display = "block";
      document.getElementById("coverSkeletonIcon").style.display = "none";
      coverContainer.classList.remove("skeleton", "skeleton-icon");
    };
  }

  const userScrobblesEl = document.getElementById("userScrobbles");
  const userMinutesEl = document.getElementById("userMinutes");
  const userDailyAvgEl = document.getElementById("userDailyAvg");

  function removeSkeletonText(el) {
    if (el) {
      el.classList.remove("skeleton", "skeleton-text");
      el.style.width = "auto";
    }
  }

  if (userScrobblesEl) {
    const playcount = rawTracks.length;
    userScrobblesEl.textContent = playcount.toLocaleString("en-US");
    removeSkeletonText(userScrobblesEl);

    if (userMinutesEl) {
      const estimatedMinutes = Math.round(playcount * 3.5);
      userMinutesEl.textContent = estimatedMinutes.toLocaleString("en-US");
      removeSkeletonText(userMinutesEl);
    }

    if (userDailyAvgEl) {
      const periodStartMs = from * 1000;
      const currentMs = to * 1000;
      let daysElapsed = Math.max(1, Math.ceil((currentMs - periodStartMs) / (1000 * 60 * 60 * 24)));
      const dailyAvg = Math.round(playcount / daysElapsed);
      userDailyAvgEl.textContent = dailyAvg.toLocaleString("en-US");
      removeSkeletonText(userDailyAvgEl);
    }

    const userVibeEl = document.getElementById("userVibe");
    if (userVibeEl) {
      userVibeEl.textContent = vibeTag || "-";
      removeSkeletonText(userVibeEl);
    }
  }

  const chartsTimeTextEl = document.getElementById("chartsTimeText");
  if (chartsTimeTextEl) {
    const activePeriodBtn = document.querySelector(".time-toggle-btn.active");
    const currentP = activePeriodBtn ? activePeriodBtn.dataset.period : "month";
    if (currentP === "week") {
      chartsTimeTextEl.textContent = "Showing charts since last Monday";
    } else {
      const d = new Date();
      const mStr = d.toLocaleString("en-US", { month: "long" });
    }
  }

  const btnGerarRelatorio = document.getElementById("btnGerarRelatorio");
  if (btnGerarRelatorio) {
    btnGerarRelatorio.classList.remove("hidden");
  }
}

document.addEventListener("DOMContentLoaded", () => {
  const btnGerarRelatorio = document.getElementById("btnGerarRelatorio");

  const formatPickerModal = document.getElementById("formatPickerModal");
  const closeFormatPicker = document.getElementById("closeFormatPicker");
  const formatOptions = document.querySelectorAll("#formatPickerModal .card-option");
  const confirmFormatBtn = document.getElementById("confirmFormatBtn");
  let selectedFormat = "9x16";

  const columnPickerModal = document.getElementById("columnPickerModal");
  const closeColumnPicker = document.getElementById("closeColumnPicker");
  const chartColOptions = document.querySelectorAll(".chart-col-option");
  const confirmColumnsBtn = document.getElementById("confirmColumnsBtn");

  if (btnGerarRelatorio && formatPickerModal) {
    btnGerarRelatorio.addEventListener("click", () => {
      formatPickerModal.classList.add("show");
    });

    if (closeFormatPicker) {
      closeFormatPicker.addEventListener("click", () => {
        formatPickerModal.classList.remove("show");
      });
    }

    formatPickerModal.addEventListener("click", (e) => {
      if (e.target === formatPickerModal) {
        formatPickerModal.classList.remove("show");
      }
    });
  }

  if (formatOptions.length > 0) {
    formatOptions.forEach((btn) => {
      btn.addEventListener("click", () => {
        formatOptions.forEach((b) => b.classList.remove("selected"));
        btn.classList.add("selected");
        selectedFormat = btn.dataset.format;
      });
    });
  }

  if (confirmFormatBtn && columnPickerModal) {
    confirmFormatBtn.addEventListener("click", () => {
      formatPickerModal.classList.remove("show");

      const selectedCount = Array.from(chartColOptions).filter((cb) => cb.checked).length;
      const subtitle = document.getElementById("columnPickerSubtitle");
      if (selectedFormat === "3x4" || selectedFormat === "1x1") {
        confirmColumnsBtn.disabled = selectedCount !== 2;
        if (subtitle) subtitle.textContent = "Choose exactly 2 charts to display.";
      } else {
        confirmColumnsBtn.disabled = !(selectedCount > 0 && selectedCount <= 2);
        if (subtitle) subtitle.textContent = "Choose 1 or 2 charts to display.";
      }

      columnPickerModal.classList.add("show");
    });

    if (closeColumnPicker) {
      closeColumnPicker.addEventListener("click", () => {
        columnPickerModal.classList.remove("show");
      });
    }

    columnPickerModal.addEventListener("click", (e) => {
      if (e.target === columnPickerModal) {
        columnPickerModal.classList.remove("show");
      }
    });
  }

  if (chartColOptions.length > 0) {
    chartColOptions.forEach((checkbox) => {
      checkbox.addEventListener("change", () => {
        const row = checkbox.closest(".custom-checkbox-row");
        if (checkbox.checked) {
          row.classList.add("checked");
        } else {
          row.classList.remove("checked");
        }

        const selectedCount = Array.from(chartColOptions).filter((cb) => cb.checked).length;

        if (selectedCount >= 2) {
          chartColOptions.forEach((cb) => {
            if (!cb.checked) {
              cb.disabled = true;
              cb.closest(".custom-checkbox-row").classList.add("disabled");
            }
          });
        } else {
          chartColOptions.forEach((cb) => {
            cb.disabled = false;
            cb.closest(".custom-checkbox-row").classList.remove("disabled");
          });
        }

        if (selectedFormat === "3x4" || selectedFormat === "1x1") {
          confirmColumnsBtn.disabled = selectedCount !== 2;
        } else {
          confirmColumnsBtn.disabled = !(selectedCount > 0 && selectedCount <= 2);
        }
      });

      const row = checkbox.closest(".custom-checkbox-row");
      row.addEventListener("click", (e) => {
        if (e.target !== checkbox && !checkbox.disabled) {
          checkbox.checked = !checkbox.checked;
          checkbox.dispatchEvent(new Event("change"));
        }
      });
    });
  }

  const colorPickerModal = document.getElementById("colorPickerModal");
  const closeColorPicker = document.getElementById("closeColorPicker");
  const colorOptions = document.querySelectorAll(".color-option");
  const customColorBtn = document.getElementById("customColorBtn");
  const customColorPicker = document.getElementById("customColorPicker");
  const confirmColorBtn = document.getElementById("confirmColorBtn");

  let selectedColor = null;

  if (confirmColumnsBtn && colorPickerModal) {
    confirmColumnsBtn.addEventListener("click", () => {
      columnPickerModal.classList.remove("show");
      colorPickerModal.classList.add("show");
    });

    if (closeColorPicker) {
      closeColorPicker.addEventListener("click", () => {
        colorPickerModal.classList.remove("show");
      });
    }

    colorPickerModal.addEventListener("click", (e) => {
      if (e.target === colorPickerModal) {
        colorPickerModal.classList.remove("show");
      }
    });
  }

  if (colorOptions.length > 0) {
    colorOptions.forEach((btn) => {
      btn.addEventListener("click", () => {
        if (btn.id === "customColorBtn") {
          customColorPicker.click();
          return;
        }

        colorOptions.forEach((b) => b.classList.remove("selected"));
        btn.classList.add("selected");
        selectedColor = btn.dataset.color;

        if (confirmColorBtn) confirmColorBtn.disabled = false;
      });
    });
  }

  if (customColorPicker) {
    customColorPicker.addEventListener("input", (e) => {
      const hexColor = e.target.value;
      customColorBtn.style.backgroundColor = hexColor;

      colorOptions.forEach((b) => b.classList.remove("selected"));
      customColorBtn.classList.add("selected");
      selectedColor = hexColor;

      if (confirmColorBtn) confirmColorBtn.disabled = false;
    });
  }

  const imagePickerModal = document.getElementById("imagePickerModal");
  const closeImagePicker = document.getElementById("closeImagePicker");
  const defaultBgCard = document.getElementById("defaultBgCard");
  const customBgCard = document.getElementById("customBgCard");
  const customBgInput = document.getElementById("customBgInput");
  const confirmImageBtn = document.getElementById("confirmImageBtn");

  let selectedBgType = null;
  let customBgDataUrl = null;

  if (confirmColorBtn && imagePickerModal) {
    confirmColorBtn.addEventListener("click", () => {
      colorPickerModal.classList.remove("show");
      if (selectedFormat === "1x1") {
        if (confirmImageBtn) {
          confirmImageBtn.disabled = false;
          confirmImageBtn.click();
        }
      } else {
        imagePickerModal.classList.add("show");
      }
    });

    if (closeImagePicker) {
      closeImagePicker.addEventListener("click", () => {
        imagePickerModal.classList.remove("show");
      });
    }

    imagePickerModal.addEventListener("click", (e) => {
      if (e.target === imagePickerModal) {
        imagePickerModal.classList.remove("show");
      }
    });
  }

  if (defaultBgCard && customBgCard) {
    defaultBgCard.addEventListener("click", () => {
      defaultBgCard.classList.add("selected");
      customBgCard.classList.remove("selected");
      selectedBgType = "default";
      if (confirmImageBtn) confirmImageBtn.disabled = false;
    });

    customBgCard.addEventListener("click", (e) => {
      if (e.target.tagName.toLowerCase() !== "input") {
        customBgInput.click();
      }
    });
  }

  if (customBgInput) {
    customBgInput.addEventListener("change", (e) => {
      const file = e.target.files[0];
      if (file) {
        const reader = new FileReader();
        reader.onload = (event) => {
          customBgDataUrl = event.target.result;
          selectedBgType = "custom";

          customBgCard.classList.add("selected");
          defaultBgCard.classList.remove("selected");
          if (confirmImageBtn) confirmImageBtn.disabled = false;
        };
        reader.readAsDataURL(file);
      }
    });
  }

  const storyCardContainer = document.getElementById("storyCardContainer");
  const storyBody = document.getElementById("storyBody");

  if (confirmImageBtn && storyCardContainer) {
    confirmImageBtn.addEventListener("click", async () => {
      imagePickerModal.classList.remove("show");
      const chartColOptions = document.querySelectorAll(".chart-col-option");
      const selectedCharts = Array.from(chartColOptions)
        .filter((cb) => cb.checked)
        .map((cb) => cb.value);

      if (!currentActiveData) {
        alert("Data not fully loaded yet. Please wait.");
        return;
      }
      const data = currentActiveData;
      const gradient = document.getElementById("storyCardGradient");
      if (gradient) {
        const c = selectedColor || "#bb86fc";
        gradient.style.background = `radial-gradient(circle at 100% 100%, ${c} 0%, transparent 55%)`;
        gradient.style.filter = "none";
        gradient.style.opacity = "0.25";
      }

      const cardElement = document.getElementById("storyCard");
      if (cardElement) {
        cardElement.classList.remove("format-9x16", "format-3x4", "format-1x1");
        cardElement.classList.add(`format-${selectedFormat}`);
      }

      const separator = document.querySelector(".story-separator");
      if (separator) {
        separator.style.backgroundColor = selectedColor || "#bb86fc";
      }
      const customStoryBg = document.getElementById("customStoryBg");
      const userImg = document.getElementById("storyUserImg");

      if (selectedBgType === "custom" && customBgDataUrl) {
        customStoryBg.style.backgroundImage = `url(${customBgDataUrl})`;
        customStoryBg.style.display = "block";
      } else {
        customStoryBg.style.display = "none";
        if (data.artistCoverImage) {
          customStoryBg.style.backgroundImage = `url(${data.artistCoverImage})`;
          customStoryBg.style.display = "block";
        }
      }
      if (data.userInfo && data.userInfo.user) {
        userImg.src =
          data.userInfo.user.image.find((img) => img.size === "extralarge")?.["#text"] ||
          data.userInfo.user.image[0]?.["#text"];
        const storyTitleEl = document.getElementById("storyTitle");
        storyTitleEl.textContent = data.userInfo.user.name;
        storyTitleEl.style.color = selectedColor || "#bb86fc";
      }

      const storySubtitleEl = document.getElementById("storySubtitle");
      if (storySubtitleEl) {
        storySubtitleEl.textContent = data.subtitleText || "";
        storySubtitleEl.style.color = selectedColor || "#bb86fc";
      }

      const storyReviewLabel = document.getElementById("storyReviewLabel");
      if (storyReviewLabel) {
        storyReviewLabel.textContent = data.reviewLabel || "Month Review";
      }
      const minutes = Math.round(data.rawTracks.length * 3.5);
      const isSingle = selectedCharts.length === 1;
      const scrobblesValueEl = document.getElementById("storyScrobblesValue");
      const scrobblesLabelEl = document.getElementById("storyScrobblesLabel");
      const statGroupEl = scrobblesValueEl.parentElement;

      if (selectedFormat === "3x4" || selectedFormat === "1x1") {
        scrobblesValueEl.textContent = `${minutes.toLocaleString("en-US")} minutes`;
        scrobblesLabelEl.style.display = "none";
        statGroupEl.style.flexDirection = "row";
        scrobblesValueEl.style.fontSize = "3.5rem";
      } else {
        if (isSingle) {
          scrobblesValueEl.textContent = `${minutes.toLocaleString("en-US")} minutes`;
          scrobblesLabelEl.style.display = "none";
          statGroupEl.style.flexDirection = "row";
          scrobblesValueEl.style.fontSize = "4.5rem";
        } else {
          scrobblesValueEl.textContent = minutes.toLocaleString("en-US");
          scrobblesLabelEl.textContent = "Total Minutes";
          scrobblesLabelEl.style.display = "block";
          statGroupEl.style.flexDirection = "column";
          scrobblesValueEl.style.fontSize = "";
        }
      }

      storyBody.innerHTML = "";

      const getTop5 = (list) => list.slice(0, 5);
      const fetchAssetImage = async (type, query, targetName = "") => {
        try {
          const json = await fetchAssetData(type, query);
          if (json && json.data && json.data.length > 0) {
            if (type === "artist") {
              const bestArtist = selectBestArtist(json.data, targetName || query);
              return bestArtist ? bestArtist.picture_medium || bestArtist.picture : null;
            } else if (type === "track" && json.data[0].album) {
              return json.data[0].album.cover_medium || json.data[0].album.cover;
            } else {
              return json.data[0].cover_medium || json.data[0].cover;
            }
          }
        } catch (e) {
          console.warn("fetchAssetImage warning:", e);
        }
        return null;
      };

      for (let i = 0; i < selectedCharts.length; i++) {
        const chartType = selectedCharts[i];
        let items = [];
        let title = "";
        let searchType = "";

        if (chartType === "artists") {
          items = getTop5(data.artists);
          title = "Top Artists";
          searchType = "artist";
        } else if (chartType === "tracks") {
          items = getTop5(data.tracks);
          title = "Top Songs";
          searchType = "track";
        } else if (chartType === "albums") {
          items = getTop5(data.albums);
          title = "Top Albums";
          searchType = "album";
        }

        const colDiv = document.createElement("div");
        colDiv.className = "story-column" + (isSingle ? " single-col" : "");
        colDiv.innerHTML = `<h3 style="border-left-color: ${selectedColor || "#bb86fc"}">${title}</h3>`;

        const listDiv = document.createElement("div");
        listDiv.className = "story-list";

        for (let j = 0; j < items.length; j++) {
          const item = items[j];
          const rank = j + 1;
          const itemDiv = document.createElement("div");
          let isTop1 = rank === 1;
          if (selectedFormat === "3x4" || selectedFormat === "1x1") {
            isTop1 = false;
          }
          itemDiv.className = `story-item ${isTop1 ? "top-1" : ""}`;

          let imgHtml = "";
          if (isSingle) {
            let q = "";
            let t = "";
            const cleanItemName = (item.name || "").replace(/["']/g, "").trim();
            const cleanArtistName = (item.artist?.name || "").replace(/["']/g, "").trim();
            if (chartType === "artists") {
              q = cleanItemName;
              t = "artist";
            } else if (chartType === "albums") {
              q = `${cleanItemName} ${cleanArtistName}`.trim();
              t = "album";
            } else {
              q = `${cleanItemName} ${cleanArtistName}`.trim();
              t = "track";
            }
            const imgSrc = (await fetchAssetImage(t, q, item.name)) || "https://via.placeholder.com/150";
            imgHtml = `<img src="${imgSrc}" class="story-item-img" />`;
          }

          let metaHtml = "";
          if (chartType === "tracks" || chartType === "albums") {
            metaHtml = `<span class="story-meta">${item.artist.name}</span>`;
          } else {
            metaHtml = `<span class="story-meta">${item.playcount} streams</span>`;
          }

          itemDiv.innerHTML = `
            <span class="story-rank" style="color: ${selectedColor || "#bb86fc"}">${rank}</span>
            ${imgHtml}
            <div class="story-item-content">
              <span class="story-text">${item.name}</span>
              ${metaHtml}
            </div>
          `;
          listDiv.appendChild(itemDiv);
        }

        colDiv.appendChild(listDiv);
        storyBody.appendChild(colDiv);
      }
      storyCardContainer.style.opacity = "0";
      storyCardContainer.style.zIndex = "-999";
      confirmImageBtn.textContent = "Generating...";

      const generationModal = document.getElementById("generationModal");
      const stateLoading = document.getElementById("generationStateLoading");
      const stateComplete = document.getElementById("generationStateComplete");

      if (generationModal) {
        generationModal.style.display = "flex";
        stateLoading.style.display = "flex";
        stateComplete.style.display = "none";
      }

      setTimeout(() => {
        const cardElement = document.getElementById("storyCard");
        html2canvas(cardElement, {
          useCORS: true,
          allowTaint: true,
          scale: 2,
          backgroundColor: null,
        })
          .then((canvas) => {
            const imgData = canvas.toDataURL("image/png");
            const link = document.createElement("a");
            link.download = `bubblefm_report_${new Date().getTime()}.png`;
            link.href = imgData;
            link.click();

            if (typeof umami !== "undefined") {
              const activeTimeBtn = document.querySelector(".time-toggle-btn.active");
              umami.track("Card Generated", {
                type: window.location.pathname.includes("match") ? "match" : "single",
                period: activeTimeBtn ? activeTimeBtn.dataset.period : "month",
                charts: selectedCharts.join(","),
                color: selectedColor || "#bb86fc",
                cover: selectedBgType || "default",
                format: selectedFormat || "9x16",
                ratio: selectedFormat || "9x16",
              });
            }

            confirmImageBtn.textContent = "Next";
            confirmImageBtn.disabled = false;

            if (generationModal) {
              stateLoading.style.display = "none";
              stateComplete.style.display = "flex";
              setTimeout(() => {
                generationModal.style.display = "none";
              }, 3000);
            }
          })
          .catch((err) => {
            console.error("Error generating canvas", err);
            confirmImageBtn.textContent = "Error";
            if (generationModal) {
              generationModal.style.display = "none";
            }
          });
      }, 1000);
    });
  }
});
