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

form.addEventListener("submit", (event) => {
  event.preventDefault();

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

const dateInput = document.querySelector("#date");
const today = new Date();
const yyyy = today.getFullYear();
const mm = String(today.getMonth() + 1).padStart(2, "0");
const dd = String(today.getDate()).padStart(2, "0");
dateInput.value = `${yyyy}-${mm}-${dd}`;
