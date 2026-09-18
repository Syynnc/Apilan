/* Apilan — camera QR scanning at the door.

   Uses the browser's own BarcodeDetector rather than shipping a decoder: it is
   hardware-accelerated, it is already on the Chrome and Edge builds a campus
   lab runs, and it keeps a third-party library out of a screen that handles
   student records.

   Where the API is missing (Firefox, Safari as of writing) the button never
   appears and the screen stays exactly what it already was: a text field a
   hardware wedge scanner or a pair of hands can both drive. The camera is an
   accelerator here, never the only way in. */
(function () {
    "use strict";

    var SUPPORTED = "BarcodeDetector" in window &&
                    navigator.mediaDevices &&
                    typeof navigator.mediaDevices.getUserMedia === "function";

    /* Two reads of the same code before we believe it. A blurry frame can
       decode to something plausible, and this screen writes attendance. */
    var CONFIRMATIONS = 2;

    /* Long enough that a second person stepping up is a new check-in, short
       enough that re-scanning the same stub to time somebody out still works. */
    var REPEAT_GUARD_MS = 2500;

    function attach(els) {
        if (!els.form || !els.field || !els.video) {
            return;
        }

        if (!SUPPORTED) {
            if (els.note) {
                els.note.hidden = false;
                els.note.textContent =
                    "This browser cannot use the camera for scanning. A handheld scanner " +
                    "typed into the field above works, and so does typing the code.";
            }
            return;
        }

        var detector = new window.BarcodeDetector({ formats: ["qr_code"] });
        var stream = null;
        var timer = null;

        var lastSeen = "";
        var seenCount = 0;
        var lastSubmitted = "";
        var lastSubmittedAt = 0;

        els.start.hidden = false;

        function say(message, isError) {
            if (!els.note) {
                return;
            }

            els.note.hidden = !message;
            els.note.textContent = message || "";
            els.note.classList.toggle("is-bad", !!isError);
        }

        function submit(code) {
            var now = Date.now();

            // The camera sees the same stub 30 times a second; only the first
            // read should turn into a request.
            if (code === lastSubmitted && now - lastSubmittedAt < REPEAT_GUARD_MS) {
                return;
            }

            lastSubmitted = code;
            lastSubmittedAt = now;

            els.field.value = code;

            // Say how it was read, so the record is honest about the method.
            if (els.method) {
                els.method.value = "QrCode";
            }

            stop();
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
            if (!stream) {
                return;
            }

            detector.detect(els.video).then(function (codes) {
                if (codes && codes.length) {
                    var value = (codes[0].rawValue || "").trim();
                    if (value) {
                        saw(value);
                    }
                }
            }).catch(function () {
                // A dropped frame is not worth telling the officer about; the
                // next one is 200ms away.
            });
        }

        function start() {
            // The rear camera on a phone or tablet propped at the door; on a
            // laptop this falls back to whatever camera there is.
            navigator.mediaDevices.getUserMedia({
                video: { facingMode: "environment" }
            }).then(function (granted) {
                stream = granted;
                els.video.srcObject = stream;
                els.video.play();

                els.shell.hidden = false;
                els.start.hidden = true;
                say("Hold the stub about a hand's width from the lens.");

                timer = setInterval(tick, 200);
            }).catch(function (err) {
                var denied = err && (err.name === "NotAllowedError" || err.name === "SecurityError");

                say(denied
                    ? "Camera access was refused. Allow it in the browser's address bar, " +
                      "or keep typing codes into the field above."
                    : "No camera was available. Typing the code still works.", true);
            });
        }

        function stop() {
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

        els.start.addEventListener("click", start);
        els.stop.addEventListener("click", function () {
            stop();
            say("");
            els.field.focus();
        });

        // Typing means the officer has taken over; the hidden method field
        // must stop claiming the code was scanned.
        els.field.addEventListener("input", function () {
            if (els.method) {
                els.method.value = "ManualSearch";
            }
        });

        // Leaving the page with the camera running keeps the light on.
        window.addEventListener("pagehide", stop);
        document.addEventListener("visibilitychange", function () {
            if (document.hidden) {
                stop();
            }
        });
    }

    window.ApilanScanner = { attach: attach, supported: SUPPORTED };
})();
