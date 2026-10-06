// Edytor aktywnych obszarów — zaznaczanie prostokąta myszą (WF-27). Wyłącznie usprawnienie:
// formularz współrzędnych działa bez tego pliku i w całości z klawiatury.
(function () {
  'use strict';

  var edytor = document.querySelector('[data-edytor-obszarow]');
  var obraz = edytor && edytor.querySelector('img');
  var pole = document.getElementById('Wspolrzedne');
  var ksztalt = document.getElementById('Ksztalt');
  var szerokosc = edytor && parseInt(edytor.dataset.szerokosc, 10);
  var wysokosc = edytor && parseInt(edytor.dataset.wysokosc, 10);
  if (!obraz || !pole || !ksztalt || !szerokosc || !wysokosc) { return; }

  document.getElementById('instrukcja-zaznaczania').hidden = false;

  var ramka = document.createElement('div');
  ramka.className = 'edytor-obszarow__zaznaczenie';
  ramka.hidden = true;
  edytor.appendChild(ramka);

  // Piksele zdjęcia ↔ piksele na ekranie (zdjęcie bywa pomniejszone przez max-width: 100%).
  function skala() { return obraz.clientWidth / szerokosc; }

  function naZdjecie(e) {
    var r = obraz.getBoundingClientRect();
    var s = skala();
    return {
      x: Math.round(Math.min(Math.max(e.clientX - r.left, 0), r.width) / s),
      y: Math.round(Math.min(Math.max(e.clientY - r.top, 0), r.height) / s)
    };
  }

  function pokaz(x1, y1, x2, y2) {
    var s = skala();
    ramka.style.left = (obraz.offsetLeft + x1 * s) + 'px';
    ramka.style.top = (obraz.offsetTop + y1 * s) + 'px';
    ramka.style.width = ((x2 - x1) * s) + 'px';
    ramka.style.height = ((y2 - y1) * s) + 'px';
    ramka.hidden = false;
  }

  // Podgląd prostokąta wpisanego z klawiatury — także po zmianie pola i rozmiaru okna.
  function pokazZPola() {
    var l = pole.value.split(',').map(function (c) { return parseInt(c, 10); });
    if (ksztalt.value === 'rect' && l.length === 4 && l.every(isFinite) && l[0] < l[2] && l[1] < l[3]) {
      pokaz(l[0], l[1], l[2], l[3]);
    } else {
      ramka.hidden = true;
    }
  }

  var start = null;
  obraz.addEventListener('pointerdown', function (e) {
    if (e.button !== 0) { return; }
    e.preventDefault(); // bez przeciągania obrazu jako pliku
    start = naZdjecie(e);
    obraz.setPointerCapture(e.pointerId);
  });

  obraz.addEventListener('pointermove', function (e) {
    if (!start) { return; }
    var p = naZdjecie(e);
    pokaz(Math.min(start.x, p.x), Math.min(start.y, p.y), Math.max(start.x, p.x), Math.max(start.y, p.y));
  });

  obraz.addEventListener('pointerup', function (e) {
    if (!start) { return; }
    var p = naZdjecie(e);
    var x1 = Math.min(start.x, p.x), y1 = Math.min(start.y, p.y), x2 = Math.max(start.x, p.x), y2 = Math.max(start.y, p.y);
    start = null;
    if (x2 - x1 < 2 || y2 - y1 < 2) { pokazZPola(); return; } // pojedyncze kliknięcie, nie zaznaczenie

    ksztalt.value = 'rect';
    pole.value = [x1, y1, x2, y2].join(',');
    pokazZPola();
    if (window.przewodnik) {
      window.przewodnik.oglos('Zaznaczono prostokąt. Współrzędne: ' + pole.value + '.');
    }
  });

  pole.addEventListener('input', pokazZPola);
  ksztalt.addEventListener('change', pokazZPola);
  window.addEventListener('resize', pokazZPola);
  if (obraz.complete) { pokazZPola(); } else { obraz.addEventListener('load', pokazZPola); }
})();
