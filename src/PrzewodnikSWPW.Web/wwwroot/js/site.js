// Multimedialny przewodnik po Uczelni — skrypty usprawniające.
// Progressive enhancement: każda funkcja działa także bez tego pliku (CLAUDE.md, zasada 7).
(function () {
  'use strict';

  var KLUCZ_USTAWIEN = 'przewodnik.ustawienia';
  var html = document.documentElement;

  // Ogłoszenie dla czytnika ekranu przez jedyny obszar aria-live (06 §3.2).
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

  // Strony, które same nie zarządzają fokusem, oznaczają element docelowy atrybutem (np. nagłówek wyniku trasy).
  var doFokusu = document.querySelector('[data-fokus-po-zaladowaniu]');
  if (doFokusu) {
    doFokusu.focus();
  }

  // Komunikat wyrenderowany przez serwer (np. „Ustawienia zostały zapisane.”) ogłaszamy po załadowaniu.
  var doOgloszenia = document.querySelector('[data-oglos]');
  if (doOgloszenia) {
    oglos(doOgloszenia.getAttribute('data-oglos'), doOgloszenia.hasAttribute('data-oglos-pilne'));
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
