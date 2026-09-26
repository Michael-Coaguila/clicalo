# Cumplimiento automático de la arquitectura

Este documento explica cómo se hacen cumplir las reglas de arquitectura del plano (`blueprint.md` §4.1–§4.4 y §13), qué falla cuando alguien las rompe y cómo se añade una excepción justificada. Es una decisión reversible: se cambia con un PR normal. Las reglas que protege no: las fija el plano.

**Principio.** Una persona y un agente reciben el mismo error, en el mismo momento y con el mismo texto. Nada depende de la disciplina ni de la memoria de nadie: si una regla no puede comprobarse en la compilación o en las pruebas, no es una regla, es un deseo.

## Resumen: los seis mecanismos

| # | Mecanismo | Dónde vive | Cuándo falla | Qué se ve |
|---|---|---|---|---|
| 1 | Lista blanca de referencias | `architecture/allowed-dependencies.json` + target `ClicaloVerifyReferences` (`Directory.Build.targets`) | Al compilar, **antes** de resolver referencias y de compilar | `CLCA001`–`CLCA003` en la línea del `.csproj` que declara la referencia |
| 2 | Reglas ArchUnitNET | `tests/Clicalo.Architecture.Tests` | Al ejecutar las pruebas (cada PR) | Prueba en rojo con cada tipo infractor y cada dependencia prohibida |
| 3 | APIs prohibidas | `architecture/BannedSymbols.{All,Domain,Application,Presentation,Surfaces}.txt` | Al compilar un proyecto de `src/` | `RS0030` en la línea que usa la API |
| 4 | Analizadores propios `CLC*` | `generators/Clicalo.Analyzers` (paquete propio) | Al compilar | `CLC0001`… |
| 5 | Facetas de `ActionKind` | `tests/Clicalo.Architecture.Tests` (llega con los tipos, M2–M4) | Al ejecutar las pruebas | Prueba en rojo |
| 6 | Reglas de producto R4, R5 y R7 | `architecture/destructive-operations.json`, `undo-exemptions.json` + pruebas | Al ejecutar las pruebas | Prueba en rojo |

Además, `architecture/domain-modules.json` fija la matriz de módulos de Domain (§4.3) y `architecture/sensitive-paths.json` fija las rutas que exigen un ADR (§13). Cada archivo JSON de `architecture/` declara su JSON Schema en `architecture/schemas/`, que el editor usa para autocompletar y que las pruebas validan.

**Cómo ejecutarlo:**

```
dotnet build Clicalo.slnx                      # mecanismos 1, 3 y 4
dotnet test --solution Clicalo.slnx            # mecanismos 2, 5 y 6, y las pruebas de los mecanismos 1 y 3
dotnet test --project tests/Clicalo.Architecture.Tests/Clicalo.Architecture.Tests.csproj --filter-not-trait "Category=Slow"
```

La última orden omite las pruebas lentas (las que compilan proyectos generados); tarda unos segundos y sirve para iterar. La CI ejecuta siempre todas.

---

## 1. Lista blanca de referencias

### Qué comprueba

Para cada proyecto, `allowed-dependencies.json` declara:

- `area`: la carpeta de primer nivel (`src`, `generators`, `tests`, `tools`, `build`).
- `rule`: por qué puede depender exactamente de eso. Aparece en el mensaje de error.
- `platform`: `portable` (TFM sin sistema operativo, como `net10.0` o `netstandard2.0`) o `windows`. Así se cumple la columna TFM de §4.2: Domain, Application y Presentation no pueden pasar a un TFM de Windows sin que la compilación falle.
- `projectReferences`, `packageReferences` y, si hacen falta, `frameworkReferences` (`UseWPF` cuenta como `Microsoft.WindowsDesktop.App.WPF`) y `assemblyReferences` (desaconsejadas).

Hay dos listas globales:

- `analyzerProjects`: `Clicalo.Analyzers` y `Clicalo.Generators` se pueden referenciar desde cualquier proyecto **solo** como componentes del compilador (`OutputItemType="Analyzer"` y `ReferenceOutputAssembly="false"`), porque no añaden ninguna dependencia en ejecución. Referenciar su ensamblado exige una entrada normal.
- `globalPackages`: los `GlobalPackageReference` de `Directory.Packages.props`.

### Cómo funciona

