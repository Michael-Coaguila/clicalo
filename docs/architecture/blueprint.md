# Clícalo: plano de arquitectura definitivo

**Versión:** 1.1 (revisión de la 1.0) · **Fecha:** 2026-09-25 · **Estado:** base normativa del proyecto.

Las decisiones difíciles de revertir se recogen en un ADR (§16). Las reversibles se documentan en `docs/architecture/*.md`. Para cambiar una decisión con ADR se escribe un ADR nuevo; el plano nunca se edita sin él.

**El plano no rebaja requisitos por su cuenta.** Si un requisito del catálogo parece inviable o inseguro, se formula una propuesta al usuario y se registra en §6.1 del catálogo (`docs/requirements/catalog.md`). Mientras el usuario no la ratifique, el requisito sigue vigente tal cual. §1.4 lista las propuestas abiertas.

**Fuentes:**
- **Vinculantes:** las funcionalidades, los comportamientos, los flujos, los estados, los textos, las medidas táctiles y las reglas de producto y UX del Prototipo v4. También la Auditoría (60 hallazgos aceptados), el catálogo de requisitos derivado del paquete (`design_handoff_clicalo/docs/01..09`) y los datos de `data/*.json`.
- **No vinculantes, por instrucción expresa del usuario:** las recomendaciones de arquitectura y tecnología del paquete. Son la separación motor/UI, reutilizar Python, la lista de tecnologías, las APIs concretas y el plan de fases de `docs/10-plan-de-fases.md`. Todo lo técnico de este plano se decide desde primeros principios.
- **Insumos de la síntesis:** la decisión tecnológica ya puntuada (WPF sobre .NET 10 LTS) y tres propuestas de arquitectura: dominio y modularidad, fiabilidad y seguridad, y equipo y DX. Donde se contradicen, §1.3 explica qué se decidió y por qué. La versión 1.1 incorpora la revisión de 23 huecos; el detalle está en el «Registro de revisión» del final.

---

## 1. Resumen ejecutivo y decisiones clave

### 1.1 En una página

Clícalo es una **aplicación de escritorio de Windows escrita en C# 14, sobre .NET 10 LTS y WPF**. Su arquitectura es un **monolito modular hexagonal** cuyas fronteras se comprueban de forma automática. La UI de WPF queda confinada en una sola capa sustituible. El dominio, la aplicación, la plataforma Win32 y los ViewModels no dependen del framework de UI. Por eso, migrar a Avalonia (el finalista) costaría rehacer las vistas, no el producto.

La arquitectura se apoya en seis ideas:

1. **La regla número 1, no robar nunca el foco, se garantiza en tres niveles.**
   - Por construcción: la clase base `NonActivatingWindow` más analizadores que impiden llamar a lo que activa.
   - Por pruebas: una ventana sonda registra `WM_ACTIVATE`.
   - En producción: `ActivationGuard` detecta cualquier activación indebida con los mensajes de activación de la propia ventana, la revierte y deja constancia.
   - Los pocos flujos que sí necesitan cambiar el primer plano (escribir en la búsqueda, abrir y cerrar el Centro de control, «Probar ahora», el menú de la bandeja) pasan por un único `ForegroundOrchestrator`. Este usa concesiones tipadas y siempre verifica la restauración.
2. **Nunca queda una tecla pulsada.**
   - El motor es una función pura `(estado, evento) → (estado, efectos)` que ejecuta un actor en su propio hilo.
   - Cada pulsación se anota en un **registro de escritura adelantada en memoria compartida** (el *ledger*) antes de enviarse.
   - Una **valla de generación** impide que un hilo del motor que estuvo colgado inyecte nada después de una liberación de emergencia.
   - Un proceso guardián diminuto (`Clicalo.Sentinel`, Native AOT) suelta lo que quede registrado si el proceso principal muere.
   - Las invariantes se verifican con pruebas de propiedades, incluidas la «muerte en cada paso» y el «hilo congelado y reanudado».
3. **El estado tiene cuatro dueños inmutables.**
   - El *Documento*: es persistente y admite deshacer.
   - La *Sesión*: el estado transitorio del panel.
   - La *Interacción*: estado transversal a varias superficies (avisos, captura, Modo prueba, atenuado, concesiones de primer plano).
   - El *Motor*: las teclas pulsadas y la ejecución en curso.
   - La UI se pinta a partir de **proyecciones puras**, y ninguna regla de producto vive en un ViewModel.
4. **Los datos son la fuente de verdad y lo generado no se edita.** Textos, tokens de tema, catálogos, tiempos y medidas son datos versionados con esquema. Los generadores Roslyn convierten los errores de datos en errores de compilación.
5. **La calidad la imponen las máquinas.** Se usan:
   - analizadores propios (`CLC*`), ArchUnitNET y reglas UIA propias;
   - instantáneas de renderizado y presupuestos de rendimiento;
   - trazabilidad requisito → prueba (`[Req]`);
   - una cadena de suministro firmada y atestada.
   
   Una sola orden dictable, `cl`, reproduce en local lo mismo que valida la CI.
6. **Operación a la medida de una persona y fronteras a la medida de un equipo.** Todo lo que cuesta operar se aplaza hasta que un requisito o una medida lo justifique: el proxy de IA, el laboratorio de varias VM, el segundo dispatcher, las pruebas de mutación y el *fuzzing*. Lo que es barato y permite crecer se mantiene desde el día 1: capas, ArchUnit, analizadores, esquemas, trazabilidad y puertos.

### 1.2 Tabla de decisiones clave

| # | Decisión | Alternativa descartada | Motivo |
|---|---|---|---|
| D1 | WPF sobre .NET 10 LTS y C# 14, con capa propia de ventanas y punteros sobre Win32 | Avalonia 12, Qt 6.12, WinUI 3, Tauri, Electron, nativo en Rust, Flutter, PySide6 | Es la única opción que hoy cumple con tecnología madura tres cosas: no activar la ventana, un UIA completo y TSF (teclado táctil y Win+H) en todos los campos. Detalle en ADR-0001. |
| D2 | Monolito modular hexagonal: **8 ensamblados** en el proceso principal y módulos por capacidad como espacios de nombres | Un ensamblado por módulo; procesos separados para motor y UI | Cada ensamblado añade tiempo al arranque en frío, y separar motor y UI mete IPC en el camino de menos de 50 ms. Las fronteras entre módulos se imponen igual con pruebas. |
| D3 | **Dos procesos por sesión:** `Clicalo.exe` y `Clicalo.Sentinel.exe` (Native AOT, 5 MB o menos). Opcionalmente, `Clicalo.Launcher.exe` (AOT) del componente de sistema, que solo vive durante el arranque | Un solo proceso; o un modo `--guardian` del mismo exe | Si el proceso que se cae es el mismo que tendría que soltar las teclas, no puede hacerlo. AOT arranca en milisegundos y no depende del runtime de WPF. |
| D4 | Motor = `EngineReducer` puro (en Domain) ejecutado por un actor de un solo hilo (`EngineHost`), con una **valla de generación** en cada efecto externo | Servicios con estado mutable y bloqueos | Las invariantes de seguridad de teclas se pueden probar como propiedades sobre una función pura. La valla cubre el único caso que el modelo puro no cubre: un hilo colgado que vuelve. |
| D5 | *Ledger* de teclas con **escritura adelantada** en memoria compartida sin nombre, heredada por el guardián, con la generación del motor y el modo de inyección por ranura | Preguntar el estado por IPC; soltar «todos los modificadores» a ciegas | Un proceso muerto no responde. Soltar a ciegas deja teclas pegadas y abre menús. |
| D6 | **Cuatro dueños de estado** (Documento, Sesión, Interacción y Motor), proyecciones puras y flujo unidireccional | Un único store tipo Redux; entidades mutables con `INotifyPropertyChanged` | Cada estado tiene su propio ciclo de vida, su hilo y sus reglas de persistencia. El estado transversal entre superficies necesita un dueño explícito. |
| D7 | Deshacer con **instantáneas por porciones** y compartición estructural: 20 entradas y agrupación por clave | *Event sourcing*; comandos con operación inversa | Es barato, correcto por construcción y no exige escribir a mano la inversa de cada comando. |
| D8 | **Un único dispatcher de UI por defecto**, con dos *roles* lógicos (Surfaces y Workspace). Se separa en dos dispatchers solo si S2 mide que el Centro de control degrada el panel | Dos dispatchers desde el día 1 | No se diseña la concurrencia antes de medir. Los *roles* y la publicación inmutable permiten separar sin reescribir. |
| D9 | Bandeja propia (`Shell_NotifyIcon`) en el hilo SysEvents. El menú vive en una **ventana de nivel superior oculta y propia**, con una concesión `TrayMenu` del `ForegroundOrchestrator` | H.NotifyIcon.Wpf; menú sobre `HWND_MESSAGE` | «Soltar todo» tiene que funcionar desde la bandeja aunque la UI esté colgada. El menú necesita el primer plano, y este debe volver verificado a la app anterior. |
| D10 | Documento JSON con envoltorio, esquema `major.minor`, conservación de campos desconocidos, escritura atómica con `ReplaceFileW` y cuarentena. El **uso** va en un archivo aparte que se guarda de forma diferida | SQLite o LiteDB; esquema con un entero sin compatibilidad hacia delante; uso dentro del documento | Es pequeño, legible y atómico. Volver a N−1 no pierde datos, y cada toque no reescribe el documento. |
| D11 | Migraciones como funciones puras sobre `JsonObject`; importador v1 independiente, idempotente y con límites contra bombas zip | Migrar a través de DTOs actuales | Una migración escrita hoy sigue compilando cuando mañana cambien los tipos. |
| D12 | **Elevación = proceso completo elevado.** El inicio elevado sin UAC (SIS-002) llega **en la 2.0** con un **componente de sistema opcional**: se instala una vez con UAC en `%ProgramFiles%` y ejecuta una copia verificada y protegida. uiAccess llega en M7 sobre ese mismo componente | Tarea `Highest` sobre `%LocalAppData%`; bróker elevado; aplazar SIS-002 | Ambas alternativas son escaladas de privilegios directas, y SIS-002 es MUST. |
| D13 | La IPC entre instancias **no tiene verbos que inyecten**: solo `Show`, `OpenUri` e `ImportFile` (este último solo abre una vista previa) | IPC rica | La peor suplantación posible solo muestra el panel. |
| D14 | Datos sensibles como tipos (`Sensitive<T>`, `SecretText` sin `string` de salida) con analizador `CLC0003` y prueba canario en CI | Filtrar con expresiones regulares al escribir el log | Las expresiones regulares fallan sin avisar; los tipos y los canarios fallan a la vista. |
| D15 | Actualizaciones con Velopack, un **manifiesto firmado con ECDSA P-256 por una clave de hardware fuera de GitHub** que maneja el mantenedor (`cl sign-manifest`), y Authenticode con editor fijado | Firma en KMS mediante OIDC desde GitHub Actions; confiar solo en el hash del feed | Con OIDC, una cuenta de GitHub comprometida puede lanzar el workflow, aprobar el entorno y firmar. Con la clave de hardware, esa cuenta no basta. |
| D16 | IA detrás del puerto `ITemplateGenerator` (Microsoft.Extensions.AI), **solo con clave propia en la 2.0**. El proxy de cuota queda diseñado pero **diferido**, con condiciones fijadas: sin identificador de instalación y con el contrato en JSON Schema | Proxy desde el día 1; un proxy que reenvía prompts | Coste de operación sin requisito MUST que lo pida. PLA-008 limita los datos a 4 campos. |
| D17 | Catálogos: los tipos y el núcleo pequeño **se generan** en compilación; plantillas, biblioteca y *seed* son **datos** que se cargan de forma perezosa | Todo generado; todo en tiempo de ejecución | Los errores salen al compilar y las contribuciones de plantillas no requieren tocar C#. |
| D18 | i18n: JSON plano con marcadores con nombre y plurales CLDR, y claves tipadas generadas | `.resx`; ICU completo; marcadores de una letra | Paridad de idiomas en compilación y compatibilidad con Weblate. |
| D19 | Sin mediador y sin *messenger* global: enrutadores explícitos y comandos que se aplican a sí mismos | MediatR, `WeakReferenceMessenger` | «¿Quién reacciona a X?» se responde con «Ir a definición», algo esencial al programar por voz. |
| D20 | Puerta de accesibilidad con **reglas UIA propias** sobre FlaUI más **instantáneas de renderizado**. Axe.Windows queda como apoyo | Solo Axe.Windows | El mantenimiento de Axe.Windows está casi parado, y la fidelidad visual al prototipo necesita píxeles, no solo el árbol UIA. |
| D21 | Monorepo, un único punto de entrada `cl` (Bullseye), CSharpier, Conventional Commits en el título del PR, *squash*, release-please y notas de usuario en español e inglés | Multirepo, Nuke o Cake, commitlint con husky | Un solo lenguaje y una sola cadena de suministro; todo se puede dictar. |
| D22 | Licencia MIT con DCO; firma de código con SignPath Foundation (o un certificado OV en HSM en la nube) | GPL, CLA | Encaja con ACE-001 y cumple la condición de SignPath para proyectos OSS. |
| D23 | **`ForegroundOrchestrator`** (Application, actor en SysEvents) es el único dueño de los cambios de primer plano. Da concesiones tipadas (`TextInput`, `KeyboardNavigation`, `ControlCenter`, `TryNowTarget`, `TrayMenu`) con restauración verificada y una escalera de derechos de primer plano por origen (toque, UIA, atajo) | `FocusBroker` solo para la búsqueda; `SetForegroundWindow` repartido | Sin un dueño, los flujos CCM-004, PRB-004/007 y bandeja no tienen camino legal ni prueba. |
| D24 | **`InjectionMode {VirtualKey, ScanCode}`** forma parte del plan resuelto (perfil → `ActivationContext` → `Inject` → *ledger*) | Enviar siempre VK con `KEYEVENTF_SCANCODE` | Con esa marca Windows ignora `wVk`, así que el modo compatible perdería su sentido y la resolución por distribución no se aplicaría. |

### 1.3 Contradicciones entre las propuestas y cómo se resolvieron

| Tema | Posiciones | Decisión y motivo |
|---|---|---|
| Guardián | Dominio: modo `--guardian` del mismo exe. Fiabilidad: `Sentinel.exe` Native AOT. Equipo: `Watchdog.exe` de unas 300 líneas | **`Clicalo.Sentinel` Native AOT.** El mismo exe cargaría un runtime autocontenido de decenas de MB y compartiría sus riesgos. AOT arranca en milisegundos y ocupa 5 MB o menos. Además es el sitio de repliegue del *hook* de grabación si falla S7. |
| Hilos de UI | Dominio: uno. Fiabilidad: dos | **Uno por defecto, con dos *roles* lógicos** (v1.1). S2 mide si el Centro de control cargado degrada el panel más de un 10 % en p95. Solo en ese caso se separan los *roles* en dos dispatchers, sin cambiar Presentation, que no sabe en qué hilo vive. |
| Bandeja | Decisión tecnológica y Equipo: H.NotifyIcon.Wpf. Fiabilidad: propia | **Propia, en el hilo SysEvents, con ventana propia oculta de nivel superior para el menú** y la concesión `TrayMenu` (v1.1). |
| Reconocedor de gestos | Decisión tecnológica: en UI.Wpf. Dominio: puro en Domain | **`Clicalo.Domain.Touch` (puro).** UI.Wpf solo traduce `WM_POINTER` a `PointerFrame`. Así TAC-002 es una única función en todas las superficies y se reutiliza si se migra a Avalonia. |
| Forma del motor | Dominio: actor con `ActivationPolicy` y ejecutores con puertos. Fiabilidad: reductor puro con efectos | **Síntesis.** `EngineReducer` puro que incorpora `ActivationPolicy` y los planificadores por tipo de acción. `EngineHost` solo interpreta efectos. Los efectos que pueden bloquear (`Launch`, `SystemCommand`) se ejecutan fuera del hilo del motor. |
| Hook de primer plano (WinEvent) | Dominio: en el hilo del motor. Fiabilidad: en el hilo SysEvents | **SysEvents**, solo para el primer plano **externo**. La activación de las ventanas propias se detecta con sus propios mensajes (v1.1). |
| Soltado de emergencia si el motor se cuelga | Dominio: desde el hilo de UI. Fiabilidad: desde SysEvents | **SysEvents**, leyendo el *ledger* **bajo la valla de generación**. Si no puede tomar la valla, escala a reinicio del proceso (v1.1). |
| `KeyId` | Dominio: cadena canónica. Fiabilidad: `ushort`. Equipo: generado | **Cadena canónica persistida** (`"ctrl"`, `"a"`, `"char:ñ"`) con constantes generadas desde `keys.json`. La tecla física (`InjectedKey`: vk, scan, ext y modo) solo existe en los efectos y en el *ledger*. |
| Versión del esquema | Dominio y Equipo: entero. Fiabilidad: `major.minor` más campos desconocidos | **`major.minor`.** Es lo único que permite volver a N−1 (ACT-005) sin perder datos. |
| Autoguardado | 750 ms / 3 s frente a 500 ms / 2 s | **500 ms de *debounce* y 2 s de latencia máxima** para el documento. El uso va aparte, con 30 s y 5 min. |
| Número de ensamblados | 7, 11 o 10 | **8 en el proceso principal**, más Sentinel, Launcher y los proyectos de compilación. Localización y catálogos son espacios de nombres. IPC, *ledger*, inyección de bajo nivel y verificación de confianza se agrupan en `Clicalo.Platform.Core`, compatible con AOT y compartido con Sentinel y Launcher. |
| Catálogos | Dominio: datos. Equipo: generados | **Híbrido** (D17). |
| Marcadores de i18n | Decisión tecnológica: `{a}`, `{p}`… Equipo: con nombre y plurales | **Con nombre y plurales.** Condición: el texto visible resultante debe ser idéntico al del paquete. Una prueba de instantánea compara ambos para los argumentos de muestra. |
| Inicio elevado | Decisión tecnológica: tarea `Highest` en la edición por usuario. Fiabilidad y Equipo: solo en la edición por máquina (después de la 2.0) | **Componente de sistema opcional en la 2.0** (v1.1). SIS-002 es MUST, y una tarea elevada que apunta a una ruta escribible por el usuario sería una escalada de privilegios. La tarea apunta a un lanzador en `%ProgramFiles%` que solo ejecuta una copia protegida y verificada. |
| Firma del manifiesto | Minisign frente a ECDSA P-256; KMS con OIDC frente a clave local | **ECDSA P-256 con clave de hardware fuera de GitHub** como camino único para los dos canales (v1.1). |
| Proxy de IA | Decisión tecnológica: desde el día 1. Fiabilidad: aplazado | **Aplazado** (v1.1). La 2.0 sale con clave propia (PQ-48). El diseño del proxy queda fijado en ADR-0014 con sus condiciones de privacidad. |
| Bus de mensajes | Decisión tecnológica: `WeakReferenceMessenger`. Dominio: enrutadores | **Enrutadores explícitos.** |
| Tipo de error | `DomainError` frente a `ClicaloError` | **Un único `Error`** con código estable, `MessageKey`, severidad, recuperación y forma de anuncio. |
| Puerta de accesibilidad | Axe.Windows frente a reglas propias | **Reglas UIA propias** e instantáneas como puerta; Axe como apoyo. |
| Dependencias | Dependabot frente a Renovate | **Renovate** para versiones; las alertas de seguridad de Dependabot siguen activas. |

### 1.4 Propuestas de producto pendientes de ratificar por el usuario

Se registran en §6.1 del catálogo. Ninguna rebaja un requisito mientras no esté ratificada.

| # | Requisito | Situación y propuesta | Mientras no se ratifique |
|---|---|---|---|
| P1 | NFR-001 (panel en menos de 1 s), en el arranque al iniciar sesión | No se propone ninguna excepción. S5 mide primero. Solo si S5 demuestra que no se puede cumplir, se propone al usuario mostrar primero la burbuja | NFR-001 es puerta de publicación sin excepciones |
| P2 | PQ-35 y la fila «Una sola ventana» [rSingle] | La instancia única es obligatoria por seguridad: dos motores serían dos dueños de las mismas teclas. Se propone quitar la fila de Sistema y mover la clave de texto a `allow-unused.txt` | La fila no se construye. El comportamiento es el mismo que con la opción activada, que es su valor por defecto |
| P3 | PLA-003 (SHOULD, cuota gratuita de 5 al día) y PQ-48 | La 2.0 sale solo con clave propia, y la cuota gratuita llega con el proxy (ADR-0014) | Los flujos PLA con cuota no se muestran; el resto de PLA sí |
| P4 | SIS-002 (MUST) | No se rebaja: se cumple en la 2.0 con el componente de sistema (D12). Solo si S14 fracasa se pediría al usuario la decisión de rebajarlo a SHOULD para la 2.0 | Se cumple |
| P5 | Win64 ARM (distribución) | ARM64 se publica en beta desde el principio. En estable, cuando pase la aceptación en un equipo ARM64 físico (§10.2) | ARM64 solo en beta |
| P6 | NFR-010 al desinstalar desde Configuración de Windows | Ese camino no puede mostrar UI y siempre conserva los datos. La pregunta se hace en Sistema › Desinstalar y, al reinstalar, en la bienvenida. Requiere dos textos nuevos en ES y EN | Se implementa así; los textos entran por PR de i18n |

---

## 2. Stack tecnológico

### 2.1 Stack elegido

Las versiones marcadas «x» se fijan de forma exacta al crear el repositorio. Después las mantiene Renovate. No se admiten rangos flotantes, y `cl check` lo comprueba.

| Área | Elección | Versión | Justificación |
|---|---|---|---|
| Lenguaje | C# | 14 | `Nullable` activado, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. |
| Runtime | .NET | 10 LTS (SDK 10.0.1xx con el último parche; soporte hasta noviembre de 2028) | Migración planificada a .NET 12 LTS en los 6 meses siguientes a su GA (noviembre de 2027). |
| TFM | `net10.0` (Domain, Application, Presentation) · `net10.0-windows10.0.19041.0` (resto) | — | Proyecciones WinRT (`UISettings`, `InputPane`, `PackageManager`). |
| UI | WPF (.NET 10), sistema de diseño propio | — | Sin el tema Fluent (experimental, WPF0001) y sin suites de terceros. |
| Interop Win32 | Microsoft.Windows.CsWin32 | 0.3.x fijada | `NativeMethods.txt` por proyecto; `LibraryImport` en las rutas calientes. |
| MVVM | CommunityToolkit.Mvvm | 8.4.x | `[ObservableProperty]` en propiedades parciales y `[RelayCommand]`. **Sin** `WeakReferenceMessenger`. |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.x | Registros explícitos, sin *Generic Host*, fábricas perezosas para lo secundario. |
| Logging | Microsoft.Extensions.Logging + Serilog (`Serilog.Extensions.Logging`) con un *sink* propio `FixedNameRollingFileSink` | 10.0.x / 4.x | `[LoggerMessage]` tipado, desestructuración de `Sensitive<T>` y nombre fijo `clicalo.log` (§9.4). |
| Trazas | `EventSource` propio `Clicalo-Perf` + `System.Diagnostics.Metrics` | BCL | Se analizan con WPR y PerfView; sin exportador de red. |
| Serialización | System.Text.Json con `JsonSerializerContext` | BCL | Generación de código, compatible con AOT (IPC, Sentinel y Launcher). |
| Secretos | DPAPI (`System.Security.Cryptography.ProtectedData`) + Administrador de credenciales (`CredWriteW`) | 10.0.x | LOG-003; clave de IA. |
| IA | Microsoft.Extensions.AI (`IChatClient`) + Microsoft.Extensions.Http.Resilience | 10.x | Proveedor intercambiable por configuración; tiempo máximo de 15 s. Solo con clave propia en la 2.0. |
| Actualizaciones | Velopack (NuGet + CLI `vpk` de la misma versión) | 1.2.x | Por usuario sin UAC, canales, deltas y fuente propia `IUpdateSource` con verificación de firma. |
| Firma del manifiesto | ECDSA P-256 en una llave de hardware PIV (YubiKey 5 serie Nano o equivalente), mediante `cl sign-manifest` | — | Clave fuera de GitHub (D15). La verificación usa la BCL (`ECDsa`). |
| Fuentes | Atkinson Hyperlegible, JetBrains Mono, Material Symbols Rounded (instancias estáticas FILL 0 y 1, recortadas) | Versionadas | El script de recorte en `/tools/fonts` es Python, se ejecuta a mano y su salida se versiona. No hay Python en la compilación. |
| Pruebas | xUnit v3, Shouldly, comparador de instantáneas propio en TestKit (texto y PNG con tolerancia; sustituye a Verify, [D-01](deviations.md)), CsCheck, `FakeTimeProvider`, ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3`), FlaUI.UIA3, Axe.Windows | 4.x ([D-02](deviations.md)) / 4.x / — / 4.x / 10.x / — / 5.x / 2.4.x | Ver §10. Stryker.NET y SharpFuzz entran **después de la 2.0** (M7). |
| Análisis | Meziantou.Analyzer, Microsoft.CodeAnalysis.BannedApiAnalyzers, NetAnalyzers del SDK y analizadores propios `Clicalo.Analyzers` | 2.x | Ver §4.4. |
| Formato | CSharpier + `.editorconfig` | 1.x | Determinista y sin opciones, así que se puede dictar sin cuidar la sangría. |
| Build | `cl` → Bullseye + SimpleExec (proyecto `build/`) | — | C# depurable, sin un DSL propio. |
| CI/CD | GitHub Actions (`windows-2025` y `windows-11-arm` alojados + un equipo táctil propio bajo demanda), CodeQL, OpenSSF Scorecard, CycloneDX, `actions/attest-build-provenance`, release-please, Renovate | — | Ver §10 y §11. |
| Firma de código | SignPath Foundation (OSS con licencia OSI). Si no es posible: Azure Artifact Signing (si el país lo permite) o un certificado OV en HSM en la nube | — | Authenticode en exe, en **todas** las DLL (también tras R2R), en Setup, Update, Launcher y paquetes. La aprobación en SignPath usa su propia cuenta con MFA, fuera de GitHub. |

### 2.2 Descartado y por qué

| Descartado | Motivo |
|---|---|
| Avalonia 12 (como ganadora) | Hoy no tiene TSF (el teclado táctil y Win+H fallan en todos los campos), su UIA está recién corregido y tiene el bug #22273, que rompe el IME de la app en primer plano. **Es el plan B.** Se reevalúa por ADR cuando publique TSF y TextPattern y pase S1, S3 y S4. |
| WinUI 3 | La vía para no activar la ventana (`NoActivate`) es experimental o depende de HWND internos. |
| Qt 6.12 (C++ y QML) | Deuda de accesibilidad en Qt Quick, C++ no es seguro en memoria en un proceso con *hooks*, y es costoso de programar por voz. |
| Tauri, Electron | Sin evidencia de no activación con toque, cientos de MB residentes y problemas con uiAccess y la elevación. |
| Nativo (Rust, Direct2D, AccessKit) | Toolkit propio y *bus factor* de 1. |
| Flutter, PySide6 | Fallan la compuerta UIA/TSF, o el arranque y la fiabilidad del *hook*. |
| H.NotifyIcon.Wpf | Ata la bandeja al hilo de UI (D9). |
| *Generic Host* | Coste en el tiempo hasta el primer frame. |
| MediatR, `WeakReferenceMessenger`, ReactiveUI | Reflexión o indirección opaca; licencia comercial (MediatR); navegación por voz más difícil. |
| FluentAssertions 8 | Licencia comercial. |
| MSIX | No admite uiAccess y complica el inicio elevado. |
| SQLite, LiteDB | Ver §6.5. |
| WPF Fluent, suites de controles | Experimental o dependencia de terceros en la capa más visible. |
| Nuke, Cake, commitlint con husky, Style Dictionary | Añaden un DSL, Node o un segundo ecosistema (principio: una sola cadena de suministro). |
| Dependabot para versiones | Agrupa peor y no gestiona `global.json` (las alertas de seguridad sí se mantienen). |
| Firma del manifiesto en KMS con OIDC | La cuenta de GitHub controlaría la firma (D15). |
| Proxy de IA en la 2.0 | Coste fijo de operación sin requisito MUST (D16). |
| Clicalo.Gallery | La sustituyen las instantáneas de renderizado por estado y `cl states` (§10.1). |
| MSI por máquina completo | Lo sustituye el componente de sistema (D12), que es más pequeño y reutiliza la instalación por usuario. |

---

## 3. Modelo de procesos, elevación, foco y ventanas

### 3.1 Vista de procesos

```
                    Sesión interactiva del usuario (una instancia por sesión WTS)
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Clicalo.exe  (integridad media; alta solo si el usuario la pide)                │
│  ├ UI           STA · un Dispatcher WPF con dos roles:                          │
│  │              Surfaces: panel, barra y asa, burbuja, laterales, menú,         │
│  │              avisos flotantes, PointerInputSource, GestureRecognizer         │
│  │              Workspace: Centro de control y bienvenida                       │
│  │              (se separan en dos dispatchers solo si S2 lo exige)             │
│  ├ Engine       buzón de 2 carriles + temporizadores: EngineHost, SendInput     │
│  │              tras InjectionGate, escritura del ledger                        │
│  ├ SysEvents    STA + ventana solo de mensajes + TrayMenuHost (nivel superior   │
│  │              oculta): WinEvent de primer plano externo y del cursor, WTS,    │
│  │              energía, WM_SETTINGCHANGE/DISPLAYCHANGE, bandeja,               │
│  │              RegisterHotKey, portapapeles, ForegroundOrchestrator,           │
│  │              PointerPositionTracker, EmergencyReleaser                       │
│  ├ Shell        STA, prioridad baja: ShellExecute/IShellDispatch2, WMI,         │
│  │              comandos de sistema (nunca bloquea al motor)                    │
│  ├ Hook         bajo demanda: WH_KEYBOARD_LL (grabar) y WH_MOUSE_LL (temporal)  │
│  ├ Persistence  consumidor único: documento, uso, copias, restauración          │
│  └ ThreadPool   IA, actualizaciones, catálogo de apps, importación              │
│   KeyLedger: sección de memoria sin nombre, 4 KiB  ◄── escritura adelantada    │
└──────┬───────────────────────────────────────▲──────────────────────────────────┘
       │ handles heredados (PROC_THREAD_ATTRIBUTE_HANDLE_LIST, solo estos 3):
       │  proceso padre (SYNCHRONIZE | QUERY_LIMITED) · ledger (solo lectura)
       │  · extremo de un pipe anónimo (latido cada 1 s)
       ▼                                       │
