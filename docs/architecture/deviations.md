# Desviaciones del plano

Registro de los puntos en los que el repositorio se aparta del [plano](blueprint.md), con su motivo. Ninguna
de estas desviaciones cambia una decisión con ADR ni rebaja un requisito del catálogo; si alguna llegara a
hacerlo, necesitaría un ADR nuevo o una propuesta al usuario.

Cada entrada dice qué pide el plano, qué hace el repositorio, por qué, qué cuesta y cuándo se revisa.

## Resumen

| # | Tema | Plano | Repositorio | Desde |
|---|---|---|---|---|
| D-01 | Instantáneas | Verify.XunitV3 | Comparador propio en `Clicalo.TestKit` | M0 |
| D-02 | Versión de xUnit | xUnit v3, línea 3.x | `xunit.v3` 4.x | M0 |
| D-03 | Roslyn de generadores y analizadores | Sin versión fijada | `Microsoft.CodeAnalysis` 4.14 | M0 |
| D-04 | Visibilidad del repositorio | Público (ARM64, CodeQL y Scorecard en cada PR) | **Cerrada el 2026-10-05**: el repositorio es público y esos trabajos se ejecutan en cada PR | M0 |
| D-05 | Ubicación del repositorio | Sin especificar | `C:\dev\clicalo`, fuera de OneDrive | M0 |
| D-06 | Notación de trazabilidad | `[Req("ID")]` | `[Trait("Req", "ID")]` | M0 |
| D-07 | Formato de `CHANGELOG.md` | El que genere release-please | Keep a Changelog; release-please se configurará en M5 para respetarlo | M0 |
| D-08 | Nota de qué es vinculante del paquete | «Un README» en `docs/design/handoff/` | `LEEME-VINCULANTE.md`, sin tocar el `README.md` original | M0 |
| D-09 | Matriz de módulos de Domain | Tabla de §4.3 | Módulos `Document` y `Timing`, cuatro aristas nuevas y matriz transitiva | M0 |
| D-10 | Puertos y revelado de secretos | Puertos de primer plano en `Application.Foreground`; `WithRevealed` solo en la ejecución y el editor | Puertos en `Application.Ports`; `WithRevealed` también en `Application.Engine` e `Infrastructure.Persistence` | M0 |
| D-11 | Tabla de APIs prohibidas | §4.4 | Ampliada: más fuentes de tiempo y aleatoriedad, `UIElement.Focus`, carga dinámica de ensamblados; `ShellExecuteEx` permitido en `Platform.Windows/Elevation` | M0 |
| D-12 | Historial de M0 | `main` lineal, solo *squash*, ámbitos de una lista cerrada, `Signed-off-by` en cada commit | El historial de M0, anterior a la protección de `main`, tiene fusiones `--no-ff`, tres ámbitos fuera de la lista y commits sin `Signed-off-by` | M0 |
| D-13 | Protección de `main` | Rama protegida en GitHub (PR obligatorio, checks, historial lineal) | **Cerrada el 2026-10-05**: protección real activa en GitHub; el *hook* `pre-push` se conserva como aviso local | M0 |
| D-14 | Contratos de M1 para el primer plano | `SurfaceId` junto a las ventanas; `ActivationGuard` llama al orquestador | `SurfaceId` y `WindowToken` en `Application.Ports`; tres puertos más (`IActivationArbiter`, `ISurfaceLookup`, `IInternalKeyEffects`) | M1 |
| D-15 | No activación medida en S1 | `SWP_NOACTIVATE` en `WM_WINDOWPOSCHANGING`; `WM_DPICHANGED` sin pasar a WPF; la violación se cierra con la desactivación | Además `ActivationVeto` (`WH_CBT` de hilo, ámbito mínimo); `WM_DPICHANGED` reenviado a WPF dentro del veto; una violación por activación, que termina con la ráfaga de mensajes que la abrió | M1 |
| D-16 | Capa de punteros | Sin fijar cómo llega el mouse ni quién ejecuta los plazos | `EnableMouseInPointer`, `GestureHost`, muestras válidas solo durante `OnFrame`, umbral de palma y regla de objetivo | M1 |
| D-17 | Primer plano | §3.6 y §7.9; el orquestador en el hilo SysEvents (§3.2) | Monitor sin `WINEVENT_SKIPOWNPROCESS`, verificación durante `RestoreRetryDelay` (miradas y aviso del monitor) sin reintentar sobre la elección de la persona, violación durante una concesión, orquestador en el grupo de hilos, espera a que se suelte el atajo interno | M1 |
| D-18 | UI Automation | Cortés = `ImportantMostRecent` | Cortés = `MostRecent`; `Invoke` asíncrono; relleno `BSTR` de `RaiseNotificationEvent` | M1 |
| D-19 | Secuencia de los spikes | M1 cierra con todos los criterios de §15 superados; S5, S7, S9, S11, S6, S14, S8, S10, S12 y S15 dentro de M1 | M1 cerrado por decisión del usuario con la evidencia real; filas manuales de S1, S3 y S4 en la aceptación en hardware de M3; S5, S7, S9 y S11 en M2; S2 residual, S6 y S15 antes de M3; S12 en M3; S8, S10 y S14 antes de M5. Ningún criterio cambia | M1 |
| D-20 | Contratos de M2 | Nombres y módulos de §6 y §7 (`Library`, `Settings`, `Error`, `SecretText` en Library, `WebAction`…) | `ShortcutLibrary`, `UserSettings`, `Failure` y `Results`, `SecretText` en Privacy, módulo `Commands`, `UrlAction`, puertos del motor y de la persistencia en `Application.Ports`, umbrales de Sentinel por línea de órdenes (ADR-0018) | M2 |
| D-21 | Integración de M2 | Nombres y reparto de §6 y §7; el guardián escribe su diario; `EmergencyReleaser` en el hilo SysEvents; el panel mínimo sin interoperabilidad propia | Miembros y tipos públicos nuevos de los cinco paquetes, comandos `DiscardDraft` y `SetSetting(ruta, valor)`, entrega ordenada de `Changed`, tokens de un solo uso, Sentinel sin escritura, `EmergencyReleaser` con su propio temporizador, instancia única en `Clicalo.App`, kit inicial del primer arranque (la migración v1, retirada por ADR-0020), `Clicalo.App.Tests` | M2 |
| D-22 | Correcciones del motor tras verificar M2 | Reintento de lo que rechaza el escritorio seguro solo al desbloquear o reanudar; escalada de la emergencia a `TerminateProcess` sin condiciones; INV-10 como propiedad de `LayoutPlanner` | Reenvío también con «Soltar todo», los eventos terminales y el regreso del escritorio de entrada (UAC, Ctrl+Alt+Supr); latido y marcas del motor bajo la valla; acordes internos desde el motor; sin guardián la emergencia nunca termina el proceso; INV-10 aplazada a M3; Sentinel reintenta lo rechazado hasta el desbloqueo y solo entonces relanza (decisión D3 del usuario, protocolo 2; el plano lo recoge desde que se aceptó ADR-0018) | M2 |
| D-23 | Criterios de salida de M2 tras la verificación | Presupuestos en `tests/Clicalo.Performance/budgets.json`; rendimiento en el equipo táctil y no obligatorio en el PR; `lab.yml` semanal y antes de cada beta; prueba de bandeja con el icono real | `data/catalogs/budgets.json` con esquema; puerta de toque → `SendInput` también en los alojados y `perf (x64)` obligatorio; `lab.yml` solo a mano; bandeja con un toque en el panel y `OpenMenuAsync`; muerte en cada paso y congelar y reanudar reducidos sin CsCheck | M2 |
| D-24 | Latencia del panel en la CI | Toque → `SendInput` p95 ≤ 50 ms sobre 20 toques (§7.1, §10.3), medido con puntero sintético | La parte del panel (levantamiento → buzón del motor) se juzga con el mismo presupuesto sobre 21 toques medidos, con un dispositivo sintético por tipo y un toque de calentamiento por dispositivo que se comprueba e informa pero no entra en el p95 | M2 |

## D-01 · Verify sustituido por un comparador propio en TestKit

