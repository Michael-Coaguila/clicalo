# Herramientas: `cl`, paquetes, analizadores, formato y CI

Cómo se compila, se verifica y se publica Clícalo. Es una decisión reversible: se cambia con un PR normal
que actualiza esta página. El marco está en [§5](blueprint.md#5-estructura-del-repositorio-y-de-la-solución),
[§10.5](blueprint.md#105-cicd) y [§13](blueprint.md#13-convenciones-de-ingeniería) del plano.

> **Estado tras M0.** `cl.cmd` y `cl.ps1` están en la raíz y lanzan el orquestador `build/` (Bullseye +
> SimpleExec). Los verbos de M0 funcionan; los de hitos posteriores responden con el hito en el que llegan.

## Kit de desarrollo

- **SDK de .NET 10.0.401**, fijado en `global.json` con `rollForward: latestPatch` y sin versiones
  preliminares. El mismo archivo selecciona Microsoft Testing Platform como ejecutor de `dotnet test`.
- **C# 14.** Los proyectos puros usan `$(ClicaloPortableTfm)` (`net10.0`); los de Windows,
  `$(ClicaloWindowsTfm)` (`net10.0-windows10.0.19041.0`), por las proyecciones WinRT.
- La salida va a `artifacts/` (`UseArtifactsOutput`), nunca junto al código.

## Verbos de `cl`

`cl` es el único punto de entrada: una palabra por tarea, dictable, idéntica en local y en la CI. Desde la
raíz del repositorio se escribe `cl check` en `cmd` y `.\cl check` en PowerShell (PowerShell no ejecuta
programas de la carpeta actual sin `.\`; `cl.cmd` funciona aunque la directiva de ejecución sea
`Restricted`). VS Code tiene una tarea por verbo (`.vscode/tasks.json`).

| Verbo | Qué hace | Disponible |
|---|---|---|
| `cl setup` | Restaura las herramientas locales y ajusta git (`core.autocrlf=false`, `core.longpaths`, `pull.rebase`, `fetch.prune`, `push.autoSetupRemote`); instala el *hook* versionado `build/githooks/prepare-commit-msg`, que añade `Signed-off-by` (DCO), y activa la firma de commits solo si ya hay una clave SSH configurada. Nunca crea ni lee claves: si falta, escribe los pasos en `artifacts/cl/setup.md` | M0 |
| `cl build` | Compila la solución completa en Debug | M0 |
| `cl fast` | Compila y prueba solo el núcleo (`Core.slnf`: Domain, Application y Presentation, los generadores que usan, sus pruebas y TestKit). Objetivo: menos de 45 s; la línea final avisa si se supera | M0 |
| `cl test` | Compila y ejecuta todas las pruebas salvo las de escritorio (`Requires=Desktop`) | M0 |
| `cl desk` | Solo las pruebas de escritorio, con `CLICALO_DESKTOP_TESTS=1` (necesitan una sesión interactiva), un módulo de pruebas cada vez porque cada uno toma el primer plano con su InputProbe. Nunca ejecuta las mediciones (`Category=Perf`, son de `cl perf`). Fuera de la CI deja fuera las de `[Trait("Injects", "ReservedKeys")]`, que inyectan Ctrl derecho o AltGr (las capturan las herramientas de dictado y voz del mantenedor), y las de caos (`Category=Chaos`), que matan procesos con teclas pulsadas o congelan hilos | M0 |
| `cl fix` | Da formato al C# con CSharpier | M0 |
| `cl check` | **Lo mismo que el trabajo `verify` de la CI.** Todo PR termina con él (ver abajo) | M0 |
| `cl clean` | Vacía `artifacts/`, salvo la salida del propio orquestador, y dice qué archivos siguen en uso | M0 |
| `cl i18n-check [--strict-unused]` | Ejecuta `i18n-check` de `tools/Clicalo.DevCli` (ver abajo) | M0 |
| `cl i18n-import [--check]` | Ejecuta `i18n-import` de `tools/Clicalo.DevCli`: reconstruye `data/i18n` o, con `--check`, solo compara | M0 |
| `cl adr-check --base <ref>` | Ejecuta `adr-check` de `tools/Clicalo.DevCli`, lo mismo que el trabajo `adr` de la CI (`cl adr-check --base main` en local) | M0 |
| `cl pr` | Abre el PR de la rama actual | M1 |
| `cl run` | Arranca la compilación Debug de `Clicalo.exe` con datos aislados en `%TEMP%\clicalo-dev` y **sin envío de teclas** (`--no-input`) | M2 |
| `cl note` | Crea un fragmento de novedades para usuarios, en ES y EN, en `changes/unreleased/` | M2 |
| `cl perf` | Publica las variantes de S5 (`sc-r2r`, `sc-r2r-composite`, `fdd`, cada una con Sentinel) y ejecuta las mediciones `Category=Perf`; fuera de la CI, sin envío de teclas. Necesita la carga de trabajo «Desarrollo para el escritorio con C++» de Visual Studio (el enlazador de Native AOT de Sentinel) | M2 |
| `cl states` | Genera las instantáneas de todos los estados y abre la carpeta (sustituye a una galería de controles) | M3 |
| `cl accept` | Acompaña la aceptación en hardware táctil real (docs/09) | M3 |
| `cl trace` | Genera `docs/requirements/traceability.md` a partir del catálogo y de los resultados | M3 |
| `cl beta` | Lanza la publicación beta (`beta.yml`) | M5 |
| `cl sign-manifest` | Firma el manifiesto con la llave de hardware del mantenedor ([ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md)) | M5 |

### Qué hace `cl check`

Los mismos pasos, en este orden, en local y en la CI; el primero que falla detiene la orden:

1. **pins**: versiones exactas en `Directory.Packages.props`, `global.json` y `.config/dotnet-tools.json`
   (NFR-014) y acciones de GitHub fijadas por SHA con su versión en un comentario.
2. **tools**: `dotnet tool restore`.
3. **format**: `dotnet csharpier check .`
4. **restore**: `dotnet restore --locked-mode` (los *lock files* deben coincidir).
5. **build**: compilación Release con `-warnaserror`.
6. **test**: todas las pruebas salvo `Requires=Desktop`, con resultados TRX: un `<Ensamblado>.trx` por módulo de
   pruebas (`-p:ClicaloTrxReport=true`, que lee `Directory.Build.targets`), para que ningún módulo sobrescriba los
   resultados de otro.
7. **i18n**: `Clicalo.DevCli i18n-check` y `Clicalo.DevCli i18n-import --check`.

### Línea final e informe de errores

Cada orden termina en **una sola línea para Narrador**, por ejemplo «cl check: correcto en 1 min 22 s;
1233 pruebas» o «cl check: falló en format; detalle en artifacts\cl\last-error.md». El número de pruebas es el
`total` que da `dotnet test` en su resumen (las omitidas cuentan y se dicen aparte: «65 pruebas, 2 omitidas»). El informe
`artifacts/cl/last-error.md` es Markdown con encabezados y listas (sin tablas ni colores) y recoge el error
exacto: errores de MSBuild con enlace a la línea, pruebas fallidas con mensaje y pila (leídas del TRX),
archivos sin formato o firmas NuGet rechazadas (NU3034). Se borra al empezar cada orden, así que nunca queda
un informe antiguo. En la terminal de VS Code se abre solo al fallar; en GitHub Actions el paso añade grupos,
una anotación `::error` y el resumen del trabajo.

Variables de entorno:

| Variable | Efecto |
|---|---|
| `CLICALO_MAXCPU` | Limita los nodos de MSBuild en paralelo (`-m`) en un equipo compartido |
| `CLICALO_OPEN_ERRORS=0` | No abre `last-error.md` al fallar |
| `CLICALO_DESKTOP_TESTS=1` | Activa las pruebas `Requires=Desktop`; `cl desk` la pone sola |
| `CLICALO_ACCEPT_SNAPSHOTS=1` | Acepta instantáneas nuevas de TestKit en local; en la CI se rechaza |

Cada paso usa `-nodeReuse:false`: ningún nodo de MSBuild sobrevive a una orden, así que no quedan archivos
bloqueados para `cl clean`.

### `tools/Clicalo.DevCli`

Aloja las órdenes que no son de compilación. `cl` las expone con el mismo nombre: todo lo que se escribe
después del verbo pasa tal cual a la herramienta (`cl i18n-import --check` equivale a
`dotnet run --project tools/Clicalo.DevCli -- i18n-import --check`), y la orden termina con la línea final
y el informe de errores de `cl`:

| Verbo | Qué hace |
|---|---|
| `i18n-check` | Valida `data/i18n` igual que el generador (errores `CLCI`), las reglas CLDR y `allow-unused.txt` |
| `i18n-import` | Reconstruye `data/i18n/strings.*.json` desde el paquete de diseño con la receta revisada ([ADR-0011](../adr/0011-formato-i18n.md)); con `--check` no escribe y falla si no coincide |
| `adr-check --base <ref>` | Falla si los archivos cambiados desde la base de fusión con `<ref>` tocan una ruta de `architecture/sensitive-paths.json` sin un ADR nuevo o cambiado en `docs/adr/` (error `CLCA010`, [§13](blueprint.md#13-convenciones-de-ingeniería)) |

Salida en formato MSBuild, última línea legible por Narrador y códigos de salida 0 (sin problemas), 1
(problemas) y 2 (uso incorrecto, con la ayuda). Más adelante llegarán `trace`, `anonymize-v1` y `states`.

### Opciones de `Clicalo.exe` para desarrollo

`Clicalo.exe` no tiene opciones en un arranque normal. Para desarrollar y medir:

- `--no-input`: ningún envío (inyector en seco, *ledger* desconectado, sin Sentinel ni soltado preventivo). Es lo que
  usan `cl run` y, fuera de la CI, `cl perf`.
- `--data <carpeta>`: todos los datos, el diario de fallos incluido, dentro de esa carpeta.
- `--migrate-v1 <archivo>`: en un primer arranque (sin `clicalo.json`), convierte ese `profiles.json` o zip de Macro
  Quick Access; el original se guarda antes en `backups\`.
- `--exit-after <segundos>`: tras el primer frame, recorre la salida completa sin intervención (diagnóstico).
- `--guardian after-first-frame`: la variante de S5 que lanza Sentinel después del primer frame.
- `--after-crash=<ms Unix>` y `--safe-mode` los pone Sentinel al relanzar ([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md)).

La orden `anonymize-v1 --in <archivo> --out <archivo>` de `tools/Clicalo.DevCli` anonimiza un `profiles.json` real para
versionarlo como *fixture*: conserva combinaciones, colores, recuentos y estructura, y sustituye todo nombre que no esté
en la lista revisada de `tools/Clicalo.DevCli/AnonymizeV1/v1-public-names.json`.

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
- `nuget.config` limpia las fuentes, usa solo nuget.org, sin carpetas de reserva, declara
  `packageSourceMapping` y exige firma (`signatureValidationMode=require`) con una lista explícita de
  `trustedSigners`: el repositorio nuget.org (sus certificados publicados) y, dentro, los propietarios
  (`<owners>`) de cada paquete que usa el repositorio, sus transitivos, los *packs* del SDK y las
  herramientas.
- **Añadir un propietario** cuando una dependencia nueva falla con NU3034: restaura con cachés vacías
  (`$env:NUGET_PACKAGES` y `$env:NUGET_HTTP_CACHE_PATH` a carpetas temporales; después
  `dotnet restore Clicalo.slnx` y `dotnet tool restore`), mira el propietario con
  `dotnet nuget verify --all <ruta del .nupkg> -v detailed` (línea «Owners») y añádelo en orden alfabético
  a `<owners>`. La validación solo ocurre al extraer un paquete: la caché local puede ocultar un NU3034 que
  la CI sí ve, por eso se prueba con cachés vacías.
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
- **Runtimes publicados.** `Directory.Build.props` fija los dos que se distribuyen
  (`ClicaloRuntimeIdentifiers` = `win-x64;win-arm64`, [§11 del plano](blueprint.md)). Los ejecutables que se
  publican (`Clicalo.App` y `Clicalo.Sentinel`) los declaran como `RuntimeIdentifiers`, así que sus *lock files*
  guardan el grafo de los dos y no dependen del equipo que restauró (con `PublishAot`, el SDK añadía solo el
  runtime del equipo y una restauración bloqueada en ARM64 fallaba con NU1004).
- **Publicar con la restauración bloqueada.** El runtime se elige con
  `-p:ClicaloRuntimeIdentifier=win-x64` (o `win-arm64`), que solo los ejecutables convierten en su
  `RuntimeIdentifier`; **nunca con `-r`**. `-r` es una propiedad global que llega a la restauración de todos los
  proyectos referenciados (bibliotecas, generadores y analizadores, cuyos *lock files* no tienen grafo por
  runtime): en la CI falla con NU1004 y fuera de ella reescribe sus *lock files*. Desactivar los *lock files* en la
  línea de órdenes tampoco sirve: NuGet lo rechaza con NU1005 mientras existan. Así publica `cl perf`
  (`BuildSteps.PublishArguments`), y así debe publicar el empaquetado de M5 (Velopack con `vpk pack`, R2R
  autocontenido por runtime): `dotnet publish src/Clicalo.App/Clicalo.App.csproj -c Release
  -p:ClicaloRuntimeIdentifier=<rid> --self-contained true -p:PublishReadyToRun=true`, con la restauración
  bloqueada, ya comprobado para `win-x64` y `win-arm64`.
- **Pendiente para M5:** `Clicalo.Launcher` también usa `PublishAot` y su *lock file* solo tiene el grafo
  `win-x64`; necesita las mismas dos propiedades que Sentinel antes de publicarse para `win-arm64` o de activar
  `verify (arm64)`. `src/Clicalo.Launcher/**` es una ruta sensible (`architecture/sensitive-paths.json`), así que
  el cambio va con su ADR.

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
relajaciones de los proyectos de prueba (nombres de prueba como frases, varios tipos por archivo en las
pruebas) están acotadas a ellos: `tests/.editorconfig` y su copia `build/Build.Tests/.editorconfig`, que
`TestEditorConfigTests` obliga a mantener idéntica.

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

- **CSharpier 1.3.0** (herramienta local en `.config/dotnet-tools.json`) da formato al código C#:
  determinista, así que se puede dictar sin cuidar la sangría. `cl fix` da formato y `cl check` lo
  comprueba (`dotnet csharpier format .` y `dotnet csharpier check .`).
- `.csharpierrc.json` fija `printWidth` en **100**, el valor por defecto de CSharpier: el código formateado
  sin configuración ya cumple, y 100 columnas obligan a desplazarse menos en horizontal con lupa o letra
  grande.
- **Solo se formatea C#.** `.csharpierignore` excluye `csproj`, `props`, `targets`, `slnx`, `config`, `xml`
  y `xaml`: el formateador XML parte los elementos de una línea y se lee peor en voz alta. Qué hacer con el
  XAML se decidirá con el primer XAML.
- `.editorconfig` es dueño de los nombres y de las severidades; la regla de formato del IDE (IDE0055) está
  desactivada para no competir con CSharpier.
- `.config/dotnet-tools.json` fija CSharpier, `dotnet-CycloneDX` y `vpk`. VS Code aplica CSharpier al
  guardar solo en C# (`.vscode/settings.json`).
- Finales de línea: LF en todo salvo `*.cmd`, `*.bat` y los *lock files* (`.gitattributes`).

## Integración continua

Workflows previstos ([§10.5 del plano](blueprint.md#105-cicd)). En M0 existen `pr.yml` (trabajos
`verify (x64)` = `cl check`, `desk (x64)` = `cl desk` (pruebas de escritorio en el *runner* alojado, validado por
[S0](../testing/spikes/S0.md)), `adr` = `adr-check` y `dco` (cada commit del PR con `Signed-off-by` de su autor,
[ADR-0015](../adr/0015-licencia-mit-y-dco.md)) en cada PR; `verify (arm64)`, CodeQL y Scorecard solo con el
repositorio público), `pr-title.yml` (título en Conventional Commits) y `s0.yml` (spike S0: `cl desk` diez
veces en `windows-2025` y, con el repositorio público o bajo petición, en `windows-11-arm`; se lanza a mano y
su resultado va a [S0.md](../testing/spikes/S0.md)):

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
