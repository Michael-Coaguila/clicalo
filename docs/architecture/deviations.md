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
| D-04 | Visibilidad del repositorio | Público (ARM64, CodeQL y Scorecard en cada PR) | Privado al inicio; esos trabajos, condicionados a que sea público | M0 |
| D-05 | Ubicación del repositorio | Sin especificar | `C:\dev\clicalo`, fuera de OneDrive | M0 |
| D-06 | Notación de trazabilidad | `[Req("ID")]` | `[Trait("Req", "ID")]` | M0 |
| D-07 | Formato de `CHANGELOG.md` | El que genere release-please | Keep a Changelog, con release-please configurado para respetarlo | M0 |
| D-08 | Nota de qué es vinculante del paquete | «Un README» en `docs/design/handoff/` | `LEEME-VINCULANTE.md`, sin tocar el `README.md` original | M0 |
| D-09 | Matriz de módulos de Domain | Tabla de §4.3 | Módulos `Document` y `Timing`, cuatro aristas nuevas y matriz transitiva | M0 |
| D-10 | Puertos y revelado de secretos | Puertos de primer plano en `Application.Foreground`; `WithRevealed` solo en la ejecución y el editor | Puertos en `Application.Ports`; `WithRevealed` también en `Application.Engine` e `Infrastructure.Persistence` | M0 |
| D-11 | Tabla de APIs prohibidas | §4.4 | Ampliada: más fuentes de tiempo y aleatoriedad, `UIElement.Focus`, carga dinámica de ensamblados; `ShellExecuteEx` permitido en `Platform.Windows/Elevation` | M0 |

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

## Puntos del plano pendientes de resolver

No son desviaciones del repositorio, sino contradicciones o huecos detectados al redactar la documentación.
Se resuelven en el hito indicado; mientras tanto, esta es la interpretación vigente.

| Punto | Detalle | Interpretación vigente | Cuándo se resuelve |
|---|---|---|---|
| Margen de sombra no clicable | La tabla del *hook* común de [§3.5](blueprint.md#35-ventanas-no-activables) respondía `HTTRANSPARENT` a `WM_NCHITTEST` en el margen de sombra, pero la verificación adversarial lo refutó para clics entre procesos (solo actúa entre ventanas del mismo hilo). El plano ya lo recoge | El mecanismo se decide en S6 (`SetWindowRgn` ajustado o alfa 0 en ventana *layered*) con el criterio «el margen no captura clics» | M1, S6 |
| Firma de todas las DLL | [§2.1](blueprint.md#21-stack-elegido) y [§11](blueprint.md#11-distribución-versionado-y-publicación) firman todas las DLL tras R2R; las condiciones de SignPath Foundation solo permiten firmar artefactos compilados desde el código propio | Se firman los ejecutables y ensamblados propios; la verificación de las DLL de terceros se decide en S8 y, si cambia, con un ADR que sustituya a ADR-0013 | M1, S8 |

Resuelto al integrar M0: la numeración del catálogo (las preguntas abiertas son su sección 6, las
propuestas pendientes la 6.1 y las discrepancias la 5; el catálogo y el plano ya se citan así).
