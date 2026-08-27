(() => {
  "use strict";
  const $ = selector => document.querySelector(selector);
  const $$ = selector => [...document.querySelectorAll(selector)];
  const sidebar = $(".sidebar");
  const drawer = $(".detail-drawer");
  const backdrop = $(".drawer-backdrop");
  const dialog = $("#license-dialog");
  const toast = $("#toast");
  let toastTimer;

  const showToast = message => {
    toast.textContent = message;
    toast.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove("show"), 2200);
  };

  $$(".nav-link").forEach(link => link.addEventListener("click", () => {
    $$(".nav-link").forEach(item => item.classList.toggle("active", item === link));
    sidebar.classList.remove("open");
  }));
  $(".mobile-menu").addEventListener("click", () => sidebar.classList.toggle("open"));

  const openDrawer = license => {
    $("#drawer-license").textContent = license;
    drawer.classList.add("open");
    drawer.setAttribute("aria-hidden", "false");
    backdrop.classList.add("open");
  };
  $$("[data-license]").forEach(button => button.addEventListener("click", () => openDrawer(button.dataset.license)));
  $$("[data-close-drawer]").forEach(button => button.addEventListener("click", () => {
    drawer.classList.remove("open");
    drawer.setAttribute("aria-hidden", "true");
    backdrop.classList.remove("open");
  }));

  $$("[data-open-dialog]").forEach(button => button.addEventListener("click", () => dialog.showModal()));
  $$("[data-toast]").forEach(button => button.addEventListener("click", () => showToast(button.dataset.toast)));
  $("[data-focus-search]").addEventListener("click", () => $("#license-search").focus());
  $("[data-section-jump]").addEventListener("click", button => location.hash = button.currentTarget.dataset.sectionJump);

  const filterRows = () => {
    const query = $("#license-search").value.toLocaleLowerCase("pt-BR").trim();
    const status = $("#status-filter").value;
    let count = 0;
    $$("[data-row]").forEach(row => {
      const visible = (!query || row.dataset.search.includes(query)) && (status === "all" || row.dataset.status === status);
      row.hidden = !visible;
      if (visible) count += 1;
    });
    $("#result-count").textContent = `${count} ${count === 1 ? "licença" : "licenças"}`;
  };
  $("#license-search").addEventListener("input", filterRows);
  $("#status-filter").addEventListener("change", filterRows);
  $("#global-search").addEventListener("input", event => {
    $("#license-search").value = event.target.value;
    filterRows();
    if (event.target.value) location.hash = "licenses";
  });
  document.addEventListener("keydown", event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
      event.preventDefault();
      $("#global-search").focus();
    }
    if (event.key === "Escape") {
      drawer.classList.remove("open");
      backdrop.classList.remove("open");
    }
  });
})();
