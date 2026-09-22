const params = new URLSearchParams(window.location.search);
const reservationNumber = params.get("reservationNumber");

document.querySelector("#reservation-number").textContent =
  reservationNumber || "---";
