const params = new URLSearchParams(window.location.search);
let reservation = RoadRideReservationApi.fromParams(params);

function formatDisplayDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value || "---";
  }

  return value.replaceAll("-", "/");
}

function formatDatetime(date, hour, minute) {
  if (!date) {
    return "---";
  }

  const time = hour && minute ? ` ${hour}:${minute}` : "";

  return `${formatDisplayDate(date)}${time}`;
}

function buildPageUrl(pageName, nextReservation = reservation) {
  const nextParams = RoadRideReservationApi.toParams(nextReservation);
  const query = nextParams.toString();

  return query ? `${pageName}?${query}` : pageName;
}

function renderReservation(nextReservation) {
  reservation = nextReservation;

  document.querySelector("#reservation-number").textContent =
    reservation.reservationNumber || "---";
  document.querySelector("#ride-datetime").textContent = formatDatetime(
    reservation.date,
    reservation.hour,
    reservation.minute
  );
  document.querySelector("#pickup").textContent = reservation.pickup || "---";
  document.querySelector("#destination").textContent =
    reservation.destination || "---";
  document.querySelector("#passengers").textContent =
    reservation.passengers ? `${reservation.passengers}人` : "---";

  document.querySelector("#back-button").href =
    buildPageUrl("reservation_detail.html", reservation);
}

async function initializeCancelPage() {
  renderReservation(reservation);

  const fetchedReservation = await RoadRideReservationApi.getReservation(
    reservation.reservationId,
    reservation
  );

  renderReservation(fetchedReservation);
}

document
  .querySelector("#confirm-cancel-button")
  .addEventListener("click", async () => {
    const button = document.querySelector("#confirm-cancel-button");

    button.disabled = true;
    button.textContent = "キャンセル中...";

    const cancelledReservation = await RoadRideReservationApi.cancelReservation(
      reservation,
      "利用者画面からキャンセル"
    );
    const nextParams = RoadRideReservationApi.toParams(cancelledReservation);

    window.location.href = `reservation_cancel_complete.html?${nextParams.toString()}`;
  });

initializeCancelPage();
