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

    /* ---- Your account, from the bottom of the sidebar ---------------------
       A native popover, so it sits above everything (the sidebar scrolls and
       would clip it) and closes on Esc or a click elsewhere by itself. It is
       placed from the button: above it in the full sidebar, beside it on the
       icon rail, below it when the sidebar is the strip on a narrow screen. */
    var pop = document.getElementById("account-pop");
    var me = document.querySelector("[popovertarget='account-pop']");
    if (!pop || !me || typeof pop.showPopover !== "function") { return; }

    var GAP = 8;

    function place() {
        var r = me.getBoundingClientRect();
        var w = Math.max(r.width, 248);
        var h = pop.offsetHeight;
        var left, top;

        if (r.width < 80) {
            left = r.right + GAP * 1.5;
            top = r.bottom - h;
            pop.setAttribute("data-from", "left");
        } else if (r.top - h - GAP >= GAP) {
            left = r.left;
            top = r.top - h - GAP;
            pop.setAttribute("data-from", "below");
        } else {
            left = r.left;
            top = r.bottom + GAP;
            pop.setAttribute("data-from", "above");
        }

        pop.style.width = w + "px";
        pop.style.left = Math.max(GAP, Math.min(left, window.innerWidth - w - GAP)) + "px";
        pop.style.top = Math.max(GAP, top) + "px";
    }

    pop.addEventListener("toggle", function (e) {
        var isOpen = e.newState === "open";
        me.setAttribute("aria-expanded", String(isOpen));
        me.classList.toggle("is-open", isOpen);
        if (isOpen) {
            place();
            var first = pop.querySelector(".ap-item");
            if (first) { first.focus(); }
        }
    });

    // Measure while open but before it paints, so it never flashes elsewhere.
    pop.addEventListener("beforetoggle", function (e) {
        if (e.newState === "open") { requestAnimationFrame(place); }
    });

    window.addEventListener("resize", function () {
        if (pop.matches(":popover-open")) { place(); }
    });

    // Change password opens in the modal; the menu steps aside for it.
    pop.addEventListener("click", function (e) {
        if (e.target.closest(".ap-item")) { pop.hidePopover(); }
    });
}());
