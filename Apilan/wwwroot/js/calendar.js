/* Apilan — the month calendar widget.

   Reads real published events from /Calendar/Feed rather than a hard-coded
   array. The server-rendered month view at /Calendar/Month is the accessible,
   no-JavaScript path; this is the same calendar for the landing page, where a
   full page load per month would be the wrong trade.

   Both are built from one projection on the server, so the marketing page and
   the live calendar cannot disagree about what is on. */
(function () {
    "use strict";

    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    var DOW = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    var MONTHS = ["January", "February", "March", "April", "May", "June",
                  "July", "August", "September", "October", "November", "December"];

    var $ = function (s, r) { return (r || document).querySelector(s); };
    var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

    var iso = function (y, m, d) {
        return y + "-" + String(m + 1).padStart(2, "0") + "-" + String(d).padStart(2, "0");
    };

    var esc = function (value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    };

    var cal = $("[data-cal]");
    if (!cal) {
        return;
    }

    var gridEl = $("[data-cal-grid]", cal);
    var monthEl = $("[data-cal-month]", cal);
    var prevBtn = $("[data-cal-prev]", cal);
    var nextBtn = $("[data-cal-next]", cal);
    var bodyEl = $("[data-cal-body]");
    var dowEl = $("[data-detail-dow]");
    var dateEl = $("[data-detail-date]");

    var feedUrl = cal.getAttribute("data-cal-feed") || "/Calendar/Feed";
    var detailUrl = cal.getAttribute("data-cal-detail") || "/Calendar/Details";

    var today = new Date();
    var view = { y: today.getFullYear(), m: today.getMonth() };
    var selected = null;

    /* Events keyed by "YYYY-MM-DD". The feed already flattens a multi-day
       event onto each of its days, so this stays a plain lookup. */
    var byDay = {};

    /* Months already fetched. The feed returns a month either side of the one
       asked for, so stepping back and forth does not re-request. */
    var loaded = {};

    var eventsOn = function (key) {
        return byDay[key] || [];
    };

    var renderDetail = function (key) {
        if (!bodyEl) {
            return;
        }

        var parts = key.split("-").map(Number);
        var dt = new Date(parts[0], parts[1] - 1, parts[2]);
        var list = eventsOn(key);

        if (dowEl) {
            dowEl.textContent = DOW[dt.getDay()] + " · " +
                                (list.length === 1 ? "1 event" : list.length + " events");
        }
        if (dateEl) {
            dateEl.textContent = parts[2] + " " + MONTHS[parts[1] - 1] + " " + parts[0];
        }

        if (!list.length) {
            bodyEl.innerHTML =
                '<div class="empty-a">' +
                "<h4>Nothing scheduled</h4>" +
                "<p>Pick another date, or step to the next month.</p>" +
                "</div>";
            return;
        }

        bodyEl.innerHTML = '<ul class="cal-events">' + list.map(function (e) {
            var pct = e.cap > 0 ? Math.round((e.taken / e.cap) * 100) : 0;
            var tone = pct >= 100 ? "full" : (pct >= 85 ? "tight" : "");
            var chip = pct >= 100
                ? '<span class="chip risk">Full</span>'
                : (pct >= 85 ? '<span class="chip warn">Few slots</span>'
                             : '<span class="chip ok">Open</span>');

            return "<li>" +
                '<div class="ev-top"><div>' +
                    '<div class="ev-name"><a href="' + detailUrl + "/" + encodeURIComponent(e.id) + '">' +
                        esc(e.name) + "</a></div>" +
                    '<span class="ev-when">' + esc(e.time) + " · " + esc(e.venue) +
                        " · " + esc(e.org) + "</span>" +
                "</div>" + chip + "</div>" +
                '<div class="ev-track"><i class="' + tone + '" style="width:' +
                    Math.min(100, pct) + '%"></i></div>' +
                '<div class="ev-slots"><span>Registered</span>' +
                '<span class="mono">' + e.taken + " / " + e.cap + "</span></div>" +
            "</li>";
        }).join("") + "</ul>";
    };

    var swapDetail = function (key) {
        selected = key;

        if (REDUCED || !bodyEl) {
            renderDetail(key);
            return;
        }

        bodyEl.classList.add("is-swapping");
        setTimeout(function () {
            renderDetail(key);
            bodyEl.classList.remove("is-swapping");
        }, 140);
    };

    var renderMonth = function () {
        var todayKey = iso(today.getFullYear(), today.getMonth(), today.getDate());

        monthEl.innerHTML = MONTHS[view.m] + "<span>" + view.y + "</span>";

        var first = new Date(view.y, view.m, 1).getDay();
        var days = new Date(view.y, view.m + 1, 0).getDate();
        var prevDays = new Date(view.y, view.m, 0).getDate();
        var cells = [];

        for (var p = first - 1; p >= 0; p--) {
            cells.push('<div class="cal-day pad" aria-hidden="true">' + (prevDays - p) + "</div>");
        }

        for (var d = 1; d <= days; d++) {
            var key = iso(view.y, view.m, d);
            var list = eventsOn(key);
            var cls = "cal-day";

            if (key === todayKey) { cls += " today"; }

            if (list.length) {
                cls += " has";
                if (key === selected) { cls += " is-sel"; }

                var marks = list.slice(0, 3).map(function (e) {
                    return '<i class="' + esc(e.type) + '"></i>';
                }).join("");

                var label = d + " " + MONTHS[view.m] + ", " +
                            (list.length === 1 ? "1 event" : list.length + " events");

                cells.push(
                    '<button type="button" class="' + cls + '" data-day="' + key + '" ' +
                    'aria-pressed="' + (key === selected) + '" aria-label="' + label + '">' +
                    "<span>" + d + "</span>" +
                    '<span class="cal-marks">' + marks + "</span>" +
                    "</button>"
                );
            } else {
                cells.push('<div class="' + cls + '"><span>' + d + "</span>" +
                           '<span class="cal-marks"></span></div>');
            }
        }

        var tail = (7 - (cells.length % 7)) % 7;
        for (var t = 1; t <= tail; t++) {
            cells.push('<div class="cal-day pad" aria-hidden="true">' + t + "</div>");
        }

        gridEl.innerHTML = cells.join("");
    };

    /* Pick a sensible day to open on: the next day this month that has
       something, else the first day that does, else the 1st. */
    var defaultDay = function () {
        var days = new Date(view.y, view.m + 1, 0).getDate();
        var todayKey = iso(today.getFullYear(), today.getMonth(), today.getDate());
        var firstWithAny = null;

        for (var d = 1; d <= days; d++) {
            var key = iso(view.y, view.m, d);
            if (!eventsOn(key).length) { continue; }

            if (!firstWithAny) { firstWithAny = key; }
            if (key >= todayKey) { return key; }
        }

        return firstWithAny || iso(view.y, view.m, 1);
    };

    var load = function () {
        var stamp = view.y + "-" + view.m;

        if (loaded[stamp]) {
            return Promise.resolve();
        }

        return fetch(feedUrl + "?year=" + view.y + "&month=" + (view.m + 1), {
            headers: { "Accept": "application/json" }
        })
            .then(function (res) {
                if (!res.ok) { throw new Error("feed " + res.status); }
                return res.json();
            })
            .then(function (entries) {
                loaded[stamp] = true;

                (entries || []).forEach(function (e) {
                    if (!byDay[e.d]) { byDay[e.d] = []; }

                    // The feed overlaps months by design, so the same entry can
                    // arrive twice. Key on the event id per day.
                    var already = byDay[e.d].some(function (x) { return x.id === e.id; });
                    if (!already) { byDay[e.d].push(e); }
                });
            })
            .catch(function () {
                // A calendar that cannot reach the server shows an empty month
                // rather than a broken one. The month view still works.
                loaded[stamp] = true;
            });
    };

    var show = function (keepSelection) {
        load().then(function () {
            if (!keepSelection || !selected) {
                selected = defaultDay();
            }

            renderMonth();
            renderDetail(selected);
        });
    };

    gridEl.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-day]");
        if (!btn) { return; }

        $$("[data-day]", gridEl).forEach(function (b) {
            b.classList.remove("is-sel");
            b.setAttribute("aria-pressed", "false");
        });

        btn.classList.add("is-sel");
        btn.setAttribute("aria-pressed", "true");
        swapDetail(btn.getAttribute("data-day"));
    });

    var shift = function (delta) {
        view.m += delta;

        if (view.m < 0) { view.m = 11; view.y--; }
        if (view.m > 11) { view.m = 0; view.y++; }

        show(false);
    };

    if (prevBtn) { prevBtn.addEventListener("click", function () { shift(-1); }); }
    if (nextBtn) { nextBtn.addEventListener("click", function () { shift(1); }); }

    show(false);
})();
