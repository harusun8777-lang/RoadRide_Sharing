const params = new URLSearchParams(window.location.search);

const careLabels = {
  wheelchair: "車いす",
  "large-luggage": "大きな荷物",
  other: "その他"
};
const statusLabels = {
  in_progress: "乗車中",
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
const confirmedStatuses = ["confirmed", "in_progress", "completed"];

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

function buildPageUrl(pageName, reservation) {
  const nextParams = RoadRideReservationApi.toParams(reservation);
  const query = nextParams.toString();

  return query ? `${pageName}?${query}` : pageName;
}

function renderStatus(reservation) {
  const status = (reservation && reservation.status) || params.get("status") || "matching";
  const statusBadge = document.querySelector("#status-badge");

  statusBadge.className = `status ${statusClasses[status] || "info"}`;
  statusBadge.textContent = statusLabels[status] || statusLabels.matching;
}

function renderConfirmedDetails(reservation) {
  const status = reservation.status;
  const details = document.querySelector("#confirmed-details");

  details.hidden = !confirmedStatuses.includes(status);
  if (details.hidden) {
    return;
  }

  document.querySelector("#confirmed-time").textContent = formatDatetime(
    reservation.date,
    reservation.hour,
    reservation.minute
  );
  document.querySelector("#confirmed-fare").textContent = params.get("fare") || "---";
  document.querySelector("#confirmed-duration").textContent = params.get("duration") || "---";
  document.querySelector("#confirmed-vehicle").textContent = params.get("vehicle") || "---";
  document.querySelector("#confirmed-rider-order").textContent = params.get("riderOrder") || "---";
}

function renderTimeline(reservation) {
  const status = (reservation && reservation.status) || params.get("status") || "matching";
  const timeline = document.querySelector("#reservation-timeline");
  const rideDatetime = formatDatetime(
    reservation.date,
    reservation.hour,
    reservation.minute
  );
  const items = [
    {
      title: "予約を受け付けました",
      time: reservation.reservationNumber || "予約番号の発行済み",
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
  } else if (status === "in_progress") {
    items.push({ title: "乗車中です", time: "目的地へ向かっています", current: true });
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
        <time>${RoadRideReservationApi.escapeHtml(item.time)}</time>
      </div>
    </li>
  `).join("");
}

function renderReservation(reservation) {
  document.querySelector("#reservation-number").textContent =
    reservation.reservationNumber || "---";
  document.querySelector("#pickup").textContent = reservation.pickup || "---";
  document.querySelector("#destination").textContent =
    reservation.destination || "---";
  document.querySelector("#reservation-date").textContent =
    formatDisplayDate(reservation.date);
  document.querySelector("#reservation-time").textContent =
    formatDisplayTime(reservation.hour, reservation.minute);
  document.querySelector("#passengers").textContent =
    reservation.passengers ? `${reservation.passengers}人` : "---";
  document.querySelector("#care").textContent =
    careLabels[reservation.care] || reservation.notes || "なし";

  renderStatus(reservation);
  renderConfirmedDetails(reservation);
  renderTimeline(reservation);

  document.querySelector("#history-link").href =
    buildPageUrl("reservation_history.html", reservation);
  document.querySelector("#cancel-button").hidden = !["matching", "confirmed"].includes(reservation.status);
  document.querySelector("#cancel-button").href =
    buildPageUrl("reservation_cancel.html", reservation);
}

async function initializeDetailPage() {
  const requestedReservation = RoadRideReservationApi.fromParams(params);

  const reservation = await RoadRideReservationApi.getReservation(
    requestedReservation.reservationId
  );

  renderReservation(reservation);
}

initializeDetailPage().catch(error => {
  document.querySelector("#cancel-button").hidden = true;
  RoadRideReservationApi.showError(error);
});
