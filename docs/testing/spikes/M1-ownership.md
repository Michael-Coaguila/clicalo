# M1 · Reparto de rutas entre paquetes

Los spikes bloqueantes S1, S3 y S4 los construyen cinco paquetes en paralelo, cada uno en su *worktree* y su rama
(`m1/<paquete>`, salida de `m1/spikes`). Este documento dice **qué rutas puede crear o modificar cada paquete**, qué
interfaces **consume** y cuáles **expone**, para que no haya solapes. Los contratos (firmas públicas, puertos y
proyectos) ya están en `m1/spikes` desde la rama `m1/contracts` y compilan con cuerpos `NotImplementedException`
marcados con `[SuppressMessage("Design", "MA0025", …)]`: el paquete dueño sustituye el cuerpo y quita la supresión.

Guiones de cada spike: [S1](S1.md) (no activación), [S3](S3.md) (UI Automation) y [S4](S4.md) (primer plano por
origen).

## Reglas comunes

1. **Solo tus rutas.** Una ruta que no está en tu lista no se toca. Si necesitas cambiarla, descríbelo en
   `sharedChangesNeeded` con el archivo, el cambio exacto y el motivo; lo aplica la integración.
2. **Las firmas públicas de los contratos no cambian.** Puedes implementar cuerpos, añadir tipos `internal` (o en un
   espacio `.Internal` de tu módulo) y añadir miembros públicos **nuevos** en tipos de tu paquete si nadie más los
   necesita. Cambiar o quitar un miembro público existente, o tocar `src/Clicalo.Application/Ports/**`, es un cambio
   compartido.
3. **Archivos compartidos por secciones.** En los archivos con secciones por paquete solo se añaden líneas dentro de
   la sección propia (al final de ella): `src/Clicalo.UI.Wpf/NativeMethods.txt` (secciones Windowing y Pointer) y
   `data/catalogs/timings.json` (un grupo por paquete, abajo). Así las fusiones no chocan.
4. **Congelado en M1** (solo la integración): `Clicalo.slnx`, `Core.slnf`, `architecture/**`, `Directory.*`,
   `nuget.config`, `global.json`, `.editorconfig`, `.github/**`, `src/Clicalo.UI.Wpf/Clicalo.UI.Wpf.csproj` (ruta
   sensible: exige ADR), `src/Clicalo.UI.Wpf/NativeMethods.json`, `src/Clicalo.Domain/Geometry/**` (salvo
   añadidos del paquete `pointer`), `tests/Clicalo.Windowing.IntegrationTests/Clicalo.Windowing.IntegrationTests.csproj`,
   `tests/Clicalo.Windowing.IntegrationTests/Desktop/**`,
   `tests/Clicalo.Windowing.IntegrationTests/RequirementTraitsTests.cs`, `data/i18n/**` (M1 no añade textos de producto: SpikeLab usa
   literales), `docs/architecture/**`, `docs/adr/**`, `docs/requirements/**`, `CHANGELOG.md`, `AGENTS.md`.
5. **Pruebas de escritorio.** Llevan `[Trait("Requires", "Desktop")]`, `[Collection(DesktopCollectionDefinition.Name)]`
   y `[DesktopFact]`/`[DesktopTheory]`. En local se ejecutan **como mucho una vez por paquete** con `.\cl.cmd desk`;
   la CI (`windows-2025`) las ejecuta en cada PR. Toda inyección comprueba el destino **inmediatamente antes de cada
   lote** (teclado: `GetForegroundWindow`; puntero: `WindowFromPoint` en el punto exacto y que sea de la prueba o de
   InputProbe), incluye sus liberaciones en el mismo lote y nunca usa AltGr ni Ctrl derecho en local.
6. **Cada paquete termina con `.\cl.cmd check` en verde**, commits Conventional Commits con el ámbito de su paquete y
   sin *push*.

## Orden de integración y dependencias entre paquetes

Todos trabajan a la vez contra los contratos. Lo que no puede probarse hasta que otro paquete se integra se escribe
igualmente y se valida al rebasar sobre `m1/spikes`:

| Paquete | Necesita implementado, de otro paquete | Mientras tanto |
|---|---|---|
| `windowing` | Nada (usa un `IActivationArbiter` de prueba propio) | — |
| `pointer` | `SyntheticPointer` (de `windowing`) para sus pruebas de escritorio | Pruebas de dominio sin escritorio; las de escritorio se validan tras integrar `windowing` |
| `automation` | Superficies reales (`windowing`) para las pruebas con FlaUI | Pruebas en proceso del *peer*; las de FlaUI tras integrar `windowing` |
| `foreground` | `SurfaceRegistry` y `SyntheticPointer` (`windowing`); `ShortcutTile` (`automation`) para el origen UIA | Pruebas con falsos en `Application.Tests`; las de escritorio tras integrar los otros |
| `spikelab` | Todo lo anterior | Compone contra los contratos; lo que aún lance `NotImplementedException` se muestra en rojo en la franja de estado |

