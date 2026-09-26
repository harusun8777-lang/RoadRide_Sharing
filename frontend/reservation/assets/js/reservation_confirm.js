// 予約内容の確認画面。「予約を確定する」で POST /api/reservations を呼び、完了画面へ進む
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const formValue = Api.formFromParams(new URLSearchParams(window.location.search));
  const confirmButton = document.querySelector("#confirm-button");
  const pageError = document.querySelector("#page-error");
  const confirmList = document.querySelector(".confirm-list");

  function formatDisplayDate(value) {
    return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value.replaceAll("-", "/") : "---";
  }

  document.querySelector("#confirm-pickup").textContent = formValue.pickup || "---";
  document.querySelector("#confirm-destination").textContent = formValue.destination || "---";
  document.querySelector("#confirm-date").textContent = formatDisplayDate(formValue.date);
  document.querySelector("#confirm-time").textContent =
    formValue.hour && formValue.minute ? `${formValue.hour}:${formValue.minute}` : "---";
  document.querySelector("#confirm-passengers").textContent =
    formValue.passengers ? `${formValue.passengers}人` : "---";
  document.querySelector("#confirm-care").textContent = Api.careLabel(formValue.care) || "なし";
  document.querySelector("#confirm-notes").textContent = formValue.notes || "なし";

  // 入力画面へ戻るときは入力値を URL パラメーターで渡す
  document.querySelector("#back-button").addEventListener("click", () => {
    window.location.href = `reservation.html?${Api.formToParams(formValue).toString()}`;
  });

  const isComplete = formValue.pickup && formValue.destination && formValue.date
    && formValue.hour && formValue.minute && formValue.passengers;

  if (!isComplete) {
    Api.showMessage(pageError, "予約内容が指定されていません。入力画面からやり直してください。");
    confirmButton.disabled = true;
    return;
  }

  confirmButton.addEventListener("click", async () => {
    const defaultLabel = confirmButton.textContent;

    confirmButton.disabled = true;
    confirmButton.textContent = "予約を登録中...";
    Api.showMessage(pageError, "");
    Api.showFieldErrors(confirmList, {});

    try {
      const reservation = await Api.createReservation(formValue);
      window.location.href = Api.pageUrl("reservation_complete.html", reservation.id);
    } catch (error) {
      const fieldErrors = error instanceof RoadRideAuth.ApiError ? error.fieldErrors : {};
      const shownCount = Api.showFieldErrors(confirmList, fieldErrors);
      const message = Api.errorMessage(error);

      Api.showMessage(
        pageError,
        shownCount > 0 ? `${message}「入力内容を修正する」から入力し直してください。` : message
      );
      pageError.scrollIntoView({ behavior: "smooth", block: "center" });
      confirmButton.disabled = false;
      confirmButton.textContent = defaultLabel;
    }
  });
})();
