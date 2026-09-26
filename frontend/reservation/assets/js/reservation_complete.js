const params = new URLSearchParams(window.location.search);
const reservation = RoadRideReservationApi.fromParams(params);

function buildPageUrl(pageName) {
  const query = params.toString();

  return query ? `${pageName}?${query}` : pageName;
}

if (reservation.reservationNumber || reservation.reservationId) {
  RoadRideReservationApi.mergeHistory(reservation);
}

document.querySelector("#reservation-number").textContent =
  reservation.reservationNumber || "---";

document.querySelector("#detail-button").href =
  buildPageUrl("reservation_detail.html");
document.querySelector("#history-button").href =
  buildPageUrl("reservation_history.html");