┌──────────────── Clicalo.Sentinel.exe (Native AOT, misma integridad, ≤5 MB) ─────┐
│ WaitForMultipleObjects(padre, pipe) → al morir el padre: leer ledger →          │
│ SendInput(key-ups en el modo registrado, con máscara) → crash-journal →         │
│ relanzar solo si ¬CleanShutdown ∧ ¬NoRelaunch ∧ no hay bucle de fallos          │
└─────────────────────────────────────────────────────────────────────────────────┘
Segunda ejecución ─► Mutex Local\Clicalo.{sidHash}.Instance ─► pipe \\.\pipe\Clicalo.{sidHash}.{sessionId}
Update.exe (Velopack): solo lo lanza Clicalo.exe en integridad MEDIA tras verificar el paquete (§9.3)

Opcional, componente de sistema (§3.3), instalado una vez con UAC:
  Tarea \Clicalo\ElevatedStart-{sidHash} (HighestAvailable, al iniciar sesión y bajo demanda)
   └► %ProgramFiles%\Clicalo\Clicalo.Launcher.exe (AOT, elevado, vive < 2 s)
        sincroniza y verifica %ProgramFiles%\Clicalo\app\<versión>\ → lanza Clicalo.exe elevado → sale
```

| Proceso | Tecnología | Vida | Responsabilidad | Presupuesto |
|---|---|---|---|---|
| `Clicalo.exe` | WPF sobre .NET 10, autocontenido, R2R | Toda la sesión | UI, motor, persistencia, actualizaciones, IA | ≤120 MB de *working set* con 4 superficies; CPU media <0,5 % en reposo |
| `Clicalo.Sentinel.exe` | .NET 10 Native AOT, sin WPF ni reflexión | Mientras viva el principal | Soltar lo que registra el *ledger*; relanzar el principal cuando procede; registrar el fallo | ≤5 MB; 0 % de CPU (bloqueado en espera) |
| `Clicalo.Launcher.exe` | .NET 10 Native AOT (solo con el componente de sistema) | Segundos, al iniciar sesión o tras actualizar | Sincronizar y verificar la copia protegida y lanzarla elevada | ≤5 MB; ≤300 ms sin sincronización |
| `Update.exe` | Velopack | Puntual, siempre en integridad media | Aplicar un paquete ya verificado | — |

**Arranque del guardián:**
- Se lanza **desde un hilo de fondo al principio del arranque**, en paralelo al primer frame y sin bloquear el hilo de UI. Si S5 mide que retrasa el primer frame, se lanza justo después.
- **El motor acepta toques desde el primer frame; no espera al guardián.**
- El breve intervalo sin guardián (menos de 200 ms, medido en S9) está cubierto por dos cosas:
  - Dentro del proceso, por `EngineHost` y `EmergencyReleaser`.
  - Si el proceso muere en ese intervalo, por el soltado preventivo del siguiente arranque: los modificadores que `GetAsyncKeyState` marque como pulsados, con máscara (SEG-006).
- Es un riesgo residual aceptado y registrado en §15.2.

**Si muere el guardián:**
- El principal lo detecta porque se rompe el pipe y lo relanza con esperas de 1, 5 y 30 s.
- El umbral de bucle de fallos está en `timings.json` → `Guardian.RestartLoop = {count: 3, window: 10 min}`. Si se supera, se avisa en Sistema › Inicio y estabilidad.

**Si muere el principal:**
- Sentinel lo relanza, salvo si el *ledger* tiene `CleanShutdown` o `NoRelaunch`.
- `timings.json` → `App.CrashLoop = {count: 3, window: 10 min}`. Al superarlo, el siguiente arranque entra en **modo seguro**. Es el mismo umbral que se prueba en S9.

**Limitación aceptada.** «Finalizar árbol de procesos» mata a los dos. Lo cubre el soltado preventivo del siguiente arranque. No se usan trucos de *re-parenting*, porque son frágiles y los antivirus los tratan como sospechosos.

### 3.2 Modelo de hilos

| Hilo | Dueño de | Prioridad | Nunca hace |
|---|---|---|---|
| UI (roles Surfaces y Workspace) | Todas las `NonActivatingWindow`, `PointerInputSource`, `GestureRecognizer`, `SessionStore`, `InteractionStore` (rol Surfaces); Centro de control, bienvenida, `WorkspaceSession` (rol Workspace) | Normal (AboveNormal mientras hay contacto) | E/S, esperas, `SendInput`, `SetForegroundWindow` |
| Engine | `EngineHost` (y dentro de él `EngineState`), escritura del *ledger*, `SendInput` a través de `InjectionGate` | AboveNormal | E/S de disco o red, llamadas a la UI, esperas bloqueantes, `ShellExecute`, WMI |
| SysEvents | *Hooks* de WinEvent, WTS, energía, bandeja y su menú, portapapeles (OLE STA), `ForegroundOrchestrator`, `PointerPositionTracker`, `EmergencyReleaser` | AboveNormal | Lógica de negocio (solo traduce, encola y orquesta el primer plano) y llamadas que puedan bloquear |
| Shell | `ShellExecutor`: `IShellDispatch2::ShellExecute`, WMI (brillo), `LockWorkStation` y el resto de `SystemCommand` | BelowNormal | Tocar el *ledger* o la UI |
| Hook | `WH_KEYBOARD_LL` y `WH_MOUSE_LL`, siempre temporales | Highest | Cualquier cosa distinta de `TryWrite` en un anillo prealocado |
| Persistence | Serializar, validar, escribir el documento y el uso, hacer copias | BelowNormal | Tocar la UI |

**Reglas:**

1. **Entre hilos solo cruzan objetos inmutables.** Por ejemplo `UserDocument`, `EngineSnapshot`, `InteractionSnapshot`, `EngineInput` y los modelos proyectados.

2. **Hay cuatro puntos de mutación y cada uno tiene un único escritor:**
   - `DocumentStore`: un *lock* corto que solo envuelve `Apply` y el historial, sin E/S dentro. Se publica con `Volatile.Write`.
   - `SessionStore`: solo el rol Surfaces.
   - `InteractionStore`: solo el rol Surfaces, que publica `InteractionSnapshot` inmutable hacia Workspace.
   - `EngineHost`: solo el hilo Engine.
   - En Debug, `ThreadGuard.AssertSurfaces()`, `AssertEngine()`, etc. comprueban la afinidad. Los *roles* se comprueban aunque compartan dispatcher.

3. **El buzón del motor tiene dos carriles.** `Priority` (ReleaseAll, eventos terminales, fin de contacto) se vacía siempre antes que `Normal`. Soltar nunca espera detrás de una macro.

4. **Las macros son máquinas de estado dirigidas por temporizadores.** Una espera es `Schedule(key, 500 ms)`, nunca `Thread.Sleep`.

5. **Las instantáneas del motor hacia la UI se agrupan:** como máximo una por frame (unos 16 ms).

6. **Vigilancia y valla de generación (REG-03):**
   - El motor hace *ping* al dispatcher cada segundo. Tras 5 s sin respuesta registra `ui.hang`. A los 30 s ofrece reiniciar desde la bandeja.
   - `EngineHost` escribe `LastHeartbeatTicks` en el *ledger* en cada vuelta del buzón y con un temporizador de 250 ms.
   - **Valla de generación.** El *ledger* guarda `EngineGeneration` (uint64). Cada `EngineHost` nace con la generación vigente `g`. Todo efecto con consecuencias externas se ejecuta a través de `InjectionGate.TryRun(g, …)`: `Inject`, escritura del *ledger*, `ClipboardPaste` y el envío de `Launch` o `SystemCommand` al hilo Shell. `TryRun`:
     ```
     lock (gate) {
       if (Volatile.Read(ledger.EngineGeneration) != g) return Fenced;   // el hilo zombi no inyecta
       ledger.BeginDown(...); SendInput(...); ledger.Commit(...);
     }
     ```
   - **Emergencia.** SysEvents detecta 2 s sin avance del latido y ejecuta `EmergencyReleaser`:
     1. `Monitor.TryEnter(gate, 250 ms)`.
     2. **Si lo consigue:** hace `Interlocked.Increment(ref EngineGeneration)`, suelta todo lo del *ledger* dentro del *lock* (en el modo registrado y con máscara), libera la valla, crea un `EngineHost` nuevo con la generación `g+1` y `EngineState.Empty`, y avisa «Algo falló; se soltaron las teclas». El hilo viejo se abandona como hilo de fondo, se registra `engine.zombie` y todo lo que intente después devuelve `Fenced`, que se descarta.
     3. **Si no lo consigue** (el hilo colgado tiene la valla, por ejemplo dentro de `SendInput` retenido por *hooks* de terceros), no se puede garantizar el orden. Se escala: el *ledger* recibe `EmergencyRestart` sin `CleanShutdown`, se escribe el `crash-journal` y se hace `TerminateProcess(self)`. Sentinel suelta desde el *ledger* (INV-2 garantiza que lo que estaba en envío está registrado) y relanza.
     4. **Un segundo cuelgue del motor en 10 minutos** escala directamente a reinicio del proceso.

7. **GC y asincronía:**
   - GC de estación de trabajo concurrente. Con un *hook* LL instalado se usa `GCSettings.LatencyMode = SustainedLowLatency`.
   - `ConfigureAwait(false)` en Application e Infrastructure.
   - Todo lo asíncrono lleva un `CancellationToken` ligado a su ventana, sección o salida.

8. **Dispatchers (D8):**
   - Los recursos WPF se generan como `Freezable` congelados, de modo que se pueden compartir entre hilos.
   - `ThemeService` y la fuente de localización enlazable se instancian **por dispatcher**, a partir de estado inmutable. Con un único dispatcher hay una sola instancia.
   - Si S2 obliga a separar, Workspace recibe su propio dispatcher y consume `InteractionSnapshot`, `EngineSnapshot` y el documento publicados. No hace falta cambiar nada en Presentation.

### 3.3 Elevación, uiAccess y componente de sistema

Hay **una sola instalación por usuario** (Velopack, sin UAC) y un **componente de sistema opcional** que se instala una vez con UAC desde Sistema › Inicio. El componente cumple SIS-002 en la 2.0 y es la base de uiAccess en M7.

| Capacidad | Solo instalación por usuario (predeterminada) | Con componente de sistema (2.0, opcional) | uiAccess (M7, sobre el componente) |
|---|---|---|---|
| Uso normal, inicio con Windows (HKCU Run) | ✔ | ✔ | ✔ |
| Relanzar elevado bajo demanda (EJE-013) | ✔, con UAC cada vez y verificando antes la firma del exe | ✔, **sin UAC**, mediante la tarea (ejecución bajo demanda) | ✔ |
| Iniciar con Windows como administrador sin UAC (SIS-002) | Al activar el interruptor se ofrece instalar el componente (un único UAC) | ✔ | ✔ |
| Por encima del menú Inicio (uiAccess) | ✘ | ✘ | ✔, con binario firmado y la mitigación T4 (S13) |

**Componente de sistema (`Clicalo.SystemComponent`):**

- **Instalación.** Desde Sistema › Inicio, `Clicalo.exe` verifica su propia firma y relanza `Clicalo.exe --install-system-component` con `runas`, lo que produce un UAC. Ese proceso elevado:
  1. copia `Clicalo.Launcher.exe` (firmado) a `%ProgramFiles%\Clicalo\`, que hereda la ACL de Program Files (solo Administradores, SYSTEM y TrustedInstaller escriben);
  2. registra la tarea `\Clicalo\ElevatedStart-{sidHash}`: principal el usuario, `RunLevel HighestAvailable`, disparador al iniciar sesión de ese usuario, ejecución bajo demanda permitida y acción el lanzador **sin argumentos**;
  3. crea la entrada de desinstalación en HKLM, «Clícalo: componente de sistema»;
  4. hace la primera sincronización.
- **El lanzador** (Native AOT, dependiente solo de `Platform.Core`) hace, en orden:
  1. Lee la versión instalada por el usuario en `%LocalAppData%\Clicalo.App\current\`.
  2. Si coincide con `%ProgramFiles%\Clicalo\app\<versión>\`, que ya está verificada y protegida, lanza esa copia (camino rápido, sin sincronizar).
  3. Si no coincide:
     - copia los archivos a `app\.staging\`, que es una ruta protegida;
     - **verifica la copia, no el original.** Cada archivo debe coincidir en SHA-256 con el **catálogo de archivos firmado** (`release-files.json` + `.sig`, ECDSA con las claves públicas fijadas en `Platform.Core.Trust`), y cada PE debe pasar `WinVerifyTrust` con el editor fijado;
     - la versión no puede ser menor que el `minSafeVersion` del último manifiesto verificado, que el lanzador guarda en su carpeta protegida y que solo puede subir;
     - si todo es correcto, renombra a `app\<versión>\`, conserva N−1 y borra lo anterior.

     Como se verifica **después** de copiar a una ubicación que el usuario no puede escribir, no hay ventana de sustitución (TOCTOU).
  4. Lanza `%ProgramFiles%\Clicalo\app\<versión>\Clicalo.exe` elevado y sale. Si la verificación falla, lanza la última copia protegida válida con un aviso o, si no hay ninguna, no lanza nada y registra `launcher.verify_failed`. El inicio normal (HKCU Run) arranca entonces la app sin elevación.
- **Por qué se permite la ejecución bajo demanda.** Así se vuelve a modo administrador sin UAC tras una actualización (§9.3). Un proceso medio del mismo usuario también podría disparar la tarea, pero solo conseguiría arrancar el Clícalo oficial verificado. UIPI le impide enviar UIA o mensajes a ese proceso elevado, y la IPC no tiene verbos que inyecten (D13). El riesgo residual está en T3.
- **Desinstalación.** Desde Sistema › Inicio (UAC) o desde Configuración de Windows (entrada de HKLM): se borra la tarea y `%ProgramFiles%\Clicalo`.
- **Coste.** Cuando no hay cambio de versión, el lanzador añade ≤300 ms al arranque al iniciar sesión, y S14 lo mide contra NFR-001. La sincronización (unos 100 MB) solo ocurre tras una actualización y se hace en el flujo posterior a la actualización, no al iniciar sesión.

**Reglas, cada una con su prueba:**
1. Antes de registrar la tarea se comprueba con `GetEffectiveRightsFromAcl` que ni el usuario ni `Authenticated Users` tienen escritura, `WRITE_DAC` o `DELETE` sobre el lanzador, su carpeta o `app\`. Si los tienen, la tarea no se crea.
2. Relanzar elevado con UAC usa `ShellExecuteEx("runas")` sobre la ruta en ejecución, **después** de `WinVerifyTrust` y la comprobación del editor fijado. Un binario manipulado nunca llega al diálogo de UAC con el nombre de Clícalo.
3. Con Clícalo elevado, las apps y las webs se abren **sin elevación** (EJE-011), mediante `IShellDispatch2::ShellExecute` del escritorio del shell, en el hilo Shell.
4. **Modo uiAccess y destino elevado (mitigación T4, M7).** Solo se inyecta si el disparo viene de hardware (`GetCurrentInputMessageSource` = `IMO_HARDWARE` en el `WM_POINTER`) o si el usuario activó expresamente «Permitir control por voz en apps de administrador».
5. **Una instancia elevada nunca escribe en la carpeta de instalación por usuario** ni aplica actualizaciones (§9.3).

**Por qué no hay bróker elevado.** Un bróker que acepta «inyecta estas teclas» de un cliente de integridad media convertiría a cualquier código del mismo usuario en controlador de las ventanas de administrador. Un proceso completo elevado sí está protegido por UIPI. `IInputInjector` es la costura donde encajaría un bróker en el futuro si se decidiera por ADR, pero hoy está prohibido.

**Relanzamiento elevado (traspaso):**
1. Motor: `Terminal(Relaunch)`. El *ledger* recibe `CleanShutdown | NoRelaunch`, así que Sentinel no compite con el traspaso.
2. `Store.FlushAsync` (máximo 2 s).
3. Con componente: `ITaskService` ejecuta `ElevatedStart` bajo demanda. Sin componente: `ShellExecuteEx(runas, "--handover")`. Si devuelve `ERROR_CANCELLED` (1223), el interruptor vuelve a apagado, se muestra un aviso, se limpia `NoRelaunch` y termina el flujo.
4. Se cierran las superficies, se libera el mutex y el proceso sale limpio.
5. La instancia elevada espera el mutex hasta 15 s, carga el documento (la posición del panel está en él) y avisa con `[adminOn]`.

No se transfiere estado por IPC, así que no hay superficie de ataque.

### 3.4 Instancia única e IPC

**La instancia única es obligatoria** (propuesta P2 sobre la fila [rSingle]). Dos procesos serían dos motores y dos *ledgers* sobre el mismo teclado.

**Nombres:**
- `sidHash` son los 16 primeros caracteres hexadecimales de `SHA-256(UserSid)`.
- El mutex está en `Local\` (una instancia por sesión, SIS-003).
- El pipe es `\\.\pipe\Clicalo.{sidHash}.{sessionId}`.

**Servidor:**
- `FILE_FLAG_FIRST_PIPE_INSTANCE`, `PIPE_REJECT_REMOTE_CLIENTS`, mensajes de 16 KiB como máximo, 4 instancias, tiempo máximo de 2 s y límite de 10 peticiones por segundo.
- DACL: `(A;;GRGW;;;<UserSid>)(D;;GA;;;NU)`. Si el servidor está elevado, SACL con etiqueta Media, para que un cliente de integridad media pueda entregar `Show`.

**Verificación en los dos sentidos:**
- **Cliente:** `GetNamedPipeServerProcessId` → la ruta debe ser una de las rutas propias (instalación por usuario o copia protegida del componente) **y** el editor debe ser el fijado. Si no coincide, alguien ha ocupado el nombre: no se envía nada, se registra `ipc.squat_detected` y se avisa al usuario.
- **Servidor:** comprueba que el cliente está en la misma sesión y con el mismo SID, y lee su nivel de integridad. Un cliente con menos integridad que el servidor solo puede pedir `Show`.

```csharp
namespace Clicalo.Platform.Core.Ipc;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
[JsonDerivedType(typeof(ShowRequest), "show")]
[JsonDerivedType(typeof(OpenUriRequest), "uri")]        // solo "clicalo:", ≤ 8 KiB
[JsonDerivedType(typeof(ImportFileRequest), "import")]  // abre VISTA PREVIA; nunca aplica
public abstract record IpcRequest(int ProtocolVersion);
public sealed record IpcResponse(IpcStatus Status);      // Ok | Rejected | Unsupported
```

**Invariante D13.** `Clicalo.Application.Ipc` (el servidor) solo depende de `IShellNavigator` (mostrar y abrir la vista previa). Una regla de ArchUnit le prohíbe depender de `Clicalo.Application.Engine` y de `Clicalo.Application.Foreground`.

### 3.5 Ventanas no activables

`NonActivatingWindow`, en `Clicalo.UI.Wpf.Windowing`, es la **única** clase base permitida para las superficies del panel. Su configuración se aplica en `OnSourceInitialized`, **antes** del primer `Show`:
- `WS_EX_NOACTIVATE | WS_EX_TOPMOST`;
- como propietaria, una ventana oculta `OwnerAnchor`, en lugar de `WS_EX_TOOLWINDOW`. Así no aparece en Alt+Tab ni en la barra de tareas y conserva el tratamiento de ventana normal para UIA;
- `SetWindowFeedbackSetting`, que desactiva todos los `FEEDBACK_TYPE` (sin el círculo táctil);
- registro en `SurfaceRegistry` y en `ActivationGuard`.

```csharp
public abstract class NonActivatingWindow : Window
{
    protected sealed override void OnSourceInitialized(EventArgs e);
    public void ShowPassive();                 // SetWindowPos(SWP_SHOWWINDOW | SWP_NOACTIVATE)
    public void HidePassive();
    public void MovePassive(PhysicalRect r);   // SWP_NOACTIVATE | SWP_NOZORDER
    public SurfaceId Id { get; }
}
```

**El *hook* común de `HwndSource`:**

| Mensaje | Respuesta |
|---|---|
| `WM_MOUSEACTIVATE` | `MA_NOACTIVATE` |
| `WM_POINTERACTIVATE` | `PA_NOACTIVATE` |
| `WM_WINDOWPOSCHANGING` | Añade `SWP_NOACTIVATE`, salvo durante una concesión `TextInput` o `KeyboardNavigation` sobre esa superficie |
| `WM_ACTIVATE` (≠ `WA_INACTIVE`), `WM_NCACTIVATE(TRUE)`, `WM_ACTIVATEAPP(TRUE)` | `ActivationGuard.OnActivated(surface, msg)` |
| `WM_DPICHANGED` | Aplica el rectángulo con `SWP_NOZORDER \| SWP_NOACTIVATE` y lo marca como manejado (#7561) |
| `WM_GETDPISCALEDSIZE` | Tamaño propio |
| `WM_NCHITTEST` | Margen de sombra no clicable; el mecanismo se decide en S6 (`SetWindowRgn` ajustado al contorno o alfa 0 en ventana *layered*). `HTTRANSPARENT` no sirve: solo pasa el clic a ventanas del mismo hilo |

**Prohibido en las superficies:** `Popup`, `ContextMenu`, `ToolTip` interactivo y `ComboBox`. Lo impiden `CLC0002` en XAML y `BannedSymbols.Surfaces.txt`. Los menús y desplegables son `NonActivatingWindow` hijas.

**`ActivationGuard` (REG-01 en tiempo de ejecución).** Se dispara con los mensajes de activación de la **propia** ventana. Son síncronos y deterministas en el hilo de UI y no dependen de los WinEvent (que se filtran con `WINEVENT_SKIPOWNPROCESS`). También detecta el caso de Clícalo en primer plano (EC-EJE-05).

```
WM_ACTIVATE / WM_NCACTIVATE / WM_ACTIVATEAPP sobre hwnd ∈ SurfaceRegistry
   ─► ¿ForegroundOrchestrator tiene una concesión activa para hwnd? ─ sí ─► legítimo
                                                                  └ no ─► VIOLACIÓN:
        1 Orchestrator.RestoreAfterViolation(lastExternalForeground)  (asíncrono, restauración verificada)
        2 volver a aplicar WS_EX_NOACTIVATE
        3 métrica reg01.violations++ y registro con la causa probable (DPI, Topmost, Show, desconocida)
        4 Debug y CI: Debug.Fail
```

`SurfaceIntegrityCheck` se ejecuta cada 30 s y después de cada `WM_DPICHANGED`, `WM_DISPLAYCHANGE` o cambio de tema. Comprueba `GWL_EXSTYLE` y `HWND_TOPMOST` en cada superficie y los repara con `SWP_NOACTIVATE`.

**Prueba negativa obligatoria.** `ActivationGuardNegativeTests` (Windowing) hace que InputProbe, estando en primer plano, llame a `SetForegroundWindow` sobre el panel. La prueba exige tres cosas: que `reg01.violations` suba en 1, que el primer plano vuelva a InputProbe en ≤200 ms y que `WS_EX_NOACTIVATE` esté presente.

### 3.6 `ForegroundOrchestrator`: el único dueño de los cambios de primer plano

Sustituye al `FocusBroker` de la versión 1.0. Vive en `Clicalo.Application.Foreground` y es un actor que se ejecuta en el hilo SysEvents. Ahí ya se conoce el primer plano externo verificado (`ForegroundMonitor`).

```csharp
namespace Clicalo.Application.Foreground;
public enum LeaseKind { TextInput, KeyboardNavigation, ControlCenter, TryNowTarget, TrayMenu }
public enum LeaseOrigin { Touch, UiaInvoke, GlobalHotkey, Tray, Internal }

public interface IForegroundOrchestrator
{
    ValueTask<LeaseResult> AcquireAsync(LeaseRequest request, CancellationToken ct);
    ValueTask RestoreAfterViolationAsync(WindowToken expected, CancellationToken ct);
    ForegroundSnapshot Current { get; }   // último primer plano EXTERNO verificado + época
}
public sealed record LeaseRequest(LeaseKind Kind, WindowToken Target, LeaseOrigin Origin, TimeSpan? IdleTimeout);
public abstract record LeaseResult
{
    public sealed record Granted(ForegroundLease Lease) : LeaseResult;
    public sealed record Denied(ForegroundDenialReason Reason) : LeaseResult;
}
public sealed class ForegroundLease : IAsyncDisposable
{
    public LeaseKind Kind { get; }
    public WindowToken PreviousForeground { get; }
    public ForegroundEpoch EpochAtAcquire { get; }
    public ValueTask<RestoreOutcome> RestoreAsync(CancellationToken ct); // Restored | RestoredAfterRetry | Flashed | Failed
    public ValueTask DisposeAsync();  // siempre restaura (según la política del tipo) y vuelve a poner WS_EX_NOACTIVATE
}

// Puertos (implementados en Platform.Windows y UI.Wpf):
public interface IForegroundControl   { bool TrySetForeground(WindowToken w); WindowToken GetForeground();
                                        void FlashTaskbar(WindowToken w); }
public interface ISurfaceActivationStyle { void AllowActivation(SurfaceId s); void RestoreNoActivate(SurfaceId s); }
```

- `SetForegroundWindow` solo aparece en `Platform.Windows/Foreground/ForegroundControl.cs`.
- `Window.Activate` está prohibido en todo el código.
- `TrackPopupMenuEx` solo aparece en `Platform.Windows/Tray/TrayMenuHost.cs`.

**Concesiones tipadas:**

| Tipo | Uso | Destino | Restauración al terminar |
|---|---|---|---|
| `TextInput` | Búsqueda del panel (BUS-002), campos de texto en superficies | La superficie (se quita `WS_EX_NOACTIVATE` mientras dura) | A `prev`, verificada con un reintento. Si falla, **no se ejecuta nada** y se avisa «No pude volver a {app}» |
| `KeyboardNavigation` | Modo teclado y voz | El panel | Igual que `TextInput` |
| `ControlCenter` | Abrir el CC desde el panel o la bandeja; cerrarlo (CCM-004) | La ventana del CC | Al cerrar el CC, a la app anterior a su apertura, verificada con un reintento |
| `TryNowTarget` | «Probar ahora» (PRB-004) | La app destino | Al CC; si falla, `FlashTaskbar(CC)` (PRB-007) |
| `TrayMenu` | Menú contextual de la bandeja | `TrayMenuHost` (ventana propia oculta de nivel superior, `WS_EX_TOOLWINDOW`, 0×0 fuera de pantalla) | A `prev` tras `TrackPopupMenuEx` + `PostMessage(WM_NULL)`, verificada |

**Reglas de concurrencia:**
- Solo hay una concesión activa.
- Una nueva sustituye a la anterior sin restaurar entre medias, porque se hereda su `prev`.
- `TrayMenu` tiene prioridad sobre todas.
- Cualquier concesión termina con un evento terminal del motor o cuando el usuario cambia de app (época distinta).

**Escalera de derechos de primer plano por origen.** Windows solo permite `SetForegroundWindow` al proceso que recibió el último evento de entrada, o a uno autorizado.

| Origen | Paso 1 | Paso 2 (si el 1 falla) | Paso 3 |
|---|---|---|---|
| `Touch`, `Tray`, `GlobalHotkey` | `TrySetForeground`, que funciona porque Clícalo recibió la entrada o el `WM_HOTKEY`; un reintento | — | `Denied` + aviso *live* |
| `UiaInvoke` (Acceso por voz, Narrador, Reconocimiento de voz de Windows, conmutadores) | `TrySetForeground` | **Atajo interno de derechos.** El motor inyecta, mediante el *ledger* y como efecto interno, una combinación reservada y registrada con `RegisterHotKey` (Ctrl+Alt+Shift+F24; ninguna app la recibe porque la consume el sistema). Al llegar `WM_HOTKEY`, Clícalo tiene derecho de primer plano y se reintenta | `Denied` + aviso *live* con la alternativa (atajo global configurable) |
| `Internal` (temporizadores de «Probar ahora») | `TrySetForeground` | — | Según el tipo: `Flashed` o `Failed` |

Quedan prohibidos `AttachThreadInput` con el hilo del primer plano, el truco de enviar Alt y `LockSetForegroundWindow`: son frágiles, pueden bloquear o abren menús en la app destino.

**Ciclo de `TextInput`:**
1. **Adquirir.** Se aplica la escalera. Con la concesión concedida, `ISurfaceActivationStyle.AllowActivation`.
2. **Entrada de texto.** `Keyboard.Focus(TextBox)` y después `IInputPaneInterop`/`ITipInvocation` (teclado táctil) o Win+H. Si el teclado táctil tapa el panel, `WindowPlacement` sube el panel (EC-BUS-01).
3. **Fin de la concesión.** Ocurre al ejecutar el resultado, al cerrar, tras 30 s sin interacción (`timings.json`) o cuando el usuario cambia de app. Se restaura verificando `GetForegroundWindow() == prev` (con un reintento) y se vuelve a poner `WS_EX_NOACTIVATE`.
4. **Ejecutar un resultado de búsqueda.** Primero se restaura el primer plano. Después la acción viaja con `RequiredForeground = prev`. Si la restauración falla, **no se envía nada**.

**«Probar ahora» como caso de uso** (`Application.UseCases.TryNow.TryNowUseCase`, con `TimeProvider` y los tiempos de `timings.json`):

```
t0      Engine: SuppressSwitchHandling(3 s, token) (PRB-006) · InteractionStore.TryNow = Running
        CC.HidePassive · Acquire(TryNowTarget, target, Touch)   ─ Denied → aviso y CC restaurado
