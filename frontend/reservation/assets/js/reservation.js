// 現時点ではUI確認用です。
// バックエンド接続時にフォーム送信処理へ置き換えます。
const form = document.querySelector("#reservation-form");

async function searchAddress(postcodeId, addressId) {
  const postcode = document.querySelector(postcodeId).value
    .replace("-", "")
    .trim();

  if (!/^\d{7}$/.test(postcode)) {
    alert("郵便番号を7桁で入力してください。");
    return;
  }

  try {
    const response = await fetch(
      `https://zipcloud.ibsnet.co.jp/api/search?zipcode=${postcode}`
    );

    const data = await response.json();

    if (!data.results) {
      alert("住所が見つかりませんでした。");
      return;
    }

    const result = data.results[0];
    const address = result.address1 + result.address2 + result.address3;

    document.querySelector(addressId).value = address;
  } catch (error) {
    console.error(error);
    alert("住所の取得に失敗しました。");
  }
}

const hourSelect = document.querySelector("#time-hour");
const minuteSelect = document.querySelector("#time-minute");
const dateInput = document.querySelector("#date");
const dateError = document.querySelector("#date-error");

for (let i = 0; i < 24; i++) {
  const option = document.createElement("option");
  option.value = String(i).padStart(2, "0");
  option.textContent = String(i).padStart(2, "0");
  hourSelect.appendChild(option);
}

for (let i = 0; i < 60; i += 15) {
  const option = document.createElement("option");
  option.value = String(i).padStart(2, "0");
  option.textContent = String(i).padStart(2, "0");
  minuteSelect.appendChild(option);
}

document.querySelector("#pickup-search").addEventListener("click", () => {
  searchAddress("#pickup-postcode", "#pickup-address");
});

document.querySelector("#destination-search").addEventListener("click", () => {
  searchAddress("#destination-postcode", "#destination-address");
});

function formatDateValue(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");

  return `${year}-${month}-${day}`;
}

function isPastOrToday(value) {
  if (!value) {
    return false;
  }

  const todayValue = formatDateValue(new Date());

  return value <= todayValue;
}

function validateRideDate() {
  if (!isPastOrToday(dateInput.value)) {
    dateError.textContent = "";
    dateInput.setCustomValidity("");
    return true;
  }

  const message = "明日以降の日付を選択してください。";

  dateError.textContent = message;
  dateInput.setCustomValidity(message);
  return false;
}

dateInput.addEventListener("change", validateRideDate);

form.addEventListener("submit", (event) => {
  event.preventDefault();

  if (!validateRideDate()) {
    dateInput.reportValidity();
    return;
  }

  const params = new URLSearchParams({
    pickup: document.querySelector("#pickup-address").value,
    destination: document.querySelector("#destination-address").value,
    date: document.querySelector("#date").value,
    hour: document.querySelector("#time-hour").value,
    minute: document.querySelector("#time-minute").value,
    passengers: document.querySelector("#passengers").value,
    care: document.querySelector("#care").value,
    notes: document.querySelector("#notes").value
  });

  window.location.href = `reservation_confirm.html?${params.toString()}`;
});

const today = new Date();
const tomorrow = new Date(today);
tomorrow.setDate(today.getDate() + 1);
dateInput.value = formatDateValue(tomorrow);
