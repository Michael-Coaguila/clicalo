using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Foreground;
using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Tiles;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// The manual scripts of docs/testing/spikes/S1.md, S3.md and S4.md as data: one step per row of their manual results
/// table, in the recommended order. The texts are the laboratory's own (M1 adds no product text, M1-ownership.md) and
/// are the single source of the «Recorrido» of each document: SpikeScriptDocumentTests fails when a document and this
/// catalog say different things, and prints the block to paste.
/// </summary>
/// <remarks>
/// Every voice order is unambiguous: «clic» with a name that no other element of SpikeLab contains, or, where the row
/// is about numbers, «mostrar números» (or «mostrar números en todas partes») and «ocultar números» said explicitly.
/// Nothing asks to say a phrase that is not a command, so nothing is dictated into the app under test by accident; the
/// only dictation into the app is the final check, in a blank document.
/// </remarks>
internal static class SpikeScripts
{
    /// <summary>Repetitions per row: «20 de 20» is the success criterion of every blocking spike (blueprint §15.1).</summary>
    public const int CyclesPerRow = 20;

    /// <summary>Repetitions of S3 row 13 (high contrast): the goal is only to see that the tree survives.</summary>
    public const int HighContrastRepetitions = 5;

    /// <summary>Where the app under test goes (<c>LabLayout.TargetZone</c>): no surface starts there.</summary>
    private const string Zone = "en la zona libre (el cuarto de arriba a la izquierda)";

    private const string FinalWordCheck =
        " Al terminar, dicta otra palabra sin tocar la app y espera a que aparezca (Wispr Flow y Typeless tardan un "
        + "momento): si se escribe en la app, toca «Funcionó»; si no aparece, «Falló».";

    private const string FinalKeyboardCheck =
        " Al terminar, sin tocar la app, escribe otra letra con el teclado táctil (el dictado de Wispr Flow y "
        + "Typeless no llega a una app de administrador): si aparece en la app, toca «Funcionó»; si no, «Falló».";

    private const string FinalNumberCheck =
        " Al terminar, sin tocar la Calculadora, escribe otro número con el teclado táctil: si aparece en la "
        + "Calculadora, toca «Funcionó»; si no, «Falló».";

    private const string NotTyped =
        " Si una orden se escribe en el Bloc de notas en vez de ejecutarse, no cuenta: repítela.";

    /// <summary>S1 · No activation (32 rows).</summary>
    public static SpikeScript S1 { get; } =
        new(SpikeId.S1, "S1 · No activación", "docs/testing/spikes/S1.md", BuildS1());

    /// <summary>S3 · UI Automation on a non-activatable window (14 rows).</summary>
    public static SpikeScript S3 { get; } =
        new(
            SpikeId.S3,
            "S3 · UI Automation sobre ventana no activable",
            "docs/testing/spikes/S3.md",
            BuildS3()
        );

    /// <summary>S4 · Text input and foreground per origin (11 rows).</summary>
    public static SpikeScript S4 { get; } =
        new(
            SpikeId.S4,
            "S4 · Entrada de texto y primer plano por origen",
            "docs/testing/spikes/S4.md",
            BuildS4()
        );

    /// <summary>Every script, in the order of the control window.</summary>
    public static ImmutableArray<SpikeScript> All { get; } = [S1, S3, S4];

    /// <summary>The script of <paramref name="spike"/>.</summary>
    public static SpikeScript For(SpikeId spike) =>
        spike switch
        {
            SpikeId.S1 => S1,
            SpikeId.S3 => S3,
            SpikeId.S4 => S4,
            _ => throw new ArgumentOutOfRangeException(nameof(spike), spike, message: null),
        };

