const params = new URLSearchParams(window.location.search);
const statusLabels = {
  matching: "マッチング中",
  confirmed: "予約確定",
  cancelled: "キャンセル済み",
  completed: "乗車完了"
};
const statusClasses = {
  matching: "status-info",
  confirmed: "status-success",
  completed: "status-success",
  cancelled: "status-danger"
};
const state = {
  query: "",
  date: "all",
  status: "all"
};

function formatDisplayDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value || "---";
  }

  return value.replaceAll("-", "/");
}

function formatScheduleDatetime(reservation) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(reservation.date)) {
    return "---";
  }

  const [, month, day] = reservation.date.split("-");
  const hour = reservation.hour || "--";
  const minute = reservation.minute || "--";

  return `${Number(month)}月${Number(day)}日 ${hour}:${minute}`;
}

function buildDetailUrl(reservation) {
  const detailParams = RoadRideReservationApi.toParams(reservation);
  const query = detailParams.toString();

  return query ? `reservation_detail.html?${query}` : "reservation_detail.html";
}

function createCurrentReservation() {
  const reservation = RoadRideReservationApi.fromParams(params);

  if (!reservation.reservationNumber && !reservation.reservationId) {
    return null;
  }

  return {
    ...reservation,
    status: statusLabels[reservation.status] ? reservation.status : "matching",
    savedAt: new Date().toISOString()
  };
}

function mergeCurrentReservation(history) {
  const currentReservation = createCurrentReservation();

  if (!currentReservation) {
    return history;
  }

  const filteredHistory = history.filter((item) => {
    if (
      currentReservation.reservationId &&
      item.reservationId === currentReservation.reservationId
    ) {
      return false;
    }

    return item.reservationNumber !== currentReservation.reservationNumber;
  });
  const nextHistory = [currentReservation, ...filteredHistory];

  RoadRideReservationApi.writeHistory(nextHistory);

  return nextHistory;
}

function getFilteredReservations(history) {
  return history.filter((reservation) => {
    const searchableText = [
      reservation.reservationNumber,
      reservation.pickup,
      reservation.destination
    ].join(" ").toLowerCase();
    const matchesQuery = searchableText.includes(state.query.toLowerCase());
    const matchesDate = state.date === "all" || reservation.date === state.date;
    const matchesStatus =
      state.status === "all" || reservation.status === state.status;

    return matchesQuery && matchesDate && matchesStatus;
  });
}

function renderSummary(history) {
  const countByStatus = (status) =>
    history.filter((reservation) => reservation.status === status).length;

  document.querySelector("#total-count").textContent = history.length;
  document.querySelector("#matching-count").textContent =
    countByStatus("matching");
  document.querySelector("#confirmed-count").textContent =
    countByStatus("confirmed");
  document.querySelector("#cancelled-count").textContent =
    countByStatus("cancelled");
}

function renderStatus(status) {
  const currentStatus = status || "matching";

  return `<span class="status ${statusClasses[currentStatus] || "status-info"}">${statusLabels[currentStatus] || statusLabels.matching}</span>`;
}

function renderTable(history) {
  const tableBody = document.querySelector("#reservation-table-body");
  const emptyState = document.querySelector("#empty-state");
  const resultCount = document.querySelector("#result-count");
  const filteredReservations = getFilteredReservations(history);

  resultCount.textContent = filteredReservations.length;
  tableBody.innerHTML = filteredReservations.map((reservation) => `
    <tr class="reservation-row" data-detail-url="${buildDetailUrl(reservation)}" tabindex="0">
      <td data-label="乗車予定">
        <span class="reservation-time">${formatScheduleDatetime(reservation)}</span>
      </td>
      <td data-label="乗車場所">${reservation.pickup || "---"}</td>
      <td data-label="目的地">${reservation.destination || "---"}</td>
      <td data-label="人数">${reservation.passengers ? `${reservation.passengers}人` : "---"}</td>
      <td data-label="状態">${renderStatus(reservation.status)}</td>
    </tr>
  `).join("");

  emptyState.hidden = filteredReservations.length > 0;

  tableBody.querySelectorAll(".reservation-row").forEach((row) => {
    const openDetail = () => {
      window.location.href = row.dataset.detailUrl;
    };

    row.addEventListener("click", (event) => {
      if (event.target.closest("a")) {
        return;
      }

      openDetail();
    });
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        openDetail();
      }
    });
  });
}

function resetFilters(history) {
  state.query = "";
  state.date = "all";
  state.status = "all";
  document.querySelector("#search-input").value = "";
  document.querySelector("#date-filter").value = "";
  document.querySelector("#status-filter").value = "all";
  renderTable(history);
}

async function initializeHistoryPage() {
  const history = mergeCurrentReservation(
    await RoadRideReservationApi.listReservations()
  );

  renderSummary(history);
  renderTable(history);

  document.querySelector("#search-input").addEventListener("input", (event) => {
    state.query = event.target.value.trim();
    renderTable(history);
  });

  document.querySelector("#date-filter").addEventListener("change", (event) => {
    state.date = event.target.value || "all";
    renderTable(history);
  });

  document.querySelector("#status-filter").addEventListener("change", (event) => {
    state.status = event.target.value;
    renderTable(history);
  });

  document.querySelector("#refresh-button").addEventListener("click", () => {
    resetFilters(history);
  });
}

initializeHistoryPage();
