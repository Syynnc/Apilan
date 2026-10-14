/* Apilan — searchable pick-or-type fields (the member form's course).
   The field is an ordinary text input, so anything typed is what gets saved.
   Under it, a list of known values narrows as you type, matching the code or
   the full name ("nurs" finds BSN). Click or Enter takes a suggestion; arrow
   keys move through them; Esc or leaving the field closes the list. Wired by
   delegation, so it also works in forms the modal loads later. */
(function () {
    "use strict";

    function parts(input) {
        var combo = input.closest("[data-combo]");
        return { combo: combo, list: combo && combo.querySelector("[data-combo-list]") };
    }

    function norm(s) { return (s || "").toLowerCase().replace(/\s+/g, " ").trim(); }

    function options(list) { return Array.prototype.slice.call(list.querySelectorAll("[role=option]")); }
    function visible(list) { return options(list).filter(function (o) { return !o.hidden; }); }

    function show(input, list, open) {
        list.hidden = !open;
        input.setAttribute("aria-expanded", String(open));
        if (!open) { mark(input, list, null); }
    }

    function mark(input, list, option) {
        options(list).forEach(function (o) { o.classList.toggle("is-active", o === option); o.setAttribute("aria-selected", String(o === option)); });
        if (option) {
            if (!option.id) { option.id = list.id + "-" + options(list).indexOf(option); }
            input.setAttribute("aria-activedescendant", option.id);
            option.scrollIntoView({ block: "nearest" });
        } else {
            input.removeAttribute("aria-activedescendant");
        }
    }

    function filter(input, list) {
        var term = norm(input.value);
        var any = false;
        options(list).forEach(function (o) {
            var text = norm(o.getAttribute("data-search"));
            var hit = !term || text.indexOf(term) >= 0;
            o.hidden = !hit;
            any = any || hit;
        });
        list.classList.toggle("is-empty", !any);
        return any;
    }

    function choose(input, list, option) {
        input.value = option.getAttribute("data-value");
        // Let validation and the ticket preview see the new value, without
        // the list taking that echo as more typing and opening again.
        input._choosing = true;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
        input._choosing = false;
        show(input, list, false);
    }

    document.addEventListener("focusin", function (e) {
        var input = e.target.closest && e.target.closest("[data-combo-input]");
        if (!input) { return; }
        var p = parts(input);
        filter(input, p.list);
        show(input, p.list, true);
    });

    document.addEventListener("input", function (e) {
        var input = e.target.closest && e.target.closest("[data-combo-input]");
        if (!input || input._choosing) { return; }
        var p = parts(input);
        show(input, p.list, filter(input, p.list));
    });

    document.addEventListener("keydown", function (e) {
        var input = e.target.closest && e.target.closest("[data-combo-input]");
        if (!input) { return; }
        var list = parts(input).list;
        var shown = visible(list);
        var current = list.querySelector(".is-active");
        var i = shown.indexOf(current);

        if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            if (list.hidden) { filter(input, list); show(input, list, true); shown = visible(list); }
            if (!shown.length) { return; }
            i = e.key === "ArrowDown" ? (i + 1) % shown.length : (i <= 0 ? shown.length - 1 : i - 1);
            mark(input, list, shown[i]);
        } else if (e.key === "Enter" && !list.hidden && current) {
            e.preventDefault();
            choose(input, list, current);
        } else if (e.key === "Escape" && !list.hidden) {
            e.preventDefault();
            e.stopPropagation();
            show(input, list, false);
        } else if (e.key === "Tab") {
            show(input, list, false);
        }
    }, true);

    // mousedown, not click: it lands before the input loses focus.
    document.addEventListener("mousedown", function (e) {
        var option = e.target.closest("[data-combo-list] [role=option]");
        if (option) {
            e.preventDefault();
            var input = option.closest("[data-combo]").querySelector("[data-combo-input]");
            choose(input, parts(input).list, option);
            return;
        }
        var caret = e.target.closest("[data-combo] .combo-caret");
        if (caret) {
            e.preventDefault();
            caret.closest("[data-combo]").querySelector("[data-combo-input]").focus();
        }
    });

    document.addEventListener("focusout", function (e) {
        var input = e.target.closest && e.target.closest("[data-combo-input]");
        if (input) { show(input, parts(input).list, false); }
    });
})();
