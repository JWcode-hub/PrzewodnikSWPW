using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PrzewodnikSWPW.Web.TagHelpers;

/// <summary>
/// Kompletne, dostępne pole formularza: <c>&lt;pole for="Nazwa" /&gt;</c>. Jedno miejsce, które gwarantuje
/// w KAŻDYM formularzu (WN-20, WCAG 1.3.1, 3.3.1–3.3.3):
/// <list type="bullet">
/// <item>widoczną etykietę &lt;label for&gt; (nigdy placeholder zamiast etykiety),</item>
/// <item>błąd jako tekst „Błąd: …” — nie sam kolor — powiązany przez aria-describedby, oraz aria-invalid,</item>
/// <item>zachowanie wpisanych danych po błędzie walidacji (wartość z ModelState),</item>
/// <item>autofocus na PIERWSZYM błędnym polu strony — fokus trafia na błąd także bez JavaScriptu.</item>
/// </list>
/// </summary>
[HtmlTargetElement("pole", Attributes = "for", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PoleTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string KluczAutofocus = "pole-autofocus-uzyty";

    [HtmlAttributeName("for")]
    public ModelExpression For { get; set; } = null!;

    /// <summary>text (domyślnie), textarea, checkbox, select, password, email, liczba, liczba-dziesietna, plik.</summary>
    [HtmlAttributeName("typ")]
    public string Typ { get; set; } = "text";

    [HtmlAttributeName("elementy")]
    public IEnumerable<SelectListItem>? Elementy { get; set; }

    /// <summary>Tekst pierwszej, pustej opcji listy, np. „— wybierz piętro —”.</summary>
    [HtmlAttributeName("pusta-opcja")]
    public string? PustaOpcja { get; set; }

    [HtmlAttributeName("wskazowka")]
    public string? Wskazowka { get; set; }

    [HtmlAttributeName("autocomplete")]
    public string? Autocomplete { get; set; }

    [HtmlAttributeName("wiersze")]
    public int Wiersze { get; set; } = 4;

    /// <summary>Wymusza oznaczenie „(wymagane)”, gdy pole jest wymagane regułą, a nie atrybutem [Required] (np. plik zdjęcia).</summary>
    [HtmlAttributeName("wymagane")]
    public bool? Wymagane { get; set; }

    /// <summary>Atrybut accept pola pliku — podpowiedź dla okna wyboru; o przyjęciu pliku decyduje serwer.</summary>
    [HtmlAttributeName("akceptuj")]
    public string? Akceptuj { get; set; }

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var nazwa = For.Name;
        var id = TagBuilder.CreateSanitizedId(nazwa, "_");
        var etykieta = For.Metadata.DisplayName ?? For.Metadata.PropertyName ?? nazwa;
        var checkbox = Typ == "checkbox";
        var wymagane = Wymagane ?? (For.Metadata.IsRequired && !checkbox);

        var blad = ViewContext.ViewData.ModelState.TryGetValue(nazwa, out var stan) && stan.Errors.Count > 0
            ? stan.Errors[0].ErrorMessage
            : null;
        var idWskazowki = Wskazowka is null ? null : $"{id}-wskazowka";
        var idBledu = blad is null ? null : $"{id}-blad";

        var atrybuty = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["class"] = checkbox ? "form-check-input" : Typ == "select" ? "form-select" : "form-control",
        };
        var opisane = string.Join(' ', new[] { idWskazowki, idBledu }.Where(x => x is not null));
        if (opisane.Length > 0) atrybuty["aria-describedby"] = opisane;
        if (blad is not null) atrybuty["aria-invalid"] = "true";
        if (wymagane) atrybuty["aria-required"] = "true";
        if (Autocomplete is not null) atrybuty["autocomplete"] = Autocomplete;
        if (blad is not null && ZajmijAutofocus(ViewContext.HttpContext))
        {
            atrybuty["autofocus"] = "autofocus";
        }

        var kontrolka = Typ switch
        {
            "textarea" => generator.GenerateTextArea(ViewContext, For.ModelExplorer, nazwa, Wiersze, 0, atrybuty),
            "checkbox" => generator.GenerateCheckBox(ViewContext, For.ModelExplorer, nazwa, null, atrybuty),
            "select" => generator.GenerateSelect(ViewContext, For.ModelExplorer, PustaOpcja, nazwa, Elementy ?? [], false, atrybuty),
            "password" => generator.GeneratePassword(ViewContext, For.ModelExplorer, nazwa, null, atrybuty),
            "plik" => PolePliku(nazwa, atrybuty),
            _ => generator.GenerateTextBox(ViewContext, For.ModelExplorer, nazwa, For.Model, null, Atrybuty(atrybuty)),
        };

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", checkbox ? "pole pole--wybor mb-3" : "pole mb-3");

        var etykietaHtml = new TagBuilder("label");
        etykietaHtml.Attributes["for"] = id;
        etykietaHtml.AddCssClass(checkbox ? "form-check-label" : "form-label");
        etykietaHtml.InnerHtml.Append(etykieta);
        if (wymagane) etykietaHtml.InnerHtml.Append(" (wymagane)");

        var tresc = output.Content;
        if (checkbox)
        {
            // Pole wyboru przed etykietą; ukryte „false”, bo niezaznaczony checkbox niczego nie wysyła.
            var wiersz = new TagBuilder("div");
            wiersz.AddCssClass("form-check");
            wiersz.InnerHtml.AppendHtml(kontrolka);
            wiersz.InnerHtml.AppendHtml(generator.GenerateHiddenForCheckbox(ViewContext, For.ModelExplorer, nazwa));
            wiersz.InnerHtml.AppendHtml(etykietaHtml);
            tresc.AppendHtml(wiersz);
        }
        else
        {
            tresc.AppendHtml(etykietaHtml);
        }

        if (Wskazowka is not null) tresc.AppendHtml($"<p id=\"{idWskazowki}\" class=\"wskazowka\">").Append(Wskazowka).AppendHtml("</p>");
        if (blad is not null) tresc.AppendHtml($"<p id=\"{idBledu}\" class=\"komunikat-bledu\">").Append($"Błąd: {blad}").AppendHtml("</p>");
        if (!checkbox) tresc.AppendHtml(kontrolka);
    }

    /// <summary>
    /// Zwraca true dokładnie raz na żądanie — dla pierwszego błędnego pola w kolejności kodu strony.
    /// Publiczne dla pól, których nie da się wyrazić znacznikiem &lt;pole&gt; (grupa przycisków opcji).
    /// </summary>
    public static bool ZajmijAutofocus(HttpContext kontekst) => kontekst.Items.TryAdd(KluczAutofocus, true);

    /// <summary>Pole pliku bez atrybutu value — przeglądarka i tak nie przyjmuje wartości początkowej pola pliku.</summary>
    private TagBuilder PolePliku(string nazwa, Dictionary<string, object?> atrybuty)
    {
        var pole = new TagBuilder("input") { TagRenderMode = TagRenderMode.SelfClosing };
        pole.Attributes["type"] = "file";
        pole.Attributes["name"] = nazwa;
        if (Akceptuj is not null) pole.Attributes["accept"] = Akceptuj;
        foreach (var (klucz, wartosc) in atrybuty) pole.Attributes[klucz] = wartosc?.ToString();
        return pole;
    }

    /// <summary>Typ pola tekstowego. Liczby dziesiętne jako tekst z inputmode — input type="number" nie przyjmuje przecinka.</summary>
    private Dictionary<string, object?> Atrybuty(Dictionary<string, object?> atrybuty)
    {
        switch (Typ)
        {
            case "email":
                atrybuty["type"] = "email";
                break;
            case "liczba-dziesietna":
                atrybuty["type"] = "text";
                atrybuty["inputmode"] = "decimal";
                break;
            case "liczba":
                atrybuty["type"] = "text";
                atrybuty["inputmode"] = "numeric";
                break;
            default:
                atrybuty["type"] = "text";
                break;
        }
        return atrybuty;
    }
}
