// 予約履歴画面。GET /api/reservations で一覧を取得する
// - 乗車日・状態の絞り込みとページ送りは API のクエリ（date / status / page / limit）で行う
// - キーワード検索は API にないため、表示中のページの中だけで絞り込む
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const PAGE_SIZE = 20;

  const state = {
    query: "",
    date: "",
    status: "",
    page: 1,
    reservations: [],
    total: 0
  };
  let requestSequence = 0;

  const tableBody = document.querySelector("#reservation-table-body");
  const emptyState = document.querySelector("#empty-state");
  const emptyTitle = document.querySelector("#empty-title");
  const emptyText = document.querySelector("#empty-text");
  const resultSummary = document.querySelector("#result-summary");
  const pageError = document.querySelector("#page-error");
  const pagination = document.querySelector("#pagination");
  const prevButton = document.querySelector("#prev-page");
  const nextButton = document.querySelector("#next-page");
  const pageInfo = document.querySelector("#page-info");
  const searchInput = document.querySelector("#search-input");
  const dateFilter = document.querySelector("#date-filter");
  const statusFilter = document.querySelector("#status-filter");

  // ─────────────────────────────────────────
  // 件数（meta.total を使う）
  // ─────────────────────────────────────────

  async function loadSummary() {
    const targets = [
      ["#total-count", ""],
      ["#matching-count", "matching"],
      ["#confirmed-count", "confirmed"],
      ["#cancelled-count", "cancelled"]
    ];

    await Promise.all(targets.map(async ([selector, status]) => {
      const element = document.querySelector(selector);
      element.textContent = "…";
      try {
        const { meta } = await Api.listReservations({ status, page: 1, limit: 1 });
        element.textContent = meta.total;
      } catch {
        // 一覧の取得エラーと同じ原因のことが多いので、メッセージは一覧側で表示する
        element.textContent = "-";
      }
    }));
  }

  // ─────────────────────────────────────────
  // 一覧
  // ─────────────────────────────────────────

  function matchesQuery(reservation) {
    if (!state.query) return true;

    const searchableText = [
      reservation.reservation_number,
      reservation.pickup_location,
      reservation.destination
    ].join(" ").toLowerCase();

    return searchableText.includes(state.query.toLowerCase());
  }

  function createCell(label, content) {
    const cell = document.createElement("td");
    cell.dataset.label = label;
    if (content instanceof Node) {
      cell.append(content);
    } else {
      cell.textContent = content;
    }
    return cell;
  }

  function createStatusBadge(status) {
    const badge = document.createElement("span");
    badge.className = `status status-${Api.statusTone(status)}`;
    badge.textContent = Api.statusLabel(status);
    return badge;
  }

  function createRow(reservation) {
    const row = document.createElement("tr");
    const detailUrl = Api.pageUrl("reservation_detail.html", reservation.id);
    const time = document.createElement("span");

    row.className = "reservation-row";
    row.tabIndex = 0;
    time.className = "reservation-time";
    time.textContent = Api.formatShortDateTime(reservation.requested_pickup_at);

    row.append(
      createCell("乗車予定", time),
      createCell("乗車場所", reservation.pickup_location || "---"),
      createCell("目的地", reservation.destination || "---"),
      createCell("人数", reservation.passenger_count ? `${reservation.passenger_count}人` : "---"),
      createCell("状態", createStatusBadge(reservation.status))
    );

    const openDetail = () => {
      window.location.href = detailUrl;
    };
    row.addEventListener("click", openDetail);
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        openDetail();
      }
    });

    return row;
  }

  function hasFilters() {
    return Boolean(state.query || state.date || state.status);
  }

  function renderTable() {
    const shown = state.reservations.filter(matchesQuery);

    tableBody.replaceChildren(...shown.map(createRow));

    const start = state.total === 0 ? 0 : (state.page - 1) * PAGE_SIZE + 1;
    const end = (state.page - 1) * PAGE_SIZE + state.reservations.length;
    let summaryText = state.total === 0
      ? "0件"
      : `全${state.total}件中 ${start}〜${end}件目を表示中`;
    if (state.query) {
      summaryText += `（このページで「${state.query}」に一致：${shown.length}件）`;
    }
    resultSummary.textContent = summaryText;

    emptyState.hidden = shown.length > 0;
    if (!hasFilters()) {
      emptyTitle.textContent = "まだ予約がありません";
      emptyText.textContent = "新しく予約すると、ここに表示されます。";
    } else {
      emptyTitle.textContent = "該当する予約がありません";
      emptyText.textContent = "検索条件を変更して、もう一度お試しください。";
    }

    const totalPages = Math.max(1, Math.ceil(state.total / PAGE_SIZE));
    pagination.hidden = totalPages <= 1;
    pageInfo.textContent = `${state.page} / ${totalPages} ページ`;
    prevButton.disabled = state.page <= 1;
    nextButton.disabled = state.page >= totalPages;
  }

  function renderLoading() {
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.colSpan = 5;
    cell.className = "table-message";
    cell.textContent = "予約を読み込み中です…";
    row.append(cell);
    tableBody.replaceChildren(row);
    emptyState.hidden = true;
    resultSummary.textContent = "読み込み中…";
  }

  async function loadList() {
    const sequence = ++requestSequence;

    Api.showMessage(pageError, "");
    renderLoading();

    try {
      const { data, meta } = await Api.listReservations({
        status: state.status,
        date: state.date,
        page: state.page,
        limit: PAGE_SIZE
      });
      if (sequence !== requestSequence) return;

      state.reservations = data;
      state.total = Number(meta.total) || 0;

      // 件数が減って今のページが範囲外になったら最後のページを取り直す
      const totalPages = Math.max(1, Math.ceil(state.total / PAGE_SIZE));
      if (state.page > totalPages) {
        state.page = totalPages;
        loadList();
        return;
      }

      renderTable();
    } catch (error) {
      if (sequence !== requestSequence) return;

      state.reservations = [];
      state.total = 0;
      tableBody.replaceChildren();
      emptyState.hidden = true;
      pagination.hidden = true;
      resultSummary.textContent = "予約を取得できませんでした";
      Api.showMessage(pageError, `${Api.errorMessage(error)}（↻ ボタンで再読み込みできます）`);
    }
  }

  // ─────────────────────────────────────────
  // 操作
  // ─────────────────────────────────────────

  searchInput.addEventListener("input", (event) => {
    state.query = event.target.value.trim();
    renderTable();
  });

  dateFilter.addEventListener("change", (event) => {
    state.date = event.target.value || "";
    state.page = 1;
    loadList();
  });

  statusFilter.addEventListener("change", (event) => {
    state.status = event.target.value === "all" ? "" : event.target.value;
    state.page = 1;
    loadList();
  });

  prevButton.addEventListener("click", () => {
    if (state.page <= 1) return;
    state.page -= 1;
    loadList();
  });

  nextButton.addEventListener("click", () => {
    state.page += 1;
    loadList();
  });

  // 絞り込みを初期状態に戻して、件数と一覧を取り直す
  document.querySelector("#refresh-button").addEventListener("click", () => {
    state.query = "";
    state.date = "";
    state.status = "";
    state.page = 1;
    searchInput.value = "";
    dateFilter.value = "";
    statusFilter.value = "all";
    loadSummary();
    loadList();
  });

  loadSummary();
  loadList();
})();
