const params = new URLSearchParams(window.location.search);
const historyKey = "roadrideReservationHistory";
const reservationNumber = params.get("reservationNumber");

function buildHistoryUrl() {
  const historyParams = new URLSearchParams(params.toString());

  historyParams.set("status", "cancelled");

  return `reservation_history.html?${historyParams.toString()}`;
}

function readHistory() {
  try {
    return JSON.parse(localStorage.getItem(historyKey)) || [];
  } catch {
    return [];
  }
}

function updateReservationStatus() {
  if (!reservationNumber) {
    return;
  }

  const history = readHistory();
  const reservationExists = history.some(
    (item) => item.reservationNumber === reservationNumber
  );

  if (!reservationExists) {
    return;
  }

  const updatedHistory = history.map((item) =>
    item.reservationNumber === reservationNumber
      ? { ...item, status: "cancelled", cancelledAt: new Date().toISOString() }
      : item
  );

  localStorage.setItem(historyKey, JSON.stringify(updatedHistory));
}

document.querySelector("#reservation-number").textContent =
  reservationNumber || "---";

updateReservationStatus();

document.querySelector("#history-button").href = buildHistoryUrl();
document.querySelector("#breadcrumb-history-link").href = buildHistoryUrl();
