/* Apilan — the member's account menu.
   A native popover under the name in the top bar, holding Change password
   and Sign out. It closes on Esc or a click elsewhere by itself; this only
   places it under the button, right-aligned, and keeps aria-expanded honest. */
(function () {
    "use strict";

    var pop = document.getElementById("member-menu");
    var me = document.querySelector("[popovertarget='member-menu']");
    if (!pop || !me || typeof pop.showPopover !== "function") { return; }

    var GAP = 8;

    function place() {
        var r = me.getBoundingClientRect();
        var w = pop.offsetWidth;
        pop.setAttribute("data-from", "above");
        pop.style.left = Math.max(GAP, Math.min(r.right - w, window.innerWidth - w - GAP)) + "px";
        pop.style.top = (r.bottom + GAP) + "px";
    }

    pop.addEventListener("beforetoggle", function (e) {
        if (e.newState === "open") { requestAnimationFrame(place); }
    });

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

    window.addEventListener("resize", function () {
        if (pop.matches(":popover-open")) { place(); }
    });
}());
