/* Apilan — date boxes.
   <input type="text" data-date> always reads MM/DD/YYYY, whatever language
   the browser is in (a native date input follows the browser's locale, which
   showed dd/mm/yyyy). Slashes go in as you type, and the calendar button
   opens the browser's own picker through a hidden date input. */
(function () {
    "use strict";

    var ICON = "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><rect x=\"4\" y=\"5\" width=\"16\" height=\"15\" rx=\"2\" /><path d=\"M4 10h16M9 3v4M15 3v4\" /></svg>";

    function pad(n) { return (n < 10 ? "0" : "") + n; }

    // "10/25/2026" -> "2026-10-25", or "" when it is not a real day.
    function toIso(text) {
        var m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text || "");
        if (!m) { return ""; }
        var d = new Date(+m[3], +m[1] - 1, +m[2]);
        return d.getMonth() === +m[1] - 1 && d.getDate() === +m[2] ? m[3] + "-" + m[1] + "-" + m[2] : "";
    }

    function fromIso(iso) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || "");
        return m ? m[2] + "/" + m[3] + "/" + m[1] : "";
    }

    document.querySelectorAll("input[data-date]").forEach(function (input) {
        input.placeholder = "MM/DD/YYYY";
        input.inputMode = "numeric";
        input.maxLength = 10;
        input.autocomplete = "off";
        input.pattern = "\\d{2}/\\d{2}/\\d{4}";
        input.title = "MM/DD/YYYY";

        var wrap = document.createElement("div");
        wrap.className = "date-field";
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var native = document.createElement("input");
        native.type = "date";
        native.tabIndex = -1;
        native.className = "date-native";
        native.setAttribute("aria-hidden", "true");
        wrap.appendChild(native);

        var button = document.createElement("button");
        button.type = "button";
        button.className = "date-pick";
        button.setAttribute("aria-label", "Pick a date");
        button.innerHTML = ICON;
        wrap.appendChild(button);

        button.addEventListener("click", function () {
            native.value = toIso(input.value);
            if (native.showPicker) { native.showPicker(); } else { native.focus(); native.click(); }
        });

        native.addEventListener("change", function () {
            if (native.value) { input.value = fromIso(native.value); input.setCustomValidity(""); }
        });

        input.addEventListener("input", function (e) {
            if (e.inputType && e.inputType.indexOf("delete") === 0) { return; }
            var digits = input.value.replace(/\D/g, "").slice(0, 8);
            var out = digits.slice(0, 2);
            if (digits.length > 2) { out += "/" + digits.slice(2, 4); }
            if (digits.length > 4) { out += "/" + digits.slice(4); }
            if (digits.length === 2 || digits.length === 4) { out += "/"; }
            input.value = out;
            input.setCustomValidity("");
        });

        input.addEventListener("blur", function () {
            // "3/7/2026" is clear enough: pad it rather than reject it.
            var m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(input.value.trim());
            if (m) { input.value = pad(+m[1]) + "/" + pad(+m[2]) + "/" + m[3]; }
            input.setCustomValidity(input.value && !toIso(input.value) ? "Enter a real date as MM/DD/YYYY." : "");
        });
    });
}());
