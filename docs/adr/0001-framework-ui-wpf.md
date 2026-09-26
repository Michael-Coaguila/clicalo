---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: nueve evaluaciones técnicas, tres verificaciones adversariales y la crítica del plano (docs/architecture/decision-record/)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0001 · WPF sobre .NET 10 LTS como framework de UI

## Contexto y planteamiento del problema

Clícalo es un panel flotante para Windows pensado primero para la accesibilidad. Lo usa, y al
principio lo mantiene, una persona que escribe con el teclado en pantalla y el dictado. Hay tres
cosas que no se pueden negociar:

1. **No activar nunca la ventana** ni quitar el foco a la app en primer plano (REG-01), con varias
   ventanas no activables (panel, pestaña con ventanas al costado, burbuja) que reciben toque real.
2. **Exponer UI Automation con fidelidad** a Acceso por voz, Narrador y el alto contraste (REG-06,
   ACC-001, ACC-009).
3. **Entrada de texto por TSF en todos los campos**: teclado táctil y Win+H en la búsqueda y en todo
   el Centro de control, que es una app de edición completa (REG-05, ACC-011, BUS-002, BUS-003).

Las recomendaciones de tecnología del paquete de diseño no son vinculantes por instrucción del
usuario ([LEEME-VINCULANTE](../design/handoff/LEEME-VINCULANTE.md)). ¿Qué framework de UI cumple
hoy, con tecnología madura, esas tres condiciones y el resto de requisitos durante 5–10 años?

## Factores de decisión

- **Compuerta (a), peso 5:** panel sin foco con táctil real y varias ventanas no activables (REG-01).
- **Compuerta (b), peso 5:** UIA, Acceso por voz, Narrador y alto contraste (REG-06).
- TSF en todos los campos de texto, porque es de lo que depende el mantenedor.
- Integración profunda con Win32: inyección según la distribución, primer plano, *hooks* (peso 4).
- Rendimiento: panel visible en menos de 1 s y toque → envío en menos de 50 ms (NFR-001, peso 4).
- Mantenibilidad y viabilidad a largo plazo (peso 4 cada una).
- Capacidad visual, distribución, seguridad y escalabilidad de equipo (peso 3 cada una).
- Accesibilidad del propio desarrollo para el mantenedor (peso 2) y portabilidad futura (peso 1).

## Opciones consideradas

- WPF sobre .NET 10 LTS con C# 14 y capa propia de ventanas y punteros sobre Win32
- Avalonia UI 12.x sobre .NET 10 (Native AOT)
- Qt 6.12 LTS con C++20 y QML
- WinUI 3 / Windows App SDK 2.5
- Nativo: Rust + Direct2D/DirectComposition + AccessKit
- Electron
- Tauri 2 + WebView2
- Qt 6 con PySide6
- Flutter Windows

## Resultado de la decisión

Opción elegida: **«WPF sobre .NET 10 LTS con C# 14 y capa propia de ventanas y punteros sobre
Win32»**, porque es la única candidata que hoy supera las dos compuertas y el requisito de TSF con
tecnología madura y probada:

- **No activación:** está verificada en el código fuente de WPF (`HwndKeyboardInputProvider.AcquireFocus`
  no llama a `SetFocus` si la ventana tiene `WS_EX_NOACTIVATE`) y en producción (OptiKey, un teclado de
  accesibilidad que usa ese patrón desde hace años).
- **UIA:** tiene el proveedor más antiguo y completo del escritorio: *peers* propios, `LiveSetting`,
  `RaiseNotificationEvent`.
- **Texto:** `TextBox` nativo con TSF.
- Sus fallos conocidos (pila táctil WISP, pila de punteros, robo de foco al cambiar de DPI,
  colocación en PerMonitorV2) están todos en la frontera entre la ventana y la entrada. Esa frontera
  se sustituye por una capa propia sobre Win32 (ADR-0005 y ADR-0006), que es exactamente el código
  que habría que escribir con Avalonia, Tauri o la opción nativa.

WPF queda confinado en `Clicalo.UI.Wpf`. Domain, Application, Presentation y la plataforma no
dependen del framework de UI (ADR-0002), así que migrar a Avalonia costaría rehacer las vistas y la
capa de ventanas, no el producto.

