---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: evaluación de WPF; verificación adversarial «Foco y táctil»; crítica del plano 1.0 (hueco «Motor / acciones de mouse»)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0006 · Capa de punteros propia sobre `WM_POINTER` y seguimiento del puntero externo

## Contexto y planteamiento del problema

El público principal de Clícalo tiene poca precisión: toques dobles involuntarios, contacto breve,
arrastres accidentales, apoyo de la palma. El catálogo exige entrada táctil nativa con datos de
contacto (ACC-007), un único algoritmo de filtro táctil para todas las superficies (TAC-002), objetivos
de 44×44 con área ampliada que resuelve por el centro más cercano (REG-02) y una respuesta del toque al
envío en menos de 50 ms (NFR-001).

Las dos pilas táctiles de WPF tienen fallos abiertos que tocan de lleno a Clícalo:

- **Pila Stylus (WISP):** la app arrancada al iniciar Windows se queda colgada esperando al hilo táctil
  (#3147); una ventana `Topmost` deja de recibir toques tras desconectar un dispositivo (#2054); un
  `ManagementEventWatcher` de WMI mata el táctil (#9752).
- **Pila de punteros** (`EnablePointerSupport`): cuelgue en Surface Pro 9 (#8435), coordenadas mal en la
  pantalla secundaria (#8517) e `IsPressAndHoldEnabled` ignorado (#5939).

Además, las acciones de mouse (EJE-009) actúan en la **última posición del puntero fuera de Clícalo**:
al tocar el panel, Windows lleva el cursor al punto del toque, así que `GetCursorPos` devuelve un punto
sobre el panel. La versión 1.0 del plano no capturaba esa posición en ningún sitio. ¿Cómo se recibe el
toque de forma fiable y cómo se sabe dónde actuar con el mouse?

## Factores de decisión

- Fiabilidad al arrancar con Windows, al conectar y desconectar la pantalla táctil y con WMI en paralelo.
- Un único algoritmo de filtro y de gestos, puro y probable con reloj simulado (TAC-002, NFR-020).
- Datos de contacto reales (`rcContact`) para detectar la palma.
- Latencia toque → frame p95 ≤ 50 ms.
- Sin *hooks* globales permanentes que parezcan un *keylogger* (NFR-009, amenaza T9).
- Reutilizable si algún día se migra a Avalonia (ADR-0001).

## Opciones consideradas

- Capa propia: `DisableStylusAndTouchSupport`, `PointerInputSource` sobre `WM_POINTER*`,
  `GestureRecognizer` puro en Domain y `PointerPositionTracker` por WinEvent
- La pila Stylus de WPF (WISP)
- La pila de punteros de WPF (`EnablePointerSupport`)
- `WH_MOUSE_LL` permanente desde el día 1 para seguir el puntero

## Resultado de la decisión

Opción elegida: **«Capa de punteros propia con `GestureRecognizer` puro y `PointerPositionTracker` por
WinEvent»**, porque evita los fallos conocidos de ambas pilas de WPF, deja el reconocimiento de gestos
como una función pura compartida por todas las superficies y sigue el puntero externo sin un *hook* de
bajo nivel permanente.

- **Entrada.** Se activa el conmutador de `AppContext`
  `Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport=true`. `PointerInputSource`
  (`Clicalo.UI.Wpf.Pointer`) procesa `WM_POINTER*` en cada superficie con `GetPointerTouchInfo` y
  `GetPointerPenInfo`, registra el origen (`GetCurrentInputMessageSource`) y produce `PointerFrame`.
- **Gestos.** `GestureRecognizer` y `TouchFilter` viven en `Clicalo.Domain.Touch`, son puros y usan
  `TimeProvider`. `HitResolver` resuelve el área ampliada por el centro más cercano. Las constantes
  (`LongPress = 600 ms`, `SwipeMinPx = 60`, `SwipeSlope = 0.6`, `PostSwipeLock = 300 ms`,
  `DragThreshold = max(6, cancelMovePx)`) se generan desde `timings.json`. La zona de prueba y el Modo
  prueba llaman a la misma función: no hay una segunda implementación.
- **Puntero externo.** `PointerPositionTracker` (`Clicalo.Platform.Windows`, hilo SysEvents) usa
  `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` fuera de contexto, sin `WINEVENT_SKIPOWNPROCESS` y
  filtrado a `OBJID_CURSOR`. Descarta los puntos dentro de `SurfaceRegistry`, confirma con
  `WindowFromPoint` y guarda `LastExternalPointer`. Se complementa con un muestreo al cambiar el primer
  plano externo y al entrar un contacto. **Repliegue** si S15 muestra huecos: un `WH_MOUSE_LL` pasivo que
  solo copia el punto a una variable atómica.
- **Centro de control y bienvenida** (ventanas activables): el toque llega como mouse promovido; el
  paneo de listas lo aporta `PointerInputSource` y `TouchKeyboardInvoker` abre el teclado táctil cuando
  un campo recibe el foco por toque.

### Consecuencias

- Buena, porque TAC-002 es una única función en todas las superficies, probada con trazas reales.
- Buena, porque se evitan #3147, #2054 y #9752, y la capa se reutilizaría en Avalonia.
- Buena, porque las acciones de mouse actúan donde el usuario tenía el puntero, sin un *hook* de mouse
  permanente salvo que S15 lo exija.
- Mala, porque `ScrollViewer.PanningMode` deja de funcionar: el desplazamiento táctil del Centro de
  control es propio.
- Mala, porque la capa es código de interoperabilidad delicado que hay que revalidar tras actualizaciones
  grandes de Windows o hardware nuevo.

### Confirmación

- Spike S2: toque → frame p95 ≤ 50 ms, sin círculo táctil, sin cuelgues al arrancar con Windows, al
  desconectar la pantalla táctil ni con WMI en paralelo, y paneo usable en el Centro de control.
- Spike S15: clic derecho, rueda y arrastre en el último punto externo en 20 de 20 por escenario (toque,
  lápiz, mouse, Acceso por voz, apps de la Tienda, elevadas y de escritorio remoto).
- `Clicalo.Platform.IntegrationTests`: el mouse sintético se coloca en un punto P de InputProbe y un
  toque sintético en el panel ejecuta «clic derecho»; InputProbe recibe los mensajes en P en 20 de 20,
  con `[Trait("Req", "EJE-009")]` y `[Trait("Req", "FIJ-006")]`.
- Trazas de puntero reales como *fixtures* (`tests/fixtures/pointer/`), grabadas de nuevo en cada pasada
  de aceptación en hardware.

## Pros y contras de las opciones

### Capa propia sobre `WM_POINTER`

- Buena, porque controla los datos de contacto y la latencia de extremo a extremo.
- Buena, porque el reconocedor es puro y portable.
- Mala, porque hay que reimplementar el paneo y la invocación del teclado táctil en el Centro de control.

### Pila Stylus de WPF (WISP)

- Buena, porque es la integrada y da eventos de alto nivel.
- Mala, porque sus fallos abiertos (#3147, #2054, #9752) afectan justo al arranque con Windows y a una
  ventana siempre encima.

### Pila de punteros de WPF

- Buena, porque se basa en `WM_POINTER`.
- Mala, porque tiene sus propios fallos abiertos (#8435, #8517, #5939) y no expone el control que exige
  el filtro táctil.

### `WH_MOUSE_LL` permanente desde el día 1

- Buena, porque vería todo movimiento del puntero.
- Mala, porque añade una llamada por cada movimiento del mouse en todo el sistema y aumenta la
  percepción de *keylogger* por parte de los antivirus (T9) sin haber demostrado que haga falta.

## Criterios de reapertura

- Si S2 falla en algo distinto de la condición de dispatchers (latencia, círculo táctil o cuelgues), se
  reabre este ADR ([§15.1](../architecture/blueprint.md#151-spikes-con-criterio-de-éxito)).
- Si S15 muestra huecos, se activa el repliegue `WH_MOUSE_LL` pasivo, previsto aquí y documentado en el
  [modelo de amenazas (T9)](../security/threat-model.md); no hace falta un ADR nuevo.

## Más información

- Plano: [§1.3, fila «Reconocedor de gestos»](../architecture/blueprint.md#13-contradicciones-entre-las-propuestas-y-cómo-se-resolvieron),
  [§7.1](../architecture/blueprint.md#71-del-toque-a-la-acción),
  [§7.8](../architecture/blueprint.md#78-filtro-táctil-y-gestos-domaintouch),
  [§7.11](../architecture/blueprint.md#711-posición-del-puntero-para-acciones-de-mouse-eje-009),
  [§8.3](../architecture/blueprint.md#83-entrada-táctil).
- Catálogo: REG-02 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  EJE-009 en [§2.12](../requirements/catalog.md#212-eje--ejecución-de-acciones); TAC-002 en
  [§2.17](../requirements/catalog.md#217-tac--filtro-táctil-precisión-táctil-y-modo-prueba); ACC-007 en
  [§2.34](../requirements/catalog.md#234-acc--accesibilidad-y-voz).
- Registro: [techEvaluations.json](../architecture/decision-record/techEvaluations.json) (evaluación de
  WPF, criterio (a)), [techVerification.json](../architecture/decision-record/techVerification.json) y
  [critique.json](../architecture/decision-record/critique.json).
- Evidencia:
  [desactivar la pila Stylus en WPF](https://blog.walterlv.com/post/wpf-disable-stylus-and-touch-support.html),
  [desactivar RealTimeStylus](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/disable-the-realtimestylus-for-wpf-applications),
  [pila táctil basada en punteros de WPF](https://github.com/microsoft/dotnet/blob/main/Documentation/compatibility/wpf-pointer-based-touch-stack.md),
  [HwndMouseInputProvider.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndMouseInputProvider.cs),
  [SetWindowFeedbackSetting](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowfeedbacksetting),
  [dotnet/wpf #3147](https://github.com/dotnet/wpf/issues/3147),
  [#2054](https://github.com/dotnet/wpf/issues/2054),
  [#9752](https://github.com/dotnet/wpf/issues/9752),
  [#8435](https://github.com/dotnet/wpf/issues/8435),
  [#8517](https://github.com/dotnet/wpf/issues/8517),
  [#5939](https://github.com/dotnet/wpf/issues/5939).
- ADR relacionados: [ADR-0001](0001-framework-ui-wpf.md),
  [ADR-0005](0005-superficies-no-activables-y-foreground-orchestrator.md).