Orden de fusión recomendado en `m1/spikes`: **windowing → pointer y automation → foreground → spikelab**.

## Paquete `windowing` (S1)

Commits: `feat(windowing)`, `test(windowing)`, `docs(windowing)`…

**Rutas propias:**

- `src/Clicalo.UI.Wpf/Windowing/**`: `NonActivatingWindow.cs`, `SurfaceRegistry.cs`, `ActivationGuard.cs`,
  `ActivationViolationEventArgs.cs`, `SurfaceIntegrityCheck.cs`, `OwnerAnchor.cs` y los archivos nuevos que haga
  falta (por ejemplo `Windowing/Internal/SurfaceHook.cs`).
- `src/Clicalo.UI.Wpf/NativeMethods.txt`: **solo** la sección «Windowing».
- `tests/Clicalo.TestKit.Windows/Input/SyntheticPointer.cs` y `SyntheticPointerKind.cs` (implementación, con las
  reglas de seguridad de su documentación), `tests/Clicalo.TestKit.Windows/NativeMethods.txt` (añadir lo que
  necesite `SyntheticPointer`) y archivos nuevos en `tests/Clicalo.TestKit.Windows/Input/` para el puntero.
- `tests/Clicalo.Windowing.IntegrationTests/Windowing/**` (incluidas sus superficies y árbitro de prueba).
- `data/catalogs/timings.json`: **solo** el grupo `Windowing`.
- `docs/testing/spikes/S1.md`: nombres de las pruebas automáticas si cambian, «Resultados» y la decisión.

**Consume:**

- `Clicalo.Application.Ports`: `IActivationArbiter`, `ActivationViolation`, `ActivationMessage`,
  `ActivationCause`, `SurfaceId`, `SurfaceKind`, `WindowToken`.
- `Clicalo.Domain.Geometry`: `PhysicalRect`, `PhysicalPoint`.
- `Clicalo.UI.Wpf.Pointer.PointerSetup.DisableTouchFeedback` (ya implementado en los contratos).
- `Clicalo.Domain.Timing.Timings.Foreground.SurfaceIntegrityInterval` y `Timings.Windowing.ViolationRestoreBudget`.
- `Clicalo.TestKit.Windows`: `InputProbeSession`, `ForegroundWindows`, `WpfThread`, `DesktopTestEnvironment`.

**Expone:**

- `NonActivatingWindow` (`ShowPassive`, `HidePassive`, `MovePassive`, `Id`, `SurfaceWindow`,
  `OnSourceInitialized` sellado, `OnSurfaceInitialized` virtual).
- `SurfaceRegistry`, que implementa `ISurfaceActivationStyle` e `ISurfaceLookup` (lo usa `foreground`).
- `ActivationGuard` (`Violations`, `MetricName`, `ViolationDetected`, `OnActivated`), `SurfaceIntegrityCheck`,
  `OwnerAnchor`.
- `SyntheticPointer` (lo usan `pointer`, `automation` y `foreground` en sus pruebas de escritorio).

## Paquete `pointer` (capa de punteros; apoya S1 y prepara S2)

Commits: `feat(touch)`, `test(touch)`, `docs(touch)`…

**Rutas propias:**

- `src/Clicalo.Domain/Touch/**`: `GestureRecognizer`, `TouchFilter` y sus tipos, más los archivos nuevos del módulo.
- `src/Clicalo.Domain/Geometry/**`: **solo añadidos** (miembros o tipos nuevos); lo existente está congelado porque lo
  consumen todos.
- `src/Clicalo.UI.Wpf/Pointer/**`: `PointerInputSource.cs`, `IPointerFrameSink.cs`, `PointerSetup.cs` y archivos
  nuevos.
- `src/Clicalo.UI.Wpf/NativeMethods.txt`: **solo** la sección «Pointer».
- `tests/Clicalo.Domain.Tests/Touch/**` y `tests/Clicalo.Domain.Tests/Geometry/**` (incluidas trazas grabadas en
  `tests/Clicalo.Domain.Tests/Touch/Fixtures/**` y los generadores CsCheck de trazas).
- `tests/Clicalo.Windowing.IntegrationTests/Pointer/**`.
- `data/catalogs/timings.json`: **solo** el grupo `Touch` (por ejemplo, el umbral de palma).