- El target `ClicaloVerifyReferences` se ejecuta con `BeforeTargets="ResolveProjectReferences"`, así que falla antes de compilar el proyecto y antes de compilar sus dependencias.
- Solo comprueba las referencias **declaradas por archivos del repositorio** (el `.csproj`, `Directory.*.props/targets`, el cableado de analizadores). Las transitivas y las implícitas del SDK (`NETStandard.Library`, `Microsoft.NET.ILLink.Tasks`…) se derivan de ellas y no se listan.
- La lógica es la tarea MSBuild `architecture/tasks/ClicaloVerifyReferences.cs`, que `RoslynCodeTaskFactory` compila bajo demanda. Usa solo la superficie de `netstandard2.0`, así que funciona igual en la CLI de .NET y en el MSBuild de Visual Studio. El mismo archivo se compila también dentro de `Clicalo.Architecture.Tests` (con `CLICALO_REFERENCE_POLICY_TESTS`), donde se prueba con los analizadores del repositorio.
- **Compilaciones incrementales.** El target calcula una huella (proyecto, TFM, `UseWPF`, referencias declaradas, contenido de la lista blanca y de la tarea) y la guarda en `obj/…/clicalo.references.stamp`. Si la huella no cambia, la tarea ni siquiera se compila ni se ejecuta: el coste en una compilación sin cambios es de unos milisegundos.
- **IDE.** Las compilaciones de diseño (`DesignTimeBuild`) lo omiten para no molestar a IntelliSense. Cualquier compilación real desde Visual Studio, Rider o VS Code lo ejecuta, y el error aparece en la lista de errores con archivo, línea y columna: un doble clic lleva a la línea del `.csproj` que declara la referencia. La lista blanca y la tarea son entradas de la comprobación rápida de Visual Studio (`UpToDateCheckInput`), así que cambiarlas vuelve a compilar.
- **Proyecto temporal de WPF.** Cuando el XAML de un proyecto usa tipos del propio proyecto, la compilación de marcado de WPF crea una copia temporal `<Proyecto>_<aleatorio>_wpftmp`. Es el mismo proyecto: no repite la comprobación (el proyecto real ya resolvió y comprobó las mismas referencias) y recibe las listas de APIs prohibidas por su nombre real (`ClicaloProjectName`). Una prueba lenta lo cubre.

### Códigos

| Código | Significado |
|---|---|
| `CLCA000` | `allowed-dependencies.json` no es JSON válido o no tiene la forma esperada (con línea y columna) |
| `CLCA001` | Una referencia declarada no está permitida. El mensaje dice qué referencia, en qué proyecto, qué permite la lista y la regla del proyecto |
| `CLCA002` | El proyecto no está declarado en la lista blanca: todo proyecto nuevo se declara |
| `CLCA003` | El proyecto usa una plataforma que su capa no permite (por ejemplo, Presentation con un TFM de Windows) |

Ejemplo real:

```
src/Clicalo.Domain/Clicalo.Domain.csproj(9,31): error CLCA001: Package reference 'Serilog' is not allowed in
'Clicalo.Domain'. architecture/allowed-dependencies.json allows package references: (none). Rule for Clicalo.Domain:
Pure domain: model, invariants, commands and EngineReducer. BCL only (...) If the dependency is intended, change the
whitelist with a justification (docs/architecture/enforcement.md).
```

### Cómo añadir un proyecto o una dependencia

1. Comprueba que la dependencia respeta §4.2. Si cambia una capa, un límite de confianza o un contrato, primero hace falta un ADR (§13).
2. Si es un paquete, añade su versión exacta en `Directory.Packages.props` y su propietario en `nuget.config` (`trustedSigners`).
3. Añade el nombre a la lista del proyecto en `allowed-dependencies.json` y, si hace falta, actualiza su `rule` para que siga explicando por qué.
4. Para un proyecto nuevo, añade su entrada completa y añádelo a `Clicalo.slnx`. Si es un proyecto de `src/`, añade también su ensamblado en `tests/Clicalo.Architecture.Tests/Support/Product.cs`.
5. Explica la justificación en la descripción del PR. `architecture/**` tiene revisión obligatoria (CODEOWNERS).

`AllowedDependenciesTests` comprueba que la lista sigue siendo fiel al repositorio: todos los proyectos de la solución declarados en su área, todos los nombres existentes, todos los paquetes versionados de forma central, el grafo de producto acíclico, que cada proyecto declara lo que le llega de forma transitiva y que solo `Clicalo.Architecture.Tests` referencia `Clicalo.App`.

