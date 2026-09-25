const params = new URLSearchParams(window.location.search);

const careLabels = {
  wheelchair: "車いす",
  "large-luggage": "大きな荷物",
  other: "その他"
};
const statusLabels = {
  matching: "マッチング中",
  confirmed: "予約確定",
  completed: "乗車完了",
  cancelled: "キャンセル済み"
};
const statusClasses = {
  matching: "info",
  confirmed: "success",
  completed: "success",
  cancelled: "danger"
};
const confirmedStatuses = ["confirmed", "completed"];

function getParam(name) {
  return params.get(name) || "";
}

function formatDisplayDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value || "---";
  }

  return value.replaceAll("-", "/");
}

function formatDatetime(date, hour, minute) {
  if (!date) {
    return "---";
  }

  const time = hour && minute ? ` ${hour}:${minute}` : "";

  return `${formatDisplayDate(date)}${time}`;
}

function formatDisplayTime(hour, minute) {
  return hour && minute ? `${hour}:${minute}` : "---";
}

function buildPageUrl(pageName) {
  const query = params.toString();

  return query ? `${pageName}?${query}` : pageName;
}

function updateHistoryStatus() {
  const status = getParam("status");
  const reservationNumber = getParam("reservationNumber");

  if (!confirmedStatuses.includes(status) || !reservationNumber) {
    return;
  }

  const historyKey = "roadrideReservationHistory";
  let history;

  try {
    history = JSON.parse(localStorage.getItem(historyKey)) || [];
  } catch {
    history = [];
  }

  const updatedHistory = history.map((item) =>
    item.reservationNumber === reservationNumber
      ? {
          ...item,
          status,
          vehicle: getParam("vehicle") || item.vehicle || "",
          fare: getParam("fare") || item.fare || "",
          duration: getParam("duration") || item.duration || "",
          riderOrder: getParam("riderOrder") || item.riderOrder || ""
        }
      : item
  );

  localStorage.setItem(historyKey, JSON.stringify(updatedHistory));
}

function renderStatus() {
  const status = getParam("status") || "matching";
  const statusBadge = document.querySelector("#status-badge");

  statusBadge.className = `status ${statusClasses[status] || "info"}`;
  statusBadge.textContent = statusLabels[status] || statusLabels.matching;
}

function renderConfirmedDetails() {
  const status = getParam("status");
  const details = document.querySelector("#confirmed-details");

  details.hidden = !confirmedStatuses.includes(status);
  if (details.hidden) {
    return;
  }

  document.querySelector("#confirmed-time").textContent = formatDatetime(
    getParam("date"),
    getParam("hour"),
    getParam("minute")
  );
  document.querySelector("#confirmed-fare").textContent = getParam("fare") || "---";
  document.querySelector("#confirmed-duration").textContent = getParam("duration") || "---";
  document.querySelector("#confirmed-vehicle").textContent = getParam("vehicle") || "---";
  document.querySelector("#confirmed-rider-order").textContent = getParam("riderOrder") || "---";
}

function renderTimeline() {
  const status = getParam("status") || "matching";
  const timeline = document.querySelector("#reservation-timeline");
  const rideDatetime = formatDatetime(
    getParam("date"),
    getParam("hour"),
    getParam("minute")
  );
  const items = [
    {
      title: "予約を受け付けました",
      time: getParam("reservationNumber") || "予約番号の発行済み",
      current: false
    },
    {
      title: "マッチング処理を開始しました",
      time: rideDatetime,
      current: status === "matching"
    }
  ];

  if (status === "confirmed") {
    items.push({
      title: "予約が確定しました",
      time: "乗車内容をご確認ください",
      current: true
    });
  } else if (status === "cancelled") {
    items.push({
      title: "予約をキャンセルしました",
      time: "キャンセル済み",
      current: true
    });
  } else if (status === "completed") {
    items.push({
      title: "乗車が完了しました",
      time: "ご利用ありがとうございました",
      current: true
    });
  } else {
    items.push({
      title: "配車担当者の確認待ち",
      time: "次の更新をお待ちください",
      pending: true
    });
  }

  timeline.innerHTML = items.map((item) => `
    <li class="timeline-item${item.current ? " is-current" : ""}${item.pending ? " is-pending" : ""}">
      <span class="timeline-dot"></span>
      <div>
        <strong>${item.title}</strong>
        <time>${item.time}</time>
      </div>
    </li>
  `).join("");
}

document.querySelector("#reservation-number").textContent =
  getParam("reservationNumber") || "---";
document.querySelector("#pickup").textContent = getParam("pickup") || "---";
document.querySelector("#destination").textContent =
  getParam("destination") || "---";
document.querySelector("#reservation-date").textContent =
  formatDisplayDate(getParam("date"));
document.querySelector("#reservation-time").textContent =
  formatDisplayTime(getParam("hour"), getParam("minute"));
document.querySelector("#passengers").textContent =
  getParam("passengers") ? `${getParam("passengers")}人` : "---";
document.querySelector("#care").textContent =
  careLabels[getParam("care")] || "なし";

renderStatus();
renderConfirmedDetails();
renderTimeline();
updateHistoryStatus();

document.querySelector("#history-link").href =
  buildPageUrl("reservation_history.html");
document.querySelector("#cancel-button").href =
  buildPageUrl("reservation_cancel.html");
