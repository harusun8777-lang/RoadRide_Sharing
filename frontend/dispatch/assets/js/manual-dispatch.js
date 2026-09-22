const reason = new URLSearchParams(window.location.search).get("reason") || "ai-failed";
const reasonStatus = document.querySelector("#reason-status");
const pageDescription = document.querySelector("#page-description");
const selectionCount = document.querySelector("#selection-count");
const capacityValue = document.querySelector("#capacity-value");
const reservationItems = document.querySelectorAll(".reservation-item");
const checkboxes = document.querySelectorAll("[data-reservation]");
const confirmButton = document.querySelector("#confirm-manual");
const departureInput = document.querySelector("#departure-input");

function addMinutesToTime(timeValue, deltaMinutes) {
  if (!timeValue) return "09:55";

  const [hours, minutes] = timeValue.split(":").map(Number);
  const totalMinutes = ((hours * 60 + minutes + deltaMinutes) % 1440 + 1440) % 1440;
  const nextHours = Math.floor(totalMinutes / 60);
  const nextMinutes = totalMinutes % 60;

  return `${String(nextHours).padStart(2, "0")}:${String(nextMinutes).padStart(2, "0")}`;
}

document.querySelectorAll("[data-time-step]").forEach((button) => {
  button.addEventListener("click", () => {
    departureInput.value = addMinutesToTime(departureInput.value, Number(button.dataset.timeStep));
    departureInput.dispatchEvent(new Event("change", { bubbles: true }));
  });
});

if (reason === "no-candidate") {
  reasonStatus.textContent = "候補なし";
  pageDescription.textContent = "AIが乗合候補を作成できなかったため、担当者が配車内容を設定します。";
}

function updateSelection() {
  let passengerCount = 0;
  let selectedCount = 0;
  checkboxes.forEach((checkbox) => {
    const item = checkbox.closest(".reservation-item");
    item.classList.toggle("is-selected", checkbox.checked);
    if (checkbox.checked) {
      selectedCount += 1;
      passengerCount += Number(checkbox.dataset.reservation) === 2 ? 2 : 1;
    }
  });
  selectionCount.textContent = `${selectedCount}件選択中`;
  capacityValue.textContent = `${passengerCount}名 / 4名`;
}

checkboxes.forEach((checkbox) => checkbox.addEventListener("change", updateSelection));

confirmButton.addEventListener("click", () => {
  if (document.querySelectorAll("[data-reservation]:checked").length === 0) {
    window.alert("配車する予約を1件以上選択してください。");
    return;
  }
  if (window.confirm("この内容で手動配車を確定しますか？")) {
    confirmButton.textContent = "手動配車を確定しました";
    confirmButton.disabled = true;
  }
});

updateSelection();
