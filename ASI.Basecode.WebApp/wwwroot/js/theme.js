/* Apilan — theme switching.
   The no-flash setter is inlined in _LayoutPublic <head>; this file only wires
   the toggle button and keeps the tab in sync with the OS preference. */
(function () {
    "use strict";

    var KEY = "apilan-theme";
    var root = document.documentElement;
    var media = window.matchMedia("(prefers-color-scheme: dark)");

    function apply(theme, persist) {
        root.setAttribute("data-theme", theme);
        if (persist) {
            try { localStorage.setItem(KEY, theme); } catch (e) { /* private mode */ }
        }
        document.querySelectorAll("[data-theme-toggle]").forEach(function (btn) {
            btn.setAttribute("aria-pressed", String(theme === "dark"));
            btn.setAttribute("aria-label", theme === "dark" ? "Switch to light theme" : "Switch to dark theme");
        });
    }

    function stored() {
        try { return localStorage.getItem(KEY); } catch (e) { return null; }
    }

    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-theme-toggle]");
        if (!btn) return;
        var next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";

        // View Transitions where supported; a plain swap everywhere else.
        if (document.startViewTransition && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            document.startViewTransition(function () { apply(next, true); });
        } else {
            apply(next, true);
        }
    });

    // Follow the OS only while the user has not made an explicit choice.
    media.addEventListener("change", function (e) {
        if (!stored()) apply(e.matches ? "dark" : "light", false);
    });

    apply(root.getAttribute("data-theme") || "light", false);
}());
