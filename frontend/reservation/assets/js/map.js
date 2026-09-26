function initMaps() {
  initMap("pickup-map", "pickup-address");
  initMap("destination-map", "destination-address");
}

function initMap(mapId, inputId) {
  const defaultPos = { lat: 35.170915, lng: 136.881537 };

  const map = new google.maps.Map(document.getElementById(mapId), {
    center: defaultPos,
    zoom: 15,
    disableDefaultUI: true,
  });

  let marker = null;

  map.addListener("click", async (event) => {
    const lat = event.latLng.lat();
    const lng = event.latLng.lng();

    if (marker) marker.setMap(null);
    marker = new google.maps.Marker({
      position: { lat, lng },
      map,
    });

    const geocoder = new google.maps.Geocoder();
    const result = await geocoder.geocode({ location: { lat, lng } });

    let address = "";
    if (result && result.results && result.results.length > 0) {
      address = result.results[0].formatted_address;
    }

    document.getElementById(inputId).value = address;
  });
}