    private static ImmutableArray<ScriptStep> BuildS1()
    {
        (string Name, string Setup, string FinalCheck)[] apps =
        [
            (
                "Word",
                "Pon Word "
                    + Zone
                    + ", con un documento en blanco, y escribe o dicta una palabra: el cursor debe "
                    + "quedar parpadeando.",
                FinalWordCheck
            ),
            (
                "Chrome",
                "Pon Chrome "
                    + Zone
                    + " con una página que tenga un campo de búsqueda; toca el campo y escribe o "
                    + "dicta una palabra, sin pulsar Intro.",
                FinalWordCheck
            ),
            (
                "VS Code",
                "Pon VS Code "
                    + Zone
                    + " con un archivo nuevo sin guardar (Archivo › Nuevo archivo de texto) y "
                    + "escribe o dicta una palabra: el cursor debe quedar parpadeando.",
                FinalWordCheck
            ),
            (
                "Bloc de notas",
                "Pon un Bloc de notas nuevo "
                    + Zone
                    + " y escribe o dicta una palabra: el cursor debe quedar "
                    + "parpadeando.",
                FinalWordCheck
            ),
            (
                "Calculadora (Tienda)",
                "Pon la Calculadora " + Zone + " y escribe un número con sus propios botones.",
                FinalNumberCheck
            ),
            (
                "Bloc de notas (administrador)",
                "Pon "
                    + Zone
                    + " el Bloc de notas abierto como administrador y escribe una palabra con el teclado "
                    + "táctil.",
                FinalKeyboardCheck
            ),
        ];

        var steps = ImmutableArray.CreateBuilder<ScriptStep>(32);
        var row = 1;
        foreach (var (app, setup, finalCheck) in apps)
        {
            foreach (
                var surface in (SurfaceGroup[])
                    [SurfaceGroup.Panel, SurfaceGroup.TabWithSide, SurfaceGroup.Bubble]
            )
            {
                steps.Add(
                    TapStep(
                        row++,
                        surface,
                        PointerKind.Finger,
                        app,
                        setup + " " + TapWith(surface, "el dedo") + finalCheck
                    )
                );
            }
        }

        const string Notepad =
            "Pon un Bloc de notas nuevo "
            + Zone
            + " y escribe o dicta una palabra: el cursor debe quedar parpadeando.";
        foreach (
            var (kind, name, missing) in (ReadOnlySpan<(PointerKind, string, string)>)
                [
                    (PointerKind.Pen, "el lápiz", "lápiz"),
                    (PointerKind.Mouse, "el mouse", "mouse o panel táctil"),
                ]
        )
        {
            foreach (
                var surface in (SurfaceGroup[])
                    [SurfaceGroup.Panel, SurfaceGroup.TabWithSide, SurfaceGroup.Bubble]
            )
            {
                steps.Add(
                    TapStep(
                        row++,
                        surface,
                        kind,
                        "Bloc de notas",
                        Notepad
                            + " "
                            + TapWith(surface, name)
                            + FinalWordCheck
                            + " Si no tienes "
                            + missing
                            + ", toca «Siguiente»: la fila queda como no aplicable."
                    ) with
                    {
                        Optional = true,
                    }
                );
            }
        }

        steps.Add(
            PanelCondition(
                row++,
                "IME japonés con composición abierta",
                "Con el Bloc de notas delante, cambia a japonés tocando el indicador de idioma, abre el teclado táctil, "
                    + "escribe か y déjalo sin confirmar (subrayado). Toca el panel 20 veces con el dedo: la composición "
                    + "debe seguir subrayada tras cada toque; si se cancela o se confirma sola, toca «Falló». Al final "
                    + "confírmala con el teclado táctil: si aparece en el Bloc de notas, toca «Funcionó»."
            )
        );
        steps.Add(
            PanelCondition(
                row++,
                "IME chino (si lo hay)",
                "Igual que la fila anterior, con el IME chino. Si no lo tienes instalado, toca «Siguiente»: la fila "
                    + "queda como no aplicable."
            ) with
            {
                Optional = true,
            }
        );
        steps.Add(
            PanelCondition(
                row++,
                "Menú Archivo del Bloc de notas abierto",
                "Toca el menú Archivo del Bloc de notas para abrirlo y, con el menú abierto, toca el panel 20 veces. "
                    + "El menú debe seguir abierto tras cada toque; si se cierra, toca «Falló» y vuelve a abrirlo antes "
                    + "del siguiente toque. Al final toca «Funcionó» si siguió abierto siempre."
            )
        );
        steps.Add(
            PanelCondition(
                row++,
                "Lista de la cinta de Word abierta",
                "Con Word "
                    + Zone
                    + ", toca «Plegar la tira» para que la tira no tape la lista. Abre la lista de "
                    + "tamaño de fuente de la cinta y, con la lista abierta, toca el panel 20 veces. Si la lista se "
                    + "cierra, toca «Falló» y vuelve a abrirla. Al final toca «Funcionó» si siguió abierta y "
                    + "«Desplegar la tira»."
            )
        );
        steps.Add(
            PanelCondition(
                row++,
                "Menú ⋮ de Chrome abierto",
                "Con Chrome "
                    + Zone
                    + ", toca «Plegar la tira» para que la tira no tape el menú. Abre el menú ⋮ y, "
                    + "con el menú abierto, toca el panel 20 veces. Si el menú se cierra, toca «Falló» y vuelve a "
                    + "abrirlo. Al final toca «Funcionó» si siguió abierto y «Desplegar la tira»."
            )
        );
        steps.Add(
            new ScriptStep(
                Id(row++),
                "Panel · arrastre del asa · entre monitores de distinto DPI",
                "Con el Bloc de notas delante, arrastra el panel por su asa (la franja ⠿ de su izquierda) de un monitor "
                    + "al otro y vuelta: cada arrastre cuenta, 20 en total. Tras cada uno la tira sigue en verde, el "
                    + "panel se ve nítido y del mismo tamaño en los dos monitores y el Bloc de notas sigue con el cursor "
                    + "parpadeando. Al final toca «Funcionó» si todo fue así. Si solo tienes un monitor, toca "
                    + "«Siguiente»: la fila queda como no aplicable.",
                CyclesPerRow,
                StepTrigger.HandleDrag,
                EvidenceCheck.NonActivation
            )
            {
                Surface = SurfaceGroup.Panel,
                Optional = true,
            }
        );
        steps.Add(
            new ScriptStep(
                Id(row++),
                "Panel · activación forzada · Bloc de notas",
                "Toca el Bloc de notas para ponerlo delante. Después toca «Forzar activación del panel» en esta tira, 20 "
                    + "veces (si el Bloc de notas deja de estar delante, tócalo antes del siguiente). SpikeLab llama a "
                    + "SetForegroundWindow sobre el panel sin concesión: reg01.violations sube en 1 y el Bloc de notas "
                    + "vuelve delante solo, en menos de 200 ms, con el cursor donde estaba. Al final toca «Funcionó» si "
                    + "el cursor siguió en su sitio.",
                CyclesPerRow,
                StepTrigger.ForcedActivation,
                EvidenceCheck.ForcedActivationReverted | EvidenceCheck.TargetStillInFront
            )
            {
                Surface = SurfaceGroup.Panel,
                Action = StepAction.ForceActivation,
            }
        );
        steps.Add(
            new ScriptStep(
                Id(row),
                "Panel · voz («clic Subrayado») · Bloc de notas",
                "Con el Bloc de notas delante y Acceso por voz despierto, di «clic Subrayado» 20 veces: cada orden "
                    + "cuenta y la tira sigue en verde. Si Acceso por voz te pide elegir con números, di el que muestra "
                    + "sobre la ficha «Subrayado» del panel."
                    + NotTyped
                    + FinalWordCheck
                    + " Si solo falla esta fila, S1 no se decide aquí: pasa a S3.",
                CyclesPerRow,
                StepTrigger.UiaCommand,
                EvidenceCheck.NonActivation
            )
            {
                Tile = LabTiles.Underline,
                Pattern = CommandPattern.Invoke,
                Decisive = false,
            }
        );
        return steps.MoveToImmutable();
    }

