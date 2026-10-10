/* Apilan — landing page behaviour: the sticky nav, scroll reveals, the
   ticket tilt, and the two looping demos in the hero.

   The demos below (the arrivals feed and the door kiosk) are deliberately
   canned: they are an illustration of the check-in flow on a marketing page,
   not a view of anybody's real attendance. */
(function () {
    "use strict";

    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var $ = function (s, r) { return (r || document).querySelector(s); };
    var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

    /* ---- Sticky nav hairline --------------------------------------------- */
    var nav = $(".site-nav");
    if (nav) {
        var onScroll = function () { nav.classList.toggle("is-stuck", window.scrollY > 8); };
        window.addEventListener("scroll", onScroll, { passive: true });
        onScroll();
    }

    /* ---- Scroll reveal ---------------------------------------------------- */
    var revealables = $$(".reveal");
    if (revealables.length) {
        if (REDUCED || !("IntersectionObserver" in window)) {
            revealables.forEach(function (el) { el.classList.add("is-in"); });
        } else {
            var io = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add("is-in");
                    io.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: "0px 0px -8% 0px" });
            revealables.forEach(function (el) { io.observe(el); });
        }
    }

    /* ---- Ticket tilt ------------------------------------------------------ */
    var ticket = $("[data-tilt]");
    if (ticket && !REDUCED && window.matchMedia("(hover: hover)").matches) {
        var frame = null;
        ticket.addEventListener("mousemove", function (e) {
            if (frame) return;
            frame = requestAnimationFrame(function () {
                frame = null;
                var r = ticket.getBoundingClientRect();
                var px = (e.clientX - r.left) / r.width - 0.5;
                var py = (e.clientY - r.top) / r.height - 0.5;
                ticket.style.transform =
                    "rotateY(" + (px * 8).toFixed(2) + "deg) rotateX(" + (-py * 8).toFixed(2) + "deg) translateZ(10px)";
            });
        });
        ticket.addEventListener("mouseleave", function () { ticket.style.transform = ""; });
    }

    /* ---- Hero door card: attendance ticking up ---------------------------- */
    var ARRIVALS = [
        "Mara Villanueva", "Job Dela Cruz", "Aliyah Ramos", "Kier Tolentino",
        "Nica Bautista", "Paolo Medina", "Shane Aguilar", "Rex Domingo",
        "Trina Lazaro", "Ivan Corpuz", "Bea Sarmiento", "Dana Mercado"
    ];

    var feed = $("[data-feed]");
    var countEl = $("[data-count]");
    var barEl = $("[data-bar]");

    if (feed && countEl && barEl) {
        var CAP = 180;
        var checked = 128;
        var clock = new Date(2026, 9, 2, 17, 8);

        var initials = function (name) {
            return name.split(" ").map(function (w) { return w[0]; }).join("").slice(0, 2).toUpperCase();
        };

        var paint = function () {
            countEl.textContent = checked;
            barEl.style.width = Math.min(100, Math.round((checked / CAP) * 100)) + "%";
        };

        var arrive = function (name, bump) {
            clock = new Date(clock.getTime() + (40 + Math.floor(Math.random() * 90)) * 1000);
            var li = document.createElement("li");
            li.innerHTML =
                '<span class="av">' + initials(name) + "</span>" +
                "<span>" + name + "</span>" +
                '<span class="t">' +
                String(clock.getHours()).padStart(2, "0") + ":" +
                String(clock.getMinutes()).padStart(2, "0") + "</span>";
            feed.prepend(li);
            while (feed.children.length > 3) { feed.lastElementChild.remove(); }
            if (bump) { checked += 1; }
            paint();
        };

        arrive(ARRIVALS[2], false);
        arrive(ARRIVALS[1], false);
        arrive(ARRIVALS[0], false);

        if (!REDUCED) {
            var ai = 3;
            setInterval(function () {
                if (document.hidden || checked >= CAP) return;
                arrive(ARRIVALS[ai % ARRIVALS.length], true);
                ai++;
            }, 4200);
        }
    }

    /* ---- Door kiosk demo -------------------------------------------------- */
    var kiosk = $("[data-kiosk]");
    if (kiosk) {
        var codeEl = $("[data-kiosk-code]", kiosk);
        var resultEl = $("[data-kiosk-result]", kiosk);
        var whoEl = $("[data-kiosk-who]", kiosk);
        var verdictEl = $("[data-kiosk-verdict]", kiosk);

        var CASES = [
            { code: "2022-04417", who: "Mara Villanueva",   verdict: "Time-in 5:12 PM",        tone: "ok" },
            { code: "2021-07725", who: "Kier Tolentino",    verdict: "Late · time-in 5:24 PM", tone: "dupe" },
            { code: "2022-09043", who: "Aliyah Ramos",      verdict: "Time-out 7:41 PM",       tone: "ok" },
            { code: "2099-00000", who: "Student not found", verdict: "Not a member",           tone: "bad" }
        ];

        var caret = '<span class="caret"></span>';
        var idx = 0;
        var busy = false;

        var settle = function (c) {
            whoEl.textContent = c.who;
            verdictEl.textContent = c.verdict;
            resultEl.className = "kiosk-result " + c.tone + " is-in";

            kiosk.classList.remove("ok-flash", "bad-flash");
            void kiosk.offsetWidth;
            kiosk.classList.add(c.tone === "bad" ? "bad-flash" : "ok-flash");
            setTimeout(function () { kiosk.classList.remove("ok-flash", "bad-flash"); }, 900);
            busy = false;
        };

        var run = function () {
            if (busy) return;
            busy = true;
            var c = CASES[idx % CASES.length];
            idx++;

            resultEl.className = "kiosk-result " + c.tone;

            if (REDUCED) {
                codeEl.textContent = c.code;
                settle(c);
                return;
            }

            codeEl.innerHTML = caret;
            var typed = "";
            var k = 0;
            var type = function () {
                if (k < c.code.length) {
                    typed += c.code[k++];
                    codeEl.innerHTML = typed + caret;
                    setTimeout(type, 90 + Math.random() * 60);
                } else {
                    setTimeout(function () { settle(c); }, 380);
                }
            };
            type();
        };

        var started = false;
        var start = function () {
            if (started) return;
            started = true;
            run();
            if (REDUCED) return;
            setInterval(function () { if (!document.hidden) run(); }, 4800);
        };

        if ("IntersectionObserver" in window) {
            var kobs = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) { start(); kobs.disconnect(); }
                });
            }, { threshold: 0.35 });
            kobs.observe(kiosk);
        } else {
            start();
        }
    }
}());
