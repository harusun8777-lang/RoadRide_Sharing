const params = new URLSearchParams(window.location.search);

function getParam(name) {
  return params.get(name) || "";
}

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

function buildPageUrl(pageName) {
  const query = params.toString();

  return query ? `${pageName}?${query}` : pageName;
}

document.querySelector("#reservation-number").textContent =
  getParam("reservationNumber") || "---";
document.querySelector("#ride-datetime").textContent = formatDatetime(
  getParam("date"),
  getParam("hour"),
  getParam("minute")
);
document.querySelector("#pickup").textContent = getParam("pickup") || "---";
document.querySelector("#destination").textContent =
  getParam("destination") || "---";
document.querySelector("#passengers").textContent =
  getParam("passengers") ? `${getParam("passengers")}人` : "---";

document.querySelector("#back-button").href =
  buildPageUrl("reservation_detail.html");

document
  .querySelector("#confirm-cancel-button")
  .addEventListener("click", () => {
    window.location.href = buildPageUrl("reservation_cancel_complete.html");
  });
