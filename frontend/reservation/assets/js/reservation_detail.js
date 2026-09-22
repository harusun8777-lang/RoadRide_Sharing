const params = new URLSearchParams(window.location.search);

const careLabels = {
  wheelchair: "車いす",
  "large-luggage": "大きな荷物",
  other: "その他"
};

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

document.querySelector("#reservation-number").textContent =
  getParam("reservationNumber") || "---";
document.querySelector("#pickup").textContent = getParam("pickup") || "---";
document.querySelector("#destination").textContent =
  getParam("destination") || "---";
document.querySelector("#ride-datetime").textContent = formatDatetime(
  getParam("date"),
  getParam("hour"),
  getParam("minute")
);
document.querySelector("#passengers").textContent =
  getParam("passengers") ? `${getParam("passengers")}人` : "---";
document.querySelector("#care").textContent =
  careLabels[getParam("care")] || "なし";