t0+0,9 s Engine.Activation(shortcut, Origin: TryNow, RequiredForeground = target)
t0+2,6 s Acquire(ControlCenter) → mostrar el CC
         ─ si TrySetForeground falla → FlashTaskbar(CC) (PRB-007) · InteractionStore.TryNow = Done(result)
```

**Modo teclado y voz (mitigación de accesibilidad).**
- Usa la concesión `KeyboardNavigation`.
- Se entra con un atajo global configurable (desactivado por defecto) o con el botón «Modo teclado». Se sale con Esc o después de ejecutar una acción.
- Si S3 demuestra que Acceso por voz no llega a la ventana no activable, este es el «modo voz», con su propio criterio: 20 de 20 ciclos sin dejar el foco en Clícalo.
- Entra en la sesión como `InteractionMode.Voice` y no cambia el dominio.

**Centro de control y bienvenida.** Son ventanas normales y activables, que se abren y cierran con la concesión `ControlCenter`. Como `DisableStylusAndTouchSupport` afecta a todo el proceso:
- el toque llega como mouse promovido;
- el desplazamiento táctil lo aporta `PointerInputSource` (paneo que mueve el `ScrollViewer`);
- `TouchKeyboardInvoker` abre el teclado táctil cuando un campo recibe el foco por toque.

Todo esto se valida en S2 y S4.

### 3.7 Colocación y orden Z

- `WindowPlacementService` trabaja en **píxeles físicos** con `MonitorFromPoint`, `GetMonitorInfo` (`rcWork`) y `GetDpiForMonitor`.
- Recoloca las superficies con `SWP_NOACTIVATE` ante `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `WM_SETTINGCHANGE(SPI_SETWORKAREA)`, `ABN_POSCHANGED` y `WTS_SESSION_UNLOCK`.
- El identificador persistente de monitor es `DISPLAYCONFIG_TARGET_DEVICE_NAME.monitorDevicePath`. Si no está disponible, se usa el hash de EDID con el nombre del dispositivo.
- La regla de colocación (`PanelGeometry.Place`: margen de 8, dentro de `rcWork`, posición guardada por monitor y paso al principal si el monitor ya no existe) es pura y está en Domain.

---

## 4. Arquitectura lógica

### 4.1 Capas

```
                         ┌──────────────────────────────┐
                         │         Clicalo.App          │  raíz de composición, arranque,
                         │ (Program, DI, ciclo de vida) │  enrutadores, flags de manifiesto
                         └──┬─────────┬──────────┬──────┘
          ┌─────────────────▼┐ ┌──────▼─────────┐ ┌▼──────────────────┐
          │  Clicalo.UI.Wpf  │ │ Clicalo.       │ │ Clicalo.          │
          │ vistas, peers,   │ │ Platform.      │ │ Infrastructure    │
          │ Windowing,       │ │ Windows        │ │ persistencia, IA, │
          │ Pointer, Theming │ │ adaptadores    │ │ actualizaciones,  │
          └────────┬─────────┘ │ Win32          │ │ logs, catálogos   │
                   │           └──────┬─────────┘ └──────┬────────────┘
          ┌────────▼─────────┐        │  implementan     │
          │ Clicalo.         │        │  puertos         │
          │ Presentation     │        │                  │
          │ VMs, sin WPF     │        │                  │
          └────────┬─────────┘        │                  │
          ┌────────▼──────────────────▼──────────────────▼─────┐
          │               Clicalo.Application                  │
          │ casos de uso, puertos, DocumentStore, Interaction- │
          │ Store, EngineHost, ForegroundOrchestrator,         │
          │ proyecciones, avisos, coordinadores, Localization  │
          └───────────────────────┬────────────────────────────┘
          ┌───────────────────────▼────────────────────────────┐
          │                  Clicalo.Domain                    │
          │ modelo, invariantes, comandos, EngineReducer,      │
          │ DimPolicy, reglas puras · net10.0, solo BCL        │
          └────────────────────────────────────────────────────┘
   Clicalo.Platform.Core (AOT-safe): KeyLedger, InjectionGate, LowLevelInjector, contratos IPC,
   TrustVerifier ─ lo usan Platform.Windows, Infrastructure, Clicalo.Sentinel y Clicalo.Launcher
```

### 4.2 Proyectos y reglas de dependencia

| Proyecto | TFM | Puede depender de | Nunca de |
|---|---|---|---|
| `Clicalo.Domain` | net10.0 | BCL (incluidos `Immutable` y `Frozen`) | Cualquier otro proyecto o paquete; `System.IO`, `System.Net`, `Microsoft.Win32` |
| `Clicalo.Application` | net10.0 | Domain, `Microsoft.Extensions.Logging.Abstractions` | Presentation, UI, Platform, Infrastructure, WPF |
| `Clicalo.Presentation` | net10.0 | Application, Domain, CommunityToolkit.Mvvm | `System.Windows*`, `PresentationCore`, `PresentationFramework`, `WindowsBase`, `System.Xaml`, Platform, Infrastructure |
| `Clicalo.UI.Wpf` | net10.0-windows… | Presentation, Application (solo tipos de puerto de UI), Domain, WPF, CsWin32 (solo `Windowing/` y `Pointer/`) | Platform.Windows, Infrastructure |
| `Clicalo.Platform.Core` | net10.0-windows… | BCL y CsWin32; `IsAotCompatible=true` | Todo lo demás |
| `Clicalo.Platform.Windows` | net10.0-windows… | Application (puertos), Domain, Platform.Core, CsWin32, proyecciones WinRT | Presentation, UI, Infrastructure, WPF |
| `Clicalo.Infrastructure` | net10.0-windows… | Application (puertos), Domain, Platform.Core (solo `Trust`), System.Text.Json, Serilog, Velopack, M.E.AI | Presentation, UI, Platform.Windows, WPF |
| `Clicalo.App` | WinExe | Todos | Nada lo referencia |
| `Clicalo.Sentinel` | WinExe, Native AOT | Platform.Core | Todo lo demás |
| `Clicalo.Launcher` | WinExe, Native AOT | Platform.Core (`Trust`) | Todo lo demás |

**Notas sobre la agrupación:**
- **Localización y catálogos no tienen ensamblado propio.** `MessageKey` (generado) y los tipos de catálogo viven en Domain. `ILocalizer` y el formateador CLDR puro viven en `Application.Localization`. La carga de JSON está en Infrastructure.
- **Contratos de red.** El contrato de plantillas de IA es `data/schemas/ai-template.v1.schema.json`. En la 2.0 su único consumidor es `Infrastructure.Ai`, que valida la estructura contra el esquema y después pasa la validación semántica de Domain (`TemplateSchema`: teclas del catálogo, solo Pulsar). Cuando se construya el proxy (ADR-0014), el validador estructural se extrae a `Clicalo.Contracts.Templates` (net10.0, AOT, sin dependencias), que compartirán cliente y proxy. **El proxy nunca referenciará Domain.** La petición lleva `contractVersion` y el servidor mantendrá a la vez las dos últimas versiones.
- **Puntos de extracción.** Un módulo se extrae a su propio ensamblado cuando supera unas 15 000 líneas, cuando la compilación incremental pasa de 60 s o cuando tiene un equipo propio. Los espacios de nombres ya tienen su forma final (`Clicalo.Domain.KeySafety`…), así que la extracción no cambia dependencias (ADR-0002).

### 4.3 Módulos por capacidad

Cada módulo es un espacio de nombres `Clicalo.<Capa>.<Módulo>` presente en las capas que lo necesiten. Su **API pública** son los tipos `public` de la raíz del módulo. Los detalles son `internal` o viven en `.Internal`.

```
Fundamentos   Primitives · Geometry · Messages(generado) · Errors · Privacy · Keys · Catalog
Núcleo        Library (perfiles, atajos, acciones) · Settings
Reglas        ProfileResolution · Frequents · Duplicates · Search · KeySafety · StickyModifiers
              Touch · PanelLayout · VoiceNumbering · Icons · Dimming · Interaction
Ejecución     Execution (ActivationPolicy, planificadores por ActionKind, EngineReducer)
Datos         Migration.V1 · Templates · Sharing
```

| Módulo de Domain | Depende de |
|---|---|
| Primitives, Geometry, Messages, Errors, Privacy | — |
| Keys | Primitives |
| Catalog | Keys, Primitives |
| Library | Keys, Catalog, Primitives, Messages, Privacy |
| Settings | Primitives |
| ProfileResolution | Library, Settings |
| Frequents | Library |
| Duplicates | Library, Keys |
| Search | Library, Catalog |
| KeySafety | Keys |
| StickyModifiers | Keys, KeySafety |
| Touch | Geometry, Primitives |
| PanelLayout | Settings, Geometry |
| VoiceNumbering | PanelLayout |
| Icons | Catalog, Keys |
| Dimming (`DimPolicy`) | Settings, Primitives |
| Interaction (`NoticeQueue`, `CaptureState`, `TestModeState`) | Messages, Primitives, Library |
| Execution | Library, KeySafety, StickyModifiers, Touch, Settings, Messages, Errors, Geometry |
| Migration.V1 | Library, Keys, Catalog, Settings |
| Templates, Sharing | Library, Catalog, Keys |

La matriz está en `architecture/domain-modules.json` y es acíclica. Si aparece una arista que no está declarada, la CI falla.

### 4.4 Cómo se hacen cumplir las reglas

Hay seis mecanismos. Todos se ejecutan en la CI y todos bloquean.

1. **Lista blanca de referencias.** El target `ClicaloVerifyReferences` de `Directory.Build.targets` (`BeforeTargets="ResolveProjectReferences"`) compara `ProjectReference` y `PackageReference` con `architecture/allowed-dependencies.json`. Falla antes de compilar.

2. **ArchUnitNET** (`tests/Clicalo.Architecture.Tests`):
   - Presentation no usa `System.Windows.*`.
   - Solo `Application.Engine`, `Platform.Windows` y `App` dependen de `IInputInjector`.
   - Solo `Application.Foreground` depende de `IForegroundControl`.
   - Ningún módulo usa el `.Internal` de otro.
   - `Application.Ipc` no depende de `Engine` ni de `Foreground`.
   - Solo `Execution` y el editor del CC llaman a `SecretText.WithRevealed`.
   - Los puertos viven en `Application.Ports`.
   - `ModuleMatrixTests` compara con la matriz de §4.3.
   - Solo el rol Surfaces escribe en `SessionStore` e `InteractionStore`.

3. **APIs prohibidas** (`BannedSymbols.<Capa>.txt`):

| Prohibido | Dónde | Alternativa |
|---|---|---|
| `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Stopwatch.StartNew`, `Task.Delay(TimeSpan)` sin `TimeProvider`, `Thread.Sleep`, `Guid.NewGuid`, `Random.Shared` | Todo `src` salvo adaptadores | `TimeProvider`, `IIdGenerator` |
| `Task.Result`, `Task.Wait`, `GetAwaiter().GetResult()` | Todo `src` salvo `App/Shutdown` | `await` |
| `Process.Start`, `ProcessStartInfo` | Todo salvo `Platform.Windows/Launch` | `ILauncher` (sin intérprete, LOG-008) |
| `File.Write*`, `File.Create`, `FileStream` en escritura | Todo salvo `Infrastructure/Persistence/AtomicFile.cs` y `Infrastructure/Logging/FixedNameRollingFileSink.cs` | `IAtomicFileWriter` |
| `Environment.Exit`, `Application.Shutdown` | Todo salvo `App/Lifecycle` | `IAppLifetime.ExitAsync` (garantiza Soltar todo) |
| `Console.*`, `System.IO.*`, `Environment.*` | Domain; `Console` en todo `src` | `ILogger` y puertos |
| `SetForegroundWindow`, `AllowSetForegroundWindow`, `AttachThreadInput`, `LockSetForegroundWindow` | Todo salvo `Platform.Windows/Foreground/ForegroundControl.cs` (y `AttachThreadInput`/`LockSetForegroundWindow` en ningún sitio) | `IForegroundOrchestrator` |
| `Window.Activate`, `Window.Focus` sobre ventanas | Todo `src` | Concesión `ControlCenter` |
| `TrackPopupMenu`, `TrackPopupMenuEx` | Todo salvo `Platform.Windows/Tray/TrayMenuHost.cs` | Concesión `TrayMenu` |
| `PInvoke.SendInput` | Todo salvo `Platform.Core/Injection` (detrás de `InjectionGate`) | `IInputInjector` |
| `ShellExecute*`, `IShellDispatch2`, WMI (`System.Management`) | Todo salvo `Platform.Windows/Launch` y `Platform.Windows/SystemCommands` (hilo Shell) | `ILauncher`, `ISystemCommandRunner` |

4. **Analizadores propios** (`generators/Clicalo.Analyzers`, prefijo `CLC`). Cada uno tiene su página en `docs/guides/analyzers.md` y una corrección automática cuando es viable.

| Id | Regla |
|---|---|
| CLC0001 | En derivadas de `NonActivatingWindow`, prohibidos `Show()` activador, `Activate()` y `ShowActivated = true` |
| CLC0002 | En el XAML de `Surfaces/`, prohibidos `Popup`, `ContextMenu`, `ToolTip` interactivo y `ComboBox` |
| CLC0003 | Un valor `[Sensitive]` (`SecretText`, `Sensitive<T>`, `WindowTitle`, `ApiKey`, `CapturedKey`, `SearchQuery`) no puede llegar a `ILogger`, a una interpolación de log ni a una excepción |
| CLC0004 | En Application y Presentation, ningún literal de milisegundos fuera de `Timings` (NFR-020) |
| CLC0005 | Un control interactivo sin `AutomationProperties.Name` localizado es un error |
| CLC0006 | Texto visible literal en XAML o en VM (`*Label`, `*Title`, `*Message`), y colores `#RRGGBB` literales |
| CLC0007 | `MinWidth` o `MinHeight` < 44 en un control con *hit test* sin `TouchTarget.Expand` |
| CLC0008 | Una prueba fuera de su proyecto de capa o sin `[Trait("Layer", …)]` |
| CLC0009 | `async void` fuera de manejadores de eventos de UI, y *fire-and-forget* sin `TaskSupervisor` |
| CLC0010 | Un `IDestructiveCommand` o un caso de uso `[Destructive]` solo puede despacharse con un `ConfirmationToken` (que solo crea `TwoStepConfirm`). Pasarlo a la sobrecarga sin *token* es un error |

5. **Prueba de facetas de `ActionKind`.** Recorre todos los subtipos de `ShortcutAction` y exige que cada uno tenga sus facetas registradas en todas las capas: planificador (Domain), validador de completitud (Domain), mapeador DTO (Infrastructure), editor (Presentation), vista (UI.Wpf) y claves de texto en ES y EN. Es la forma de tener exhaustividad mientras C# no tenga uniones discriminadas.

6. **Pruebas de reglas de producto** (`Architecture.Tests/ProductRules`):
   - **R4 (REG-04):** todo comando destructivo implementa `IDestructiveCommand`. Una lista cerrada en `architecture/destructive-operations.json` exige que existan y lo implementen: `DeleteShortcut`, `DeleteProfile`, `DeleteDuplicate`, `ResetFrequents`, `ReplaceOnImport`, `RestoreBackup`, `DeleteMacroStep` y los casos de uso `RollbackVersion`, `UninstallKeepOrDeleteData` y `UninstallSystemComponent`. Un comando cuyo `Apply` elimina entidades o vacía listas (detectado con CsCheck sobre documentos generados) y no está en la lista hace fallar la prueba.
   - **R7 (REG-07):** la prueba recorre todos los `IDocumentCommand` y los aplica a documentos generados. Exige `UndoIntent.Record`, salvo los que figuran en `architecture/undo-exemptions.json` con su justificación (`RecordUsage`, posición del panel, ajustes cuyo descriptor tenga `Undoable = false`, `FinishOnboarding`).
   - **R5 (REG-05, ACC-011):** es la regla UIA010 (§10.2). Todo campo de texto libre tiene un botón hermano «Dictar» (🎤), o «Pegar» en el campo de la clave de IA.

---

## 5. Estructura del repositorio y de la solución

Un monorepo, `clicalo`. Se descarta el multirepo porque hay un solo producto con un solo ciclo de publicación, y la trazabilidad entre requisitos y pruebas exige un único repositorio.

```
clicalo/
├─ .github/
│  ├─ workflows/   pr.yml · main.yml · lab.yml · beta.yml · release.yml · release-core.yml
│  │               release-publish.yml · patch-tuesday.yml · codeql.yml · scorecard.yml · docs.yml
│  ├─ ISSUE_TEMPLATE/  bug.yml · a11y-barrier.yml · feature.yml · template-request.yml
│  │                   translation.yml · release-verification.yml · config.yml
│  ├─ PULL_REQUEST_TEMPLATE.md · CODEOWNERS · renovate.json5
├─ .config/dotnet-tools.json         csharpier, dotnet-cyclonedx, vpk
├─ .vscode/                          extensions · tasks (una por orden de cl) · launch · settings
├─ architecture/
│  ├─ allowed-dependencies.json · domain-modules.json · sensitive-paths.json
│  ├─ destructive-operations.json · undo-exemptions.json
│  └─ BannedSymbols.{All,Domain,Application,Presentation,Surfaces}.txt
├─ build/                            Build.csproj (Bullseye + SimpleExec): destinos de cl
├─ changes/unreleased/               fragmentos de notas de usuario ES/EN por PR
├─ data/                             FUENTE DE VERDAD NO-CÓDIGO (validada contra schemas/)
│  ├─ catalogs/   keys.json · keys.win32.json · mouse.json · categories.json · icons.json
│  │              combo-icons.es.json · combo-icons.en.json · blocked-combos.json
│  │              system-commands.json · touch-presets.json · sizes.json · timings.json
│  ├─ content/    library.json · seed.json · templates/{word,browser,vscode,excel,…}.json
│  ├─ i18n/       strings.es.json · strings.en.json · locales.json · allow-unused.txt
│  ├─ tokens/     theme-palettes.json · extra-tokens.json · contrast-pairs.json
│  │              hc-system-map.json · motion.json
│  └─ schemas/    *.schema.json (JSON Schema 2020-12): documento, uso, compartir,
│                 ai-template.v1, release-files, manifiesto de actualización
├─ assets/fonts/  Atkinson/ · JetBrainsMono/ · MaterialSymbols/ (FILL0/1 + codepoints.json) · LICENSES
├─ docs/
│  ├─ adr/        0001-….md (MADR 4; solo decisiones difíciles de revertir)
│  ├─ architecture/ blueprint.md (este documento) · overview.md (arc42 ligero, C4 en Mermaid)
│  │              threading.md · windowing.md · foreground.md · engine.md · persistence.md
│  │              contracts.md · testing-strategy.md · tooling.md
│  ├─ requirements/ catalog.md (catálogo del paquete; §6 = preguntas y propuestas de producto)
│  │              traceability.md (generado en CI, no versionado)
│  ├─ security/   threat-model.md · privacy.md
│  ├─ guides/     dev-setup.md · voice-and-touch-workflow.md · testing.md · i18n.md
│  │              design-tokens.md · analyzers.md · release-runbook.md
│  ├─ runbooks/   bad-release.md · key-compromise.md · stuck-key-report.md
│  ├─ testing/    manual-a11y-script.md · touch-acceptance.md (docs/09 en hardware real)
│  │              spikes/S0…S15.md
│  └─ design/handoff/  paquete original, solo lectura, con un README de qué es vinculante
├─ src/
│  ├─ Clicalo.Domain/
│  │   Primitives/ Geometry/ Messages/ Errors/ Privacy/ Keys/ Catalog/ Library/ Settings/
│  │   ProfileResolution/ Frequents/ Duplicates/ Search/ KeySafety/ StickyModifiers/ Touch/
│  │   PanelLayout/ VoiceNumbering/ Icons/ Dimming/ Interaction/ Execution/ Migration/V1/
│  │   Templates/ Sharing/
│  ├─ Clicalo.Application/
│  │   Ports/ Store/ Session/ Interaction/ Engine/ Foreground/ Projections/ Notices/
│  │   Localization/ Ipc/ UseCases/<Módulo>/ Coordinators/ Routing/
│  ├─ Clicalo.Presentation/
│  │   Panel/ Dock/ Bubble/ SideWindows/ ControlCenter/<Sección>/ Welcome/ Common/
│  ├─ Clicalo.UI.Wpf/
│  │   Windowing/ Pointer/ Surfaces/ Workspace/ Controls/ Automation/ Theming/ Localization/
│  ├─ Clicalo.Platform.Core/         KeyLedger/ Injection/ Ipc/ Trust/
│  ├─ Clicalo.Platform.Windows/
│  │   SysEvents/ Input/ Foreground/ PointerTracking/ Session/ Clipboard/ Launch/
│  │   SystemCommands/ Apps/ Startup/ Elevation/ SystemComponent/ SingleInstance/ Secrets/
│  │   Display/ SystemPrefs/ Tray/ Hooks/ SentinelHost/ Legacy/
│  ├─ Clicalo.Infrastructure/
│  │   Persistence/(Dto, Mappers, Migrations, JsonContext, AtomicFile, Usage) Backup/ Catalogs/
│  │   Localization/ Ai/ Updates/ Logging/ Diagnostics/
│  ├─ Clicalo.App/                   Program, Composition, Lifecycle, Routing registrations
│  ├─ Clicalo.Sentinel/              Native AOT
│  └─ Clicalo.Launcher/              Native AOT (componente de sistema)
├─ generators/
│  ├─ Clicalo.Generators/            netstandard2.0: i18n, tokens, catálogos, timings, validador XAML
│  ├─ Clicalo.Analyzers/             netstandard2.0: CLC0001…
│  └─ Clicalo.Design.Math/           OKLCH→sRGB, gamut, WCAG (código enlazado en generadores y pruebas)
├─ tests/
│  ├─ Clicalo.Architecture.Tests/  Clicalo.Data.Tests/  Clicalo.Domain.Tests/
│  ├─ Clicalo.Application.Tests/   Clicalo.Presentation.Tests/  Clicalo.Infrastructure.Tests/
│  ├─ Clicalo.UI.Wpf.Tests/        Clicalo.Platform.IntegrationTests/  Clicalo.Windowing.IntegrationTests/
│  ├─ Clicalo.E2E/                 Clicalo.Performance/  Clicalo.Sentinel.Tests/  Clicalo.Launcher.Tests/
│  ├─ Clicalo.TestKit/             fixtures/ (pointer/, v1/, schema/<major.minor>/, render/)
├─ tools/
│  ├─ InputProbe/                  ventana Win32 instrumentada (sin WPF)
│  ├─ Clicalo.DevCli/              i18n-check, trace, notas, i18n-import, anonymize-v1, sign-manifest, states
│  └─ fonts/subset.py              fonttools, se ejecuta a mano; la salida se versiona
├─ Clicalo.slnx · Core.slnf (Domain, Application, Presentation y sus pruebas)
├─ global.json · nuget.config · Directory.Build.props · Directory.Build.targets
├─ Directory.Packages.props · .editorconfig · .csharpierrc.json
├─ cl.ps1 · cl.cmd · AGENTS.md (CLAUDE.md apunta aquí)
└─ README.md · CONTRIBUTING.md · CODE_OF_CONDUCT.md · SECURITY.md · SUPPORT.md
   PRIVACY.md · CODE_SIGNING_POLICY.md · LICENSE · CHANGELOG.md
```

`GOVERNANCE.md` y `docs/rfcs/` se crean cuando haya un segundo mantenedor (§13). `services/Clicalo.AiProxy` se crea cuando se active ADR-0014.

**Archivos raíz (lo esencial):**
- **`global.json`:** SDK exacto con `rollForward: latestPatch` y `test.runner: Microsoft.Testing.Platform`.
- **`Directory.Build.props`:**
  - `LangVersion 14`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`.
  - `Deterministic`, `UseArtifactsOutput`, `ContinuousIntegrationBuild` en CI.
  - `RestorePackagesWithLockFile`, `RestoreLockedMode` en CI.
  - `NuGetAudit` con modo `all` y nivel `low`.
  - `InvariantGlobalization=false`.
  - `VersionPrefix`, que actualiza release-please.
- **`Directory.Packages.props`:** Central Package Management con `CentralPackageTransitivePinningEnabled`. Los analizadores se declaran como `GlobalPackageReference`.
- **`nuget.config`:**
  - `<clear/>` y solo nuget.org;
  - `packageSourceMapping`;
  - `signatureValidationMode=require`;
  - `trustedSigners` con una lista explícita de propietarios. Cada dependencia nueva añade su propietario y la CI falla si falta.

---

## 6. Modelo de dominio y persistencia

### 6.1 Primitivas, teclas y errores

```csharp
namespace Clicalo.Domain.Primitives;
public readonly record struct ProfileId(string Value) { public static readonly ProfileId General = new("general"); }
public readonly record struct ShortcutId(string Value);
public readonly record struct CatalogRef(string Source, string Version, string ItemId); // DAT-004
public readonly record struct LangCode(string Value) { public static readonly LangCode Es = new("es"), En = new("en"); }
public readonly struct ValueList<T> : IEquatable<ValueList<T>>, IReadOnlyList<T> { /* ImmutableArray con igualdad estructural */ }
public sealed record LocalizedText(ImmutableSortedDictionary<LangCode, string> Values); // diccionario: admite N idiomas (IDI-006)
public interface IIdGenerator { ProfileId NewProfileId(); ShortcutId NewShortcutId(); }   // ids opacos

namespace Clicalo.Domain.Keys;
public readonly record struct KeyId(string Value)          // canónica y persistida: "ctrl", "a", "num.add", "char:ñ"
{ public bool IsCharacter => Value.StartsWith("char:", StringComparison.Ordinal); }
public enum KeySide : byte { Any, Left, Right }            // EDI-009: un único mecanismo
public readonly record struct KeyStroke(KeyId Key, KeySide Side = KeySide.Any);
public sealed record KeyChord(ValueList<KeyStroke> Strokes); // ORDEN DE PULSACIÓN (EJE-003, EDI-010)
public readonly record struct CanonicalChord(ModifierSet Modifiers, ValueList<KeyId> Main) // REP-001
{ public static CanonicalChord From(KeyChord c, KeyCatalog k); public string ToStableString(); }
public enum InjectionMode : byte { VirtualKey, ScanCode }  // D24; ScanCode = «modo compatible»
// KeyIds.Ctrl, KeyIds.A… (y KeyDefinitions.TryGet) son constantes GENERADAS desde data/catalogs/keys.json

namespace Clicalo.Domain.Errors;
public sealed record Error(string Code /* "persist.io.locked" */, MessageKey Message, ImmutableArray<MessageArg> Args,
                           Severity Severity, Recovery Recovery /* None|Retry|OpenSettings|RestoreBackup|Relaunch|Rollback */,
                           Announce Announce /* Polite|Assertive */);
public readonly record struct Result<T> { /* Ok(T) | Fail(Error); Map, Bind; sin excepciones */ }
```

**Por qué `KeyId` es semántico y no un VK.** El VK depende de la distribución de teclado: `Ñ` no tiene un VK estable entre es-ES y en-US. El dominio compara, elimina repetidos y presenta usando la identidad semántica. La traducción a VK, scancode o marca extendida se hace **al pulsar**, contra la distribución del hilo en primer plano y según el `InjectionMode` del plan. Esa tecla física (`InjectedKey`, que incluye el modo) es la que se registra, de modo que **se suelta exactamente lo que se pulsó y de la misma forma**, aunque la distribución cambie entre medias.

### 6.2 Atajos, acciones y agregado

```csharp
namespace Clicalo.Domain.Library;
public sealed record Shortcut(ShortcutId Id, LocalizedText Name, IconRef Icon, bool AutoIcon, CategoryId Category,
                              ShortcutAction Action, ShortcutOptions Options, CatalogRef? Origin, ProfileId? PinnedFrom);
public sealed record ShortcutOptions(bool Confirm, HoldLimit MaxHold, bool IsPrivate);
public abstract record HoldLimit { public sealed record InheritGlobal : HoldLimit; public sealed record After(TimeSpan V) : HoldLimit; public sealed record Never : HoldLimit; }

public abstract record ShortcutAction { private protected ShortcutAction() { } public abstract ActionKind Kind { get; } } // jerarquía cerrada
public sealed record TapAction(KeyChord Chord, AppsLangVariants<KeyChord>? Variants = null) : ShortcutAction;
public sealed record HoldAction(KeyChord Chord) : ShortcutAction;
public sealed record ToggleAction(KeyChord Chord) : ShortcutAction;
public sealed record TextAction(SecretText Text, TextMethod Method) : ShortcutAction;   // Unicode | Paste
public sealed record MouseAction(MouseOp Op, ScrollSpeed Speed) : ShortcutAction;
public sealed record MacroAction(ValueList<MacroStep> Steps) : ShortcutAction;
public sealed record WebAction(WebTarget Target) : ShortcutAction;     // Valid(Uri http/https) | Raw(string)
public sealed record AppAction(AppTarget Target) : ShortcutAction;     // Exe | StoreApp(aumid) | Document | Raw
public sealed record SystemAction(SystemCommandId Command) : ShortcutAction;
public abstract record MacroStep { private protected MacroStep() { } }  // KeysStep | WaitStep(100..10000 ms) | TextStep | MouseStep

public sealed class SecretText   // ToString() redactado; el cifrado en disco es cosa de la persistencia
{
    public static SecretText Unavailable { get; }
    public bool IsAvailable { get; }
    public int Length { get; }
    // Sin método que devuelva string. El contenido vive en un char[] privado (nunca en un string internado).
    public void WithRevealed<TState>(TState state, ReadOnlySpanAction<char, TState> use);
    public override string ToString() => $"[oculto · {Length} caracteres]";
}

