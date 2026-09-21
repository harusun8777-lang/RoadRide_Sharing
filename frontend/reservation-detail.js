const reservations = {
  "reservation-001": {
    number: "RR-20260921-0001",
    date: "2026-09-21",
    time: "10:00",
    user: "佐藤 由美",
    pickup: "電波学園前",
    destination: "市役所",
    passengers: 1,
    status: "matching"
  },
  "reservation-002": {
    number: "RR-20260921-0002",
    date: "2026-09-21",
    time: "10:10",
    user: "鈴木 一郎",
    pickup: "中央公園入口",
    destination: "市役所",
    passengers: 2,
    status: "matching"
  },
  "reservation-003": {
    number: "RR-20260921-0003",
    date: "2026-09-21",
    time: "10:30",
    user: "高橋 美咲",
    pickup: "駅前ロータリー",
    destination: "中央病院",
    passengers: 1,
    status: "confirmed"
  }
};

const reservationId = new URLSearchParams(window.location.search).get("id") || "reservation-001";
const reservation = reservations[reservationId] || {
  number: "RR-20260921-0012",
  date: "2026-09-21",
  time: "10:00",
  user: "佐藤 由美",
  pickup: "電波学園前",
  destination: "市役所",
  passengers: 1,
  status: "matching"
};

const statusText = {
  matching: "マッチング中",
  confirmed: "予約確定",
  in_progress: "運行中",
  completed: "乗車完了",
  cancelled: "キャンセル"
};

const statusClass = {
  matching: "status-info",
  confirmed: "status-success",
  in_progress: "status-info",
  completed: "status-success",
  cancelled: "status-danger"
};

const statusBadge = document.querySelector("#status-badge");
const noticeTitle = document.querySelector("#notice-title");
const noticeMessage = document.querySelector("#notice-message");
const confirmedDetails = document.querySelector("#confirmed-details");

function updateStatus(status) {
  reservation.status = status;
  statusBadge.className = `status ${statusClass[status]}`;
  statusBadge.textContent = statusText[status];

  const messages = {
    matching: ["配車を調整しています", "条件に合う乗合候補を確認中です。確定したらこの画面でお知らせします。"],
    confirmed: ["予約が確定しました", "乗車時刻、乗車場所、料金目安を確認してください。"],
    cancelled: ["配車をキャンセルしました", "この配車はキャンセル済みです。"]
  };
  const [title, message] = messages[status] || messages.matching;
  noticeTitle.textContent = title;
  noticeMessage.textContent = message;
  confirmedDetails.hidden = status !== "confirmed";

  const cancelButton = document.querySelector("#cancel-button");
  cancelButton.disabled = status === "cancelled" || status === "completed";
  cancelButton.textContent = status === "cancelled" ? "キャンセル済み" : "配車をキャンセル";
}

function showMessage(message) {
  noticeTitle.textContent = message;
  noticeMessage.textContent = "この画面はフロントエンドの確認用です。バックエンドには接続していません。";
}

function renderReservation() {
  const [, month, day] = reservation.date.split("-");
  document.querySelector("#reservation-number").textContent = reservation.number;
  document.querySelector("#reservation-date").textContent = `${Number(month)}月${Number(day)}日`;
  document.querySelector("#reservation-time").textContent = reservation.time;
  document.querySelector("#reservation-passengers").textContent = `${reservation.passengers}名`;
  document.querySelector("#reservation-user").textContent = reservation.user;
  document.querySelector("#pickup-location").textContent = reservation.pickup;
  document.querySelector("#destination-location").textContent = reservation.destination;
}

document.querySelector("#copy-button").addEventListener("click", async (event) => {
  try {
    await navigator.clipboard.writeText(reservation.number);
    event.currentTarget.textContent = "コピーしました";
  } catch {
    showMessage("予約番号を選択してコピーしてください");
  }

  window.setTimeout(() => {
    event.currentTarget.textContent = "番号をコピー";
  }, 1800);
});

document.querySelector("#cancel-button").addEventListener("click", () => {
  if (reservation.status === "cancelled" || reservation.status === "completed") return;
  const confirmed = window.confirm("この配車をキャンセルしますか？");
  if (confirmed) updateStatus("cancelled");
});

document.querySelector("#confirm-button").addEventListener("click", () => {
  if (reservation.status !== "matching") return;
  updateStatus("confirmed");
});

renderReservation();
updateStatus(reservation.status);