---

## 2. Reglas ArchUnitNET

`tests/Clicalo.Architecture.Tests` carga los diez ensamblados de `src/` con ArchUnitNET (`Support/Product.cs`) y comprueba, sobre el IL compilado, las reglas de §4.4 punto 2. Las reglas están configuradas en un único archivo, `ProductRules.cs`, a partir de unas pocas formas reutilizables (`Support/DependencyRules.cs`, `ModuleMatrix.cs` y `ConfinedApis.cs`):

| Regla del plano | Prueba |
|---|---|
| Capas de §4.2 (derivadas de `allowed-dependencies.json`), Domain solo BCL y sin E/S, Application sin WPF | `LayerRulesTests` |
| Presentation no usa `System.Windows.*` (salvo `System.Windows.Input.ICommand`, ver decisiones) | `LayerRulesTests` |
| UI.Wpf usa de Application solo los puertos; CsWin32 solo en `Windowing/` y `Pointer/`; Infrastructure y Launcher solo usan `Platform.Core.Trust`; nada depende de App | `LayerRulesTests` |
| Los puertos viven en `Application.Ports` (los adaptadores solo implementan interfaces de ahí) | `ConfinementRulesTests` |
| Solo `Application.Engine`, `Platform.Windows` y App usan `IInputInjector` | `ConfinementRulesTests` |
| Solo `Application.Foreground` usa `IForegroundControl` (más su adaptador y App) | `ConfinementRulesTests` |
| Solo Execution y el editor del CC llaman a `SecretText.WithRevealed` (más el motor y los mappers de persistencia) | `ConfinementRulesTests` |
| Solo el rol Surfaces escribe en `SessionStore` e `InteractionStore` | `ConfinementRulesTests` |
| `Application.Ipc` no depende de Engine ni de Foreground, y de Application solo usa `IShellNavigator` | `ConfinementRulesTests` |
| Ningún módulo usa el `.Internal` de otro | `InternalNamespaceTests` |
| Matriz de módulos de §4.3 (`domain-modules.json`) | `ModuleMatrixTests` |
| Tabla de APIs prohibidas, comprobada en el IL | `ConfinedApiTests` |
| R4 y R7: registros cerrados | `ProductRuleTests` |

### Reglas sobre tipos que todavía no existen

Casi todos los tipos que protegen estas reglas (`IInputInjector`, `SessionStore`, `SecretText`, los módulos de Domain…) llegan entre M2 y M4. Las reglas ya están escritas y activas: en cuanto aparezca el primer tipo, se aplican.

ArchUnitNET 0.13 hace fallar por defecto una regla cuyo conjunto de tipos está vacío. Aquí eso sería un falso rojo, así que todas las reglas usan `WithoutRequiringPositiveResults()` (en `Support/Rule.cs`). Para que un verde vacío no sea engañoso hay dos guardas:

- `ArchitectureLoadingTests` comprueba que los diez ensamblados de producto están cargados, que coinciden con los proyectos de `src/` de la solución y que los tipos son visibles para las reglas. Si una regla pasa, no es porque no se haya cargado nada.
- **Cada forma de regla tiene una prueba negativa** que la hace fallar sobre tipos infractores escritos a propósito en `tests/Clicalo.Architecture.Tests/Fixtures/`. Las reglas reciben un `Scope`: con `Scope.Product` miran el producto, y con `Scope.Fixture("Confinement")` miran los mismos nombres reflejados bajo `Clicalo.Architecture.Tests.Fixtures.Confinement.*`. Es exactamente la misma regla, no una copia. Los fixtures nunca los ven las reglas reales, porque estas solo cargan los ensamblados de `src/`.

La prueba negativa obligatoria del hito M0 («ArchUnit falla ante una referencia prohibida») es `LayerRulesTests.A_forbidden_layer_dependency_fails_the_rule`: un tipo de «Domain» que usa un tipo de «Infrastructure» dentro de un método hace fallar la regla de capas.

### Cómo añadir o cambiar una regla

1. Configúrala en `ProductRules.cs` con una forma existente (o añade una forma en `Support/` si de verdad es nueva).
2. Añade su prueba positiva (sobre `Product.Architecture`) y su prueba negativa (sobre fixtures).
3. Si verifica un requisito del catálogo, márcala con `[Trait("Req", "<ID>")]`.

---

