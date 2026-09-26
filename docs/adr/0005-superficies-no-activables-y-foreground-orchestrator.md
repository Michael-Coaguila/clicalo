---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: verificación adversarial «Foco y táctil»; crítica del plano 1.0 (huecos de ActivationGuard, bandeja, foco del CC y FocusBroker por voz)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0005 · Superficies no activables y un único `ForegroundOrchestrator`

## Contexto y planteamiento del problema

La regla número 1 del producto es que el panel nunca quita el foco (REG-01): tocar, arrastrar o hacer
un toque largo en el panel, la barra, el asa, la burbuja, los menús o las ventanas al costado nunca
cambia la ventana en primer plano ni el foco de teclado. En Macro Quick Access el estilo no activable
se aplicaba **después** de mostrar la ventana, el icono de bandeja activaba la ventana y había un campo
de texto en una ventana no activable (lección L-WIN-1).

Pero hay flujos que **sí** necesitan cambiar el primer plano, de forma controlada:

- escribir en la búsqueda del panel (BUS-002, BUS-003);
- abrir y cerrar el Centro de control devolviendo el foco a la app anterior (CCM-004);
- «Probar ahora» en la app destino y volver al Centro de control (PRB-004, PRB-006, PRB-007);
- el menú de la bandeja, porque `TrackPopupMenuEx` exige el primer plano.

