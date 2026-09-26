const params = new URLSearchParams(window.location.search);
const reservation = RoadRideReservationApi.fromParams(params);

function formatDisplayDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value || "---";
  }

  return value.replaceAll("-", "/");
}

document.querySelector("#confirm-pickup").textContent = reservation.pickup || "---";
document.querySelector("#confirm-destination").textContent =
  reservation.destination || "---";
document.querySelector("#confirm-date").textContent =
  formatDisplayDate(reservation.date);
document.querySelector("#confirm-time").textContent =
  reservation.hour && reservation.minute
    ? `${reservation.hour}:${reservation.minute}`
    : "---";
document.querySelector("#confirm-passengers").textContent =
  reservation.passengers ? `${reservation.passengers}人` : "---";
document.querySelector("#confirm-care").textContent = reservation.care || "なし";
document.querySelector("#confirm-notes").textContent = reservation.notes || "なし";

document.querySelector("#back-button").addEventListener("click", () => {
  history.back();
});

document.querySelector("#confirm-button").addEventListener("click", async () => {
  const confirmButton = document.querySelector("#confirm-button");

  confirmButton.disabled = true;
  confirmButton.textContent = "予約を登録中...";

  try {
    const createdReservation = await RoadRideReservationApi.createReservation(reservation);
    const completeParams = RoadRideReservationApi.toParams(createdReservation);

    window.location.href = `reservation_complete.html?${completeParams.toString()}`;
  } catch (error) {
    RoadRideReservationApi.showError(error);
    confirmButton.disabled = false;
    confirmButton.textContent = "予約を確定する";
  }
});
