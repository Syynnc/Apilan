/* Apilan — "Save as image" on the attendance pass. Draws the whole ticket,
   QR included, to a PNG with html-to-image and downloads it. html-to-image
   lets the browser render the ticket itself (through an SVG foreignObject), so
   it handles color-mix() and the rest of the stylesheet, which html2canvas
   cannot. The link's href is the server's QR-only PNG, kept as the fallback
   when the capture can't run. */
(function () {
    "use strict";

    function download(blob, name) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

    document.addEventListener("click", function (e) {
        var link = e.target.closest("[data-pass-save]");
        var lib = window.htmlToImage;
        if (!link || !lib) { return; }

        var ticket = link.closest(".pass-wrap").querySelector(".ticket");
        if (!ticket) { return; }

        e.preventDefault();
        if (link.getAttribute("aria-busy") === "true") { return; }
        link.setAttribute("aria-busy", "true");

        // Capture the ticket flat, not mid-tilt.
        var tilt = ticket.style.transform;
        ticket.style.transform = "none";

        var ready = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();

        ready.then(function () {
            return lib.toBlob(ticket, {
                pixelRatio: Math.max(2, window.devicePixelRatio || 1),
                cacheBust: true
            });
        }).then(function (blob) {
            if (!blob) { throw new Error("The ticket rendered to an empty image."); }
            download(blob, link.getAttribute("data-pass-save") || "apilan-pass.png");
        }).catch(function (err) {
            if (window.console) { console.error("Saving the pass as an image failed:", err); }
            if (window.Apilan && window.Apilan.toast) {
                window.Apilan.toast("bad", "Couldn't draw the ticket, so only the QR code was saved.", "Saved QR only");
            }
            window.location.href = link.href;
        }).then(function () {
            ticket.style.transform = tilt;
            link.removeAttribute("aria-busy");
        });
    });
}());
