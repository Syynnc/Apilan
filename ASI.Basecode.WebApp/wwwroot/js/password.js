/* Apilan — password strength.
   <input data-strength> gets a four-bar meter, a checklist of rules, and a
   word for the stub's lock. <input data-strength-confirm> shows whether it
   matches. The submit button ([data-strength-submit]) waits until the new
   password is at least Fair and confirmed; the server checks the hard rules
   again (8+ characters, letters and numbers, not the default). */
(function () {
    "use strict";

    var WORDS = ["Too weak", "Weak", "Fair", "Good", "Strong"];
    var COMMON = ["password", "123456", "qwerty", "letmein", "welcome", "admin", "iloveyou", "abc123", "apilan", "student"];

    function rules(value, user, fallback) {
        var v = value || "";
        var lower = v.toLowerCase();
        return {
            length: v.length >= 8,
            mix: /[a-z]/i.test(v) && /\d/.test(v),
            "case": /[a-z]/.test(v) && /[A-Z]/.test(v),
            symbol: /[^A-Za-z0-9]/.test(v),
            fresh: v.length > 0 &&
                v !== fallback &&
                !(user && user.length > 2 && lower.indexOf(user.toLowerCase()) >= 0) &&
                !COMMON.some(function (c) { return lower.indexOf(c) >= 0; })
        };
    }

    // 0..4. The hard rules gate the score; length and variety push it up.
    function score(value, r) {
        if (!value) { return 0; }
        if (!r.length || !r.mix || !r.fresh) { return value.length >= 6 ? 1 : 0; }
        var s = 2;
        if (r["case"]) { s++; }
        if (r.symbol) { s++; }
        if (value.length >= 14) { s++; }
        if (/(.)\1\1/.test(value)) { s--; }   // aaa, 111
        return Math.max(2, Math.min(4, s));
    }

    function wire(form) {
        var input = form.querySelector("[data-strength]");
        if (!input || input._strength) { return; }
        input._strength = true;

        var meter = form.querySelector("[data-strength-meter]");
        var label = meter && meter.querySelector(".pw-label");
        var list = form.querySelector("[data-strength-rules]");
        var word = form.querySelector("[data-strength-word]");
        var confirm = form.querySelector("[data-strength-confirm]");
        var submit = form.querySelector("[data-strength-submit]");
        var user = input.getAttribute("data-strength-user") || "";
        var fallback = input.getAttribute("data-strength-default") || "";

        function paint() {
            var v = input.value;
            var r = rules(v, user, fallback);
            var s = score(v, r);

            if (meter) {
                meter.setAttribute("data-score", v ? String(s) : "0");
                meter.classList.toggle("is-empty", !v);
            }
            if (label) { label.textContent = v ? WORDS[s] : "Type a password"; }
            if (word) { word.textContent = v ? WORDS[s] : "—"; }
            form.setAttribute("data-strength-score", v ? String(s) : "");

            if (list) {
                Array.prototype.forEach.call(list.querySelectorAll("[data-rule]"), function (li) {
                    var ok = !!r[li.getAttribute("data-rule")];
                    li.classList.toggle("is-ok", ok && v.length > 0);
                });
            }

            var matches = confirm && confirm.value.length > 0 && confirm.value === v;
            if (confirm) {
                var wrap = confirm.closest(".pw-field");
                if (wrap) {
                    wrap.classList.toggle("is-match", matches);
                    wrap.classList.toggle("is-mismatch", confirm.value.length > 0 && !matches);
                }
            }

            if (submit) {
                var ready = s >= 2 && matches;
                submit.disabled = !ready;
                submit.title = ready ? "" : (s < 2 ? "Make the password stronger first" : "Confirm the password");
            }
        }

        input.addEventListener("input", paint);
        if (confirm) { confirm.addEventListener("input", paint); }
        paint();
    }

    // Show/hide toggles beside password fields.
    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-reveal]");
        if (!btn) { return; }
        var field = btn.parentNode.querySelector("input");
        var show = field.type === "password";
        field.type = show ? "text" : "password";
        btn.classList.toggle("is-on", show);
        btn.setAttribute("aria-label", show ? "Hide password" : "Show password");
        field.focus();
    });

    // A "Caps Lock is on" note under [data-caps] fields. Only a key event can
    // tell, so it appears on the first key pressed and goes when the field does.
    function caps(e) {
        var field = e.target.closest && e.target.closest("[data-caps]");
        var warn = field && field.closest(".field").querySelector("[data-caps-warn]");
        if (!warn) { return; }
        warn.hidden = e.type === "blur" || typeof e.getModifierState !== "function" || !e.getModifierState("CapsLock");
    }
    document.addEventListener("keydown", caps);
    document.addEventListener("keyup", caps);
    document.addEventListener("blur", caps, true);

    function wireAll(root) {
        Array.prototype.forEach.call((root || document).querySelectorAll("form"), wire);
    }

    wireAll(document);

    // Forms swapped in by the modal script (after a failed submit) are new nodes.
    new MutationObserver(function (records) {
        records.forEach(function (rec) {
            Array.prototype.forEach.call(rec.addedNodes, function (n) {
                if (n.nodeType === 1) { wireAll(n.parentNode || n); }
            });
        });
    }).observe(document.body, { childList: true, subtree: true });
}());
