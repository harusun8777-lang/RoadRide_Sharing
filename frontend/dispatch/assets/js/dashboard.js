const reservations = [
  {
    id: "reservation-001",
    number: "RR-20260921-0001",
    date: "2026-09-21",
    time: "10:00",
    pickup: "電波学園前",
    destination: "市役所",
    passengers: 1,
    status: "matching"
  },
  {
    id: "reservation-002",
    number: "RR-20260921-0002",
    date: "2026-09-21",
    time: "10:10",
    pickup: "中央公園入口",
    destination: "市役所",
    passengers: 2,
    status: "matching"
  },
  {
    id: "reservation-003",
    number: "RR-20260921-0003",
    date: "2026-09-21",
    time: "10:30",
    pickup: "駅前ロータリー",
    destination: "中央病院",
    passengers: 1,
    status: "in_progress"
  },
  {
    id: "reservation-004",
    number: "RR-20260921-0004",
    date: "2026-09-21",
    time: "11:15",
    pickup: "市役所前",
    destination: "中央公園入口",
    passengers: 2,
    status: "confirmed"
  }
];

const statusLabels = {
  matching: "確認待ち",
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

function getActionUrl(reservation) {
  return `reservations.html?status=${encodeURIComponent(reservation.status)}`;
}

function renderSummary() {
  const countByStatus = (status) =>
    reservations.filter((reservation) => reservation.status === status).length;

  document.querySelector("#total-count").textContent = reservations.length;
  document.querySelector("#matching-count").textContent = countByStatus("matching");
  document.querySelector("#confirmed-count").textContent = countByStatus("confirmed");
  document.querySelector("#in-progress-count").textContent = countByStatus("in_progress");
}

function renderAttention() {
  const attentionReservations = reservations.filter(
    (reservation) => reservation.status === "matching"
  );
  const list = document.querySelector("#attention-list");
  const emptyState = document.querySelector("#attention-empty");

  document.querySelector("#attention-count").textContent = `${attentionReservations.length}件`;
  emptyState.hidden = attentionReservations.length > 0;
  list.innerHTML = attentionReservations.map((reservation) => `
    <a class="attention-item" href="${getActionUrl(reservation)}">
      <span class="attention-main">
        <strong>${reservation.number}</strong>
        <small>${reservation.pickup} → ${reservation.destination} / ${reservation.passengers}名</small>
      </span>
      <span class="attention-action">候補を確認 →</span>
    </a>
  `).join("");
}

function renderOperationStatus() {
  const operationStatuses = [
    { label: "確認待ち", status: "matching", color: "#f59e0b" },
    { label: "予約確定", status: "confirmed", color: "#15803d" },
    { label: "運行中", status: "in_progress", color: "#c2410c" },
    { label: "乗車完了", status: "completed", color: "#155eef" }
  ];
  const list = document.querySelector("#operation-list");

  list.innerHTML = operationStatuses.map((operation) => {
    const count = reservations.filter(
      (reservation) => reservation.status === operation.status
    ).length;
    const percentage = reservations.length ? Math.round((count / reservations.length) * 100) : 0;

    return `
      <div class="operation-item">
        <span class="operation-label">${operation.label}</span>
        <strong class="operation-value">${count}件</strong>
        <div class="operation-bar"><span style="width: ${percentage}%; background: ${operation.color}"></span></div>
      </div>
    `;
  }).join("");
}

function renderSchedule() {
  const scheduleReservations = reservations
    .filter((reservation) => ["confirmed", "in_progress"].includes(reservation.status))
    .sort((firstReservation, secondReservation) => firstReservation.time.localeCompare(secondReservation.time));
  const list = document.querySelector("#schedule-list");

  list.innerHTML = scheduleReservations.map((reservation) => `
    <a class="schedule-item" href="${getActionUrl(reservation)}">
      <span class="schedule-main">
        <strong>${reservation.pickup} → ${reservation.destination}</strong>
        <small>${reservation.number} / ${reservation.passengers}名 / ${statusLabels[reservation.status]}</small>
      </span>
      <span class="schedule-time">${reservation.time}<small>${statusLabels[reservation.status]}</small></span>
    </a>
  `).join("");
}

renderSummary();
renderAttention();
renderOperationStatus();
renderSchedule();