## 3. APIs prohibidas

### Listas y cableado

| Lista | Se aplica a | Contenido |
|---|---|---|
| `BannedSymbols.All.txt` | Todos los proyectos de `src/` | Reloj, temporizadores, ids y aleatoriedad fuera de `TimeProvider` e `IIdGenerator`; esperas bloqueantes; `Process.Start`; escrituras de archivos; salida del proceso; `Console`; primer plano (`SetForegroundWindow`…, `Window.Activate`, `UIElement.Focus`); `TrackPopupMenu(Ex)`; `SendInput`; `ShellExecute*`, `IShellDispatch2` y WMI |
| `BannedSymbols.Domain.txt` | `Clicalo.Domain` | `System.IO`, `System.Net`, `Microsoft.Win32`, `Environment`, `Process`, interop y `AppContext` |
| `BannedSymbols.Application.txt` | `Clicalo.Application` | Archivos, directorios, tuberías, red, registro, procesos, variables de entorno, carpetas especiales y `System.Windows` |
| `BannedSymbols.Presentation.txt` | `Clicalo.Presentation` | Todo WPF y Windows Forms (salvo `ICommand`), el `Dispatcher`, diálogos y acceso al sistema operativo |
| `BannedSymbols.Surfaces.txt` | `Clicalo.UI.Wpf` | `Popup`, `ContextMenu`, `ToolTip`, `ComboBox` y `MessageBox` (REG-01, §3.5) |

- Los proyectos de producto son los de `src/`. `Directory.Build.targets` añade `BannedSymbols.All.txt` a todos ellos y la lista de su capa según el nombre del proyecto. Las pruebas, las herramientas y los generadores no reciben listas.
- Formato de Microsoft.CodeAnalysis.BannedApiAnalyzers: `<id de documentación>;<motivo y alternativa>`. Un id `M:` sin lista de parámetros solo cubre la sobrecarga sin parámetros, así que **cada sobrecarga está listada**. Un id `N:` cubre también los espacios de nombres hijos.
- El código generado (sobrecargas amigables de CsWin32, `.g.cs` de XAML, generadores propios) queda fuera gracias a `BannedSymbols.globalconfig` (`dotnet_banned_api_analyzer.exclude_generated_code = true`). Se añade como `EditorConfigFiles` y no como `GlobalAnalyzerConfigFiles`, porque el SDK convierte estos últimos antes de importar `Directory.Build.targets` y se perderían.

### Pruebas de las listas

- `BannedSymbolsTests` resuelve cada entrada con Roslyn, igual que el analizador. Una errata en un id no prohibiría nada en silencio: aquí falla. También comprueba que cada familia prohibida (por ejemplo, todas las sobrecargas de `File.Write*`, `Task.Delay` sin `TimeProvider` o los `GetResult` de todos los *awaiters*) está completa, así que una sobrecarga nueva de una versión de .NET futura hace fallar la prueba.
- `BannedSymbolsBuildTests` (lenta) compila proyectos de sonda con el nombre de cada capa (Domain, Application, Presentation y UI.Wpf) y comprueba que cada línea marcada da `RS0030` y ninguna otra, así que el cableado por nombre de proyecto queda probado en una compilación real. Los ids de CsWin32 se generan en cada proyecto y no se pueden resolver con Roslyn: la sonda de UI.Wpf usa CsWin32 y WPF, y comprueba cada sobrecarga prohibida en su línea exacta y que el código generado no da ninguno.

### Excepciones por archivo (adaptadores)

La tabla de §4.4 permite algunas APIs en archivos concretos: `SetForegroundWindow` en `ForegroundControl.cs`, `SendInput` en `Platform.Core/Injection`, etc. BannedApiAnalyzers no admite listas por carpeta, así que el mecanismo es este:

1. **La excepción se registra** en `architecture/banned-api-exceptions.json`: rutas (globs bajo `src/`), APIs, justificación y sección del plano. Ya están registradas todas las de la tabla, más la elevación (ver decisiones) y las ventanas de Workspace para la lista Surfaces.
2. **En el archivo**, la llamada se suprime en el ámbito más pequeño posible con un atributo justificado:

   ```csharp
   [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
       Justification = "The single adapter behind IForegroundControl (banned-api-exceptions.json: foreground-control).")]
   private static bool SetForeground(HWND window) => PInvoke.SetForegroundWindow(window);
   ```

   Se admite también `#pragma warning disable RS0030 // <justificación>` si se cierra con `#pragma warning restore RS0030` en el mismo archivo.
