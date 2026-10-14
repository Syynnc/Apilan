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

/* ---- Copy buttons: <button data-copy="#element"> ------------------------ */
(function () {
    "use strict";
    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-copy]");
        if (!btn) { return; }
        var source = document.querySelector(btn.getAttribute("data-copy"));
        if (!source || !navigator.clipboard) { return; }
        navigator.clipboard.writeText(source.textContent.trim()).then(function () {
            var was = btn.textContent;
            btn.textContent = "Copied";
            btn.classList.add("is-done");
            setTimeout(function () { btn.textContent = was; btn.classList.remove("is-done"); }, 1400);
        });
    });
}());

/* ---- Chart tooltips: any [data-tip="Title|line|line"] ------------------- */
/* Follows the pointer over a mark; on keyboard focus it sits above the mark. */
(function () {
    "use strict";
    var tip = document.getElementById("chart-tip");
    if (!tip) { return; }

    function show(el, x, y) {
        var parts = el.getAttribute("data-tip").split("|");
        tip.innerHTML = "";
        parts.forEach(function (p, i) {
            var line = document.createElement(i === 0 ? "strong" : "span");
            line.textContent = p;
            tip.appendChild(line);
        });
        tip.hidden = false;
        var r = tip.getBoundingClientRect();
        var left = Math.min(window.innerWidth - r.width - 12, Math.max(12, x - r.width / 2));
        var top = y - r.height - 14;
        if (top < 8) { top = y + 18; }
        tip.style.left = left + "px";
        tip.style.top = top + "px";
        el.classList.add("is-hot");
    }

    function hide(el) {
        tip.hidden = true;
        if (el) { el.classList.remove("is-hot"); }
    }

    document.addEventListener("pointermove", function (e) {
        var el = e.target.closest ? e.target.closest("[data-tip]") : null;
        document.querySelectorAll("[data-tip].is-hot").forEach(function (h) { if (h !== el) { h.classList.remove("is-hot"); } });
        if (el) { show(el, e.clientX, e.clientY); } else { hide(); }
    });
    document.addEventListener("focusin", function (e) {
        var el = e.target.closest && e.target.closest("[data-tip]");
        if (!el) { return; }
        var r = el.getBoundingClientRect();
        show(el, r.left + r.width / 2, r.top);
    });
    document.addEventListener("focusout", function (e) {
        var el = e.target.closest && e.target.closest("[data-tip]");
        if (el) { hide(el); }
    });
    window.addEventListener("scroll", function () { hide(); }, { passive: true });
}());
