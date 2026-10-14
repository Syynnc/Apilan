/* Apilan — console shell.
   The sidebar collapses to an icon rail. The no-flash setter is inlined in
   _LayoutAdmin <head>; this file wires the button, keeps the accessible state
   honest, and remembers the choice per browser. */
(function () {
    "use strict";

    var KEY = "apilan-side";
    var root = document.documentElement;

    /* Below this width the sidebar is already a horizontal strip, so there is
       nothing to collapse — see the breakpoint in admin.css. */
    var wide = window.matchMedia("(min-width: 961px)");

    function collapsed() {
        return root.getAttribute("data-side") === "collapsed";
    }

    function apply(isCollapsed, persist) {
        if (isCollapsed) {
            root.setAttribute("data-side", "collapsed");
        } else {
            root.removeAttribute("data-side");
        }

        if (persist) {
            try {
                localStorage.setItem(KEY, isCollapsed ? "collapsed" : "open");
            } catch (e) { /* private mode — the rail just forgets */ }
        }

        document.querySelectorAll("[data-side-toggle]").forEach(function (btn) {
            btn.setAttribute("aria-expanded", String(!isCollapsed));

            var label = btn.querySelector(".visually-hidden");
            if (label) {
                label.textContent = isCollapsed ? "Expand the sidebar" : "Collapse the sidebar";
            }

            btn.title = isCollapsed ? "Expand the sidebar" : "Collapse the sidebar";
        });
    }

    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-side-toggle]");
        if (!btn) { return; }

        apply(!collapsed(), true);
    });

    /* A rail that was collapsed on a laptop should not stay collapsed once the
       layout drops to the horizontal strip and back. */
    wide.addEventListener("change", function () {
        if (!wide.matches) { return; }

        var stored = null;
        try { stored = localStorage.getItem(KEY); } catch (e) { /* ignore */ }
        apply(stored === "collapsed", false);
    });

    apply(collapsed(), false);
}());
