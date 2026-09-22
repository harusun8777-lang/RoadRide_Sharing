const params = new URLSearchParams(window.location.search);

const pickup = params.get("pickup");
const destination = params.get("destination");
const date = params.get("date");
const hour = params.get("hour");
const minute = params.get("minute");
const passengers = params.get("passengers");
const care = params.get("care");
const notes = params.get("notes");

function formatDisplayDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value || "---";
  }

  return value.replaceAll("-", "/");
}

document.querySelector("#confirm-pickup").textContent = pickup || "---";
document.querySelector("#confirm-destination").textContent = destination || "---";
document.querySelector("#confirm-date").textContent = formatDisplayDate(date);
document.querySelector("#confirm-time").textContent =
  hour && minute ? `${hour}:${minute}` : "---";
document.querySelector("#confirm-passengers").textContent =
  passengers ? `${passengers}人` : "---";
document.querySelector("#confirm-care").textContent = care || "なし";
document.querySelector("#confirm-notes").textContent = notes || "なし";

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

function createReservationNumber() {
  const reservationDate = formatReservationNumberDate(date);
  const sequence = String(Math.floor(Math.random() * 10000)).padStart(4, "0");

  return `RR-${reservationDate}-${sequence}`;
}

document.querySelector("#confirm-button").addEventListener("click", () => {
  const reservationNumber = createReservationNumber();
  const completeParams = new URLSearchParams(window.location.search);

  completeParams.set("reservationNumber", reservationNumber);

  window.location.href = `reservation_complete.html?${completeParams.toString()}`;
});