3. **`BannedApiExceptionsTests` lo comprueba** sobre el árbol sintáctico de cada archivo de `src/` (da igual que CSharpier parta el atributo en varias líneas o que el nombre esté cualificado): cualquier supresión de `RS0030` fuera de un archivo registrado, sin justificación, a nivel de ensamblado o de módulo, con un *pragma* que no se restaura o con un `#pragma warning disable` sin ids (que desactiva todas las advertencias) hace fallar la prueba. También falla si un `.csproj`, `.props`, `.targets`, `.editorconfig` o `.globalconfig` baja la severidad de `RS0030` o de la categoría `ApiDesign`, o lo pone en `NoWarn` o `WarningsNotAsErrors`.
4. **`ConfinedApiTests` cierra el hueco que deja la granularidad por archivo:** sobre el IL compilado, cada API de la tabla solo puede usarse desde el espacio de nombres o el tipo que la tabla permite, sea cual sea la sobrecarga, la supresión o el código generado. Así, un archivo registrado para `SetForegroundWindow` no puede usar además `AttachThreadInput`. La única API de la tabla que no se confina en el IL es `GetAwaiter().GetResult()`: el compilador la emite en cada `await`, así que solo la prohíbe la compilación.

Para añadir una excepción justificada: primero, confirma que el plano la permite (si no, hace falta cambiar el plano con su ADR). Después registra la ruta en `banned-api-exceptions.json`, añade o amplía la zona permitida en `Support/ConfinedApis.cs`, escribe la supresión justificada en el archivo y explica el motivo en el PR.

---

## 4. Analizadores propios

`Directory.Build.targets` importa `generators/Clicalo.Analyzers/Clicalo.Analyzers.Wiring.targets` solo si existe. Ese archivo lo mantiene el paquete de los analizadores `CLC*` y decide a qué proyectos se aplican. Una referencia a `Clicalo.Analyzers` como componente del compilador ya está permitida en cualquier proyecto por la lista blanca (`analyzerProjects`).

## 5. Facetas de `ActionKind`

La prueba de facetas (§4.4 punto 5) recorre los subtipos de `ShortcutAction` y exige sus registros en todas las capas. Llega con esos tipos (M2–M4) y vivirá en este mismo proyecto de pruebas.

## 6. Reglas de producto

- **R4 (REG-04):** `destructive-operations.json` es la lista cerrada de comandos destructivos y de casos de uso `[Destructive]`. Desde M0, `ProductRuleTests` exige que ningún tipo implemente `IDestructiveCommand` ni lleve `[Destructive]` sin estar en la lista, y que un tipo listado que exista los lleve. Un caso de uso puede llamarse `X` o `XUseCase`. La exigencia de que **todos** existan y la comprobación con CsCheck de que ningún comando no listado elimina entidades llegan con `DocumentStore` (M4, criterio de salida del hito).
- **R7 (REG-07):** `undo-exemptions.json` lista los comandos exentos de `UndoIntent.Record`, cada uno con su justificación (`SetSetting` solo cuando el descriptor tiene `Undoable = false`). Desde M0 se exige que cada exención nombre un comando de documento. La prueba de comportamiento (aplicar cada comando a documentos generados) llega en M4.
- **R5 (REG-05):** es la regla UIA010 de las pruebas de accesibilidad (§10.2), no una regla de este proyecto.

## Registros de apoyo

- **`domain-modules.json`.** Matriz de módulos de Domain. Un módulo es `Clicalo.Domain.<Módulo>` y todo lo que cuelga de él, y puede usar los módulos que alcanza por `dependsOn` (la relación es transitiva, como indica la tabla de §4.3). La matriz debe ser acíclica, y todo tipo de Domain debe estar en un módulo declarado. Las filas que difieren de la tabla de §4.3 llevan `deviation` con el motivo, y `ModuleMatrixTests` compara fila a fila con esa tabla: una diferencia sin `deviation`, o un `deviation` sin diferencia, hace fallar la prueba.
- **`sensitive-paths.json`.** Globs de rutas cuyo cambio exige un ADR (§13): límites de confianza, formatos persistidos, contratos públicos, modelo de procesos, framework, licencia y firma. La comprobación de ADR de la CI de PR (`pr.yml`) lo leerá. La propia lista es sensible: quitar una ruta también exige un ADR.

