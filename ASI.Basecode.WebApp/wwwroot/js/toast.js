/* Apilan — toasts.
   Notices across the top of the page. Each one slides in, a bar along its
   bottom runs down how long it has left, and it slides out when the bar is
   empty. Hovering or focusing a toast holds its timer; × closes it at once.

   The server renders the ones for the last action (_Toasts.cshtml); script
   can add more with Apilan.toast("ok" | "bad", "Message", "Title"). */
(function () {
    "use strict";

    var LIFE_MS = 5000;
    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var stack = document.getElementById("toasts");
    if (!stack) { return; }
    stack.setAttribute("data-js", "");

    function dismiss(toast) {
        if (toast.classList.contains("is-leaving")) { return; }
        toast.classList.add("is-leaving");
        var gone = false;
        function remove() {
            if (gone) { return; }
            gone = true;
            toast.remove();
        }
        if (REDUCED) { remove(); return; }
        toast.addEventListener("transitionend", function (e) {
            if (e.target === toast && e.propertyName === "transform") { remove(); }
        });
        setTimeout(remove, 400);
    }

    function start(toast) {
        var life = Number(toast.getAttribute("data-toast-life")) || LIFE_MS;
        toast.style.setProperty("--toast-life", life + "ms");

        var timer = toast.querySelector(".toast-timer");
        if (timer) {
            // The bar is the clock: when its run ends, the toast goes.
            timer.addEventListener("animationend", function () { dismiss(toast); });
        } else {
            setTimeout(function () { dismiss(toast); }, life);
        }

        // Slide in on the frame after it is drawn, so it moves rather than appears.
        requestAnimationFrame(function () {
            requestAnimationFrame(function () { toast.classList.add("is-in"); });
        });
    }

    stack.addEventListener("click", function (e) {
        var close = e.target.closest("[data-toast-close]");
        if (close) { dismiss(close.closest("[data-toast]")); }
    });

    // Esc closes the newest one, unless a dialog is open and wants Esc itself.
    document.addEventListener("keydown", function (e) {
        if (e.key !== "Escape" || document.querySelector("dialog[open]")) { return; }
        var toasts = stack.querySelectorAll("[data-toast]:not(.is-leaving)");
        if (toasts.length) { dismiss(toasts[toasts.length - 1]); }
    });

    stack.querySelectorAll("[data-toast]").forEach(start);

    function toast(kind, message, title) {
        var ok = kind !== "bad";
        var el = document.createElement("div");
        el.className = "toast " + (ok ? "ok" : "bad");
        el.setAttribute("role", ok ? "status" : "alert");
        el.setAttribute("data-toast", "");
        if (!ok) { el.setAttribute("data-toast-life", "9000"); }
        el.innerHTML =
            '<svg class="toast-icon" viewBox="0 0 24 24" aria-hidden="true">' +
                (ok ? '<circle cx="12" cy="12" r="9"/><path d="M8 12.5l2.8 2.8L16.5 9.5"/>'
                    : '<circle cx="12" cy="12" r="9"/><path d="M12 7.5v5.5M12 16.5v.01"/>') +
            '</svg><div class="toast-text"><strong></strong><span></span></div>' +
            '<button type="button" class="toast-close" data-toast-close aria-label="Close notification">' +
                '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18"/></svg></button>' +
            '<i class="toast-timer" aria-hidden="true"></i>';
        el.querySelector("strong").textContent = title || (ok ? "Done" : "Not done");
        el.querySelector(".toast-text span").textContent = message;
        stack.appendChild(el);
        start(el);
        return el;
    }

    window.Apilan = window.Apilan || {};
    window.Apilan.toast = toast;
})();
