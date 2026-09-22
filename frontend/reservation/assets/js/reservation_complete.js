const params = new URLSearchParams(window.location.search);
const reservationNumber = params.get("reservationNumber");

document.querySelector("#reservation-number").textContent =
  reservationNumber || "---";

document.querySelector("#detail-button").href =
  `reservation_detail.html?${params.toString()}`;