---

## Decisiones y desviaciones respecto al plano

| Tema | Decisión | Motivo |
|---|---|---|
| Módulo `Document` | Se añade a la matriz (Library, Settings, Frequents, Duplicates, Errors, Messages, Catalog y Primitives) | `UserDocument` agrega Library, Frequents, Duplicates y Settings (§6.3). Ningún módulo de §4.3 puede contenerlo sin crear un ciclo |
| Aristas nuevas en la matriz | `Errors → Messages`, `Library → Errors`, `Settings → Messages` | `Error` lleva `MessageKey` (§6.1), las operaciones de `Library` devuelven `Result<Library>` (§6.2) y `SettingDescriptor` lleva claves de texto (§6.3) |
| Módulo `Timing` | Se añade a los fundamentos, sin dependencias, con aristas desde `Touch`, `Dimming` y `Execution` | Todo umbral vive en `timings.json` y se genera como constantes en `Clicalo.Domain.Timing` (§13, NFR-020). Los gestos (§7.8), el atenuado (§6.4) y el intervalo de inyección (§7.7) los usan. Otra arista hacia `Timing` se declara cuando su módulo la necesite |
| Matriz transitiva | Un módulo puede usar lo que alcanza por `dependsOn` | La tabla de §4.3 omite `Primitives` en módulos que usan `ProfileId` o `ShortcutId`: solo tiene sentido transitiva |
| `ICommand` en Presentation | Permitido | `System.Windows.Input.ICommand` vive en la BCL (`System.ObjectModel`) y es el contrato que implementa CommunityToolkit.Mvvm |
| `SecretText.WithRevealed` | Además de Execution y el editor del CC (`Presentation.ControlCenter.Editor`), lo pueden llamar `Application.Engine` y `Infrastructure.Persistence` | §6.7: el motor copia el texto para enviarlo y el mapper lo necesita en claro para cifrarlo con DPAPI |
| `IForegroundControl` | Además de `Application.Foreground`, lo usan su adaptador (`Platform.Windows.Foreground`) y App (registro en DI) | Implementar o registrar un puerto es depender de él |
| Puertos | Viven en `Application.Ports`, también `IForegroundControl` e `ISurfaceActivationStyle` | §4.4 es la norma de cumplimiento; el bloque de código de §3.6 los muestra en `Application.Foreground` |
| `ShellExecuteEx` en `Platform.Windows/Elevation` | Excepción registrada | §3.3 (regla 2) relanza elevado con `ShellExecuteEx("runas")`, aunque la tabla de §4.4 no lo mencione |
| `UIElement.Focus` | Prohibido en todo `src` | La tabla prohíbe `Window.Focus`, pero `Focus` se hereda de `UIElement`. Enfocar un elemento activa su ventana, y la alternativa (`Keyboard.Focus` bajo una concesión) existe |
| Reloj y aleatoriedad | Además de la tabla, se prohíben `DateTime.Today`, `Environment.TickCount`, `Stopwatch` sin `TimeProvider`, `Task.WaitAsync(TimeSpan)`, `CancellationTokenSource(TimeSpan)`, `PeriodicTimer(TimeSpan)`, `System.Threading.Timer`, `System.Timers.Timer`, `Guid.CreateVersion7` y `new Random()` sin semilla | §13: «`TimeProvider` en todo lo que dependa del tiempo». Son las mismas fuentes no deterministas por otra puerta |
| Adaptadores | Las APIs de reloj e ids se permiten en `Platform.Windows` e `Infrastructure` (confinamiento en el IL). La excepción por archivo se registra al crear el adaptador concreto (por ejemplo, el de `IIdGenerator`) | Son las capas que implementan puertos (§4.1). No se inventan rutas antes de que existan |
| Lista Surfaces | Se aplica a todo `Clicalo.UI.Wpf`, con la excepción registrada de `UI.Wpf/Workspace/**` | La tarea la aplica al proyecto. El Centro de control y la bienvenida son ventanas activables (§3.6) |
| Plataforma y frameworks | La lista blanca también comprueba el TFM (`portable` o `windows`), `UseWPF`/`UseWindowsForms` y las referencias crudas a ensamblados | Es la columna TFM de §4.2 y la única forma de que Presentation no pueda activar WPF |
| Proyectos no declarados | `CLCA002` | Un proyecto nuevo sin reglas sería una puerta abierta |
