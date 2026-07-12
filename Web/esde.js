(function () {
  "use strict";

  var PLUGIN_ID = "esde-launcher";
  var API_BASE = "/EsDe";

  // ── Helpers ──────────────────────────────────────────────────────────────

  function apiCall(method, endpoint) {
    return fetch(API_BASE + endpoint, {
      method: method,
      headers: {
        Authorization: 'MediaBrowser Token="' + ApiClient.accessToken() + '"',
      },
    })
      .then(function (r) {
        return r.json();
      })
      .catch(function (e) {
        console.error("[ES-DE]", e);
        return null;
      });
  }

  function getStatus() {
    return apiCall("GET", "/Status");
  }
  function launchEsde() {
    return apiCall("POST", "/Launch");
  }

  // ── Styles ──────────────────────────────────────────────────────────────

  function injectStyles() {
    if (document.getElementById(PLUGIN_ID + "-styles")) return;
    var s = document.createElement("style");
    s.id = PLUGIN_ID + "-styles";
    s.textContent =
      ".esde-card-inner {" +
      "  display:flex;align-items:center;justify-content:center;" +
      "  width:100%;height:100%;position:relative;" +
      "  background:#79010F;" +
      "}" +
      ".esde-card-inner .esde-icon {" +
      "  font-size:4em;color:rgba(255,255,255,0.3);" +
      "}" +
      ".esde-card-inner.esde-running .esde-icon {" +
      "  opacity:0.15;" +
      "}" +
      ".esde-card-inner.esde-running::after {" +
      '  content:"En cours\\2026";' +
      "  position:absolute;bottom:0.8em;font-size:0.85em;" +
      "  color:rgba(255,255,255,0.6);" +
      "}";
    document.head.appendChild(s);
  }

  // ── Card builder ────────────────────────────────────────────────────────

  function buildCard(refCard) {
    var card = refCard.cloneNode(true);
    card.id = PLUGIN_ID + "-card";

    card.querySelectorAll("a").forEach(function (a) {
      a.removeAttribute("href");
      a.removeAttribute("data-id");
    });

    // Replace image with flat background + Material Icon (same as Films/Shows)
    var imgContainer = card.querySelector(".cardImageContainer");
    if (imgContainer) {
      imgContainer.style.backgroundImage = "none";
      imgContainer.style.backgroundColor = "transparent";
      imgContainer.innerHTML =
        '<div class="esde-card-inner">' +
        '<span class="material-icons esde-icon">sports_esports</span>' +
        "</div>";
    }

    var textEl = card.querySelector(".cardText");
    if (textEl) textEl.textContent = "ES-DE";

    card.querySelectorAll(".cardText").forEach(function (el, i) {
      if (i > 0) el.textContent = "";
    });

    card.addEventListener(
      "click",
      function (e) {
        e.preventDefault();
        e.stopPropagation();
        handleLaunch(card);
      },
      true,
    );

    return card;
  }

  // ── Launch handler ──────────────────────────────────────────────────────

  async function handleLaunch(card) {
    var inner = card.querySelector(".esde-card-inner");
    if (!inner || inner.classList.contains("esde-running")) return;

    inner.classList.add("esde-running");

    var result = await launchEsde();

    if (!result || (!result.pid && result.message)) {
      inner.classList.remove("esde-running");
      return;
    }

    var poll = setInterval(async function () {
      var s = await getStatus();
      if (s && !s.running) {
        clearInterval(poll);
        inner.classList.remove("esde-running");
      }
    }, 3000);
  }

  // ── Section + injection ─────────────────────────────────────────────────

  async function injectSection() {
    if (document.getElementById(PLUGIN_ID + "-section")) return;

    var homeSections = document.querySelector(".homeSectionsContainer");
    if (!homeSections) return;

    var refCard = homeSections.querySelector(".card");
    if (!refCard) return;

    var status = await getStatus();
    if (!status || !status.detected) return;

    injectStyles();

    var section = document.createElement("div");
    section.id = PLUGIN_ID + "-section";
    section.className = "verticalSection";

    var titleContainer = document.createElement("div");
    titleContainer.className =
      "sectionTitleContainer sectionTitleContainer-cards padded-left padded-right";
    var title = document.createElement("h2");
    title.className = "sectionTitle sectionTitle-cards";
    title.style.margin = "0";
    title.textContent = "Retro Gaming";
    titleContainer.appendChild(title);
    section.appendChild(titleContainer);

    var refItemsContainer = refCard.closest(".itemsContainer");
    var itemsRow;
    if (refItemsContainer) {
      itemsRow = refItemsContainer.cloneNode(false);
      itemsRow.innerHTML = "";
    } else {
      itemsRow = document.createElement("div");
      itemsRow.className = "itemsContainer padded-left padded-right";
    }

    itemsRow.appendChild(buildCard(refCard));

    var refScroller = refItemsContainer
      ? refItemsContainer.parentElement
      : null;
    if (refScroller && refScroller.classList.contains("emby-scroller")) {
      var scroller = refScroller.cloneNode(false);
      scroller.innerHTML = "";
      scroller.appendChild(itemsRow);
      section.appendChild(scroller);
    } else {
      section.appendChild(itemsRow);
    }

    homeSections.appendChild(section);
  }

  // ── Observer ────────────────────────────────────────────────────────────

  function observePageChanges() {
    var observer = new MutationObserver(function () {
      clearTimeout(observePageChanges._timer);
      observePageChanges._timer = setTimeout(function () {
        if (
          document.querySelector(".homeSectionsContainer") &&
          !document.getElementById(PLUGIN_ID + "-section")
        ) {
          injectSection();
        }
      }, 500);
    });
    observer.observe(document.body, { childList: true, subtree: true });
  }

  // ── Init ────────────────────────────────────────────────────────────────

  function init() {
    console.log("[ES-DE] Plugin loaded");
    injectSection();
    observePageChanges();
  }

  if (
    document.readyState === "complete" ||
    document.readyState === "interactive"
  ) {
    setTimeout(init, 1000);
  } else {
    document.addEventListener("DOMContentLoaded", function () {
      setTimeout(init, 1000);
    });
  }
})();
