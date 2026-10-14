/* Apilan — behaviour shared by the console and the member pages: scroll
   reveals and the tilting ticket. Each feature is a small block that does
   nothing when its markup is not on the page. */
(function () {
    "use strict";

    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

    window.Apilan = window.Apilan || {};

    /* ---- Scroll reveal ---------------------------------------------------- */
    var revealables = $$(".reveal");
    if (revealables.length) {
        if (REDUCED || !("IntersectionObserver" in window)) {
            revealables.forEach(function (el) { el.classList.add("is-in"); });
        } else {
            var io = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) { return; }
                    entry.target.classList.add("is-in");
                    io.unobserve(entry.target);
                });
            }, { threshold: 0.12, rootMargin: "0px 0px -6% 0px" });
            revealables.forEach(function (el) { io.observe(el); });
        }
    }

    /* ---- Ticket tilt ------------------------------------------------------ */
    if (!REDUCED && window.matchMedia("(hover: hover)").matches) {
        $$("[data-tilt]").forEach(function (ticket) {
            var frame = null;
            ticket.addEventListener("mousemove", function (e) {
                if (frame) { return; }
                frame = requestAnimationFrame(function () {
                    frame = null;
                    var r = ticket.getBoundingClientRect();
                    var px = (e.clientX - r.left) / r.width - 0.5;
                    var py = (e.clientY - r.top) / r.height - 0.5;
                    ticket.style.transform =
                        "perspective(1200px) rotateY(" + (px * 6).toFixed(2) + "deg) rotateX(" + (-py * 6).toFixed(2) + "deg) translateZ(8px)";
                });
            });
            ticket.addEventListener("mouseleave", function () { ticket.style.transform = ""; });
        });
    }
}());
