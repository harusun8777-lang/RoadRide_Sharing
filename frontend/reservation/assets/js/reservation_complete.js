// 予約完了画面。URL の ?id= の予約を GET /api/reservations/{id} で取得して表示する
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const reservationId = Api.reservationIdFromUrl();
  const pageError = document.querySelector("#page-error");
  const loading = document.querySelector("#page-loading");
  const summary = document.querySelector("#reservation-summary");
  const detailButton = document.querySelector("#detail-button");

  function render(reservation) {
    document.querySelector("#reservation-number").textContent = reservation.reservation_number || "---";
    document.querySelector("#summary-datetime").textContent = Api.formatDateTime(reservation.requested_pickup_at);
    document.querySelector("#summary-pickup").textContent = reservation.pickup_location || "---";
    document.querySelector("#summary-destination").textContent = reservation.destination || "---";
    document.querySelector("#summary-passengers").textContent =
      reservation.passenger_count ? `${reservation.passenger_count}人` : "---";
    summary.hidden = false;
    detailButton.href = Api.pageUrl("reservation_detail.html", reservation.id);
    detailButton.hidden = false;
  }

  async function initialize() {
    detailButton.hidden = true;

    if (!reservationId) {
      Api.showMessage(pageError, "予約が指定されていません。予約履歴から予約を確認してください。");
      return;
    }

    Api.showMessage(loading, "予約内容を読み込み中です…");
    try {
      render(await Api.getReservation(reservationId));
    } catch (error) {
      Api.showMessage(pageError, `予約内容を取得できませんでした。${Api.errorMessage(error)}`);
    } finally {
      Api.showMessage(loading, "");
    }
  }

  initialize();
})();
