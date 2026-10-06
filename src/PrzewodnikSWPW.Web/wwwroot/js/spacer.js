// Ekran miejsca — wyłącznie usprawnienia. Bez tego pliku spacer działa w pełni (kierunki to zwykłe linki).
(function () {
  'use strict';

  // Fokus na nazwie miejsca po załadowaniu ustawia site.js (atrybut data-fokus-po-zaladowaniu w widoku).

  // 1. Skróty kierunków — decyzja D-02: zawsze z Alt, nigdy gołe litery ani strzałki (P-08);
  //    kolejność cyfr jak na liście (D-01). Alt+S i Alt+T celowo nieprzypisane (kolizja z Firefoksem).
  var SKROTY = {
    Digit1: 'prosto', Numpad1: 'prosto',
    Digit2: 'lewo', Numpad2: 'lewo',
    Digit3: 'prawo', Numpad3: 'prawo',
    Digit4: 'tyl', Numpad4: 'tyl'
  };

  document.addEventListener('keydown', function (e) {
    // Wyłącznie sam lewy/prawy Alt. Ctrl+Alt (= AltGr w polskim układzie klawiatury), Shift i Meta pomijamy.
    if (!e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) { return; }
    // WCAG 2.1.4 — użytkownik może wyłączyć skróty w /ustawienia.
    if (document.documentElement.dataset.skroty === 'wylaczone') { return; }

    // e.code nie zależy od układu klawiatury (na macOS Option+2 daje e.key = „™”, ale e.code = „Digit2”).
    // Część klawiatur ekranowych i narzędzi wspomagających wysyła zdarzenia bez e.code — wtedy e.key.
    // Alt+P — powtórz opis miejsca (D-02): ogłoszenie przez obszar aria-live, czytnik ekranu przeczyta je od razu.
    if (e.code === 'KeyP' || (!e.code && (e.key === 'p' || e.key === 'P'))) {
      var opis = document.getElementById('opis-miejsca');
      if (opis && window.przewodnik) {
        e.preventDefault();
        window.przewodnik.oglos(opis.textContent.trim(), true);
      }
      return;
    }

    var kierunek = SKROTY[e.code] || SKROTY['Digit' + e.key];
    if (!kierunek) { return; }

    var pozycja = document.querySelector('.lista-kierunkow [data-kierunek="' + kierunek + '"]');
    if (!pozycja) { return; }
    e.preventDefault();

    var link = pozycja.querySelector('a');
    if (link) {
      link.click();
    } else if (window.przewodnik) {
      // Brak przejścia: nie przeładowujemy strony, tylko ogłaszamy opis przeszkody.
      window.przewodnik.oglos(pozycja.textContent.trim(), true);
    }
  });

  // 2. Aktywne obszary zdjęcia (WF-27): współrzędne są w pikselach zdjęcia, a zdjęcie bywa pomniejszone
  //    (max-width: 100%). Przeliczamy coords do wyświetlanego rozmiaru. Bez JavaScriptu obszary pasują
  //    tylko do zdjęcia w pełnym rozmiarze — lista kierunków działa zawsze.
  function przeskalujObszary() {
    document.querySelectorAll('img[usemap]').forEach(function (obraz) {
      var szerokosc = parseInt(obraz.getAttribute('width'), 10);
      var mapa = document.getElementById(obraz.getAttribute('usemap').slice(1));
      if (!szerokosc || !mapa || !obraz.clientWidth) { return; }
      var skala = obraz.clientWidth / szerokosc;
      mapa.querySelectorAll('area[data-wspolrzedne]').forEach(function (obszar) {
        obszar.coords = obszar.dataset.wspolrzedne.split(',')
          .map(function (c) { return Math.round(parseInt(c, 10) * skala); })
          .join(',');
      });
    });
  }

  window.addEventListener('resize', przeskalujObszary);
  window.addEventListener('load', przeskalujObszary);
  przeskalujObszary();
})();
