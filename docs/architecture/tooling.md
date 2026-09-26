# Herramientas: `cl`, paquetes, analizadores, formato y CI

Cómo se compila, se verifica y se publica Clícalo. Es una decisión reversible: se cambia con un PR normal
que actualiza esta página. El marco está en [§5](blueprint.md#5-estructura-del-repositorio-y-de-la-solución),
[§10.5](blueprint.md#105-cicd) y [§13](blueprint.md#13-convenciones-de-ingeniería) del plano.

> **Estado en M0.** El proyecto `build/` (Bullseye + SimpleExec) y `tools/Clicalo.DevCli` existen, pero
> sus verbos se están implementando en este hito, y `cl.cmd`/`cl.ps1` todavía no están en la raíz. Cada
> tabla indica el equivalente con `dotnet` mientras tanto.

## Kit de desarrollo

- **SDK de .NET 10.0.401**, fijado en `global.json` con `rollForward: latestPatch` y sin versiones
  preliminares. El mismo archivo selecciona Microsoft Testing Platform como ejecutor de `dotnet test`.
- **C# 14.** Los proyectos puros usan `$(ClicaloPortableTfm)` (`net10.0`); los de Windows,
  `$(ClicaloWindowsTfm)` (`net10.0-windows10.0.19041.0`), por las proyecciones WinRT.
- La salida va a `artifacts/` (`UseArtifactsOutput`), nunca junto al código.

## Verbos de `cl`

`cl` es el único punto de entrada: una palabra por tarea, dictable, idéntica en local y en la CI. Su salida
termina en **una línea legible por Narrador**; los errores largos se escriben en un archivo Markdown que se
abre en VS Code. VS Code tiene una tarea por verbo.

| Verbo | Qué hace | Equivalente en M0 |
|---|---|---|
| `cl setup` | Prepara el equipo: restaura las herramientas locales y configura la firma SSH de los commits y el DCO | Ver [preparar el entorno](../guides/dev-setup.md) |
| `cl build` | Compila la solución completa | `dotnet build Clicalo.slnx -m:2 -nodeReuse:false` |
| `cl fast` | Compila y prueba solo el núcleo (`Core.slnf`: Domain, Application y Presentation, los generadores que usan, sus pruebas y TestKit), en menos de 45 s | `dotnet test --solution Core.slnf` |
| `cl test` | Ejecuta las pruebas | `dotnet test --solution Clicalo.slnx` |
| `cl desk` | Pruebas de integración de escritorio y E2E de humo (necesitan sesión interactiva) | — |
| `cl fix` | Aplica el formato de CSharpier y las correcciones automáticas | `dotnet dnx csharpier@1.3.0 --yes -- format <rutas>` |
| `cl check` | **Lo mismo que el trabajo `verify` de la CI:** *restore* bloqueado, compilación, formato, i18n, catálogos y nota de usuario. Todo PR termina con él | Compilación y pruebas completas, y formato de las rutas tocadas |
| `cl run` | Arranca la app con datos aislados en `%TEMP%\clicalo-dev` | — |
| `cl states` | Genera las instantáneas de todos los estados y abre la carpeta (sustituye a una galería de controles) | — |
| `cl accept` | Acompaña la aceptación en hardware táctil real (docs/09) | — |
| `cl trace` | Genera `docs/requirements/traceability.md` a partir del catálogo y de los resultados | — |
| `cl note` | Crea un fragmento de novedades para usuarios, en ES y EN, en `changes/unreleased/` | — |
| `cl pr` | Abre el PR de la rama actual | — |
| `cl beta` | Lanza la publicación beta (`beta.yml`) | — |
| `cl perf` | Mide los presupuestos de rendimiento | — |
| `cl sign-manifest` | Firma el manifiesto con la llave de hardware del mantenedor ([ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md)) | — |
| `cl i18n-import` | Conversión única, revisada a mano, de los textos del paquete ([ADR-0011](../adr/0011-formato-i18n.md)) | — |

`tools/Clicalo.DevCli` aloja las órdenes que no son de compilación: `i18n-check`, `trace`, notas,
`i18n-import`, `anonymize-v1`, `sign-manifest` y `states`.

### Compilar mientras se itera

```powershell
dotnet build src/Clicalo.Domain/Clicalo.Domain.csproj -m:2 -nodeReuse:false
```

`-m:2` limita los nodos de MSBuild en paralelo y `-nodeReuse:false` evita que queden procesos de MSBuild
vivos que bloqueen archivos entre compilaciones.

## Paquetes: Central Package Management

- **Las versiones solo viven en `Directory.Packages.props`.** Los proyectos usan `PackageReference` sin
  versión. Las versiones son exactas, sin rangos flotantes.
- `CentralPackageTransitivePinningEnabled` fija también las dependencias transitivas que se declaran ahí.
- Los analizadores comunes (Meziantou.Analyzer y Microsoft.CodeAnalysis.BannedApiAnalyzers) se declaran como
  `GlobalPackageReference`: llegan a todos los proyectos sin tocarlos.
- `NuGetAudit` está activo en modo `all` (también transitivas) y nivel `low`.
- `nuget.config` limpia las fuentes, usa solo nuget.org y declara `packageSourceMapping`. El plano prevé
  además `signatureValidationMode=require` con una lista explícita de `trustedSigners`, de modo que cada
  dependencia nueva añada su propietario.
- **Añadir una dependencia:** comprobar la última versión estable en
  `https://api.nuget.org/v3-flatcontainer/<id-en-minúsculas>/index.json`, añadir una línea
  `PackageVersion` con la versión exacta, referenciarla sin versión y actualizar los *lock files*. Cambiar
  `Directory.Packages.props` o `nuget.config` exige la revisión de un mantenedor (`CODEOWNERS`).
- Renovate mantendrá las versiones al día (agrupa mejor y gestiona `global.json`); las alertas de seguridad
  de Dependabot siguen activas.

## *Lock files*

- `RestorePackagesWithLockFile` genera un `packages.lock.json` por proyecto; se versionan con el cambio
  que los motiva.
- En la CI (`CI=true` o `GITHUB_ACTIONS=true`) se activa `RestoreLockedMode`: si el *lock file* no coincide,
  la restauración falla.
- Para regenerarlos tras cambiar una versión: `dotnet restore Clicalo.slnx --force-evaluate`.
- NuGet los escribe con CRLF en Windows; `.gitattributes` los mantiene en CRLF para evitar *diffs* falsos.

## Analizadores y reglas de compilación

Configuración común en `Directory.Build.props`: `TreatWarningsAsErrors`, `Nullable`, `ImplicitUsings`,
`AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, `Deterministic` y `Features=strict`. Toda
advertencia es un error.

| Analizador | Qué aporta |
|---|---|
| NetAnalyzers del SDK (`latest-recommended`) | Calidad, fiabilidad, rendimiento y globalización |
| Meziantou.Analyzer | Un tipo por archivo (MA0048), APIs sensibles a la cultura explícitas (MA0011, MA0076) y buenas prácticas |
| BannedApiAnalyzers | APIs prohibidas por capa (`architecture/BannedSymbols.<Capa>.txt`, [§4.4](blueprint.md#44-cómo-se-hacen-cumplir-las-reglas)) |
| Reglas de estilo del IDE (`.editorconfig`) | *Namespaces* de ámbito de archivo, sin `this.`, llaves obligatorias, modificadores de acceso, nombres (`_camelCase` en campos privados, `PascalCase` en constantes, prefijo `I` en interfaces) |
| `Clicalo.Analyzers` (`CLC*`) | Reglas de producto: en M0, CLC0001, CLC0003, CLC0004, CLC0006 y CLC0010 |

Convenciones que hacen cumplir: *namespaces* de ámbito de archivo, un tipo por archivo (MA0048), sin
`this.` y `Nullable` activado. `sealed` por defecto lo impone CA1852 en los tipos internos; en los tipos
públicos es una convención que se comprueba en revisión.

**Supresiones.** Nunca se debilita un analizador en un archivo compartido (`.editorconfig`,
`Directory.Build.props`). Una supresión local solo se admite con
`[SuppressMessage("Categoría", "Id", Justification = "motivo real")]` sobre el símbolo concreto. Las
relajaciones de `tests/.editorconfig` (nombres de prueba como frases, varios tipos por archivo en las
pruebas) están acotadas a `tests/`.

## Generadores de código

- Proyectos `generators/*` en `netstandard2.0`, como componentes de Roslyn con `EnforceExtendedAnalyzerRules`.
- Referencian **Roslyn 4.14** para cargarse en cualquier SDK de .NET 10 y en cualquier IDE compatible (ver
  [deviations.md](deviations.md)).
- Solo `IIncrementalGenerator`, con salida determinista.
- Utilidades comunes en `generators/Clicalo.Generators/Common/`: `MiniJson` (lector de JSON con línea y
  columna), `GeneratorContext.FilesIn`, `Profile` y `At`, y `Polyfills`.
- **Un error de datos es un diagnóstico** con identificador propio y ubicación exacta en el archivo JSON:
  el error aparece al compilar, en el archivo y la línea que hay que corregir.
- `Clicalo.Design.Math` (OKLCH → sRGB, gama y contraste WCAG) se enlaza como código dentro del generador,
  porque un generador no puede cargar referencias de proyecto en tiempo de ejecución.
- **Lo generado nunca se edita a mano**: se cambia el dato o el generador.

## Formato

- **CSharpier** da formato al código C#: determinista y sin opciones, así que se puede dictar sin cuidar la
  sangría. Versión 1.3.0:
  `dotnet dnx csharpier@1.3.0 --yes -- format <rutas>` y, para comprobar sin escribir,
  `dotnet dnx csharpier@1.3.0 --yes -- check <rutas>`.
- `.editorconfig` es dueño de los nombres y de las severidades; la regla de formato del IDE (IDE0055) está
  desactivada para no competir con CSharpier.
- Está previsto fijar CSharpier, `dotnet-cyclonedx` y `vpk` en `.config/dotnet-tools.json` y aplicar
  CSharpier al guardar en VS Code.
- Finales de línea: LF en todo salvo `*.cmd`, `*.bat` y los *lock files* (`.gitattributes`).

## Integración continua

Workflows previstos ([§10.5 del plano](blueprint.md#105-cicd)); `pr.yml` llega en M0:

| Workflow | Cuándo | Qué hace |
|---|---|---|
| `pr.yml` | Cada PR | Título en Conventional Commits, `verify` (= `cl check`), unitarias con cobertura, UI con instantáneas, humo de escritorio, CodeQL y DCO |
| `main.yml` | *Push* a `main` | Lo mismo, más un artefacto `dev` sin firmar y la trazabilidad |
| `lab.yml` | Bajo demanda, semanal y antes de cada publicación | E2E completo e integración en el equipo táctil, caos de Sentinel, puerta de rendimiento y VM de Windows 10 |
| `beta.yml`, `release.yml` → `release-core.yml` | `cl beta` o fusión del PR de release-please | Compila, firma el código y produce el manifiesto **sin firmar** |
| `release-publish.yml` | Manual, con el `.sig` de `cl sign-manifest` | Verifica la firma con las claves fijadas y publica |
| `patch-tuesday.yml` | Semanal | Parche de .NET → PR con la etiqueta `security` |
| `scorecard.yml`, `codeql.yml`, `docs.yml` | Semanal o al tocar `docs/**` | OpenSSF Scorecard, CodeQL completo, markdownlint, enlaces y Mermaid |

Reglas de la CI:

- Todas las acciones fijadas por SHA y con permisos mínimos por trabajo.
- Sin `pull_request_target`; los PR de *forks* se ejecutan sin secretos y con permisos de solo lectura.
- El equipo táctil de laboratorio nunca ejecuta código de *forks*.
- Mientras el repositorio sea privado, el trabajo ARM64, CodeQL y Scorecard están condicionados a que pase
  a público (ver [deviations.md](deviations.md)).