    private static ImmutableArray<ScriptStep> BuildS3()
    {
        const string Target =
            "Deja el Bloc de notas delante, "
            + Zone
            + ", con el cursor parpadeando (la tira dice «Primer plano: notepad»). ";
        const string AfterEach =
            " Tras cada orden la tira sigue en verde y «Última orden» muestra la ficha y el patrón.";

        return
        [
            Voice(
                "1",
                "Acceso por voz · «mostrar números» + «clic» y el número de «Copiar»",
                Target
                    + "Di «mostrar números». Si Acceso por voz pone números sobre las fichas del panel, di «clic» y el "
                    + "número que muestra sobre «Copiar» (el de Acceso por voz, no el de Clícalo). Si solo los pone en "
                    + "el Bloc de notas, no digas ningún número: di «ocultar números» y toca «Falló». Repite las dos "
                    + "órdenes 20 veces y, al terminar, di «ocultar números»."
                    + AfterEach
                    + NotTyped
                    + FinalWordCheck,
                LabTiles.Copy,
                CommandPattern.Invoke
            ),
            Voice(
                "2",
                "Acceso por voz · «clic Negrita»",
                Target + "Di «clic Negrita». 20 veces." + AfterEach + NotTyped + FinalWordCheck,
                LabTiles.Bold,
                CommandPattern.Invoke
            ),
            Voice(
                "3",
                "Acceso por voz · «mostrar números en todas partes» + «clic» y el número de «Guardar»",
                Target
                    + "Di «mostrar números en todas partes» y después «clic» con el número que Acceso por voz muestra "
                    + "sobre la ficha «Guardar». 20 veces. Al terminar, di «ocultar números»."
                    + AfterEach
                    + NotTyped
                    + FinalWordCheck,
                LabTiles.Save,
                CommandPattern.Invoke
            ),
            Voice(
                "4",
                "Acceso por voz · «clic Mayús» (tres estados)",
                Target
                    + "Di «clic Mayús»: cada orden cambia su estado (desactivada, activada, bloqueada) y la tira dice "
                    + "«Toggle». 20 órdenes."
                    + NotTyped
                    + FinalWordCheck,
                LabTiles.Shift,
                CommandPattern.Toggle
            ),
            Voice(
                "5",
                "Acceso por voz · «clic Perfil» (expandir y contraer)",
                Target
                    + "Di «clic Perfil»: se abre la ventana de perfil y la tira dice «ExpandCollapse»; dilo otra vez "
                    + "para cerrarla. Cada orden cuenta: 20 órdenes."
                    + NotTyped
                    + FinalWordCheck,
                LabTiles.Profile,
                CommandPattern.ExpandCollapse
            ),
            Voice(
                "6",
                "Acceso por voz · «clic 7» con los números de Clícalo",
                Target
                    + "Si Acceso por voz muestra sus números, di «ocultar números». Toca «Activar números de Clícalo» "
                    + "en esta tira: cada ficha del panel muestra su número y su nombre empieza por él. Di «clic 7» "
                    + "(el número de Clícalo de «Guardar»). 20 veces. Al terminar, toca «Quitar números de Clícalo»."
                    + NotTyped
                    + FinalWordCheck,
                LabTiles.Save,
                CommandPattern.Invoke
            ) with
            {
                Checks = EvidenceCheck.NonActivation | EvidenceCheck.VoiceNumberInName,
                Action = StepAction.ToggleVoiceNumbers,
            },
            Voice(
                "7",
                "Narrador · exploración táctil + doble toque",
                Target
                    + "Con Narrador activo, arrastra un dedo sobre las fichas: debe leer «Negrita, botón» (o «1 "
                    + "Negrita, botón» con los números de Clícalo). Toca dos veces con un dedo para activar. 20 "
                    + "activaciones."
                    + FinalWordCheck,
                tile: null,
                pattern: null
            ),
            Voice(
                "8",
                "Narrador · navegación con el teclado táctil",
                Target
                    + "Con el teclado táctil en el modo de Narrador (o deslizando a la derecha con un dedo), avanza de "
                    + "ficha en ficha y activa con doble toque. 20 activaciones. Si no puedes hacerlo sin teclado "
                    + "físico, toca «Falló» y anótalo: es información útil, no un fallo de Clícalo.",
                tile: null,
                pattern: null
            ) with
            {
                Optional = true,
                Decisive = false,
            },
            Notice(
                "9a",
                "Narrador · «Aviso cortés»",
                Target
                    + "Toca «Aviso cortés» en esta tira: Narrador debe leer el aviso sin mover el foco del Bloc de "
                    + "notas. Después toca «Funcionó» o «Falló». 20 veces.",
                StepAction.PoliteNotice
            ),
            Notice(
                "9b",
                "Narrador · «Aviso urgente»",
                Target
                    + "Haz que Narrador lea algo largo (por ejemplo, esta tira) y toca «Aviso urgente»: Narrador debe "
                    + "interrumpir y leer el aviso. Después toca «Funcionó» o «Falló». 20 veces.",
                StepAction.AssertiveNotice
            ),
            new ScriptStep(
                "10",
                "Narrador · estado de «Mayús»",
                Target
                    + "Toca «Mayús» (o di «clic Mayús») y pon el dedo sobre la ficha: Narrador debe decir su estado "
                    + "(«activado» o «bloqueado»), no solo el color. Toca «Funcionó» o «Falló» cada vez. 20 veces.",
                CyclesPerRow,
                StepTrigger.Manual,
                EvidenceCheck.NoOwnForeground | EvidenceCheck.NoViolation
            ),
            Voice(
                "11",
                "Reconocimiento de voz (W10) · «mostrar números» + «clic» y el número de «Copiar»",
                "Solo en la máquina virtual de Windows 10 22H2, con Reconocimiento de voz de Windows. "
                    + Target
                    + "Di «mostrar números» y «clic» con el número que muestre sobre «Copiar». 20 veces. Sin máquina "
                    + "virtual, toca «Siguiente»: la fila queda pendiente.",
                LabTiles.Copy,
                CommandPattern.Invoke
            ) with
            {
                Optional = true,
            },
            Voice(
                "12",
                "Reconocimiento de voz (W10) · «clic Negrita»",
                "Solo en la máquina virtual de Windows 10 22H2. "
                    + Target
                    + "Di «clic Negrita». 20 veces. Sin máquina virtual, toca «Siguiente».",
                LabTiles.Bold,
                CommandPattern.Invoke
            ) with
            {
                Optional = true,
            },
            Voice(
                "13",
                "Acceso por voz con alto contraste · filas 1 y 2",
                "Activa el alto contraste (Configuración › Accesibilidad › Temas de contraste › Aplicar) y vuelve a "
                    + "tocar el Bloc de notas. "
                    + Target
                    + "Repite las órdenes de las filas 1 y 2: 5 órdenes en total, por ejemplo 3 sobre «Copiar» (con "
                    + "«mostrar números» y su número) y 2 «clic Negrita». Las fichas conservan sus nombres y la tira "
                    + "sigue en verde. Al terminar, di «ocultar números», desactiva el alto contraste y toca "
                    + "«Funcionó».",
                tile: null,
                pattern: null
            ) with
            {
                Required = HighContrastRepetitions,
            },
        ];
    }

