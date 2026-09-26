// 予約詳細画面。URL の ?id= の予約を GET /api/reservations/{id} で取得して表示する
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const reservationId = Api.reservationIdFromUrl();
  const pageError = document.querySelector("#page-error");
  const loading = document.querySelector("#page-loading");
  const content = document.querySelector("#detail-content");
  const statusBadge = document.querySelector("#status-badge");
  const cancelButton = document.querySelector("#cancel-button");
  const cancelNote = document.querySelector("#cancel-note");

  function renderStatus(reservation) {
    statusBadge.className = `status ${Api.statusTone(reservation.status)}`;
    statusBadge.textContent = Api.statusLabel(reservation.status);
    statusBadge.hidden = false;
  }

  function createTimelineItem({ title, time, current = false, pending = false }) {
    const item = document.createElement("li");
    const dot = document.createElement("span");
    const body = document.createElement("div");
    const heading = document.createElement("strong");
    const timeElement = document.createElement("time");

    item.className = "timeline-item";
    item.classList.toggle("is-current", current);
    item.classList.toggle("is-pending", pending);
    dot.className = "timeline-dot";
    heading.textContent = title;
    timeElement.textContent = time;
    body.append(heading, timeElement);
    item.append(dot, body);

    return item;
  }

  function renderTimeline(reservation) {
    const status = reservation.status;
    const items = [
      {
        title: "予約を受け付けました",
        time: reservation.created_at ? Api.formatDateTime(reservation.created_at) : "---"
      },
      {
        title: "マッチング処理を開始しました",
        time: `希望乗車日時 ${Api.formatDateTime(reservation.requested_pickup_at)}`,
        current: status === "matching"
      }
    ];

    if (status === "confirmed") {
      items.push({ title: "予約が確定しました", time: "乗車内容をご確認ください", current: true });
    } else if (status === "in_progress") {
      items.push({ title: "乗車中です", time: "目的地へ向かっています", current: true });
    } else if (status === "completed") {
      items.push({ title: "乗車が完了しました", time: "ご利用ありがとうございました", current: true });
    } else if (status === "cancelled") {
      const reason = reservation.cancellation_reason ? `（理由：${reservation.cancellation_reason}）` : "";
      items.push({
        title: "予約をキャンセルしました",
        time: `${reservation.cancelled_at ? Api.formatDateTime(reservation.cancelled_at) : "キャンセル済み"}${reason}`,
        current: true
      });
    } else {
      items.push({ title: "配車担当者の確認待ち", time: "次の更新をお待ちください", pending: true });
    }

    document.querySelector("#reservation-timeline").replaceChildren(...items.map(createTimelineItem));
  }

  function renderCancelAction(reservation) {
    const cancellable = Api.isCancellable(reservation);

    cancelButton.hidden = !cancellable;
    cancelButton.href = Api.pageUrl("reservation_cancel.html", reservation.id);
    Api.showMessage(
      cancelNote,
      cancellable ? "" : `この予約は「${Api.statusLabel(reservation.status)}」のため、キャンセルできません。`
    );
  }

  function renderReservation(reservation) {
    document.querySelector("#reservation-number").textContent = reservation.reservation_number || "---";
    document.querySelector("#pickup").textContent = reservation.pickup_location || "---";
    document.querySelector("#destination").textContent = reservation.destination || "---";
    document.querySelector("#reservation-date").textContent = Api.formatDate(reservation.requested_pickup_at);
    document.querySelector("#reservation-time").textContent = Api.formatTime(reservation.requested_pickup_at);
    document.querySelector("#passengers").textContent =
      reservation.passenger_count ? `${reservation.passenger_count}人` : "---";
    document.querySelector("#care").textContent = reservation.consideration_notes || "なし";

    renderStatus(reservation);
    renderTimeline(reservation);
    renderCancelAction(reservation);
    content.hidden = false;
  }

  async function initialize() {
    if (!reservationId) {
      Api.showMessage(pageError, "予約が指定されていません。予約履歴から予約を選んでください。");
      return;
    }

    Api.showMessage(loading, "予約内容を読み込み中です…");
    try {
      renderReservation(await Api.getReservation(reservationId));
    } catch (error) {
      Api.showMessage(pageError, Api.errorMessage(error));
    } finally {
      Api.showMessage(loading, "");
    }
  }

  initialize();
})();
