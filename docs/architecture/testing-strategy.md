# Estrategia de pruebas

Cómo se prueba Clícalo, qué proyecto prueba qué, cómo se traza cada requisito hasta su prueba y cómo
funcionan las instantáneas. Es una decisión reversible: se cambia con un PR normal. El marco normativo está
en [§10 del plano](blueprint.md#10-calidad).

## Principios

1. **Las reglas las hacen cumplir las máquinas.** Una regla de producto que no tiene prueba, analizador o
   regla de arquitectura acabará rota.
2. **Todo requisito verificado deja rastro.** Una prueba que verifica un requisito del catálogo lleva su
   identificador (ver [Trazabilidad](#trazabilidad-requisito--prueba)).
3. **Determinismo.** Todo lo que depende del tiempo usa `TimeProvider` (`FakeTimeProvider` en las
   pruebas); ninguna prueba que bloquee un PR depende del reloj real, del azar, de la red ni del escritorio
   (ver [Niveles de pruebas](#niveles-de-pruebas)).
4. **La verdad física la da InputProbe.** Las pruebas de integración comprueban lo que recibe una ventana
   Win32 instrumentada, no lo que Clícalo cree que envió.
5. **Sin reintentos.** Una prueba inestable se arregla o se pone en cuarentena con un *issue* (ver
   [Pruebas inestables](#pruebas-inestables)).

## Niveles de pruebas

Decisión del usuario del 2026-10-05: simplicidad primero. Los requisitos del catálogo no cambian (REG-01 y
REG-03 se siguen verificando); cambia cuándo se ejecuta cada prueba.

| Nivel | Qué ejecuta | Cuándo | Si falla |
|---|---|---|---|
| **PR** | `cl check` (trabajo `verify`): pruebas deterministas, sin `Requires=Desktop`, `Category=Chaos`, `Category=Perf` ni `Category=Quarantine` | Cada PR y cada *push* a `main` | Bloquea la fusión |
| **Nocturno** | [`nightly.yml`](../../.github/workflows/nightly.yml): `cl desk` (escritorio, caos incluido), `cl perf` (presupuestos de `budgets.json`) y `cl quarantine` | A mano (Actions › nightly › *Run workflow*, con una rama o `refs/pull/<n>/head` para probar el escritorio de un PR), o `cl desk` en local; sin ejecución diaria, para no gastar los minutos de Actions | No bloquea |
| **Publicación** | Todo lo anterior en verde, más el equipo táctil (`lab.yml`) y la aceptación en hardware | Antes de cualquier versión, de M5 en adelante | No se publica |

Las pruebas de escritorio, de tiempos reales, de caos y los bucles estadísticos van al nivel nocturno: llevan
`[Trait("Requires", "Desktop")]`, `Category=Chaos` o `Category=Perf`. Una prueba sin escritorio que mida
milisegundos reales no es determinista: se reescribe con `FakeTimeProvider` o se marca para el nocturno.

## Pirámide

De la base (más pruebas, más rápidas) a la cima (menos pruebas, en hardware real):

| Nivel | Qué cubre | Volumen previsto | Dónde se ejecuta |
|---|---|---|---|
| Estáticas | Analizadores `CLC*`, generadores, ArchUnit, reglas de producto, esquemas, paridad i18n, contraste | — | Cada PR |
| Unitarias y de propiedades | Domain, Application, Presentation e Infrastructure | 3000 o más | Cada PR |
| UI en proceso (STA) | *Peers*, medidas, disposición S/M/L × 100–150 %, pseudolocalización, gestos con trazas, **instantáneas de renderizado** por estado | ~350 | Cada PR (x64; ARM64 cuando el repositorio sea público, ver [D-04](deviations.md#d-04--repositorio-privado-al-inicio)) |
| Integración | Platform contra InputProbe; Windowing con puntero sintético | ~220 | Nocturno en alojado interactivo; equipo táctil |
| Rendimiento | Presupuestos de [§10.3](blueprint.md#103-presupuestos-de-rendimiento) | — | Nocturno en alojado (puerta de toque → `SendInput`); puerta completa en el equipo táctil |
| E2E | FlaUI sobre la app **publicada**: reglas UIA, árbol, no activación, recorridos | ~60 | Humo en alojado; completo en el equipo táctil |
| Aceptación | docs/09 completo en hardware táctil real y guion manual de accesibilidad | Por versión | Equipo táctil del mantenedor |

## Proyectos de prueba

Todo proyecto bajo `tests/` (salvo las bibliotecas `Clicalo.TestKit` y `Clicalo.TestKit.Windows`) es un
ejecutable de xUnit v3 sobre Microsoft Testing Platform, con `Xunit` y `Shouldly` como *usings* globales
(`tests/Directory.Build.props`). Las pruebas del orquestador `cl` viven junto a él, en
`build/Build.Tests`, y también están en `Clicalo.slnx`.

| Proyecto | Estado en M0 | Cubre |
|---|---|---|
| `Clicalo.Architecture.Tests` | Existe | Lista blanca de referencias, ArchUnit, matriz de módulos, facetas de `ActionKind`, enrutadores sin eventos huérfanos, R4 (destructivos), R7 (deshacer), escritor único |
| `Clicalo.Data.Tests` | Existe | Esquemas, integridad referencial (`labelKey`, iconos, `KeyId`), contenido inicial sin repetidos, `keys.json` ↔ `keys.win32.json`, coherencia de `timings.json` |
| `Clicalo.Domain.Tests` | Existe | Invariantes de `Library` y `KeyboardLedger`, `EngineReducer` (INV-1, INV-3 a INV-10 e INV-12), `TouchFilter`, `GestureRecognizer`, `ActivationPolicy`, `DimPolicy`, resolución de perfil, Frecuentes, repetidos |
| `Clicalo.Application.Tests` | Existe | `DocumentStore`, `EngineHost` con `PhysicalStateInjector`, `ForegroundOrchestrator`, `TryNowUseCase`, coordinadores, programador de guardado |
| `Clicalo.Generators.Tests` | Existe | Generadores y analizadores de Roslyn: salida determinista y diagnósticos con ubicación exacta en el JSON |
| `Clicalo.Platform.IntegrationTests` | Existe | Inyección en los dos modos con varias distribuciones, *hook* LL bajo GC, `PointerPositionTracker`, sesión, portapapeles, lanzador, ACL de la tarea elevada |
| `Clicalo.DevCli.Tests` | Existe | Órdenes de `tools/Clicalo.DevCli`: códigos de salida, receta e importación de i18n, detector de claves usadas, `allow-unused.txt` y `adr-check` |
| `Build.Tests` (en `build/`) | Existe | Orquestador `cl`: línea final, informes, lectura del registro de MSBuild y del TRX, versiones fijadas (NFR-014) |
| `Clicalo.TestKit` | Existe (biblioteca) | `RepoPaths`, instantáneas de texto (`TextSnapshot`), reloj simulado (`TestTime`) y comprobación de `[Trait("Req")]` contra el catálogo |
| `Clicalo.TestKit.Windows` | Existe (biblioteca) | Instantáneas de renderizado WPF (`RenderSnapshot`), sesiones de `tools/InputProbe` y un inyector de pruebas con barrera de seguridad |
| `Clicalo.Presentation.Tests` | Previsto | ViewModels contra proyecciones, equivalentes sin gesto, `TwoStepConfirm`, idioma en caliente |
| `Clicalo.Infrastructure.Tests` | Previsto | DTO ↔ dominio, migraciones con *fixtures*, importación y exportación del formato propio, DPAPI, IA con servidor falso (4 campos exactos), `SignedManifestSource` |
| `Clicalo.UI.Wpf.Tests` | Previsto | *Peers*, 44 px, disposición, pseudolocalización, contraste resuelto, instantáneas de renderizado |
| `Clicalo.Windowing.IntegrationTests` | Existe (M1) | No activación de las superficies, `ActivationGuard` (prueba negativa), concesiones por origen, bandeja, `Upstream/` |
| `Clicalo.Sentinel.Tests`, `Clicalo.Launcher.Tests` | Previstos | Sentinel con un estado de teclas y un `SendInput` falsos (soltado, reintentos y relanzamiento, ADR-0023); verificación tras la copia y `minSafeVersion` |
| `Clicalo.E2E`, `Clicalo.Performance` | Previstos | Recorridos sobre la app publicada; presupuestos (`budgets.json`) |

`Core.slnf` reúne Domain, Application y Presentation, los generadores que usan, sus pruebas y
`Clicalo.TestKit` para iterar rápido (`cl fast`).

## Herramientas

| Herramienta | Uso |
|---|---|
| xUnit v3 (línea 4.x) sobre Microsoft Testing Platform | Marco de pruebas (ver [deviations.md](deviations.md) sobre la versión) |
| Shouldly | Aserciones. FluentAssertions 8 está descartada por su licencia comercial |
| CsCheck | Pruebas de propiedades y generadores de entradas hostiles |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider` para tiempos, antirrebote y plazos |
| TngTech.ArchUnitNET (xUnit v3) | Reglas de arquitectura |
| FlaUI.UIA3 | E2E y reglas UIA propias |
| Axe.Windows | Apoyo, nunca puerta (su mantenimiento está casi parado) |
| Comparador de instantáneas propio de TestKit | Instantáneas de texto y PNG (sustituye a Verify, ver [más abajo](#instantáneas)) |

## Trazabilidad requisito → prueba

Una prueba que verifica un requisito del [catálogo](../requirements/catalog.md) lleva el rasgo `Req` con su
identificador. Si verifica varios, lleva un rasgo por cada uno:

```csharp
[Fact]
[Trait("Req", "EJE-003")]
[Trait("Req", "ATJ-004")]
public void Scan_code_mode_releases_with_the_same_scan_code_it_pressed() { /* … */ }
```

- El identificador debe existir en el catálogo tal cual (`EJE-003`, no `EJE-3`).
- Se puede ejecutar solo lo que verifica un requisito:
  `dotnet test --project tests/Clicalo.Domain.Tests/Clicalo.Domain.Tests.csproj --filter-trait "Req=EJE-003"`.
  Si ninguna prueba coincide, Microsoft Testing Platform termina con el código 8 («no se ejecutó ninguna
  prueba»).
- `cl trace` escribe `artifacts/cl/trace.md`: cada requisito del catálogo con las pruebas que lo nombran y,
  arriba del todo, los MUST sin prueba. Lee los rasgos del código de las pruebas (no hace falta compilar ni
  ejecutar), así que da lo mismo en cualquier equipo; falla solo si un rasgo nombra un identificador que no
  está en el catálogo. El archivo no se versiona ([D-29](deviations.md#d-29--sin-cl-states-ni-cl-accept)).
- **Desde el hito RC, ningún requisito MUST puede quedar sin prueba automática** o sin una entrada en el
  [guion de aceptación manual](../guides/aceptacion-manual.md): `cl trace` marca como «solo en el guion
  manual» el MUST sin prueba cuyo identificador aparece en el guion, y como «SIN PRUEBA» el resto.
- Al tocar el comportamiento de un requisito, su prueba se actualiza o se crea en el mismo PR.

El plano escribe `[Req("…")]` como notación abreviada; la forma canónica en el código es
`[Trait("Req", "…")]` (ver [deviations.md](deviations.md)).

## Instantáneas

Las instantáneas comparan una salida con una referencia aprobada y versionada. Clícalo usa un
**comparador propio en `Clicalo.TestKit`** en lugar de Verify (el motivo está en
[deviations.md](deviations.md)). Reglas:

- **Texto:** UTF-8 con finales de línea LF y bytes estables. Las referencias se guardan como
  `*.verified.txt` (`.gitattributes` fija `eol=lf`).
- **Imagen:** PNG con tolerancia, porque el renderizado puede variar en detalles invisibles. El objetivo es
  ΔE ≤ 2 por píxel (OKLab) y como máximo un 0,5 % de píxeles distintos. `RenderSnapshot` compara hoy con
  una tolerancia **por canal** (2 de 255 en BGRA sin premultiplicar) y el mismo 0,5 %; el modo ΔE queda
  pendiente para M3, antes de las primeras referencias de la UI. Las referencias se guardan como
  `*.verified.png` (binarias) y, si fallan, se escriben `*.received.png` y `*.received.diff.png`.
- **Una diferencia nunca se acepta sola.** El comparador escribe la salida nueva como `*.received.*`
  (ignorada por Git) y la prueba falla. Aprobar es sustituir la referencia de forma explícita
  (`CLICALO_ACCEPT_SNAPSHOTS=1` en local; en la CI se rechaza), y el cambio se revisa en el *diff* del PR.
- **Fidelidad visual:** las referencias iniciales de renderizado se aprueban comparándolas lado a lado con
  el Prototipo v4.

Dónde se usan:

- el texto visible de los 669 textos con argumentos de muestra, idéntico al del paquete (condición
  vinculante de [ADR-0011](../adr/0011-formato-i18n.md));
- la salida de los generadores y los diagnósticos de los analizadores;
- las migraciones del esquema propio con *fixtures* por versión;
- el árbol UIA por ventana y estado (un cambio de accesibilidad aparece en el *diff*);
- el renderizado (`RenderTargetBitmap`) por forma, tamaño S/M/L, tema, escala y estado. `cl states` no se
  construye ([D-29](deviations.md#d-29--sin-cl-states-ni-cl-accept)): las pruebas sin pantalla comprueban lo que
  cada vista proyecta y, con `CLICALO_CC_PREVIEW=1`, escriben las vistas previas en `artifacts/cc-preview`.

## Pruebas de propiedades y de modelo

- El motor se prueba con secuencias de hasta 200 eventos generadas con CsCheck (toques, varios contactos,
  temporizadores, cambios de app, bloqueos, suspensiones, fallos de inyección, Modo prueba, teclas fijas y
  los dos modos de inyección). En cada paso se comprueban INV-1, INV-3 a INV-10 e INV-12
  ([§7.5 del plano](blueprint.md#75-invariantes-de-seguridad-de-teclas)).
- La muerte real del proceso la cubre la prueba de caos nocturna de S9 (`Category=Chaos`, ADR-0023).
- Los contraejemplos reducidos se guardan como pruebas de regresión.
- Generadores de entradas hostiles (documentos y perfiles compartidos enormes, anidados o con campos que
  mienten) sobre el lector del documento y el de importación. Stryker.NET y SharpFuzz llegan después de la 2.0
  (M7).

## Accesibilidad

- **Puerta:** reglas UIA propias (UIA001–UIA010, [§10.2](blueprint.md#102-pruebas-de-accesibilidad-y-aceptación-en-hardware))
  sobre FlaUI: nombre localizado (con el número de voz si está activo), tipo, patrones, estados, 44 px,
  `LiveSetting`, equivalentes sin gesto, ningún glifo como nombre, invocar sin cambiar el primer plano y un
  botón «Dictar» junto a cada campo de texto libre.
- **Apoyo:** Axe.Windows fijado.
- **Manual:** guion con Narrador y Acceso por voz en Windows 11, y con Narrador y Reconocimiento de voz de
  Windows en Windows 10, con y sin alto contraste, antes de cada versión estable.

## Cobertura

| Capa | Líneas | Ramas | Tipo |
|---|---|---|---|
| Domain | 90 % | 85 % | Obligatoria |
| Application | 85 % | 80 % | Obligatoria |
| Persistencia, migraciones y `Application.Localization` | 85 % | 75 % | Obligatoria |
| Presentation | 70 % | — | Informativa |
| UI.Wpf y Platform | — | — | Se cubren con integración, instantáneas y E2E |

## Pruebas inestables

- Ningún reintento, en ningún nivel.
- Si una prueba falla y no es un defecto real del producto, se pone en cuarentena y se registra; no se
  persigue en el mismo trabajo. Si es un defecto del producto, se corrige.
- **Cómo se marca:** se abre un *issue* con la etiqueta `flaky` (qué falla, con qué frecuencia y el enlace a la
  ejecución) y la prueba lleva, en el mismo archivo:

  ```csharp
  [Trait("Category", "Quarantine")]
  [Trait("Issue", "123")] // número del issue de GitHub
  ```

  `cl check` la deja fuera y `cl quarantine` la ejecuta cada noche. `Build.Tests` (`QuarantineTests`) falla si
  un archivo con la cuarentena no nombra su *issue*. Una medición (`Category=Perf`) no se pone en cuarentena: se
  discute su presupuesto en `data/catalogs/budgets.json`.
- **Cómo se sale:** se corrige la causa, se quitan los dos rasgos en el mismo PR que la corrección y se cierra
  el *issue*. La cuarentena se revisa a los 14 días: o se corrige, o se borra la prueba con una propuesta que
  explique cómo queda cubierto su requisito (los requisitos no se rebajan).
- Los informes de `cl` y los resultados TRX de los trabajos fallidos se guardan como artefactos durante 14 días.

## Cómo ejecutarlas

| Qué | Con `cl` | Con `dotnet` |
|---|---|---|
| Las pruebas deterministas (nivel PR) | `cl test` | `dotnet test --solution Clicalo.slnx --filter-not-trait Requires=Desktop --filter-not-trait Category=Chaos --filter-not-trait Category=Perf --filter-not-trait Category=Quarantine` |
| Solo el núcleo (menos de 45 s) | `cl fast` | `dotnet test --solution Core.slnf` |
| Un proyecto | — | `dotnet test --project tests/<Proyecto>/<Proyecto>.csproj` |
| Integración de escritorio | `cl desk` | `CLICALO_DESKTOP_TESTS=1` y `dotnet test --solution Clicalo.slnx --filter-trait Requires=Desktop` |
| Vistas previas del Centro de control y la bienvenida | — | `CLICALO_CC_PREVIEW=1` y `dotnet test --project tests/Clicalo.Windowing.IntegrationTests/Clicalo.Windowing.IntegrationTests.csproj` (salen en `artifacts/cc-preview`) |
| Rendimiento | `cl perf` | — |
| Cuarentena | `cl quarantine` | `CLICALO_DESKTOP_TESTS=1` y `dotnet test --solution Clicalo.slnx --filter-trait Category=Quarantine` |
| Trazabilidad requisito → pruebas | `cl trace` | `dotnet run --project tools/Clicalo.DevCli -- trace` |
| Aceptación con la persona usuaria y en una máquina limpia | — | A mano, con el [guion de aceptación manual](../guides/aceptacion-manual.md) |

Los verbos de `cl` están descritos en [tooling.md](tooling.md#verbos-de-cl).
