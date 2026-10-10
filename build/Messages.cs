using System.Globalization;

namespace Clicalo.Build;

/// <summary>
/// Every sentence <c>cl</c> shows to the maintainer, in one place. This is developer tooling, not product
/// text (product text lives in <c>data/i18n</c>), and it is written in Spanish because it is read aloud by
/// Narrator for a Spanish-speaking maintainer. Lines avoid symbols that screen readers spell out.
/// </summary>
internal static class Messages
{
    public const string LessThanOneSecond = "menos de 1 s";

    // ---- Verb descriptions (cl --list-targets) ------------------------------------------------

    public const string SetupDescription =
        "Prepara el equipo: herramientas locales, configuración de git, DCO y firma SSH.";
    public const string BuildDescription = "Compila toda la solución en Debug.";
    public const string FastDescription =
        "Compila y prueba el núcleo portátil (Core.slnf). Objetivo: menos de 45 s.";
    public const string TestDescription =
        "Compila y ejecuta las pruebas deterministas: sin escritorio, caos, rendimiento ni cuarentena.";
    public const string DeskDescription =
        "Compila y ejecuta solo las pruebas de escritorio, con CLICALO_DESKTOP_TESTS=1.";
    public const string FixDescription = "Da formato al código C# con CSharpier.";
    public const string CheckDescription =
        "La misma puerta que la CI: versiones fijadas, formato, restauración bloqueada, "
        + "compilación Release sin advertencias, pruebas e i18n.";
    public const string CleanDescription =
        "Borra las salidas de compilación, los registros y los resultados de artifacts.";
    public const string I18nCheckDescription =
        "Valida data/i18n como el generador; con --strict-unused también falla por claves sin uso.";
    public const string I18nImportDescription =
        "Reconstruye data/i18n con la receta revisada; con --check no escribe y compara.";
    public const string AdrCheckDescription =
        "Exige un ADR si el cambio toca una ruta sensible; se usa con --base y la rama de comparación.";
    public const string TraceDescription =
        "Escribe artifacts/cl/trace.md: cada requisito del catálogo con sus pruebas y los MUST sin prueba.";

    public const string RunDescription =
        "Compila y abre Clícalo con datos aislados en %TEMP%\\clicalo-dev y sin envío de teclas.";
    public const string NoteDescription =
        "Crea la nota de novedades para usuarios de la rama, en español e inglés, en changes/unreleased.";
    public const string QuarantineDescription =
        "Compila y ejecuta solo las pruebas en cuarentena (Category=Quarantine), con CLICALO_DESKTOP_TESTS=1.";
    public const string PackageDescription =
        "Empaqueta Clícalo con Velopack en artifacts/package (Setup.exe y paquetes) sin publicarlo; con --channel y --version.";
    public const string PerfDescription =
        "Publica las variantes de S5 y mide el arranque, la memoria y, en la CI, del toque al envío.";

    public static string FutureDescription(string milestone) => "Disponible en " + milestone + ".";

    // ---- Step headers ---------------------------------------------------------------------------

    public const string PinsPurpose =
        "versiones exactas de paquetes, herramientas, SDK y acciones de GitHub";
    public const string ToolsPurpose = "restauración de las herramientas locales";
    public const string FormatCheckPurpose = "comprobación del formato C#";
    public const string FormatFixPurpose = "formato C# con CSharpier";
    public const string RestorePurpose = "restauración bloqueada de NuGet";
    public const string BuildDebugPurpose = "compilación Debug";
    public const string BuildReleasePurpose = "compilación Release sin advertencias";
    public const string TestPurpose = "pruebas deterministas";
    public const string QuarantinePurpose = "pruebas en cuarentena";
    public const string DeskPurpose = "pruebas de escritorio";
    public const string I18nPurpose = "comprobación de textos (i18n-check e i18n-import --check)";
    public const string CleanPurpose = "borrado de artifacts";
    public const string GitPurpose = "configuración de git, DCO y firma";
    public const string RunPurpose = "apertura de Clícalo sin envío de teclas";
    public const string NotePurpose = "nota de novedades para usuarios";
    public const string PublishPurpose = "publicación de las variantes de arranque (S5)";
    public const string PerfPurpose = "mediciones de rendimiento en el escritorio";
    public const string PackagePublishPurpose =
        "publicación autocontenida de Clícalo y Sentinel para el paquete";
    public const string PackagePurpose = "empaquetado con Velopack";

