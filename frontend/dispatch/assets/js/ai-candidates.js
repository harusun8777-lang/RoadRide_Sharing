const candidateDetails = {
  1: { label: "候補 A", departure: "9:55", passengers: "2名 / 4名", warning: "鈴木 一郎さんに大きな荷物があります。車両の積載スペースを確認してください。" },
  2: { label: "候補 B", departure: "10:05", passengers: "1名 / 4名", warning: "この候補には配慮事項の不一致はありません。" }
};

const candidateCards = document.querySelectorAll(".candidate-card");
const selectedLabel = document.querySelector("#selected-label");
const departureTime = document.querySelector("#departure-time");
const passengerCount = document.querySelector("#passenger-count");
const warningBox = document.querySelector("#warning-box");
const confirmButton = document.querySelector("#confirm-candidate");
const editCandidateButton = document.querySelector("#edit-candidate");
let selectedCandidate = 1;

function selectCandidate(candidateId) {
  const details = candidateDetails[candidateId];
  if (!details) return;

  selectedCandidate = candidateId;
  candidateCards.forEach((card) => card.classList.toggle("is-selected", card.dataset.candidate === String(candidateId)));
  selectedLabel.textContent = details.label;
  departureTime.textContent = details.departure;
  passengerCount.textContent = details.passengers;
  warningBox.querySelector("p").textContent = details.warning;
}

function openRouteEdit() {
  window.location.href = `route-edit.html?candidate=${selectedCandidate}`;
}

document.querySelectorAll("[data-select]").forEach((button) => {
  button.addEventListener("click", () => selectCandidate(Number(button.dataset.select)));
});

if (confirmButton) {
  confirmButton.addEventListener("click", () => {
    const details = candidateDetails[selectedCandidate];
    if (window.confirm(`${details.label}を確定しますか？`)) {
      confirmButton.textContent = "確定しました";
      confirmButton.disabled = true;
      warningBox.classList.add("is-confirmed");
      warningBox.querySelector("strong").textContent = "配車候補を確定しました";
    }
  });
}

if (editCandidateButton) {
  editCandidateButton.addEventListener("click", openRouteEdit);
}

function openManualDispatch(reason) {
  const reservationId = new URLSearchParams(window.location.search).get("id") || "reservation-001";
  window.location.href = `manual-dispatch.html?reason=${reason}&id=${encodeURIComponent(reservationId)}`;
}

const aiFailedButton = document.querySelector("#ai-failed-button");
const noCandidateButton = document.querySelector("#no-candidate-button");

if (aiFailedButton) {
  aiFailedButton.addEventListener("click", () => openManualDispatch("ai-failed"));
}

if (noCandidateButton) {
  noCandidateButton.addEventListener("click", () => openManualDispatch("no-candidate"));
}
