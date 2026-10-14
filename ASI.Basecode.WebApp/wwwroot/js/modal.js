/* Apilan — modals.
   A link with data-modal opens its page's form in a <dialog> instead of
   navigating: the server returns just the form (_Form.cshtml) when asked
   with X-Requested-With. Submitting posts in place; the server answers 422
   with the form and its errors, or JSON naming where to go next. Without
   JavaScript the same links simply open the form as a page.

   Also here: <button data-dialog-open="#id"> for dialogs already in the page,
   and the live preview that fills a ticket's stub as its form is typed in. */
(function () {
    "use strict";

    var REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var shell = document.getElementById("app-modal");
    var lastTrigger = null;

    /* ---- Opening and closing --------------------------------------------- */
    function open(dialog, trigger) {
        lastTrigger = trigger || document.activeElement;
        dialog.classList.remove("is-closing");
        if (!dialog.open) { dialog.showModal(); }
        var first = dialog.querySelector("[autofocus], input:not([type=hidden]):not([disabled]), select, textarea, button");
        if (first) { first.focus(); }
    }

    function close(dialog) {
        if (!dialog.open) { return; }
        if (REDUCED) { dialog.close(); return; }
        dialog.classList.add("is-closing");
        setTimeout(function () {
            dialog.classList.remove("is-closing");
            dialog.close();
        }, 180);
    }

    document.addEventListener("click", function (e) {
        var closer = e.target.closest("[data-modal-close]");
        if (closer && closer.closest("dialog")) {
            e.preventDefault();
            close(closer.closest("dialog"));
            return;
        }

        var opener = e.target.closest("[data-dialog-open]");
        if (opener) {
            var target = document.querySelector(opener.getAttribute("data-dialog-open"));
            if (target) {
                e.preventDefault();
                if (window.Apilan.onDialogOpen) { window.Apilan.onDialogOpen(target, opener); }
                open(target, opener);
            }
            return;
        }

        var link = e.target.closest("a[data-modal]");
        if (link && shell && !e.ctrlKey && !e.metaKey && !e.shiftKey && e.button === 0) {
            e.preventDefault();
            load(link.href, link);
        }
    });

    // A click on the backdrop (the dialog element itself, outside its content) closes it.
    document.addEventListener("mousedown", function (e) {
        if (e.target.tagName === "DIALOG" && e.target.open) { close(e.target); }
    });

    // Esc animates out instead of snapping shut.
    document.addEventListener("cancel", function (e) {
        if (e.target.tagName === "DIALOG") {
            e.preventDefault();
            close(e.target);
        }
    }, true);

    document.addEventListener("close", function (e) {
        if (e.target.tagName === "DIALOG" && lastTrigger && document.contains(lastTrigger)) {
            lastTrigger.focus();
        }
    }, true);

    /* ---- Loading a form into the shared modal ---------------------------- */
    function load(url, trigger) {
        shell.classList.add("is-loading");
        fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" })
            .then(function (r) {
                if (!r.ok) { throw new Error(r.status); }
                return r.text();
            })
            .then(function (html) {
                shell.querySelector(".modal-content").innerHTML = html;
                wire(shell);
                open(shell, trigger);
            })
            .catch(function () { window.location.href = url; })
            .then(function () { shell.classList.remove("is-loading"); });
    }

    function wire(root) {
        var form = root.querySelector("form[data-modal-form]");
        if (!form) { return; }

        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            window.jQuery.validator.unobtrusive.parse(form);
        }

        preview(form);

        form.addEventListener("submit", function (e) {
            if (window.jQuery && window.jQuery(form).valid && !window.jQuery(form).valid()) {
                e.preventDefault();
                shake(form);
                return;
            }
            e.preventDefault();

            var submit = form.querySelector("[type=submit]");
            if (submit) { submit.disabled = true; submit.classList.add("is-busy"); }

            fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            })
                .then(function (r) {
                    var type = r.headers.get("Content-Type") || "";
                    if (type.indexOf("application/json") >= 0) {
                        return r.json().then(function (data) {
                            window.location.href = data.redirect || window.location.href;
                        });
                    }
                    return r.text().then(function (html) {
                        var host = form.parentNode;
                        host.innerHTML = html;
                        wire(host.closest("dialog") || host);
                        var fresh = host.querySelector("form[data-modal-form]");
                        if (fresh) { shake(fresh); }
                    });
                })
                .catch(function () {
                    if (submit) { submit.disabled = false; submit.classList.remove("is-busy"); }
                    form.submit();
                });
        });
    }

    function shake(el) {
        if (REDUCED) { return; }
        el.classList.remove("shake");
        void el.offsetWidth;
        el.classList.add("shake");
    }

    /* ---- Live ticket preview --------------------------------------------- */
    // [data-preview="FirstName LastName"] shows those fields joined; an
    // optional data-format reshapes it, and data-empty stands in when blank.
    var MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    var DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    function fieldText(form, name) {
        var f = form.elements[name];
        if (!f) { return ""; }
        if (f.tagName === "SELECT") { return f.value ? f.options[f.selectedIndex].text : ""; }
        return (f.value || "").trim();
    }

    function format(value, how) {
        if (!value) { return ""; }
        if (how === "initials") {
            return value.split(/\s+/).filter(Boolean).slice(0, 2).map(function (w) { return w[0].toUpperCase(); }).join("");
        }
        if (how === "month" || how === "day" || how === "weekday" || how === "time") {
            var d = new Date(value);
            if (isNaN(d)) { return ""; }
            if (how === "month") { return MONTHS[d.getMonth()]; }
            if (how === "day") { return String(d.getDate()).padStart(2, "0"); }
            if (how === "weekday") { return DAYS[d.getDay()]; }
            var h = d.getHours();
            return ((h % 12) || 12) + ":" + String(d.getMinutes()).padStart(2, "0") + (h < 12 ? " AM" : " PM");
        }
        return value;
    }

    // Mirrors Ui.TicketType on the server, so the colour never disagrees.
    function tone(name) {
        var n = (name || "").trim().toLowerCase();
        if (!n) { return "talk"; }
        if (n.indexOf("assembly") >= 0) { return "assembly"; }
        if (n.indexOf("workshop") >= 0 || n.indexOf("training") >= 0) { return "workshop"; }
        if (n.indexOf("seminar") >= 0 || n.indexOf("talk") >= 0 || n.indexOf("lecture") >= 0) { return "talk"; }
        if (n.indexOf("sport") >= 0) { return "training"; }
        if (n.indexOf("social") >= 0 || n.indexOf("outreach") >= 0) { return "service"; }
        var h = 17;
        for (var i = 0; i < n.length; i++) { h = (Math.imul(h, 31) + n.charCodeAt(i)) | 0; }
        return ["talk", "workshop", "assembly", "training", "service"][Math.abs(h % 5)];
    }

    function preview(form) {
        var targets = Array.prototype.slice.call(form.querySelectorAll("[data-preview]"));
        var toneFrom = form.getAttribute("data-tone-from");

        function paint() {
            targets.forEach(function (t) {
                var names = t.getAttribute("data-preview").split(" ");
                var raw = names.map(function (n) { return fieldText(form, n); }).filter(Boolean).join(" ");
                var out = format(raw, t.getAttribute("data-format"));
                t.textContent = out || t.getAttribute("data-empty") || "";
                t.classList.toggle("is-empty", !out);
            });

            if (toneFrom && form.elements[toneFrom]) {
                var f = form.elements[toneFrom];
                var opt = f.tagName === "SELECT" ? f.options[f.selectedIndex] : null;
                var next = opt && opt.getAttribute("data-tone") ? opt.getAttribute("data-tone") : (f.tagName === "SELECT" ? null : tone(f.value));
                if (next) { form.setAttribute("data-type", next); }
            }
        }

        form.addEventListener("input", paint);
        form.addEventListener("change", paint);
        paint();
    }

    // A form rendered as a page (no modal) still gets its preview.
    document.querySelectorAll(".form-page form[data-modal-form]").forEach(function (f) {
        preview(f);
        f.removeAttribute("data-modal-form");
    });

    window.Apilan = window.Apilan || {};
    window.Apilan.openDialog = open;
    window.Apilan.closeDialog = close;
}());
