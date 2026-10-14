/* Apilan — tables: pagination, live filtering, and search suggestions.
   Loaded by the console and member layouts after app.js. */

/* ==========================================================================
   Pagination and live filtering
   --------------------------------------------------------------------------
   <table data-paginate="10" data-page-sizes="5,10,25,50" id="...">
   Rows may carry data-search="…" to say what a filter should match against;
   otherwise their text is used. Without JavaScript every row is simply
   shown, so nothing is ever hidden for good.
   ========================================================================== */
(function () {
    "use strict";

    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    function norm(s) {
        return (s || "").toString().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim();
    }

    function el(tag, cls, text) {
        var n = document.createElement(tag);
        if (cls) { n.className = cls; }
        if (text != null) { n.textContent = text; }
        return n;
    }

    function Pager(table) {
        this.table = table;
        this.body = table.tBodies[0];
        this.rows = Array.prototype.slice.call(this.body.rows).filter(function (r) { return !r.hasAttribute("data-static"); });
        this.rows.forEach(function (r) { r._text = norm(r.getAttribute("data-search") || r.textContent); });
        this.sizes = (table.getAttribute("data-page-sizes") || "5,10,25,50").split(",").map(Number);
        this.key = table.id ? "apilan-page-size:" + location.pathname.toLowerCase() + "#" + table.id : null;

        var stored = null;
        try { stored = this.key && Number(localStorage.getItem(this.key)); } catch (e) { /* private mode */ }
        this.size = stored && this.sizes.indexOf(stored) >= 0 ? stored : Number(table.getAttribute("data-paginate")) || 10;
        this.page = 1;
        this.term = "";

        this.build();
        this.render(false);
    }

    Pager.prototype.build = function () {
        var self = this;
        var cols = this.table.tHead ? this.table.tHead.rows[0].cells.length : 1;

        this.empty = el("tr", "pager-empty");
        this.empty.setAttribute("data-static", "");
        var td = el("td");
        td.colSpan = cols;
        this.emptyText = el("div", "pager-empty-text");
        td.appendChild(this.emptyText);
        this.empty.appendChild(td);
        this.empty.hidden = true;
        this.body.appendChild(this.empty);

        var bar = el("div", "pager");
        bar.setAttribute("role", "navigation");
        bar.setAttribute("aria-label", "Table pages");

        var sizeWrap = el("label", "pager-size");
        sizeWrap.appendChild(el("span", null, "Rows per page"));
        this.select = el("select", "input-a");
        this.sizes.forEach(function (n) {
            var o = el("option", null, String(n));
            o.value = n;
            if (n === self.size) { o.selected = true; }
            self.select.appendChild(o);
        });
        this.select.addEventListener("change", function () {
            self.size = Number(self.select.value);
            self.page = 1;
            try { if (self.key) { localStorage.setItem(self.key, self.size); } } catch (e) { /* ignore */ }
            self.render(true);
        });
        sizeWrap.appendChild(this.select);

        this.info = el("span", "pager-info");
        this.info.setAttribute("aria-live", "polite");
        this.pages = el("div", "pager-pages");

        bar.appendChild(sizeWrap);
        bar.appendChild(this.info);
        bar.appendChild(this.pages);

        this.table.parentNode.insertBefore(bar, this.table.nextSibling);
        this.bar = bar;
    };

    Pager.prototype.filter = function (term) {
        this.term = norm(term);
        this.page = 1;
        this.render(true);
    };

    Pager.prototype.go = function (page) {
        this.page = page;
        this.render(true);
        if (this.table.getBoundingClientRect().top < 0) {
            this.table.scrollIntoView({ block: "start", behavior: REDUCED ? "auto" : "smooth" });
        }
    };

    Pager.prototype.render = function (animate) {
        var self = this;
        var terms = this.term ? this.term.split(/\s+/) : [];
        var matches = this.rows.filter(function (r) {
            return terms.every(function (t) { return r._text.indexOf(t) >= 0; });
        });

        var total = matches.length;
        var pages = Math.max(1, Math.ceil(total / this.size));
        if (this.page > pages) { this.page = pages; }
        var from = (this.page - 1) * this.size;
        var shown = matches.slice(from, from + this.size);

        this.rows.forEach(function (r) { r.hidden = true; r.classList.remove("row-in"); });
        shown.forEach(function (r, i) {
            r.hidden = false;
            if (animate && !REDUCED) {
                r.style.animationDelay = (i * 22) + "ms";
                void r.offsetWidth;
                r.classList.add("row-in");
            }
        });

        this.empty.hidden = total > 0;
        this.emptyText.textContent = this.term ? "No matches for “" + this.term + "”." : "Nothing to show.";

        this.info.textContent = total === 0
            ? "0 results"
            : (from + 1) + "–" + (from + shown.length) + " of " + total;

        // A list that fits on the smallest page needs no pager at all.
        // data-pager-always keeps it, so the page-size choice is always there.
        this.bar.hidden = !this.table.hasAttribute("data-pager-always") && total <= this.sizes[0];

        this.pages.innerHTML = "";
        var btn = function (label, page, opts) {
            opts = opts || {};
            var b = el("button", "pager-btn" + (opts.cls ? " " + opts.cls : ""), label);
            b.type = "button";
            if (opts.aria) { b.setAttribute("aria-label", opts.aria); }
            if (!opts.nav && page === self.page) { b.classList.add("is-current"); b.setAttribute("aria-current", "page"); }
            if (opts.disabled) { b.disabled = true; }
            b.addEventListener("click", function () { self.go(page); });
            self.pages.appendChild(b);
        };

        btn("‹", this.page - 1, { nav: true, aria: "Previous page", cls: "nav", disabled: this.page <= 1 });
        windowed(this.page, pages).forEach(function (p) {
            if (p === "gap") { self.pages.appendChild(el("span", "pager-gap", "…")); }
            else { btn(String(p), p); }
        });
        btn("›", this.page + 1, { nav: true, aria: "Next page", cls: "nav", disabled: this.page >= pages });
    };

    // 1 … 4 5 6 … 12 — always both ends, and the neighbours of the current page.
    function windowed(current, last) {
        var out = [];
        if (last <= 7) {
            for (var i = 1; i <= last; i++) { out.push(i); }
            return out;
        }
        var lo = Math.max(2, current - 1);
        var hi = Math.min(last - 1, current + 1);
        if (current <= 3) { lo = 2; hi = 4; }
        if (current >= last - 2) { lo = last - 3; hi = last - 1; }
        out.push(1);
        if (lo > 2) { out.push("gap"); }
        for (var p = lo; p <= hi; p++) { out.push(p); }
        if (hi < last - 1) { out.push("gap"); }
        out.push(last);
        return out;
    }

    var pagers = new Map();
    document.querySelectorAll("table[data-paginate]").forEach(function (t) {
        if (t.tBodies.length) { pagers.set(t, new Pager(t)); }
    });

    window.Apilan = window.Apilan || {};
    window.Apilan.pagerFor = function (table) { return pagers.get(table); };
}());

