// Wyszukiwanie sal — wyłącznie usprawnienia. Bez tego pliku formularz działa zwykłym POST.
// Wzorzec: ARIA 1.2 combobox z listą podpowiedzi (listbox), fokus zostaje w polu tekstowym,
// aktywną podpowiedź wskazuje aria-activedescendant.
(function () {
  'use strict';

  var przewodnik = window.przewodnik || { oglos: function () { } };

  // Fokus na nagłówku z liczbą wyników (WF-13) ustawia site.js (atrybut data-fokus-po-zaladowaniu w widoku).

  // Combobox z podpowiedziami.
  var pole = document.getElementById('pole-szukaj');
  if (!pole || !window.fetch) { return; }

  var lista = document.createElement('ul');
  lista.id = 'lista-podpowiedzi';
  lista.className = 'lista-podpowiedzi';
  lista.setAttribute('role', 'listbox');
  lista.setAttribute('aria-label', 'Podpowiedzi sal');
  lista.hidden = true;
  pole.parentNode.appendChild(lista);

  pole.setAttribute('role', 'combobox');
  pole.setAttribute('aria-autocomplete', 'list');
  pole.setAttribute('aria-expanded', 'false');
  pole.setAttribute('aria-controls', lista.id);

  var podpowiedzi = [];
  var aktywna = -1;
  var zegar = null;
  var biezaceZapytanie = null;

  function zamknij() {
    lista.hidden = true;
    pole.setAttribute('aria-expanded', 'false');
    pole.removeAttribute('aria-activedescendant');
    aktywna = -1;
  }

  function ustawAktywna(indeks) {
    var elementy = lista.children;
    if (aktywna >= 0 && elementy[aktywna]) {
      elementy[aktywna].setAttribute('aria-selected', 'false');
    }
    aktywna = indeks;
    if (aktywna >= 0 && elementy[aktywna]) {
      elementy[aktywna].setAttribute('aria-selected', 'true');
      pole.setAttribute('aria-activedescendant', elementy[aktywna].id);
      elementy[aktywna].scrollIntoView({ block: 'nearest' });
    } else {
      pole.removeAttribute('aria-activedescendant');
    }
  }

  function przejdzDo(indeks) {
    if (podpowiedzi[indeks]) {
      window.location.href = podpowiedzi[indeks].adres;
    }
  }

  function pokaz(elementy) {
    podpowiedzi = elementy;
    lista.textContent = '';
    aktywna = -1;

    if (elementy.length === 0) {
      zamknij();
      przewodnik.oglos('Brak podpowiedzi. Naciśnij Enter, aby wyszukać.');
      return;
    }

    elementy.forEach(function (p, i) {
      var li = document.createElement('li');
      li.id = 'podpowiedz-' + i;
      li.setAttribute('role', 'option');
      li.setAttribute('aria-selected', 'false');
      li.textContent = p.tekst;
      // mousedown zamiast click: pole nie traci fokusa przed nawigacją.
      li.addEventListener('mousedown', function (e) {
        e.preventDefault();
        przejdzDo(i);
      });
      lista.appendChild(li);
    });

    lista.hidden = false;
    pole.setAttribute('aria-expanded', 'true');
    var n = elementy.length;
    // „podpowiedzi” jest i mianownikiem liczby mnogiej (2–4), i dopełniaczem (5+) — różni się tylko 1.
    przewodnik.oglos(n + (n === 1 ? ' podpowiedź' : ' podpowiedzi') +
      '. Strzałki w górę i w dół przełączają podpowiedzi.');
  }

  function pobierz() {
    var fraza = pole.value.trim();
    if (fraza.length === 0) {
      zamknij();
      return;
    }

    if (biezaceZapytanie) { biezaceZapytanie.abort(); }
    biezaceZapytanie = new AbortController();

    fetch('/szukaj/podpowiedzi?q=' + encodeURIComponent(fraza), { signal: biezaceZapytanie.signal })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(pokaz)
      .catch(function (e) {
        if (e.name !== 'AbortError') { zamknij(); } // błąd sieci: formularz nadal działa bez podpowiedzi
      });
  }

  pole.addEventListener('input', function () {
    clearTimeout(zegar);
    zegar = setTimeout(pobierz, 250);
  });

  pole.addEventListener('keydown', function (e) {
    var otwarta = !lista.hidden;
    switch (e.key) {
      case 'ArrowDown':
        if (!otwarta) {
          if (podpowiedzi.length > 0) { lista.hidden = false; pole.setAttribute('aria-expanded', 'true'); ustawAktywna(0); }
          else { pobierz(); }
        } else {
          ustawAktywna(Math.min(aktywna + 1, podpowiedzi.length - 1));
        }
        e.preventDefault();
        break;
      case 'ArrowUp':
        if (otwarta) {
          ustawAktywna(Math.max(aktywna - 1, 0));
          e.preventDefault();
        }
        break;
      case 'Enter':
        // Wybrana podpowiedź → od razu karta sali. Bez wybranej — zwykłe wysłanie formularza.
        if (otwarta && aktywna >= 0) {
          e.preventDefault();
          przejdzDo(aktywna);
        }
        break;
      case 'Escape':
        if (otwarta) {
          e.preventDefault();
          zamknij();
        }
        break;
      case 'Tab':
        zamknij();
        break;
    }
  });

  pole.addEventListener('blur', function () {
    setTimeout(zamknij, 150);
  });
})();