    public static string DevCliPurpose(string verb) =>
        "orden " + verb + " de la herramienta de desarrollo";

    public static string StepStarted(string step, string purpose) =>
        "Paso " + step + ": " + purpose + ".";

    // ---- Final lines ----------------------------------------------------------------------------

    public static string Success(string command, string duration) =>
        "cl " + command + ": correcto en " + duration;

    public static string Failure(string command, string step, string errorFile) =>
        "cl " + command + ": falló en " + step + "; detalle en " + errorFile;

    public static string NotYetAvailable(string verb, string milestone) =>
        "cl " + verb + ": disponible en " + milestone;

    public static string UnknownVerb(string verb, string verbs) =>
        "cl " + verb + ": orden desconocida; las órdenes son " + verbs;

    public static string VerbList(string verbs) => "cl: las órdenes son " + verbs;

    public static string DryRun(string command) =>
        "cl " + command + ": simulación sin ejecutar nada (--dry-run)";

    public static string InvalidUsage(string command, string reason) =>
        "cl " + command + ": uso no válido; " + reason;

    public static string WithNotes(string line, IEnumerable<string> notes)
    {
        var all = notes.ToList();
        return all.Count == 0 ? line : line + "; " + string.Join("; ", all);
    }

    // ---- Notes appended to a successful final line -------------------------------------------

    public static string TestCount(int count) =>
        count switch
        {
            0 => "ninguna prueba ejecutada",
            1 => "1 prueba",
            _ => string.Create(CultureInfo.InvariantCulture, $"{count} pruebas"),
        };

    /// <summary>The <c>total</c> of <c>dotnet test</c> and, when there are any, how many of them were skipped.</summary>
    public static string TestCount(int total, int skipped) =>
        skipped switch
        {
            <= 0 => TestCount(total),
            1 => TestCount(total) + ", 1 omitida",
            _ => TestCount(total)
                + string.Create(CultureInfo.InvariantCulture, $", {skipped} omitidas"),
        };

    public static string OverBudget(string budget) => "superó el objetivo de " + budget;

    public static string RunStarted(string dataDirectory) =>
        "Clícalo abierto sin envío de teclas, con datos en "
        + dataDirectory
        + "; se cierra con Salir en la bandeja";

    public const string RunAlreadyOpen = "ya había un Clícalo abierto en esta sesión y se mostró";

    public static string NoteCreated(string file) => "nota nueva en " + file;

    public static string NoteExists(string file) => "la nota ya existía en " + file;

    public static string PerfReport(string file) => "números en " + file;