**Consume:** `Clicalo.Domain.Timing.Timings.Touch.*`, `Clicalo.Domain.Catalog.TouchPreset` (solo en pruebas: la
conversión a `TouchSettings` la hace quien llama, porque `Touch` no depende de `Catalog` en `domain-modules.json`),
`Clicalo.Application.Ports.WindowToken`, `SyntheticPointer` (de `windowing`).

**Expone:** `PointerSample`, `PointerFrame`, `PointerKind`, `PointerPhase`, `PointerInputOrigin`, `TouchSettings`,
`TouchTarget`, `TouchTargetId`, `TouchTargetKind`, `GestureRecognizer`, `GestureEvent`, `GestureKind`,
`IgnoreReason`, `HoldEndReason`, `SwipeDirection`, `TouchFilter`, `ButtonFilterState`, `ContactSummary`,
`TouchVerdict`; `PointerInputSource` e `IPointerFrameSink`; `PointerSetup`.

## Paquete `automation` (S3)

Commits: `feat(a11y)`, `test(a11y)`, `docs(a11y)`…

**Rutas propias:**

- `src/Clicalo.UI.Wpf/Automation/**`: `ShortcutTile.cs`, `ShortcutTileAutomationPeer.cs`, `ShortcutTilePattern.cs`,
  `LiveAnnouncer.cs`, `AnnouncementUrgency.cs` y archivos nuevos (por ejemplo el *peer* de la región *live*).
- `tests/Clicalo.Windowing.IntegrationTests/Automation/**` (FlaUI.UIA3 y Axe.Windows ya están referenciados).
- `docs/testing/spikes/S3.md`: nombres de las pruebas automáticas si cambian, «Resultados» y la decisión.

**Consume:** `NonActivatingWindow` y `SurfaceRegistry` (de `windowing`) para las pruebas con ventana real;
`IActivationArbiter` con un falso propio; `WpfThread`, `InputProbeSession` y `ForegroundWindows` de TestKit.Windows.

**Expone:** `ShortcutTile` (propiedades `AccessibleName`, `VoiceNumber`, `Pattern`, `ToggleState`, `IsExpanded`,
`AutomationName`; eventos `Invoked`, `Toggled`, `ExpandRequested`, `CollapseRequested`),
`ShortcutTileAutomationPeer`, `ShortcutTilePattern`, `LiveAnnouncer` (`Announce`, `LastText`, `ActivityId`) y
`AnnouncementUrgency`.

## Paquete `foreground` (S4)

Commits: `feat(foreground)` (o `feat(platform)` para los adaptadores), `test(foreground)`, `docs(foreground)`…

**Rutas propias:**

- `src/Clicalo.Application/Foreground/**`: la implementación `ForegroundOrchestrator.cs` (implementa
  `IForegroundOrchestrator` e `IActivationArbiter`), los cuerpos de `ForegroundLease` y los tipos internos. Las
  firmas públicas existentes no cambian.
- `src/Clicalo.Platform.Windows/Foreground/**` (`ForegroundControl.cs`: el único `SetForegroundWindow`, con su
  `[SuppressMessage("ApiDesign", "RS0030…")]` ya registrado en `banned-api-exceptions.json`;
  `InternalRightsHotkey.cs`; `TouchKeyboard.cs`).
- `src/Clicalo.Platform.Windows/SysEvents/**` (`SysEventsThread.cs`, `ForegroundMonitor.cs`).
- `src/Clicalo.Platform.Windows/Tray/**` (`TrayIcon.cs`, `TrayMenuHost.cs`: el único `TrackPopupMenuEx`, también
  registrado; `TrayMenuItem.cs`, `TrayIconEventArgs.cs`).
- `src/Clicalo.Platform.Windows/NativeMethods.txt` y `NativeMethods.json` (nuevos; nadie más los toca) y
  `src/Clicalo.Platform.Windows/Clicalo.Platform.Windows.csproj` solo para añadir `InternalsVisibleTo`.
- `tests/Clicalo.Application.Tests/Foreground/**` (falsos de los puertos incluidos).
- `tests/Clicalo.Platform.IntegrationTests/Foreground/**`.
- `tests/Clicalo.Windowing.IntegrationTests/Foreground/**` (incluida `GuardedInternalKeyEffects`, la implementación
  de prueba de `IInternalKeyEffects` sobre `TestKeyboardInjector`).
- `data/catalogs/timings.json`: **solo** el grupo `Foreground` (ajustar `RestoreRetryDelay` y
  `RightsHotkeyTimeout` con lo medido y añadir entradas).