public sealed record Profile(ProfileId Id, LocalizedText Name, IconRef Icon, bool AutoIcon, AppBinding Binding,
                             InjectionMode Injection /* persistido como "compat": bool */,
                             ValueList<Shortcut> Shortcuts, CatalogRef? Origin);
public abstract record AppBinding { public sealed record Manual : AppBinding; public sealed record Processes(ValueList<ProcessName> Names) : AppBinding; }

public sealed class Library : IEquatable<Library>   // AGREGADO RAÍZ; constructor privado
{
    public ValueList<Shortcut> AlwaysVisible { get; }  public ValueList<Profile> Profiles { get; }  public Profile General { get; }
    public bool TryLocate(ShortcutId id, out ShortcutLocation loc);        // índice Frozen perezoso
    public Result<Library> AddShortcut(ListRef list, Shortcut s, Position at);
    public Result<Library> ReplaceShortcut(Shortcut s);
    public Result<Library> RemoveShortcut(ShortcutId id);
    public Result<Library> MoveShortcut(ShortcutId id, ListRef to, Position at);
    public Result<Library> AddProfile(Profile p, Position at);
    public Result<Library> RemoveProfile(ProfileId id);
    public Result<Library> Bind(ProfileId id, ProcessName p, bool takeOver);  // ATJ-007
    internal static Result<Library> CreateValidated(/*…*/);                    // persistencia y seed
}
```

**Límite documentado de `SecretText`:**
- Sus usuarios la reciben como `ReadOnlySpan<char>`.
- El motor copia el texto a búferes alquilados que limpia con `CryptographicOperations.ZeroMemory`.
- El `char[]` interno no se limpia, porque el objeto es inmutable y compartido entre versiones del documento.
- El `TextBox` del editor necesita un `string`: es el único llamador autorizado a construirlo, y lo comprueba ArchUnit.

**Invariantes del agregado.** Se comprueban en `CreateValidated` y se mantienen en cada operación. `LibraryInvariantTests` las verifica con CsCheck sobre secuencias aleatorias.

| ID | Invariante | Cómo se garantiza |
|---|---|---|
| I1 | Ids únicos en todo el documento | Índice al construir e `IIdGenerator` |
| I2 | Un atajo vive en una sola lista | Por estructura (está anidado); `Move` es atómico |
| I3 | General y Siempre visible existen y no se pueden borrar | `RemoveProfile(General)` → `Fail` |
| I4 | General no tiene proceso | `Bind(General, …)` → `Fail` |
| I5 | Un proceso pertenece a un solo perfil (sin distinguir mayúsculas) | `Bind(takeOver:false)` → `ProcessAlreadyBound`. La UI pregunta y reintenta |
| I6 | Pasos de macro de tipo válido y esperas en rango | Sistema de tipos y constructor de `WaitStep` |

**Completo frente a válido.** Un atajo incompleto es **válido**: se puede guardar y deshacer. `ShortcutCompleteness.Evaluate` aplica ATJ-009. El motor se niega a ejecutarlo (EJE-015) y el editor lo marca.

### 6.3 Documento, ajustes y comandos

```csharp
public sealed record UserDocument(long Revision, Library Library, FrequentsState Frequents,
                                  DuplicatePolicy Duplicates, Settings Settings, OnboardingState Onboarding);
public sealed record FrequentsState(ValueList<ShortcutId> Pins, ValueSet<ShortcutId> Hidden,
                                    long UsageEpoch,                                  // lo sube ResetFrequents
                                    ImmutableDictionary<ShortcutId, UsageLog> Usage); // referencias colgantes intencionadas (FRE-005)

public sealed record SettingDescriptor(string Path, SettingScope Scope /* Presentation|Behavior|Placement */,
    bool Undoable, object Default, SettingRange? Range, MessageKey Label, MessageKey? Description);
public static class SettingsSchema { public static IReadOnlyList<SettingDescriptor> All { get; } }
// Fuente única de: valores por defecto, recorte al cargar, conversión v1, filas simples de la UI
// y pruebas (toda hoja de Settings tiene descriptor y textos ES/EN).

public interface IDocumentCommand { Result<DocumentChange> Apply(UserDocument doc, DomainContext ctx); }
public interface IDestructiveCommand : IDocumentCommand { }          // R4: exige ConfirmationToken (CLC0010)
public sealed record DomainContext(DateTimeOffset Now, IIdGenerator Ids, CatalogSet Catalogs, LangCode UiLang, LangCode AppsLang);
public sealed record DocumentChange(UserDocument Next, ImmutableArray<DomainEvent> Events,
                                    UndoIntent Undo /* Record(label, key?) | Transparent | Barrier */,
                                    BackupRequirement Backup /* None | BeforeApply(kind) */);
// Ejemplos: DeleteShortcut (destructivo), EditShortcut (Record, key = Id), PinToAlwaysVisible, InstallTemplate,
// RecordUsage (Transparent, exento), ResetFrequents (destructivo; Record + BackupBefore), SetSetting<T>
// (Undo según descriptor), MarkDuplicateAccepted, KeepOnlyInAlwaysVisible (REP-005), BindProcess,
// FinishOnboarding (un solo paso).
```

Los comandos son clases pequeñas y puras que no usan puertos. Lo que necesita E/S (instalar desde la IA, importar un archivo, volver a la versión anterior) es un **caso de uso** de Application: prepara un plan puro y despacha el comando. Los casos de uso destructivos reciben un `ConfirmationToken`.

### 6.4 Estado: cuatro dueños, deshacer y autoguardado

| Estado | Dueño e hilo | Persistencia | Deshacer |
|---|---|---|---|
| `UserDocument` | `DocumentStore` (escritor único con *lock*; lectura sin *lock*) | Sí (documento + archivo de uso) | Sí, por porciones |
| `PanelSession` | `SessionStore` (rol Surfaces) | No, salvo lo que es ajuste (`lastProfile`, posiciones) | No |
| `InteractionState` | `InteractionStore` (rol Surfaces; publica `InteractionSnapshot` hacia Workspace) | No | No |
| `WorkspaceSession` (editor, borradores, sección del CC) | Rol Workspace | No | No (el borrador vive aquí) |
| `EngineState` → `EngineSnapshot` | `EngineHost` (solo Engine) | No; lo que debe sobrevivir a un fallo va al *ledger* | No |

**La sesión no admite estados inválidos (PAN-001 y PAN-008):**

```csharp
public sealed record PanelSession(PanelPresence Presence /* Visible|Bubble(restoreTo)|Hidden */,
    ViewTarget View /* Frequents | Profile(id); nunca Siempre visible */, PageState Pages,
    PrimaryLayer Layer /* None | Search(q, origin) | QuickSettings | ProfilePicker: UN solo campo */,
    ContextMenuState? ContextMenu, bool EditMode, DockSession Dock,
    ForegroundApp Foreground, ValueSet<ProcessName> DismissedSuggestions, LayoutFreeze Freeze, InteractionMode Mode);
```

Las transiciones las hace `PanelSessionReducer.Reduce(session, action, ctx)`, una función pura cubierta por una **tabla de transiciones**.

**`InteractionState`: el estado transversal a varias superficies.**

```csharp
namespace Clicalo.Domain.Interaction;
public sealed record InteractionState(
    NoticeQueue Notices,          // AVI-002: prioridades; la leen el panel, la Pestaña (PES-014) y la barra de estado del CC (CCM-003)
    CaptureState Capture,         // ATJ-008: lo inicia el CC, se cancela desde el panel o el CC
    TestModeState TestMode,       // TAC-008: 30 s, visible en el panel y en ajustes rápidos
    TryNowState TryNow,           // PRB-*
    DimState Dim,                 // GEN-009 / docs/04
    LeaseView ActiveLease,        // espejo de solo lectura de la concesión de primer plano
    long Version);
public static class InteractionReducer { public static InteractionState Reduce(InteractionState s, InteractionAction a, DateTimeOffset now); }
```

- Workspace nunca escribe en este estado. Envía `InteractionAction` al rol Surfaces (por ejemplo `StartCapture` o `CancelCapture`), que la aplica y publica.
- Con un único dispatcher, esto es una llamada directa. Con dos, es un `BeginInvoke` y el protocolo es el mismo.

**`DimPolicy` (Domain.Dimming, pura).**

```csharp
public readonly record struct DimInputs(bool AutoDim, double Opacity, double DimTo, SurfaceKind Surface,
    bool PointerInside, DateTimeOffset? LastLeave, DimExceptions Active, bool ReduceMotion, DateTimeOffset Now);
[Flags] public enum DimExceptions { None = 0, Panic = 1, QuickSettings = 2, ContextMenu = 4, ProfileGrid = 8,
    Search = 16, EditMode = 32, DockSideWindows = 64, ControlCenterOpen = 128, WelcomeOpen = 256 }
public static class DimPolicy
{
    // Atenuado a DimTo 2,5 s después de que el dedo o el puntero salgan (Timings.Dimming.DimDelay);
    // se recupera al tocar o al pasar el cursor; nunca con cualquier bit de DimExceptions;
    // la burbuja nunca baja del 55 %; con pánico, la burbuja va al 100 %; transición 350 ms (0 con reduceMotion).
    public static DimDecision Evaluate(in DimInputs i);   // → TargetOpacity, Transition, NextEvaluationAt?
}
```

- **EJE-017: el primer toque despierta *y* ejecuta.** El atenuado es solo visual. Ninguna capa de entrada consume un toque para «despertar», y lo comprueba una propiedad: para todo `DimState`, `ActivationPolicy.Decide` da el mismo resultado.
- **SEG-002:** la franja de pánico y su superficie nunca se atenúan.
- `DimPolicyTests` usa la **tabla completa de excepciones** de docs/04, con `[Trait("Req", "GEN-009")]`, `[Trait("Req", "EJE-017")]` y `[Trait("Req", "SEG-002")]`.

**`DocumentStore` y deshacer por porciones (20 entradas):**

```
Dispatch(cmd) / Dispatch(destructiveCmd, ConfirmationToken):  lock
  change = cmd.Apply(current, ctx)          ─ falla → se devuelve el error y no hay efectos
  si change.Backup = BeforeApply(k): backups.SnapshotNow(current, k)   (memoria; el disco va a la cola)
  slices = SliceDiff.Touched(current, change.Next)   ← por REFERENCIA: Library, Frequents.Curation,
                                                        Frequents.Usage, Duplicates, Settings.Behavior, Onboarding
  Record(label,key): si la cima no está sellada y cima.Key == key → se agrupa (se conserva el Before original)
                     si no → push UndoEntry(Before=current, slices, label, key); trim(20)
  Transparent: no toca la pila (uso, presentación, posiciones) · Barrier: vacía la pila
  current = change.Next with { Revision + 1 } ; Changed(old, current, slices, events)
Undo(): e = pop ; current = current.RestoreSlices(e.Before, e.Slices) with { Revision + 1 }
SealCoalescing(): al cambiar de atajo o perfil en el editor (EDI-021)
```

- Restaurar solo las porciones tocadas es lo que hace que deshacer «borrar atajo» **no** borre los usos registrados después. `ResetFrequents` sí toca `Usage` (y sube `UsageEpoch`), así que su deshacer restaura usos, fijados y ocultos (FRE-004).
- **Borrador sin rastro (ATJ-011).** «Crear el mío» solo crea un `EditorDraft` en la sesión del CC. La primera edición significativa despacha `CreateShortcut` con `Record(key: newId)`. Descartar un borrador vacío no deja nada en la pila (EC-EDI-04).
- Rehacer lo admite la estructura, pero la UI no lo expone porque ningún requisito lo pide.
- Coste: gracias a la compartición estructural, 20 entradas con 2000 atajos ocupan decenas de KB.

**Autoguardado.** `DocumentStore.Changed` → `PersistenceScheduler`, que enruta por porción:
- **Porciones significativas** (todas salvo `Frequents.Usage`): *debounce* de 500 ms y latencia máxima de 2 s → `clicalo.json`. Un cambio que solo toca `Revision` no cuenta. Estas porciones son también las **únicas que disparan la copia automática** (§6.8).
- **`Frequents.Usage`:** *debounce* de 30 s y latencia máxima de 5 min (`timings.json`) → `usage.json`. Nunca reescribe el documento ni programa copias.
- Ambos pasan por un `Channel<SaveRequest>` con un único consumidor (`IDocumentRepository`, `IUsageRepository`).
- **Garantía:** 10 cambios en 1 s producen como máximo una escritura del documento, y 1000 ejecuciones no producen ninguna escritura del documento.
- **Vaciados síncronos de ambos:** al suspender, al cerrar sesión, al salir, al relanzar y al instalar una actualización.

### 6.5 Persistencia

**Ubicaciones:**

```
%AppData%\Clicalo\                        (Roaming: sobrevive a desinstalar; Known Folder Move no lo redirige)
  clicalo.json · clicalo.json.prev · usage.json · usage.json.prev
  logs\clicalo.log (+ clicalo.1.log … clicalo.4.log)       (docs/08, LOG-001, ACE-003)
  backups\auto\ (12) · manual\ (sin límite) · pre-update\ pre-migrate\ pre-restore\ pre-import\ pre-reset\ (10 c/u)
  backups\v1-original-<fecha>.json         (copia byte a byte; nunca se borra sola)
  quarantine\clicalo.<fecha>.json.corrupt
%LocalAppData%\Clicalo\   diag\ · crash\ (volcados opt-in) · pending\ (guardado de emergencia) · crash-journal.json
%LocalAppData%\Clicalo.App\  ← instalación de Velopack (packId distinto: desinstalar nunca toca los datos)
%ProgramFiles%\Clicalo\      ← solo con el componente de sistema: Clicalo.Launcher.exe · app\<versión>\ · trust-state.json
```

- Los archivos de datos que crea una instancia elevada reciben una DACL explícita con el SID del usuario como propietario. Así, la instancia de integridad media puede reemplazarlos después, y hay una prueba de integración para este caso.
- Una instancia elevada **nunca** escribe en `%LocalAppData%\Clicalo.App` (§3.3, regla 5).

**Envoltorio del documento:**

```jsonc
{ "format": "clicalo.document", "schema": { "major": 1, "minor": 0 }, "writtenBy": "2.0.0",
  "seq": 1842, "writtenAtUtc": "2026-09-25T10:31:02Z", "payloadSha256": "…",
  "payload": { "settings": {…}, "always": {…}, "profiles": [ … ], "frequents": { "pins": […], "hidden": […], "usageEpoch": 3 },
               "dupIgnored": [ … ], "onboarding": {…} } }
```

**`usage.json`:**
- Formato: `{ "format": "clicalo.usage", "schema": {…}, "usageEpoch": 3, "seq": …, "usage": {…} }`.
- Al cargar, si `usageEpoch` no coincide con el del documento, el uso se descarta. Es el caso de un `ResetFrequents` interrumpido entre dos escrituras.
- Si `usage.json` falta o es ilegible, el uso empieza vacío, se registra `usage.reset_on_load` y no se pone nada en cuarentena. Es un dato no crítico, y su pérdida solo afecta a Frecuentes.

**Reglas del documento:**
- **Minor = cambio aditivo.** Una versión anterior con el mismo major lee el documento y conserva los campos desconocidos (`[JsonExtensionData]` en la raíz, el perfil, el atajo y los ajustes). Por eso volver a N−1 no pierde nada.
- **Major = migración.** Una versión que encuentra un major mayor que el suyo **no escribe nunca**: entra en solo lectura y ofrece la copia `pre-update`.
- **El hash detecta daños, no aporta seguridad.** Si el hash falla pero el documento se valida, se acepta con el registro `doc.edited_externally`.
- **DTOs separados del dominio.** `UserDocumentDto` (con `JsonSerializerContext`) solo existe en Infrastructure. `DtoMapper` valida y repara lo reparable (por ejemplo un `lastProfile` colgante o ajustes fuera de rango) y construye con `Library.CreateValidated`.

**Protocolo de escritura** (igual para ambos archivos):
1. Serializar, calcular SHA-256 y `seq++`.
2. **Validar** releyendo los bytes con `DocumentValidator` (ida y vuelta). Un documento inválido **no se guarda**: se conserva el último válido y se avisa.
3. Escribir `*.tmp` con `FILE_FLAG_WRITE_THROUGH` y `FlushFileBuffers`.
4. `ReplaceFileW(destino, tmp, destino.prev)`. Si es la primera escritura, `MoveFileEx(MOVEFILE_WRITE_THROUGH)`.
5. Si solo es el documento y alguna porción significativa cambió, se programa la copia automática (§6.8).

Errores transitorios (`SHARING_VIOLATION`, `LOCK_VIOLATION`, `ACCESS_DENIED` transitorio): reintentos a 50, 100, 200, 400 y 800 ms. Si el error persiste:
- aparece un estado «no guardado» **visible** en el panel y en la barra de estado del CC (DAT-002), a través de `InteractionState.Notices`;
- se reintenta cada 30 s;
- se hace una copia de emergencia en `pending\`.

**Carga y recuperación (DAT-003, SIS-004):**

```
clicalo.json → parse → schema (major ≤ soportado) → validación semántica
  ├ OK ─► listo (+ cargar usage.json con comprobación de usageEpoch)
  ├ reparable (id duplicado, referencia rota) ─► RepairMigration + informe + copia pre-repair
  ├ major futuro ─► solo lectura + ofrecer pre-update
  └ ilegible o truncado ─► MOVER a quarantine\ (nunca borrar) ─► probar .prev ─► probar la copia
       válida más reciente (por seq desc) ─► si no hay nada: documento por defecto EN MEMORIA
       + aviso persistente; no se escribe hasta que el usuario lo acepte
