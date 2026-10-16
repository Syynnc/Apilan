/* Apilan — "Save as image" on the attendance pass. Draws the whole ticket,
   QR included, to a PNG with html2canvas and downloads it. The link's href is
   the server's QR-only PNG, kept as the fallback when the capture can't run. */
(function () {
    "use strict";

    document.addEventListener("click", function (e) {
        var link = e.target.closest("[data-pass-save]");
        if (!link || typeof window.html2canvas !== "function") { return; }

        var ticket = link.closest(".pass-wrap").querySelector(".ticket");
        if (!ticket) { return; }

        e.preventDefault();
        if (link.getAttribute("aria-busy") === "true") { return; }
        link.setAttribute("aria-busy", "true");

        var fallback = function () { window.location.href = link.href; };
        var done = function () { link.removeAttribute("aria-busy"); };

        // Wait for web fonts so the name and number render in the right face.
        var ready = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();

        ready.then(function () {
            return window.html2canvas(ticket, {
                scale: Math.max(2, window.devicePixelRatio || 1),
                backgroundColor: null,
                useCORS: true,
                logging: false,
                // Capture the ticket flat, not mid-tilt.
                onclone: function (doc) {
                    var copy = doc.querySelector(".pass-wrap .ticket");
                    if (copy) { copy.style.transform = "none"; }
                }
            });
        }).then(function (canvas) {
            canvas.toBlob(function (blob) {
                if (!blob) { fallback(); return; }
                var url = URL.createObjectURL(blob);
                var a = document.createElement("a");
                a.href = url;
                a.download = link.getAttribute("data-pass-save") || "apilan-pass.png";
                document.body.appendChild(a);
                a.click();
                a.remove();
                setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
            }, "image/png");
        }).catch(fallback).then(done, done);
    });
}());
