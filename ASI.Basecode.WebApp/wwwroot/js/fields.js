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

/* ---- Taken already? ---------------------------------------------------- */
/* data-check-url on a field asks the server, when the field is left, whether
   its value already belongs to someone (a student number, say). The answer
   shows under the field the way a validation message does. Only a value that
   fits the field's pattern or mask is asked about. The server checks again
   on save, so this only brings the news early. */
(function () {
    "use strict";

    function message(field, text) {
        var span = field.form && field.form.querySelector("[data-valmsg-for='" + field.name + "']");
        field.classList.toggle("input-validation-error", !!text);
        if (!span) { return; }
        if (text) {
            span.textContent = text;
            span.classList.remove("field-validation-valid");
            span.classList.add("field-validation-error");
            span.setAttribute("data-taken", "");
        } else if (span.hasAttribute("data-taken")) {
            span.textContent = "";
            span.classList.remove("field-validation-error");
            span.classList.add("field-validation-valid");
            span.removeAttribute("data-taken");
        }
    }

    document.addEventListener("change", function (e) {
        var field = e.target;
        if (!(field instanceof HTMLInputElement) || !field.hasAttribute("data-check-url")) { return; }
        var value = field.value.trim();
        var mask = field.getAttribute("data-mask");
        if (!value || (mask && value.length !== mask.length)) { message(field, null); return; }

        var url = field.getAttribute("data-check-url");
        url += (url.indexOf("?") < 0 ? "?" : "&") + encodeURIComponent(field.name) + "=" + encodeURIComponent(value);
        fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                // Ignore a late answer for a value that has since changed.
                if (!data || field.value.trim() !== value) { return; }
                message(field, data.taken ? data.message : null);
            })
            .catch(function () { /* offline: the save checks anyway */ });
    });
}());
