/* Apilan — the door station on an event page.

   The mode switch is how a person gets recorded, and it is also what the record
   says about it: QR Code shows the camera, Manual shows the entry field.
   Only the chosen way's controls
   are on screen, and the choice survives the reload after each entry. */
(function () {
    "use strict";

    var KEY_MODE = "apilan-door-mode";

    /* A handheld scanner "types" a whole code in well under this per key. */
    var WEDGE_GAP_MS = 60;

    var form = document.getElementById("scan-form");
    if (!form) { return; }

    var field = document.getElementById("scan-field");
    var label = document.getElementById("scan-label");
    var modes = Array.prototype.slice.call(form.querySelectorAll("input[name=AttendanceModeId]"));
    var panes = {
        camera: form.querySelector("[data-pane=camera]"),
        keyed: form.querySelector("[data-pane=keyed]")
    };

    // Only where there is a real keyboard: on a phone, focusing pops the
    // on-screen keyboard over everything.
    var keyboardFirst = window.matchMedia("(pointer: fine)").matches;

    function store(value) {
        try { sessionStorage.setItem(KEY_MODE, value); } catch (e) { }
    }

    function remembered() {
        try { return sessionStorage.getItem(KEY_MODE); } catch (e) { return null; }
    }

    function current() {
        return modes.filter(function (m) { return m.checked; })[0] || modes[0];
    }

    function kind() {
        return current() ? current().getAttribute("data-kind") : "keyed";
    }

    // Before the scanner attaches: it restarts the camera only in QR mode.
    var saved = remembered();
    modes.forEach(function (m) { if (saved && m.value === saved) { m.checked = true; } });

    var scanner = window.ApilanScanner.attach({
        enabled: function () { return kind() === "camera"; },
        form: form,
        field: field,
        video: document.getElementById("scan-video"),
        shell: document.getElementById("scan-cam"),
        view: document.getElementById("scan-view"),
        status: document.getElementById("scan-status"),
        cameras: document.getElementById("scan-camera"),
        start: document.getElementById("scan-start"),
        idle: document.getElementById("scan-start-wrap"),
        stop: document.getElementById("scan-stop"),
        note: document.getElementById("scan-cam-note")
    });

    /* Shows the chosen way and hides the other. `fromClick` is a real choice
       by the officer, which may start the camera; a restore after reload only
       brings it back if it was running. */
    function apply(fromClick) {
        var mode = current();
        var k = kind();

        panes.camera.hidden = k !== "camera";
        panes.keyed.hidden = k !== "keyed";
        field.required = k === "keyed";

        if (k === "keyed") {
            if (scanner) { scanner.stop(); }
            label.textContent = mode.getAttribute("data-label");
            field.placeholder = mode.getAttribute("data-placeholder");
            if (keyboardFirst || fromClick) { field.focus(); }
        } else if (fromClick && scanner) {
            scanner.start();
        }
    }

    // "click", not "change": choosing QR Code again while it is already on is
    // still a request for the camera. Arrow keys fire it too.
    modes.forEach(function (m) {
        m.addEventListener("click", function () {
            store(m.value);
            apply(true);
        });
    });
    apply(false);

    // Keep the entry field ready between people, unless the officer clicked
    // something that wants the focus itself.
    document.addEventListener("click", function (e) {
        if (!keyboardFirst || kind() !== "keyed") { return; }
        if (e.target.closest("form, a, button, input, select, textarea, label, video, dialog")) { return; }
        field.focus();
    });

    /* In QR mode the field is out of sight, but a handheld QR scanner still
       types into the page. Collect a fast burst of keys ending in Enter and
       record it, just as the camera would. */
    var burst = "";
    var lastKey = 0;
    document.addEventListener("keydown", function (e) {
        if (kind() !== "camera" || e.ctrlKey || e.metaKey || e.altKey) { return; }
        // The mode and action switches are radios, which keep the focus after
        // a click; keys there still belong to the scanner.
        if (e.target.closest("input:not([type=radio]), select, textarea, [contenteditable], dialog")) { return; }

        var now = Date.now();
        if (now - lastKey > WEDGE_GAP_MS * 4) { burst = ""; }
        lastKey = now;

        if (e.key === "Enter") {
            // Never let Enter submit the form empty.
            e.preventDefault();
            var code = burst.trim();
            burst = "";
            if (code.length >= 4) {
                field.value = code;
                form.submit();
            }
            return;
        }
        if (e.key.length === 1) { burst += e.key; }
    });

    /* The open window counts down in the header and fills its bar, so the
       officer sees how long is left. The page reloads itself when it closes. */
    var left = document.querySelector("[data-window-left]");
    var bar = document.querySelector("[data-window-bar]");
    if (left && bar) {
        var opens = new Date(bar.getAttribute("data-opens")).getTime();
        var closes = new Date(left.getAttribute("data-closes")).getTime();
        var closesText = left.textContent.trim().replace(/^closes\s*/, "");
        var fill = bar.querySelector("i");

        var tick = function () {
            var now = Date.now();
            var mins = Math.max(0, Math.ceil((closes - now) / 60000));
            left.textContent = (mins <= 1 ? "under a minute" : mins + " min") + " left · closes " + closesText;
            fill.style.width = Math.min(100, Math.max(0, (now - opens) / (closes - opens) * 100)) + "%";
            bar.classList.toggle("is-ending", mins <= 3);
        };
        tick();
        setInterval(tick, 15000);
    }
})();
