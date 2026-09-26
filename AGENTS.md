# AGENTS.md · Guía para agentes de código

Instrucciones para cualquier agente (y cualquier persona) que modifique este repositorio. Las reglas viven
también en los analizadores, las pruebas de arquitectura y la CI: si te saltas una, recibirás el mismo error
que una persona. La autoridad es el [plano](docs/architecture/blueprint.md); este archivo es su resumen
operativo.

## Qué es Clícalo

Un panel flotante para Windows, pensado primero para la accesibilidad, que ejecuta atajos, macros, textos y
acciones de mouse solo con la pantalla táctil o la voz. C# 14, .NET 10 LTS, WPF confinado en
`Clicalo.UI.Wpf`, monolito modular hexagonal. Lo mantiene una persona que programa con pantalla táctil y
voz.

Las 8 reglas que no se pueden romper (catálogo, §1): el panel nunca quita el foco (REG-01); todo objetivo
táctil mide al menos 44×44 (REG-02); siempre hay forma de soltar las teclas (REG-03); nada destructivo sin
dos toques y sin deshacer (REG-04); todo se hace sin teclado físico (REG-05); todo control se expone a UI
Automation (REG-06); autoguardado y todo se puede deshacer (REG-07); nunca se pierden datos (REG-08).

## Reglas para agentes

1. **Termina siempre con `cl check`** y déjalo en verde (`.\cl check` en PowerShell). Es lo mismo que el
   trabajo `verify` de la CI: versiones fijadas, formato CSharpier, restauración bloqueada, compilación
   Release sin advertencias, pruebas e i18n. Si falla, el error exacto está en `artifacts/cl/last-error.md`;
   `cl fix` resuelve el formato.
2. **Nunca edites lo generado.** Cambia el dato (`data/`) o el generador (`generators/`).
3. **`[Trait("Req", "<ID>")]` en todo requisito tocado.** Si cambias el comportamiento de un requisito del
   catálogo, su prueba se actualiza o se crea en el mismo cambio.
4. **Textos en los dos JSON.** Ningún texto de producto en C# ni en XAML: va en
   `data/i18n/strings.es.json` **y** `data/i18n/strings.en.json`, con los mismos marcadores. Esos archivos
   salen de la receta `data/i18n/handoff-import.json` (`i18n-import --check` está en `cl check`): una clave
   nueva se declara en su sección `added` y se vuelve a importar ([guía de i18n](docs/guides/i18n.md)).
5. **Un ADR si cambias un límite de confianza, un formato persistido o un contrato público** (o el
   framework, el modelo de procesos o de estado, la licencia o la firma). Ver [docs/adr](docs/adr/README.md).
   Las rutas están en `architecture/sensitive-paths.json` y el trabajo `adr` de la CI lo comprueba
   (`dotnet run --project tools/Clicalo.DevCli -- adr-check --base main` en local).
