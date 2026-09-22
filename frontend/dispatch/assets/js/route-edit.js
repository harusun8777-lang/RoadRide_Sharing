const candidateMap = {
  1: {
    label: "候補 A",
    departure: "09:55",
    members: [
      { id: 1, name: "佐藤 由美", pickup: "電波学園前", destination: "市役所", note: "配慮なし", tagClass: "note" },
      { id: 2, name: "鈴木 一郎", pickup: "中央公園入口", destination: "市役所", note: "大きな荷物", tagClass: "warning" }
    ]
  },
  2: {
    label: "候補 B",
    departure: "10:05",
    members: [
      { id: 2, name: "鈴木 一郎", pickup: "中央公園入口", destination: "市役所", note: "大きな荷物", tagClass: "warning" }
    ]
  }
};

const candidateId = Number(new URLSearchParams(window.location.search).get("candidate")) || 1;
const currentCandidate = candidateMap[candidateId] || candidateMap[1];
const riders = currentCandidate.members;

const state = {
  order: riders.map((rider) => rider.id),
  departure: currentCandidate.departure
};

const orderList = document.querySelector("#order-list");
const routeLine = document.querySelector("#route-line");
const departureInput = document.querySelector("#departure-edit");
const departureSummary = document.querySelector("#summary-departure");
const editStatus = document.querySelector("#edit-status");
const groupCount = document.querySelector("#group-count");

function addMinutesToTime(timeValue, deltaMinutes) {
  if (!timeValue) return "09:55";

  const [hours, minutes] = timeValue.split(":").map(Number);
  const totalMinutes = ((hours * 60 + minutes + deltaMinutes) % 1440 + 1440) % 1440;
  const nextHours = Math.floor(totalMinutes / 60);
  const nextMinutes = totalMinutes % 60;

  return `${String(nextHours).padStart(2, "0")}:${String(nextMinutes).padStart(2, "0")}`;
}

function findRiderById(id) {
  return riders.find((rider) => rider.id === id);
}

function moveRider(index, direction) {
  const targetIndex = index + direction;
  if (targetIndex < 0 || targetIndex >= state.order.length) {
    return;
  }

  const newOrder = [...state.order];
  [newOrder[index], newOrder[targetIndex]] = [newOrder[targetIndex], newOrder[index]];
  state.order = newOrder;
  render();
}

function render() {
  groupCount.textContent = `${state.order.length}名`;
  departureInput.value = state.departure;
  departureSummary.textContent = state.departure;

  orderList.innerHTML = state.order
    .map((riderId, index) => {
      const rider = findRiderById(riderId);
      if (!rider) {
        return "";
      }

      return `
        <div class="order-item ${index === 0 ? "is-priority" : ""}">
          <span class="order-index">${index + 1}</span>
          <div class="order-main">
            <strong>${rider.name}</strong>
            <div class="order-meta">
              <span>${rider.pickup}</span>
              <span>→</span>
              <span>${rider.destination}</span>
              <span class="order-tag ${rider.tagClass}">${rider.note}</span>
            </div>
          </div>
          <div class="order-actions">
            <button class="order-button" type="button" data-action="up" data-index="${index}" ${index === 0 ? "disabled" : ""}>上へ</button>
            <button class="order-button" type="button" data-action="down" data-index="${index}" ${index === state.order.length - 1 ? "disabled" : ""}>下へ</button>
          </div>
        </div>
      `;
    })
    .join("");

  routeLine.innerHTML = "";
  state.order.forEach((riderId, index) => {
    const rider = findRiderById(riderId);
    if (!rider) {
      return;
    }

    const stop = document.createElement("div");
    stop.className = "route-stop";
    stop.innerHTML = `<small>乗車 ${index + 1}</small><strong>${rider.pickup}</strong>`;
    routeLine.appendChild(stop);

    if (index < state.order.length - 1) {
      const arrow = document.createElement("span");
      arrow.textContent = "→";
      routeLine.appendChild(arrow);
    }
  });

  const destinationStop = document.createElement("div");
  destinationStop.className = "route-stop";
  destinationStop.innerHTML = "<small>降車</small><strong>市役所</strong>";
  routeLine.appendChild(destinationStop);

  document.querySelectorAll("[data-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const direction = button.dataset.action === "up" ? -1 : 1;
      moveRider(Number(button.dataset.index), direction);
    });
  });
}

document.querySelectorAll("[data-time-step]").forEach((button) => {
  button.addEventListener("click", () => {
    departureInput.value = addMinutesToTime(departureInput.value, Number(button.dataset.timeStep));
    state.departure = departureInput.value;
    departureSummary.textContent = state.departure;
    editStatus.textContent = "未反映";
    editStatus.classList.remove("status-success");
    editStatus.classList.add("status-info");
  });
});

document.querySelector("#apply-edit").addEventListener("click", () => {
  const nextDeparture = departureInput.value || state.departure;
  state.departure = nextDeparture;
  editStatus.textContent = "反映済み";
  editStatus.classList.remove("status-info");
  editStatus.classList.add("status-success");
  render();
  window.alert("ルートと乗車順を反映しました。");
});

document.querySelector("#back-candidate").addEventListener("click", () => {
  window.history.back();
});

departureInput.addEventListener("change", () => {
  state.departure = departureInput.value || state.departure;
  departureSummary.textContent = state.departure;
  editStatus.textContent = "未反映";
  editStatus.classList.remove("status-success");
  editStatus.classList.add("status-info");
});

render();