    private static ImmutableArray<ScriptStep> BuildS4()
    {
        const string OpenByTouch =
            "Pon Word "
            + Zone
            + ", con un documento en blanco y el cursor parpadeando. Toca 🔍 «Buscar» en el panel: el campo tiene el "
            + "cursor dentro y la tira dice «Concesión: TextInput concedida». ";
        const string WaitForText =
            " Espera a ver «negr» o «negrita» en el campo de búsqueda antes de seguir: Wispr Flow y Typeless pegan el "
            + "texto un momento después. Si no aparece, toca «Cerrar búsqueda»: el ciclo cuenta como fallido.";
        const string Result =
            " Después toca «Resultado Negrita» (o di «clic Resultado Negrita»): SpikeLab no envía ninguna acción en "
            + "M1, solo devuelve el primer plano y lo verifica; con el campo vacío no lo devuelve y te avisa. Word "
            + "vuelve delante con el cursor donde estaba y la tira dice «devolución: Restaurado» (o «Restaurado al "
            + "reintentar»). 20 ciclos. Al final toca «Funcionó» si el texto cayó siempre en el campo de búsqueda y "
            + "nunca en Word.";
        const string Probe =
            "Toca «Abrir sonda» en la ventana de control (una vez) y pon la sonda delante antes de cada ciclo. ";
        const string ProbeResult =
            " La sonda vuelve delante y la tira dice «La sonda recibió: F24 = 0, caracteres = 0, menú = 0». 20 ciclos. "
            + "Al final toca «Funcionó» si el texto cayó siempre en el campo de búsqueda.";
        const EvidenceCheck TextCycle =
            EvidenceCheck.LeaseRoundTrip | EvidenceCheck.TextReachedField;
        const EvidenceCheck ProbeCycle =
            TextCycle | EvidenceCheck.ProbeSilent | EvidenceCheck.ProbeWasTarget;

        return
        [
            Lease(
                "1",
                "Toque · Word · teclado táctil",
                OpenByTouch
                    + "Escribe «negr» con el teclado táctil (toca el campo si no sale solo; si tapa el panel, el panel "
                    + "debe subir)."
                    + WaitForText
                    + Result,
                LeaseKind.TextInput,
                LeaseOrigin.Touch,
                TextCycle
            ),
            Lease(
                "2",
                "Toque · Word · 🎤 Dictar (Win+H)",
                OpenByTouch
                    + "Toca 🎤 «Dictar» y dicta «negrita» con el dictado de Windows."
                    + WaitForText
                    + Result,
                LeaseKind.TextInput,
                LeaseOrigin.Touch,
                TextCycle
            ),
            Lease(
                "3",
                "Toque · Word · Wispr Flow",
                OpenByTouch + "Dicta «negrita» con Wispr Flow." + WaitForText + Result,
                LeaseKind.TextInput,
                LeaseOrigin.Touch,
                TextCycle
            ),
            Lease(
                "4",
                "Toque · Word · Typeless",
                OpenByTouch + "Dicta «negrita» con Typeless." + WaitForText + Result,
                LeaseKind.TextInput,
                LeaseOrigin.Touch,
                TextCycle
            ),
            Lease(
                "5",
                "Acceso por voz (UIA) · sonda · dictado de Acceso por voz",
                Probe
                    + "Di «clic Buscar» con Acceso por voz; dicta «negr» con Acceso por voz y espera a verlo en el "
                    + "campo; di «clic Resultado Negrita»."
                    + ProbeResult,
                LeaseKind.TextInput,
                LeaseOrigin.UiaInvoke,
                ProbeCycle
            ),
            Lease(
                "6",
                "Narrador (UIA) · sonda · teclado táctil",
                Probe
                    + "Con Narrador, explora con el dedo hasta «Buscar, botón» y toca dos veces; escribe «negr» con el "
                    + "teclado táctil; activa «Resultado Negrita» con doble toque."
                    + ProbeResult,
                LeaseKind.TextInput,
                LeaseOrigin.UiaInvoke,
                ProbeCycle
            ),
            Lease(
                "7",
                "Reconocimiento de voz (W10) · sonda · dictado",
                "Solo en la máquina virtual de Windows 10 22H2. "
                    + Probe
                    + "Di «clic Buscar», dicta «negr», espera a verlo en el campo y di «clic Resultado Negrita». Sin "
                    + "máquina virtual, toca «Siguiente»: la fila queda pendiente."
                    + ProbeResult,
                LeaseKind.TextInput,
                LeaseOrigin.UiaInvoke,
                ProbeCycle
            ) with
            {
                Optional = true,
            },
            Lease(
                "8",
                "Atajo global · sonda · teclado táctil",
                Probe
                    + "Di con Acceso por voz «pulsa Control Mayúsculas F11» (o usa el teclado táctil en modo completo): "
                    + "se abre la búsqueda. Escribe «negr» con el teclado táctil y toca «Resultado Negrita»."
                    + ProbeResult,
                LeaseKind.TextInput,
                LeaseOrigin.GlobalHotkey,
                ProbeCycle
            ),
            Lease(
                "9",
                "Centro de control desde el panel · Bloc de notas · los tres métodos",
                "Pon el Bloc de notas delante, "
                    + Zone
                    + ". Toca ⚙ «Centro de control» en el panel. Rellena los tres campos: «Nombre» con el teclado "
                    + "táctil, «Texto» con 🎤 «Dictar» (Win+H) y «Web» con Wispr Flow o Typeless; espera a ver cada "
                    + "texto en su campo antes de seguir. Ciérralo con «Cerrar»: el Bloc de notas vuelve delante con el "
                    + "cursor en su sitio. 20 ciclos. Al final toca «Funcionó» si cada texto cayó en su campo.",
                LeaseKind.ControlCenter,
                LeaseOrigin.Touch,
                TextCycle
            ),
            Lease(
                "10",
                "Centro de control desde la bandeja · Bloc de notas",
                "Con el Bloc de notas delante, toca el icono de SpikeLab en la bandeja (si está oculto, toca ^ primero) "
                    + "y elige «Abrir el Centro de control»; ciérralo con «Cerrar». El Bloc de notas vuelve delante. 20 "
                    + "ciclos. Al final toca «Funcionó».",
                LeaseKind.ControlCenter,
                LeaseOrigin.Tray,
                EvidenceCheck.LeaseRoundTrip
            ),
            Lease(
                "11",
                "Menú de la bandeja · Bloc de notas",
                "Con el Bloc de notas delante, mantén el dedo sobre el icono de SpikeLab en la bandeja (o tócalo) y "
                    + "elige «Soltar todas las teclas». El menú se cierra y el Bloc de notas vuelve delante. 20 ciclos. "
                    + "Al final toca «Funcionó».",
                LeaseKind.TrayMenu,
                LeaseOrigin.Tray,
                EvidenceCheck.LeaseRoundTrip
            ),
        ];
    }

