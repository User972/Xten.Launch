/*!
 * eBook Indonesia theme — behaviour layer.
 * Dependency-free, progressive enhancement only. Never bypasses nopCommerce logic:
 * the sticky Buy CTA simply triggers the real add-to-cart button.
 */
(function () {
  "use strict";

  function ready(fn) {
    if (document.readyState !== "loading") fn();
    else document.addEventListener("DOMContentLoaded", fn);
  }

  /* ---- FAQ accordion (used in topics / homepage / product description) ---- */
  function initFaq() {
    var qs = document.querySelectorAll(".xt-faq__q");
    qs.forEach(function (q) {
      q.setAttribute("role", "button");
      q.setAttribute("tabindex", "0");
      var ans = q.nextElementSibling;
      if (ans) { ans.hidden = true; }
      function toggle() {
        var open = q.classList.toggle("is-open");
        if (ans) ans.hidden = !open;
        q.setAttribute("aria-expanded", open ? "true" : "false");
      }
      q.addEventListener("click", toggle);
      q.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(); }
      });
    });
  }

  /* ---- Smooth scroll for in-page anchors (e.g. hero "How it works") ---- */
  function initSmoothScroll() {
    document.addEventListener("click", function (e) {
      var a = e.target.closest('a[href^="#"]');
      if (!a) return;
      var id = a.getAttribute("href");
      if (id.length < 2) return;
      var target = document.querySelector(id);
      if (!target) return;
      e.preventDefault();
      target.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  /* ---- Sticky mobile "Buy now" bar on product pages (triggers the real button) ---- */
  function initStickyBuy() {
    if (!document.querySelector(".html-product-details-page")) return;

    // nopCommerce renders the add-to-cart button as input.add-to-cart-button (id add-to-cart-button-<id>)
    var realBtn = document.querySelector(".add-to-cart-panel .add-to-cart-button, input.add-to-cart-button, .add-to-cart-button");
    if (!realBtn) return;

    var priceEl = document.querySelector(".product-essential .prices .product-price, .product-essential .prices .actual-price, .product-essential .product-price span");
    var label = realBtn.value || realBtn.textContent || "Buy now";

    var bar = document.createElement("div");
    bar.className = "xt-buybar";
    bar.setAttribute("aria-hidden", "true");
    bar.innerHTML =
      '<span class="xt-buybar__price">' + (priceEl ? priceEl.textContent.trim() : "") + "</span>" +
      '<button type="button" class="xt-buybar__btn">' + label + "</button>";
    document.body.appendChild(bar);

    bar.querySelector(".xt-buybar__btn").addEventListener("click", function () {
      // Reuse nopCommerce's own handler — no logic is duplicated or bypassed.
      realBtn.click();
    });

    // Show the bar only while the real button is OUT of view (mobile only via CSS).
    if ("IntersectionObserver" in window) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) {
          bar.classList.toggle("is-visible", !en.isIntersecting);
        });
      }, { rootMargin: "0px 0px -10% 0px" });
      io.observe(realBtn);
    } else {
      bar.classList.add("is-visible");
    }
  }

  /* ---- Auto Table of Contents from the product description headings ---- */
  function initToc() {
    var nav = document.querySelector(".xt-toc");
    if (!nav) return;
    var desc = document.querySelector(".full-description");
    if (!desc) return;
    var heads = desc.querySelectorAll("h2");
    if (heads.length < 2) return; // only worth a TOC with 2+ sections
    var ul = document.createElement("ul");
    heads.forEach(function (h, i) {
      if (!h.id) h.id = "sec-" + (i + 1);
      var li = document.createElement("li");
      var a = document.createElement("a");
      a.href = "#" + h.id;
      a.textContent = (h.textContent || "").trim();
      li.appendChild(a);
      ul.appendChild(li);
    });
    nav.appendChild(ul);
    nav.classList.add("is-built");
  }

  /* ---- Light / dark theme toggle (no-flash script in Head.cshtml sets the initial theme) ---- */
  function initTheme() {
    var root = document.documentElement, KEY = "xt-theme";
    function apply(t) { root.setAttribute("data-theme", t); try { localStorage.setItem(KEY, t); } catch (e) {} }

    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "xt-theme-toggle";
    btn.setAttribute("aria-label", "Toggle light / dark theme");
    btn.title = "Toggle theme";
    btn.innerHTML =
      '<svg class="icon-moon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>' +
      '<svg class="icon-sun" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="4.2"/><path d="M12 2v2.2M12 19.8V22M4.6 4.6l1.6 1.6M17.8 17.8l1.6 1.6M2 12h2.2M19.8 12H22M4.6 19.4l1.6-1.6M17.8 6.2l1.6-1.6"/></svg>';
    btn.addEventListener("click", function () {
      apply(root.getAttribute("data-theme") === "dark" ? "light" : "dark");
    });

    // Prefer the nopCommerce header selectors area; otherwise float it top-right.
    var host = document.querySelector(".header-selectors-wrapper") || document.querySelector(".header-links");
    if (host) { host.insertBefore(btn, host.firstChild); }
    else { btn.classList.add("is-floating"); document.body.appendChild(btn); }
  }

  /* ---- WhatsApp float — clones the first wa.me link on the page (admin-controlled number) ---- */
  function initWaFloat() {
    if (document.querySelector(".xt-wa-float")) return;
    var link = document.querySelector('a[href*="wa.me/"], a[href*="api.whatsapp.com"]');
    if (!link) return;
    var f = document.createElement("a");
    f.className = "xt-wa-float";
    f.href = link.href;
    f.target = "_blank";
    f.rel = "noopener";
    f.setAttribute("aria-label", "Chat on WhatsApp");
    f.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M17.6 6.32A7.85 7.85 0 0 0 12.05 4a7.95 7.95 0 0 0-6.9 11.93L4 20l4.18-1.1a7.93 7.93 0 0 0 3.86 1h.01c4.39 0 7.96-3.57 7.96-7.96 0-2.13-.83-4.13-2.34-5.62zM12.05 18.5h-.01a6.6 6.6 0 0 1-3.36-.92l-.24-.14-2.49.65.66-2.42-.16-.25a6.6 6.6 0 0 1-1.01-3.5c0-3.65 2.97-6.62 6.62-6.62 1.77 0 3.43.69 4.68 1.94a6.59 6.59 0 0 1 1.94 4.68c0 3.65-2.97 6.62-6.63 6.62z"/></svg>';
    document.body.appendChild(f);
  }

  ready(function () {
    try { initTheme(); } catch (e) {}
    try { initWaFloat(); } catch (e) {}
    try { initFaq(); } catch (e) {}
    try { initSmoothScroll(); } catch (e) {}
    try { initStickyBuy(); } catch (e) {}
    try { initToc(); } catch (e) {}
  });
})();
