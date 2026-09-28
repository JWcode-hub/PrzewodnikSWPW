// Ekran miejsca — wyłącznie usprawnienia. Bez tego pliku spacer działa w pełni (kierunki to zwykłe linki).
(function () {
  'use strict';

  // 1. Po załadowaniu fokus na nazwie miejsca — czytnik zaczyna od nowego miejsca (06 §3.1).
  var naglowek = document.getElementById('naglowek-miejsca');
  if (naglowek) {
    naglowek.focus();
  }

  // 2. Skróty kierunków — decyzja D-02: zawsze z Alt, nigdy gołe litery ani strzałki (P-08);
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
})();
