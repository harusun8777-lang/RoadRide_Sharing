const actionCards = document.querySelectorAll(".action-card");

actionCards.forEach((card) => {
  card.addEventListener("pointerdown", () => {
    card.classList.add("is-pressed");
  });

  card.addEventListener("pointerleave", () => {
    card.classList.remove("is-pressed");
  });

  card.addEventListener("pointerup", () => {
    card.classList.remove("is-pressed");
  });
});
