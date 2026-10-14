/* Apilan — camera QR scanning at the door.

   A member's pass encodes their student number and nothing else, so a read
   from the camera fills the same field an officer would type into and submits
   the same form. The scanned and typed paths cannot drift apart.

   Decoding uses the browser's own BarcodeDetector where it can read QR codes
   (Chrome on Android, macOS and ChromeOS). Everywhere else — Chrome and Edge on
   Windows, Firefox, Safari — it falls back to jsQR, vendored under lib/jsqr,
   reading frames off a canvas. Either way nothing leaves the page.

   Recording reloads the page, so the camera is switched back on afterwards if
   it was running, and the last code is remembered for a few seconds so the
   pass still in front of the lens is not read a second time and timed out. */
(function () {
    "use strict";

    var CAN_STREAM = !!(navigator.mediaDevices && typeof navigator.mediaDevices.getUserMedia === "function");

    /* Two matching reads before we believe one. A blurry frame can decode to
       something plausible, and this screen writes attendance. */
    var CONFIRMATIONS = 2;

    /* Long enough to cover the reload and the student stepping away, short
       enough that scanning the same pass again on the way out still works. */
    var REPEAT_GUARD_MS = 8000;

    var SCAN_EVERY_MS = 180;
    var MAX_FRAME_WIDTH = 640;

    var KEY_CAMERA = "apilan-scan-camera";
    var KEY_LAST = "apilan-scan-last";

    function store(key, value) {
        try {
            if (value === null) { sessionStorage.removeItem(key); }
            else { sessionStorage.setItem(key, value); }
        } catch (e) { }
    }

    function read(key) {
        try { return sessionStorage.getItem(key); } catch (e) { return null; }
    }

    /* Resolves to a function frame -> Promise<string|null>, or null when the
       browser can decode nothing at all. */
    function makeDecoder() {
        var canvas = document.createElement("canvas");
        var ctx = canvas.getContext("2d", { willReadFrequently: true });

        var viaJsQr = function (video) {
            var w = video.videoWidth, h = video.videoHeight;
            if (!w || !h) { return Promise.resolve(null); }

            var scale = Math.min(1, MAX_FRAME_WIDTH / w);
            canvas.width = Math.round(w * scale);
            canvas.height = Math.round(h * scale);
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

            var image = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var hit = window.jsQR(image.data, image.width, image.height, { inversionAttempts: "dontInvert" });
            return Promise.resolve(hit ? hit.data : null);
        };

        var fallback = typeof window.jsQR === "function" ? viaJsQr : null;

        if (!("BarcodeDetector" in window)) {
            return Promise.resolve(fallback);
        }

        // The constructor exists on Windows Chrome too, it just finds nothing,
        // so ask what it can actually read before trusting it.
        return window.BarcodeDetector.getSupportedFormats().then(function (formats) {
            if (formats.indexOf("qr_code") < 0) { return fallback; }

            var detector = new window.BarcodeDetector({ formats: ["qr_code"] });
            return function (video) {
                return detector.detect(video).then(function (codes) {
                    return codes && codes.length ? codes[0].rawValue : null;
                });
            };
        }).catch(function () { return fallback; });
    }

    function attach(els) {
        if (!els.form || !els.field || !els.video || !els.start) {
            return;
        }

        function say(message, isError) {
            if (!els.note) { return; }
            els.note.hidden = !message;
            els.note.textContent = message || "";
            els.note.classList.toggle("is-bad", !!isError);
        }

        if (!CAN_STREAM || !window.isSecureContext) {
            say("The camera needs the https:// address of this site. A handheld scanner " +
                "or typing the student number still works.");
            return;
        }

        var decode = null;
        var stream = null;
        var timer = null;
        var busy = false;
        var lastSeen = "";
        var seenCount = 0;

        function recentlyRecorded(code) {
            var raw = read(KEY_LAST);
            if (!raw) { return false; }
            var parts = raw.split("|");
            return parts[1] === code && Date.now() - Number(parts[0]) < REPEAT_GUARD_MS;
        }

        function submit(code) {
            if (recentlyRecorded(code)) {
                say("Already recorded " + code + ". Next person, please.");
                return;
            }

            store(KEY_LAST, Date.now() + "|" + code);
            els.field.value = code;

            // Say how it was read, so the record is honest about the method.
            if (els.mode && els.qrModeId) {
                els.mode.value = els.qrModeId;
            }

            halt();
            say("Recording " + code + "…");
            els.form.submit();
        }

        function saw(code) {
            if (code !== lastSeen) {
                lastSeen = code;
                seenCount = 1;
                return;
            }

            seenCount += 1;
            if (seenCount >= CONFIRMATIONS) {
                submit(code);
            }
        }

        function tick() {
            if (!stream || busy || els.video.readyState < 2) { return; }

            busy = true;
            decode(els.video).then(function (value) {
                value = (value || "").trim();
                if (value) { saw(value); }
            }).catch(function () {
                // A dropped frame is not worth telling the officer about; the
                // next one is a fraction of a second away.
            }).then(function () { busy = false; });
        }

        function start() {
            if (stream) { return; }

            // The rear camera on a phone or tablet propped at the door; on a
            // laptop this falls back to whatever camera there is.
            navigator.mediaDevices.getUserMedia({
                video: { facingMode: "environment", width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            }).then(function (granted) {
                stream = granted;
                els.video.srcObject = stream;
                els.video.play();

                els.shell.hidden = false;
                els.start.hidden = true;
                store(KEY_CAMERA, "on");
                say("Hold the pass about a hand's width from the camera.");

                timer = setInterval(tick, SCAN_EVERY_MS);
            }).catch(function (err) {
                store(KEY_CAMERA, null);
                var denied = err && (err.name === "NotAllowedError" || err.name === "SecurityError");

                say(denied
                    ? "Camera access was refused. Allow it from the address bar, " +
                      "or keep typing student numbers into the field above."
                    : "No camera was available. Typing the student number still works.", true);
            });
        }

        /* Stops the stream without forgetting that the officer wants it on. */
        function halt() {
            if (timer) {
                clearInterval(timer);
                timer = null;
            }

            if (stream) {
                stream.getTracks().forEach(function (track) { track.stop(); });
                stream = null;
            }

            els.video.srcObject = null;
            els.shell.hidden = true;
            els.start.hidden = false;
            lastSeen = "";
            seenCount = 0;
        }

        makeDecoder().then(function (decoder) {
            if (!decoder) {
                say("This browser cannot read QR codes from the camera. A handheld " +
                    "scanner or typing the student number still works.");
                return;
            }

            decode = decoder;
            els.start.hidden = false;
            els.start.addEventListener("click", start);

            els.stop.addEventListener("click", function () {
                store(KEY_CAMERA, null);
                halt();
                say("");
                els.field.focus();
            });

            // Typing means the officer has taken over from the camera.
            els.field.addEventListener("input", function () {
                if (els.mode && els.manualModeId && els.mode.value === els.qrModeId) {
                    els.mode.value = els.manualModeId;
                }
            });

            // Leaving the page with the camera running keeps the light on.
            window.addEventListener("pagehide", halt);
            document.addEventListener("visibilitychange", function () {
                if (document.hidden) { halt(); }
                else if (read(KEY_CAMERA) === "on") { start(); }
            });

            // It was on before the last entry reloaded the page.
            if (read(KEY_CAMERA) === "on") {
                start();
            }
        });
    }

    window.ApilanScanner = { attach: attach };
})();