- `docs/testing/spikes/S4.md`: nombres de las pruebas automáticas si cambian, «Resultados» y la decisión.

**Consume:** todos los puertos de primer plano de `Clicalo.Application.Ports` (`IForegroundControl`,
`IForegroundMonitor`, `ExternalForeground`, `ExternalForegroundChangedEventArgs`, `IInternalRightsHotkey`,
`IInternalKeyEffects`, `ITouchKeyboard`, `ISurfaceActivationStyle`, `ISurfaceLookup`, `WindowToken`, `SurfaceId`);
`SurfaceRegistry` y `SyntheticPointer` (de `windowing`) y `ShortcutTile` (de `automation`) en las pruebas de
escritorio; `Clicalo.Domain.Geometry`; `Timings.Foreground.*`.

**Expone:** `IForegroundOrchestrator` y `ForegroundOrchestrator`, `LeaseKind`, `LeaseOrigin`, `LeaseRequest`,
`LeaseResult` (`Granted`, `Denied`), `ForegroundLease`, `ForegroundSnapshot`, `ForegroundEpoch`, `RestoreOutcome`,
`ForegroundDenialReason`; los adaptadores `ForegroundControl`, `ForegroundMonitor`, `SysEventsThread`,
`InternalRightsHotkey`, `TouchKeyboard`, `TrayIcon`, `TrayMenuHost`, `TrayMenuItem`.

## Paquete `spikelab` (laboratorio de S1, S3 y S4)

Commits: `test(build)` para el laboratorio (no es producto) y `docs(build)` para los guiones…

**Rutas propias:**

- `tools/SpikeLab/**`: todo, incluidos `SpikeLab.csproj`, `packages.lock.json`, `NativeMethods.txt`, las superficies
  de laboratorio (derivan de `NonActivatingWindow`; CLC0001 aplica porque el proyecto activa
  `ClicaloProductRules`), la ventana de control, el CC de laboratorio, la franja de estado, los informes en
  `%LOCALAPPDATA%\Clicalo.SpikeLab\reports` y su implementación protegida de `IInternalKeyEffects`; también sus
  pruebas unitarias en `tools/SpikeLab/Tests/**` (`SpikeLab.Tests`).
- `docs/testing/spikes/README.md`: cómo se abre SpikeLab, qué muestra y el formato de sus informes.
- `.vscode/tasks.json`: **una** tarea nueva, «SpikeLab: abrir».
- En `docs/testing/spikes/S1.md`, `S3.md` y `S4.md`: **solo** las líneas de «Preparación» que describen SpikeLab, si
  la interfaz final cambia algún nombre de botón.

**Consume:** todo lo que exponen los otros cuatro paquetes, `InputProbeSession` y los inyectores de prueba de
`Clicalo.TestKit.Windows` (la sonda es la app objetivo de los ciclos de voz de S4).

**Expone:** nada que use el producto.

## Estado de los contratos al empezar

| Contrato | Estado |
|---|---|
| `Clicalo.Domain.Geometry` (`PhysicalPoint`, `PhysicalRect`) | Implementado |
| `Clicalo.Domain.Touch` (tipos de valor) | Implementado; `GestureRecognizer` y `TouchFilter` pendientes |
| `Clicalo.Application.Ports` | Completo (interfaces y tipos de valor) |
| `Clicalo.Application.Foreground` | Tipos completos; `ForegroundLease.RestoreAsync`/`DisposeAsync` pendientes; falta `ForegroundOrchestrator` |
| `Clicalo.UI.Wpf.Windowing` | Firmas; cuerpos pendientes salvo el contador de `ActivationGuard` |
| `Clicalo.UI.Wpf.Pointer` | `PointerSetup` implementado; `PointerInputSource` pendiente |
| `Clicalo.UI.Wpf.Automation` | `ShortcutTile` y su *peer* implementados; `LiveAnnouncer` pendiente |
| `Clicalo.Platform.Windows` (`Foreground`, `SysEvents`, `Tray`) | Firmas; cuerpos pendientes |
| `Clicalo.TestKit.Windows.Input.SyntheticPointer` | Firmas; cuerpos pendientes |
| `tests/Clicalo.Windowing.IntegrationTests` | Proyecto con una prueba de humo en `Windowing/`, `Pointer/` y `Automation/` |
| `tools/SpikeLab` | Proyecto WinExe WPF que abre la ventana de control |
| `data/catalogs/timings.json` | Nuevos: `Foreground.RestoreRetryDelay` (50 ms) y `Foreground.RightsHotkeyTimeout` (500 ms), provisionales hasta S4; grupo `Windowing` con `ViolationRestoreBudget` (200 ms) |
