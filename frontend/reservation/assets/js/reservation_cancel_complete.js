// 予約キャンセル完了画面。URL の ?id= の予約を GET で取得し、予約番号とキャンセル日時を表示する
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const reservationId = Api.reservationIdFromUrl();
  const pageError = document.querySelector("#page-error");
  const loading = document.querySelector("#page-loading");
  const cancelledAt = document.querySelector("#cancelled-at");

  async function initialize() {
    if (!reservationId) {
      Api.showMessage(pageError, "予約が指定されていません。予約履歴から予約を確認してください。");
      return;
    }

    Api.showMessage(loading, "予約内容を読み込み中です…");
    try {
      const reservation = await Api.getReservation(reservationId);

      document.querySelector("#reservation-number").textContent = reservation.reservation_number || "---";
      if (reservation.status !== "cancelled") {
        Api.showMessage(pageError, `この予約の状態は「${Api.statusLabel(reservation.status)}」です。予約詳細で状態を確認してください。`);
      } else if (reservation.cancelled_at) {
        Api.showMessage(cancelledAt, `キャンセル日時：${Api.formatDateTime(reservation.cancelled_at)}`);
      }
    } catch (error) {
      Api.showMessage(pageError, `予約内容を取得できませんでした。${Api.errorMessage(error)}`);
    } finally {
      Api.showMessage(loading, "");
    }
  }

  initialize();
})();