La verificación adversarial refutó que ningún otro camino de WPF active estas ventanas: al cambiar de
DPI, WPF llama a `SetWindowPos` sin `SWP_NOACTIVATE` (#7561), y `Popup`, `ContextMenu` y `ComboBox`
dependen de una captura del mouse que no funciona en ventanas en segundo plano. Además, Windows solo
permite `SetForegroundWindow` al proceso que recibió el último evento de entrada, lo que complica los
orígenes por voz (UIA). ¿Cómo se garantiza REG-01 y, a la vez, se da un camino legal y probado a los
flujos que necesitan el primer plano?

## Factores de decisión

- REG-01 garantizado en tres niveles: por construcción, por pruebas y en producción.
- Un único dueño de los cambios de primer plano, con restauración verificada.
- Derechos de primer plano distintos según el origen: toque, UIA (Acceso por voz, Narrador,
  Reconocimiento de voz de Windows, conmutadores), atajo global, bandeja o temporizador interno.
- Nada frágil: sin `AttachThreadInput` con el hilo del primer plano, sin el truco de enviar Alt y sin
  `LockSetForegroundWindow`, que pueden bloquear o abrir menús en la app destino.
- Detección de activaciones indebidas síncrona y determinista.

## Opciones consideradas

- `NonActivatingWindow` sellada más analizadores, `ActivationGuard` por mensajes de activación propios y
  un único `ForegroundOrchestrator` con concesiones tipadas y escalera de derechos por origen
- Confiar solo en los estilos de ventana (`WS_EX_NOACTIVATE`)
- Ventanas activables que devuelven el foco a posteriori
- Un `FocusBroker` solo para la entrada de texto (versión 1.0 del plano)
- Forzar el primer plano con `AttachThreadInput` o el truco de Alt

## Resultado de la decisión

Opción elegida: **«`NonActivatingWindow` + `ActivationGuard` + `ForegroundOrchestrator`»**, porque es la
única que cubre REG-01 en los tres niveles y da a cada flujo que necesita el primer plano un camino
legal, tipado y comprobable.

**Por construcción** ([§3.5 del plano](../architecture/blueprint.md#35-ventanas-no-activables)):

- `NonActivatingWindow` (en `Clicalo.UI.Wpf.Windowing`) es la única clase base permitida para las
  superficies del panel. Su configuración se aplica en `OnSourceInitialized`, antes del primer `Show`:
  `WS_EX_NOACTIVATE | WS_EX_TOPMOST`, una ventana propietaria oculta en lugar de `WS_EX_TOOLWINDOW` (no
  aparece en Alt+Tab ni en la barra de tareas y conserva el tratamiento normal para UIA),
  `SetWindowFeedbackSetting` sin círculo táctil y registro en `SurfaceRegistry` y `ActivationGuard`.
- Un *hook* común de `HwndSource` responde `MA_NOACTIVATE` y `PA_NOACTIVATE`, añade `SWP_NOACTIVATE` en
  `WM_WINDOWPOSCHANGING` (salvo durante una concesión de texto o de teclado) y aplica él mismo el
  rectángulo de `WM_DPICHANGED` con `SWP_NOACTIVATE` (#7561).
- En las superficies quedan prohibidos `Popup`, `ContextMenu`, `ToolTip` interactivo y `ComboBox`
  (analizador CLC0002 y `BannedSymbols.Surfaces.txt`); los menús y desplegables son `NonActivatingWindow`
  hijas. CLC0001 prohíbe `Show()` activador, `Activate()` y `ShowActivated = true`.

**En producción:** `ActivationGuard` se dispara con los mensajes de activación de la propia ventana
(`WM_ACTIVATE`, `WM_NCACTIVATE`, `WM_ACTIVATEAPP`), que son síncronos y no dependen de los WinEvent
filtrados con `WINEVENT_SKIPOWNPROCESS`. Si no hay una concesión activa para esa ventana, restaura el
primer plano anterior con verificación, vuelve a aplicar `WS_EX_NOACTIVATE`, incrementa
`reg01.violations` y, en Debug y CI, falla. `SurfaceIntegrityCheck` repara estilos y orden Z cada 30 s y
tras cambios de DPI, pantalla o tema.

**Un único dueño del primer plano** ([§3.6](../architecture/blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano)):
`ForegroundOrchestrator` (`Clicalo.Application.Foreground`, actor en el hilo SysEvents) concede
concesiones tipadas con restauración verificada y un reintento:

| Concesión | Uso | Al terminar |
|---|---|---|
| `TextInput` | Búsqueda del panel y campos de texto en superficies | Vuelve a la app anterior; si falla, no se ejecuta nada y se avisa |
| `KeyboardNavigation` | Modo teclado y voz | Igual que `TextInput` |
| `ControlCenter` | Abrir y cerrar el Centro de control (CCM-004) | Vuelve a la app anterior a su apertura |
| `TryNowTarget` | «Probar ahora» (PRB-004) | Vuelve al CC; si falla, parpadeo en la barra de tareas (PRB-007) |
| `TrayMenu` | Menú de la bandeja, en una ventana propia oculta de nivel superior | Vuelve a la app anterior tras `TrackPopupMenuEx` |

La **escalera de derechos por origen**: con toque, bandeja o atajo global basta `TrySetForeground` con un
reintento. Con una invocación UIA, si falla, el motor inyecta un atajo interno reservado y registrado
con `RegisterHotKey` (Ctrl+Alt+Shift+F24, que consume el sistema); al llegar `WM_HOTKEY`, Clícalo tiene
derecho de primer plano y reintenta. Si todo falla, `Denied` con un aviso *live* y la alternativa del
atajo global configurable.

`SetForegroundWindow` solo aparece en `Platform.Windows/Foreground/ForegroundControl.cs`,
`TrackPopupMenuEx` solo en `Platform.Windows/Tray/TrayMenuHost.cs`, y `Window.Activate` está prohibido
en todo el código.

### Consecuencias

- Buena, porque cada flujo que necesita el primer plano tiene un camino legal y una prueba.
- Buena, porque una activación indebida se detecta, se revierte y queda medida (`reg01.violations`).
- Buena, porque «¿quién cambia el primer plano?» tiene una sola respuesta en el código.
- Mala, porque hay que reimplementar menús, desplegables y ventanitas emergentes como ventanas propias.
- Mala, porque el arrastre y el mantener pulsado con mouse no pueden depender de `SetCapture`, que es
  exclusivo de la ventana en primer plano; se siguen con técnicas propias validadas en S1.
- Mala, porque el margen de sombra no clicable necesita un mecanismo que funcione entre procesos: la
  verificación refutó `WM_NCHITTEST`/`HTTRANSPARENT` para eso (solo actúa entre ventanas del mismo
  hilo). Las alternativas (`SetWindowRgn` ajustado al contorno o alfa 0 en una ventana *layered*) se
  deciden en S6 con el criterio «el margen no captura clics».

### Confirmación

- Spike S1: 20 de 20 por superficie sin cambio de primer plano ni de foco, y la activación forzada se
  detecta y se revierte en 20 de 20.
- Spike S4: 20 de 20 ciclos por origen (toque, Acceso por voz, Narrador, Reconocimiento de voz de
  Windows y atajo global).
- Prueba negativa obligatoria `ActivationGuardNegativeTests`: InputProbe, en primer plano, llama a
  `SetForegroundWindow` sobre el panel; `reg01.violations` sube en 1, el primer plano vuelve a InputProbe
  en 200 ms o menos y `WS_EX_NOACTIVATE` sigue presente.
- Prueba de bandeja: Bloc de notas activo → menú → Soltar todo → el foco vuelve al Bloc de notas.
- Criterio de M2: `reg01.violations = 0` en la suite de no activación.

## Pros y contras de las opciones

### `NonActivatingWindow` + `ActivationGuard` + `ForegroundOrchestrator`

- Buena, porque combina construcción, pruebas y vigilancia en producción.
- Buena, porque las concesiones tienen tipo, prioridad y restauración verificada.
- Mala, porque añade un actor y una escalera de derechos que hay que probar por origen.

### Confiar solo en los estilos de ventana

- Buena, porque es lo más simple.
- Mala, porque WPF activa la ventana en caminos conocidos (#7561) y nada lo detectaría en producción.

### Ventanas activables que devuelven el foco a posteriori

- Buena, porque evita pelear con el framework.
- Mala, porque incumple REG-01 por diseño: la app destino pierde el foco, cancela el IME y cierra sus
  menús antes de recuperarlo.

### `FocusBroker` solo para texto

- Buena, porque resolvía la búsqueda.
- Mala, porque dejaba sin camino legal el Centro de control, «Probar ahora» y la bandeja, y no resolvía
  el derecho de primer plano cuando la búsqueda se abre por voz.

### Forzar el primer plano con `AttachThreadInput` o el truco de Alt

- Buena, porque suele funcionar en pruebas rápidas.
- Mala, porque es frágil, puede bloquear el hilo de la app destino y el Alt sintético abre menús.

## Criterios de reapertura

- Si S1 falla, o si S4 falla con el origen táctil, se reabre [ADR-0001](0001-framework-ui-wpf.md) y, con
  él, este ADR.
- Si S4 falla solo con un origen de voz, no se reabre: el modo teclado y voz con el atajo global pasa a
  ser el camino documentado para ese origen.

## Más información

- Plano: [§1.2 (D9 y D23)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§3.5](../architecture/blueprint.md#35-ventanas-no-activables),
  [§3.6](../architecture/blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano),
  [§4.4](../architecture/blueprint.md#44-cómo-se-hacen-cumplir-las-reglas).
- Catálogo: REG-01 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  BUS-002 en [§2.3](../requirements/catalog.md#23-bus--búsqueda); CCM-004 en
  [§2.18](../requirements/catalog.md#218-ccm--centro-de-control-marco); PRB-004 a PRB-007 en
  [§2.21](../requirements/catalog.md#221-prb--probar).
- Registro: [techVerification.json](../architecture/decision-record/techVerification.json) (enfoque
  «Foco y táctil») y [critique.json](../architecture/decision-record/critique.json).
- Evidencia:
  [estilos extendidos de ventana](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles),
  [WM_POINTERACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputmsg/wm-pointeractivate),
  [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow),
  [SetCapture](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcapture),
  [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest),
  [IInputPaneInterop](https://learn.microsoft.com/en-us/windows/win32/api/inputpaneinterop/nn-inputpaneinterop-iinputpaneinterop),
  [HwndKeyboardInputProvider.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndKeyboardInputProvider.cs),
  [HwndTarget.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndTarget.cs),
  [Popup.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Popup.cs),
  [dotnet/wpf #7561](https://github.com/dotnet/wpf/issues/7561).
- ADR relacionados: [ADR-0001](0001-framework-ui-wpf.md), [ADR-0006](0006-capa-de-punteros-propia.md),
  [ADR-0010](0010-ipc-minima.md).
