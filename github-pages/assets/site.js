(function () {
  function parsePageData() {
    const element = document.getElementById("page-data");
    if (!element) {
      return {};
    }

    try {
      return JSON.parse(element.textContent || "{}");
    } catch {
      return {};
    }
  }

  const pageData = parsePageData();
  const skillsByName = new Map((pageData.skills || []).map((skill) => [skill.name, skill]));

  function copyText(text, button) {
    if (!navigator.clipboard) {
      return;
    }

    navigator.clipboard.writeText(text).then(() => {
      if (!button) {
        return;
      }

      const originalLabel = button.textContent;
      button.textContent = "Copied";
      window.setTimeout(() => {
        button.textContent = originalLabel;
      }, 1400);
    });
  }

  function initCopyButtons() {
    document.querySelectorAll("[data-copy]").forEach((button) => {
      button.addEventListener("click", () => copyText(button.dataset.copy || "", button));
    });
  }

  function isInteractiveTarget(target, container) {
    if (!(target instanceof Element)) {
      return false;
    }

    const interactive = target.closest("a, button, input, textarea, select, summary, [role='button'], [role='link']");
    return Boolean(interactive && interactive !== container && container.contains(interactive));
  }

  function initCardLinks() {
    document.querySelectorAll("[data-card-href]").forEach((card) => {
      const href = card.dataset.cardHref;
      if (!href) {
        return;
      }

      card.addEventListener("click", (event) => {
        if (isInteractiveTarget(event.target, card)) {
          return;
        }

        window.location.href = href;
      });

      card.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") {
          return;
        }

        if (isInteractiveTarget(event.target, card)) {
          return;
        }

        event.preventDefault();
        window.location.href = href;
      });
    });
  }

  function initListingFilters() {
    const input = document.getElementById("search-input");
    const cards = Array.from(document.querySelectorAll(".js-filter-card"));
    const tabs = Array.from(document.querySelectorAll(".filter-tab"));
    const emptyState = document.getElementById("listing-empty");
    const configuredListPath = pageData.querySyncPath || "";
    const listPath = configuredListPath ? new URL(configuredListPath, window.location.origin).pathname : window.location.pathname;
    let activeFilter = tabs.find((tab) => tab.classList.contains("is-active"))?.dataset.filter || "all";

    if (!input && tabs.length === 0) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const initialQuery = params.get("q");
    const initialFilter = params.get("filter");

    if (input && initialQuery) {
      input.value = initialQuery;
    }

    if (initialFilter && tabs.some((tab) => tab.dataset.filter === initialFilter)) {
      activeFilter = initialFilter;
      tabs.forEach((tab) => {
        tab.classList.toggle("is-active", tab.dataset.filter === initialFilter);
      });
    }

    function applyFilters() {
      const query = (input?.value || "").trim().toLowerCase();
      let visibleCount = 0;

      cards.forEach((card) => {
        const filterText = (card.dataset.filtertext || "").toLowerCase();
        const filterValue = card.dataset.collection || card.dataset.category || "";
        const matchesQuery = !query || filterText.includes(query);
        const matchesFilter = activeFilter === "all" || filterValue === activeFilter;
        const visible = matchesQuery && matchesFilter;

        card.classList.toggle("is-hidden", !visible);
        if (visible) {
          visibleCount += 1;
        }
      });

      if (emptyState) {
        emptyState.classList.toggle("is-hidden", visibleCount !== 0);
      }

      const nextParams = new URLSearchParams(window.location.search);
      if (query) {
        nextParams.set("q", query);
      } else {
        nextParams.delete("q");
      }

      if (activeFilter !== "all" && tabs.length > 0) {
        nextParams.set("filter", activeFilter);
      } else {
        nextParams.delete("filter");
      }

      const nextQuery = nextParams.toString();
      const nextUrl = nextQuery ? `${listPath}?${nextQuery}` : listPath;
      window.history.replaceState({}, "", nextUrl);
    }

    tabs.forEach((tab) => {
      tab.addEventListener("click", () => {
        activeFilter = tab.dataset.filter || "all";
        tabs.forEach((candidate) => candidate.classList.toggle("is-active", candidate === tab));
        applyFilters();
      });
    });

    if (input) {
      input.addEventListener("input", applyFilters);
    }

    applyFilters();
  }

  function initSkillModal() {
    const overlay = document.getElementById("skill-modal-overlay");
    const dialog = overlay?.querySelector(".skill-modal");
    const closeButton = document.getElementById("skill-modal-close");
    const copyButton = document.getElementById("skill-modal-copy");
    const sourceLink = document.getElementById("skill-modal-source");
    const pageLink = document.getElementById("skill-modal-page");

    if (!overlay || !dialog || !closeButton || skillsByName.size === 0) {
      return;
    }

    let currentSkill = null;
    let lastTrigger = null;

    function closeModal() {
      overlay.hidden = true;
      overlay.setAttribute("aria-hidden", "true");
      document.body.style.overflow = "";
      currentSkill = null;
      if (lastTrigger && typeof lastTrigger.focus === "function") {
        lastTrigger.focus();
      }
    }

    function openModal(skillName, trigger) {
      const skill = skillsByName.get(skillName);
      if (!skill) {
        return;
      }

      lastTrigger = trigger || document.activeElement;
      currentSkill = skill;
      document.getElementById("skill-modal-title").textContent = skill.title || skill.name;
      document.getElementById("skill-modal-version").textContent = `v${skill.version}`;
      document.getElementById("skill-modal-collection").textContent = skill.collection;
      document.getElementById("skill-modal-description").textContent = skill.description;
      document.getElementById("skill-modal-compatibility").textContent = skill.compatibility || "Works with current .NET projects.";
      document.getElementById("skill-modal-command").textContent = skill.installCommand;
      sourceLink.href = skill.sourceUrl;
      pageLink.href = skill.detailUrl;

      overlay.hidden = false;
      overlay.setAttribute("aria-hidden", "false");
      document.body.style.overflow = "hidden";
      closeButton.focus();
    }

    document.querySelectorAll("[data-open-skill]").forEach((button) => {
      button.addEventListener("click", () => openModal(button.dataset.openSkill || "", button));
    });

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) {
        closeModal();
      }
    });

    dialog.addEventListener("click", (event) => {
      event.stopPropagation();
    });

    closeButton.addEventListener("click", closeModal);

    copyButton.addEventListener("click", () => {
      if (currentSkill) {
        copyText(currentSkill.installCommand, copyButton);
      }
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !overlay.hidden) {
        closeModal();
      }
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    initCopyButtons();
    initCardLinks();
    initListingFilters();
    initSkillModal();
  });
})();
