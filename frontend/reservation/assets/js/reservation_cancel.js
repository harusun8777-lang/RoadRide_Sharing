// 予約キャンセル画面。予約を GET で取得して表示し、POST /api/reservations/{id}/cancel でキャンセルする
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const reservationId = Api.reservationIdFromUrl();
  const pageError = document.querySelector("#page-error");
  const loading = document.querySelector("#page-loading");
  const confirmButton = document.querySelector("#confirm-cancel-button");
  const reasonInput = document.querySelector("#cancel-reason");

  function renderReservation(reservation) {
    document.querySelector("#reservation-number").textContent = reservation.reservation_number || "---";
    document.querySelector("#ride-datetime").textContent = Api.formatDateTime(reservation.requested_pickup_at);
    document.querySelector("#pickup").textContent = reservation.pickup_location || "---";
    document.querySelector("#destination").textContent = reservation.destination || "---";
    document.querySelector("#passengers").textContent =
      reservation.passenger_count ? `${reservation.passenger_count}人` : "---";
    document.querySelector("#status").textContent = Api.statusLabel(reservation.status);

    if (!Api.isCancellable(reservation)) {
      Api.showMessage(
        pageError,
        `この予約は「${Api.statusLabel(reservation.status)}」のため、キャンセルできません。`
      );
      confirmButton.disabled = true;
      reasonInput.disabled = true;
    } else {
      confirmButton.disabled = false;
    }
  }

  async function initialize() {
    confirmButton.disabled = true;
    document.querySelector("#back-button").href = Api.pageUrl("reservation_detail.html", reservationId);

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

  confirmButton.addEventListener("click", async () => {
    const defaultLabel = confirmButton.textContent;

    confirmButton.disabled = true;
    confirmButton.textContent = "キャンセル中...";
    Api.showMessage(pageError, "");

    try {
      await Api.cancelReservation(reservationId, reasonInput.value);
      window.location.href = Api.pageUrl("reservation_cancel_complete.html", reservationId);
    } catch (error) {
      Api.showMessage(pageError, Api.errorMessage(error));
      confirmButton.textContent = defaultLabel;
      // 409（キャンセルできない状態）と 404 はやり直しても成功しないため、ボタンを押せないままにする
      const retryable = !(error instanceof RoadRideAuth.ApiError) || ![404, 409].includes(error.status);
      confirmButton.disabled = !retryable;
    }
  });

  initialize();
})();