    private static string Id(int row) => row.ToString(CultureInfo.InvariantCulture);

    private static string SurfaceName(SurfaceGroup surface) =>
        surface switch
        {
            SurfaceGroup.Panel => "Panel",
            SurfaceGroup.TabWithSide => "Pestaña con lateral",
            SurfaceGroup.Bubble => "Burbuja",
            _ => "Cualquier superficie",
        };

    private static string PointerName(PointerKind kind) =>
        kind switch
        {
            PointerKind.Finger => "dedo",
            PointerKind.Pen => "lápiz",
            _ => "mouse",
        };

    private static string TapWith(SurfaceGroup surface, string device) =>
        surface switch
        {
            SurfaceGroup.Panel => "Toca el panel 20 veces con "
                + device
                + ", alternando sus fichas. Tras cada toque la tira sigue en verde y el cursor de la app no se "
                + "mueve.",
            SurfaceGroup.TabWithSide => "Toca la Pestaña 20 veces con "
                + device
                + ": sus fichas y su asa, que abre y cierra la ventana lateral (los toques en la lateral también "
                + "cuentan). Tras cada toque la tira sigue en verde.",
            _ => "Toca la burbuja 20 veces con "
                + device
                + ". Tras cada toque la tira sigue en verde.",
        };

    private static ScriptStep TapStep(
        int row,
        SurfaceGroup surface,
        PointerKind pointer,
        string app,
        string instruction
    ) =>
        new(
            Id(row),
            SurfaceName(surface) + " · " + PointerName(pointer) + " · " + app,
            instruction,
            CyclesPerRow,
            StepTrigger.SurfaceTap,
            EvidenceCheck.NonActivation
        )
        {
            Surface = surface,
            Pointer = pointer,
        };