### Puntuación ponderada

Doce criterios con 41 puntos de peso. Las notas son las de cada evaluación, corregidas donde el
contraste entre evaluadores mostró optimismo (por ejemplo, Avalonia (a) 3 → 2,5 por el fallo de IME
#22273 y (b) 3 → 2,5 por no tener TSF; WPF (a) 3 → 3,5 por la verificación en el código fuente).

| Candidata | (a) 5 | (b) 5 | (c) 4 | (d) 4 | (e) 4 | (f) 4 | (g) 3 | (h) 3 | (i) 3 | (j) 3 | (k) 2 | (l) 1 | Total |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **WPF / .NET 10 LTS** | 3,5 | 4,5 | 4,5 | 3 | 4 | 2,5 | 3,5 | 4 | 4 | 3,5 | 4 | 1,5 | **3,67** |
| Avalonia 12 | 2,5 | 2,5 | 4 | 4 | 4,5 | 3,5 | 4 | 4 | 4 | 3,5 | 4 | 4 | 3,60 |
| Qt 6.12 C++/QML | 4 | 2,5 | 5 | 4 | 3 | 4 | 4 | 3 | 3 | 3 | 2,5 | 5 | 3,55 |
| WinUI 3 | 2 | 4 | 4 | 3 | 4 | 4 | 3 | 4 | 4 | 4 | 4 | 2 | 3,54 |
| Nativo (Rust) | 5 | 2,5 | 5 | 5 | 2 | 2,5 | 4 | 4 | 4 | 2 | 3 | 1 | 3,52 |
| Electron | 2 | 4 | 3 | 2 | 4 | 4 | 4 | 5 | 2 | 5 | 4 | 5 | 3,49 |
| Tauri 2 | 2,5 | 3,5 | 4 | 2 | 4 | 4 | 4 | 4 | 2,5 | 4 | 4,5 | 3,5 | 3,46 |
| PySide6 | 4 | 3 | 2 | 2 | 3 | 3 | 4 | 2 | 2 | 4 | 4 | 5 | 3,02 |
| Flutter | 2 | 2 | 3 | 3 | 4 | 3 | 3 | 3 | 3 | 3 | 4 | 4 | 2,93 |

Criterios: (a) panel sin foco con táctil real; (b) UIA, voz, Narrador y alto contraste; (c)
integración con Win32; (d) rendimiento y consumo; (e) mantenibilidad; (f) viabilidad a largo plazo;
(g) capacidad visual; (h) distribución; (i) seguridad; (j) escalabilidad de equipo; (k) accesibilidad
del desarrollo para el mantenedor; (l) portabilidad a macOS o Linux.

Cinco candidatas quedan entre 3,46 y 3,67: **la suma ponderada no discrimina bien**. Decide la
solidez con la que cada una supera las compuertas y el coste real de cada riesgo bloqueante.

### Compuertas

| Candidata | Compuerta (a): no activación | Compuerta (b): UIA y texto | Resultado |
|---|---|---|---|
| WPF | Verificada en el código fuente y en producción; #7561 (DPI) tiene mitigación en la capa propia | Proveedor UIA completo; TSF nativo | Supera ambas; S1, S3 y S4 deben confirmarlo |
| Avalonia 12 | Crear una ventana con `ShowActivated=false` rompe el IME de la app en primer plano (#22273) | Sin TSF: el teclado táctil (#10136) y Win+H (#16847) fallan en todos los campos | Solo con condiciones; es el plan B |
| Qt 6.12 C++/QML | La mejor no activación de serie | Deuda de accesibilidad en Qt Quick (QTBUG-139296, QTBUG-92560, QTBUG-113412) | No supera (b) sin una capa `QAccessible` propia |
| WinUI 3 | `InputPointerSource.ActivationBehavior = NoActivate` sigue siendo experimental | Regresiones de UIA y alto contraste | No supera (a) |
| Nativo (Rust) | La mejor | Hay que construir UIA (AccessKit) y la edición de texto con TSF | Supera (a); (b) exige un *toolkit* propio |
| Electron | Con `focusable:false`, Chromium devuelve `MA_NOACTIVATEANDEAT` y se traga los clics | UIA de Chromium correcto | No supera (a) sin parchear su WndProc |
| Tauri 2 | Sin evidencia con toque: el HWND de Chromium vive en otro proceso (#4746) | uiAccess no funciona con WebView2 (#4884) | No verificada |
| PySide6 | Hereda la de Qt | Hereda la deuda de Qt Quick | Falla el arranque (<1 s) y el *hook* LL bajo el GIL |
| Flutter | Débil | UIA desactivado por defecto y sin TSF (#182876) | No supera (b) |

### Condición: spikes bloqueantes S1, S3 y S4

La decisión se toma **condicionada** a tres spikes bloqueantes del hito M1 ([§15.1 del
plano](../architecture/blueprint.md#151-spikes-con-criterio-de-éxito)). Cada spike se escribe como
prueba en `tests/Clicalo.Windowing.IntegrationTests` o `tests/Clicalo.Platform.IntegrationTests` y deja
su informe en `docs/testing/spikes/`.

| Spike | Criterio de éxito | Si falla |
|---|---|---|
| **S1 · No activación** (1 semana) | Ningún `WM_ACTIVATE`, `WM_KILLFOCUS` ni cambio de `GetForegroundWindow` sin acción explícita, con dedo, lápiz y mouse, en Word, Chrome, VS Code, Bloc de notas, una app de la Tienda y otra elevada; IME japonés o chino sin cancelar; cambio de monitor con distinto DPI; 20 de 20 por superficie; la activación forzada se detecta y se revierte en 20 de 20 | Se reabre este ADR |
| **S3 · UIA sobre ventana no activable** (3–4 días) | «mostrar números», «clic 4», «clic Negrita» y «en todas partes» con Acceso por voz (Windows 11) y Reconocimiento de voz de Windows (Windows 10), y Narrador táctil y con teclado, sin activar la ventana; invocar por UIA no cambia el primer plano | Se diseña y valida `InteractionMode.Voice` (20 de 20); si tampoco funciona, se reabre este ADR |
| **S4 · Entrada de texto y primer plano por origen** (4 días) | 20 de 20 ciclos por origen (toque, Acceso por voz, Narrador, Reconocimiento de voz de Windows y atajo global); TSF con dictado en todos los campos; el atajo interno de derechos no llega a ninguna app | Si falla un origen de voz: modo teclado y voz con el atajo global como camino documentado. Si falla el toque: se reabre este ADR |

**Punto de decisión de M1:** si S1, S3 o S4 fallan en WPF, este ADR se reabre **antes** de escribir
funcionalidad.

### Consecuencias

- Buena, porque los tres requisitos innegociables se apoyan en la pila de UIA y de texto más madura
  de Windows.
- Buena, porque hay un solo lenguaje, un solo runtime, un solo modelo de UI y una sola cadena de
  suministro (NuGet).
- Buena, porque la capa de UI es sustituible: Avalonia reutiliza tal cual el dominio, la aplicación,
  la plataforma y los ViewModels.
- Mala, porque WPF está en modo mantenimiento: la discusión sobre su hoja de ruta para .NET 11
  (#11188) no tuvo respuesta oficial. Se paga un «impuesto WPF» fijo: una capa de plataforma propia de
  unas 3–5 mil líneas y un 10–20 % del esfuerzo anual estimado en mantenerla.
- Mala, porque el arranque en frío es su punto más débil medido (2–3 s en una app WPF vacía tras
  encender Windows, dotnet/runtime #78379) y WPF no admite AOT ni recorte. NFR-001 sigue siendo puerta
  sin excepciones: S5 mide y, solo si demuestra que no se puede cumplir, se abre la propuesta P1 al
  usuario.
- Mala, porque en las superficies quedan prohibidos `Popup`, `ContextMenu`, `ToolTip` interactivo y
  `ComboBox` (ADR-0005), y el desplazamiento táctil del Centro de control es propio (ADR-0006).
- Neutral, porque Sentinel y Launcher, que deben arrancar en milisegundos, no usan WPF: son Native AOT
  (ADR-0004 y ADR-0009).

### Confirmación

- Spikes S1, S3 y S4 superados y documentados en M1.
- ArchUnit: Domain, Application y Presentation no referencian ningún ensamblado de WPF, para que la
  vía a Avalonia siga abierta.
- Carpeta `Upstream/` de `Clicalo.Windowing.IntegrationTests`: una prueba por cada solución provisional
  de WPF (#3147, #2054, #9752, #7561, #4127, #10459, #10422, #7857, #11847), con
  `[Trait("Upstream", …)]`, para saber cuándo se puede retirar.
- Revisión trimestral del estado de dotnet/wpf ([§15.2 del plano](../architecture/blueprint.md#152-riesgos-abiertos)).

## Pros y contras de las opciones

### WPF sobre .NET 10 LTS

- Buena, porque la no activación está verificada en el código fuente y hay precedente en producción.
- Buena, porque UIA (peers, `LiveSetting`, `RaiseNotificationEvent`) y TSF son maduros.
- Buena, porque C# tiene la mejor refactorización y una gran bolsa de talento.
- Mala, porque el framework casi no evoluciona y su soporte más allá de .NET 12 no está confirmado.
- Mala, porque el arranque en frío y la memoria residente hay que medirlos (S5).

### Avalonia 12

- Buena, porque es la mejor en mantenibilidad, Native AOT y portabilidad, y su hoja de ruta está viva.
- Buena, porque comparte C# y .NET: es el plan B de menor coste.
- Mala, porque hoy no tiene TSF: el teclado táctil y Win+H fallan en todos los campos.
- Mala, porque crear ventanas con `ShowActivated=false` rompe el IME de la app en primer plano.
- Mala, porque su proveedor UIA acababa de recibir correcciones básicas (SelectionPattern en 12.1.3).

### Qt 6.12 LTS con C++20 y QML

- Buena, porque trae de serie la mejor no activación entre los frameworks.
- Mala, porque Qt Quick tiene muchos fallos de accesibilidad abiertos, varios P1, y no tiene
  `LiveSetting`.
- Mala, porque C++ no es seguro en memoria en un proceso con *hooks*, inyección y posible elevación.
- Mala, porque programar C++ y CMake por voz es muy costoso, y 6.12 es la última versión con
  Windows 10.

### WinUI 3 / Windows App SDK 2.5

- Buena, porque es el framework moderno de Microsoft y tiene buen UIA de base.
- Mala, porque XAML llama a `SetFocus` al dar foco y la API para evitarlo sigue siendo experimental.
- Mala, porque la alternativa depende de HWND internos que pueden cambiar en cada versión mensual.
- Mala, porque no hay ventanas transparentes sin marco de primera clase (#1247) y el teclado táctil
  falla (#10349).

### Nativo: Rust + Direct2D/DirectComposition + AccessKit

- Buena, porque da el máximo control y rendimiento en la frontera ventana/entrada.
- Mala, porque obliga a construir y mantener un *toolkit* completo, incluida la edición de texto con
  TSF: de 6 a 12 meses para llegar a la paridad funcional.
- Mala, porque una sola persona sería el único punto de fallo (*bus factor* 1).

### Electron

- Buena, porque tiene la mayor bolsa de colaboradores y una distribución muy resuelta.
- Mala, porque con `focusable:false` Chromium descarta los clics (también los de Acceso por voz) y solo
  se arregla parcheando su WndProc, algo que hay que revalidar cada 8 semanas.
- Mala, porque cada ventana es un *renderer*: cientos de MB residentes.
- Mala, porque relanzar elevado significa ejecutar Chromium y Node como administrador.

### Tauri 2 + WebView2

- Buena, porque daría la fidelidad 1:1 con el prototipo HTML.
- Mala, porque no hay evidencia de que el toque funcione sin activar cuando atraviesa el HWND hijo de
  otro proceso, y arrastrar la ventana con el dedo no funciona (#4746).
- Mala, porque ocuparía 150–300 MB residentes y uiAccess no funciona con WebView2 (#4884).
- Mala, porque exige dos lenguajes y dos cadenas de suministro.

### Qt 6 con PySide6

- Buena, porque continuaría el lenguaje de Macro Quick Access.
- Mala, porque el arranque en frío es de 1–2 s y el *hook* de teclado LL queda bajo el GIL y el GC,
  con riesgo de que Windows lo retire en silencio.
- Mala, porque el empaquetado es frágil, pesa más de 100 MB y da falsos positivos de antivirus.

### Flutter Windows

- Buena, porque es muy mantenible y tiene buenas herramientas.
- Mala, porque el proveedor UIA nativo está desactivado por defecto y los campos de texto no usan
  TSF (#182876): fallan el dictado, Acceso por voz y el teclado táctil.
- Mala, porque la API multiventana es experimental.

## Criterios de reapertura

Se escribe un ADR nuevo que sustituya a este si ocurre cualquiera de estas cosas:

1. S1, S3 o S4 fallan en WPF en M1, en los términos de la tabla anterior.
2. WPF no aparece en una versión candidata (RC) de .NET 12 o de .NET 14.
3. Un fallo crítico de seguridad o de accesibilidad de WPF queda más de 6 meses sin corregir.
4. Avalonia publica TSF y TextPattern, cierra el fallo de IME #22273 y supera S1, S3 y S4.

## Más información

- Plano: [§1.2 (D1)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§2](../architecture/blueprint.md#2-stack-tecnológico),
  [§15](../architecture/blueprint.md#15-riesgos-abiertos-y-spikes).
- Registro de la decisión: [techDecision.json](../architecture/decision-record/techDecision.json),
  [techEvaluations.json](../architecture/decision-record/techEvaluations.json) y
  [techVerification.json](../architecture/decision-record/techVerification.json). Las tres
  verificaciones adversariales (foco y táctil, accesibilidad, sostenibilidad) no refutaron WPF; las
  tres dieron severidad «mayor, no bloqueante» y sus mitigaciones están incorporadas al plano.
- ADR relacionados: [ADR-0002](0002-monolito-modular-hexagonal.md),
  [ADR-0005](0005-superficies-no-activables-y-foreground-orchestrator.md),
  [ADR-0006](0006-capa-de-punteros-propia.md).

Evidencia principal (de las evaluaciones y verificaciones):

- WPF, no activación: [HwndKeyboardInputProvider.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndKeyboardInputProvider.cs),
  [OptiKey WindowManipulationService.cs](https://github.com/OptiKey/OptiKey/blob/main/src/JuliusSweetland.OptiKey.Core/Services/WindowManipulationService.cs),
  [dotnet/wpf #7561](https://github.com/dotnet/wpf/issues/7561).
- WPF, UIA: [AutomationProperties.LiveSetting](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationproperties.livesetting),
  [AutomationPeer.RaiseNotificationEvent](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.raisenotificationevent).
- WPF, viabilidad y arranque: [política de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core),
  [dotnet/wpf discusión #11188](https://github.com/dotnet/wpf/discussions/11188),
  [novedades de WPF en .NET 11](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net110),
  [dotnet/runtime #78379](https://github.com/dotnet/runtime/issues/78379).
- Avalonia: [#22273](https://github.com/AvaloniaUI/Avalonia/issues/22273),
  [#10136](https://github.com/AvaloniaUI/Avalonia/issues/10136),
  [#16847](https://github.com/AvaloniaUI/Avalonia/issues/16847).
- WinUI 3: [InputPointerSource.ActivationBehavior](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputpointersource.activationbehavior),
  [microsoft-ui-xaml #1247](https://github.com/microsoft/microsoft-ui-xaml/issues/1247),
  [#10349](https://github.com/microsoft/microsoft-ui-xaml/issues/10349).
- Qt: [QTBUG-139296](https://qt-project.atlassian.net/browse/QTBUG-139296),
  [QTBUG-92560](https://qt-project.atlassian.net/browse/QTBUG-92560),
  [QTBUG-113412](https://qt-project.atlassian.net/browse/QTBUG-113412).
- Electron: [hwnd_message_handler.cc](https://raw.githubusercontent.com/chromium/chromium/main/ui/views/win/hwnd_message_handler.cc).
- Tauri: [tauri #4746](https://github.com/tauri-apps/tauri/issues/4746),
  [WebView2Feedback #4884](https://github.com/MicrosoftEdge/WebView2Feedback/issues/4884).
- Flutter: [flutter #182876](https://github.com/flutter/flutter/issues/182876).
- Acceso por voz: [lista de órdenes](https://support.microsoft.com/en-us/accessibility/windows/voice-access/voice-access-command-list).