```

**Por qué JSON y no SQLite.** El documento pesa menos de 2 MB con 2000 atajos y se carga entero para las proyecciones. Una copia de seguridad es copiar un archivo. Es legible, se puede comparar entre versiones y es atómico con `ReplaceFileW`. Separar el uso elimina la única fuente de escrituras frecuentes. Se reconsiderará si NFR-016 crece 10 veces.

### 6.6 Migraciones, incluida v1 → v2

```csharp
public interface IDocumentMigration
{
    SchemaVersion From { get; } SchemaVersion To { get; }
    JsonObject Apply(JsonObject document, MigrationReport report);   // pura, idempotente, sobre JsonObject
}
public sealed class MigrationRunner(IReadOnlyList<IDocumentMigration> chain, IBackupStore backups, TimeProvider time)
{ public ValueTask<MigrationOutcome> RunAsync(JsonObject doc, CancellationToken ct); } // copia pre-migrate ANTES
```

- La cadena es contigua. Hay pruebas de «sin huecos ni ciclos» y de `Apply(Apply(x)) == Apply(x)`.
- **Fixtures inmutables por versión** en `tests/fixtures/schema/<major.minor>/`. Cada migración nueva se prueba desde **todas** las versiones anteriores, con instantáneas de TestKit.

**Importación desde Macro Quick Access (v1 → documento 1.0 de Clícalo 2.x):**
- **Es una etapa aparte, no un eslabón de la cadena de migraciones.**
- `ILegacyInstallLocator` encuentra las instalaciones v1 (MIG-001).
- **Flujo:** copia byte a byte a `backups\v1-original-*` → `V1Reader` (tolera claves desconocidas, BOM y archivos truncados) → `V1Document` → `V1Converter.Convert(v1, KeyCatalog, IIdGenerator) → (UserDocument, MigrationReport)`. Es pura e idempotente.
- **Límites de `.zip` (MIG-009, LOG-006), en `SafeZipReader`:**

  | Límite | Valor |
  |---|---|
  | Tamaño total descomprimido | ≤50 MiB |
  | Número de entradas | ≤1000 |
  | Relación de compresión por entrada | ≤100:1, contada al leer, no confiando en la cabecera |
  | Tamaño de cada JSON | ≤5 MiB |

  Además:
  - sin rutas con `..`, absolutas ni con unidad;
  - no se siguen zips anidados;
  - la lectura es en streaming con contador y aborta al superar un límite.

  Los valores están en `timings.json`/`limits`.
- **Tokenizador `V1ComboTokenizer`:** implementa la gramática del catálogo de requisitos (acordes, `ctrl++`, `num+`, alias, U+2212).
- **Repetidos (MIG-008):** se calculan con `DuplicateIndex` sobre el resultado y se añaden a `dupIgnored` con su informe.
- **Pruebas:**
  - los 3 `profiles.json` reales anonimizados (`tools/anonymize-v1` conserva las combinaciones), los 2 respaldos por idioma, el zip y los casos dañados (EC-MIG-02 a 05);
  - generadores CsCheck de zips hostiles (bomba de relación, miles de entradas, rutas con `..`, cabeceras que mienten) sobre `SafeZipReader`;
  - SharpFuzz se añade después de la 2.0.
- **Criterio: 210 → 210 atajos, ningún token vacío** (MIG-003 y MIG-004). La tarjeta de migración y el informe los muestra la bienvenida.

### 6.7 Cifrado y secretos

- **Textos (LOG-003):** `{"enc":"dpapi.v1","blob":"…","len":42,"private":true}`.
  - `CryptProtectData` con alcance CurrentUser y entropía `"Clicalo.Text.v1"`, aplicado en el mapper.
  - Si un texto no se puede descifrar (otro equipo u otro usuario), queda `SecretText.Unavailable` y el atajo pasa a incompleto, con el mensaje «Texto no disponible · vuelve a escribirlo» (COP-005).
- **En memoria:**
  - El mapper descifra a un `char[]` que pasa a ser propiedad de `SecretText`, y limpia los búferes intermedios.
  - El motor accede solo mediante `WithRevealed`, copia a búferes alquilados y los limpia con `CryptographicOperations.ZeroMemory` después de `SendInput`.
  - Los límites están documentados en §6.2.
- **Clave de IA:**
  - Se guarda en el Administrador de credenciales: destino `Clicalo/ai/{providerId}`, `CRED_TYPE_GENERIC`, `CRED_PERSIST_LOCAL_MACHINE` (no viaja con el perfil).
  - La UI solo permite pegarla o borrarla; nunca la vuelve a mostrar.

### 6.8 Copias

| Tipo | Cuándo | Retención |
|---|---|---|
| Automática | 30 s después del último cambio **en una porción significativa** (nunca por uso) | Las últimas 12, y como máximo una cada 30 s |
| Manual | Botón | Sin límite |
| pre-update, pre-migrate, pre-restore, pre-import-replace, pre-reset-frequents, pre-repair | Antes de la operación | 10 de cada tipo |
| v1-original | Primera importación | Permanente |

- Cada copia es un documento completo con su envoltorio e incluye el uso de ese momento. Los recuentos que se muestran son los de **esa** copia (COP-004).
- Restaurar se confirma con dos toques (`TwoStepConfirm` → `ConfirmationToken`), crea antes una copia del estado actual y entra en el historial de deshacer (`Replace`).
- Las operaciones masivas dejan copia persistente, así que se pueden deshacer también tras reiniciar (DAT-006).
- **Prueba de uso intensivo** (Application.Tests, `FakeTimeProvider`): 10 000 `RecordUsage` repartidos en 8 h no cambian `backups\auto\`, no escriben `clicalo.json` y escriben `usage.json` como máximo `8 h / 30 s` veces. Una edición real después sí genera su copia automática, y las 11 anteriores siguen siendo las de cambios reales.

---

## 7. Motor de entrada

### 7.1 Del toque a la acción

```
 UI · rol Surfaces                                           Engine (actor)
 WM_POINTERDOWN/UPDATE/UP ─► PointerInputSource (UI.Wpf)
   GetPointerTouchInfo/PenInfo, rcContact ─► PointerFrame(id, t, pt, contactRect, kind, source)
       ▼
 GestureRecognizer (Domain.Touch, puro, TimeProvider)
   HitResolver: área extra y centro más cercano (REG-02)
   ├ ContactStarted(target, contact, t) ──────────────────► EngineInput.Contact (Priority)
   ├ ContactLeftTarget(contact) ──────────────────────────►   Mantener: soltar (EJE-004)
   ├ ContactEnded(summary: duración, desplazamiento, palma) ►   ActivationPolicy → decisión
   ├ LongPress(target) → sesión abre el menú + ContactCancelled ► cancela; no ejecuta
   └ Swipe(dir) → sesión cambia de página + ContactCancelled ─► cancela; bloquea toques 300 ms
       ▼
 PanelInteractionController (Presentation): añade el ActivationContext
   (origen, perfil de origen e InjectionMode, EditMode, página, época de primer plano,
    última posición externa del puntero)
                                                             ▼
                               EngineReducer.Reduce(state, evt, cfg, now) → (state', effects)
                               Inject · Schedule · CancelTimer · Announce · RecordUsage
                               SetLastAction · ClipboardPaste · Launch · SystemCommand
                                                             ▼
                               EngineHost interpreta, siempre a través de InjectionGate(g):
                               KeyLedger.BeginDown → SendInput → Commit
                               Launch/SystemCommand → hilo Shell → LaunchCompleted/Failed al buzón
                               → EngineOutput: Snapshot, Notice, UsageRecorded, DockCollapse(900 ms)
```

**Voz, teclado y conmutador (EJE-005 y ACC-004).** `IInvokeProvider.Invoke()` del peer produce `ActivationRequest(Phase: Invoke, Origin: UiaInvoke)`: sin contacto, sin filtro táctil y sin duración. Un Mantener invocado así se comporta como Alternar.

**Presupuestos:**
- toque → `SendInput`: p95 ≤ 50 ms;
- toque → frame: p95 ≤ 50 ms;
- cambio de app → perfil aplicado: p95 ≤ 300 ms (NFR-002).

### 7.2 Política de activación única (EJE-001)

```csharp
namespace Clicalo.Domain.Execution;
public readonly record struct ActivationContext(ActivationRequest Request, Shortcut Shortcut, ProfileId? OriginProfile,
    InjectionMode Injection,                 // D24: del perfil en vista al activar; General y Frecuentes heredan el del
                                             // perfil resuelto para la app en primer plano; por defecto VirtualKey
    PhysicalPoint? LastExternalPointer,      // EJE-009 (§7.11)
    bool EditMode, TestModeState TestMode, ElevationState Elevation, ArmedConfirmation? Armed,
    ButtonFilterState Filter, TouchParams Touch, KeyCatalog Keys, DateTimeOffset Now);
public static class ActivationPolicy
{
    // Orden normativo: filtro táctil → edición → Modo prueba → elevación → incompleto/bloqueado → confirmación → ejecutar
    public static (ActivationDecision Decision, ButtonFilterState NextFilter) Decide(in ActivationContext ctx);
}
// Ignored(Verdict) | OpenEditor(id) | TestMark(ok|verdict) | BlockedElevated(app) | Armed(id, until)
// | Refused(Incomplete|BlockedCombo) | Execute(plan) | CancelMacro(runId)
```

Todas las superficies usan esta misma función: cuadrícula, fila fija, barra, laterales, búsqueda, Frecuentes, Repetir e invocación UIA. `ActivationPolicyTests` usa la **misma tabla** de entradas y salidas para cada `Source` (TAC-002).

### 7.3 Núcleo funcional y actor

```csharp
namespace Clicalo.Domain.Execution;
public readonly record struct InjectedKey(ushort Vk, ushort Scan, bool Extended, InjectionMode Mode);   // resuelta al pulsar
public sealed record EngineState(KeyboardLedger Keys, ArmedConfirmation? Armed, MacroRun? Macro,
    TestMode? Test, bool Paused, StickyState Sticky, ForegroundInfo Foreground, KeyboardLayoutSnapshot Layout,
    ShiftBurstWindow ShiftGuard, ImmutableDictionary<EffectId, PendingExternal> PendingExternal, long Version);
public abstract record EngineEvent   // Activation(ShortcutRef, Origin, Contact?, Epoch, RequiredForeground?, Injection) · ContactEnded(c, cancelled)
{ /* TimerFired · ForegroundChanged(info, isUserSwitch) · Terminal(reason) · ReleaseAll(reason) · StickyTapped(mod)
     InjectFailed(batch, failure) · ClipboardReady(token) · DocumentUpdated(doc) · SetTestMode(on)
     SuppressSwitchHandling(for, token) (Probar ahora, PRB-006) · InternalHotkeyRequest (derechos de primer plano, §3.6)
     LaunchCompleted(effectId) · LaunchFailed(effectId, error) · SystemCommandCompleted(effectId, result) */ }
public abstract record EngineEffect  // Inject(batch, events, epoch, requiredFg?, isRelease, isInternal) · Schedule · CancelTimer
{ /* Announce(notice) · RecordUsage(id, at) · SetLastAction(id) · ClipboardPaste(textRef)
     Launch(effectId, target) · SystemCommand(effectId, SystemCommandId) */ }
public static class EngineReducer
{ public static EngineTransition Reduce(EngineState s, EngineEvent e, EngineConfig cfg, long nowTicks); }
```

- **Planificadores por tipo** (en Domain, puros). Todos producen efectos y ninguno toca un puerto:
  - `TapPlanner`, `HoldPlanner`, `TogglePlanner` y `TextPlanner`.
  - `MousePlanner`: actúa en `LastExternalPointer` (§7.11); repetición de rueda con aceleración (60/40/25 ms); `drag` como Alternar.
  - `MacroPlanner`: `MacroRun` reanudable (`Step i/n`, `Waiting until t`).
  - `WebPlanner`, `AppPlanner` y `SystemPlanner`.
- **`EngineHost`** (Application) es el actor. Interpreta los efectos contra `IInputInjector` (siempre a través de `InjectionGate` con su generación), `IKeyLedger`, `ITimerScheduler` (sobre `TimeProvider`), `IEngineOutput`, `IClipboard` y `IShellExecutor`.
  - `Launch` y `SystemCommand` **no se ejecutan en el hilo Engine**. Se encolan en el hilo Shell, con la generación, y el resultado vuelve al buzón como `LaunchCompleted`/`LaunchFailed`/`SystemCommandCompleted`. Un `ShellExecute` lento o una llamada WMI (#9752) nunca retrasan el carril Priority.
  - Cada mensaje va en un `try/catch`. Ante una excepción: liberación de emergencia desde el *ledger* (bajo la valla), `EngineState.Empty` y aviso «Algo falló; se soltaron las teclas» (NFR-005).
- **Teclas fijas (FIJ-006):** `StickyModifiers.Compose` aplica las reglas (a)–(d), elimina duplicados y ordena Ctrl, Alt, Shift, Win (EC-EJE-06). Se aplica también a los clics de mouse.

### 7.4 Registro de pulsadas: el lógico y el físico

**Lógico (Domain.KeySafety).** Dos titulares pueden compartir una tecla física; por ejemplo, un Mantener con Shift y Shift fijado. Soltar uno de ellos no debe soltar Shift.

```csharp
public readonly record struct HolderId(string Value); // "contact:17", "toggle:<id>", "sticky:ctrl", "macro:<run>", "dock:scrollUp"
public sealed record PressedItem(HolderId Holder, HoldOrigin Origin, ShortcutId? Shortcut, PointerId? Contact,
    ValueList<InjectedKey> Keys, MouseButtons Buttons, long SinceTicks, long? DeadlineTicks);
public sealed record KeyboardLedger(ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>> Holders,
                                    ImmutableDictionary<HolderId, PressedItem> Items)
{
    public (KeyboardLedger, ImmutableArray<InjectedEvent>) Acquire(PressedItem i);  // KeyDown solo al pasar de 0 a 1
    public (KeyboardLedger, ImmutableArray<InjectedEvent>) Release(HolderId h);     // KeyUp solo al pasar de 1 a 0
    public (KeyboardLedger, ImmutableArray<InjectedEvent>) ReleaseAll();            // orden inverso
    public long? NextDeadline(TimeSpan? globalLimit);                                // un único temporizador
}
```

Como `InjectedKey` incluye el modo, la misma tecla pulsada en VK y en scancode son titularidades distintas. Cada una se suelta en su modo.

**Físico (`Clicalo.Platform.Core.KeyLedger`).** Es una sección de 4 KiB sin nombre, creada con `CreateFileMapping(INVALID_HANDLE_VALUE)` y heredable solo por el guardián:

```
0x000 uint32 Magic 'CLKL' · 0x004 uint16 LayoutVersion=2
0x006 uint16 Flags (CleanShutdown, NoRelaunch, EngineAlive, EmergencyRestart)
0x008 uint64 Sequence (Interlocked++) · 0x010 int64 LastHeartbeatTicks
0x018 uint64 EngineGeneration (Interlocked; solo sube)
0x020 Slot[128] { uint16 Vk; uint16 Scan; uint8 KeyFlags (Extended | ScanCodeMode); uint8 State; uint16 RefCount }
      State: 0 Free · 1 DownPending · 2 Down · 3 ReleasePending
0x420 uint8 MouseButtonsDown (L/R/M/X1/X2)
```

**Protocolo** (siempre dentro de `InjectionGate.TryRun(g, …)`): `BeginDown` (DownPending) → `SendInput(down)` → Down … `SendInput(up)` → `CommitUp` (Free).
- Si el proceso muere entre `BeginDown` y el envío, se manda una liberación de más, que es inocua gracias a la máscara de menú en Alt y Win.
- **No existe ningún instante en que una tecla esté pulsada sin estar registrada, y ningún hilo con generación vieja puede pulsar nada.**

### 7.5 Invariantes de seguridad de teclas

Notación:
- `D` = teclas y botones pulsados por Clícalo según el receptor (el estado que modela `PhysicalStateInjector`);
- `R` = los titulares;
- `T` = la transacción de Pulsar en curso;
- `L` = las entradas del *ledger* físico que no están en Free;
- `G` = la generación vigente.

| ID | Invariante | Verificación |
|---|---|---|
| INV-1 Solidez | `D = ⋃ Keys(R) ∪ keys(T)` y el recuento de referencias coincide | Propiedad CsCheck en cada paso |
| INV-2 Cobertura | `D ⊆ L` en todo instante observable desde fuera | «Muerte en cada paso» (§7.10) |
| INV-3 Terminales | Tras `Terminal` o `ReleaseAll`: `R = ∅`, `T = ∅`, macro cancelada, `D = ∅` (o `D ⊆ ReleasePending` si el escritorio seguro bloqueó; se reintenta al desbloquear o reanudar) | Propiedad y arnés |
| INV-4 Vida acotada | Todo titular tiene un plazo ≤ `Since + maxHold` (salvo Nunca, que sigue sujeto a INV-3) | Propiedad con `FakeTimeProvider` |
| INV-5 Transacción | Al terminar `T` por cualquier camino, `keys(T) ∩ D ⊆ keys(R)` | Fallo inyectado en el evento n |
| INV-6 Destino válido | Todo `Inject` que no es liberación ni interno lleva `Epoch` y `RequiredForeground?`; el host lo descarta si no coinciden o si el destino está elevado y Clícalo no | Unitaria y propiedad |
| INV-7 Sin envío en prueba o pausa | Con `Test` o `Paused`: ningún `Inject` que no sea liberación o interno (§3.6), ningún `Launch`, `SystemCommand` ni `ClipboardPaste` | Propiedad |
| INV-8 Soltar nunca se filtra | Las liberaciones ignoran época, destino, elevación, prueba y pausa (pero **no** la valla de generación: tras la emergencia, soltar corresponde al `EmergencyReleaser`) | Propiedad |
| INV-9 Contacto dueño | Un titular con `Contact = c` solo se elimina por `ContactEnded(c)`, vencimiento, evento terminal o `ReleaseAll` (EJE-006) | Propiedad con varios contactos |
| INV-10 Estabilidad | Con contactos activos, la composición no mueve ningún objetivo que tenga un contacto (PAN-009) | Propiedad de `LayoutPlanner` |
| INV-11 Valla | Ningún efecto externo con generación `< G` llega al sistema; tras una emergencia, `D = ∅` aunque el hilo viejo se reanude con efectos pendientes | Modelo «congelar y reanudar» (§7.10) |
| INV-12 Modo coherente | Toda liberación usa el mismo `InjectionMode`, vk y scan que la pulsación registrada | Propiedad + InputProbe en los dos modos |

### 7.6 Eventos terminales (SEG-007)

| Evento | Fuente | Acción |
|---|---|---|
| Levantar o cancelar el contacto | `WM_POINTERUP`, `WM_POINTERCAPTURECHANGED` | `ContactEnded` (Priority) |
| Salir del área extra | `GestureRecognizer` | `ContactEnded(cancelled)` |
| Cambio real de app (`safeSwitch`) | WinEvent (excluye las superficies propias, el shell, el teclado táctil y Acceso por voz) | `ReleaseAll(Switch)` y cancelar la macro |
| Vencimiento del plazo | Temporizador | Soltar ese titular |
| Bloqueo de sesión | `WTS_SESSION_LOCK` | `Terminal(Lock)`; lo que falle pasa a ReleasePending y se reintenta en `UNLOCK` |
| Suspensión | `PBT_APMSUSPEND`, respondido **de forma síncrona** con espera de hasta 500 ms a que confirme el motor | `Terminal(Suspend)` y vaciar la persistencia |
| Cierre de sesión o apagado | `WM_QUERYENDSESSION`/`WM_ENDSESSION` | Terminal, vaciar y `CleanShutdown` |
| Salir, ocultar desde la bandeja, pausar, cambiar de vista | Bandeja, CC o UI | `Terminal`/`ReleaseAll` con aviso `[releasedAll]`; al salir, `CleanShutdown` |
| Relanzar elevado | CC | `Terminal(Relaunch)` + `CleanShutdown \| NoRelaunch` |
| Instalar una actualización | Updater | Solo si `R = ∅`; si no, se aplaza. `Terminal(Update)` + `CleanShutdown \| NoRelaunch` |
| Excepción en el motor | `EngineHost` | Liberación de emergencia, estado vacío y aviso |
| Motor sin latido durante 2 s | SysEvents | `EmergencyReleaser` con valla de generación, o reinicio del proceso (§3.2, regla 6) |
| Muerte del proceso | Guardián | Liberaciones del *ledger*; relanzar según las marcas |
| Arranque | App | Soltado preventivo con máscara |

### 7.7 Detalles del envío (NFR-004)

**Ruta única:** `SendInputInjector` (Platform.Core), detrás de `InjectionGate`.

**Modos de inyección (D24, EJE-003, ATJ-004):**

| Modo | `wVk` | `wScan` | Marcas | Resolución |
|---|---|---|---|---|
| `VirtualKey` (normal) | VK resuelto con la distribución del primer plano | Scancode **informativo**, para las apps que lo leen de `lParam`: el de `keys.win32.json` en las teclas fijas y `MapVirtualKeyEx(VK → VSC_EX)` solo en los caracteres de la distribución | `KEYEVENTF_EXTENDEDKEY` según `keys.win32.json` (`extended`); **sin** `KEYEVENTF_SCANCODE` | `KeyId` → VK (con `VkKeyScanEx` para caracteres) |
| `ScanCode` (compatible: juegos, escritorio remoto, máquinas virtuales) | 0 | Scancode resuelto | `KEYEVENTF_SCANCODE` + `KEYEVENTF_EXTENDEDKEY` si procede | Teclas fijas: `scan` y `extended` de `keys.win32.json`. Caracteres: `KeyId` → VK con `VkKeyScanEx` (distribución del primer plano) → `MapVirtualKeyEx(VK → VSC_EX)` |

- Los lados se envían con el VK izquierdo o derecho en modo normal, y con el scancode correspondiente (por ejemplo Ctrl derecho = `0x1D` + extendida) en modo compatible.
- **`MapVirtualKeyEx(VK → VSC_EX)` no basta para las teclas fijas** (verificado en Windows 11 en-US por los paquetes de catálogos y de InputProbe): devuelve sin el prefijo `E0` las flechas, Insert, Supr, Inicio, Fin, RePág y AvPág (los códigos del teclado numérico), `0x54` (PetSis) para Impr Pant, `0x1C` para Intro del teclado numérico y Pausa como `E1 1D`. Por eso las teclas fijas toman `scan` y `extended` de `keys.win32.json` en los dos modos; sin esa tabla, Mayús+flecha con Bloq Num activo rompe la selección y Raw Input ve teclas del teclado numérico. Pausa (prefijo `E1`) se envía siempre por VK. `tests/Clicalo.TestKit.Windows/Input/KeyboardLayouts.ToScanCode` aplica hoy la misma corrección en el inyector de pruebas, y `KeyCatalogConsistencyTests` compara las dos tablas en todas las teclas fijas.
- **Bloq Num no es un fallo de `MapVirtualKeyEx`.** Su código de pulsación es `0x45` sin `E0`, lo que devuelve `MapVirtualKeyEx`, y `E0 45` no es ninguna tecla (`MapVirtualKeyEx(0xE045, VSC_TO_VK_EX)` devuelve 0). Por eso `keys.win32.json` lo guarda sin marca extendida y el modo scancode lo envía así. Una pulsación física, en cambio, llega con la marca extendida en `lParam` (Microsoft la incluye en la tabla «Extended-Key Flag»), y eso reproduce `KeyboardLayouts.ToScanCode` en modo VK. Es una de las tres diferencias declaradas entre las dos tablas, junto con Intro del teclado numérico (comparte `VK_RETURN` con Intro) y Pausa. Qué marca lleva el scancode informativo de Bloq Num en el modo VK del producto, y si Windows añade la marca de `lParam` también en modo scancode, lo mide S7 con InputProbe.
- `KEYEVENTF_UNICODE` se usa para Texto en ambos modos.
- El mouse no depende del modo.
- **Resolución con la distribución del hilo en primer plano.** `GetKeyboardLayout(GetWindowThreadProcessId(fg))` se captura en `KeyboardLayoutSnapshot` con cada cambio de primer plano y con `WM_INPUTLANGCHANGE`. El reductor usa esa tabla pura (la plataforma la construye con `VkKeyScanEx`/`MapVirtualKeyEx`). Si una tecla no existe en la distribución, no se envía nada y se avisa (EC-EJE-10).
- **Intervalo:** `Timings.Injection.InterEventDelay`, generado a partir de `timings.json`, empieza en 20 ms. Con 0, se hace un único `SendInput` atómico.
- **MenuMaskKey:** al soltar Alt o Win **por seguridad** se envía antes `VK 0xE8`, así no se abre ni el menú Inicio ni la barra de menús. Win se mantiene durante toda la combinación y se suelta el último.
- **SEG-008:** como mucho 4 Shift en una ventana de 1 s. El quinto se retrasa para no disparar las Teclas especiales de Windows.
- **Pegar (EJE-008), en SysEvents (OLE STA):**
  1. Capturar todos los formatos del portapapeles (hasta 64 MiB; los diferidos se informan como no restaurables).
  2. Colocar el texto con los formatos `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory=0` y `CanUploadToCloudClipboard=0`.
  3. `ClipboardReady` → Ctrl+V (bajo la valla).
  4. A los 500 ms, restaurar el contenido original **solo si** el número de secuencia del portapapeles sigue siendo el nuestro.
- **Pruebas:** InputProbe y S7 cubren los dos modos con es-ES, en-US, es-419 y AltGr. En modo compatible, InputProbe comprueba que `lParam` lleva el scancode y la marca extendida, y que el VK que ve la app es el que traduce su propia distribución. `[Trait("Req", "EJE-003")]`, `[Trait("Req", "ATJ-004")]`.

### 7.8 Filtro táctil y gestos (Domain.Touch)

```csharp
public readonly record struct TouchParams(TimeSpan Debounce, double HitSlopPx, double CancelMovePx, TimeSpan MinContact);
public readonly record struct ContactSummary(TimeSpan Duration, double MaxDisplacementPx, bool PalmLike);
public enum TouchVerdict { Accepted, IgnoredSwipe, IgnoredShort, IgnoredDouble, IgnoredPalm }
public static class TouchFilter
{
    // TAC-002, en orden: desplazamiento > cancelMove → Swipe; duración < minContact → Short;
    // último aceptado en ESTE botón hace menos de debounce → Double; si no → Accepted (se guarda la hora).
    // Un toque ignorado no reinicia la ventana. Los demás botones nunca se bloquean.
    public static TouchVerdict Evaluate(ref ButtonFilterState s, in ContactSummary c, in TouchParams p, DateTimeOffset now);
    public static bool CanStartHold(in ButtonFilterState s, in TouchParams p, DateTimeOffset now);
}
public sealed class GestureRecognizer  // una instancia por superficie; sin asignar memoria en la ruta caliente
{ public void Feed(in PointerFrame f, List<GestureEvent> output); public void OnTick(DateTimeOffset now, List<GestureEvent> output); }
```

- `Timings.Touch.*` se genera desde `timings.json` (NFR-020): `LongPress = 600 ms`, `SwipeMinDistancePx = 60`, `SwipeMaxSlope = 0.6`, `PostSwipeLock = 300 ms`, `DragMinDistancePx` (el arrastre empieza en `max(DragMinDistancePx, cancelMovePx)`).
- La zona de prueba (TAC-006) y el Modo prueba (TAC-008) llaman a `TouchFilter.Evaluate` directamente. No hay una segunda implementación.
- `PalmLike` se calcula a partir de `rcContact`.
- **Fixtures:** trazas de puntero reales grabadas en el hardware del mantenedor (`tests/fixtures/pointer/*.json`), con temblor, palma, dos dedos y un deslizamiento que empieza sobre un Mantener. Se vuelven a grabar en cada pasada de aceptación con hardware (§10.2).

### 7.9 Cambio de app de extremo a extremo (PER-003)

```
SysEvents: EVENT_SYSTEM_FOREGROUND (fuera de contexto, WINEVENT_SKIPOWNPROCESS: solo primer plano EXTERNO;
           la activación propia la ve ActivationGuard por mensajes, §3.5)
  → confirmar con GetForegroundWindow → ForegroundInfo{Process, Aumid (resuelve ApplicationFrameHost),
    Kind, Integrity (Unknown si no se pudo abrir, EC-PER-03), Epoch++, Layout}
  → ForegroundOrchestrator.Current se actualiza (último primer plano externo verificado)
  1 Engine: si es un cambio real ∧ safeSwitch ∧ R≠∅ → ReleaseAll(Switch) + aviso; recalcular ElevationState
  → EngineOutput.ForegroundSwitched ─(rol Surfaces)─► ForegroundChangeCoordinator
  2 InteractionState.Capture = WaitingForApp(p) ∧ app válida → Dispatch(BindProcess(p, app))   (ATJ-008)
  3 vista' = ProfileResolution.OnForegroundChanged(doc, session, app)          (pura)
  4 vista' ≠ vista → páginas = 1 (panel y barra)
  5 cerrar y vaciar la búsqueda; cerrar el menú contextual → SessionStore (congelada si hay contactos)
```

`ForegroundClassifier` (Domain) decide qué es un «cambio real»: solo lo es un `ForegroundKind.External` de otro proceso, fuera de una ventana de supresión de «Probar ahora».

### 7.10 Grabación de combinaciones y verificación

**Grabación (EDI-010):**
- `WH_KEYBOARD_LL` se instala solo mientras se graba, en el hilo Hook.
- El Centro de control muestra el indicador permanente «Escuchando el teclado». `InteractionState.Capture` refleja el estado en todas las superficies.
- Se desinstala sola a los 30 s, al pulsar Esc o al cancelar desde el panel.
- El callback copia `KBDLLHOOKSTRUCT` a un anillo prealocado y devuelve `CallNextHookEx`. Nunca bloquea ni registra teclas.
- **Plan B si falla S7:** el *hook* se traslada a Sentinel y emite eventos por el pipe heredado. `IKeyboardRecorder` no cambia.

**Verificación del motor:**
1. **Pruebas basadas en modelo (CsCheck).** Secuencias de hasta 200 eventos:
   - toques, varios contactos, temporizadores, cambios de app, bloqueos, suspensiones y `ReleaseAll`;
   - fallos de inyección, Modo prueba y teclas fijas;
   - los dos modos de inyección y resultados de `Launch`/`SystemCommand` fuera de orden.

   Se comprueban INV-1 a INV-12 en cada paso. Al final se añade `Terminal(Exit)` y se exige `D = ∅`. Los contraejemplos reducidos se guardan como regresiones.
2. **Muerte en cada paso.** Para cada prefijo se aplica la lógica del guardián (la misma biblioteca `KeyLedger`) y se exige `D = ∅`.
3. **Congelar y reanudar (INV-11).** Para cada prefijo y cada punto de congelación del hilo del motor, incluido dentro de `TryRun`:
   - el modelo ejecuta `EmergencyReleaser`;
   - si toma la valla, reanuda el hilo viejo con todos sus efectos pendientes y exige `D = ∅` y ningún efecto externo con generación vieja;
   - si no la toma, exige la escalada a reinicio y aplica la lógica del guardián.

   El modelo usa un `InjectionGate` real sobre un `PhysicalStateInjector` que permite bloquear un `SendInput` concreto.
4. **Arnés Win32 con `InputProbe`:** es-ES, en-US, es-419 y AltGr; lados; teclas extendidas; Unicode; los dos modos.
5. **Caos en el equipo de laboratorio:**
   - un Mantener de Ctrl+Shift seguido de `TerminateProcess(Clicalo)` debe producir las liberaciones en ≤200 ms en 50 de 50 intentos;
   - lo mismo con bloquear y desbloquear, con suspensión en una VM y con el motor congelado mediante un punto de ruptura de prueba (compilación `Chaos`).

### 7.11 Posición del puntero para acciones de mouse (EJE-009)

Al tocar el panel, Windows lleva el cursor al punto del toque. Por eso `GetCursorPos` en el `WM_POINTERDOWN` devuelve un punto sobre el panel. `PointerPositionTracker` (Platform.Windows, hilo SysEvents) guarda la **última posición del puntero fuera de Clícalo**:

- **Fuente principal.** `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` fuera de contexto, **sin** `WINEVENT_SKIPOWNPROCESS`, filtrado a `idObject == OBJID_CURSOR`.
  - Es asíncrono y no está en la ruta de entrada del sistema: si Clícalo va lento, no retrasa el cursor de nadie. No es un *hook* de teclado (NFR-009).
  - En cada evento se lee `GetCursorPos`. El punto se descarta si cae dentro de algún rectángulo de `SurfaceRegistry`, una comprobación pura con los rectángulos publicados. Si no cae en ninguno, se confirma con `WindowFromPoint` + `GetAncestor(GA_ROOT)` que no es una ventana propia. Si pasa, se guarda `LastExternalPointer` (punto físico, monitor y hora).
  - Los eventos se coalescen: solo se procesa el último por vuelta de mensajes.
- **Muestreo complementario:**
  - al cambiar de primer plano externo;
  - al entrar un contacto en una superficie, antes de procesarlo, en `PointerInputSource`, con `GetCursorPos` solo si todavía está fuera; esto cubre el caso en que el cursor no se haya movido por toque.
- **Repliegue si S15 muestra huecos** (por ejemplo, apps que no generan el evento de cursor): un `WH_MOUSE_LL` **pasivo y permanente** en el hilo Hook, que solo copia `pt` a una variable atómica y llama a `CallNextHookEx`. Coste: una llamada por cada movimiento del mouse. Riesgo con antivirus: bajo, porque es de mouse, está firmado y no registra nada. Se documenta en `docs/security/threat-model.md` (T9).
- **Uso.** `ActivationContext.LastExternalPointer` llega a `MousePlanner`. Las acciones mueven el cursor a ese punto (`MOUSEEVENTF_MOVE | ABSOLUTE | VIRTUALDESK`), ejecutan la acción allí y lo dejan allí. Si nunca ha habido un punto externo, o su monitor ya no existe, se usa el centro del área cliente de la ventana en primer plano, y lo cubre una prueba de dominio.
- **Pruebas:**
  - Platform.IntegrationTests: el mouse sintético se coloca en el punto P del Bloc de notas de InputProbe; después, un toque sintético (`InjectSyntheticPointerInput`) en el panel ejecuta «clic derecho»; InputProbe debe recibir `WM_RBUTTONDOWN/UP` en P, en 20 de 20.
  - Lo mismo con desplazamiento (rueda) y `drag`.
  - `[Trait("Req", "EJE-009")]`, `[Trait("Req", "FIJ-006")]`.

---

## 8. Presentación

### 8.1 Superficies y ventanas

| Ventana | Tipo | Rol | Notas |
|---|---|---|---|
| Panel (formas Completa y Compacta) | `NonActivatingWindow` | Surfaces | Máquina de formas `PanelFormMachine` (PAN-001) |
| Barra de borde (Pestaña) y su asa | `NonActivatingWindow` | Surfaces | Plegado a los 900 ms tras una acción |
| Ventanas laterales: Fijos, Teclas fijas, Selector de perfil, Ajustes rápidos | `NonActivatingWindow` hijas | Surfaces | Cierre al tocar fuera con `WH_MOUSE_LL` temporal (FIJ-007) |
| Burbuja de 64 px | `NonActivatingWindow` | Surfaces | Recuerda la forma a la que vuelve; opacidad mínima del 55 % |
| Menú contextual y avisos flotantes | `NonActivatingWindow` hijas | Surfaces | Sustituyen a `ContextMenu` y `Popup` |
| Centro de control | `Window` activable | Workspace | Secciones creadas de forma perezosa y virtualizadas; se abre y se cierra con la concesión `ControlCenter` (CCM-004) |
| Bienvenida (pasos 0–4 y migración) | `Window` activable | Workspace | Concesión `ControlCenter` |
| Bandeja | `Shell_NotifyIcon` + `TrayMenuHost` (ventana propia oculta) + `TrackPopupMenuEx` | SysEvents | Clic: mostrar u ocultar el panel. Menú: Centro de control, Soltar todo, Pausar, Salir. Concesión `TrayMenu` |

**Opacidad, atenuado y desenfoque:**
- Opacidad del 30 al 100 % y atenuado según `DimPolicy` (§6.4), con animación de 350 ms (0 ms con reducir movimiento).
- **Desenfoque del fondo (PAN-003, SHOULD):**
  - En Windows 11 22H2 o posterior, fondo de sistema de DWM (`DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_TRANSIENTWINDOW`) sobre una ventana no *layered*, con la opacidad de ventana aplicada con `LWA_ALPHA`, si S6 confirma que ambos se combinan.
  - En Windows 10, y cuando el usuario desactiva los efectos de transparencia (`UISettings.AdvancedEffectsEnabled = false`), **no hay desenfoque**: fondo sólido con la opacidad elegida. Es una ausencia justificada, porque la única vía en Windows 10 es la API no documentada `SetWindowCompositionAttribute`, que está prohibida.
  - S6 decide entre esta vía y `AllowsTransparency` (alfa por píxel sin desenfoque) y deja el resultado en `docs/testing/spikes/S6.md`.
- Sombras precalculadas, nunca `DropShadowEffect`, con un margen de sombra que no captura clics.

### 8.2 Flujo MVVM

```
DocumentStore + SessionStore + InteractionSnapshot + EngineSnapshot
  + SurfaceMetrics(alto medido, DPI, rcWork) + idioma
      └─► PanelProjector.Project(in PanelInputs) → PanelModel (inmutable; memoizado por Revision/Version)
            usa LayerVisibility, GridMetrics, Paging, VoiceNumbering, FrequentsProjector, DuplicateIndex,
            SearchIndex, ProfileResolution, ShortcutCompleteness, DimPolicy (todas puras)
      └─► PanelViewModel.Apply(model): diff por clave (KeyedCollectionSync<ShortcutId, TileVM>)
      └─► VM reenvía INTENCIONES: acción de sesión | acción de interacción | comando del documento | petición al motor
```

- **Ningún ViewModel decide reglas de producto.** La proyección completa cuesta ≤2 ms con 50 perfiles y 2000 atajos, y hay un *benchmark* en CI (NFR-016).
- **No hay tipos de WPF en Presentation.** Se usan `ThemeToken`, `IconRef`, la geometría de Domain y la visibilidad como `bool`. Los convertidores viven en UI.Wpf.
- **Estabilidad bajo el dedo (PAN-009).** Con `Freeze.ActiveContacts > 0`, `GridMetrics` y `Paging` reciben la composición congelada. El pánico, el aviso de administrador y la sugerencia se proyectan como superposiciones flotantes. La composición pendiente se aplica al terminar el último contacto.
- **`TwoStepConfirm`** (REG-04, 3,5 s con `TimeProvider`) es el **único** productor de `ConfirmationToken` (CLC0010, §4.4). Lo usan ✕, Eliminar (atajo, perfil, repetido, paso), Reiniciar Frecuentes, Reemplazar al importar, Restaurar, Volver a la versión anterior, desinstalar el componente de sistema y borrar datos. El comando destructivo solo se despacha en el segundo toque.
- **Enrutadores explícitos** (`DomainEventRouter`, `EngineOutputRouter`) con registros en `Clicalo.App`. Una prueba falla si algún evento no tiene manejador.
  - `ProfileDeleted` → volver a General (PER-008).
  - `ShortcutDeleted` → cerrar el menú o el editor.
  - `TemplateInstalled` → PER-007.
- **Avisos:** `NoticeQueue` pura con prioridades (AVI-002), dentro de `InteractionState`. La leen la barra de avisos del panel, la superficie de la Pestaña (PES-014) y la barra de estado del CC (CCM-003), todas a partir de la misma instantánea. Un aviso lleva `MessageKey` y argumentos, y se localiza al pintarse.

### 8.3 Entrada táctil

- Se activa `AppContext` `Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport=true`. Así se evitan WISP, #3147, #2054 y #9752.
- `PointerInputSource` procesa `WM_POINTER*` en cada superficie, registra `source` (`GetCurrentInputMessageSource`) y publica la entrada y salida del puntero (para el atenuado y el paso del cursor).
- Objetivos táctiles de ≥44 px lógicos, más el área extra resuelta por `HitResolver`.

### 8.4 Temas y tokens

```
data/tokens/{theme-palettes, extra-tokens, contrast-pairs, hc-system-map, motion}.json
  → TokenGenerator (Clicalo.Design.Math: OKLCH→OKLab→sRGB lineal→sRGB; gamut CSS Color 4, JND 0,02)
    diagnósticos: CLCT001 color no válido · CLCT002 contraste < mínimo (sobre el fondo compuesto real en
    8 bits: pila translúcida sobre los 8 vértices del cubo sRGB como escritorio, peor caso 1:1 si el primer
    plano cruza la luminancia del fondo entre dos de ellos; atenuado exento, TEM-004/PQ-42) · CLCT003 mapeo
    de gama con ΔEOK > maxDeltaEOK (también un color cromático con L = 0 o 1) · CLCT004 archivo mal formado
    o miembro desconocido · CLCT005 token desconocido, ausente o sin decisión de contraste · CLCT006
    corrección desfasada · CLCT007 archivo ausente
  → UI.Wpf (generado en C#, no en XAML): ColorToken · CategoryToken · ThemePalette (Dark, Light,
    HighContrast en ThemePalettes) · SystemHighContrastPalette (→ SystemColors, Capture()) · Radii ·
    FocusRing · ShadowSpec/Shadows · MotionToken/Motion (con reducir movimiento); ver docs/guides/design-tokens.md
  → Sizes S/M/L: data/catalogs/sizes.json → CatalogGenerator → Clicalo.Domain.Catalog.PanelSizes
  → ThemeService (uno por dispatcher): Auto lee AppsUseLightTheme y SystemParameters.HighContrast y escucha
    WM_SETTINGCHANGE, WM_SYSCOLORCHANGE y UISettings.ColorValuesChanged; el alto contraste del sistema tiene prioridad (TEM-001)
```

- Reducir movimiento se lee de `SPI_GETCLIENTAREAANIMATION` y de `UISettings.AnimationsEnabled`.
- Los fallos de contraste del tema claro que cita TEM-004 aparecen en la primera compilación y se corrigen en el mismo PR.

### 8.5 i18n

- **Fuente:** `strings.es.json` y `strings.en.json` del paquete, con las 669 claves. `cl i18n-import` hace una única conversión revisada a mano:
  - marcadores con nombre: `{p}`→`{profile}`/`{profiles}`, `{n}`→`{count}`, `{i}`→`{index}`, `{t}`→`{total}`, `{a}`→`{app}`, `{k}`→`{keys}`, `{v}`→`{version}`, `{x}`→`{name}`;
  - plurales CLDR con sufijos `_one` y `_other` en las claves que marca el catálogo (`comboN`, `instNoteSome`, `sugLine`, `dupHead`, `twMacro`, `migT`, `addMissing`);
  - eliminación de las 49 claves huérfanas.
  - **Condición vinculante:** una prueba de instantánea verifica que el texto que se muestra con los argumentos de muestra es idéntico al del paquete.
- **`LocalizationGenerator`:**
  - genera `MessageKey` (en Domain) y el API tipado `L.MigT(profiles, shortcuts)`;
  - da error de compilación ante falta de paridad, marcadores distintos entre idiomas, un marcador desconocido o la falta de `_other`;
  - `XamlLocRefValidator` comprueba `{loc:T clave}`.
- **En ejecución:** `ILocalizer` (Application) es una instantánea inmutable. `LocalizationSource` es una por dispatcher, con `PropertyChanged("Item[]")` para el cambio en caliente. Los campos que se están editando conservan su texto (IDI-001).
- **Cultura:** `CultureInfo` para decimales y fechas. Comillas «» en español (hay prueba de estilo).
- **Pseudolocalización `qps-ploc`** (+40 % de longitud) en `UI.Wpf.Tests` para detectar desbordamientos.
- **Tercer idioma:** Weblate (formato i18next JSON v4). Añadir un idioma es añadir un archivo más una entrada en `locales.json` (IDI-006).
- **Textos nuevos que exige esta revisión** (P6 y los avisos de foco denegado, si el paquete no tiene clave): entran por PR de i18n en ambos idiomas y se registran en §6.1 del catálogo.

### 8.6 Accesibilidad, UIA y números de voz

- **`AutomationPeer` propios** en `ShortcutButton`, `SegmentedControl`, `ProfileGrid` y el interruptor Auto/Fijo:
  - ControlType correcto;
  - patrones Invoke, Toggle (3 estados en teclas fijas), SelectionItem, ExpandCollapse, RangeValue y Tab;
  - `LiveSetting` Polite en las barras de avisos y de estado; Assertive en la franja de pánico y en los errores;
  - `RaiseNotificationEvent`.
- **Cada VM interactivo expone** `AccessibleName` (localizado), `AccessibleState` y `SecondaryAction` (menú contextual por voz, CUA-014).
- **Todo gesto tiene un equivalente sin gesto**, y hay una prueba en Presentation que lo comprueba:
  - toque largo → `SecondaryAction`;
  - deslizar → Anterior o Siguiente;
  - mantener → Toggle enclavado.
- **Dictado junto a todo campo de texto libre (ACC-011, docs/07):** botón hermano 🎤 «Dictar», que abre Win+H con el foco en el campo, a través de la concesión correspondiente si el campo está en una superficie. Lo verifica UIA010.
- **Números de voz (ACC-009 y ACC-010):**
  - `VoiceNumbering.Assign(listCount, perPage, stripCount)` numera de forma continua entre páginas; la fila fija y Fijos siguen tras N.
  - Con la opción activada, `AutomationProperties.Name` empieza por `"{n} "`.
  - Se relocaliza al cambiar de idioma.
- **Invocar por UIA no activa la ventana.** Lo comprueban S3 y la regla UIA009. Cuando una invocación por UIA necesita primer plano (búsqueda), la gestiona la escalera de §3.6.
- **Además:** alto contraste del sistema, escala de texto del 100 al 150 %, `FocusVisual` en el modo teclado y ningún glifo usado como nombre (UIA008).

---

## 9. Servicios

### 9.1 Plantillas

- **Fuentes:** `data/content/templates/*.json` (una por archivo; versión, autoría, idiomas revisados, procesos y variantes) y `library.json`. Se incrustan como recursos y se cargan de forma perezosa.
- **Tratadas como contenido no confiable (LOG-006):** se validan contra el esquema al compilar y al cargar.
- **Flujo:** `ITemplateSource` → `TemplatePreview`. La vista previa es editable y las acciones de riesgo aparecen desmarcadas (LOG-008). El usuario confirma y se ejecuta `InstallTemplate(plan)`, un solo paso de deshacer. `CatalogRef` guarda el origen con su versión (DAT-004).
- **Compartir perfiles:** formato `clicalo.profile-share` con su propio `schemaVersion`. La importación siempre pasa por vista previa. Límites: 5 MiB, `MaxDepth 32`, 200 perfiles y 10 000 atajos como máximo. «Reemplazar» es destructivo (`ConfirmationToken`).

### 9.2 IA

```csharp
public interface ITemplateGenerator   // Application.Ports
{ ValueTask<Result<AiTemplateProposal>> GenerateAsync(TemplateRequest r, CancellationToken ct); } // tiempo máximo 15 s
public sealed record TemplateRequest(string AppName, KeyboardLayoutKind Layout, LangCode ProgramsLang, LangCode UiLang);
// EXACTAMENTE los 4 datos de PLA-008 / [aiPrivacy]. Ningún otro dato sale del equipo.
```

**En la 2.0 (clave propia, PQ-48):**
- **Adaptadores** en `Infrastructure.Ai`:
  - `ByoKeyTemplateGenerator`: `IChatClient` de Microsoft.Extensions.AI con el adaptador del proveedor elegido por configuración (PLA-008) y la clave del Administrador de credenciales. El prompt se construye en el cliente a partir de una plantilla versionada e incluye solo los 4 datos.
  - `CannedTemplateGenerator`: pruebas, con los 6 tipos de fallo (`offline`, `limit`, `invalid`, `timeout`, `unavailable`, `blocked`).
- **El cliente siempre valida** la respuesta en dos pasos:
  1. Estructural, contra `data/schemas/ai-template.v1.schema.json`.
  2. Semántica, con `TemplateSchema` de Domain:
     - solo acciones Pulsar;
     - teclas del catálogo;
     - nombres de 32 caracteres como máximo, sin caracteres de control ni bidi;
     - las combinaciones bloqueadas se filtran y las peligrosas (Alt+F4, Ctrl+W, Supr) se marcan en la vista previa.

  El orden de estados es el de PLA-005.
- **Con clave propia no hay cuota** (docs/08: «sin límite con la clave propia»). La cuota gratuita (PLA-003) llega con el proxy (P3).

**Proxy de cuota: diferido, con el diseño fijado (ADR-0014).** Condiciones que debe cumplir cuando se construya:
- **Contrato.** `POST /v1/templates` con `{ contractVersion, app, layout, programsLang, uiLang }`: los 4 datos más la versión del contrato, que no es un dato del usuario. **Sin identificador de instalación ni ningún otro dato persistente o enlazable.** Validador compartido `Clicalo.Contracts.Templates` (§4.2), sin dependencia de Domain.
- **Límites.** `HMAC(IP, sal diaria)` como techo antiabuso por día UTC, más un presupuesto global diario con corte automático. La sal diaria se descarta.
- **Cuota visible al usuario (PLA-003, EC-PLA-05: medianoche local).** La lleva el cliente: un contador local por fecha local que se reinicia a medianoche local. El servidor aplica solo el techo antiabuso y devuelve `remaining` en su propio marco. Si el servidor rechaza antes que el contador local, el cliente muestra el estado `limit`. Así se cumple la medianoche local sin enviar la zona horaria.
- **Prompt.** Se construye en el servidor (el cliente no envía prompt), con caché por (app normalizada, layout, programsLang). La cuota se descuenta **solo** si la respuesta es válida (PLA-003).
- **Presupuesto agotado:** `503 unavailable`, y el cliente muestra PLA-006 y ofrece «Usar mi clave».
- **Retención:** solo contadores agregados; nunca se guarda `app` junto a la IP.
- **Encendido y apagado:** `disabledFeatures: ["ai.proxy"]` en el manifiesto firmado (§9.3).
- **Activación:** requiere que el usuario ratifique P3 y que se escriba un ADR con el proveedor y el coste (docs/08 los deja a decisión del autor).

### 9.3 Actualizaciones

```
UpdateService (ThreadPool; al arrancar, cada 24 h y a petición; desactivable)
 1 GET releases.{channel}.json + .sig (GitHub Releases) mediante SignedManifestSource : Velopack IUpdateSource
 2 ECDSA P-256 con claves públicas FIJADAS (actual + siguiente) en Platform.Core.Trust
   rechazar si: firma inválida · manifest.seq < último seq visto (anti-rollback DEL MANIFIESTO)
   · expiresUtc vencido (anti-freeze → aviso)
 3 ¿instancia elevada? → NO descarga ni aplica; solo informa (ver «Instancias elevadas»)
 4 descargar delta o paquete completo → SHA-256 == manifiesto
 5 staging → WinVerifyTrust(exe, Sentinel, Launcher, *.dll) + editor fijado (Subject CN + O + raíz; no la huella)
   + hashes == release-files.json firmado
 6 "Nueva versión" (ACT-001) → condiciones: R=∅, 5 min sin contacto (o petición del usuario), sin edición
   abierta en el CC, sin Modo prueba, sin concesión de primer plano activa
 7 copia pre-update → Flush (documento y uso) → Engine Terminal(Update) → ledger CleanShutdown|NoRelaunch
   → ApplyUpdatesAndRestart
 8 arranque nuevo con marca "pending-health": panel mostrado + documento cargado + motor vivo en ≤60 s
   → confirmar. Dos arranques sin confirmar → MODO SEGURO + ofrecer volver a la versión anterior
 9 con componente de sistema y arranque elevado activo: ejecutar la tarea ElevatedStart bajo demanda
   (sincroniza la copia protegida y relanza elevado sin UAC)
```

**Contenido del manifiesto firmado:** `manifestVersion`, `seq`, `expiresUtc`, versión, canal, hashes, notas ES/EN, `minSafeVersion`, `revoked[]`, `rollbackAllowed[]` (versiones con su hash de paquete completo) y `disabledFeatures[]`.

**Canales:** `stable` y `beta`, cada uno con su manifiesto (`ExplicitChannel`).
- Beta recibe también las estables posteriores.
- **Nunca se baja de versión de forma implícita.** Pasar de beta a estable espera a que estable alcance o supere la versión instalada.

**Anti-rollback frente a reversión (ACT-005).** Son dos cosas distintas:
- **El manifiesto nunca baja:** su `seq` es monótono y se rechaza cualquier manifiesto con `seq` menor que el último visto.
- **La versión instalada puede bajar solo por acción explícita del usuario.** «Volver a la versión anterior» se confirma con dos toques y exige tres cosas del paquete N−1 conservado:
  1. que su hash figure en `rollbackAllowed` del manifiesto **vigente**;
  2. que no esté en `revoked`;
  3. que su versión sea ≥ `minSafeVersion`.
  
  Si N−1 está revocada o por debajo de `minSafeVersion`, la opción no se ofrece y se explica el motivo. `AllowVersionDowngrade` solo se activa en ese flujo.
- **Datos al volver:** si N cambió el major del esquema, se restaura `pre-update` y se informa de qué se pierde. Si solo cambió el minor, el documento sigue valiendo (D10).
- El paquete completo N−1 se conserva 7 días.
- **Pruebas** (Infrastructure):
  - una bajada legítima (N−1 en `rollbackAllowed` y el usuario confirma) se aplica;
  - se rechazan tres bajadas atacantes: un manifiesto antiguo con `seq` menor, un N−1 fuera de `rollbackAllowed` y un N−1 revocado;
  - una actualización automática nunca baja.

**Instancias elevadas (NFR-010, SIS-004):**
- Una instancia elevada **nunca** descarga ni aplica actualizaciones, porque los archivos quedarían con propietario Administradores.
- Muestra «Nueva versión» y, al aplicar:
  1. hace el traspaso con `Terminal(Update)`, `CleanShutdown | NoRelaunch` y *flush*;
  2. lanza `%LocalAppData%\Clicalo.App\current\Clicalo.exe --apply-update` en **integridad media**, mediante `IShellDispatch2::ShellExecute` del escritorio del shell;
  3. sale.
- La instancia media verifica, aplica y, en el paso 9, vuelve a modo elevado. Sin componente, ofrece el relanzamiento elevado con UAC (EJE-013).
- **Comprobación defensiva al arrancar:** si alguna carpeta de `%LocalAppData%\Clicalo.App` no tiene al usuario como propietario (instalaciones previas), se registra `install.owner_mismatch` y se ofrece reparar mediante reinstalación.
- Pruebas en S8 y S9: actualizar desde una instancia elevada deja todos los archivos con el usuario como propietario, y Sentinel no relanza durante la actualización ni durante el traspaso.

**Interruptores de emergencia**, como datos firmados y nunca como código remoto: `disabledFeatures`, `minSafeVersion` y `revoked`.

**Desinstalación (NFR-010):**
- **Desde Sistema › Desinstalar:** pregunta «¿conservar datos?» con dos toques si se borran, borra si procede, desinstala el componente de sistema si existe (UAC) y lanza `Update.exe --uninstall`.
- **Desde Configuración de Windows:** los *hooks* de Velopack no pueden mostrar UI, así que este camino **conserva siempre los datos** y no toca `%AppData%\Clicalo`. Una prueba de S8 lo comprueba.
- **Al reinstalar**, la bienvenida detecta los datos existentes y ofrece conservarlos (por defecto) o empezar de cero con una copia previa `pre-reset` (P6).

### 9.4 Registros y diagnóstico

- **Registros (LOG-001, ACE-003, docs/08):**
  - `ILogger` más Serilog, con el archivo **`%AppData%\Clicalo\logs\clicalo.log`** de nombre fijo;
  - rotación de 5 × 1 MB con `FixedNameRollingFileSink`, un *sink* propio de unas 100 líneas: al llegar a 1 MB renombra `clicalo.3.log → clicalo.4.log` … `clicalo.log → clicalo.1.log`, y el archivo actual siempre se llama `clicalo.log`;
  - nivel Information; Debug a petición durante 1 h;
  - plantilla con `seq`, hilo lógico, `Code` y `EventId`;
  - todos los mensajes son `[LoggerMessage]`.
- **Redacción por tipos:**
  - `Sensitive<T>` con `RedactionKind` (FreeText, WindowTitle, SearchQuery, Url, FilePath, Secret, KeyCapture) y `SecretText` redactan en `ToString()`: `[oculto · N caracteres]`, `[título oculto]`;
  - `CLC0003` actúa en compilación;
  - una política de desestructuración de Serilog y un enriquecedor eliminan rutas con `%USERNAME%`;
  - nunca se registra el texto inyectado, lo que captura el *hook*, los títulos de ventana, las claves ni las posiciones del puntero.
- **Prueba canario (CI):** 5 valores `CANARY-<guid>` como texto de atajo, proceso, título de ventana, búsqueda y clave. Se recorren los flujos E2E, se exporta el diagnóstico y se buscan en logs, ETW y en el paquete. Cualquier coincidencia hace fallar la CI.
- **Métricas locales** (`System.Diagnostics.Metrics`, sin exportador): `touch_to_inject.ms`, `fg_to_profile.ms`, `startup.first_frame.ms`, `persist.write.ms`, `reg01.violations`, `engine.emergency_releases`, `engine.zombie`, `engine.fenced_effects`, `foreground.lease_denied`, `ledger.pending_release`, `ui.hang.count`, `gc.pause.ms`. Se ven en Sistema › Inicio y estabilidad › Diagnóstico.
- **ETW `Clicalo-Perf`:** `ProcessStart`, `StartupPhase`, `FirstFramePresented`, `PointerDown`, `InputInjected`, `ForegroundChanged`, `ProfileSwitched`, `PersistWrite`. Solo llevan identificadores, nunca contenido.
- **Paquete de diagnóstico (a petición):**
  - contenido: registros, entorno (build de Windows, versión, componente de sistema sí o no, DPI y monitores, digitalizador, distribución), ajustes **sin** perfiles ni textos, métricas, las últimas 200 transiciones del motor (tipos y códigos, sin teclas de Texto) y `crash-journal`;
  - se muestra entero antes de guardarse (ACE-003);
  - se guarda como zip local.
- **Volcados:** desactivados por defecto. Si se activan, `MiniDumpWithThreadInfo` sin heap.
- **Sin telemetría** ni informes de fallo automáticos (LOG-002).

---

## 10. Calidad

### 10.1 Pirámide y proyectos de prueba

```
           Aceptación por versión en HARDWARE TÁCTIL REAL (docs/09 completo) + guion manual de accesibilidad
         E2E (FlaUI sobre la app PUBLICADA): reglas UIA, árbol, no activación, recorridos (~60)
       Rendimiento con presupuestos (equipo táctil = puerta; alojado = tendencia)
     Integración: Platform contra InputProbe; Windowing con puntero sintético (~220)
   UI en proceso (STA): peers, medidas, layout S/M/L × 100–150 %, pseudo, gestos con trazas,
   INSTANTÁNEAS DE RENDERIZADO por estado (~350)
 Unitarias + propiedades: Domain, Application, Presentation, Infrastructure (3000+)
Estáticas: analizadores, generadores, ArchUnit, reglas de producto, esquemas, paridad i18n, contraste
```

| Proyecto | Cubre | Dónde |
|---|---|---|
| Architecture.Tests | Lista blanca, ArchUnit, matriz de módulos, facetas de `ActionKind`, enrutadores sin eventos huérfanos, R4 (destructivos), R7 (`Record` salvo exenciones), escritor único | Cada PR |
| Data.Tests | Esquemas, integridad referencial (`labelKey`, iconos en la fuente, `KeyId`), CAT-004, contenido inicial sin repetidos (CAT-003), `keys.json` ↔ `keys.win32.json`, coherencia de `timings.json` (umbrales únicos) | Cada PR |
| Domain.Tests | Invariantes de `Library`, `KeyboardLedger`, `EngineReducer` (INV-1 a 12, incluido «congelar y reanudar»), `TouchFilter` y `GestureRecognizer`, `ActivationPolicy` por `Source`, `DimPolicy` (tabla de excepciones), `InteractionReducer`, resolución de perfil, Frecuentes, repetidos, capas, métricas, numeración, tokenizador v1, tabla de formas | Cada PR |
| Application.Tests | `DocumentStore` (porciones, agrupación, 20 entradas, borrador sin rastro, `ConfirmationToken`), `EngineHost` con `PhysicalStateInjector` e `InjectionGate`, `ForegroundOrchestrator` con `FakeForegroundControl` (escalera por origen, concesiones y prioridades), `TryNowUseCase` con `FakeTimeProvider`, coordinadores, programador de guardado (uso intensivo) | Cada PR |
| Presentation.Tests | VM contra proyecciones, equivalentes sin gesto, `TwoStepConfirm`, idioma en caliente | Cada PR |
| Infrastructure.Tests | DTO ↔ dominio, migraciones con fixtures, importación v1 (instantáneas de TestKit), `SafeZipReader` con zips hostiles (CsCheck), `usage.json` y `usageEpoch`, `CrashingFileSystem`, cuarentena, DPAPI, cliente de IA con servidor falso y los 4 campos exactos, `SignedManifestSource` (firma incorrecta, `seq` menor, bajada legítima o atacante, revocada, `minSafeVersion`), `FixedNameRollingFileSink` | Cada PR |
| UI.Wpf.Tests | Peers, ≥44 px, layout, pseudo, contraste resuelto, **instantáneas de renderizado** (`RenderTargetBitmap` por forma, tamaño S/M/L, tema, escala y estado de la matriz, comparadas con `RenderSnapshot` de TestKit y tolerancia por píxel ΔE ≤ 2 y ≤0,5 % de píxeles distintos; hoy la tolerancia es por canal y el modo ΔE llega antes de las primeras referencias de la UI) | Cada PR (x64 y ARM64) |
| Platform.IntegrationTests | Inyección en los dos modos con varias distribuciones, *hook* LL bajo presión de GC, `PointerPositionTracker` con toque sintético, sesión y suspensión, portapapeles, lanzador sin intérprete en el hilo Shell, ACL de la tarea elevada, escritura elevada y luego media | Alojado interactivo o equipo táctil |
| Windowing.IntegrationTests | No activación de las 4 superficies con dedo, lápiz y mouse sintéticos; `ActivationGuard` (prueba negativa); concesiones 20 de 20 por origen; bandeja (Bloc de notas activo → menú → Soltar todo → el foco vuelve al Bloc de notas); CCM-004; PRB-004/007; menús; IME; `Upstream/` con una prueba por cada solución provisional de WPF (#3147, #2054, #9752, #7561, #4127, #10459, #10422, #7857, #11847), con `[Trait("Upstream", …)]` | Alojado y equipo táctil |
| Sentinel.Tests | Lectura del *ledger* v2 (modos y generación), liberación con máscara, relanzamiento según las marcas, bucle de fallos (`timings.json`) | Equipo táctil |
| Launcher.Tests | Verificación tras la copia, rechazo de un archivo alterado, `minSafeVersion` monótono, camino rápido, sin argumentos | Alojado (con elevación de runner) y equipo táctil |
| E2E | Reglas UIA, instantánea del árbol, Axe.Windows, recorridos (bienvenida sin teclado, crear cada tipo, vincular y Probar ahora, cerrar el CC devuelve el foco) | Alojado (humo) y equipo táctil (completo) |
| Performance | §10.3 | Tendencia y puerta |

**`Clicalo.TestKit`:**
- `PhysicalStateInjector`, con `PressedKeys`, `Log`, `FailAfter(n)`, `BlockAt(n)` (congelar), y las aserciones `ShouldBeFullyReleased()` y `ShouldHaveSentInOrder()`;
- `FakeForegroundMonitor`, con `SwitchTo(…, elevated)`, `Lock()` y `Suspend()`; `FakeForegroundControl`, con la política de derechos configurable;
- `FakeClipboard`, `FakeAppCatalog`, `CannedTemplateGenerator`, `InMemoryFileSystem` y `CrashingFileSystem`;
- constructores y generadores CsCheck (`KeyChord`, `TouchTrace`, `Document`, `HostileZip`);
- `SyntheticPointer` (`CreateSyntheticPointerDevice` / `InjectSyntheticPointerInput`);
- `RenderSnapshot` (comparador PNG con tolerancia) y `StateMatrixFixture`.

`cl states` genera las instantáneas de todos los estados y abre la carpeta. Sustituye a la Gallery como catálogo vivo.

**`InputProbe`:**
- es una ventana Win32 controlada por pipe;
- carga la distribución que se le pide y registra `WM_KEYDOWN/UP` (vk, scan, extendida y `lParam` completo), `WM_CHAR`, `WM_SYSKEY*`, `WM_*BUTTON*` con coordenadas, `WM_MOUSEWHEEL`, `WM_ACTIVATE`, `WM_KILLFOCUS`, `WM_IME_*` y `GetForegroundWindow` por evento, con marca QPC;
- es la «verdad física» de todas las pruebas de integración.

### 10.2 Pruebas de accesibilidad y aceptación en hardware

**Puerta principal: reglas UIA propias** (`IUiaRule`):

| Regla | Comprueba |
|---|---|
| UIA001 | Name no vacío y localizado; con números de voz, empieza por «{n} » |
| UIA002 | ControlType |
| UIA003 | Patrones |
| UIA004 | Estados coherentes con el VM |
| UIA005 | Tamaño ≥44 lógicos × escala |
| UIA006 | `LiveSetting` |
| UIA007 | Equivalentes sin gesto |
| UIA008 | Ningún glifo como Name |
| UIA009 | Invocar no cambia el primer plano |
| UIA010 | Todo Edit de texto libre tiene un botón hermano con Invoke llamado «Dictar» (o «Pegar» en el campo de clave de IA) (ACC-011, REG-05) |

**Además:**
- Instantánea de texto (TestKit) del árbol UIA por ventana y estado. Un cambio de accesibilidad aparece en el diff del PR.
- Instantáneas de renderizado de la misma matriz, para verificar la fidelidad visual («las tres vistas y los tres tamaños se ven como en el prototipo»). Las referencias iniciales se aprueban comparándolas lado a lado con el Prototipo v4.
- `StateMatrixFixture` recorre formas, CC por secciones, bienvenida por pasos, alto contraste, números de voz activados y apagados, atenuado y ES/EN.
- Axe.Windows 2.4.x fijado, como apoyo.

**Guion manual obligatorio** antes de cada versión estable (`release-verification.yml`):
- **Windows 11:** Narrador (con exploración táctil) y **Acceso por voz** («mostrar números», «clic 4», «clic Negrita», «mostrar números en todas partes»).
- **Windows 10 22H2:** Narrador y **Reconocimiento de voz de Windows** («mostrar números», «clic 4», «clic Negrita»), porque Acceso por voz no existe en Windows 10 (PQ-29).
- Ambos, con y sin alto contraste.

**Aceptación en hardware táctil real** (`docs/testing/touch-acceptance.md`, dentro de `release-verification.yml`):
- Es la lista completa de criterios de docs/09 y se ejecuta en el equipo táctil del mantenedor antes de cada beta relevante y de cada estable.
- Incluye palma, círculo táctil ausente, teclado táctil, `rcContact` real, arrastre, deslizar sobre un Mantener, atenuado y sus excepciones, acciones de mouse en la última posición externa, bandeja y «Probar ahora».
- Las trazas grabadas se añaden a `tests/fixtures/pointer/`.

**ARM64 (P5):**
- Un trabajo en el runner alojado `windows-11-arm` ejecuta unitarias, UI en proceso (incluidas las instantáneas de renderizado, que cubren el fallo de canales R y B #11847) y el humo de escritorio si S0 lo permite.
- ARM64 se publica en beta. En estable, cuando la aceptación en hardware se haya pasado una vez en un equipo ARM64 físico.

### 10.3 Presupuestos de rendimiento

Se guardan en `tests/Clicalo.Performance/budgets.json`, que está versionado.

| Métrica | Presupuesto | Puerta |
|---|---|---|
| Primer frame, arranque manual en frío | p50 700 ms, máximo 1000 ms | Equipo táctil |
| Primer frame en caliente | p50 300 ms, máximo 450 ms | Tendencia en alojado |
| Primer frame al iniciar sesión (con y sin componente de sistema) | **máximo 1000 ms (NFR-001, sin excepción)** | Equipo táctil |
| Toque aceptado por el motor | Desde el primer frame (sin esperar al guardián) | Prueba de integración |
| Toque → `SendInput` p95 · toque → frame p95 | ≤50 ms · ≤50 ms | Equipo táctil |
| Cambio de perfil p95 | ≤300 ms | Equipo táctil |
| *Working set* con 4 superficies · Sentinel · Launcher | ≤120 MB · ≤5 MB · ≤5 MB | Equipo táctil |
| CPU media en reposo durante 8 h | ≤0,5 % | Equipo táctil, antes de cada estable |
| Proyección completa con 50 perfiles y 2000 atajos | ≤2 ms | CI |
| Asignación en el camino puntero → `Engine.Post` | Mínima, medida con `dotnet-counters` | Spike S2 y tendencia |

- **Medición:** app **publicada**, EventPipe o TraceEvent, puntero sintético más la marca QPC de InputProbe. El frío real exige reiniciar el equipo con Defender activo.
- **En PR:** comentario con el delta frente a `main`. Un empeoramiento de más del 20 % añade la etiqueta `perf-regression` (el *check* no es obligatorio).
- **Al fallar:** se sube la traza `.nettrace` o `.etl` como artefacto.
- **Si S5 demuestra que el inicio de sesión no puede cumplir 1 s**, se abre la propuesta P1 al usuario. Mientras no la ratifique, el presupuesto no cambia.

### 10.4 Análisis estático, cobertura y mutación

- **Análisis:** NetAnalyzers, Meziantou, BannedApi, `CLC*`, CodeQL (C#, `build-mode: manual`), NuGetAudit incluidas las transitivas, y comprobación de CSharpier y de `dotnet format --verify-no-changes`.

| Capa | Líneas | Ramas | Tipo |
|---|---|---|---|
| Domain | 90 % | 85 % | Obligatoria |
| Application | 85 % | 80 % | Obligatoria |
| Persistencia, migraciones y `Application.Localization` | 85 % | 75 % | Obligatoria |
| Presentation | 70 % | — | Informativa |
| UI.Wpf y Platform | — | — | Se cubren con integración, instantáneas y E2E |

- **Mutación y *fuzzing*: después de la 2.0 (M7).** Stryker.NET semanal sobre Domain y `Application.Engine` (umbral del 70 %); SharpFuzz sobre el lector del documento, `SafeZipReader`, el importador v1 y el tokenizador. Antes de la 2.0 los cubren los generadores CsCheck de entradas hostiles.
- **Trazabilidad:** rasgo `[Trait("Req", "SEG-007")]` ([D-06](deviations.md)) más `cl trace`, que genera `traceability.md` a partir de `docs/requirements/catalog.md` y los resultados. Desde el hito RC, ningún MUST puede quedar sin prueba automática o sin entrada en el guion manual o en la aceptación en hardware.
- **Pruebas inestables:**
  - ningún reintento en unitarias ni en UI en proceso;
  - un reintento en escritorio, con un *issue* `flaky` automático;
  - cuarentena de 14 días como máximo con *issue* obligatorio;
  - volcados WER, logs depurados y capturas de FlaUI como artefactos durante 14 días.

### 10.5 CI/CD

| Workflow | Disparador | Contenido |
|---|---|---|
| `pr.yml` | `pull_request` | `title` (Conventional Commits) · `verify` (`cl check`: *restore* bloqueado, compilación, formato, i18n, catálogos, nota de usuario) · `unit` (con cobertura) · `ui` (x64 y `windows-11-arm`, con instantáneas) · `desk-smoke` (integración con un monitor, E2E de humo, reglas UIA, tendencia de rendimiento) · `codeql` · `dco`. Todos obligatorios salvo el rendimiento. PR de *forks*: permisos de solo lectura y sin secretos |
| `main.yml` | Push a main | Lo mismo, más artefacto `dev` sin firmar y trazabilidad |
| `lab.yml` | `workflow_dispatch`, semanal y antes de cada `beta`/`release` | En el equipo táctil: E2E completo, integración de Windowing y Platform, caos de Sentinel, puerta de rendimiento, frío tras reinicio; CPU de 8 h solo antes de una estable; Windows 10 22H2 en una VM Hyper-V del mismo equipo (puntero sintético) |
| `beta.yml` / `release.yml` → `release-core.yml` | `cl beta` / fusión del PR de release-please | Compila, firma el código y produce el manifiesto **sin firmar** (§11) |
| `release-publish.yml` | `workflow_dispatch` con el `.sig` subido por `cl sign-manifest` | Verifica la firma con las claves públicas fijadas y publica |
| `patch-tuesday.yml` | Semanal | Parche de .NET → PR con la etiqueta `security` |
| `scorecard.yml`, `codeql.yml`, `docs.yml` | Semanal o al tocar `docs/**` | OpenSSF, CodeQL completo, markdownlint, enlaces y Mermaid |

**Runners:**
- `windows-2025` y `windows-11-arm`, alojados.
- **`lab`: un equipo físico con pantalla táctil, dedicado** (no el equipo de trabajo del mantenedor), con Windows 11 25H2 y una VM Hyper-V de Windows 10 22H2.
  - Inicio de sesión automático y runner **como proceso interactivo**.
  - Se ejecuta bajo demanda y no de forma continua.
  - **Nunca ejecuta código de *forks***: solo `main`, etiquetas y `workflow_dispatch` de mantenedores, o un PR con la etiqueta `run-lab` y aprobación.
  - Se restaura con un punto de restauración o una instantánea de la VM después de cada trabajo.
- Si en algún momento el volumen lo justifica, se añaden VM, que no forman parte del plan inicial.
- No se usa `pull_request_target`.
- Todas las acciones están fijadas por SHA y los permisos son mínimos por trabajo.

---

## 11. Distribución, versionado y publicación

- **Instalador:** Velopack `Setup.exe` por usuario y sin UAC, con `packId Clicalo.App`.
  - Autocontenido para `win-x64` (beta y estable) y `win-arm64` (beta; estable según P5).
  - R2R (compuesto si S5 lo confirma) y sin recorte.
  - El paquete incluye `Clicalo.Launcher.exe` y `release-files.json` + `.sig` para el componente de sistema.
- **Componente de sistema** (inicio elevado sin UAC en la 2.0; uiAccess en M7): se instala desde la propia app (§3.3). No hay un segundo instalador.
- **Otros canales:** winget (PR automático con `wingetcreate` al publicar en estable) y envío a Defender para mejorar la reputación en SmartScreen.

**Versionado:**

| Artefacto | Versión |
|---|---|
| Producto | SemVer 2.0.0, **desde 2.0.0** (continúa la numeración de Macro Quick Access, PQ-05). Estable `vX.Y.Z`; beta `vX.Y.Z-beta.N` |
| Documento de usuario y `usage.json` | `schema.major.minor` (§6.5) |
| Formato para compartir | `schemaVersion` propio; siempre se leen las anteriores |
| Catálogos y plantillas | `catalogVersion` y `version` por plantilla (incluida en `CatalogRef`) |
| Manifiesto de actualización y catálogo de archivos | `manifestVersion`; los clientes y lanzadores antiguos deben seguir entendiéndolo |
| Contrato de plantillas de IA | `ai-template.vN` (`contractVersion` en la petición) |
| Contratos públicos (CLI, `clicalo://`, IPC) | `docs/architecture/contracts.md`; romperlos implica versión mayor |

**Publicación:**

```
tag vX.Y.Z[-beta.N] → release-core.yml
 ├ matriz {win-x64, win-arm64}: publish autocontenido + R2R · SBOM CycloneDX por RID
 ├ puerta: último lab.yml en verde sobre este commit o uno posterior;
 │         estable: issue "Release verification" (guion manual + aceptación en hardware) cerrado
 ├ firma de código 1 (SignPath; aprobación en SignPath con su MFA, fuera de GitHub):
 │   Clicalo.exe, Sentinel, Launcher y TODAS las DLL (incluidas las R2R)
 ├ vpk pack --channel {stable|beta} --packId Clicalo.App
 ├ firma de código 2: Setup.exe + Update.exe (≤2 aprobaciones por versión)
 ├ generar releases.{channel}.json + release-files.json + notas ES/EN (de changes/) SIN FIRMAR → artefacto
 └ attest-build-provenance (Setup, nupkg, SBOM, manifiestos sin firmar)

en el equipo del mantenedor:  cl sign-manifest vX.Y.Z
 ├ descarga los artefactos y verifica la atestación y los hashes contra lo compilado
 ├ muestra en una línea legible por Narrador: versión, canal, seq, hashes abreviados
 ├ firma ECDSA P-256 con la llave de hardware PIV (PIN en teclado en pantalla; toque en la llave)
 └ sube los .sig y lanza release-publish.yml

release-publish.yml
 ├ verifica los .sig con las claves públicas FIJADAS (actual + siguiente); si falla, se detiene
 ├ vpk upload github (se conserva N−1 completo) · estable: wingetcreate
 └ promoción a estable: ≥7 días en beta sin regresiones en reg01.violations, emergency_releases ni informes
```

- **Accesibilidad de la llave de hardware.** Se elige un modelo *nano* que queda conectado de forma permanente, con PIN obligatorio (política `always`) y toque obligatorio (`cached`, 15 s). El sensor es capacitivo, así que se activa con el lápiz capacitivo o con cualquier contacto de piel.
  - Antes de M5 se valida con el mantenedor que puede hacer el toque.
  - Si no puede, se usa la política de toque `never` y se compensa con un **equipo de firma dedicado** (sin correo ni navegación general) en el que la llave está conectada.
- **Rotación y pérdida:** la clave «siguiente» se genera en una segunda llave guardada fuera del domicilio. El procedimiento está en `docs/runbooks/key-compromise.md`.
- **Changelogs:**
  - `CHANGELOG.md`, técnico y en inglés, lo genera **release-please**, que también actualiza `VersionPrefix`.
  - **Novedades para usuarios en ES/EN (ACT-004):** fragmentos en `changes/unreleased/*.yml` (`cl note`). Son obligatorios en los PR `feat`, `fix`, `a11y` y `perf` que tocan `src/`, salvo con la etiqueta `no-user-note`. Van al manifiesto firmado y la app los muestra.
- **Runtime autocontenido:** `patch-tuesday.yml` publica beta en ≤72 h y estable en ≤7 días cuando un CVE afecta a `Microsoft.NETCore.App` o `Microsoft.WindowsDesktop.App`. El SLA está en `SECURITY.md`.
- **Entornos protegidos:** `release-beta` y `release-stable`, con revisores. Las etiquetas `v*` están protegidas. Estas protecciones son una barrera más, **no** la que protege el canal: esa es la firma fuera de GitHub.

---

## 12. Seguridad y privacidad

### 12.1 Activos y límites de confianza

**Activos:**
- A1: la capacidad de inyectar entrada, también en apps elevadas si Clícalo está elevado o tiene uiAccess.
- A2: los textos guardados.
- A3: la clave de IA.
- A4: la integridad del documento y de las copias.
- A5: el canal de actualizaciones, la identidad del editor y la llave de firma del manifiesto.
- A6: la privacidad de uso (qué apps se usan).
- A7: la cadena de compilación.
- A8: la copia protegida del componente de sistema.

**Límites de confianza:**
- entre procesos del mismo usuario (integridad media);
- entre integridad media y alta (UIPI);
- entre la carpeta escribible por el usuario y `%ProgramFiles%`;
- entre el equipo y la red;
- entre el contenido importado y el documento;
- entre un PR de *fork* y la CI;
- entre GitHub y la firma del manifiesto.

### 12.2 Amenazas y controles

| # | Amenaza | Vector | Controles | Residual |
|---|---|---|---|---|
| T1 | Elevación mediante un perfil malicioso | Perfil compartido con App `cmd /c`, Web `file://` o una macro | Vista previa obligatoria con las acciones de riesgo desmarcadas y confirmadas una a una; App sin intérprete (se rechazan `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.wsf`, `.scr` y `.lnk` a intérpretes); Web solo http(s); UNC con confirmación; límites de tamaño, profundidad y recuento | Aceptación deliberada del usuario |
| T2 | Suplantación de la IPC | Pipe ocupado o cliente malicioso | DACL con SID, rechazo remoto, `FIRST_PIPE_INSTANCE`, verificación de PID, ruta y firma en ambos sentidos, **sin verbos que inyecten** | Mostrar el panel o una vista previa |
| T3 | Elevación por la tarea de inicio | Tarea `Highest` que apunta a un binario escribible; sustitución de archivos durante la sincronización | La tarea solo apunta a `%ProgramFiles%\Clicalo\Clicalo.Launcher.exe` (prueba de ACL); el lanzador verifica **después** de copiar a una ubicación protegida, contra un catálogo firmado y Authenticode; `minSafeVersion` monótono; sin argumentos | Un proceso medio del mismo usuario puede arrancar el Clícalo oficial elevado; UIPI y D13 impiden que lo controle. Un documento manipulado por ese malware sigue sujeto a T1 y a la exigencia de un toque del usuario |
| T4 | Elevación por UIA con uiAccess (M7) | Malware medio edita el documento e invoca un botón con una app elevada en primer plano | Hacia destinos elevados solo se acepta `IMO_HARDWARE` o una opción explícita del usuario | La opción explícita, documentada |
| T5 | Manipulación de la actualización | Cuenta de GitHub, workflow o feed comprometidos | Manifiesto ECDSA firmado **solo** con una llave de hardware fuera de GitHub (PIN + toque); CI sin acceso a la clave; `release-publish` verifica con las claves fijadas; anti-rollback del manifiesto, anti-freeze, `rollbackAllowed`, Authenticode con editor fijado (SignPath aprueba con su propio MFA), hash por archivo | Compromiso simultáneo del equipo del mantenedor, su PIN y el toque físico, **y** de la firma de código |
| T6 | Binarios manipulados en `%LocalAppData%` | Malware del mismo usuario | Firma verificada antes de elevar con UAC; la ejecución elevada sin UAC solo usa la copia protegida; `SetDefaultDllDirectories(APPLICATION_DIR \| SYSTEM32)`; autocontenido | Se acepta en la ejecución no elevada: es el mismo usuario |
| T7 | Filtración por los registros | Paquete de diagnóstico, *issues* | Tipos sensibles, CLC0003, canarios, vista previa exacta, fixtures v1 anonimizados | — |
| T8 | Filtración o abuso vía IA | Inyección de prompt o datos de más | **Exactamente 4 datos** (PLA-008) y prueba de que no sale nada más; validación con esquema; solo Pulsar; filtrado y marcado de combinaciones | Nombres engañosos, visibles en la vista previa |
| T9 | Percepción de *keylogger* o antivirus | *Hook* LL | *Hook* de teclado temporal, indicador visible, 30 s como máximo, sin registrar; seguimiento del puntero por WinEvent (y `WH_MOUSE_LL` pasivo solo como repliegue); binario firmado, sin empaquetadores | — |
| T10 | Denegación de servicio | Documento enorme, `clicalo://` gigante, spam por IPC, bomba zip | Límites de tamaño, `SafeZipReader`, 4 instancias de pipe, 2 s de tiempo máximo y 10 peticiones por segundo | — |
| T11 | Portapapeles | Historial o nube | Formatos de exclusión y restauración condicionada al número de secuencia | Lecturas de terceros durante 500 ms |
| T12 | Cadena de suministro | Paquete o acción comprometidos | *Lockfiles* en modo bloqueado, `packageSourceMapping`, `trustedSigners`, NuGetAudit, SHA fijados, Renovate con revisión y sin fusión automática en dependencias de runtime, Scorecard, SBOM, atestación, commits firmados | Dependencias con un solo mantenedor (planes de salida en `docs/architecture/dependencies.md`) |
| T13 | Abuso del proxy de IA | Cuota gratis como LLM genérico | **No aplica en la 2.0** (proxy diferido). Cuando exista: prompt en el servidor, entrada enumerada, límites, presupuesto con corte, interruptor firmado | — |
| T14 | PR malicioso en la CI | *Fork* | Sin secretos, sin `pull_request_target`, laboratorio solo con etiqueta y aprobación, CodeQL | — |
| T15 | Hilo del motor zombi | Cuelgue y reanudación tras la emergencia | Valla de generación bajo *lock*; escalada a reinicio del proceso si no se toma la valla | — |

`docs/security/threat-model.md` es el documento vivo. Se revisa en cada ADR que toque un límite de confianza.

### 12.3 Privacidad: datos que salen del equipo (LOG-002)

| Destino | Qué | Cuándo |
|---|---|---|
| Feed de actualizaciones | Un GET anónimo (la IP es inevitable) | Al arrancar y cada 24 h; desactivable |
| Proveedor de IA (clave propia) | **Solo los 4 datos de PLA-008**: `appName`, `keyboardLayout`, `appsLang`, `uiLang` | Acción explícita con consentimiento |
| Correo o GitHub (opinión) | Lo que el usuario ve en la vista previa exacta | Acción explícita |

- Una prueba de Infrastructure intercepta la petición HTTP del adaptador de IA y exige que el cuerpo solo contenga esos 4 valores más la plantilla fija del prompt.
- No hay telemetría ni informes de fallo automáticos. `PRIVACY.md` lo documenta, y SignPath Foundation lo exige.

---

## 13. Convenciones de ingeniería

**Idioma:**
- Código, identificadores, comentarios de código, títulos de PR y `CHANGELOG.md`: **inglés**.
- Documentación de arquitectura, ADR, guías y runbooks: **español** como fuente.
- `README`, `CONTRIBUTING` y `SECURITY`: bilingües.
- Textos de producto: solo en `data/i18n`.

**Estilo:**
- CSharpier y `.editorconfig` con reglas IDE como error; *file-scoped namespaces*; sin `this.`; `var` según `.editorconfig`.
- `sealed` por defecto; records inmutables para el estado; `required` en lugar de constructores enormes.
- `Result<T>` para errores esperados y excepciones solo para defectos. Toda frontera de error acaba en `ReleaseAll` si el motor está implicado.
- `TimeProvider` en todo lo que dependa del tiempo; `CancellationToken` en todo lo asíncrono.
- Ningún tipo de UI por debajo de UI.Wpf; ningún texto visible literal.
- Todo umbral de tiempo o de recuento vive en `timings.json` y se genera como constante. Una prueba de Data prohíbe umbrales duplicados con valores distintos.

**Commits y PR:**
- Fusión **solo por *squash***. El título del PR es el commit y sigue Conventional Commits.
- **Tipos:** `feat`, `fix`, `perf`, `a11y`, `i18n`, `refactor`, `test`, `build`, `ci`, `docs`, `chore` y `revert`.
- **Ámbitos cerrados:** `panel`, `tab`, `bubble`, `cc`, `editor`, `templates`, `ai`, `engine`, `keysafety`, `touch`, `platform`, `windowing`, `foreground`, `data`, `migration`, `updates`, `launcher`, `i18n`, `theme`, `a11y`, `build`, `deps`.
- `!` solo si se rompe un contrato público.
- Commits firmados con SSH y DCO (`Signed-off-by`). Todo lo configura `cl setup`.

**Ramas:**
- *Trunk-based*: `main` siempre en verde, protegido e historial lineal.
- Ramas cortas `feat/…`, `fix/…`, `spike/Sn-…`, que se borran al fusionar.
- No hay ramas de versión, salvo `release/2.x` para parches si alguna vez hay dos mayores con soporte.

**ADR y documentación de decisiones:**
- **ADR (MADR 4, `docs/adr/`) solo para decisiones difíciles de revertir:**
  - un límite de confianza;
  - un formato persistido o un contrato público;
  - el framework;
  - el modelo de procesos o de estado;
  - la licencia;
  - la firma.
  
  Un ADR aceptado no se edita: se sustituye por otro.
- Las decisiones reversibles se documentan en la página de `docs/architecture/` que corresponda y se cambian con un PR normal. Ejemplos: número de dispatchers, modo de publicación o herramientas.
- La CI exige un ADR si un PR toca una ruta de `architecture/sensitive-paths.json` (límites de confianza, formatos, contratos, Launcher, `Platform.Core/Trust`).
- Los cambios de requisito o de texto de producto se registran en §6.1 del catálogo, y solo el usuario los ratifica.
- RFC: se introducen cuando haya 3 mantenedores.

**Revisión:**
- `CODEOWNERS` exige un mantenedor en `Platform.Core/**`, `Platform.Windows/{Input,Foreground,SystemComponent}/**`, `Sentinel/**`, `Launcher/**`, `UI.Wpf/Windowing/**`, `Application/Foreground/**`, `Infrastructure/{Persistence,Updates}/**`, `Application/Ipc/**`, `.github/workflows/**`, `nuget.config`, `Directory.Packages.props` y `generators/Clicalo.Analyzers/**`.
- Las plantillas y la localización admiten revisores de la comunidad.
- Hoy basta con la CI en verde (hay un único mantenedor). Con equipo, se exigen dos revisiones en las rutas sensibles.

**Flujo diseñado para voz y pantalla táctil (`cl <verbo>`, una sola palabra):**
- `setup`, `build`, `fast` (Core.slnf, <45 s), `test`, `desk`, `fix`, `check` (idéntico a `verify`), `run` (datos aislados en `%TEMP%\clicalo-dev`), `states` (instantáneas de todos los estados), `accept`, `trace`, `note`, `pr`, `beta`, `perf`, `sign-manifest`.
- La salida acaba en una línea legible por Narrador. Los errores largos se escriben en un archivo Markdown que se abre en VS Code.
- VS Code tiene una tarea por cada verbo, guardado automático y CSharpier al guardar. No hay diseñadores visuales: `cl states` con `dotnet watch` hace ese papel.

**`AGENTS.md`** (con `CLAUDE.md` apuntando a él) contiene:
- el mapa de capas y de hilos;
- los verbos de `cl` y la regla «termina con `cl check`»;
- «nunca edites lo generado», «`[Req]` en todo requisito tocado», «textos en ambos JSON», «un ADR si cambias un límite de confianza, un formato o un contrato» y «nunca rebajes un requisito: propón en §6.1 del catálogo».

Como las reglas viven en los analizadores, un agente recibe el mismo error que una persona.

**Gobierno (proporcional al tamaño del proyecto):**
- Licencia MIT con DCO.
- Mantenedor principal: Michael Coaguila (en `README.md` y `CODEOWNERS`). `GOVERNANCE.md` se escribe cuando se incorpore un segundo mantenedor, con esta pauta: colaborador tras 3 PR significativos y mantenedor por invitación, con 2FA y firma.
- Informes privados de vulnerabilidades en GitHub, con primera respuesta en 72 h, parche crítico en 7 días y divulgación a 90 días.
- Contributor Covenant 2.1.
- Plantillas de *issue* como formularios accesibles.
- Etiquetas `type/*`, `area/*`, `priority/P0–P3`, `good first issue`, `needs-spike`, `flaky`, `security` y `upstream-wpf`.

---

## 14. Hoja de ruta por hitos

Este plan sustituye al plan de fases del paquete. Las duraciones son estimaciones para una sola persona, no compromisos. Un hito se cierra solo cuando cumple **todos** sus criterios.

| Hito | Contenido | Criterios de salida verificables |
|---|---|---|
| **M0 · Cimientos y arnés** (≈2 semanas) | Esqueleto (§5); `cl`; `Directory.*`; `nuget.config`; ADR 0001, 0002 y 0015; `pr.yml` (x64 + ARM64); `Clicalo.Analyzers` (CLC0001, 0003, 0004, 0006 y 0010); `LocalizationGenerator` más `cl i18n-import`; `TokenGenerator` con contraste; `CatalogGenerator` (incluido `timings.json`); InputProbe; TestKit con `RenderSnapshot`; **S0** | CI en verde en un PR vacío; `cl check` idéntico en local y en CI; ArchUnit falla ante una referencia prohibida (prueba negativa); las 669 claves importadas con paridad y la instantánea de «texto visible idéntico» en verde; tokens generados sin CLCT002 (con los casos de TEM-004 corregidos); S0 resuelto |
| **M1 · Spikes de riesgo** (≈5–6 semanas) | Bloqueantes primero: S1, S3, S4 y S2; después S5, S7, S15, S9, S6, S14, S8, S10, S11 y S12. **Cada spike se escribe como prueba** en `Windowing/Platform.IntegrationTests` y deja su informe en `docs/testing/spikes/` | Todos los criterios de §15 superados, o una decisión registrada (y, si afecta a un requisito, una propuesta al usuario). **Punto de decisión:** si S1, S3 o S4 fallan en WPF, ADR-0001 se reabre antes de escribir funcionalidad. Decisiones de dispatchers (S2), publicación (S5) y desenfoque (S6) documentadas con datos |
| **M2 · Esqueleto andante** (≈6–8 semanas) | Domain núcleo (Keys, Library, Settings, Execution, KeySafety, Touch); `DocumentStore` con deshacer; persistencia completa (documento y uso) con cuarentena; `EngineHost` más *ledger* v2, `InjectionGate` y Sentinel; `PointerInputSource`; panel mínimo con un perfil; Tap, Hold y Toggle en los dos modos; importador v1 con `SafeZipReader`; bandeja propia con `TrayMenuHost`; `ForegroundOrchestrator` (concesiones `TrayMenu` y `ControlCenter`); `ActivationGuard` por mensajes | Tocar → `SendInput` en InputProbe con p95 ≤50 ms en el equipo táctil; propiedades INV-1 a 12, «muerte en cada paso» y «congelar y reanudar» en verde con 10 000 casos; caos de Sentinel 50 de 50; `CrashingFileSystem` sin ningún documento perdido; importación de los 3 archivos reales 210 → 210; `reg01.violations = 0` en la suite de no activación y la prueba negativa en verde; prueba de bandeja con el Bloc de notas en verde |
| **M3 · Panel completo** (≈8–10 semanas) | Todas las formas y superficies (Pestaña, laterales, burbuja, menú); perfiles automáticos y Auto/Fijo; Frecuentes; repetidos; búsqueda con la concesión `TextInput` y la escalera por origen; `InteractionStore` (avisos, captura, Modo prueba, atenuado con `DimPolicy`); teclas fijas; Texto, Mouse (con `PointerPositionTracker`), Macro, Web, App y Sistema (en el hilo Shell); números de voz; los 4 temas; idioma en caliente | Reglas UIA001–010 en verde en la matriz de estados; instantáneas de renderizado aprobadas contra el prototipo; guion manual con Narrador y voz superado en Windows 10 y 11; presupuestos de §10.3 en el equipo táctil; tabla de transiciones de PAN-001 al 100 %; tabla de `DimPolicy` al 100 %; todos los MUST de los módulos panel, motor, táctil y seguridad con `[Req]` |
| **M4 · Centro de control y bienvenida** (≈8–10 semanas) | Editor de las 3 columnas y de todos los tipos (con 🎤 en cada campo libre); grabación con el *hook*; biblioteca y plantillas locales; vincular y «Probar ahora» (caso de uso); copias, importación y exportación; ajustes guiados por descriptores; bienvenida con migración; modo teclado y voz | Recorridos E2E (bienvenida sin teclado físico, crear cada tipo, vincular y probar, cerrar el CC devuelve el foco) en verde; S4 repetido sobre formularios reales con solo teclado en pantalla y dictado, 20 de 20 por origen; prueba de facetas de `ActionKind` y reglas R4 y R7 en verde; el *hook* sobrevive a GC forzados en S7 |
| **M5 · Servicios y distribución** (≈4–6 semanas) | Velopack con `SignedManifestSource`, canales, reversión con `rollbackAllowed` y desinstalación propia; firma de código; `cl sign-manifest` con llave de hardware y `release-publish.yml`; **componente de sistema y Launcher** (SIS-002); actualización delegada desde una instancia elevada; IA con clave propia; paquete de diagnóstico; `patch-tuesday.yml`; winget | Primera **beta firmada** publicada con el manifiesto firmado fuera de GitHub; actualización delta, reversión legítima a N−1 y rechazo de las bajadas atacantes y de un paquete sin la firma esperada verificados; inicio elevado sin UAC en 20 de 20 inicios de sesión y dentro de NFR-001; actualización desde una instancia elevada sin archivos de propietario Administradores; prueba canario en verde; prueba de «4 datos exactos» de la IA en verde |
| **M6 · Endurecimiento y 2.0.0** (≈4 semanas) | Los 60 hallazgos de la Auditoría cerrados y trazados; revisión del modelo de amenazas; ajuste de rendimiento; puerta de trazabilidad activa; aceptación completa en hardware táctil; documentación de usuario | Ningún MUST sin prueba o sin entrada en el guion manual o en la aceptación en hardware; propuestas de §1.4 resueltas por el usuario; ≥7 días en beta sin regresiones en `reg01.violations`, `emergency_releases` ni informes; guion manual y aceptación en hardware firmados; publicación **2.0.0 estable** (x64; ARM64 según P5) |
| **M7 · Después de la 2.0** | uiAccess sobre el componente de sistema (con S13); proxy de IA si se ratifica P3 (ADR-0014); Stryker.NET y SharpFuzz; ARM64 estable; tercer idioma con Weblate; migración a .NET 12 LTS (≤6 meses tras su GA); reevaluación de Avalonia si cumple sus condiciones; decisión sobre el fin del soporte de Windows 10; `GOVERNANCE.md` al llegar un segundo mantenedor | Cada punto con su spike aprobado y su ADR cuando sea difícil de revertir |

---

## 15. Riesgos abiertos y spikes

### 15.1 Spikes con criterio de éxito

| Spike | Objetivo | Criterio de éxito | Si falla |
|---|---|---|---|
| **S0 · Escritorio de CI** (1 día) | Comprobar si `windows-2025` y `windows-11-arm` alojados permiten FlaUI, `SendInput`, `InjectSyntheticPointerInput` y `RenderTargetBitmap` | 10 de 10 ejecuciones sin fallos por falta de escritorio | `desk-smoke` pasa al equipo táctil con la etiqueta `run-lab`; en ARM64 solo UI en proceso |
| **S1 · No activación** (1 semana, bloqueante) | Panel, Pestaña con lateral y burbuja sobre `NonActivatingWindow`; dedo, lápiz y mouse; Word, Chrome, VS Code, Bloc de notas, una app de la Tienda y otra elevada; IME japonés o chino activo; mover entre monitores de distinto DPI (#7561); menús de la app objetivo abiertos; `ActivationGuard` por mensajes con activación forzada | Ningún `WM_ACTIVATE`, `WM_KILLFOCUS` ni cambio de `GetForegroundWindow` sin acción explícita; IME no cancelado; 20 de 20 por superficie; la activación forzada se detecta y se revierte en 20 de 20 | Reabrir ADR-0001 |
| **S2 · Punteros, gestos y dispatchers** (1 semana, bloqueante) | `DisableStylusAndTouchSupport` con `PointerInputSource` en el hardware real; `SetWindowFeedbackSetting`; 600 ms, repetición, >60 px, área extra, antirrebote; arrancar con Windows (#3147), conectar y desconectar la pantalla táctil (#2054), WMI en paralelo (#9752); paneo en el CC; **tocar el panel mientras el CC maqueta 2000 atajos con un único dispatcher** | Toque → frame p95 ≤50 ms; sin el círculo táctil; sin cuelgues en los tres escenarios; p95 del panel dentro de ±10 % con el CC cargado **con un dispatcher** | Si solo falla la última condición: separar los roles en dos dispatchers y repetir la medida. Si fallan las otras: reabrir ADR-0006 |
| **S3 · UIA sobre ventana no activable** (3–4 días, bloqueante) | Peers con los patrones y Name numerado; Acceso por voz (Windows 11) y Reconocimiento de voz de Windows (Windows 10) con «mostrar números», «clic 4», «clic Negrita» y «en todas partes»; Narrador táctil y con teclado; `LiveRegionChanged` y `RaiseNotificationEvent`; alto contraste en caliente; Axe sin errores | Todas las órdenes funcionan sin activar la ventana; invocar por UIA no cambia el primer plano | Diseñar y validar `InteractionMode.Voice` (20 de 20); si tampoco funciona, reabrir ADR-0001 |
| **S4 · Entrada de texto y primer plano por origen** (4 días, bloqueante) | `ForegroundOrchestrator` con la búsqueda abierta **por toque, por Acceso por voz (UIA Invoke), por Narrador, por Reconocimiento de voz de Windows y por el atajo global**; teclado táctil (`IInputPaneInterop`/`ITipInvocation`), Win+H y devolución del primer plano; escalera con el atajo interno de derechos; formularios del CC solo con teclado en pantalla y dictado; CC abierto desde el panel y la bandeja | **20 de 20 ciclos por origen**; TSF con dictado en todos los campos; el atajo interno no llega a ninguna app | Si falla un origen de voz: modo teclado y voz con el atajo global como camino documentado para ese origen. Si falla el toque: reabrir ADR-0001 |
| **S5 · Arranque y memoria** (2–3 días) | Autocontenido con R2R frente a R2R compuesto frente a dependiente del framework; x64 y ARM64; frío tras vaciar caché; inicio de sesión (con y sin Launcher); Sentinel lanzado en paralelo frente a después del primer frame; *working set* con 4 ventanas; CPU durante 8 h | Frío manual ≤1 s; **al iniciar sesión ≤1 s**; ≤120 MB; ≤0,5 % de CPU | Plan de reducción (ruta crítica, recursos perezosos); si aun así no se cumple, propuesta P1 al usuario |
| **S6 · Capacidad visual** (2 días) | Opacidad del 30 al 100 % (`AllowsTransparency` frente a DWM con `LWA_ALPHA`); **desenfoque con `DWMWA_SYSTEMBACKDROP_TYPE` en Windows 11 combinado con la opacidad**, y la ausencia justificada en Windows 10; sombras con margen que deja pasar clics; fuentes incrustadas; Material Symbols FILL 0 y 1; temas generados; ARM64 con canales R y B (#11847) en el runner alojado | 60 fps en las animaciones; el margen no captura clics; los colores coinciden con OKLCH (ΔEOK <0,02); decisión documentada sobre el desenfoque | Renderizado por software solo en las ventanas afectadas; sin desenfoque (PAN-003 es SHOULD) con la justificación en el catálogo |
| **S7 · Inyección y hook** (2 días) | es-ES, en-US, es-419 y AltGr; lados; **modo VK y modo scancode**; Unicode; relanzar elevado e inyectar en una app elevada; *hook* LL con GC completos forzados y carga de UI; soltar al bloquear, suspender y cambiar de app | 100 % de eventos correctos en InputProbe en ambos modos; el *hook* no se retira en 30 minutos de estrés | *Hook* trasladado al Sentinel |
| **S8 · Distribución y firma** (2 días) | `vpk pack` con canales, delta, reversión con `rollbackAllowed`, desinstalación propia y desde Configuración, `SignedManifestSource`, `cl sign-manifest` con la llave de hardware (incluida la validación de que el mantenedor puede tocarla), firma con SignPath o un OV en la nube, nueva firma de las DLL R2R, SmartScreen con un binario nuevo; actualización desde una instancia elevada | Todo el flujo automatizado salvo la firma del manifiesto (un verbo); ninguna DLL sin firma; se rechaza un paquete sin la firma esperada; propietario de los archivos correcto tras una actualización desde una instancia elevada; los datos se conservan al desinstalar desde Configuración | Proveedor alternativo (ADR-0013); política de toque `never` con equipo de firma dedicado; si no hay firma, no se publica en estable |
| **S9 · Guardián, ledger y valla** (3 días) | Sentinel AOT con *handles* heredados; `TerminateProcess` durante un Mantener de Ctrl+Shift, un arrastre y una macro; relanzamiento según `CleanShutdown`/`NoRelaunch` (no relanza en actualización ni en traspaso); bucle de fallos; **motor congelado dentro y fuera de `SendInput`** | Liberaciones en ≤200 ms en 50 de 50; modo seguro tras 3 fallos en 10 minutos (`timings.json`); ninguna tecla pegada al reanudar un hilo congelado; escalada a reinicio cuando no se toma la valla | Replantear ADR-0004 |
| **S10 · IPC y etiquetas** (1 día) | Servidor elevado con SACL Media frente a cliente medio; ocupación del pipe | Detección 10 de 10; el cliente medio solo puede pedir `Show` | — |
| **S11 · Persistencia hostil** (2 días) | Defender, indexador, un monitor que bloquea el archivo y sincronización simulada; enumeración de los puntos de fallo; documento y uso | Ningún documento perdido o de fábrica; error visible si el bloqueo dura más de 3 s | — |
| **S12 · Bloqueo y suspensión** (1 día) | Win+L y suspensión con un Mantener activo; `SendInput` en el escritorio seguro y reintento | Estado vacío al desbloquear o reanudar en 20 de 20 | — |
| **S13 · Origen de la entrada** (1 día; en M7) | `GetCurrentInputMessageSource` con dedo, lápiz, mouse, `InjectTouchInput` y Acceso por voz | Distinción fiable entre hardware e inyectado | uiAccess sin inyección hacia apps elevadas |
| **S14 · Componente de sistema** (3 días) | Instalación con un UAC; tarea `HighestAvailable` al iniciar sesión y bajo demanda; Launcher: camino rápido, sincronización, verificación tras la copia, rechazo de un archivo alterado entre copia y verificación, `minSafeVersion`; relanzamiento tras actualizar; desinstalación por los dos caminos | Inicio elevado sin UAC en 20 de 20; primer frame ≤1 s al iniciar sesión sin sincronización; ningún archivo no verificado se ejecuta elevado | Propuesta P4-B al usuario (rebajar SIS-002 a SHOULD en la 2.0) |
| **S15 · Posición del puntero** (1 día) | `EVENT_OBJECT_LOCATIONCHANGE(OBJID_CURSOR)` con toque, lápiz, mouse, Acceso por voz y apps de la Tienda, elevadas y de escritorio remoto; `WH_MOUSE_LL` pasivo como alternativa (coste y reacción de Defender) | El clic derecho, la rueda y el arrastre actúan en el último punto externo en 20 de 20 por escenario | Activar el repliegue `WH_MOUSE_LL` pasivo |

### 15.2 Riesgos abiertos

| Riesgo | Probabilidad e impacto | Mitigación | Señal de alarma |
|---|---|---|---|
| WPF se estanca o sale de .NET 12/14 | Baja / alto | Capa UI sustituible; ADR-0001 con criterios de reapertura (WPF ausente en un RC de .NET; un fallo crítico de seguridad o accesibilidad más de 6 meses sin corregir; Avalonia con TSF y TextPattern y S1, S3 y S4 superados); revisión trimestral de dotnet/wpf | Anuncios oficiales o *issues* críticos sin respuesta |
| Arranque en frío de WPF y NFR-001 al iniciar sesión | Media / alto | R2R, ruta crítica mínima, DI perezosa, diccionarios en C#, Sentinel en paralelo, Launcher con camino rápido, medición en CI | Presupuesto del equipo táctil en rojo |
| Escalera de primer plano por voz | Media / alto | S4 por origen; atajo interno de derechos; atajo global y modo teclado como repliegue | Tasa de `foreground.lease_denied` |
| UIA con ventana no activable | Media / alto | S3 y modo voz | S3 |
| Seguimiento del puntero incompleto | Media / medio | S15; repliegue `WH_MOUSE_LL` pasivo | Informes de clics sobre el panel |
| Intervalo sin guardián al arrancar | Baja / medio | Sentinel en paralelo (<200 ms); protecciones en proceso; soltado preventivo | Registros `startup.sentinel_late` |
| Fin del ESU de Windows 10 (13-10-2026) frente al requisito de Windows 10 22H2 o posterior | Alta / medio | El soporte se mantiene en la 2.x (el requisito es vinculante); VM de Windows 10 en el equipo táctil; ADR-0016 fija una revisión formal en la primera versión menor posterior a octubre de 2027 | Coste de la matriz y fallos exclusivos de Windows 10 |
| Elegibilidad para la firma de código | Media / alto | SignPath Foundation (MIT); alternativas en ADR-0013 | S8 |
| Llave de hardware inaccesible o perdida | Baja / alto | Validación de accesibilidad en S8; segunda llave con la clave «siguiente»; runbook de compromiso | No se puede firmar una versión |
| Mantenimiento de Axe.Windows y FlaUI | Media / medio | Reglas propias; plan de salida (bifurcar, o cliente UIA3 por COM) | Sin versión en 12 meses |
| Escritorio interactivo en la CI alojada | Media / bajo | Equipo táctil propio | S0 |
| *Bus factor* de 1 | Alta / alto | Todo automatizado y documentado; operación mínima (§1.1, idea 6); ADR solo donde importa; `AGENTS.md`; el repositorio guía por sí mismo | — |
| Velopack con fuente propia firmada | Baja / medio | S8; `IUpdateService` detrás de un puerto | Cambios de API en Velopack |
| Deriva entre `keys.json` y `keys.win32.json` | Baja / medio | Prueba de integridad y envío en integración en los dos modos | CI |
| ARM64 sin hardware físico | Media / bajo | Runner alojado ARM64; estable solo tras la aceptación física (P5) | Fallos exclusivos de ARM64 |

---

## 16. ADRs iniciales

Solo decisiones difíciles de revertir. Cada entrada sigue el formato **Contexto → Decisión → Alternativas**. Las decisiones reversibles (número de dispatchers, modo de publicación, estrategia de pruebas, herramientas, versionado, catálogos, tokens y deshacer) viven en `docs/architecture/*.md`.

**ADR-0001 · Framework de UI: WPF sobre .NET 10 LTS.**
- Contexto: hay que no activar la ventana nunca, exponer un UIA fiel y tener TSF en todos los campos. Lo mantiene una persona que usa teclado en pantalla y voz.
- Decisión: WPF con capa propia de ventanas y punteros sobre Win32, y el resto independiente del framework. Criterios de reapertura en §15.2.
- Alternativas: Avalonia 12 (plan B), Qt 6.12, WinUI 3, Tauri, Electron, nativo, Flutter y PySide6.

**ADR-0002 · Monolito modular hexagonal con 8 ensamblados.**
- Contexto: se necesita escalar a un equipo sin pagar el coste de arranque de muchos ensamblados.
- Decisión: capas como ensamblados, capacidades como espacios de nombres y seis mecanismos de verificación (§4.4), con criterios de extracción.
- Alternativas: un ensamblado por módulo; procesos para motor y UI; Clean Architecture con CQRS y MediatR.

**ADR-0003 · Cuatro dueños de estado y proyecciones puras.**
- Contexto: documento, sesión, interacción transversal y motor tienen ciclos de vida e hilos distintos, y el estado compartido entre superficies necesita un dueño.
- Decisión: `DocumentStore`, `SessionStore`, `InteractionStore` (único escritor, el rol Surfaces) y `EngineHost`, todos inmutables; UI proyectada por funciones puras; deshacer por porciones.
- Alternativas: un único store tipo Redux; MVVM clásico con entidades mutables.

**ADR-0004 · Motor funcional, ledger, valla de generación y Sentinel.**
- Contexto: nunca debe quedar una tecla pulsada, ni si el proceso muere ni si un hilo colgado vuelve.
- Decisión:
  - `EngineReducer` puro con actor;
  - *ledger* v2 en memoria compartida sin nombre, con generación y modo;
  - `InjectionGate` con compare-and-send bajo *lock* y escalada a reinicio del proceso;
  - `Clicalo.Sentinel` Native AOT;
  - efectos bloqueantes en el hilo Shell;
  - `InjectionMode` por plan.
- Alternativas: estado mutable con bloqueos; guardián `--guardian` en el mismo exe; soltar a ciegas; un servicio de Windows (sesión 0); reiniciar siempre el proceso ante un cuelgue.

**ADR-0005 · Superficies no activables y `ForegroundOrchestrator`.**
- Contexto: REG-01 frente a la necesidad de escribir (BUS-002), abrir y cerrar el CC (CCM-004), «Probar ahora» (PRB-004/007) y el menú de la bandeja.
- Decisión:
  - `NonActivatingWindow` sellada, CLC0001 y CLC0002;
  - `ActivationGuard` por mensajes de activación propios;
  - un único orquestador con concesiones tipadas, escalera de derechos por origen y restauración verificada;
  - `SetForegroundWindow` en un solo archivo.
- Alternativas: confiar solo en los estilos; ventanas activables con devolución de foco a posteriori; `FocusBroker` solo para texto; `AttachThreadInput`.

**ADR-0006 · Capa de punteros propia.**
- Contexto: las pilas táctiles de WPF (WISP y pila de punteros) tienen fallos conocidos, y las acciones de mouse necesitan la última posición externa.
- Decisión: `DisableStylusAndTouchSupport`, `WM_POINTER` propio, `GestureRecognizer` puro en Domain y `PointerPositionTracker` por WinEvent (con repliegue `WH_MOUSE_LL` pasivo).
- Alternativas: la pila Stylus de WPF; la pila de punteros de WPF; `WH_MOUSE_LL` desde el día 1.

**ADR-0007 · Documento JSON versionado, escritura atómica y uso aparte.**
- Contexto: durabilidad, legibilidad y reversión a N−1, sin reescribir el documento en cada toque ni agotar las copias con cambios de uso.
- Decisión: envoltorio con `major.minor`, campos desconocidos conservados, `ReplaceFileW`, `.prev`, validación antes de escribir, cuarentena, migraciones puras sobre `JsonObject`, `usage.json` con `usageEpoch` y copias automáticas solo por porciones significativas.
- Alternativas: SQLite o LiteDB; diario WAL; esquema con un entero; uso dentro del documento.

**ADR-0008 · Secretos con DPAPI y Administrador de credenciales.**
- Contexto: LOG-003 y la clave de IA.
- Decisión: textos con DPAPI CurrentUser y entropía propia; `SecretText` sin salida `string` (`WithRevealed`); la clave en CredMan `LOCAL_MACHINE`, que no se vuelve a mostrar.
- Alternativas: cifrado con clave propia en disco; guardar la clave en el documento.

**ADR-0009 · Elevación: proceso completo elevado y componente de sistema.**
- Contexto: inyectar en apps elevadas y arrancar elevado sin UAC (SIS-002, MUST) sin abrir una escalada de privilegios.
- Decisión:
  - proceso completo elevado bajo demanda;
  - componente de sistema opcional en la 2.0, con Launcher AOT en `%ProgramFiles%` que verifica después de copiar y ejecuta solo una copia protegida;
  - la instancia elevada nunca aplica actualizaciones;
  - uiAccess en M7 sobre el mismo componente, con la mitigación T4.
- Alternativas: bróker elevado; tarea `Highest` en `%LocalAppData%`; aplazar SIS-002 al MSI por máquina.

**ADR-0010 · IPC mínima entre instancias.**
- Contexto: instancia única obligatoria y enlaces `clicalo://`.
- Decisión: pipe con DACL, verificación en ambos sentidos y solo `Show`, `OpenUri` e `ImportFile` (este último en vista previa).
- Alternativas: IPC rica para automatización; permitir varias instancias.

**ADR-0011 · Formato de i18n.**
- Contexto: 669 claves, marcadores de una letra, sin plurales y un tercer idioma en el futuro.
- Decisión: JSON plano con marcadores con nombre y sufijos CLDR, claves tipadas generadas y texto visible idéntico al del paquete.
- Alternativas: `.resx`; ICU MessageFormat; conservar los marcadores originales.

**ADR-0012 · Velopack, canales, packId y ubicación de los datos.**
- Contexto: instalación por usuario, beta, reversión y conservación de datos.
- Decisión:
  - Velopack 1.2 con `packId Clicalo.App` y datos en `%AppData%\Clicalo`;
  - N−1 durante 7 días, con reversión solo por acción del usuario y lista `rollbackAllowed`;
  - la desinstalación pregunta desde el CC; desde Configuración de Windows conserva siempre los datos.
- Alternativas: MSIX; Squirrel; un MSI por usuario.

**ADR-0013 · Firma de código y manifiesto firmado fuera de GitHub.**
- Contexto: SmartScreen, confianza en las actualizaciones y que una cuenta de GitHub comprometida no controle el canal.
- Decisión:
  - Authenticode con SignPath Foundation (alternativas: Azure Artifact Signing según el país, o OV en HSM en la nube), firmando todas las DLL tras R2R;
  - manifiesto y catálogo de archivos firmados con ECDSA P-256 **solo** mediante una llave de hardware del mantenedor (`cl sign-manifest`), con dos claves públicas fijadas, anti-rollback del manifiesto, anti-freeze e interruptores de emergencia;
  - la CI nunca tiene acceso a la clave.
- Alternativas: KMS con OIDC desde GitHub Actions; KMS con aprobación en la consola de la nube; minisign o ed25519; confiar solo en el hash.

**ADR-0014 · IA con clave propia; proxy de cuota diferido.**
- Contexto: plantillas por IA (PLA-*) con 4 datos exactos (PLA-008), un solo mantenedor y coste de operación.
- Decisión:
  - la 2.0 usa el puerto `ITemplateGenerator` sobre Microsoft.Extensions.AI con clave propia;
  - el proxy queda diseñado y condicionado: sin identificador de instalación, contrato en JSON Schema con `Clicalo.Contracts.Templates` y sin dependencia de Domain, techo antiabuso por IP con HMAC y sal diaria, cuota visible contada en el cliente a medianoche local, prompt en el servidor e interruptor firmado;
  - se activa si el usuario ratifica P3.
- Alternativas: proxy desde el día 1; un proxy que reenvía prompts; identificador de instalación.

**ADR-0015 · Licencia MIT y DCO.**
- Contexto: open source, SignPath Foundation y adopción por organizaciones de accesibilidad.
- Decisión: MIT con DCO y firma de commits.
- Alternativas: GPL; CLA.

**ADR-0016 · Soporte de Windows 10.**
- Contexto: el requisito exige Windows 10 22H2 o posterior, y el ESU de consumo termina el 13-10-2026.
- Decisión: soporte completo en la 2.x, VM de Windows 10 en el equipo de pruebas, guion con Reconocimiento de voz de Windows y revisión formal en la primera versión menor posterior a octubre de 2027.
- Alternativas: solo Windows 11 (incumple el requisito); soporte indefinido.

**ADR-0017 · Sin plugins de código de terceros.**
- Contexto: el proceso tiene *hooks*, inyección y posible elevación.
- Decisión: la extensibilidad es solo por datos validados (plantillas e idiomas). Si algún día hace falta código de terceros, irá fuera de proceso y sin privilegios, con su ADR.
- Alternativas: carga dinámica de ensamblados; *scripting*.

---

## Registro de revisión (1.0 → 1.1)

| # | Severidad | Hueco | Cómo se resolvió | Dónde |
|---|---|---|---|---|
| 1 | Alta | Hilo del motor colgado que vuelve e inyecta tras la emergencia (REG-03) | Añadida la valla de generación: `EngineGeneration` en el *ledger* v2 y `InjectionGate.TryRun(g)` con compare-and-send **bajo *lock*** para todo efecto externo (Inject, *ledger*, portapapeles, Launch y SystemCommand). `EmergencyReleaser` toma la valla con 250 ms de límite, sube la generación y suelta dentro del *lock*. Si no puede tomarla, escala a `TerminateProcess(self)` con `EmergencyRestart` y suelta Sentinel. Un segundo cuelgue en 10 min reinicia el proceso. Añadidos INV-11, el modelo CsCheck «congelar y reanudar», el caos con motor congelado y T15 | §3.2 (regla 6), §7.4, §7.5, §7.6, §7.10, §12.2, S9, ADR-0004 |
| 2 | Alta | `CompatMode` no llega al envío; «VK + `KEYEVENTF_SCANCODE`» es contradictorio | Añadido `InjectionMode {VirtualKey, ScanCode}` en `Profile` → `ActivationContext` → `EngineEvent.Activation` → `InjectedKey` → ranura del *ledger*. Modo normal: VK más scancode informativo, sin `KEYEVENTF_SCANCODE`. Modo compatible: `wVk = 0`, scancode más `EXTENDEDKEY`. Las liberaciones se hacen en el modo de la pulsación (INV-12). Casos en InputProbe y S7, con `[Req]` EJE-003 y ATJ-004 | §1.2 D24, §6.1, §6.2, §7.2–7.5, §7.7, S7 |
| 3 | Alta | Nadie captura «la última posición del puntero fuera de Clícalo» (EJE-009) | Añadido `PointerPositionTracker` en SysEvents con `EVENT_OBJECT_LOCATIONCHANGE(OBJID_CURSOR)`, sin `SKIPOWNPROCESS`, filtrado por `SurfaceRegistry`, más muestreo al cambiar de primer plano y antes del contacto. Repliegue documentado: `WH_MOUSE_LL` pasivo (coste y antivirus en T9). Llega a `MousePlanner` por `ActivationContext.LastExternalPointer`. Prueba de integración con toque sintético (20 de 20) y spike S15 | §3.1, §7.11, §10.1, §12.2 T9, S15, ADR-0006 |
| 4 | Alta | La firma del manifiesto por OIDC no protege de una cuenta de GitHub comprometida (D15, T5) | La CI solo produce el manifiesto **sin firmar**. La firma se hace únicamente con `cl sign-manifest` y una llave de hardware PIV del mantenedor (PIN y toque, con validación de accesibilidad en S8). `release-publish.yml` verifica con las claves fijadas antes de publicar. Se eliminan KMS y OIDC. T5 y su riesgo residual se corrigen al diseño real | §1.2 D15, §2, §11, §12.2 T5, S8, ADR-0013 |
| 5 | Alta | SIS-002 (MUST) quedaba fuera de la 2.0 | **Opción A:** componente de sistema opcional en la 2.0 (un UAC), con `Clicalo.Launcher` AOT en `%ProgramFiles%`, tarea `HighestAvailable`, sincronización **verificada después de copiar** a una ubicación protegida (catálogo de archivos firmado + Authenticode) y `minSafeVersion` monótono. Spike S14. La opción B (rebajar a SHOULD) solo se propondría al usuario si S14 fracasa (P4) | §1.2 D12, §1.4 P4, §3.1, §3.3, §9.3, §12.2 T3, S14, M5, ADR-0009 |
| 6 | Media | `ActivationGuard` dependía de un WinEvent filtrado con `SKIPOWNPROCESS` | Se detecta con `WM_ACTIVATE`/`WM_NCACTIVATE`/`WM_ACTIVATEAPP` en el *hook* común de `HwndSource` (síncrono y determinista). El WinEvent queda solo para el primer plano externo. Prueba negativa: InputProbe fuerza la activación y se comprueban `reg01.violations`, la restauración y el estilo | §3.5, §7.9, S1, ADR-0005 |
| 7 | Media | Menú de bandeja sobre `HWND_MESSAGE` e incompatible con la prohibición de `SetForegroundWindow` | `TrayMenuHost`: ventana propia oculta de nivel superior en SysEvents, con concesión `TrayMenu` del orquestador (restauración verificada tras `TrackPopupMenuEx` + `WM_NULL`) y `TrackPopupMenuEx` confinado en `BannedSymbols`. Prueba: Bloc de notas activo → menú → Soltar todo → el foco vuelve al Bloc de notas | §1.2 D9, §3.6, §4.4, §8.1, §10.1 |
| 8 | Media | CCM-004, PRB-004/006/007 y abrir el CC no tenían camino legal para cambiar el primer plano | `FocusBroker` se generaliza a `ForegroundOrchestrator` (actor en SysEvents) con concesiones tipadas `TextInput`, `KeyboardNavigation`, `ControlCenter`, `TryNowTarget` y `TrayMenu`, con restauración verificada, reintento y resultado (`Flashed` para PRB-007). «Probar ahora» es un caso de uso con `TimeProvider`. E2E para CCM-004 y PRB-004/007 | §1.2 D23, §3.6, §4.4, §10.1, M4, ADR-0005 |
| 9 | Media | La búsqueda abierta por voz o por conmutador no tiene derecho de primer plano | Escalera por origen: directo (toque, bandeja, atajo global) → atajo interno de derechos (`RegisterHotKey` + inyección por el motor, que otorga el `WM_HOTKEY`) → `Denied` con alternativa. Quedan prohibidos `AttachThreadInput` y el truco de Alt. S4 se amplía a toque, Acceso por voz, Narrador, Reconocimiento de voz de Windows y atajo global, con **20 de 20 por origen** | §3.6, §4.4, S4 |
| 10 | Media | Estado transversal sin dueño (avisos, captura, Modo prueba, atenuado) y atenuado ausente del plano | Cuarto almacén `InteractionState` (escritor único: rol Surfaces; publicación inmutable a Workspace). `DimPolicy` pura en Domain con la tabla de excepciones de docs/04, la burbuja al 55 % como mínimo y 350 ms. Propiedad EJE-017: el primer toque despierta y ejecuta. `[Req]` GEN-009, EJE-017 y SEG-002 | §1.2 D6, §3.2, §4.3, §6.4, §8.2, §10.1, M3, ADR-0003 |
| 11 | Media | El anti-rollback impedía distinguir una reversión legítima de un ataque de bajada | Separados: el `seq` del manifiesto nunca baja, y la versión instalada solo baja por acción explícita del usuario si N−1 está en `rollbackAllowed` del manifiesto vigente, no revocada y ≥ `minSafeVersion`. Si no se cumple, la opción no se ofrece y se explica el motivo. Pruebas de bajada legítima y de tres bajadas atacantes | §9.3, §10.1, ADR-0012, ADR-0013 |
| 12 | Media | El `install` UUID era un quinto dato que contradecía PLA-008 | Eliminado. En la 2.0 solo salen los 4 datos, y lo comprueba una prueba del cuerpo HTTP. El diseño del proxy diferido limita con `HMAC(IP, sal diaria)` y un presupuesto global | §9.2, §12.2 T8, §12.3, ADR-0014 |
| 13 | Media | Con `RecordUsage`, las 12 copias automáticas solo acababan conteniendo uso y cada toque reescribía el documento | El uso se persiste en `usage.json`, con 30 s de espera y 5 min de máximo, y `usageEpoch` para la coherencia con `ResetFrequents`. Deshacer no cambia, porque en memoria sigue siendo una porción del documento. Solo las porciones significativas disparan la copia automática. Prueba de uso intensivo simulado | §1.2 D10, §6.3, §6.4, §6.5, §6.8, ADR-0007 |
| 14 | Media | R4, R5 y R7 dependían de disciplina | R4: `IDestructiveCommand` + `ConfirmationToken`, que solo emite `TwoStepConfirm`, con el analizador CLC0010 y una lista cerrada `destructive-operations.json`. R7: prueba que recorre todos los comandos y exige `Record` salvo `undo-exemptions.json` justificado. R5: regla UIA010 (🎤 «Dictar» o «Pegar» junto a cada campo libre) | §4.4, §6.3, §8.2, §8.6, §10.2 |
| 15 | Media | Excepción a NFR-001 decidida sin autoridad; el motor ignoraba toques hasta 2 s | Se elimina la excepción: NFR-001 es puerta sin excepciones y S5 mide. Solo si S5 fracasa se abre la propuesta P1 al usuario. El motor acepta toques desde el primer frame; Sentinel se lanza en paralelo y el intervalo lo cubren las protecciones en proceso y el soltado preventivo (riesgo registrado) | §1.4 P1, §3.1, §10.3, S5, §15.2 |
| 16 | Media | Faltaban la aceptación con hardware táctil real, la regresión visual y la cobertura de ARM64 | `touch-acceptance.md` (docs/09 completo) en `release-verification.yml`, ejecutado en el equipo táctil. Instantáneas `RenderTargetBitmap` por estado con Verify y tolerancia. Runner alojado `windows-11-arm` para unitarias, UI e instantáneas; ARM64 solo en beta hasta la aceptación física (P5) | §1.4 P5, §10.1, §10.2, §10.5, §11, M6 |
| 17 | Media | Sobreingeniería para una sola persona | Proxy de IA diferido (clave propia en la 2.0). Un dispatcher por defecto, y dos solo si S2 lo mide. Un único equipo táctil dedicado bajo demanda en lugar de dos VM con nightly. Firma con llave de hardware sin KMS. Stryker y SharpFuzz después de la 2.0 (CsCheck antes). Sin Gallery (`cl states`). ADR solo para decisiones difíciles de revertir (de 31 a 17). `GOVERNANCE.md` y RFC al crecer. Se mantienen las fronteras, ArchUnit y los analizadores | §1.1 (idea 6), §1.2 D8/D15/D16, §2, §5, §10, §13, §14, §16 |
| 18 | Media | Actualizar desde una instancia elevada rompía los propietarios, y Sentinel podía competir | Una instancia elevada nunca descarga ni aplica: delega en `Clicalo.exe --apply-update` en integridad media (mediante el token del shell) y vuelve a elevado con la tarea. `CleanShutdown \| NoRelaunch` en `Terminal(Update\|Relaunch\|Exit)`, y Sentinel solo relanza sin esas marcas. Comprobación defensiva de propietario al arrancar. Pruebas en S8 y S9 | §3.1, §3.3, §7.6, §9.3, S8, S9 |
| 19 | Baja | Contradicciones menores | Umbrales unificados en `timings.json` (3 fallos en 10 min para guardián y app) con una prueba anti-duplicados. Guion de Windows 10 con Reconocimiento de voz de Windows. `SecretText.Reveal()` sustituido por `WithRevealed(ReadOnlySpanAction)`. Registro en `%AppData%\Clicalo\logs\clicalo.log` con nombre fijo (`FixedNameRollingFileSink`, 5 × 1 MB, según docs/08). PQ-35: instancia única obligatoria y propuesta de quitar [rSingle] (P2) | §3.1, §3.4, §6.2, §6.5, §9.4, §10.2, §13, §1.4 P2 |
| 20 | Baja | `Launch` y los comandos de sistema bloqueaban el hilo del motor | Añadido el efecto `SystemCommand(id)`. `Launch` y `SystemCommand` se ejecutan en el hilo Shell (STA, BelowNormal), con resultados `LaunchCompleted`/`LaunchFailed`/`SystemCommandCompleted` de vuelta al buzón y la generación comprobada | §3.1, §3.2, §4.4, §7.1, §7.3 |
| 21 | Baja | El proxy dependía del ensamblado Domain | Contrato `data/schemas/ai-template.v1.schema.json` con `contractVersion`. Al activar el proxy, validador compartido `Clicalo.Contracts.Templates` sin dependencia de Domain; el cliente valida estructura (esquema) y semántica (Domain) por separado | §4.2, §9.2, §11, ADR-0014 |
| 22 | Baja | Desinstalación desde Configuración de Windows sin comportamiento definido; zips v1 sin límites | Desde Configuración siempre se conservan los datos (prueba en S8), y al reinstalar la bienvenida ofrece conservar o empezar de cero (P6). `SafeZipReader` con límites de tamaño total, entradas, relación de compresión, rutas y anidación, y generadores CsCheck hostiles (SharpFuzz después de la 2.0) | §1.4 P6, §6.6, §9.3, §10.1, §12.2 T10 |
| 23 | Baja | Cuota de IA por día UTC frente a medianoche local; el desenfoque no se validaba | Cuota visible contada en el cliente por fecha local, con el techo antiabuso del servidor por día UTC, sin enviar la zona horaria (en el diseño diferido; P3). Desenfoque: fondo de sistema de DWM en Windows 11 si S6 confirma la combinación con la opacidad; ausencia justificada en Windows 10 y con los efectos de transparencia desactivados | §8.1, §9.2, S6 |