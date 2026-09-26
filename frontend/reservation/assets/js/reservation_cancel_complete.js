const params = new URLSearchParams(window.location.search);
const reservation = {
  ...RoadRideReservationApi.fromParams(params),
  status: "cancelled"
};

function buildHistoryUrl() {
  const historyParams = RoadRideReservationApi.toParams(reservation);

  historyParams.set("status", "cancelled");

  return `reservation_history.html?${historyParams.toString()}`;
}

document.querySelector("#reservation-number").textContent =
  reservation.reservationNumber || "---";



document.querySelector("#history-button").href = buildHistoryUrl();
document.querySelector("#breadcrumb-history-link").href = buildHistoryUrl();
