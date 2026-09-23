const params = new URLSearchParams(window.location.search);
const reservationNumber = params.get("reservationNumber");
const historyKey = "roadrideReservationHistory";

function buildPageUrl(pageName) {
  const query = params.toString();

  return query ? `${pageName}?${query}` : pageName;
}

function readHistory() {
  try {
    return JSON.parse(localStorage.getItem(historyKey)) || [];
  } catch {
    return [];
  }
}

function saveCurrentReservation() {
  if (!reservationNumber) {
    return;
  }

  const history = readHistory();
  const reservation = {
    reservationNumber,
    pickup: params.get("pickup") || "",
    destination: params.get("destination") || "",
    date: params.get("date") || "",
    hour: params.get("hour") || "",
    minute: params.get("minute") || "",
    passengers: params.get("passengers") || "",
    care: params.get("care") || "",
    notes: params.get("notes") || "",
    status: "matching",
    savedAt: new Date().toISOString()
  };

  const filteredHistory = history.filter(
    (item) => item.reservationNumber !== reservationNumber
  );

  localStorage.setItem(
    historyKey,
    JSON.stringify([reservation, ...filteredHistory])
  );
}

document.querySelector("#reservation-number").textContent =
  reservationNumber || "---";

saveCurrentReservation();

document.querySelector("#detail-button").href =
  buildPageUrl("reservation_detail.html");
document.querySelector("#history-button").href =
  buildPageUrl("reservation_history.html");
