(() => {
  const themeToggle = document.getElementById("theme-switcher");
  if (!themeToggle) return;

  const metaThemeColor = document.querySelector('meta[name="theme-color"]');
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)");
  const getStoredTheme = () => localStorage.getItem("theme-preference");

  const applyTheme = (mode, persist = true) => {
    const theme = mode === "dark" ? "dark" : "light";
    document.documentElement.setAttribute("data-theme", theme);
    themeToggle.classList.toggle("is-dark", theme === "dark");
    themeToggle.setAttribute("aria-pressed", theme === "dark");
    themeToggle.setAttribute("aria-label", theme === "dark" ? "Switch to light mode" : "Switch to dark mode");
    if (metaThemeColor) {
      metaThemeColor.setAttribute("content", theme === "dark" ? "#0b0c0f" : "#0f6cbd");
    }
    if (persist) {
      localStorage.setItem("theme-preference", theme);
    }
  };

  const storedTheme = getStoredTheme();
  const initialTheme = storedTheme || (prefersDark.matches ? "dark" : "light");
  applyTheme(initialTheme, Boolean(storedTheme));

  prefersDark.addEventListener("change", (event) => {
    if (!getStoredTheme()) {
      applyTheme(event.matches ? "dark" : "light", false);
    }
  });

  themeToggle.addEventListener("click", () => {
    const isDark = document.documentElement.getAttribute("data-theme") === "dark";
    applyTheme(isDark ? "light" : "dark");
  });
})();

document.querySelectorAll(".feature-card, .minor-card, .support-card").forEach((card) => {
  card.addEventListener("mousemove", (e) => {
    const rect = card.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    card.style.setProperty("--mouse-x", `${x}px`);
    card.style.setProperty("--mouse-y", `${y}px`);
  });
});

const observerOptions = {
  root: null,
  rootMargin: "0px",
  threshold: 0.15,
};

const observer = new IntersectionObserver((entries, observer) => {
  entries.forEach((entry) => {
    if (entry.isIntersecting) {
      entry.target.classList.add("in-view");
    }
  });
}, observerOptions);

document.querySelectorAll(".showcase-image-wrapper, .showcase-card").forEach((el) => {
  observer.observe(el);
});

const mobileMenuToggle = document.getElementById("mobile-menu-toggle");
const navLinks = document.querySelector(".nav-links");

if (mobileMenuToggle && navLinks) {
  mobileMenuToggle.addEventListener("click", (e) => {
    e.stopPropagation();
    navLinks.classList.toggle("active");
  });

  document.addEventListener("click", (e) => {
    if (!mobileMenuToggle.contains(e.target) && !navLinks.contains(e.target)) {
      navLinks.classList.remove("active");
    }
  });

  navLinks.querySelectorAll("a").forEach((link) => {
    link.addEventListener("click", () => {
      navLinks.classList.remove("active");
    });
  });
}