- **Plano.** [§2.1](blueprint.md#21-stack-elegido) elige Verify.XunitV3 para instantáneas de texto y PNG;
  [§6.6](blueprint.md#66-migraciones-incluida-v1--v2), [§10.1](blueprint.md#101-pirámide-y-proyectos-de-prueba)
  y [§10.2](blueprint.md#102-pruebas-de-accesibilidad-y-aceptación-en-hardware) lo usan para migraciones,
  importación v1, árbol UIA y renderizado.
- **Repositorio.** Verify no está en `Directory.Packages.props`. Las instantáneas las compara un componente
  propio de `Clicalo.TestKit` (`TextSnapshot`) y de `Clicalo.TestKit.Windows` (`RenderSnapshot`): texto con
  bytes estables y PNG con tolerancia. El plano fija ΔE ≤ 2 por píxel y 0,5 % de píxeles distintos como
  máximo; en M0 la tolerancia es por canal (2 de 255) con el mismo 0,5 %, y el modo ΔE llega antes de las
  primeras referencias de la UI (M3). Se conservan las convenciones
  `*.verified.*` y `*.received.*` que ya recogen `.gitattributes` y `.gitignore`. El detalle está en la
  [estrategia de pruebas](testing-strategy.md#instantáneas).
- **Motivo.** El README de Verify establece que toda versión publicada después del 1 de septiembre de 2026
  está sujeta a una cuota de mantenimiento para las organizaciones que generan ingresos y para las
  administraciones públicas, que se paga con un patrocinio mensual renovable en GitHub. Es una condición de
  licencia sobre una dependencia de la cadena de suministro, que alcanzaría a organizaciones de
  accesibilidad que quieran adoptar o contribuir al proyecto. Es el mismo criterio por el que
  [§2.2](blueprint.md#22-descartado-y-por-qué) descarta FluentAssertions 8 (licencia comercial). Quedarse
  en una versión anterior a esa fecha dejaría la dependencia congelada y sin parches.
- **Coste.** Código propio que mantener y probar (comparación de texto, decodificación de PNG, métrica de
  color y escritura de `*.received.*`). Es acotado y no depende de nadie.
- **Revisión.** Si Verify vuelve a una licencia sin condiciones para organizaciones, se puede reconsiderar
  con un PR normal (es una decisión reversible).
- **Fuente.** [README de Verify](https://github.com/VerifyTests/Verify).

## D-02 · xUnit v3 en la línea 4.x en lugar de 3.x

- **Plano.** [§2.1](blueprint.md#21-stack-elegido) cita xUnit v3 «3.x».
- **Repositorio.** `xunit.v3` 4.0.1 en `Directory.Packages.props`.
- **Motivo.** La versión 4.0 es la línea estable vigente. Hace de Microsoft Testing Platform v2 la opción
  por defecto y deja de dar soporte oficial a la v1; el repositorio usa Microsoft Testing Platform como
  ejecutor de `dotnet test` (`global.json`) con el SDK de .NET 10. Empezar un repositorio nuevo en la línea
  anterior obligaría a una migración con cambios incompatibles a corto plazo (ordenadores de pruebas y APIs
  de extensibilidad). La 4.0 añade además soporte de Native AOT.
- **Coste.** Ninguno conocido: la pila de pruebas (Shouldly, CsCheck, ArchUnitNET para xUnit v3) compila y
  pasa.
- **Revisión.** Con cada versión mayor de xUnit, como cualquier dependencia (Renovate).
- **Fuente.** [Notas de la versión 4.0.0 de xUnit v3](https://xunit.net/releases/v3/4.0.0).

## D-03 · Roslyn 4.14 para generadores y analizadores

- **Plano.** No fija la versión de `Microsoft.CodeAnalysis` para `generators/`.
- **Repositorio.** `Microsoft.CodeAnalysis.CSharp` y `Microsoft.CodeAnalysis.Analyzers` 4.14.0.
- **Motivo.** Un generador o un analizador compilado contra una versión de Roslyn solo se carga en
  compiladores de esa versión o posteriores. La 4.14 corresponde como mínimo a Visual Studio 2022 17.14, y
  la 5.0 a Visual Studio 2026 18.0. Con 4.14, los componentes se cargan en cualquier SDK de .NET 10 y en
  cualquier IDE o extensión de VS Code con un compilador igual o posterior, sin obligar a colaboradores ni
  agentes a una versión concreta del IDE.
- **Coste.** Los generadores y analizadores no pueden usar APIs de Roslyn posteriores a 4.14. Los
  generadores leen datos JSON y no las necesitan; si un analizador `CLC*` necesitara analizar construcciones
  de C# 14 que solo expone una API más nueva, se revisará esta decisión.
- **Revisión.** Al migrar a .NET 12 LTS, o si un analizador necesita una API más nueva.
- **Fuente.** [Correspondencia entre versiones de Roslyn y de Visual Studio](https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support).

## D-04 · Repositorio privado al inicio

- **Plano.** [§10.5](blueprint.md#105-cicd) incluye en cada PR un trabajo de UI en `windows-11-arm`, CodeQL
  y, cada semana, OpenSSF Scorecard; [§11](blueprint.md#11-distribución-versionado-y-publicación) y
  [ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md) cuentan con SignPath Foundation para
  código abierto.
- **Repositorio.** Empieza privado. Mientras lo sea, el trabajo ARM64, CodeQL y Scorecard están
  condicionados a que el repositorio pase a público y no son comprobaciones obligatorias.
- **Motivo.**
  - El escaneo de código (CodeQL) en repositorios privados exige una licencia de GitHub Code Security.
  - Scorecard Action solo admite repositorios privados con GitHub Advanced Security.
  - Los *runners* alojados son gratuitos en repositorios públicos; en privados consumen minutos de pago, y
    los de Windows y ARM tienen una tarifa por minuto más alta. El trabajo ARM64 gastaría la cuota sin
    aportar una puerta: ARM64 solo se publica en beta hasta la aceptación en un equipo físico (P5).
- **Coste.** Mientras sea privado no hay análisis de CodeQL ni puntuación de Scorecard. Se mantienen
  NuGetAudit, los *lock files* en modo bloqueado, los analizadores y las pruebas en x64.
- **Revisión.** Al hacer público el repositorio, esos trabajos pasan a ser obligatorios en `pr.yml` y se
  solicita la admisión en SignPath Foundation, en todo caso antes de M5 (primera beta firmada).
- **Fuentes.**
  [Acerca del escaneo de código](https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning),
  [Scorecard Action](https://github.com/ossf/scorecard-action),
  [facturación de GitHub Actions](https://docs.github.com/en/billing/concepts/product-billing/github-actions),
  [*runners* alojados por GitHub](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

- **Cierre (2026-10-05).** El usuario decidió hacer público el repositorio, ya que Clícalo será una herramienta
  que cualquiera podrá usar. Además, el límite de gasto de GitHub Actions había detenido la CI del repositorio
  privado. Desde ese día, los trabajos ARM64, CodeQL y Scorecard se ejecutan en cada PR. Pasarán a ser
  comprobaciones obligatorias de la protección de `main` cuando tengan su primera ejecución en verde. La
  solicitud a SignPath Foundation sigue pendiente para antes de M5.

## D-05 · Repositorio en `C:\dev\clicalo`, fuera de OneDrive

- **Plano.** No especifica dónde se clona.
- **Repositorio.** El clon principal vive en `C:\dev\clicalo` y los *worktrees* en `C:\dev\clicalo-wt\`,
  fuera de cualquier carpeta sincronizada.
- **Motivo.** Macro Quick Access vivía dentro de OneDrive y sufrió fallos de guardado por bloqueos de
  archivo (WinError 5 y 32) y copias divergentes (lección L-DAT-3 del
  [catálogo](../requirements/catalog.md#8-lecciones-de-la-app-antigua-y-del-prototipo)). Una compilación de
  .NET escribe miles de archivos en `artifacts/` y `obj/`, y el cliente de sincronización puede bloquearlos
  a mitad de compilación o convertirlos en marcadores de posición a petición; también sincronizaría a la
  nube la carpeta `.git` y los artefactos.
- **Coste.** Ninguno; la copia de seguridad del código es el propio repositorio remoto.
- **Revisión.** No se prevé.
- **Guía.** [Preparar el entorno](../guides/dev-setup.md).

## D-06 · `[Trait("Req", …)]` como notación de trazabilidad

- **Plano.** [§6.4](blueprint.md#64-estado-cuatro-dueños-deshacer-y-autoguardado),
  [§7.7](blueprint.md#77-detalles-del-envío-nfr-004), [§7.11](blueprint.md#711-posición-del-puntero-para-acciones-de-mouse-eje-009)
  y [§10.4](blueprint.md#104-análisis-estático-cobertura-y-mutación) escriben `[Req("SEG-007")]`.
- **Repositorio.** La forma canónica es el rasgo estándar de xUnit `[Trait("Req", "SEG-007")]`.
- **Motivo.** Funciona sin código propio: filtra con `--filter-trait "Req=SEG-007"` en Microsoft Testing
  Platform, agrupa en los exploradores de pruebas y lo puede leer `cl trace` desde los resultados. Si TestKit
  añade un atributo abreviado, tendrá que emitir el mismo rasgo `Req`.
- **Coste.** Una palabra más en cada prueba.
- **Revisión.** Si se añade el atributo abreviado, esta entrada se actualiza.

## D-07 · `CHANGELOG.md` en formato Keep a Changelog

- **Plano.** [§11](blueprint.md#11-distribución-versionado-y-publicación): `CHANGELOG.md` técnico y en
  inglés, generado por release-please.
- **Repositorio.** `CHANGELOG.md` sigue [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) en
  inglés, con una sección `Unreleased` que recoge M0.
- **Motivo.** Hasta M5 no hay publicaciones y el historial de M0 debe tener un sitio legible. Keep a
  Changelog agrupa por tipo de cambio (*Added*, *Changed*, *Fixed*…) con un formato estable.
- **Coste.** Al introducir release-please (M5) hay que configurar sus secciones (`changelog-sections`) para
  que los tipos de Conventional Commits caigan en los encabezados de Keep a Changelog; la sección
  `Unreleased` se convierte entonces en la de la primera beta.
- **Revisión.** En M5, al configurar release-please.

## D-08 · `LEEME-VINCULANTE.md` junto al paquete de diseño

- **Plano.** [§5](blueprint.md#5-estructura-del-repositorio-y-de-la-solución): `docs/design/handoff/` es el
  paquete original, de solo lectura, «con un README de qué es vinculante».
- **Repositorio.** El paquete ya trae su propio `README.md`, que es parte del original. La nota de qué es
  vinculante es un archivo aparte,
  [LEEME-VINCULANTE.md](../design/handoff/LEEME-VINCULANTE.md), y el resto del paquete no se toca.
- **Motivo.** Sobrescribir el `README.md` original rompería la regla de solo lectura y perdería el índice y
  las reglas del autor.
- **Coste.** Ninguno.

## D-09 · Matriz de módulos de Domain

- **Plano.** [§4.3](blueprint.md#43-módulos-por-capacidad) fija los módulos de `Clicalo.Domain` y de qué
  depende cada uno.
- **Repositorio.** `architecture/domain-modules.json` añade el módulo `Document` (`UserDocument` y sus
  comandos) y el módulo base `Timing` (lo que genera `timings.json`), y las aristas `Errors → Messages`,
  `Library → Errors`, `Settings → Messages` y `Touch`, `Dimming` y `Execution → Timing`. La matriz es
  transitiva: un módulo puede usar lo que alcanza por `dependsOn`. Cada fila que difiere de §4.3 lleva un
  campo `deviation` con el motivo, y `ModuleMatrixTests` falla si una diferencia no lo lleva o si sobra.
- **Motivo.** Sin esos módulos y aristas, los propios tipos de §6.1–§6.3 (un `Error` con `MessageKey`,
  `Result<Library>`, `SettingDescriptor` con claves de texto) y los umbrales generados de NFR-020 violarían
  la tabla.
- **Coste.** Ninguno en tiempo de ejecución; la matriz queda algo más permisiva que la tabla literal.
- **Revisión.** Pendiente de que el usuario la ratifique o de un ADR que actualice §4.3.

## D-10 · Puertos en `Application.Ports` y revelado de secretos

- **Plano.** El bloque de código de [§3.6](blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano) muestra `IForegroundControl`
  e `ISurfaceActivationStyle` en `Application.Foreground`; §4.4 (punto 2) limita
  `SecretText.WithRevealed` a la ejecución y al editor.
- **Repositorio.** Los puertos viven en `Application.Ports`, como exige el propio §4.4 («puertos en
  `Application.Ports`»). `WithRevealed` se permite también en `Application.Engine` (el motor envía el
  texto) y en `Infrastructure.Persistence` (el *mapper* lo cifra, §6.7).
- **Motivo.** Son los dos sitios donde §6.7 necesita el texto en claro; sin ellos, la regla obligaría a
  duplicar el secreto en otro tipo.
- **Coste.** Dos zonas más con acceso al secreto, ambas con pruebas de confinamiento.
- **Revisión.** Pendiente de ratificar junto con D-09.

## D-11 · Tabla de APIs prohibidas ampliada

- **Plano.** La tabla de [§4.4](blueprint.md#44-cómo-se-hacen-cumplir-las-reglas).
- **Repositorio.** `architecture/BannedSymbols.*.txt` va más allá de la tabla: prohíbe además
  `DateTime.Today`, `Environment.TickCount`, `Stopwatch`, `Task.WaitAsync(TimeSpan)`,
  `CancellationTokenSource(TimeSpan)`, `PeriodicTimer(TimeSpan)`, los `Timer` de `System.Threading` y
  `System.Timers`, `Guid.CreateVersion7` y `new Random()` sin semilla (regla «`TimeProvider` en todo» de
  §13); aplica `Window.Focus` como `UIElement.Focus` (enfocar un elemento activa su ventana); y prohíbe la
  carga dinámica de ensamblados (`Assembly.Load*`, `Assembly.UnsafeLoadFrom`, `AssemblyLoadContext.LoadFrom*`)
  para hacer cumplir ADR-0017. ADR-0017 no se edita (un ADR aceptado es inmutable): su «Confirmación» sigue
  citando la revisión de código, y el mecanismo automático está descrito en
  [enforcement.md](enforcement.md#listas-y-cableado). A cambio, registra `ShellExecuteEx` como excepción en
  `Platform.Windows/Elevation`, que §3.3 regla 2 necesita para relanzar elevado.
- **Motivo.** La tabla literal dejaba abiertas otras fuentes de tiempo no simulables y la carga de código.
- **Coste.** Algún adaptador más tendrá que registrarse en `banned-api-exceptions.json`.
- **Revisión.** Pendiente de ratificar junto con D-09.

## D-12 · Historial de M0 anterior a la protección de `main`

- **Plano.** [§13](blueprint.md#13-convenciones-de-ingeniería) y
  [CONTRIBUTING.md](../../CONTRIBUTING.md#flujo-de-trabajo): `main` protegida y con historial lineal, fusión
  solo por *squash*, títulos en Conventional Commits con ámbitos de una lista cerrada y `Signed-off-by` (DCO,
  [ADR-0015](../adr/0015-licencia-mit-y-dco.md)) en cada commit.
- **Repositorio.** M0 se construyó en local, antes de que existieran el remoto, la protección de rama y los
  trabajos `title` y `dco`. Su historial tiene:
  - ocho fusiones `--no-ff`, una por paquete integrado (`2a933b1`, `f02a577`, `12961db`, `0639ec3`,
    `531c5f3`, `dd29f6b`, `067183f` y `1f590c5`);
  - tres commits con ámbitos que no están en la lista cerrada: `a131a97` y `cdd11de` (`arch`) y `287f959`
    (`generators`);
  - commits sin la línea `Signed-off-by`, porque el *hook* de `cl setup` no estaba instalado.
- **Motivo.** Reescribir el historial (linealizarlo, cambiar mensajes o añadir `Signed-off-by` en nombre del
  autor) es destructivo y solo puede hacerlo el mantenedor. Añadir `arch` y `generators` a la lista cerrada
  para cubrir tres commits antiguos cambiaría la convención para todos los PR futuros.
- **Coste.** El historial de M0 no sigue las convenciones que sí siguen los PR. Si se activa «Require linear
  history» en la protección de `main` antes del primer *push*, GitHub rechazará subir las fusiones.
- **Cómo se aplica.** El primer *push* de `main` se hace **antes** de activar la protección con «Require linear
  history». Desde entonces todo entra por PR con *squash*: `pr-title.yml` comprueba el título y el trabajo
  `dco` de `pr.yml` comprueba los commits del PR (solo `base..head`, nunca el historial ya publicado).
- **Revisión.** Única: se cierra con el primer *push*. Si el mantenedor prefiere un historial limpio, puede
  linealizarlo y corregir los mensajes antes de ese *push*; entonces esta entrada se elimina.

## D-13 · Protección de `main` sin reglas de GitHub

- **Plano.** [§13](blueprint.md#13-convenciones-de-ingeniería): `main` protegida, siempre en verde e historial
  lineal; todo entra por PR con *squash* y con los checks obligatorios.
- **Repositorio.** GitHub no ofrece protección de ramas ni *rulesets* para repositorios privados en el plan
  gratuito (la API responde 403: «Upgrade to GitHub Pro or make this repository public»). Mientras tanto:
  - el repositorio solo admite *squash* (título del PR como commit, mensajes de los commits como cuerpo, con
    sus `Signed-off-by`) y borra la rama al fusionar;
  - `build/githooks/pre-push`, que instala `cl setup`, bloquea el *push* directo a `main` en cada clon. Se
    salta solo con `CLICALO_ALLOW_MAIN_PUSH=1`, para emergencias.
- **Coste.** Es una protección de disciplina, no de servidor: un clon sin `cl setup` o la interfaz web de
  GitHub pueden saltársela, y los checks no son obligatorios para fusionar.
- **Revisión.** Al hacer público el repositorio (o con GitHub Pro) se activa la protección real: PR
  obligatorio, checks `verify (x64)`, `desk (x64)`, `title`, `adr` y `dco`, historial lineal, conversación
  resuelta y sin excepciones para administradores. Entonces esta entrada se elimina.
- **Cierre (2026-10-05).** Con el repositorio público, `main` tiene la protección real de GitHub: PR
  obligatorio, comprobaciones `verify (x64)`, `desk (x64)`, `title`, `adr` y `dco`, historial lineal,
  conversaciones resueltas, sin *force push* ni borrado y **sin excepciones para administradores**. El *hook*
  `pre-push` de `cl setup` se conserva porque avisa antes de llegar a la red, pero ya no es la única barrera.
  También se activaron las alertas de Dependabot, la detección de secretos con bloqueo de *push* y el informe
  privado de vulnerabilidades que promete `SECURITY.md`.

## D-14 · Contratos de M1 para el primer plano

- **Plano.** [§3.5](blueprint.md#35-ventanas-no-activables) pone `SurfaceId` junto a `NonActivatingWindow`
  (`Clicalo.UI.Wpf.Windowing`) y hace que `ActivationGuard` llame directamente a
  `Orchestrator.RestoreAfterViolation`; [§3.6](blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano)
  declara `IForegroundControl` e `ISurfaceActivationStyle` y describe el atajo interno de derechos y Win+H como
  efectos que inyecta el motor.
- **Repositorio.**
  - `SurfaceId` (con `SurfaceKind`) y `WindowToken` viven en `Clicalo.Application.Ports`, porque los usan los
    puertos y Application no ve UI.Wpf. `NonActivatingWindow.Id` los expone igual.
  - `IActivationArbiter` (Ports), que implementa `ForegroundOrchestrator`: es lo que `ActivationGuard` llama
    (`IsActivationLeased` y `ReportViolation`), porque UI.Wpf solo puede usar de Application los tipos de
    `Application.Ports` (§4.2, regla de ArchUnit).
  - `ISurfaceLookup` (Ports), que implementa `SurfaceRegistry`: traduce el `WindowToken` destino de una concesión
    a su `SurfaceId`, que es lo que pide `ISurfaceActivationStyle`.
  - `IInternalKeyEffects` (Ports): el envío del atajo interno (Ctrl+Alt+Shift+F24) y de Win+H. En M2 lo implementa
    el motor tras `InjectionGate`; en M1, implementaciones de prueba protegidas.
  - `ForegroundLease` añade la propiedad `Target`; `IForegroundMonitor`, `IInternalRightsHotkey` e `ITouchKeyboard`
    son los puertos que el plano nombra sin firma.
- **Motivo.** Sin estas piezas, los tipos de §3.5 y §3.6 no compilan respetando las reglas de capas de §4.2 y §4.4.
- **Coste.** Tres interfaces más en Ports, todas pequeñas y con una sola implementación.
- **Revisión.** Al cerrar S1 y S4; pendiente de ratificar junto con D-09.

## D-15 · No activación medida en S1

- **Plano.** La tabla del *hook* común de [§3.5](blueprint.md#35-ventanas-no-activables) confía en añadir
  `SWP_NOACTIVATE` en `WM_WINDOWPOSCHANGING` y aplica el rectángulo de `WM_DPICHANGED` sin pasar el mensaje a WPF;
  `ActivationGuard` cuenta cada mensaje de activación.
- **Repositorio.**
  - Añadir `SWP_NOACTIVATE` en `WM_WINDOWPOSCHANGING` **no impide** la activación: se midió en S1 que Windows activa
    igual. La regla se mantiene, pero el `Show` de WPF de `ShowPassive` y el `WM_DPICHANGED` reenviado van dentro
    de `ActivationVeto`: un *hook* `WH_CBT` del propio hilo de UI (`SetWindowsHookEx` con `dwThreadId`), de ámbito
    mínimo y anidable, que rechaza `HCBT_ACTIVATE` solo de las superficies con un ámbito abierto y solo mientras dura
    la llamada. No es permanente, porque vetaría las activaciones externas que `ActivationGuard` debe ver.
  - `WM_DPICHANGED` se maneja como pide el plano y además se reenvía a `HwndTarget` (con un indicador de reentrada)
    dentro del veto, porque WPF necesita el mensaje para reescalar (ACC-008) y su `SetWindowPos` no lleva
    `SWP_NOACTIVATE` (#7561).
  - `ActivationGuard` sigue una regla simple ([ADR-0024](../adr/0024-vigilante-de-foco-simple.md)): cada
    activación sin concesión pide una restauración, salvo que ya haya una en cola en el *dispatcher*, a la que se
    suma, así que la restauración siempre llega después de las activaciones que cubre. Sustituye al juicio aplazado
    y a las violaciones abiertas de M1, que se tragaron activaciones forzadas cuando Windows no desactivaba la
    superficie (S1, hallazgos 6, 8, 11 y 13).
  - Las superficies, `OwnerAnchor` y `SurfaceRegistry` viven en un único hilo; todas comparten `OwnerAnchor`, así que
    la banda *topmost* se pierde y se repara en familia.
- **Motivo.** Sin el veto, una prueba sin escritorio mostró `WM_ACTIVATEAPP`, `WM_ACTIVATE` y `WM_SETFOCUS` en la
  superficie al mostrarla y al cambiar de DPI (resultados en [S1](../testing/spikes/S1.md)).
- **Coste.** Un *hook* de hilo más. Una activación externa que coincida exactamente con el `Show` o con el cambio de
  DPI también se rechaza y el guardián no la ve. Una superficie activada sin que el primer plano llegue al proceso (sin
  quitárselo a nadie) no cuenta como violación. Una activación que solo se confirma en la segunda mirada se revierte
  hasta 50 ms más tarde, dentro del presupuesto de 200 ms.
- **Revisión.** Al cerrar S1 con las filas manuales del equipo táctil; valorar un veto general entre
  `WM_WINDOWPOSCHANGING` y `WM_WINDOWPOSCHANGED` para cualquier `SetWindowPos` sin `SWP_NOACTIVATE`.

## D-16 · Capa de punteros implementada en M1

- **Plano.** [§7.8](blueprint.md#78-filtro-táctil-y-gestos-domaintouch) y
  [§8.3](blueprint.md#83-entrada-táctil) describen `TouchFilter`, `GestureRecognizer` y `PointerInputSource` sin
  fijar cómo llega el mouse, quién ejecuta los plazos ni la regla exacta de resolución del objetivo.
- **Repositorio.**
  - `PointerSetup.EnableMouseInPointer` al arrancar: el mouse llega a las superficies como `WM_POINTER`. Afecta a
    todo el proceso y no se puede deshacer.
  - `GestureHost` (público, UI.Wpf.Pointer): la tubería de gestos de cada superficie, con un único temporizador de
    `TimeProvider` llevado al dispatcher.
  - Las muestras de `PointerFrame` solo son válidas durante `IPointerFrameSink.OnFrame`: `PointerInputSource`
    reutiliza sus búferes para no asignar memoria (copia: `frame with { Samples = [.. frame.Samples] }`).
  - `PointerInputSource` marca como manejados los mensajes de contacto y el cambio de captura de sus contactos; el
    *hover* y el botón derecho siguen a `DefWindowProc`. La hora del fotograma sale de `PerformanceCount` con el tope
    `Timings.Touch.PointerStampMaxAge` (1 s).
  - Un 0 desactiva cada comprobación del filtro, también `cancelMovePx`; umbral de palma
    `Timings.Touch.PalmContactMinPx` (120 px lógicos, provisional); el deslizamiento se decide al levantar el dedo con
    `dx > 60 px`.
  - Resolución del objetivo: acierto directo; centro más cercano si los límites se solapan (REG-02); objetivo más
    cercano con desempate por el centro en el área extra (TAC-002).
- **Motivo.** S1 exige que dedo, lápiz y mouse lleguen como fotogramas, y la ruta caliente no puede asignar memoria.
- **Coste.** La documentación de `WM_POINTERCAPTURECHANGED` avisa de que consumir de forma selectiva la entrada de
  puntero y pasar el resto a `DefWindowProc` tiene un comportamiento no definido: S2 o S15 deben confirmar el *hover*
  y el botón derecho en hardware real. La lectura conjunta de REG-02 y TAC-002 está pendiente de que el usuario la
  confirme.
- **Revisión.** En S2, con trazas reales del equipo táctil.

## D-17 · Primer plano implementado en M1

- **Plano.** [§3.6](blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano),
  [§7.9](blueprint.md#79-cambio-de-app-de-extremo-a-extremo-per-003) y la tabla de hilos de
  [§3.2](blueprint.md#32-modelo-de-hilos), que pone `ForegroundOrchestrator` en el hilo SysEvents.
- **Repositorio.**
  - `ForegroundMonitor` se engancha **sin** `WINEVENT_SKIPOWNPROCESS`: anota las ventanas propias, pero nunca las
    publica; así, tocar la app a la que volvería una concesión cuenta como cambio y la termina.
  - `ExternalForeground` añade `AppProcessId` (la app detrás de ApplicationFrameHost) y `Elevation` con el enum
    `ProcessElevation` (`Unknown` cubre EC-PER-03), ambos opcionales.
  - Tipos públicos nuevos: `ForegroundPorts`, `LadderStep`, `LeaseEndReason`; en `ForegroundLease`, `Origin`,
    `GrantedAt`, `IsActive`, `EndReason`, `Ended` y `KeepAlive()`; en `ForegroundOrchestrator`, `ActiveLease` y
    `EndActiveLeaseAsync` (evento terminal).
  - Cada `SetForegroundWindow` que `GetForegroundWindow` no confirma al momento se vuelve a comprobar cada
    `RestoreVerifyInterval` durante `RestoreRetryDelay` antes de contarse como rechazado: la activación entre hilos es asíncrona (S4, hallazgo 1), y una
    sola mirada al final de la espera daba por rechazada una restauración que había funcionado (S1, hallazgo 10). No se
    reintenta si el monitor verificó entretanto un cambio a otra app, y una restauración tras una violación que esperó
    su turno no hace nada si el monitor verificó otra ventana externa después del informe y ninguna superficie está
    delante (S1, hallazgo 12): en ambos casos se tomaría el primer plano de lo que la persona eligió.
  - `TryNowTarget` vuelve al Centro de control que estaba delante y conserva el `prev` original (CCM-004).
  - `NOTIFYICONDATAW` escrito a mano, solo x64 y ARM64 (CsWin32 no lo genera para AnyCPU).
  - Una violación durante una concesión devuelve el primer plano al destino de la concesión, no a
    `lastExternalForeground`: §3.5 no cubre ese caso, y restaurar la app externa terminaba la concesión.
  - El orquestador no se ejecuta en el hilo SysEvents, sino en el grupo de hilos: `ReportViolation` vuelve enseguida y
    restaura desde allí (se llama dentro del procedimiento de ventana de la superficie, en el hilo de UI), y
    `AcquireAsync`, la devolución de una concesión, la restauración tras una violación y la reacción a un cambio de
    primer plano verificado salen del hilo que las llama antes de tocar el primer plano o una superficie. Así el hilo
    de UI nunca llama a `SetForegroundWindow` y el *callback* WinEvent de SysEvents nunca espera un `SetWindowLong`
    entre hilos (§3.2); lo comprueban `ForegroundOrchestratorLeaseTests` (afinidad) y las pruebas de escritorio de
    `Windowing.IntegrationTests/Foreground`, que piden las concesiones desde el hilo de UI.
  - Tras el `WM_HOTKEY` del atajo interno, la escalera espera a que sus teclas estén sueltas
    (`IInternalRightsHotkey.WaitForChordReleaseAsync`, `Timings.Foreground.ChordReleasePoll`) antes de reintentar: si
    no, una liberación llegaba a la ventana nueva y la app de delante se quedaba con Ctrl pulsada (S4).
- **Motivo.** Hallazgos de la primera ejecución de escritorio de S4 ([S4](../testing/spikes/S4.md)) y de la
  integración de M1.
- **Coste.** Más superficie pública en Application.Foreground; todas las adiciones son compatibles.
- **Revisión.** Al cerrar S4 con las filas manuales (Acceso por voz, Narrador y Word).

## D-18 · UI Automation implementada en M1

- **Plano.** [§8.6](blueprint.md#86-accesibilidad-uia-y-números-de-voz) y el contrato de `AnnouncementUrgency`
  (cortés = `ImportantMostRecent`).
- **Repositorio.**
  - Un aviso cortés usa `AutomationNotificationProcessing.MostRecent`: las variantes `Important*` interrumpen al
    lector, lo contrario de «cortés» (S3, fila 9a). El urgente sigue en `ImportantAll`.
  - `IInvokeProvider.Invoke` vuelve enseguida y `Invoked` llega en el siguiente turno del dispatcher, como pide UI
    Automation; `Toggle`, `Expand` y `Collapse` son síncronos.
  - El *peer* solo es enfocable por teclado mientras la ventana está activa (bajo concesión); si no, `SetFocus` lanza
    `InvalidOperationException` sin enfocar.
  - `LiveAnnouncer.ForUiaBstr` compensa un defecto de WPF: `RaiseNotificationEvent` pasa una cadena ancha donde UI
    Automation lee un `BSTR`, y los lectores recibían la mitad del texto. Lo vigila
    `Upstream/WpfNotificationBstrTests` (`wpf-notification-bstr`).
- **Motivo.** Hallazgos de S3 ([S3](../testing/spikes/S3.md)).
- **Coste.** El relleno depende de que WPF pase la cadena fijada sin copiarla; la prueba Upstream avisa si WPF lo
  corrige, pero no si cambia a otro formato.
- **Revisión.** Con Narrador en la fila manual 9a (quizá `CurrentThenMostRecent`) y con cada actualización de WPF.

## D-19 · Spikes resecuenciados al cerrar M1

- **Plano.** [§14](blueprint.md#14-hoja-de-ruta-por-hitos) pone en M1 todos los spikes salvo S13 (primero S1, S3, S4 y
  S2; después S5, S7, S15, S9, S6, S14, S8, S10, S11 y S12) y cierra M1 con «todos los criterios de §15 superados, o una
  decisión registrada».
- **Repositorio.**
  - M1 se cierra con la **decisión registrada** del usuario (2026-09-26, «continuar») sobre la evidencia real:
    473 toques reales sin ningún cambio de primer plano ni violación `reg01` (p95 15,7 ms) y todas las pruebas
    automáticas de S1, S3 y S4 en verde en la CI. Las filas manuales de S1 (panel, Pestaña, burbuja, IME y menús), S3
    (Acceso por voz y Narrador) y S4 (dictado) se repiten con el panel real en la aceptación en hardware de M3,
    **midiendo automáticamente** ([M1-closure.md](../testing/spikes/M1-closure.md)).
  - S5, S7, S9 y S11 se resuelven **dentro de M2** como pruebas (sus paquetes están en
    [M2-ownership.md](../testing/spikes/M2-ownership.md)).
  - S2 residual (el *dispatcher* con el Centro de control cargado; conectar y desconectar la pantalla táctil), S6 y S15,
    **antes de M3**.
  - S12, en la aceptación en hardware de M3.
  - S8, S10 y S14, **antes de M5**.
  - S13, en M7, como ya fijaba §15.1.
- **Ningún criterio se rebaja.** Los criterios de éxito y las reglas «si falla» de
  [§15.1](blueprint.md#151-spikes-con-criterio-de-éxito) y de cada guion siguen intactos; solo cambia el hito en que se
  miden. En particular, si en M3 falla una fila de toque de S1 o S4, se reabre ADR-0001 antes de seguir con
  funcionalidad.
- **Motivo.** La sesión manual mostró que el laboratorio pedía al usuario contar y pulsar «Funcionó», y la tira-guía
  acaparó los toques; repetir con el panel real y medición automática da mejor evidencia y respeta la preferencia del
  usuario. Los spikes de M2 son de las piezas que M2 construye (arranque, inyección, guardián y persistencia).
- **Coste.** M2 se construye sin la prueba manual del panel real (riesgo acotado: M2 no añade superficies y todo lo que
  construye es independiente de la capa de UI). Las decisiones de *dispatchers* (S2), publicación (S5) y desenfoque (S6)
  se documentan con datos más tarde de lo previsto.
- **Revisión.** En la aceptación en hardware de M3, con las filas manuales hechas.

## D-20 · Contratos de M2

- **Plano.** [§6.1 a §6.6](blueprint.md#6-modelo-de-dominio-y-persistencia) y [§7.2 a §7.4](blueprint.md#72-política-de-activación-única-eje-001)
  nombran los tipos del dominio y del motor; §4.3 la matriz de módulos.
- **Repositorio.**
  - `Library` es `ShortcutLibrary` y `Settings` es `UserSettings`: un tipo con el mismo nombre que su espacio de nombres
    (`Clicalo.Domain.Library.Library`) hace ambiguas todas las referencias desde los módulos hermanos.
  - `Error` es `Failure`, y las fábricas de `Result<T>` están en `Results`: `Error` es palabra reservada de Visual Basic
    (CA1716) y un genérico no puede tener miembros estáticos (CA1000).
  - `SecretText` vive en `Clicalo.Domain.Privacy` (donde lo buscan CLC0003 y `docs/guides/analyzers.md`), no en Library.
  - Módulo nuevo `Clicalo.Domain.Commands` (`IDocumentCommand`, `IDestructiveCommand`, `UndoIntent`, `DocumentChange`,
    `DomainContext`, `DomainEvent`, `BackupRequirement`): CLC0010 lo espera ahí y así no hay ciclo con `Document`.
    `Library` suma `Timing` (el rango de `WaitStep`, I6) y `Migration.V1` sumaba `Document` (el conversor devolvía un
    `UserDocument`; el módulo se retiró con [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)); todo en
    `architecture/domain-modules.json`.
  - `WebAction` es `UrlAction` (el tipo persistido es `url`); `InjectedKey` está en `Keys` porque lo necesita el
    *ledger* lógico (`KeySafety` solo depende de `Keys`); el efecto que cuenta un uso es `EngineEffect.CountUsage`,
    porque `RecordUsage` es el comando exento de `undo-exemptions.json`.
  - `EngineState` no lleva teclas fijas (llegan en M3) y la distribución viaja en `ForegroundInfo`; `ActivationContext`
    lleva `TestMode` como `bool` porque `Execution` no depende de `Interaction`.
  - Los puertos del motor (`IInputInjector`, `IKeyLedger`, `IShellExecutor`, `IClipboardPaster`, `IEngineInbox`) y de
    la persistencia (`IDocumentRepository`, `IUsageRepository`, `IAtomicFileWriter`, `IBackupService`) viven en
    `Application.Ports`, como en [D-10](#d-10--puertos-en-applicationports-y-revelado-de-secretos). `IInputInjector` e
    `IClipboardPaster` reciben el texto como `ReadOnlySpan<char>`: solo el motor llama a `WithRevealed`. El estado de un
    envío es `InjectionStatus` (SpikeLab ya tiene un `InjectionOutcome`).
  - `PersistenceScheduler` vive en `Application.Persistence`, separado de `Application.Store`, para que dos paquetes no
    compartan carpeta.
  - Sentinel recibe sus umbrales por la línea de órdenes, porque no puede leer `timings.json`
    ([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md)).
  - El arnés de `CatalogGeneratorHarness` compila también `Domain/Primitives`, del que ahora dependen `Keys` y `Catalog`.
  - Proyectos de prueba nuevos: `Clicalo.Infrastructure.Tests`, `Clicalo.Sentinel.Tests` y `Clicalo.Performance`.
- **Motivo.** Que los contratos compilen con las reglas de §4 (capas, módulos, analizadores) sin ambigüedades.
- **Coste.** Nombres distintos de los del plano en seis tipos; la tabla de arriba es la traducción.
- **Revisión.** Al cerrar M2; si el plano pasa a 1.2, se adoptan los nombres del repositorio.

## D-21 · Integración de M2

- **Plano.** [§3.1](blueprint.md#31-vista-de-procesos), [§3.2](blueprint.md#32-modelo-de-hilos),
  [§6](blueprint.md#6-modelo-de-dominio-y-persistencia) y [§7](blueprint.md#7-motor-de-ejecución) describen el dominio,
  el motor, la persistencia, la migración y el arranque; [D-20](#d-20--contratos-de-m2) fijó sus contratos.
- **Repositorio.** Lo que los cinco paquetes y la integración añadieron o hicieron de otra forma:
  - **Dominio.** Módulo `ProfileResolution` (tabla PER-001 a PER-008) con sus rutas en el paquete `domain`. Comando
    nuevo `DiscardDraft` (ATJ-011, EC-EDI-04). `SetSetting` no es genérico: recibe `(Path, Value)`, para que su nombre
    coincida con `undo-exemptions.json`. `CanonicalChord.TryFrom` y `TryParse` en lugar de `From(KeyChord, KeyCatalog)`;
    `ForBlockedComparison` compara sin el lado del modificador (R-08). La porción `Settings` del deshacer solo restaura
    los ajustes deshacibles; `lockProfile` y `lastProfile` son de colocación y no se deshacen (propuesta R-13);
    `RestoreSlices` corrige un `lastProfile` que apunta a un perfil que ya no existe. `Settings` depende ahora de `Timing`
    y `Catalog` (`domain-modules.json`), y sus valores por defecto salen de `Timings` y `TouchPresets`.
  - **Almacén.** `DocumentStore` descarta una entrada agrupada cuyo deshacer no cambiaría nada, entrega `Changed` en
    orden de revisión por una cola (posiblemente en otro hilo que también despacha) y gasta el `ConfirmationToken` al
    aplicarlo (`store.confirmation.spent`): dos toques ejecutan un solo borrado (REG-04).
  - **Motor.** Miembros y tipos públicos nuevos (`EngineState.Outbox`, `QueuedStep`, `PressedBatch`, `StickyState`,
    `HoldOrigin.Tap`, `KeyLedgerSection.ReadOnlyView`, `InjectionGate.TryReleaseEverything`, `GuardianProcess`,
    `CrashJournal`…) y los eventos `ReleasesBlocked`, `SessionResumed`, `StickyTapped` y `ClearSticky`. La tabla fija de
    `keys.win32.json` es una copia a mano en `Domain.Execution.Internal.Win32FixedKeys`, comparada con el JSON por una
    prueba, hasta que el generador la emita. Una liberación que `SendInput` envía solo en parte vuelve como
    `ReleasesBlocked`, y `GateInputInjector` suelta lo que un lote equilibrado (clic o texto) dejó pulsado. Sentinel no
    escribe el diario de fallos: lo añade el principal relanzado con `--after-crash` ([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md),
    punto 5). `EmergencyReleaser` corre en su propio temporizador de `TimeProvider`, no en el hilo SysEvents, para que
    ni la UI ni SysEvents colgados lo bloqueen. S9 congela el motor en la CI con el modelo, no con una compilación Chaos
    con punto de ruptura.
  - **Persistencia.** `DocumentLoadOutcome.RecoveredFromPending` (la copia de emergencia de `pending\`). Los esquemas
    persistidos viven en `data/schemas` y los valida `Clicalo.Data.Tests`. El registro está en
    `Infrastructure/Logging`, fuera del reparto de M2, porque `banned-api-exceptions.json` espera ahí su *sink*.
  - **Migración.** `V1Importer.MigrateAsync` (guarda el original byte a byte antes de convertir), `V1ComboScan`,
    `V1Counts.Of` y tres valores de `MigrationNoteKind`; los repetidos de una importación salen de `DuplicateIndex`.
    **Retirado** con todo el módulo `Migration.V1` por la decisión D1 del usuario del 2026-10-03
    ([ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)).
  - **App.** La instancia única (mutex y *pipe* con DACL) vive en `Clicalo.App/SingleInstance` hasta que la IPC de
    `ImportFile` y `OpenUri` la lleve a `Platform.Windows/SingleInstance` y `Platform.Core/Ipc`; es ruta sensible. La
    comprobación va en los dos sentidos, con lo que M2 tiene: el cliente compara la imagen del servidor con su propia
    ruta (el editor fijado y la copia protegida del componente llegan con `Platform.Core/Trust` en M5), y el servidor
    exige la misma sesión y el SID del usuario en el *token* del cliente y lee su integridad (`PipeAdmission`; con
    `show` como único verbo, un cliente de menor integridad no obtiene más). Su interoperabilidad (`PipePeer`,
    `ProcessIdentity`) ya está en `Platform.Windows/SingleInstance`, y D13 se vigila en `App.SingleInstance` y
    `Platform.Windows.SingleInstance` hasta que exista `Application.Ipc`. Un segundo arranque que encuentra el nombre
    ocupado solo lo dice con su código de salida (`InstanceSquatted`): el registro `ipc.squat_detected` y el aviso de
    §3.4 llegan con la IPC de M4, porque ese proceso no abre el registro (lo tiene el primero) ni tiene ventana.
    `App/Interop` conservaba solo `MonitorLayout` (los monitores de la migración v1); se retiró con
    [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md). `sidHash` se calcula sobre el SID binario, y la
    DACL da también `CreateNewInstance` al usuario, que el SDDL de §3.4 omite. `PanelProjector` está en
    `Presentation/Panel`, no en `Application.Projections`, y sus pruebas sin ventana en el proyecto de Windowing. La
    franja de «Soltar todo» va debajo de las fichas, para no mover ninguna bajo el dedo.
  - **Arranque.** Un primer arranque sin documento instala el kit inicial por defecto de `content` («Básicos», decisión
    D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)) en el idioma de Windows y lo escribe al
    momento. La conversión del archivo v1 de `--migrate-v1` sobre la semilla se retiró con
    [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md). `--exit-after` recorre la salida completa sin
    intervención (diagnóstico). Bloquear y suspender sueltan por `SessionKeyRelease` (SEG-006). El proyecto copia
    `i18n`, el contenido inicial (kit, semilla y plantillas) y `Clicalo.Sentinel.exe` junto a `Clicalo.exe`, y los
    textos ya no se buscan en carpetas superiores.
  - **Pruebas.** Proyecto nuevo `Clicalo.App.Tests` (opciones, protocolo del *pipe*, adaptadores sin envío, documento
    del primer arranque) con `InternalsVisibleTo` en `Clicalo.App`.
  - **Tras la verificación de M2 (persistencia e IPC).**
    - Un único consumidor de persistencia (§3.1, §6.4): `BackupService` ya no tiene hilo propio. `SnapshotNow` solo
      encola dentro del *lock* del almacén, y `PersistenceScheduler` escribe esas copias en cuanto llega el cambio y
      siempre antes del documento. Si una no se puede escribir, el documento cambiado espera y el fallo sigue las
      reglas de un guardado fallido, hasta hacerse visible (DAT-006, §6.8: «crea antes una copia»).
    - La E/S del arranque (diario de fallos, documento e idiomas) corre en el grupo de hilos (`StartupReader`), nunca en
      el hilo de UI (§3.2).
    - Un documento del arranque que no se pudo escribir pasa al autoguardado (`PersistenceScheduler.MarkUnsaved`). La
      marca `migration-v1.pending` de una migración v1 fallida, un archivo que §6.5 no listaba, se retiró con
      [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md).
    - Un idioma con la entrada o los textos rotos se omite y se registra (`startup.language_skipped`); solo
      `locales.json` ilegible o sin textos del idioma por defecto detienen el arranque.
    - **Hilo SysEvents.** Al suspender, `SuspendRelease` espera en SysEvents, como mucho
      `Timings.KeySafety.SuspendReleaseWait` (500 ms), a que el motor confirme que no retiene nada, porque el equipo
      puede dormirse en cuanto se responde `WM_POWERBROADCAST`. §3.2 dice que SysEvents nunca hace llamadas que puedan
      bloquear: esta espera acotada es la excepción `app-suspend` de `banned-api-exceptions.json`, separada de
      `app-shutdown` (solo el vaciado de fin de sesión) y de `app-second-start` (la espera del segundo proceso).
- **Motivo.** Que los cinco paquetes compongan un `Clicalo.exe` que arranca, envía por la valla y se cierra limpio sin
  cambiar ningún contrato de D-20 ni rebajar ningún requisito.
- **Coste.** Una copia a mano de la tabla Win32 hasta cambiar el generador; la instancia única y su interoperabilidad
  en la raíz de composición durante M2.
- **Revisión.** Al cerrar M2, con la primera ejecución de `desk (x64)` y `perf (x64)` en la CI; la tabla Win32, al
  cambiar `KeysEmitter`; la instancia única y su aviso de nombre ocupado, con la IPC de M4; la espera de SysEvents al
  suspender, si la CI mide que retrasa la bandeja o el primer plano.

## D-22 · Correcciones del motor tras verificar M2

- **Plano.** [§3.2, regla 6](blueprint.md#32-modelo-de-hilos) escala la emergencia a `TerminateProcess` siempre que no
  puede tomar la valla o en el segundo cuelgue en 10 minutos; [§7.6](blueprint.md#76-eventos-terminales-seg-007) reintenta
  lo que rechaza el escritorio seguro «en `UNLOCK`»; [§7.5](blueprint.md#75-invariantes-de-seguridad-de-teclas) verifica
  INV-10 como propiedad de `LayoutPlanner`; [§3.6](blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano)
  y [D-14](#d-14--contratos-de-m1-para-el-primer-plano) dejan el atajo interno y Win+H al motor; [§3.1](blueprint.md#31-vista-de-procesos)
  daba a Sentinel la vida del principal y, en la fila «Muerte del proceso» de §7.6, un solo soltado antes de relanzar
  (hasta aceptar ADR-0018).
- **Repositorio.** Lo que corrigió la verificación de M2 en el motor ([ADR-0019](../adr/0019-valla-en-las-escrituras-del-motor-y-reenvio-de-liberaciones.md)):
  - **Latido y marcas bajo la valla.** `EngineHost` escribe el latido y sus marcas (`EngineAlive`, `CleanShutdown`,
    `NoRelaunch`) con `InjectionGate.TryWriteHeartbeat` y `TryUpdateMarks` y su generación: un motor zombi que se
    reanuda se detiene en su primera vuelta y nunca renueva el latido del motor que lo sustituyó (`IKeyLedger`
    cambia `WriteHeartbeat` por `TryWriteHeartbeat` y gana `TryUpdateMarks`).
  - **Escritorio seguro sin bloqueo.** «Soltar todo» y todos los eventos terminales reenvían las liberaciones
    rechazadas (sin soltar una tecla que un titular volvió a pulsar). Un *hook* `EVENT_SYSTEM_DESKTOPSWITCH` del hilo
    SysEvents (`InputDesktopWatch`) publica `SessionResumed` en cuanto el escritorio de entrada se puede abrir, y lo
    vuelve a comprobar un número acotado de veces (`Timings.KeySafety.InputDesktopRecheck`). `SessionResumed` pide
    además a la valla que reenvíe lo que el *ledger* físico guarda en `ReleasePending`
    (`InjectionGate.TryReleasePending`), así que tampoco se pierde lo que rechazó una emergencia o el soltado de un
    motor que falló, que ahora conserva `BlockedReleases`.
  - **Lotes equilibrados en la valla.** `InjectionGate.TryInjectBalanced` y `TryInjectChord` sueltan en el mismo
    *lock* lo que un `SendInput` parcial dejó pulsado, con la máscara de menú delante de Alt o Win.
  - **Acordes internos desde el motor.** `EngineKeyEffects` publica `InternalChordRequested`; el reductor emite
    `SendInternalChord` (también en modo de prueba o pausa, INV-7) y el anfitrión lo envía con su generación
    (`IInputInjector.SendChord`) y responde por `InternalChordReplies`; un motor colgado o sustituido responde «no
    enviado» tras `Timings.Engine.InternalChordWait`. Desaparece `InternalKeyEffects`.
  - **Emergencia sin guardián.** Sin Sentinel en marcha (antes de lanzarlo, entre dos lanzamientos o tras
    `GuardianUnstable`) la emergencia nunca termina el proceso: en el segundo cuelgue suelta y reinicia el motor, y si
    no puede tomar la valla lo reintenta en cada comprobación con esperas crecientes
    (`Timings.Engine.EmergencyGateRetryWaits`). `IGuardian.Unstable` se registra y se avisa en el panel, de forma
    asertiva, de que la protección de teclas está desactivada hasta reiniciar Clícalo (`GuardianUnstableNotice`, clave
    `guardianUnstable`).
  - **Liberación pendiente y tecla pulsada otra vez.** Si un titular vuelve a pulsar una tecla cuya liberación rechazó
    el escritorio seguro, la ranura del *ledger* vuelve a contar una sola referencia (la subida pendiente ya anuló las
    anteriores), así que la liberación de ese titular la deja libre y los acordes internos no la saltan; si esa pulsación
    no llega a `SendInput`, la ranura vuelve a `ReleasePending` (las pulsaciones de un lote se deshacen de la última a la
    primera).
  - **Suspender vacía la persistencia.** Tras el soltado, el manejador de `PBT_APMSUSPEND` ejecuta el vaciado de la salida
    (documento, uso y copias en cola, todo por el consumidor de persistencia) con límite
    `Timings.App.SuspendFlushTimeout` (`App/Shutdown/SuspendFlush`). Es otra espera acotada en SysEvents: comparte con
    `SuspendRelease` la excepción `app-suspend` de `banned-api-exceptions.json`. Un vaciado que corta su límite (un
    bloqueo de OneDrive o del antivirus) deja pendiente lo que no escribió y vuelve a armar sus temporizadores: el
    autoguardado lo escribe al reanudar y el vaciado de la salida si se sale antes (REG-08).
  - **Sentinel con la sesión bloqueada (decisión D3 del usuario, 2026-10-03).** Si el principal muere y `SendInput`
    rechaza el lote de liberación, Sentinel vuelve a enviar lo que no salió en cada latido
    (`Timings.Guardian.PipeHeartbeatInterval`) y solo cuando todo salió decide el relanzamiento, así que el proceso nuevo
    nunca pulsa una tecla que Sentinel vaya a soltar después. El rechazo del escritorio seguro (nada aceptado y
    `ERROR_ACCESS_DENIED`) se reintenta sin límite mientras dure el bloqueo; cualquier otro, como mucho
    `Timings.Guardian.RefusedReleaseWait` (30 s), y después relanza igualmente. Si la sesión termina, Windows termina
    Sentinel con ella y no se relanza nada. El arranque pasa al protocolo 2 con un séptimo argumento
    `--refused-release-wait-ms` ([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md), punto 6;
    [contracts.md](contracts.md#arranque-de-clicalosentinelexe)). La propuesta preparada aquí lo llamaba
    `LockedReleaseWait`; se renombró al decidir que la espera con la sesión bloqueada no tiene límite.
  - **INV-10.** No existe `LayoutPlanner` en M2: la propiedad llega con él en M3. Mientras tanto, un contacto conserva el
    objetivo sobre el que bajó (`GestureRecognizer`, PAN-009) y la franja de «Soltar todo» va debajo de las fichas
    ([D-21](#d-21--integración-de-m2)). Las pruebas del modelo del motor enumeran lo que comprueban de verdad.
- **Motivo.** Sin estas correcciones, un motor zombi ocultaba el cuelgue del siguiente, una tecla soltada durante un
  aviso de UAC quedaba pulsada sin que «Soltar todo» la arreglara, un acorde interno parcial dejaba Ctrl+Alt+Mayús o
  Win pulsados sin titular, un cuelgue sin guardián terminaba el proceso sin que nadie soltara ni relanzara, y una
  muerte con la sesión bloqueada dejaba las teclas pulsadas al desbloquear (REG-03).
- **Coste.** Un *hook* WinEvent más en SysEvents; el latido toma el *lock* de la valla en cada vuelta (sin contención
  salvo durante un `SendInput`); una vuelta del buzón del motor en cada acorde interno; Sentinel puede sobrevivir al
  principal tanto como dure el bloqueo (un `SendInput` fallido por segundo, bloqueado en un evento entre medias) y
  Clícalo no vuelve hasta el desbloqueo.
- **Revisión.** INV-10, con `LayoutPlanner` en M3. La espera de Sentinel con la sesión bloqueada quedó resuelta el
  2026-10-03 por la decisión D3 del usuario ([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md),
  punto 6). Con ADR-0018 aceptado al integrar M2 (2026-10-05), §3.1 y la fila «Muerte del proceso» de §7.6 del plano
  ya lo recogen, y esa parte deja de ser una desviación. Queda comprobar en un escritorio real bloqueado (CI o
  aceptación en hardware, como el regreso del escritorio de entrada) que el rechazo llega como `ERROR_ACCESS_DENIED`
  y que la tecla sube al desbloquear; si un escritorio bloqueado devolviera otro código, se amplía la lectura del
  rechazo con un ADR, no se rebaja D3.

## D-23 · Criterios de salida de M2 tras la verificación

- **Plano.** [§10.3](blueprint.md#103-presupuestos-de-rendimiento) guarda los presupuestos en
  `tests/Clicalo.Performance/budgets.json` y los mide en el equipo táctil; [§10.5](blueprint.md#105-cicd) dice que el
  rendimiento no es obligatorio en el PR y ejecuta `lab.yml` cada semana y antes de cada beta; la fila M2 de §14 pide la
  prueba de bandeja con el Bloc de notas y las propiedades de 10 000 casos con contraejemplos reducidos (CsCheck).
- **Repositorio.** Lo que corrigió la verificación de M2 en los criterios de salida
  ([M2-ownership.md](../testing/spikes/M2-ownership.md#criterios-de-salida)):
  - **Presupuestos en `data/catalogs/budgets.json`**, con su esquema en `data/schemas/budgets.schema.json` validado por
    `Clicalo.Data.Tests`: son datos versionados como el resto de catálogos. Solo están los que hace cumplir una medición.
  - **La puerta de toque → `SendInput` (p95 ≤ 50 ms) también en los *runners* alojados** (`gate: everyRun`), y el
    trabajo `perf (x64)` de `pr.yml` es obligatorio. Es más estricto que el plano: mientras no exista el equipo táctil,
    es la única forma de que una ejecución haga cumplir el criterio de M2. Los presupuestos de S5 siguen siendo
    tendencia en los alojados. Si un alojado supera 50 ms, el criterio no se rebaja: se mide en el equipo táctil o se
    abre una propuesta en §6.1 del catálogo.
  - **`lab.yml` solo a mano** (`workflow_dispatch`) hasta que el equipo táctil esté registrado como *runner*
    (`self-hosted`, `Windows`, `lab`); entonces gana la programación semanal y la ejecución antes de cada beta.
  - **Prueba de bandeja**: el clic en el icono se sustituye por un toque sintético en el panel del propio proceso (el
    mismo derecho de primer plano, paso 1 de la escalera de §3.6) y el menú se abre con `TrayController.OpenMenuAsync`;
    el Bloc de notas real solo se usa en la CI, e InputProbe sigue siendo la app delante en local.
  - **Muerte en cada paso y congelar y reanudar** reducen sus contraejemplos con un reductor propio (`Counterexamples`)
    sobre las mismas 10 000 semillas deterministas, no con CsCheck: `architecture/allowed-dependencies.json` no permite
    CsCheck en `Clicalo.Platform.IntegrationTests`. Las regresiones guardan valores, no semillas
    ([property-regressions.md](../testing/property-regressions.md)).
- **Motivo.** La verificación encontró que ninguna ejecución hacía cumplir el p95 de 50 ms, que la prueba de bandeja no
  usaba el Bloc de notas y que los contraejemplos no se guardaban. En un *runner* alojado el icono nuevo queda en el
  desbordamiento del área de notificación y hacer clic ahí inyectaría en el Explorador.
- **Coste.** Un PR puede fallar por el rendimiento de un alojado ruidoso; el camino real del icono (`TrayIcon`,
  `NIN_SELECT`, `WM_CONTEXTMENU`) queda para la aceptación en hardware de §10.2.
- **Revisión.** Cuando el equipo táctil sea *runner*: volver a la puerta en `lab.yml` según §10.5 y decidir si
  `perf (x64)` sigue siendo obligatorio. Si se permite CsCheck en `Clicalo.Platform.IntegrationTests`, pasar las dos
  pruebas a `Gen.Sample` sin cambiar sus regresiones.

## D-24 · Latencia del panel medida en la CI

- **Plano.** [§7.1](blueprint.md#71-del-toque-a-la-acción) y [§10.3](blueprint.md#103-presupuestos-de-rendimiento):
  toque → `SendInput` con p95 ≤ 50 ms (NFR-001), medido con puntero sintético; la fila M2 de §14 lo exige en el equipo
  táctil sobre 20 toques.
- **Repositorio.** `PanelDesktopTests` juzga la parte del panel (Windows registra el levantamiento → la activación está
  en el buzón del motor) con los números de `TouchToSendInput` de `data/catalogs/budgets.json` (p95 ≤ 50 ms, al menos
  20 muestras), leídos del catálogo, sobre **21 toques medidos** (dedo, lápiz y ratón). Usa **un dispositivo sintético
  por tipo** durante todo el ciclo y hace antes **un toque de calentamiento por dispositivo** sobre el mismo mosaico, que
  tiene que llegar al motor sin quitar el primer plano ni el foco, y cuya latencia se informa pero no entra en el p95.
  Cada toque se parte en tramos (espera en la cola del hilo de UI y trabajo del panel) con la línea de tiempo del hilo
  de UI de los toques lentos ([panel-latency.md](../testing/panel-latency.md)).
- **Motivo.** La prueba fallaba de forma intermitente en los *runners* alojados (6 de 50 ejecuciones de `s0`) con el
  hilo de UI del panel sin trabajo del panel: creaba un dispositivo nuevo en cada toque, y Windows retiene el primer
  contacto de un dispositivo sintético hasta anunciarlo (`WM_TABLET_ADDED`, 15–200 ms), algo que una pantalla táctil,
  un único dispositivo, no añade a un toque. Quedan además dos retrasos de un solo toque antes de que el panel reciba el
  contacto: el primer contacto sobre la ventana del panel recién creada (12–38 ms, una vez por ventana) y, sobre todo
  con la suite, el primer contacto del lápiz (hasta 615 ms, causa no identificada). Con el criterio nuevo: 0 fallos en
  70 ejecuciones (30 con la suite del módulo, 30 solas y 10 de `cl desk`), p95 por ejecución de 14,5 ms como máximo.
- **Coste.** El primer contacto de cada dispositivo sobre el panel queda fuera del p95 de la CI (sigue en la salida y en
  `panel-tap-latency-*.json`), y con él el JIT del primer toque en Debug (3–8 ms medidos). El presupuesto no cambia y no
  hay un umbral propio de la CI.
- **Revisión.** En la aceptación en hardware (equipo táctil) del requisito, incluido si el primer toque tras mostrar
  de nuevo el panel paga otra vez el retraso del primer contacto, y en S2 para el retraso del primer contacto del lápiz
  con un lápiz real.

## Puntos del plano pendientes de resolver

No son desviaciones del repositorio, sino contradicciones o huecos detectados al redactar la documentación.
Se resuelven en el hito indicado; mientras tanto, esta es la interpretación vigente.

| Punto | Detalle | Interpretación vigente | Cuándo se resuelve |
|---|---|---|---|
| Margen de sombra no clicable | La tabla del *hook* común de [§3.5](blueprint.md#35-ventanas-no-activables) respondía `HTTRANSPARENT` a `WM_NCHITTEST` en el margen de sombra, pero la verificación adversarial lo refutó para clics entre procesos (solo actúa entre ventanas del mismo hilo). El plano ya lo recoge | El mecanismo se decide en S6 (`SetWindowRgn` ajustado o alfa 0 en ventana *layered*) con el criterio «el margen no captura clics» | Antes de M3, S6 ([D-19](#d-19--spikes-resecuenciados-al-cerrar-m1)) |
| Firma de todas las DLL | [§2.1](blueprint.md#21-stack-elegido) y [§11](blueprint.md#11-distribución-versionado-y-publicación) firman todas las DLL tras R2R; las condiciones de SignPath Foundation solo permiten firmar artefactos compilados desde el código propio | Se firman los ejecutables y ensamblados propios; la verificación de las DLL de terceros se decide en S8 y, si cambia, con un ADR que sustituya a ADR-0013 | Antes de M5, S8 ([D-19](#d-19--spikes-resecuenciados-al-cerrar-m1)) |

Resuelto al integrar M0: la numeración del catálogo (las preguntas abiertas son su sección 6, las
propuestas pendientes la 6.1 y las discrepancias la 5; el catálogo y el plano ya se citan así).
