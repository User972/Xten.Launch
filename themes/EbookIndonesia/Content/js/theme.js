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

  ready(function () {
    try { initFaq(); } catch (e) {}
    try { initSmoothScroll(); } catch (e) {}
    try { initStickyBuy(); } catch (e) {}
  });
})();
