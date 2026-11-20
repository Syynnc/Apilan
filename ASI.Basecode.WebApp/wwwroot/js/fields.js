/* Apilan — fields that only take certain characters.

   data-mask="####-#####": only digits go in; the dash adds itself.
   data-allow="0-9+": anything outside the set is dropped as it is typed or
   pasted. Listens during capture so the live previews read the clean value.
   The server checks the same rules; this only stops the typo early. */
(function () {
    "use strict";

    function applyMask(value, mask) {
        var digits = value.replace(/\D/g, "");
        var out = "";
        for (var i = 0, d = 0; i < mask.length && d < digits.length; i++) {
            out += mask[i] === "#" ? digits[d++] : mask[i];
        }
        return out;
    }

    document.addEventListener("input", function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement)) { return; }
        var mask = el.getAttribute("data-mask");
        var allow = el.getAttribute("data-allow");
        if (!mask && !allow) { return; }
        // Let a backspace through a dash without the dash coming straight back.
        if (mask && e.inputType && e.inputType.indexOf("delete") === 0) { return; }

        var clean = mask ? applyMask(el.value, mask) : el.value.replace(new RegExp("[^" + allow + "]", "g"), "");
        if (clean !== el.value) { el.value = clean; }
    }, true);
}());
