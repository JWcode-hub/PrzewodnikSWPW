// Odczyt opisu miejsca mową syntetyczną (WF-35, 06 §4, decyzja D-11) — wyłącznie usprawnienie.
// Dla osób, które NIE korzystają z czytnika ekranu. Opis jest zawsze na stronie jako tekst.
//
// Czego ten plik nie robi, i tak ma zostać (ryzyko P-05 — dwa głosy naraz):
// - nie mówi sam z siebie: żadnego speak() przy ładowaniu strony, tylko przycisk albo Alt+P,
// - nie próbuje wykrywać czytnika ekranu — nie da się tego zrobić wiarygodnie; decyduje ustawienie.
(function () {
  'use strict';

  // Serwer renderuje panel tylko wtedy, gdy mowa jest włączona w /ustawienia. Brak panelu = koniec.
  var panel = document.getElementById('panel-mowy');
  if (!panel) { return; }

  var html = document.documentElement;
  var syntezator = window.speechSynthesis;
  var przyciskCzytaj = document.getElementById('btn-czytaj');
  var przyciskZatrzymaj = document.getElementById('btn-zatrzymaj');
  var komunikat = document.getElementById('mowa-komunikat');

  // Komunikat tekstowy pod przyciskami — nigdy cisza zamiast wyjaśnienia. Zmiana bez przeładowania
  // strony, więc trafia też do obszaru aria-live (zasada ogłaszania, site.js).
  function powiadom(tekst) {
    komunikat.textContent = tekst;
    komunikat.hidden = !tekst;
    if (tekst && window.przewodnik) {
      window.przewodnik.oglos(tekst);
    }
  }

  panel.hidden = false;

  if (!syntezator || !window.SpeechSynthesisUtterance) {
    przyciskCzytaj.hidden = true;
    przyciskZatrzymaj.hidden = true;
    powiadom('Twoja przeglądarka nie obsługuje mowy syntetycznej. Opis miejsca przeczytasz na stronie.');
    return;
  }

  // Chrome wczytuje listę głosów w tle: pierwsze getVoices() zwraca pustą listę, a gotowość
  // zgłasza zdarzenie voiceschanged. Pytamy już teraz, żeby lista była gotowa przed kliknięciem.
  var glosyGotowe = syntezator.getVoices().length > 0;
  var czekajacy = [];

  function poZmianieGlosow() {
    glosyGotowe = true;
    czekajacy.splice(0).forEach(function (dalej) { dalej(); });
  }

  if (syntezator.addEventListener) {
    syntezator.addEventListener('voiceschanged', poZmianieGlosow);
  } else {
    syntezator.onvoiceschanged = poZmianieGlosow; // starsze Safari: speechSynthesis nie jest EventTarget
  }

  // Wywołuje „dalej” dokładnie raz: gdy głosy są gotowe albo po 1,5 s (przeglądarka bez voiceschanged).
  function gdyGlosyGotowe(dalej) {
    if (glosyGotowe || syntezator.getVoices().length > 0) {
      glosyGotowe = true;
      dalej();
      return;
    }
    var wykonane = false;
    function raz() {
      if (wykonane) { return; }
      wykonane = true;
      clearTimeout(zegar);
      dalej();
    }
    var zegar = setTimeout(raz, 1500);
    czekajacy.push(raz);
  }

  // Wyłącznie polski głos; najpierw zainstalowany w systemie (działa bez sieci). Android podaje „pl_PL”.
  function glosPolski() {
    var polskie = syntezator.getVoices().filter(function (g) { return /^pl([-_]|$)/i.test(g.lang); });
    return polskie.filter(function (g) { return g.localService; })[0] || polskie[0] || null;
  }

  // Ustawienie to procenty (50–200); SpeechSynthesisUtterance.rate przyjmuje 0.5–2.0.
  function tempo() {
    var procent = parseInt(html.dataset.tempoMowy, 10);
    return Math.min(2, Math.max(0.5, (isFinite(procent) ? procent : 100) / 100));
  }

  function zatrzymaj() {
    syntezator.cancel();
  }

  // Wywoływane tylko z gestu użytkownika: przycisk „Przeczytaj opis” albo Alt+P (spacer.js).
  function czytaj() {
    if (html.dataset.mowa !== 'wlaczona') { return; }
    syntezator.cancel(); // każda nowa wypowiedź przerywa poprzednią

    gdyGlosyGotowe(function () {
      var glos = glosPolski();
      if (!glos) {
        // Bez polskiego głosu przeglądarka czytałaby polski tekst obcym głosem — wolimy komunikat.
        powiadom('Twoja przeglądarka nie ma zainstalowanego polskiego głosu. Opis miejsca przeczytasz na stronie.');
        return;
      }

      powiadom('');
      syntezator.cancel();
      var wypowiedz = new SpeechSynthesisUtterance(panel.dataset.tekst);
      wypowiedz.lang = 'pl-PL';
      wypowiedz.voice = glos;
      wypowiedz.rate = tempo();
      wypowiedz.volume = 1;
      syntezator.speak(wypowiedz);
    });
  }

  przyciskCzytaj.addEventListener('click', czytaj);
  przyciskZatrzymaj.addEventListener('click', zatrzymaj);

  // Głos nie może mówić dalej o miejscu, z którego użytkownik właśnie wyszedł.
  window.addEventListener('pagehide', zatrzymaj);

  if (window.przewodnik) {
    window.przewodnik.mowa = { czytaj: czytaj, zatrzymaj: zatrzymaj };
  }
})();
