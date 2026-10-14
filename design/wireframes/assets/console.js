/* Clickable prototype helpers. No build step.
   Copyright (c) 2026 Bryan C. Weis and Common Consulting Company */
(function () {
  var CAP = 2.0;
  var LIVE_SPEND = 1.24;
  var KEY = "mps-wireframe-mode";

  function mode() {
    return document.documentElement.getAttribute("data-mode") || localStorage.getItem(KEY) || "replay";
  }

  function apply() {
    var m = mode();
    document.documentElement.setAttribute("data-mode", m);
    localStorage.setItem(KEY, m);
    var spend = m === "live" ? LIVE_SPEND : 0;
    var pct = Math.min(100, (spend / CAP) * 100);
    document.querySelectorAll("[data-mode-label]").forEach(function (el) {
      el.textContent = m === "live" ? "LIVE AI" : m === "mock" ? "MOCK REASONER" : "REPLAY MODE";
    });
    document.querySelectorAll("[data-mode-note]").forEach(function (el) {
      el.textContent =
        m === "live"
          ? "Azure OpenAI via model gateway. Writes still require human approval."
          : m === "mock"
            ? "No recording for this injection; deterministic mock reasoner."
            : "Recorded responses. No live model calls.";
    });
    document.querySelectorAll("[data-mode-toggle]").forEach(function (el) {
      el.textContent = m === "live" ? "Switch to REPLAY" : "Switch to LIVE AI";
    });
    document.querySelectorAll("[data-spend-val]").forEach(function (el) {
      el.textContent = "$" + spend.toFixed(2) + " of $" + CAP.toFixed(2);
    });
    document.querySelectorAll("[data-spend-fill]").forEach(function (el) {
      el.style.width = pct + "%";
      el.classList.toggle("is-hot", spend / CAP >= 0.8);
    });
  }

  document.addEventListener("click", function (e) {
    var t = e.target.closest("[data-mode-toggle]");
    if (t) {
      var next = mode() === "live" ? "replay" : "live";
      document.documentElement.setAttribute("data-mode", next);
      apply();
    }
    var open = e.target.closest("[data-open]");
    if (open) {
      var id = open.getAttribute("data-open");
      var ov = document.getElementById(id);
      if (ov) ov.classList.add("is-open");
    }
    var close = e.target.closest("[data-close]");
    if (close) {
      var ov2 = close.closest(".overlay");
      if (ov2) ov2.classList.remove("is-open");
    }
  });

  document.addEventListener("submit", function (e) {
    var form = e.target.closest("[data-inject]");
    if (!form) return;
    e.preventDefault();
    window.location.href = "alerts.html?injected=1";
  });

  var params = new URLSearchParams(location.search);
  if (params.get("injected") === "1") {
    document.documentElement.setAttribute("data-injected", "1");
  }
  if (params.get("id") === "D-01") {
    var s2 = document.getElementById("s2-banner");
    if (s2) s2.hidden = false;
  }

  apply();
})();
