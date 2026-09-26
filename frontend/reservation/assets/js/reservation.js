// 乗車予約の入力画面。入力値を URL パラメーターで確認画面へ渡す（API への登録は確認画面で行う）
(() => {
  if (!RoadRideAuth.requireAuth()) return;

  const Api = RoadRideReservationApi;
  const form = document.querySelector("#reservation-form");
  const pickupInput = document.querySelector("#pickup-address");
  const destinationInput = document.querySelector("#destination-address");
  const hourSelect = document.querySelector("#time-hour");
  const minuteSelect = document.querySelector("#time-minute");
  const dateInput = document.querySelector("#date");
  const dateError = document.querySelector("#date-error");
  const passengersSelect = document.querySelector("#passengers");
  const careSelect = document.querySelector("#care");
  const notesInput = document.querySelector("#notes");

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

  // 乗車日は日本時間の明日以降
  function validateRideDate() {
    if (!dateInput.value || dateInput.value > Api.tokyoDateAfter(0)) {
      dateError.textContent = "";
      dateInput.setCustomValidity("");
      return true;
    }

    const message = "明日以降の日付を選択してください。";

    dateError.textContent = message;
    dateInput.setCustomValidity(message);
    return false;
  }

  // 空白だけの入力は未入力として扱う
  function validateRequiredText(input, message) {
    input.setCustomValidity(input.value.trim() ? "" : message);
    return input.value.trim() !== "";
  }

  dateInput.addEventListener("change", validateRideDate);
  pickupInput.addEventListener("input", () => pickupInput.setCustomValidity(""));
  destinationInput.addEventListener("input", () => destinationInput.setCustomValidity(""));

  function setSelectValue(select, value) {
    if (value && Array.from(select.options).some((option) => option.value === value)) {
      select.value = value;
    }
  }

  // 確認画面の「入力内容を修正する」から戻ったときは、URL パラメーターの値を入れ直す
  function restoreForm() {
    const formValue = Api.formFromParams(new URLSearchParams(window.location.search));

    pickupInput.value = formValue.pickup;
    destinationInput.value = formValue.destination;
    dateInput.value = /^\d{4}-\d{2}-\d{2}$/.test(formValue.date) ? formValue.date : Api.tokyoDateAfter(1);
    setSelectValue(hourSelect, formValue.hour);
    setSelectValue(minuteSelect, formValue.minute);
    setSelectValue(passengersSelect, formValue.passengers);
    setSelectValue(careSelect, formValue.care);
    notesInput.value = formValue.notes;

    if (formValue.date) validateRideDate();
  }

  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const validPickup = validateRequiredText(pickupInput, "乗車場所を入力してください。");
    const validDestination = validateRequiredText(destinationInput, "目的地を入力してください。");
    const validDate = validateRideDate();

    if (!validPickup || !validDestination || !validDate) {
      form.reportValidity();
      return;
    }

    const params = Api.formToParams({
      pickup: pickupInput.value.trim(),
      destination: destinationInput.value.trim(),
      date: dateInput.value,
      hour: hourSelect.value,
      minute: minuteSelect.value,
      passengers: passengersSelect.value,
      care: careSelect.value,
      notes: notesInput.value.trim()
    });

    window.location.href = `reservation_confirm.html?${params.toString()}`;
  });

  restoreForm();
})();
