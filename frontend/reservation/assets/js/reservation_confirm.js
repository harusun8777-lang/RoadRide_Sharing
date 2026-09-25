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

function formatReservationNumberDate(value) {
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value.replaceAll("-", "");
  }

  const sourceDate = new Date();
  const year = sourceDate.getFullYear();
  const month = String(sourceDate.getMonth() + 1).padStart(2, "0");
  const day = String(sourceDate.getDate()).padStart(2, "0");

  return `${year}${month}${day}`;
}

function createFallbackReservation() {
  const reservationDate = formatReservationNumberDate(reservation.date);
  const sequence = String(Math.floor(Math.random() * 10000)).padStart(4, "0");

  return {
    ...reservation,
    reservationNumber: `RR-${reservationDate}-${sequence}`,
    status: "matching",
    savedAt: new Date().toISOString()
  };
}

document.querySelector("#confirm-button").addEventListener("click", async () => {
  const confirmButton = document.querySelector("#confirm-button");

  confirmButton.disabled = true;
  confirmButton.textContent = "予約を登録中...";

  try {
    const createdReservation = await RoadRideReservationApi.createReservation(reservation);
    const completeParams = RoadRideReservationApi.toParams(createdReservation);

    window.location.href = `reservation_complete.html?${completeParams.toString()}`;
  } catch {
    const fallbackReservation = createFallbackReservation();
    const completeParams = RoadRideReservationApi.toParams(fallbackReservation);

    RoadRideReservationApi.mergeHistory(fallbackReservation);
    window.location.href = `reservation_complete.html?${completeParams.toString()}`;
  }
});