    public static string TraceReport(string file, int? uncovered) =>
        "trazabilidad en "
        + file
        + uncovered switch
        {
            null => string.Empty,
            0 => "; ningún MUST sin prueba ni guion manual",
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"; {uncovered} MUST sin prueba ni guion manual"
            ),
        };

    public static string SetupPending(string items, string file) =>
        "falta " + items + "; instrucciones en " + file;

    public const string PendingSigning = "la firma SSH";
    public const string PendingIdentity = "tu identidad de git";

    /// <summary>Joins items the way they are said in Spanish: "a, b y c".</summary>
    public static string JoinList(IReadOnlyList<string> items) =>
        items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            _ => string.Join(", ", items.Take(items.Count - 1)) + " y " + items[^1],
        };

    // ---- Failure report (artifacts/cl/last-error.md) -----------------------------------------

    public static string ReportTitle(string command, string step) =>
        "cl " + command + ": falló en " + step;

    public const string ReportCommandLabel = "Orden";
    public const string ReportStepLabel = "Paso";
    public const string ReportProcessLabel = "Comando";
    public const string ReportExitCodeLabel = "Código de salida";
    public const string ReportElapsedLabel = "Tiempo hasta el fallo";
    public const string ReportDateLabel = "Fecha";
    public const string ReportLocalTime = "hora local";
    public const string ReportWhatToDo = "Qué hacer";

    public const string PinsFailed =
        "Hay versiones sin fijar o acciones de GitHub sin SHA completo (NFR-014, blueprint §12.2 T12).";
    public const string PinsSection = "Versiones no fijadas";
    public const string PinsHint =
        "Usa versiones exactas solo en Directory.Packages.props, sin rangos ni comodines, "
        + "y fija cada acción de GitHub por su SHA completo con un comentario de versión.";

    public const string ToolsFailed = "No se pudieron restaurar las herramientas locales.";
    public const string ToolsHint =
        "Revisa .config/dotnet-tools.json. Si el error es NU3034, el propietario de la herramienta "
        + "no está en trustedSigners de nuget.config.";

    public const string FormatFailed = "Hay archivos C# sin el formato de CSharpier.";
    public const string FormatSection = "Archivos sin formato";
    public const string FormatHint = "Ejecuta cl fix y vuelve a ejecutar la orden.";
    public const string FormatFixFailed = "CSharpier no pudo dar formato a todos los archivos.";

    public const string RestoreFailed = "La restauración bloqueada de NuGet falló.";
    public const string RestoreSection = "Errores de restauración";
    public const string RestoreHint =
        "Si añadiste o cambiaste un paquete, ejecuta cl build para actualizar packages.lock.json "
        + "y confírmalo. Si el error es NU3034, el propietario del paquete no está en trustedSigners "
        + "de nuget.config: añádelo y justifícalo en el pull request.";

    public static string RunMissing(string file) => "No se encontró " + file + " tras compilar.";

    public const string RunNotStarted = "Clicalo.exe no arrancó.";

    public static string RunEnded(int exitCode) =>
        exitCode switch
        {
            70 => "Clicalo.exe terminó al arrancar (código 70): su registro dice por qué.",
            2 => "Clicalo.exe no pudo hablar con la instancia abierta (código 2).",
            3 => "Otro programa ocupa el canal de Clícalo (código 3, ipc.squat_detected).",
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"Clicalo.exe terminó con el código {exitCode}."
            ),
        };

    public const string RunHint =
        "cl run abre la compilación Debug con --no-input: nunca envía teclas en este equipo. "
        + "Si el arranque falla por piezas aún no integradas, la integración de M2 las registra.";

    public const string PublishFailed = "La publicación de una variante de Clícalo falló.";
    public const string PublishSection = "Errores de publicación";
    public const string PublishHint =
        "Revisa el error; la publicación con Native AOT de Sentinel necesita las herramientas de C++ de Visual Studio.";

    public const string PackageUsage =
        "Uso: cl package [--channel stable|beta] [--version X.Y.Z o X.Y.Z-beta.N].";

    public static string PackageBadChannel(string channel) =>
        "El canal «" + channel + "» no existe: usa stable o beta.";

    public static string PackageBadVersion(string version) =>
        "La versión «" + version + "» no es SemVer: usa X.Y.Z o X.Y.Z-beta.N.";

    public static string PackageUnknownOption(string option) =>
        "La opción «" + option + "» no existe en cl package.";

    public const string PackageFailed = "vpk pack no pudo crear el paquete.";
    public const string PackageSection = "Salida de vpk";
    public const string PackageHint =
        "Revisa la salida; vpk es la herramienta local de .config/dotnet-tools.json (cl setup la restaura).";

    public static string PackageDone(string setup) => "instalador en " + setup + ", sin publicar";

    public const string BuildFailed = "La compilación terminó con errores.";
    public const string BuildSection = "Errores de compilación";
    public const string BuildHint = "Corrige los errores y vuelve a ejecutar la orden.";
    public const string NoParsedErrors =
        "MSBuild no dejó errores en el registro. Revisa la salida de la consola justo encima de la última línea.";

    public const string TestsFailed = "Hay pruebas que fallaron.";
    public const string TestsSection = "Pruebas que fallaron";
    public const string TestsHint =
        "Corrige las pruebas o el código. Para repetir solo el núcleo portátil, usa cl fast.";
    public const string TestRunFailedWithoutFailures =
        "La ejecución de pruebas terminó con error sin ninguna prueba fallida.";
    public const string TestMessageLabel = "Mensaje";
    public const string TestStackLabel = "Pila";
    public const string TestResultsLabel = "Resultados TRX";

    public const string I18nFailed = "La comprobación de textos (i18n-check) falló.";
    public const string I18nSection = "Salida de i18n-check";
    public const string I18nHint =
        "Todo texto de producto vive en data/i18n/strings.es.json y strings.en.json con las mismas claves.";
    public const string I18nImportFailed =
        "data/i18n no coincide con una importación limpia (i18n-import --check).";
    public const string I18nImportSection = "Salida de i18n-import --check";
    public const string I18nImportHint =
        "Declara los textos nuevos en data/i18n/handoff-import.json y vuelve a importar con "
        + "cl i18n-import. Ver docs/guides/i18n.md.";

    public const string AdrCheckFailed =
        "El cambio toca una ruta sensible sin un ADR nuevo o cambiado (adr-check).";
    public const string AdrCheckSection = "Salida de adr-check";
    public const string AdrCheckHint =
        "Escribe o actualiza un ADR en docs/adr (ver docs/adr/README.md), o pasa --base con la rama de comparación.";

    public const string TraceFailed =
        "Hay rasgos de requisito que no nombran ningún requisito del catálogo (trace).";
    public const string TraceHint =
        "Corrige el identificador del rasgo Req de la prueba o declara el requisito en docs/requirements/catalog.md.";

    public static string DevCliFailed(string verb) => "La orden " + verb + " falló.";

    public static string DevCliSection(string verb) => "Salida de " + verb;

    public const string CleanFailed = "No se pudieron borrar algunos archivos de artifacts.";
    public const string CleanSection = "Rutas que siguen en uso";
    public const string CleanHint =
        "Cierra los procesos que usan esos archivos (la app, pruebas o depuradores) y vuelve a ejecutar cl clean.";

    public const string GitFailed = "No se pudo configurar git.";
    public const string GitHint =
        "Comprueba que git está instalado y que estás dentro del repositorio.";

    public const string UnexpectedFailed = "Error inesperado dentro de cl.";
    public const string UnexpectedSection = "Excepción";
    public const string UnexpectedHint =
        "Es un defecto de cl. Abre un issue con este archivo o corrige build/.";

    public const string OutputSection = "Salida";

    public static string LineColumn(string path, int? line, int? column) =>
        (line, column) switch
        {
            (null, _) => path,
            ({ } l, null) => string.Create(CultureInfo.InvariantCulture, $"{path}, línea {l}"),
            ({ } l, { } c) => string.Create(
                CultureInfo.InvariantCulture,
                $"{path}, línea {l}, columna {c}"
            ),
        };

    public static string CountedHeading(string heading, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{heading} ({count})");

    public static string TruncatedItems(int shown, int total) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Se muestran {shown} de {total}. El resto está en el registro indicado arriba."
        );

    public static string LogFileLine(string path) => "Registro completo: " + path;

    // ---- Microsoft.Testing.Platform exit codes ------------------------------------------------

    /// <summary>Meaning of an exit code of <c>dotnet test</c> (Microsoft.Testing.Platform).</summary>
    public static string TestExitCodeMeaning(int exitCode) =>
        exitCode switch
        {
            0 => "correcto",
            1 => "error desconocido",
            2 => "al menos una prueba falló",
            3 => "la sesión de pruebas se abortó",
            4 => "la configuración de las extensiones no es válida",
            5 => "los argumentos no son válidos",
            6 => "función no implementada",
            7 => "el proceso de pruebas terminó de forma inesperada",
            8 => "no se ejecutó ninguna prueba",
            9 => "no se alcanzó el mínimo de pruebas esperado",
            10 => "el adaptador de pruebas falló",
            11 => "un proceso del que dependían las pruebas terminó",
            12 => "versión de protocolo incompatible",
            13 => "se alcanzó el máximo de pruebas fallidas",
            _ => "código no documentado",
        };

    // ---- Version pins ---------------------------------------------------------------------------

    public const string PinFloatingVersion = "versión no exacta";
    public const string PinMissingVersion = "sin versión";
    public const string PinVersionOutsideCentral =
        "versión fuera de Directory.Packages.props (Central Package Management)";
    public const string PinActionNotSha = "acción no fijada por SHA completo";
    public const string PinActionNoVersionComment =
        "acción fijada por SHA sin el comentario de versión (# vX.Y.Z) que usa Renovate";
    public const string PinUnreadable = "archivo no válido; no se pudieron comprobar sus versiones";

    // ---- cl setup ---------------------------------------------------------------------------

    public static string GitSettingApplied(string key, string value) =>
        "git config " + key + " " + value;

    public const string SigningEnabled =
        "Firma SSH detectada: commit.gpgsign y tag.gpgsign activados en este repositorio.";

    public const string SetupNotesTitle = "cl setup: pasos pendientes";
    public const string SetupNotesIntro =
        "cl setup ya configuró el repositorio. Faltan estos pasos, que dependen de ti y de tu equipo. "
        + "cl nunca genera ni copia claves.";

    public const string IdentityHeading = "Identidad de git";
    public const string IdentityBody =
        "Los commits llevan la línea Signed-off-by (DCO) con tu nombre y tu correo. Configúralos una vez por equipo:";
    public const string IdentityNamePlaceholder = "Tu nombre";
    public const string IdentityEmailPlaceholder = "tu-correo@ejemplo.com";

    public const string SigningHeading = "Firma de commits con SSH";
    public const string SigningIntro =
        "Los commits de main se firman (blueprint §13). Usa una clave SSH que ya tengas, por ejemplo la de GitHub.";
    public const string SigningKeysFound = "Claves públicas encontradas en tu carpeta .ssh:";
    public const string SigningNoKeysFound =
        "No se encontró ninguna clave pública en tu carpeta .ssh. Crea una tú mismo siguiendo la guía de GitHub "
        + "(Generating a new SSH key), con frase de contraseña.";
    public const string SigningMissingKeyFile =
        "user.signingkey apunta a un archivo que no existe. Corrige la ruta:";
    public const string SigningStepConfigure =
        "Indica a git que firme con SSH y con qué clave pública (una vez por equipo):";
    public const string SigningStepGitHub =
        "Sube la misma clave pública a GitHub como clave de firma: Settings, SSH and GPG keys, New SSH key, "
        + "Key type: Signing Key.";
    public const string SigningStepAgent =
        "Opcional, para no escribir la frase de contraseña en cada commit: activa el servicio "
        + "«OpenSSH Authentication Agent» de Windows (las dos primeras órdenes, en PowerShell como "
        + "administrador), añade la clave con ssh-add y dile a git que use el ssh-keygen de Windows:";
    public const string SigningStepRerun =
        "Ejecuta de nuevo cl setup: activará commit.gpgsign y tag.gpgsign en este repositorio.";
}
