const reservations = [
  {
    id: "reservation-001",
    number: "RR-20260921-0001",
    date: "2026-09-21",
    time: "10:00",
    user: "佐藤 由美",
    pickup: "電波学園前",
    destination: "市役所",
    passengers: 1,
    notes: "なし",
    status: "matching"
  },
  {
    id: "reservation-002",
    number: "RR-20260921-0002",
    date: "2026-09-21",
    time: "10:10",
    user: "鈴木 一郎",
    pickup: "中央公園入口",
    destination: "市役所",
    passengers: 2,
    notes: "大きな荷物あり",
    status: "matching"
  },
  {
    id: "reservation-003",
    number: "RR-20260921-0003",
    date: "2026-09-21",
    time: "10:30",
    user: "高橋 美咲",
    pickup: "駅前ロータリー",
    destination: "中央病院",
    passengers: 1,
    notes: "車いす対応希望",
    status: "confirmed"
  }
];

const statusLabels = {
  matching: "マッチング中",
  confirmed: "予約確定",
  in_progress: "運行中",
  completed: "乗車完了",
  cancelled: "キャンセル"
};

const statusClasses = {
  matching: "status-info",
  confirmed: "status-success",
  in_progress: "status-warning",
  completed: "status-success",
  cancelled: "status-danger"
};

const state = {
  query: "",
  date: "all",
  status: "all"
};

const tableBody = document.querySelector("#reservation-table-body");
const emptyState = document.querySelector("#empty-state");
const resultCount = document.querySelector("#result-count");

function formatDate(date) {
  const [, month, day] = date.split("-");
  return `${Number(month)}月${Number(day)}日`;
}

function getFilteredReservations() {
  return reservations.filter((reservation) => {
    const searchableText = [
      reservation.number,
      reservation.user,
      reservation.pickup,
      reservation.destination
    ].join(" ").toLowerCase();
    const matchesQuery = searchableText.includes(state.query.toLowerCase());
    const matchesDate = state.date === "all" || reservation.date === state.date;
    const matchesStatus = state.status === "all" || reservation.status === state.status;
    return matchesQuery && matchesDate && matchesStatus;
  });
}

function renderStatus(status) {
  return `<span class="status ${statusClasses[status]}">${statusLabels[status]}</span>`;
}

function renderTable() {
  const filteredReservations = getFilteredReservations();
  resultCount.textContent = filteredReservations.length;
  tableBody.innerHTML = filteredReservations.map((reservation) => `
    <tr class="reservation-row" data-detail-url="reservation-detail.html?id=${encodeURIComponent(reservation.id)}" tabindex="0">
      <td>${formatDate(reservation.date)} ${reservation.time}</td>
      <td>${reservation.pickup}</td>
      <td>${reservation.destination}</td>
      <td>${renderStatus(reservation.status)}</td>
    </tr>
  `).join("");

  emptyState.hidden = filteredReservations.length > 0;
  tableBody.querySelectorAll(".reservation-row").forEach((row) => {
    const openDetail = () => {
      window.location.href = row.dataset.detailUrl;
    };

    row.addEventListener("click", (event) => {
      if (event.target.closest("a")) return;
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

function renderSummary() {
  const countByStatus = (status) => reservations.filter((reservation) => reservation.status === status).length;
  document.querySelector("#total-count").textContent = reservations.length;
  document.querySelector("#matching-count").textContent = countByStatus("matching");
  document.querySelector("#confirmed-count").textContent = countByStatus("confirmed");
  document.querySelector("#completed-count").textContent = countByStatus("completed");
}

function resetFilters() {
  state.query = "";
  state.date = "all";
  state.status = "all";
  document.querySelector("#search-input").value = "";
  document.querySelector("#date-filter").value = "all";
  document.querySelector("#status-filter").value = "all";
  renderTable();
}

document.querySelector("#search-input").addEventListener("input", (event) => {
  state.query = event.target.value.trim();
  renderTable();
});

document.querySelector("#date-filter").addEventListener("change", (event) => {
  state.date = event.target.value;
  renderTable();
});

document.querySelector("#status-filter").addEventListener("change", (event) => {
  state.status = event.target.value;
  renderTable();
});

document.querySelector("#refresh-button").addEventListener("click", resetFilters);
document.querySelector("#new-reservation-button").addEventListener("click", () => {
  window.alert("予約追加画面は次の実装で追加します。");
});

renderSummary();
renderTable();