/* ==========================================================================
   Live search with suggestions
   --------------------------------------------------------------------------
   <input data-live-search data-target="#table" data-suggest="/Events/Suggest">
   Typing filters the table straight away and asks the server for the
   closest matches, listed under the box. Arrow keys move through them,
   Enter opens the highlighted one — or, with none highlighted, submits the
   search like any form. Escape closes the list.
   ========================================================================== */
(function () {
    "use strict";

    function escapeHtml(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c];
        });
    }

    function highlight(text, term) {
        var safe = escapeHtml(text);
        var words = (term || "").trim().split(/\s+/).filter(Boolean).map(function (w) {
            return escapeHtml(w).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        });
        if (!words.length) { return safe; }
        return safe.replace(new RegExp("(" + words.join("|") + ")", "gi"), "<mark>$1</mark>");
    }

    document.querySelectorAll("[data-live-search]").forEach(function (input) {
        var form = input.form;
        var table = input.getAttribute("data-target") ? document.querySelector(input.getAttribute("data-target")) : null;
        var url = input.getAttribute("data-suggest");
        var list = document.createElement("ul");
        var listId = (input.id || "search") + "-suggest";
        var items = [];
        var active = -1;
        var timer = null;
        var seq = 0;

        list.className = "suggest";
        list.id = listId;
        list.setAttribute("role", "listbox");
        list.hidden = true;
        input.parentNode.appendChild(list);

        input.setAttribute("role", "combobox");
        input.setAttribute("aria-autocomplete", "list");
        input.setAttribute("aria-controls", listId);
        input.setAttribute("aria-expanded", "false");
        input.setAttribute("autocomplete", "off");

        function pager() { return table && window.Apilan.pagerFor ? window.Apilan.pagerFor(table) : null; }

        function close() {
            list.hidden = true;
            active = -1;
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
        }

        function paint(term) {
            list.innerHTML = "";
            if (!items.length) {
                if (!term) { close(); return; }
                var none = document.createElement("li");
                none.className = "suggest-none";
                none.textContent = "Nothing matches “" + term + "”. Press Enter to search anyway.";
                list.appendChild(none);
                list.hidden = false;
                input.setAttribute("aria-expanded", "true");
                return;
            }

            items.forEach(function (it, i) {
                var li = document.createElement("li");
                li.id = listId + "-" + i;
                li.setAttribute("role", "option");
                li.innerHTML =
                    "<a href=\"" + escapeHtml(it.url) + "\">" +
                    "<span class=\"sg-badge\" data-tone=\"" + escapeHtml(it.tone || "") + "\">" + escapeHtml(it.badge || "") + "</span>" +
                    "<span class=\"sg-main\"><span class=\"sg-title\">" + highlight(it.title, term) + "</span>" +
                    "<span class=\"sg-sub\">" + highlight(it.subtitle, term) + "</span></span>" +
                    (it.meta ? "<span class=\"sg-meta\">" + escapeHtml(it.meta) + "</span>" : "") +
                    "</a>";
                // Keep focus in the box so blur does not close the list mid-click.
                li.addEventListener("mousedown", function (e) { e.preventDefault(); });
                list.appendChild(li);
            });

            var foot = document.createElement("li");
            foot.className = "suggest-foot";
            foot.innerHTML = "<kbd>↑</kbd><kbd>↓</kbd> move <span>·</span> <kbd>Enter</kbd> open <span>·</span> <kbd>Esc</kbd> close";
            list.appendChild(foot);

            list.hidden = false;
            input.setAttribute("aria-expanded", "true");
        }

        function move(delta) {
            if (list.hidden || !items.length) { return; }
            active = (active + delta + items.length) % items.length;
            Array.prototype.forEach.call(list.querySelectorAll("[role=option]"), function (li, i) {
                li.classList.toggle("is-active", i === active);
                if (i === active) {
                    input.setAttribute("aria-activedescendant", li.id);
                    li.scrollIntoView({ block: "nearest" });
                }
            });
        }

        function fetchSuggestions(term) {
            if (!url) { return; }
            if (!term) { items = []; close(); return; }
            var mine = ++seq;
            fetch(url + (url.indexOf("?") >= 0 ? "&" : "?") + "term=" + encodeURIComponent(term), {
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            })
                .then(function (r) { return r.ok ? r.json() : []; })
                .then(function (data) {
                    if (mine !== seq || input.value.trim() !== term) { return; }
                    items = data || [];
                    active = -1;
                    paint(term);
                })
                .catch(function () { /* suggestions are a convenience; the form still works */ });
        }

        input.addEventListener("input", function () {
            var term = input.value.trim();
            var p = pager();
            if (p) { p.filter(term); }
            clearTimeout(timer);
            timer = setTimeout(function () { fetchSuggestions(term); }, 140);
        });

        input.addEventListener("keydown", function (e) {
            if (e.key === "ArrowDown") { e.preventDefault(); move(1); }
            else if (e.key === "ArrowUp") { e.preventDefault(); move(-1); }
            else if (e.key === "Escape") { close(); }
            else if (e.key === "Enter" && !list.hidden && active >= 0 && items[active]) {
                e.preventDefault();
                window.location.href = items[active].url;
            }
            // Any other Enter submits the form, as a search box should.
        });

        input.addEventListener("focus", function () {
            if (input.value.trim() && items.length) { paint(input.value.trim()); }
        });
        input.addEventListener("blur", function () { setTimeout(close, 120); });

        // A term that came back from the server keeps the table filtered to match.
        if (input.value.trim()) {
            var p = pager();
            if (p) { p.filter(input.value.trim()); }
        }

        if (form) { form.addEventListener("submit", close); }
    });
}());
