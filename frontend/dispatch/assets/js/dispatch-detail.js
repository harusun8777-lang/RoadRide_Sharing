const reservationId = new URLSearchParams(window.location.search).get("id") || "reservation-001";
const source = new URLSearchParams(window.location.search).get("source") || "ai";

const reservations = {
  "reservation-001": {
    number: "RR-20260921-0001",
    date: "2026-09-21",
    time: "09:55",
    user: "佐藤 由美",
    pickup: "電波学園前",
    destination: "市役所",
    passengers: 2,
    vehicle: "乗合タクシー 1号車",
    fare: "800円",
    routeDuration: "約25分",
    riders: "佐藤 由美 → 鈴木 一郎",
    sourceLabel: source === "manual" ? "手動配車" : "AI配車候補",
    status: "confirmed"
  },
  "reservation-002": {
    number: "RR-20260921-0002",
    date: "2026-09-21",
    time: "10:10",
    user: "鈴木 一郎",
    pickup: "中央公園入口",
    destination: "市役所",
    passengers: 1,
    vehicle: "乗合タクシー 1号車",
    fare: "500円",
    routeDuration: "約20分",
    riders: "鈴木 一郎",
    sourceLabel: source === "manual" ? "手動配車" : "AI配車候補",
    status: "confirmed"
  },
  "reservation-003": {
    number: "RR-20260921-0003",
    date: "2026-09-21",
    time: "10:30",
    user: "高橋 美咲",
    pickup: "駅前ロータリー",
    destination: "中央病院",
    passengers: 1,
    vehicle: "乗合タクシー 2号車",
    fare: "700円",
    routeDuration: "約18分",
    riders: "高橋 美咲",
    sourceLabel: source === "manual" ? "手動配車" : "AI配車候補",
    status: "in_progress"
  }
};

const reservation = reservations[reservationId] || reservations["reservation-001"];

try {
  const dispatchPlan = JSON.parse(localStorage.getItem("roadrideDispatchPlan"));
  if (dispatchPlan?.reservationId === reservationId) {
    reservation.time = dispatchPlan.departure || reservation.time;
    reservation.passengers = dispatchPlan.passengers || reservation.passengers;
    reservation.vehicle = dispatchPlan.vehicle || reservation.vehicle;
    reservation.riders = dispatchPlan.riderOrder || reservation.riders;
    reservation.sourceLabel = dispatchPlan.source === "manual" ? "手動配車" : reservation.sourceLabel;
  }
} catch {
}

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
  in_progress: "status-warning",
  completed: "status-success",
  cancelled: "status-danger"
};

const statusBadge = document.querySelector("#status-badge");
const noticeTitle = document.querySelector("#notice-title");
const noticeMessage = document.querySelector("#notice-message");
const confirmedDetails = document.querySelector("#confirmed-details");
const primaryActionButton = document.querySelector("#primary-action-button");
const cancelButton = document.querySelector("#cancel-button");
const notificationKey = `roadrideNotification:${reservation.number}`;
let notificationSent = localStorage.getItem(notificationKey) === "true";

function updateStatus(status) {
  reservation.status = status;
  saveReservationStatus(status);
  statusBadge.className = `status ${statusClass[status]}`;
  statusBadge.textContent = statusText[status];

  const messages = {
    matching: ["配車を調整しています", "条件に合う乗合候補を確認中です。確定したらここで状態更新できます。"],
    confirmed: ["配車内容を確認しました", "運行開始を行うか、必要に応じてキャンセルしてください。"],
    in_progress: ["運行を開始しました", "現在、乗車予定に沿って運行中です。乗車完了を記録できます。"],
    completed: ["乗車を完了しました", "予約は運行完了として記録されました。"],
    cancelled: ["配車をキャンセルしました", "この配車はキャンセル済みです。"]
  };

  const [title, message] = messages[status] || messages.confirmed;
  noticeTitle.textContent = title;
  noticeMessage.textContent = message;

  confirmedDetails.hidden = false;

  const actionLabel = status === "confirmed" && !notificationSent
    ? "利用者へ確定内容を通知"
    : status === "confirmed"
      ? "運行開始"
      : status === "in_progress"
        ? "乗車完了"
        : "";
  primaryActionButton.hidden = !actionLabel;
  primaryActionButton.textContent = actionLabel;

  const isTerminal = ["completed", "cancelled"].includes(status);
  cancelButton.hidden = isTerminal;
  cancelButton.disabled = isTerminal;
  cancelButton.textContent = status === "cancelled" ? "キャンセル済み" : "キャンセル";
}

function renderReservation() {
  const [, month, day] = reservation.date.split("-");
  document.querySelector("#reservation-number").textContent = reservation.number;
  document.querySelector("#reservation-date").textContent = `${Number(month)}月${Number(day)}日`;
  document.querySelector("#reservation-time").textContent = reservation.time;
  document.querySelector("#reservation-user").textContent = reservation.user;
  document.querySelector("#reservation-passengers").textContent = `${reservation.passengers}名`;
  document.querySelector("#vehicle-name").textContent = reservation.vehicle;
  document.querySelector("#fare-estimate").textContent = reservation.fare;
  document.querySelector("#pickup-location").textContent = reservation.pickup;
  document.querySelector("#destination-location").textContent = reservation.destination;
  document.querySelector("#route-duration").textContent = reservation.routeDuration;
  document.querySelector("#rider-order").textContent = reservation.riders;
  document.querySelector("#confirmed-departure").textContent = reservation.time;
  document.querySelector("#source-label").textContent = reservation.sourceLabel;
}

function showMessage(message) {
  noticeTitle.textContent = message;
  noticeMessage.textContent = "この画面はフロントエンド確認用です。バックエンドには接続していません。";
}

function saveReservationStatus(status) {
  const historyKey = "roadrideReservationHistory";
  let history;

  try {
    history = JSON.parse(localStorage.getItem(historyKey)) || [];
  } catch {
    history = [];
  }

  const updatedHistory = history.map((item) =>
    item.reservationNumber === reservation.number
      ? {
          ...item,
          status,
          vehicle: reservation.vehicle,
          fare: reservation.fare,
          duration: reservation.routeDuration,
          riderOrder: reservation.riders
        }
      : item
  );

  localStorage.setItem(historyKey, JSON.stringify(updatedHistory));
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

primaryActionButton.addEventListener("click", () => {
  if (reservation.status === "confirmed" && !notificationSent) {
    notificationSent = true;
    localStorage.setItem(notificationKey, "true");
    updateStatus("confirmed");
    return;
  }

  if (reservation.status === "confirmed") {
    updateStatus("in_progress");
    return;
  }

  if (reservation.status === "in_progress") {
    const confirmed = window.confirm("運行を完了しますか？");
    if (!confirmed) return;
    updateStatus("completed");
    window.location.href = "reservations.html";
  }
});

cancelButton.addEventListener("click", () => {
  if (reservation.status === "completed" || reservation.status === "cancelled") return;
  const confirmCancel = window.confirm("この配車をキャンセルしますか？\nキャンセル後は一覧画面に戻ります。");
  if (confirmCancel) {
    updateStatus("cancelled");
  }
});

renderReservation();
updateStatus(reservation.status);