    private static ScriptStep PanelCondition(int row, string condition, string instruction) =>
        new(
            Id(row),
            "Panel · dedo · " + condition,
            instruction,
            CyclesPerRow,
            StepTrigger.SurfaceTap,
            EvidenceCheck.NonActivation
        )
        {
            Surface = SurfaceGroup.Panel,
            Pointer = PointerKind.Finger,
        };

    private static ScriptStep Voice(
        string id,
        string title,
        string instruction,
        string? tile,
        CommandPattern? pattern
    ) =>
        new(
            id,
            title,
            instruction,
            CyclesPerRow,
            StepTrigger.UiaCommand,
            EvidenceCheck.NonActivation
        )
        {
            Tile = tile,
            Pattern = pattern,
        };

    private static ScriptStep Notice(
        string id,
        string title,
        string instruction,
        StepAction action
    ) =>
        new(id, title, instruction, CyclesPerRow, StepTrigger.Manual, EvidenceCheck.NonActivation)
        {
            Action = action,
        };

    private static ScriptStep Lease(
        string id,
        string title,
        string instruction,
        LeaseKind kind,
        LeaseOrigin origin,
        EvidenceCheck checks
    ) =>
        new(id, title, instruction, CyclesPerRow, StepTrigger.LeaseCycle, checks)
        {
            Lease = kind,
            Origin = origin,
        };
}
