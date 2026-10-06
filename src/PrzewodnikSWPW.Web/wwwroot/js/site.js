// Multimedialny przewodnik po Uczelni — skrypty usprawniające.
// Progressive enhancement: każda funkcja działa także bez tego pliku (CLAUDE.md, zasada 7).
(function () {
  'use strict';

  var KLUCZ_USTAWIEN = 'przewodnik.ustawienia';
  var html = document.documentElement;

  // ZASADA OGŁASZANIA — jedna dla całego serwisu (docs/RAPORT_CZYTNIK.md rozdz. 2):
  //
  // 1. PEŁNE PRZEŁADOWANIE strony → obszar aria-live zostaje PUSTY. Wynik akcji niosą:
  //    - <title>, który czytnik odczytuje pierwszy („Zapisano – …”, „Błąd w formularzu – …”,
  //      „Brak przejścia – …”, „Znaleziono 3 sale…”) — działa także bez JavaScriptu,
  //    - element, na który trafia fokus (niżej): nagłówek <h1> nowego miejsca albo wyniku,
  //      widoczny komunikat o wyniku akcji albo pierwsze błędne pole formularza.
  //    Ten sam tekst wpisany dodatkowo do aria-live byłby czytany drugi raz, dlatego serwer
  //    nie renderuje nic „do ogłoszenia”, a podsumowanie błędów nie ma role="alert".
  //
  // 2. ZMIANA BEZ PRZEŁADOWANIA → oglos(). Tylko tu: podpowiedzi wyszukiwania (szukaj.js),
  //    Alt+P i skrót kierunku bez przejścia (spacer.js), zaznaczenie obszaru myszą (obszary.js).
  //
  // Jedyny obszar aria-live strony to #komunikaty w _Layout.cshtml (06 §3.2).
  function oglos(tekst, pilne) {
    var el = document.getElementById('komunikaty');
    if (!el) { return; }
    el.setAttribute('aria-live', pilne ? 'assertive' : 'polite');
    el.textContent = '';                                  // wymusza ponowne ogłoszenie
    setTimeout(function () { el.textContent = tekst; }, 50); // tego samego tekstu
  }
  window.przewodnik = { oglos: oglos };

  // Źródłem prawdy jest ciasteczko (serwer wpisuje ustawienia w atrybuty <html>).
  // localStorage to kopia dla skryptów klienta, np. mowy syntetycznej.
  function ustawieniaZDokumentu() {
    return {
      motyw: html.dataset.motyw,
      rozmiar: html.dataset.rozmiar,
      animacje: html.dataset.animacje,
      mowa: html.dataset.mowa,
      tempoMowy: html.dataset.tempoMowy,
      skroty: html.dataset.skroty
    };
  }

  function zapiszLokalnie(ustawienia) {
    try {
      localStorage.setItem(KLUCZ_USTAWIEN, JSON.stringify(ustawienia));
    } catch (e) {
      // Tryb prywatny lub zablokowane dane witryny — ustawienia i tak są w ciasteczku.
    }
  }

  zapiszLokalnie(ustawieniaZDokumentu());

  // Fokus po załadowaniu — jedyne miejsce, które go ustawia. Widok oznacza cel atrybutem
  // data-fokus-po-zaladowaniu; gdy oznaczonych jest kilka, wygrywa pierwszy w kodzie strony.
  // Pierwsze błędne pole ma atrybut autofocus (działa bez JavaScriptu) i ma pierwszeństwo. Ustawiamy
  // na nim fokus także tutaj: przeglądarka stosuje autofocus dopiero przy rysowaniu strony, więc
  // w karcie otwartej w tle fokus zostałby na początku dokumentu.
  var doFokusu = document.querySelector('[autofocus]') || document.querySelector('[data-fokus-po-zaladowaniu]');
  if (doFokusu) {
    doFokusu.focus();
  }

  // Panel ustawień: natychmiastowy podgląd wyglądu. Zapis na serwerze nadal wymaga przycisku.
  var formularz = document.getElementById('formularz-ustawien');
  if (formularz) {
    var info = document.getElementById('podglad-info');
    if (info) { info.hidden = false; }

    // ASP.NET dodaje do każdego pola wyboru ukryte pole „false” o tej samej nazwie — pytamy o sam checkbox.
    function zaznaczone(nazwa) {
      var pole = formularz.querySelector('input[type="checkbox"][name="' + nazwa + '"]');
      return !!(pole && pole.checked);
    }

    var MOTYWY = { Systemowy: 'systemowy', Podstawowy: 'podstawowy', WysokiKontrast: 'wysoki-kontrast', Ciemny: 'ciemny' };

    formularz.addEventListener('change', function () {
      var dane = new FormData(formularz);
      html.dataset.motyw = MOTYWY[dane.get('Motyw')] || 'systemowy';
      html.dataset.rozmiar = dane.get('RozmiarTekstu') || '100';
      html.dataset.animacje = zaznaczone('OgraniczAnimacje') ? 'ograniczone' : 'pelne';
      html.dataset.mowa = zaznaczone('MowaWlaczona') ? 'wlaczona' : 'wylaczona';
      html.dataset.tempoMowy = dane.get('TempoMowy') || '100';
      html.dataset.skroty = zaznaczone('SkrotyWlaczone') ? 'wlaczone' : 'wylaczone';
      zapiszLokalnie(ustawieniaZDokumentu());
    });
  }
})();
