# M2 · Reparto de rutas entre paquetes

M2 «Esqueleto andante» ([§14 del plano](../../architecture/blueprint.md#14-hoja-de-ruta-por-hitos)) lo construyen cinco
paquetes en paralelo, cada uno en su *worktree* y su rama (`m2/<paquete>`, salida de `m2/skeleton`): **domain**,
**engine**, **persistence**, **migration** y **app**. Este documento dice qué rutas puede crear o modificar cada
paquete, qué consume y qué expone, para que no haya solapes. Los contratos (firmas públicas, puertos, proyectos y
módulos) ya están en `m2/skeleton` desde la rama `m2/contracts` y compilan con cuerpos `NotImplementedException`
marcados con `[SuppressMessage("Design", "MA0025", …)]` en el tipo: **el paquete dueño sustituye los cuerpos y quita la
supresión** cuando el tipo ya no lanza. Lo que decidieron los contratos está en
[D-20](../../architecture/deviations.md#d-20--contratos-de-m2) y en
[ADR-0018](../../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md).

Evidencia y decisiones de M1: [M1-closure.md](M1-closure.md). Spikes de este hito, como pruebas: **S5** (`app`),
**S7** y **S9** (`engine`) y **S11** (`persistence`).

## Reglas comunes

1. **Solo tus rutas.** Una ruta que no está en tu lista no se toca. Si necesitas cambiarla, descríbelo en
   `sharedChangesNeeded` con el archivo, el cambio exacto y el motivo; lo aplica la integración.
2. **Las firmas públicas de los contratos no cambian.** Puedes implementar cuerpos, añadir tipos `internal` (o en un
   espacio `.Internal` de tu módulo) y añadir miembros o tipos públicos **nuevos** en tus módulos. Cambiar o quitar un
   miembro público existente, o tocar `src/Clicalo.Application/Ports/**`, es un cambio compartido.
3. **Archivos compartidos por secciones.** Solo se añaden líneas al final de la sección propia:
   - `data/catalogs/timings.json`: cada paquete solo sus grupos (abajo). Un grupo nuevo se añade al final del archivo
     con el nombre del paquete en su `description`.
   - `src/Clicalo.Platform.Windows/NativeMethods.txt` y `src/Clicalo.Platform.Core/NativeMethods.txt` (nuevo): una
     sección por paquete, encabezada por un comentario `// <paquete>`.
4. **Congelado en M2** (solo la integración): `Clicalo.slnx`, `Core.slnf`, `architecture/**`, `Directory.*`,
   `nuget.config`, `global.json`, `.editorconfig`, `.github/**`, todos los `*.csproj` existentes (salvo lo que se
   indica abajo), `src/Clicalo.Application/Ports/**`, `data/i18n/**` salvo para el paquete `app`, `docs/architecture/**`,
   `docs/adr/**`, `docs/requirements/**`, `CHANGELOG.md`, `AGENTS.md` y los generadores y analizadores
   (`generators/**`).
5. **Textos de producto.** Solo el paquete `app` toca `data/i18n/**` (con `cl i18n-import`). Los demás usan las claves
   que ya existen y piden las nuevas en `sharedChangesNeeded` (clave, texto ES, texto EN y marcadores); mientras
   tanto, sus pruebas usan claves existentes.
6. **Seguridad en el equipo del usuario.** Nunca se inyecta entrada real fuera de InputProbe o de una ventana de prueba
   propia: se comprueba el destino justo antes de cada lote (`GetForegroundWindow` o `WindowFromPoint`) y se aborta sin
   inyectar si no coincide; las liberaciones van en el mismo lote; nunca AltGr ni Ctrl derecho en local. Se usa el mutex
   de escritorio `Global\Clicalo.DesktopTests`. Las pruebas que matan procesos con teclas pulsadas, congelan hilos con
   inyección real o bloquean la sesión son **solo para la CI**: `[Trait("Requires", "Desktop")]` **y**
   `[Trait("Category", "Chaos")]`. Nunca se lanza `Clicalo.exe` con envío de teclas activo en el equipo del usuario.
   Pruebas de escritorio en local: **como mucho una vez por paquete** (`.\cl.cmd desk`); la CI es la fuente de verdad.
7. **Cada paquete termina con `.\cl.cmd check` en verde**, `[Trait("Req", "<ID>")]` en cada requisito verificado,
   commits Conventional Commits con los ámbitos de §13 y sin *push*.

## Orden de integración y dependencias

Todos trabajan a la vez contra los contratos; lo que no puede probarse hasta que otro paquete se integra se escribe
igualmente y se valida al rebasar sobre `m2/skeleton`.

| Paquete | Necesita implementado, de otro paquete | Mientras tanto |
|---|---|---|
| `domain` | Nada | — |
| `engine` | `KeyChord`, `ShortcutLibrary` y `ShortcutCompleteness` (`domain`) para el reductor completo | Pruebas del *ledger*, la valla, Sentinel y los planificadores con acciones construidas a mano; `KeyChord.Create` con un doble local en las pruebas |
| `persistence` | `ShortcutLibrary.CreateValidated`, `UserDocument.Validate`, `SettingsSchema` y `SliceDiff` (`domain`) | Envoltorio, `AtomicFile`, cuarentena y S11 sobre bytes; los *mappers* se validan al integrar `domain` |
| `migration` | `KeyChord.Create`, `ShortcutLibrary.CreateValidated`, `SettingsSchema` (`domain`); `IBackupService.KeepV1OriginalAsync` (`persistence`) | Lector v1, tokenizador y `SafeZipReader` completos; el conversor se valida al integrar `domain` |
| `app` | Todo lo anterior | Composición contra los contratos; lo que aún lance `NotImplementedException` no se registra |

Orden de fusión recomendado en `m2/skeleton`: **domain → engine y persistence → migration → app**.

## Paquete `domain` (núcleo del dominio, almacén y confirmación)

Commits: `feat(data)`, `test(data)`, `feat(editor)` para la confirmación, `docs(data)`…

**Rutas propias:**

- `src/Clicalo.Domain/{Primitives,Errors,Privacy,Library,Settings,Frequents,Duplicates,Document,Commands}/**`.
- `src/Clicalo.Domain/Keys/**` y `src/Clicalo.Domain/Catalog/**` salvo lo generado (nunca se edita lo generado; los
  generadores están congelados).
- `src/Clicalo.Application/Store/**` y `src/Clicalo.Application/Confirmation/**`.
- `src/Clicalo.Domain/ProfileResolution/**` (la tabla PER-001 a PER-008; módulo declarado en `domain-modules.json`).
- `tests/Clicalo.Domain.Tests/{Primitives,Errors,Privacy,Keys,Library,Settings,Frequents,Duplicates,Document,Commands,Generators,ProfileResolution}/**`
  (los generadores CsCheck de `KeyChord` y `UserDocument` van en `Generators/`).
- `tests/Clicalo.Application.Tests/{Store,Confirmation}/**` y, en
  `tests/Clicalo.Application.Tests/Clicalo.Application.Tests.csproj`, **solo** un `<Compile Include>` enlazado a
  `tests/Clicalo.Domain.Tests/Generators/**` para compartir los generadores.
- `tests/Clicalo.Architecture.Tests/ProductRuleTests.cs` y `ProductRules.cs`: **solo** para activar la parte de
  comportamiento de R4 y R7 (aplicar los comandos a documentos generados), sin cambiar las reglas estructurales.

**Consume:** `Timings.Confirmation.*`, `Timings.Persistence.UndoDepth`, `Timings.Macro.*`, `KeyDefinitions` y `KeyIds`
(generados), `IBackupService.SnapshotNow` (puerto), `architecture/destructive-operations.json` y
`architecture/undo-exemptions.json` (solo lectura: los comandos se llaman exactamente así).

**Expone:** `ValueList<T>`, `LocalizedText`, `Result<T>`, `Results` y `Failure`; `SecretText`; `KeyChord.Create`,
`CanonicalChord`; `ShortcutLibrary` con sus invariantes I1 a I6, `ShortcutCompleteness`; `UserSettings` y
`SettingsSchema` (`Defaults`, `All`, `Clamp`); `UserDocument.Validate` y `RestoreSlices`, `SliceDiff`; los comandos de
M2 (crear, editar, mover y borrar atajos y perfiles, vincular, `SetSetting`, `RecordUsage`); `DocumentStore` con deshacer
por porciones (20 entradas, agrupación, sellado); `TwoStepConfirm` y `ConfirmationToken`.

**Timings:** grupo `Confirmation`.

## Paquete `engine` (motor, *ledger* v2, valla y Sentinel; S7 y S9)

Commits: `feat(engine)`, `feat(keysafety)`, `feat(platform)` para los adaptadores, `test(engine)`, `docs(engine)`…

**Rutas propias:**

- `src/Clicalo.Domain/{KeySafety,Execution}/**` (y `StickyModifiers/**` si adelanta algo de M3).
- `src/Clicalo.Application/Engine/**`.
- `src/Clicalo.Platform.Core/**`: `KeyLedger/`, `Injection/`, `Guardian/`, `NativeMethods.txt` y `NativeMethods.json`
  (nuevos) y, en `Clicalo.Platform.Core.csproj`, **solo** `InternalsVisibleTo`.
- `src/Clicalo.Platform.Windows/Input/**` (nuevo: adaptadores de `IInputInjector` e `IKeyLedger` sobre
  `InjectionGate`, `EmergencyReleaser` en SysEvents, soltado preventivo del arranque con máscara, SEG-006, y la
  implementación de `IInternalKeyEffects` tras la valla, como anunciaba D-14), `src/Clicalo.Platform.Windows/SentinelHost/**`
  (nuevo: lanzar Sentinel con `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, latido por el pipe, relanzar con
  `Timings.Guardian.RestartBackoff`) y la sección `// engine` de `src/Clicalo.Platform.Windows/NativeMethods.txt`.
- `src/Clicalo.Sentinel/**` (`Program.Main` llama a `SentinelEntryPoint.Run`).
- `tests/Clicalo.Domain.Tests/{KeySafety,Execution}/**`, `tests/Clicalo.Application.Tests/Engine/**`,
  `tests/Clicalo.Sentinel.Tests/**` y `tests/Clicalo.Platform.IntegrationTests/Engine/**` (nuevo).
- `docs/testing/spikes/S7.md` y `docs/testing/spikes/S9.md` (nuevos).

**Consume:** `Shortcut` y las acciones, `KeyChord`, `ShortcutCompleteness` (`domain`); `KeyDefinitions` y
`data/catalogs/keys.win32.json` (solo lectura); `Timings.Engine.*`, `Timings.Injection.*`, `Timings.KeySafety.*`,
`Timings.Guardian.*`, `Timings.App.CrashLoop`; `InputProbeSession`, `TestKeyboardInjector` y el mutex de escritorio de
`Clicalo.TestKit.Windows` (solo lectura); los puertos de `Application.Ports` del motor.

**Expone:** `EngineReducer`, `ActivationPolicy` y los planificadores Tap, Hold y Toggle en los dos modos;
`KeyboardLedger`; `EngineHost` y `EngineMailbox`; `KeyLedgerSection`, `InjectionGate`, `LowLevelInjector`,
`LedgerRelease`; `SentinelStartInfo` y `RelaunchPolicy`; `Clicalo.Sentinel.exe`; los adaptadores de `Platform.Windows/Input`
y `SentinelHost`.

**Criterios de M2 que cierra:** propiedades INV-1 a INV-12, «muerte en cada paso» y «congelar y reanudar» con 10 000
casos; caos de Sentinel 50 de 50 (solo CI, `Category=Chaos`); S7 y S9.

**Timings:** grupos `Engine`, `Injection`, `KeySafety` y `Guardian`.

## Paquete `persistence` (documento, uso, copias y cuarentena; S11)

Commits: `feat(data)`, `test(data)`, `docs(data)`…

**Rutas propias:**

- `src/Clicalo.Infrastructure/Persistence/**` (incluidos `Dto/`, `Mappers/` y el `JsonSerializerContext`) y
  `src/Clicalo.Infrastructure/Backup/**`. `AtomicFile.cs` es el único escritor de archivos (su excepción de RS0030 ya
  está en `architecture/banned-api-exceptions.json`); `File.Replace` es `ReplaceFileW`, y la escritura directa usa
  `FileOptions.WriteThrough` y `Flush(flushToDisk: true)`.
- `src/Clicalo.Application/Persistence/**` (`PersistenceScheduler`).
- `src/Clicalo.Infrastructure/Logging/**` y `tests/Clicalo.Infrastructure.Tests/Logging/**` (el registro de producto;
  `banned-api-exceptions.json` espera ahí su *sink*).
- `data/schemas/document.schema.json` y `data/schemas/usage.schema.json` (nuevos; formato persistido de ADR-0007).
- `tests/Clicalo.Infrastructure.Tests/{Persistence,Backup,Fixtures/schema}/**` y
  `tests/Clicalo.Application.Tests/Persistence/**`.
- `docs/testing/spikes/S11.md` (nuevo).

**Consume:** `UserDocument`, `ShortcutLibrary.CreateValidated`, `SettingsSchema`, `SliceDiff`, `SecretText.WithRevealed`
en los *mappers* (permitido en `Infrastructure.Persistence`, D-10), `DocumentStore.Changed`; `Timings.Persistence.*`
y `Timings.Backups.*`; los puertos de persistencia.

**Expone:** `DocumentRepository`, `UsageRepository`, `BackupService`, `AtomicFile`, `QuarantineStore`,
`EnvelopeCodec`, `DataLocations`, `PersistenceScheduler`.

**Criterios de M2 que cierra:** `CrashingFileSystem` sin ningún documento perdido; 10 cambios en 1 s, una escritura;
1000 ejecuciones, ninguna escritura del documento; S11.

**Timings:** grupos `Persistence` y `Backups`.

## Paquete `migration` (importador v1 y `SafeZipReader`)

Commits: `feat(migration)`, `test(migration)`, `docs(migration)`…

**Rutas propias:**

- `src/Clicalo.Domain/Migration/V1/**` y `src/Clicalo.Infrastructure/Migration/**`.
- `src/Clicalo.Platform.Windows/Legacy/**` (nuevo, opcional en M2: `ILegacyInstallLocator` de MIG-001; su puerto se pide
  en `sharedChangesNeeded`).
- `tests/Clicalo.Domain.Tests/Migration/**` y `tests/Clicalo.Infrastructure.Tests/{Migration,Fixtures/v1}/**`.
- `tools/Clicalo.DevCli/**` (la orden `anonymize-v1`; nadie más toca DevCli en M2) y
  `tests/Clicalo.DevCli.Tests/AnonymizeV1/**`.

**Privacidad de los *fixtures*:** los tres `profiles.json` reales del usuario (`Documentos\Macro Quick Access\`) se leen
en local, **solo lectura**, y se versionan únicamente **anonimizados** con `anonymize-v1` (conserva combinaciones,
recuentos, colores y estructura; sustituye etiquetas, nombres y procesos no públicos). El original nunca entra en el
repositorio ni en un registro.

**Consume:** `KeyChord.Create`, `ShortcutLibrary.CreateValidated`, `SettingsSchema.Opacity` y `Defaults`,
`UserDocument`, `DuplicatePolicy` y el índice de repetidos (`domain`); `IBackupService.KeepV1OriginalAsync`
(`persistence`); `Timings.Import.*`.

**Expone:** `V1Reader`, `V1ComboTokenizer`, `V1Converter`, `V1Importer`, `SafeZipReader`.

**Criterio de M2 que cierra:** importación de los 3 archivos reales 210 → 210, ningún token vacío (MIG-003, MIG-004),
y los zips hostiles de CsCheck (MIG-009).

**Timings:** grupo `Import`.

## Paquete `app` (composición, panel mínimo, bandeja; S5)

Commits: `feat(panel)`, `feat(platform)` para la bandeja, `feat(build)` para los verbos de `cl`, `test(panel)`…

**Rutas propias:**

- `src/Clicalo.App/**` (salvo `app.manifest`, ruta sensible): `Program`, composición, ciclo de vida (Sentinel lanzado en
  paralelo al primer frame, soltado preventivo, vaciado síncrono al salir), `Shutdown/`.
- `src/Clicalo.Presentation/Panel/**` y `src/Clicalo.Application/{Session,Coordinators}/**` (panel mínimo con un perfil y
  el controlador que construye `EngineEvent.Activation`).
- `src/Clicalo.UI.Wpf/Surfaces/**` (el panel sobre `NonActivatingWindow`; CLC0001 y CLC0002 aplican).
- `src/Clicalo.Platform.Windows/Tray/**` (en M2 pasa de `foreground` a `app`: «Soltar todo» manda
  `EngineEvent.ReleaseAll`).
- `data/i18n/**` (el único paquete que añade claves; recoge las que pidan los demás).
- `build/**` (verbos `run`, `note` y `perf` de M2) y `.vscode/tasks.json` (una tarea por verbo nuevo).
- `tests/Clicalo.Performance/**` (S5 y el presupuesto toque → `SendInput` de p95 ≤ 50 ms),
  `tests/Clicalo.Windowing.IntegrationTests/Panel/**` (nuevo) y `tests/Clicalo.Application.Tests/{Session,Coordinators}/**`.
- `docs/testing/spikes/S5.md` (nuevo).

**Seguridad propia:** `cl run` y las pruebas de S5 arrancan `Clicalo.exe` con datos aislados (`%TEMP%\clicalo-dev`) y
**sin envío de teclas** en el equipo del usuario; la medición con envío real solo corre en la CI.

**Consume:** todo lo que exponen los otros cuatro paquetes; `NonActivatingWindow`, `ShortcutTile`,
`PointerInputSource`, `GestureRecognizer`, `ForegroundOrchestrator` y la bandeja de M1.

**Expone:** `Clicalo.exe` con el panel mínimo, la bandeja y la composición completa de M2.

**Criterios de M2 que cierra:** tocar → `SendInput` en InputProbe con p95 ≤ 50 ms en el equipo táctil;
`reg01.violations = 0` en la suite de no activación con el panel real; bandeja con el Bloc de notas; S5.

**Timings:** grupos `App` y `Dock`.

## Estado de los contratos al empezar

| Contrato | Estado |
|---|---|
| `Domain.Primitives` (`ProfileId`, `ShortcutId`, `CatalogRef`, `LangCode`, `ProcessName`, `ValueList<T>`, `LocalizedText`, `IIdGenerator`) | Implementado |
| `Domain.Errors` (`Failure`, `Result<T>`, `Results`) y `Domain.Privacy` (`SecretText`, `Sensitive<T>`, `SensitiveAttribute`) | Implementado |
| `Domain.Keys` (`KeyStroke`, `KeyChord`, `CanonicalChord`, `ChordModifiers`, `InjectionMode`, `InjectedKey`) | Tipos; `KeyChord.Create` y `CanonicalChord` pendientes |
| `Domain.Library` (perfiles, atajos, acciones, `ShortcutLibrary`) | Tipos y `WaitStep` (I6); operaciones del agregado y `ShortcutCompleteness` pendientes |
| `Domain.Settings` | Tipos y rangos (`SettingRange.Snap`); `SettingsSchema.Defaults`, `All` y `Clamp` pendientes |
| `Domain.Frequents`, `Domain.Duplicates`, `Domain.Document`, `Domain.Commands` | Tipos; `UserDocument.Validate`, `RestoreSlices` y `SliceDiff` pendientes |
| `Domain.KeySafety`, `Domain.Execution` | Tipos; `KeyboardLedger`, `ShiftBurstWindow`, `ActivationPolicy`, `KeyboardLayoutSnapshot.TryResolve` y `EngineReducer` pendientes |
| `Domain.Migration.V1` | Tipos; `V1ComboTokenizer` y `V1Converter` pendientes |
| `Application.Ports` (motor y persistencia) | Completo |
| `Application.Engine`, `Application.Store`, `Application.Persistence`, `Application.Confirmation` | Firmas; cuerpos pendientes |
| `Platform.Core` (`KeyLedger`, `Injection`, `Guardian`) | Constantes del *ledger* v2 implementadas; el resto, firmas |
| `Clicalo.Sentinel` (`GuardianLoop`, `SentinelEntryPoint`) | Firmas; `Program.Main` aún devuelve 0 |
| `Infrastructure.Persistence`, `Infrastructure.Backup`, `Infrastructure.Migration` | `SchemaVersion`, `DocumentFormats`, `DataLocations` y `SafeZipLimits` implementados; el resto, firmas |
| `tests/Clicalo.Infrastructure.Tests`, `tests/Clicalo.Sentinel.Tests`, `tests/Clicalo.Performance` | Proyectos nuevos registrados en `Clicalo.slnx` y `allowed-dependencies.json`, con sus primeras pruebas |

## Integración

Los cinco paquetes se fusionaron en `m2/skeleton` en el orden recomendado (domain → engine → persistence → migration →
app). La integración aplicó los cambios compartidos que pidieron, quitó los dobles y las omisiones que esperaban a
`domain` (`TestRules`, `Chords`, `EngineRules`, `DomainPending` y `SkipExceptions`), compuso `Clicalo.exe` de punta a
punta (motor real tras la valla, persistencia real, semilla o migración v1 en el primer arranque, panel y bandeja) y
añadió `Clicalo.App.Tests`. Lo que cambia respecto al plano está en
[D-21](../../architecture/deviations.md#d-21--integración-de-m2); las propuestas para el usuario, en R-11 a R-14 de
[§6.1 del catálogo](../../requirements/catalog.md#61-propuestas-pendientes-de-ratificar).
