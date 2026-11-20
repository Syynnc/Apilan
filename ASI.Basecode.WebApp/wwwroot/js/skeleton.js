/* Apilan — skeletons between pages.
   The app renders on the server, so moving between pages waits on the next
   response. Rather than leave the old page frozen (or a blank one), the
   content area is swapped for a page-shaped outline the moment a link or a
   form is followed, and stays until the next page arrives. */
(function () {
    "use strict";

    var host = document.querySelector("[data-skeleton-host]");
    if (!host) { return; }

    // Only show it if the wait is long enough to notice.
    var DELAY_MS = 90;
    var timer = null;

    function bar(cls) { return '<span class="sk ' + (cls || "") + '"></span>'; }
    function repeat(n, html) { var s = ""; for (var i = 0; i < n; i++) { s += html; } return s; }

    var head =
        '<div class="sk-head">' +
            '<div class="sk-stack">' + bar("sk-eyebrow") + bar("sk-title") + '</div>' +
            '<div class="sk-row">' + bar("sk-btn") + bar("sk-btn") + '</div>' +
        '</div>';
    var tiles = '<div class="sk-tiles">' + repeat(4, '<div class="sk-card">' + bar("sk-line w40") + bar("sk-num") + '</div>') + '</div>';
    var table =
        '<div class="sk-card sk-table">' +
            '<div class="sk-row sk-filters">' + bar("sk-input") + bar("sk-chip") + bar("sk-chip") + bar("sk-chip") + '</div>' +
            repeat(8, '<div class="sk-tr">' + bar("sk-av") + bar("sk-line w30") + bar("sk-line w20") + bar("sk-line w15") + bar("sk-chip") + '</div>') +
        '</div>';
    var detail =
        '<div class="sk-split">' +
            '<div class="sk-card sk-stack">' + bar("sk-line w60") + repeat(6, bar("sk-line")) + '</div>' +
            '<div class="sk-card sk-stack">' + bar("sk-line w40") + repeat(4, bar("sk-line w80")) + '</div>' +
        '</div>';
    var member =
        '<div class="sk-split">' +
            '<div class="sk-stack">' + bar("sk-title") + bar("sk-line w60") + tiles + '</div>' +
            '<div class="sk-card sk-pass"></div>' +
        '</div>' + table;

    function shapeFor(url) {
        var p = url.pathname.toLowerCase();
        if (host.hasAttribute("data-skeleton-member")) { return p.indexOf("/account") === 0 ? head + detail : member; }
        if (/\/details\//.test(p)) { return head + detail; }
        if (p === "/" || p.indexOf("/dashboard") === 0 || p.indexOf("/participation") === 0) { return head + tiles + detail; }
        return head + table;
    }

    function show(url) {
        clearTimeout(timer);
        timer = setTimeout(function () {
            host.setAttribute("aria-busy", "true");
            host.classList.add("is-skeleton");
            var sk = document.createElement("div");
            sk.className = "sk-page";
            sk.setAttribute("aria-hidden", "true");
            sk.innerHTML = shapeFor(url);
            host.appendChild(sk);
        }, DELAY_MS);
    }

    function reset() {
        clearTimeout(timer);
        host.classList.remove("is-skeleton");
        host.removeAttribute("aria-busy");
        host.querySelectorAll(".sk-page").forEach(function (n) { n.remove(); });
    }

    function sameDocument(url) {
        return url.origin === location.origin
            && url.pathname === location.pathname
            && url.search === location.search;
    }

    function skip(url) {
        if (url.origin !== location.origin) { return true; }
        if (url.hash && sameDocument(url)) { return true; }
        // Downloads stay on the page.
        return /\/export\b|\.csv$/i.test(url.pathname);
    }

    document.addEventListener("click", function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) { return; }
        var a = e.target.closest && e.target.closest("a[href]");
        if (!a || a.hasAttribute("data-modal") || a.hasAttribute("download") || a.hasAttribute("data-no-skeleton")) { return; }
        if (a.target && a.target !== "_self") { return; }
        var url = new URL(a.href, location.href);
        if (skip(url)) { return; }
        show(url);
    });

    /* Filters and other plain forms reload the page too. Late, so any
       handler that takes the submit over (modal forms, confirms) goes first. */
    document.addEventListener("submit", function (e) {
        var form = e.target;
        setTimeout(function () {
            if (e.defaultPrevented || form.hasAttribute("data-modal-form") || form.hasAttribute("data-no-skeleton")) { return; }
            if (form.closest("dialog")) { return; }
            var url = new URL(form.action || location.href, location.href);
            if (skip(url)) { return; }
            show(url);
        }, 0);
    });

    // Back/forward restores the page from cache with the skeleton still on.
    window.addEventListener("pageshow", reset);
}());
