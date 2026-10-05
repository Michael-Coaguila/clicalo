using System.Text.RegularExpressions;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

/// <summary>
/// A hand-reviewed golden of every handoff text with placeholders, independent of the import recipe
/// (data/i18n/handoff-import.json). <see cref="VisibleTextSnapshotTests"/> derives the letter → name mapping from the
/// recipe, so it only catches drift between the recipe and data/i18n: a recipe that maps a letter to the wrong name
/// (for example <c>phStep</c> as «Paso {total} de {index}») passes it. Here each argument is named by reading the
/// original sentence, and the expected text is written out by hand, so a swapped or wrong mapping fails.
/// </summary>
/// <remarks>
/// Sample values are all different (index 2, total 5, count 3), so two placeholders that trade places change the
/// text. Plural families are checked in their <c>one</c> and <c>other</c> forms. When the
/// user ratifies a text change (catalog §6.1 and §9), this golden changes in the same pull request.
/// </remarks>
public sealed partial class HandoffTextGoldenTests
{
    private static readonly GoldenText[] Golden =
    [
        new("quotaFree", [Count(3)], "IA gratis: 3 de 5 hoy", "Free AI: 3 of 5 today"),
        new(
            "sharedProf",
            [Profile()],
            "Enlace de «Informes» copiado",
            "Link for “Informes” copied"
        ),
        new(
            "rollbackT",
            [MessageArgument.Text("version", "2.0.1")],
            "Volver a la versión 2.0.1",
            "Go back to version 2.0.1"
        ),
        new(
            "stepEditMsg",
            [Index()],
            "Editando las teclas del paso 2",
            "Editing the keys of step 2"
        ),
        new(
            "vh2",
            [Index(), MessageArgument.Text("name", "Negrita")],
            "Con el panel visible, di «clic 2» o «clic Negrita».",
            "With the panel visible, say “click 2” or “click Negrita”."
        ),
        new("lcFor", [Profile()], "Para Informes", "For Informes"),
        new("panicMsg", [Keys()], "Pulsado: Ctrl + B", "Held: Ctrl + B"),
        new(
            "releasedAuto",
            [Count(3)],
            "Se soltaron solas tras 3 s por seguridad",
            "Released automatically after 3 s for safety"
        ),
        new(
            "adminMsg",
            [App()],
            "Word se ejecuta como administrador. Windows no deja enviarle atajos si Clícalo no lo es también.",
            "Word runs as administrator. Windows won’t let Clícalo send shortcuts to it unless Clícalo is too."
        ),
        new(
            "emptyProfS",
            [Profile()],
            "Añade el primero para «Informes».",
            "Add the first one for “Informes”."
        ),
        new("createFor", [App()], "Crear para Word", "Create for Word"),
        new("phStep", [Index(), Total()], "Paso 2 de 5", "Step 2 of 5"),
        new("delFrom", [Profile()], "Eliminar de Informes", "Delete from Informes"),
        new(
            "twMacro",
            [Count(3)],
            "Ejecuta 3 pasos, uno tras otro.",
            "Runs 3 steps, one after another."
        ),
        new("twMacro", [Count(1)], "Ejecuta 1 paso.", "Runs 1 step."),
        new("testLive2", [App()], "Probar ahora en Word", "Test now in Word"),
        new(
            "testAskQ",
            [App()],
            "¿Hizo lo esperado en Word?",
            "Did it do what you expected in Word?"
        ),
        new("switching", [App()], "Cambiando a Word…", "Switching to Word…"),
        new("linkedT", [App()], "Se activa solo con Word", "Switches on with Word"),
        new(
            "unknownMsg",
            [App()],
            "No encontré atajos fiables para «Word». Te muestro atajos comunes que suelen funcionar, pero revísalos.",
            "I couldn’t find reliable shortcuts for “Word”. Here are common shortcuts that usually work, but check them."
        ),
        new("unknownBlank", [App()], "Mejor, crear «Word» vacío", "Rather, create “Word” empty"),
        new(
            "replaceMsg",
            [Keys()],
            "Reemplazando Ctrl + B. Toca las teclas nuevas; los cambios se guardan solos.",
            "Replacing Ctrl + B. Tap the new keys; changes save automatically."
        ),
        new("comboN", [Count(3)], "3 teclas · se guarda solo", "3 keys · auto-saved"),
        new("comboN", [Count(1)], "1 tecla · se guarda solo", "1 key · auto-saved"),
        new(
            "pinAllOff2",
            [Profile()],
            "Ahora solo aparece cuando usas «Informes». Actívalo para verlo siempre, en la fila fija de arriba del panel.",
            "Now it only shows when you use “Informes”. Turn on to always see it in the fixed row at the top of the panel."
        ),
        new(
            "testWhat",
            [Keys(), App()],
            "Al tocarlo en el panel se enviará Ctrl + B a Word.",
            "Tapping it on the panel will send Ctrl + B to Word."
        ),
        new("testLive", [App()], "Probar de verdad en Word", "Really test in Word"),
        new(
            "pinAllOff",
            [Profile()],
            "Ahora solo aparece en «Informes». Si lo activas, pasa a la fila «Siempre visible» de cualquier app.",
            "Now it only shows in “Informes”. Turn on to move it to the “Always visible” row in every app."
        ),
        new(
            "dupHead",
            [Count(3), Index(), Total()],
            "Combinación repetida en 3 sitios · 2 de 5",
            "Combo repeated in 3 places · 2 of 5"
        ),
        new(
            "dupHead",
            [Count(1), Index(), Total()],
            "Combinación repetida en 1 sitio · 2 de 5",
            "Combo repeated in 1 place · 2 of 5"
        ),
        new("dupStep", [Index(), Total()], "Repetida 2 de 5", "Duplicate 2 of 5"),
        new(
            "instNoteSome",
            [Count(3)],
            "Ya instalado. Faltan 3 atajos de esta plantilla.",
            "Installed. 3 shortcuts from this template are missing."
        ),
        new(
            "instNoteSome",
            [Count(1)],
            "Ya instalado. Falta 1 atajo de esta plantilla.",
            "Installed. 1 shortcut from this template is missing."
        ),
        new("addMissing", [Count(3)], "Añadir 3 que faltan", "Add 3 missing"),
        new("addMissing", [Count(1)], "Añadir 1 que falta", "Add 1 missing"),
        new(
            "sugLine",
            [App(), Count(3)],
            "Tienes Word abierto · 3 atajos listos",
            "Word is open · 3 shortcuts ready"
        ),
        new(
            "sugLine",
            [App(), Count(1)],
            "Tienes Word abierto · 1 atajo listo",
            "Word is open · 1 shortcut ready"
        ),
        new(
            "libHint",
            [Profile()],
            "Toca una acción para añadirla a «Informes». No hace falta escribir nada.",
            "Tap an action to add it to “Informes”. No typing needed."
        ),
    ];

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-001")]
    [Trait("Req", "IDI-004")]
    public void Every_handoff_text_with_placeholders_matches_the_hand_reviewed_golden(
        string language
    )
    {
        var localizer = I18nRepository.Localizer(language);
        var mismatches = new List<string>();

        foreach (var golden in Golden)
        {
            var expected = string.Equals(language, "es", StringComparison.Ordinal)
                ? golden.Spanish
                : golden.English;
            var actual = localizer.Format(
                new Message(new MessageKey(golden.Key), golden.Arguments)
            );
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                mismatches.Add(golden.Key + ": «" + expected + "» ≠ «" + actual + "»");
            }
        }

        mismatches.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void The_golden_covers_exactly_the_handoff_texts_with_placeholders(string language)
    {
        var withPlaceholders = I18nRepository
            .HandoffInProduct(language)
            .Where(static entry => Marker().IsMatch(entry.Value))
            .Select(static entry => entry.Key)
            .Order(StringComparer.Ordinal);

        Golden
            .Select(static golden => golden.Key)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe(withPlaceholders);
    }

    [GeneratedRegex(@"\{[a-z]\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Marker();

    private static MessageArgument App() => MessageArgument.Text("app", "Word");

    private static MessageArgument Profile() => MessageArgument.Text("profile", "Informes");

    private static MessageArgument Keys() => MessageArgument.Text("keys", "Ctrl + B");

    private static MessageArgument Count(long count) => MessageArgument.WholeNumber("count", count);

    private static MessageArgument Index() => MessageArgument.WholeNumber("index", 2);

    private static MessageArgument Total() => MessageArgument.WholeNumber("total", 5);

    /// <summary>One handoff key formatted with named arguments, and the text expected in each language.</summary>
    private sealed record GoldenText(
        string Key,
        MessageArgument[] Arguments,
        string Spanish,
        string English
    );
}