6. **Nunca rebajes un requisito.** Si uno parece inviable, propón el cambio con evidencia en la sección de
   propuestas pendientes del catálogo (§6.1); solo el usuario lo ratifica. Ver
   [cómo leer el catálogo](docs/requirements/README.md#cambiar-un-requisito).

## Reglas de código

- Código, identificadores y comentarios en **inglés**; documentación en **español**.
- Un tipo por archivo (MA0048), *namespaces* de ámbito de archivo, `sealed` por defecto, sin `this.`,
  `Nullable` activado. `TreatWarningsAsErrors` está activo: toda advertencia rompe la compilación.
- **No debilites analizadores en archivos compartidos.** Una supresión local solo con
  `[SuppressMessage("Categoría", "Id", Justification = "motivo real")]`.
- Versiones de paquetes **solo** en `Directory.Packages.props`; los proyectos usan `PackageReference` sin
  versión. Actualiza los `packages.lock.json` con el cambio.
- `TimeProvider` en todo lo que dependa del tiempo; `CancellationToken` en todo lo asíncrono; `Result<T>`
  para errores esperados y excepciones solo para defectos.
- Todo umbral de tiempo o de recuento vive en `data/catalogs/timings.json` y se genera como constante.
- Ningún tipo de UI por debajo de `Clicalo.UI.Wpf`; ninguna regla de producto en un ViewModel.
- Generadores: `IIncrementalGenerator`, `netstandard2.0`, salida determinista, con las utilidades de
  `generators/Clicalo.Generators/Common/` (`MiniJson`, `GeneratorContext.FilesIn/Profile/At`, `Polyfills`).
  Un error de datos es un diagnóstico con identificador propio y la línea y la columna del JSON.
- Pruebas: xUnit v3 y Shouldly (ya incluidos por `tests/Directory.Build.props`); `Clicalo.TestKit` ofrece
  `RepoPaths.Root`, `RepoPaths.Data` y `RepoPaths.Handoff`.
- Commits pequeños en [Conventional Commits](https://www.conventionalcommits.org/es/v1.0.0/) con los tipos y
  ámbitos de [CONTRIBUTING.md](CONTRIBUTING.md#títulos-de-pr-y-commits-conventional-commits), con
  `Signed-off-by` (DCO).

### APIs que solo pueden aparecer en un sitio

| API | Solo en | Alternativa |
|---|---|---|
| `SetForegroundWindow`, `AllowSetForegroundWindow` | `Platform.Windows/Foreground/ForegroundControl.cs` | `IForegroundOrchestrator` |
| `AttachThreadInput`, `LockSetForegroundWindow` | En ningún sitio | — |
| `Window.Activate`, `Window.Focus` sobre ventanas | En ningún sitio | Concesión `ControlCenter` |
| `TrackPopupMenu`, `TrackPopupMenuEx` | `Platform.Windows/Tray/TrayMenuHost.cs` | Concesión `TrayMenu` |
| `PInvoke.SendInput` | `Platform.Core/Injection`, detrás de `InjectionGate` | `IInputInjector` |
| `Process.Start`, `ProcessStartInfo` | `Platform.Windows/Launch` | `ILauncher` (sin intérprete) |
| `Assembly.Load*`, `AssemblyLoadContext.LoadFrom*` | En ningún sitio (ADR-0017) | Datos validados |
| `ShellExecute*`, `IShellDispatch2`, WMI (`System.Management`) | `Platform.Windows/Launch` y `Platform.Windows/SystemCommands` (hilo Shell) | `ILauncher`, `ISystemCommandRunner` |
| Escritura de archivos | `Infrastructure/Persistence/AtomicFile.cs` y el *sink* de registros | `IAtomicFileWriter` |
| `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Stopwatch.StartNew`, `Task.Delay` sin `TimeProvider`, `Thread.Sleep`, `Guid.NewGuid`, `Random.Shared` | Solo adaptadores | `TimeProvider`, `IIdGenerator` |
| `Task.Result`, `Task.Wait`, `GetAwaiter().GetResult()` | `App/Shutdown` | `await` |
| `Environment.Exit`, `Application.Shutdown` | `App/Lifecycle` | `IAppLifetime.ExitAsync` |
| `Popup`, `ContextMenu`, `ToolTip` interactivo, `ComboBox` | Nunca en las superficies del panel | `NonActivatingWindow` hijas |

La lista completa está en [§4.4 del plano](docs/architecture/blueprint.md#44-cómo-se-hacen-cumplir-las-reglas).

### Seguridad

- La IPC entre instancias solo tiene `Show`, `OpenUri` e `ImportFile` (vista previa): **nunca añadas un
  verbo que inyecte**, edite el documento o cambie el primer plano sin un ADR.
- Los secretos son tipos (`SecretText`, `Sensitive<T>`): nunca los conviertas en `string` ni los pases a un
  registro (CLC0003).
- Todo contenido importado (perfiles, copias, plantillas, respuestas de la IA) es no confiable: se valida
  contra su esquema y nunca se ejecuta al importar.
- No se cargan *plugins* ni *scripts* de terceros (ADR-0017).

## Mapa de capas

| Proyecto | Puede depender de | Nunca de |
|---|---|---|
| `Clicalo.Domain` (`net10.0`) | BCL (incluidos `Immutable` y `Frozen`) | Otros proyectos o paquetes; `System.IO`, `System.Net`, `Microsoft.Win32` |
| `Clicalo.Application` (`net10.0`) | Domain, `Microsoft.Extensions.Logging.Abstractions` | Presentation, UI, Platform, Infrastructure, WPF |
| `Clicalo.Presentation` (`net10.0`) | Application, Domain, CommunityToolkit.Mvvm | `System.Windows*` y demás ensamblados de WPF, Platform, Infrastructure |
| `Clicalo.UI.Wpf` | Presentation, Application (solo tipos de puerto de UI), Domain, WPF, CsWin32 (solo `Windowing/` y `Pointer/`) | Platform.Windows, Infrastructure |
| `Clicalo.Platform.Core` (compatible con AOT) | BCL y CsWin32 | Todo lo demás |
| `Clicalo.Platform.Windows` | Application (puertos), Domain, Platform.Core, CsWin32, proyecciones WinRT | Presentation, UI, Infrastructure, WPF |
| `Clicalo.Infrastructure` | Application (puertos), Domain, Platform.Core (solo `Trust`), System.Text.Json, Serilog, Velopack, M.E.AI | Presentation, UI, Platform.Windows, WPF |
| `Clicalo.App` | Todos | Nada lo referencia |
| `Clicalo.Sentinel` (Native AOT) | Platform.Core | Todo lo demás |
| `Clicalo.Launcher` (Native AOT) | Platform.Core (`Trust`) | Todo lo demás |

Cada capacidad es un espacio de nombres `Clicalo.<Capa>.<Módulo>` (por ejemplo `Clicalo.Domain.KeySafety`).
Su API pública son los tipos `public` de la raíz del módulo; los detalles son `internal` o viven en
`.Internal`, y ningún módulo usa el `.Internal` de otro. La matriz de dependencias entre módulos está en
[§4.3 del plano](docs/architecture/blueprint.md#43-módulos-por-capacidad).

## Mapa de hilos

| Hilo | Dueño de | Nunca hace |
|---|---|---|
| UI (roles Surfaces y Workspace) | Ventanas no activables, `PointerInputSource`, `SessionStore` e `InteractionStore` (Surfaces); Centro de control y bienvenida (Workspace) | E/S, esperas, `SendInput`, `SetForegroundWindow` |
| Engine | `EngineHost`, escritura del *ledger*, `SendInput` a través de `InjectionGate` | E/S de disco o red, llamadas a la UI, esperas bloqueantes, `ShellExecute`, WMI |
| SysEvents | *Hooks* de WinEvent, sesión, energía, bandeja, portapapeles, `ForegroundOrchestrator`, `PointerPositionTracker`, `EmergencyReleaser` | Lógica de negocio y llamadas que puedan bloquear |
| Shell | Lanzar apps y webs, comandos de sistema | Tocar el *ledger* o la UI |
| Hook (bajo demanda) | `WH_KEYBOARD_LL` y `WH_MOUSE_LL` temporales | Cualquier cosa distinta de escribir en un anillo prealocado |
| Persistence | Serializar, validar y escribir el documento y el uso; copias | Tocar la UI |

Entre hilos solo cruzan objetos inmutables, y cada punto de mutación tiene un único escritor. Detalle en
[§3.2 del plano](docs/architecture/blueprint.md#32-modelo-de-hilos).

## Verbos de `cl`

Disponibles desde M0: `setup`, `build`, `fast`, `test`, `desk`, `fix`, `check` y `clean`. Llegan después:
`pr` (M1); `run`, `note` y `perf` (M2); `states`, `accept` y `trace` (M3); `beta` y `sign-manifest` (M5).
Cada orden termina en una línea legible por Narrador. Las órdenes que no son de compilación viven en
`tools/Clicalo.DevCli` (`i18n-check`, `i18n-import`, `adr-check`). Detalle, pasos de `cl check` y
variables de entorno: [tooling.md](docs/architecture/tooling.md#verbos-de-cl).

## Dónde está cada cosa

| Qué | Dónde |
|---|---|
| Plano normativo | `docs/architecture/blueprint.md` |
| Decisiones difíciles de revertir | `docs/adr/` |
| Desviaciones del plano | `docs/architecture/deviations.md` |
| Requisitos | `docs/requirements/catalog.md` (cómo leerlo: `docs/requirements/README.md`) |
| Paquete de diseño original (solo lectura) | `docs/design/handoff/` (qué es vinculante: `LEEME-VINCULANTE.md`) |
| Datos del producto (fuente de verdad no código) | `data/` (`i18n/`, `tokens/`, `catalogs/`, `content/`, `schemas/`) |
| Reglas de arquitectura como datos | `architecture/` |
| Código del producto | `src/` |
| Generadores y analizadores | `generators/` |
| Pruebas y utilidades de prueba | `tests/` (`Clicalo.TestKit`) |
| Destinos de `cl` y herramientas | `build/` y `tools/` |
| Configuración común de compilación | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `global.json`, `nuget.config` |
| Seguridad y privacidad | `docs/security/`, `SECURITY.md`, `PRIVACY.md` |
| Guías | `docs/guides/` |
