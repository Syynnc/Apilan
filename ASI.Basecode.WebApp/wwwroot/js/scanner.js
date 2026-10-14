/* Apilan — camera QR scanning at the door.

   A member's pass encodes their student number and nothing else, so a read
   from the camera fills the same field an officer would type into and submits
   the same form. The scanned and typed paths cannot drift apart.

   Decoding uses the browser's own BarcodeDetector where it can read QR codes
   (Chrome on Android, macOS and ChromeOS). Everywhere else — Chrome and Edge on
   Windows, Firefox, Safari — it uses jsQR, vendored under lib/jsqr, on frames
   copied to a canvas. Each jsQR pass alternates between the whole frame and an
   enlarged centre crop, so a pass held far from a laptop webcam still reads,
   and both polarities are tried, so a phone in dark mode reads too.

   Recording reloads the page, so the camera is switched back on afterwards if
   it was running, and the last code is remembered for a few seconds so the
   pass still in front of the lens is not read again and timed out. */
(function () {
    "use strict";

    /* Long enough to cover the reload and the student stepping away, short
       enough that scanning the same pass again on the way out still works. */
    var REPEAT_GUARD_MS = 8000;

    /* Pause between decode attempts. jsQR can take 100ms+ on a busy frame. */
    var SCAN_GAP_MS = 60;

    /* Frames are scaled down to this before decoding a whole frame. */
    var FULL_FRAME_WIDTH = 800;

    /* The centre crop: this share of the frame, enlarged to at least this wide. */
    var CROP_SHARE = 0.55;
    var CROP_MIN_WIDTH = 700;

    var KEY_CAMERA = "apilan-scan-camera";
    var KEY_DEVICE = "apilan-scan-device";
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

    /* Resolves to { name, decode(video) -> Promise<string|null> }, or null
       when the browser can decode nothing at all. */
    function makeDecoder() {
        var canvas = document.createElement("canvas");
        var ctx = canvas.getContext("2d", { willReadFrequently: true });
        var useCrop = false;

        function jsQrRegion(video, sx, sy, sw, sh, outWidth) {
            var scale = outWidth / sw;
            canvas.width = Math.round(sw * scale);
            canvas.height = Math.round(sh * scale);
            ctx.imageSmoothingEnabled = scale < 1;
            ctx.drawImage(video, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);

            var image = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var hit = window.jsQR(image.data, image.width, image.height, { inversionAttempts: "attemptBoth" });
            return hit ? hit.data : null;
        }

        var jsQr = {
            name: "jsQR",
            decode: function (video) {
                var w = video.videoWidth, h = video.videoHeight;
                if (!w || !h) { return Promise.resolve(null); }

                // Alternate so neither strategy costs every frame its time.
                useCrop = !useCrop;
                if (useCrop) {
                    var cw = Math.round(w * CROP_SHARE), ch = Math.round(h * CROP_SHARE);
                    return Promise.resolve(jsQrRegion(video, (w - cw) / 2, (h - ch) / 2, cw, ch, Math.max(cw, CROP_MIN_WIDTH)));
                }
                return Promise.resolve(jsQrRegion(video, 0, 0, w, h, Math.min(w, FULL_FRAME_WIDTH)));
            }
        };

        var fallback = typeof window.jsQR === "function" ? jsQr : null;

        if (!("BarcodeDetector" in window)) {
            return Promise.resolve(fallback);
        }

        // Some builds expose the constructor without a QR backend, so ask what
        // it can actually read before trusting it.
        return window.BarcodeDetector.getSupportedFormats().then(function (formats) {
            if (formats.indexOf("qr_code") < 0) { return fallback; }

            var detector = new window.BarcodeDetector({ formats: ["qr_code"] });
            return {
                name: "BarcodeDetector",
                decode: function (video) {
                    return detector.detect(video).then(function (codes) {
                        return codes && codes.length ? codes[0].rawValue : null;
                    });
                }
            };
        }).catch(function () { return fallback; });
    }

    /* A short tick so the officer hears the read without looking up. */
    function beep(ok) {
        try {
            var Ctx = window.AudioContext || window.webkitAudioContext;
            var audio = new Ctx();
            var osc = audio.createOscillator();
            var gain = audio.createGain();
            osc.frequency.value = ok ? 1320 : 330;
            gain.gain.setValueAtTime(0.12, audio.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + 0.15);
            osc.connect(gain).connect(audio.destination);
            osc.start();
            osc.stop(audio.currentTime + 0.16);
        } catch (e) { }
    }

    function attach(els) {
        if (!els.form || !els.field || !els.video || !els.start) {
            return;
        }

        var stats = { decoder: "loading", attempts: 0, reads: 0, last: "", state: "idle" };
        var decoderReady = makeDecoder();
        var decoder = null;
        var stream = null;
        var running = false;
        var submitting = false;
        var statusAt = 0;

        function say(message, isError) {
            if (!els.note) { return; }
            els.note.hidden = !message;
            els.note.textContent = message || "";
            els.note.classList.toggle("is-bad", !!isError);
        }

        /* The line over the video: what the scanner is doing right now. */
        function status(text, tone) {
            statusAt = Date.now();
            stats.state = tone || "scanning";
            if (els.status) { els.status.textContent = text; }
            if (els.view) { els.view.setAttribute("data-state", stats.state); }
        }

        function recentlyRecorded(code) {
            var raw = read(KEY_LAST);
            if (!raw) { return false; }
            var cut = raw.indexOf("|");
            return raw.slice(cut + 1) === code && Date.now() - Number(raw.slice(0, cut)) < REPEAT_GUARD_MS;
        }

        function found(code) {
            stats.reads += 1;
            stats.last = code;

            // A pass holds a student number. A link or anything long is some
            // other QR code — the landing page, a poster — not a person.
            if (code.length > 20 || /\s|:\/\//.test(code)) {
                status("That QR code is not a member pass.", "bad");
                return false;
            }

            if (recentlyRecorded(code)) {
                status(code + " recorded. Next person, please.", "hold");
                return false;
            }

            store(KEY_LAST, Date.now() + "|" + code);
            submitting = true;
            els.field.value = code;

            // Say how it was read, so the record is honest about the method.
            if (els.mode && els.qrModeId) {
                els.mode.value = els.qrModeId;
            }

            status("Read " + code + " — recording…", "ok");
            beep(true);

            // Keep the frame on screen while the page reloads; the track is
            // released by pagehide.
            running = false;
            els.form.submit();
            return true;
        }

        function loop() {
            if (!running) { return; }

            if (els.video.readyState < 2 || !els.video.videoWidth) {
                setTimeout(loop, 100);
                return;
            }

            stats.attempts += 1;
            decoder.decode(els.video).then(function (value) {
                value = (value || "").trim();
                if (value && found(value)) { return; }

                // Once the pass that caused a warning is out of view, go back
                // to the plain prompt instead of leaving the warning up.
                if (!value && stats.state !== "scanning" && Date.now() - statusAt > 1500) {
                    status("Looking for a QR code…", "scanning");
                }
                if (running) { setTimeout(loop, SCAN_GAP_MS); }
            }, function () {
                // A dropped frame is not worth telling the officer about.
                if (running) { setTimeout(loop, SCAN_GAP_MS); }
            });
        }

        function constraints() {
            var device = read(KEY_DEVICE);
            var video = { width: { ideal: 1280 }, height: { ideal: 720 } };
            if (device) { video.deviceId = { exact: device }; }
            else { video.facingMode = { ideal: "environment" }; }
            return { video: video, audio: false };
        }

        /* Offer a choice only when there is one: laptop plus a USB camera, or
           a phone's front and back. Labels exist only after permission. */
        function listCameras() {
            if (!els.cameras || !navigator.mediaDevices.enumerateDevices) { return; }

            navigator.mediaDevices.enumerateDevices().then(function (devices) {
                var cams = devices.filter(function (d) { return d.kind === "videoinput"; });
                if (cams.length < 2) { els.cameras.hidden = true; return; }

                var current = stream && stream.getVideoTracks()[0].getSettings().deviceId;
                els.cameras.innerHTML = "";
                cams.forEach(function (cam, i) {
                    var opt = document.createElement("option");
                    opt.value = cam.deviceId;
                    opt.textContent = cam.label || ("Camera " + (i + 1));
                    opt.selected = cam.deviceId === current;
                    els.cameras.appendChild(opt);
                });
                els.cameras.hidden = false;
            });
        }

        function start() {
            if (stream || submitting) { return; }

            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.isSecureContext) {
                say("The camera only works on the https:// address of this site (or on localhost). " +
                    "A handheld scanner or typing the student number still works.", true);
                return;
            }

            els.start.disabled = true;
            els.start.textContent = "Starting the camera…";

            decoderReady.then(function (d) {
                if (!d) {
                    throw { name: "NoDecoder" };
                }
                decoder = d;
                stats.decoder = d.name;
                return navigator.mediaDevices.getUserMedia(constraints()).catch(function (err) {
                    // A remembered camera that has since been unplugged.
                    if (err && err.name === "OverconstrainedError" && read(KEY_DEVICE)) {
                        store(KEY_DEVICE, null);
                        return navigator.mediaDevices.getUserMedia(constraints());
                    }
                    throw err;
                });
            }).then(function (granted) {
                stream = granted;
                els.video.srcObject = stream;

                // A laptop's own camera faces the officer, so show it the way a
                // mirror would. Decoding reads the unmirrored frame regardless.
                var facing = stream.getVideoTracks()[0].getSettings().facingMode;
                els.view.classList.toggle("is-mirrored", facing !== "environment");

                els.shell.hidden = false;
                els.start.hidden = true;
                say("");
                status("Looking for a QR code…", "scanning");
                store(KEY_CAMERA, "on");
                listCameras();

                return els.video.play();
            }).then(function () {
                running = true;
                loop();
            }).catch(function (err) {
                halt();
                store(KEY_CAMERA, null);

                var name = err && err.name;
                say(name === "NoDecoder"
                        ? "This browser cannot read QR codes. A handheld scanner or typing the student number still works."
                    : name === "NotAllowedError" || name === "SecurityError"
                        ? "Camera access was blocked. Allow it from the camera icon in the address bar, then try again."
                    : name === "NotReadableError"
                        ? "The camera is in use by another app (Zoom, Teams, the Camera app…). Close it and try again."
                    : name === "NotFoundError" || name === "OverconstrainedError"
                        ? "No camera was found. Typing the student number still works."
                        : "The camera could not start" + (name ? " (" + name + ")" : "") + ". Typing the student number still works.",
                    true);
            }).then(function () {
                els.start.disabled = false;
                els.start.textContent = "Scan with the camera";
            });
        }

        /* Stops the stream without forgetting that the officer wants it on. */
        function halt() {
            running = false;

            if (stream) {
                stream.getTracks().forEach(function (track) { track.stop(); });
                stream = null;
            }

            els.video.srcObject = null;
            els.shell.hidden = true;
            els.start.hidden = false;
            status("", "idle");
        }

        els.start.addEventListener("click", start);

        els.stop.addEventListener("click", function () {
            store(KEY_CAMERA, null);
            halt();
            els.field.focus();
        });

        if (els.cameras) {
            els.cameras.addEventListener("change", function () {
                store(KEY_DEVICE, els.cameras.value);
                halt();
                start();
            });
        }

        // Typing means the officer has taken over from the camera.
        els.field.addEventListener("input", function () {
            if (els.mode && els.manualModeId && els.mode.value === String(els.qrModeId)) {
                els.mode.value = els.manualModeId;
            }
        });

        // Leaving the page with the camera running keeps the light on.
        window.addEventListener("pagehide", function () {
            running = false;
            if (stream) { stream.getTracks().forEach(function (track) { track.stop(); }); }
        });
        document.addEventListener("visibilitychange", function () {
            if (document.hidden) {
                if (stream) { halt(); }
            } else if (read(KEY_CAMERA) === "on" && !submitting) {
                start();
            }
        });

        // It was on before the last entry reloaded the page.
        if (read(KEY_CAMERA) === "on") {
            start();
        }

        // For checking a misbehaving door from the console: ApilanScanner.stats()
        window.ApilanScanner.stats = function () { return JSON.parse(JSON.stringify(stats)); };
    }

    window.ApilanScanner = { attach: attach };
})();
