# Spikes de M1 · SpikeLab, el laboratorio de S1, S3 y S4

Los spikes bloqueantes de M1 tienen dos mitades: las pruebas automáticas, que corren en la CI, y los recorridos a
mano en el equipo táctil del mantenedor, con el dedo y la voz. **SpikeLab** (`tools/SpikeLab`) es la app de esa
segunda mitad: compone las piezas **reales** del producto (`NonActivatingWindow`, `SurfaceRegistry`,
`ActivationGuard`, `PointerInputSource` con `GestureRecognizer`, `ShortcutTile` y su *peer*, `LiveAnnouncer`,
`ForegroundOrchestrator` con `ForegroundControl`, `ForegroundMonitor`, `InternalRightsHotkey` y `TouchKeyboard`, y la
bandeja), te guía paso a paso por el guion y guarda un informe que el mantenedor lee después. No es producto: nunca
se publica y sus textos no están localizados (M1 no añade textos de producto).

Guiones: [S1](S1.md) (no activación), [S3](S3.md) (UI Automation) y [S4](S4.md) (primer plano por origen). Reparto de
rutas: [M1-ownership.md](M1-ownership.md).

## Cómo se abre

- **VS Code:** paleta de tareas (*Terminal › Ejecutar tarea…*) → **«SpikeLab: abrir»**. Se abre la ventana de control
  y en ella tocas el spike: **S1**, **S3** o **S4**.
- **Terminal** (se puede dictar):

  ```powershell
  dotnet run --project tools\SpikeLab -c Release -- --spike S1
  ```

  Cambia `S1` por `S3` o `S4`. Sin `--spike`, la ventana de control te pregunta.
- **Opciones:**

  | Opción | Qué hace |
  |---|---|
  | `--spike S1` (`-s S1`, `--spike=S1`) | Empieza directamente ese spike |
  | `--reports <carpeta>` | Guarda los informes en otra carpeta |
  | `--check` | Compone todas las piezas **sin mostrar nada** (ni ventanas, ni bandeja, ni atajos), escribe `check-<fecha>.md` en la carpeta de informes con el estado de cada pieza y termina: código 0 si todas están listas, 2 si falta alguna |

## Qué aparece

| Ventana | ¿Se activa? | Para qué |
|---|---|---|
| **Ventana de control** (centrada) | **Sí** (ventana normal) | Preparar: elegir el spike, «Mostrar superficies», «Abrir sonda», «Números de voz», «Enviar teclas», «Abrir el CC desde la ventana de control», «Paso anterior», «Escribir el informe ahora», **«Abrir carpeta de informes»** y **«Abrir resumen»**. Arriba dice qué piezas del producto no están listas (el detalle, al final). **No la toques durante un ciclo**: te quitaría el primer plano |
| **Tira-guía** (abajo a la izquierda) | **No** (`NonActivatingWindow`) | El paso actual, los botones y las mediciones automáticas, compacta, plegable y arrastrable por su asa. Es la franja de estado de los guiones |
| **Panel** (abajo a la derecha) | No | 14 fichas reales (`ShortcutTile`) y el asa ⠿ (48 px de ancho) para arrastrarlo |
| **Pestaña con su lateral** (borde derecho, arriba) | No | Asa que abre y cierra la ventana lateral, y tres fichas |
| **Burbuja** (encima del panel) | No | Una ficha de 56 px |
| **Búsqueda** (arriba, a la izquierda de la Pestaña; S4) | Solo con la concesión `TextInput` | «Campo de búsqueda», «Dictar», «Resultado Negrita» y «Cerrar búsqueda» |
| **Centro de control de laboratorio** (centrado; S4) | Sí, con la concesión `ControlCenter` | Campos «Nombre», «Texto» y «Web», cada uno con «Dictar», y «Cerrar» |
| **Icono de bandeja** | — | Menú con la concesión `TrayMenu`: «Abrir el Centro de control», «Soltar todas las teclas», «Salir de SpikeLab» |
| **InputProbe** («la sonda», S4) | Sí | La app objetivo de los ciclos de voz de S4: cuenta lo que le llega |

**Dónde queda cada cosa.** SpikeLab coloca cada superficie con el tamaño que mide de verdad, antes de mostrarla, y la
mantiene dentro del área de trabajo (la pantalla sin la barra de tareas) cuando aparece y cuando se suelta tras
arrastrarla; la ventana de control y el Centro de control de laboratorio nunca son más grandes que el área de trabajo.
El cuarto de arriba a la izquierda queda libre: es la **zona libre** donde los guiones piden poner la app objetivo
(arrastra su barra de título a esa esquina, o di con Acceso por voz «pulsa Windows flecha izquierda» y «pulsa Windows
flecha arriba»), así ninguna superficie tapa el cursor, los menús ni la cinta que hay que vigilar.

Las fichas del panel, por orden (es su número de voz): 1 Negrita (Ctrl+B), 2 Cursiva (Ctrl+I), 3 Subrayado (Ctrl+U),
4 Copiar (Ctrl+C), 5 Pegar (Ctrl+V), 6 Deshacer (Ctrl+Z), 7 Guardar (Ctrl+S), 8 Localizar (Ctrl+F), 9 Mayús (Toggle
de tres estados), 10 Mantener Ctrl (Toggle), 11 Perfil (ExpandCollapse), 12 Buscar, 13 Centro de control y 14 Soltar
todo. **Ningún nombre se repite en ninguna ventana de SpikeLab** (superficies, ventana de control, Centro de control
de laboratorio y menú de la bandeja), salvo «Dictar», que la regla UIA010 pide junto a cada campo de texto; así «clic
Negrita» nunca necesita desambiguar y ninguna orden de voz elige por error la ventana de control, que se activa. Lo
comprueba `UiaNameUniquenessTests`, que además exige que cada «clic X» de los guiones llegue a un solo elemento y que
ningún otro nombre contenga X. «Buscar» y «Centro de control» solo piden concesión en S4; en S1 y S3 no cambian el
primer plano.

## La tira-guía

Empieza **abajo a la izquierda** y es **compacta**: arriba, su asa ⠿ (a la izquierda, para arrastrarla con el dedo),
el spike, el paso y la fila de la tabla de resultados, el título y el estado **con palabras** («Todo bien.», «Algo
cambió: …», «Paso superado: toca «Siguiente».»), con el borde **verde** o **rojo**; debajo, **qué hacer** en tres
líneas, las repeticiones («Repeticiones: 7 de 20 · correctas: 7 · fallos: 0 · comprobación final: pendiente»), las
mediciones automáticas, la última nota (la región *live* que lee Narrador) y los botones. Nunca sale del área de
trabajo: si crece o se pliega, conserva su borde de abajo (o el de arriba, si la has llevado a la mitad de arriba).

**Mediciones automáticas:**

- **Primer plano:** el proceso que está delante (nunca el título de la ventana), o «SpikeLab · Panel#0» si una
  superficie se quedó con él.
- **Cambios de primer plano** y **activaciones de superficies (WM_ACTIVATE)** desde que empezó el paso, y
  **reg01.violations** desde que se abrió SpikeLab.
- **Última orden:** la ficha, el patrón o el dispositivo (dedo, lápiz o mouse) y su **latencia**,
  desde que se levanta el dedo (o llega la orden de UI Automation) hasta que la acción está hecha.
- **Concesión** y **devolución:** tipo, origen, concedida o denegada (con el motivo), paso de la escalera (1 o 2),
  tiempos y `Restaurado`, `Restaurado al reintentar`, `Parpadeo en la barra de tareas` o `Falló`.
- **La sonda recibió:** F24, caracteres y menú (solo con la sonda abierta).
- Si «Enviar teclas» está activado y cuántas piezas del producto no arrancaron.

**Botones** (fichas reales de al menos 44 px con nombre para la voz: di «clic Funcionó»):

| Botón | En un paso automático | En un paso manual |
|---|---|---|
| **Funcionó** | Confirma la comprobación final que SpikeLab no puede ver (la palabra dictada llegó a la app) | Cuenta una repetición correcta, con lo medido desde la anterior |
| **Falló** | Marca como fallida la última repetición (viste algo que SpikeLab no mide: un menú que se cerró, el cursor que se movió) | Cuenta una repetición fallida |
| **Repetir** | Empieza el paso de cero. El intento anterior **no se borra**: queda en el informe como «intento descartado» | Igual |
| **Siguiente** | Pasa al siguiente paso. Un paso opcional sin repeticiones queda «no aplicable»; uno obligatorio, «incompleto» | Igual |
| **Acción del paso** | Solo en los pasos que la necesitan: «Forzar activación del panel» (S1 fila 31), «Aviso cortés» y «Aviso urgente» (S3 filas 9a y 9b), «Activar números de Clícalo» y «Quitar números de Clícalo» (S3 fila 6) | Igual |
| **Soltar todo ya** | Suelta Mayús, Ctrl y Alt izquierdas si están pulsadas y quita los enclavamientos | Igual |
| **Ver instrucción completa** | Muestra toda la instrucción en lugar de sus tres primeras líneas; **«Acortar la instrucción»** vuelve | Igual |
| **Plegar la tira** | Deja solo el paso, el estado, el aviso y los botones, para que no tape una lista o un menú (S1 filas 28 y 29); **«Desplegar la tira»** vuelve | Igual |
| **Mover la tira** | La lleva a la otra mitad de la pantalla, arriba o abajo (con el dedo se arrastra por su asa) | Igual |

Plegar, mover y arrastrar la tira no cuentan como repeticiones ni cambian «Última orden».

## Cómo se cuentan las repeticiones

Cada paso es una fila de la tabla de resultados de su guion. SpikeLab cuenta solo lo que corresponde al paso; lo que
no cuenta lo dice en la nota («Este paso se hace con el lápiz; el toque con el dedo no cuenta»).

| El paso se cuenta con… | Cada repetición es… | Comprobaciones automáticas |
|---|---|---|
| Toques (S1) | Un toque aceptado sobre la superficie de la fila, con el dispositivo de la fila | Ninguna superficie recibió activación; el primer plano nunca pasó a SpikeLab; `reg01.violations` no subió; la app objetivo sigue delante |
| Arrastres del asa (S1 fila 30) | Un arrastre del panel por su asa | Las mismas |
| Órdenes de UI Automation (S3, S1 fila 32) | Un Invoke, Toggle, Expand o Collapse sobre la ficha de la fila | Las mismas; en S3 fila 6, además, que el nombre empiece por su número de voz |
| Concesiones (S4) | Un ciclo completo: pedir la concesión del tipo y origen de la fila y devolver el primer plano | Concedida; devolución `Restaurado` o `Restaurado al reintentar` y delante la ventana de antes; `reg01.violations` no subió; texto en todos los campos; en los orígenes de voz y el atajo global, la sonda estaba delante y no recibió F24, caracteres ni menú |
| Activación forzada (S1 fila 31) | Un `SetForegroundWindow` sobre el panel sin concesión | `reg01.violations` subió exactamente en 1; el primer plano anterior volvió en ≤ 200 ms (`Timings.Windowing.ViolationRestoreBudget`); el panel conserva `WS_EX_NOACTIVATE`; la app objetivo vuelve a estar delante |
| «Funcionó» y «Falló» (S3 filas 9a, 9b y 10) | Cada marca | Las del paso, sobre lo medido desde la marca anterior |

Una repetición recoge todo lo que pasó **desde la anterior** (o desde que empezó el paso) hasta un momento después
del toque o la orden (`InputProbeSession.SettleTime`, 150 ms): una activación que Windows provoca al *bajar* el dedo
también cuenta. En S4 la ventana de cada ciclo empieza al pedir la concesión. **Una repetición que rompe una
comprobación falla aunque digas «Funcionó».** La app objetivo es la que estaba delante al empezar la primera
repetición del paso.

**Veredicto de un paso:** superado con todas las repeticiones correctas (20 de 20, o 5 de 5 en S3 fila 13) y, en los
pasos automáticos, la comprobación final confirmada; fallido si alguna repetición falló; incompleto si lo dejaste
antes; no aplicable si era opcional (lápiz, mouse, IME chino, segundo monitor, máquina virtual de Windows 10) y lo
dejaste sin repeticiones.

**Veredicto del spike:** fallido si falla un paso decisivo; superado si todos están superados o no aplican (un paso no
decisivo fallido se informa aparte: S1 fila 32 pasa a S3; S3 fila 8 puede ser una limitación de Windows); si no,
incompleto. La decisión final la toma el mantenedor con la regla de su guion.

## Reglas de seguridad del laboratorio

SpikeLab corre en el equipo de trabajo real, con dictado (Wispr Flow, Typeless) y Acceso por voz:

- **Las fichas no escriben** mientras «Enviar teclas» esté desactivado, que es lo normal en los guiones (S1 no inyecta
  teclas). Activado, cada ficha envía su atajo a la app de delante con **un solo `SendInput`**:
  - el lote está **equilibrado** (cada pulsación con su liberación en el mismo lote);
  - solo usa **modificadores izquierdos**: **nunca AltGr ni Ctrl derecho**;
  - comprueba la ventana de delante **justo antes** del envío y no envía nada si cambió;
  - **no envía nada si hay una modificadora pulsada** (a mano o por otro programa), para no combinarse con ella.
- **«Mayús» y «Mantener Ctrl» se enclavan**: se aplican dentro del lote del siguiente atajo, así nunca queda una tecla
  pulsada entre lotes (es el equivalente sin gesto de mantener, §8.6 del plano).
- **El atajo interno (Ctrl+Alt+Mayús+F24) y Win+H** solo se inyectan si delante está una ventana de SpikeLab o la
  sonda, comprobado justo antes con el inyector protegido de TestKit.Windows. Por eso los ciclos de voz de S4 usan la
  sonda como app objetivo.
- **Soltar todo** está en el panel («Soltar todo»), en la tira-guía («Soltar todo ya», que no quita el foco: di «clic
  Soltar todo ya»), en la ventana de control («Soltar todo desde la ventana de control», que se activa) y en la
  bandeja («Soltar todas las teclas»). Solo envía liberaciones de Mayús, Ctrl y Alt izquierdas que estén pulsadas
  (nunca Windows, que abriría Inicio).
- **Ninguna instrucción de los guiones pide decir una frase que no sea una orden** («clic …», «mostrar números»,
  «mostrar números en todas partes», «ocultar números», «pulsa …»), así Acceso por voz nunca escribe una orden en la
  app objetivo; lo comprueba `UiaNameUniquenessTests`. Lo único que se dicta en la app es la comprobación final, en un
  documento de prueba.

## Piezas y cómo llega la entrada

SpikeLab construye cada pieza real directamente (`ForegroundOrchestrator` con sus puertos: `ForegroundControl`,
`ForegroundMonitor`, `SurfaceRegistry`, `InternalRightsHotkey` y el inyector protegido del laboratorio). Si una pieza
no arranca (por ejemplo, otro programa ya registró el atajo de laboratorio o el Explorador no tiene bandeja), la
ventana de control la muestra en rojo con el motivo, el informe la lista y el laboratorio sigue con el resto; lo que
espera a una pieza que falló aparece como **«Pendiente»**. Para ver el estado sin abrir nada: `--check`.

Como el producto, SpikeLab apaga la pila táctil de WPF y encamina el mouse por `WM_POINTER`
(`PointerSetup.EnableMouseInPointer`) antes de la primera ventana: dedo, lápiz y mouse llegan a cada superficie por
`PointerInputSource` y `GestureHost`, y el informe anota el dispositivo de cada toque. Un paso de un dispositivo solo
cuenta ese dispositivo.

## Informes

Cada ejecución guarda dos archivos en **`%LOCALAPPDATA%\Clicalo.SpikeLab\reports`** (o en `--reports`), con el
spike y la hora local de inicio: `S1-2026-09-26-101530.json` y `S1-2026-09-26-101530.md`. Se reescriben (de forma
atómica) tras cada repetición, cada 10 segundos y al cerrar, así que un cierre inesperado pierde como mucho la última
repetición. La ruta exacta aparece en la ventana de control, y sus botones **«Abrir carpeta de informes»** (el
Explorador, con el informe seleccionado: la carpeta está oculta en `%LOCALAPPDATA%`) y **«Abrir resumen»** (el `.md`,
con la app que Windows tenga para él) los abren sin escribir la ruta.

**Privacidad:** solo nombres de proceso, contadores, longitudes y tiempos. **Nunca** títulos de ventana ni el texto que
escribas o dictes (de la búsqueda y del Centro de control solo se guarda si el campo tenía texto).

### Resumen Markdown (`.md`)

Pensado para leerlo con Narrador: la primera frase es el resultado («**Resultado: fallido.** 1 de 32 pasos
superados…»), luego la máquina (Windows con su revisión, pantalla táctil, lápiz y mouse, y cada monitor con su
resolución, su escala y su área de trabajo: lo que pide la cabecera de los resultados manuales de S1) y los ajustes,
las piezas que no estaban listas, una tabla con una fila por paso
(resultado, correctas, fallos y latencia p95) y, por cada paso con fallos, la lista de repeticiones fallidas con su
motivo y los intentos descartados con «Repetir».

### Formato JSON (`clicalo.spikelab.report/1`)

Enumeraciones en camelCase, fechas ISO 8601 con zona, `null` cuando un valor no aplica.

| Campo | Contenido |
|---|---|
| `format` | `"clicalo.spikelab.report/1"` |
| `spike`, `title`, `document` | `"S1"`, su título y su guion (`docs/testing/spikes/S1.md`) |
| `startedAt`, `updatedAt` | Inicio de la ejecución y última escritura |
| `machine` | `windows` (compilación con su revisión y nombre de versión, «10.0.26200.6584 (25H2)»), `windowsBuild`, `windowsDisplayVersion`, `architecture`, `labVersion` (versión de SpikeLab, con el commit si la compilación lo conoce), `input` (`touch`, `integratedTouch`, `externalTouch`, `maxTouches`, `pen`, `digitizerReady` y `mouse`, según `GetSystemMetrics`) y `monitors[]` (`primary`, `left`, `top`, `width`, `height`, `workWidth`, `workHeight`, `dpi` y `scalePercent`, en píxeles físicos; los monitores de ese momento) |
| `settings` | `sendsKeys` («Enviar teclas»), `voiceNumbers` («Números de voz») |
| `verdict` | `passed`, `failed` o `incomplete` |
| `summary` | `steps` y el número de pasos por veredicto: `pending`, `inProgress`, `passed`, `failed`, `incomplete`, `notApplicable` |
| `currentStep` | Fila del paso actual, o `null` al terminar |
| `components[]` | `name`, `state` (`ready`, `pending`, `failed`) y `detail` de cada pieza |
| `steps[]` | Una entrada por fila (abajo) |
| `events[]` | La línea de tiempo: `at`, `kind` (`foreground`, `activation`, `violation`, `command`, `lease`, `probe`, `injection`, `refused`, `notice`, `dpi`, `forced`, `ignored`, `release`, `search`, `keyboard`, `session`, `error`) y `detail`. Como máximo 5000; `droppedEvents` cuenta los descartados |

Cada `steps[]`:

| Campo | Contenido |
|---|---|
| `id`, `title` | Fila de la tabla de resultados y título |
| `required` | Repeticiones necesarias (20; 5 en S3 fila 13) |
| `trigger` | `manual`, `surfaceTap`, `handleDrag`, `uiaCommand`, `leaseCycle` o `forcedActivation` |
| `surface`, `pointer`, `tile`, `pattern`, `lease`, `origin` | Qué cuenta en esa fila (`null` si cualquiera) |
| `optional`, `decisive` | Si puede quedar «no aplicable» y si decide el spike |
| `checks[]` | Comprobaciones automáticas: `noSurfaceActivation`, `noOwnForeground`, `noViolation`, `targetStillInFront`, `violationCountedOnce`, `restoredWithinBudget`, `noActivateStyleKept`, `leaseGranted`, `foregroundReturned`, `textReachedField`, `probeSilent`, `probeWasTarget`, `voiceNumberInName` |
| `verdict`, `passed`, `failed`, `confirmed`, `startedAt` | Veredicto, repeticiones correctas y fallidas, comprobación final y cuándo empezó |
| `latencyMs` | `count`, `p50`, `p95` y `max` de las latencias de sus repeticiones |
| `repetitions[]` | `index`, `at`, `source` (`automatic` o `user`), `passed`, `failedByUser`, `problems[]` (frases) y `evidence` |
| `discarded[]` | Intentos descartados con «Repetir»: `at`, `confirmed` y sus `repetitions[]` |

Cada `evidence`:

| Campo | Contenido |
|---|---|
| `trigger` | `kind`, `surface` («Panel#0»), `group`, `tile`, `pattern`, `pointer` (`finger`, `pen`, `mouse`), `channel` (`pointer`, `mouse-promoted`, `uia`, `hotkey`, `tray`, `lab`) y `voiceNumber`; `null` en las marcas manuales |
| `foregroundProcess`, `targetProcess`, `targetInFront` | El proceso de delante al cerrar la repetición, la app objetivo del paso y si seguía delante |
| `latencyMs` | Latencia de esa repetición |
| `counters` | Lo que pasó durante la repetición: `foregroundChanges`, `ownForegroundChanges`, `surfaceActivations`, `violations`, `probeF24`, `probeChars`, `probeMenus`, `probeModifierKeys` (solo informativo), `rightsChords` (atajos internos inyectados) y `refusedInjections` |
| `lease` | `kind`, `origin`, `granted`, `denial`, `unavailable` (por qué no pudo pedirse), `ladderStep`, `acquireMs`, `restore`, `restoreMs`, `previousProcess`, `previousWasProbe` y `foregroundReturned`; `null` si no hubo concesión |
| `restoredWithinMs`, `noActivateStyleKept` | Solo en la activación forzada |
| `fields` | `count` y `withText`: cuántos campos tenía el ciclo y cuántos recibieron texto |

## Pruebas del laboratorio

`tools/SpikeLab/Tests` (`SpikeLab.Tests`) prueba sin escritorio el motor del guion (pasos, 20 de 20, «Falló»,
«Repetir», «Siguiente», veredictos), las comprobaciones automáticas, la ventana de cada repetición, los informes (JSON,
Markdown y escritura atómica), el inyector de laboratorio y sus reglas de seguridad (con un `SendInput` falso: no
inyecta nada), la tira-guía y las superficies creadas en proceso sin mostrarse, dónde empieza cada superficie
(`LabLayoutTests`: dentro del área de trabajo y fuera de la zona libre, también en la pantalla de 2400 × 1600 al
175 %), que ningún nombre UIA se repite entre ventanas y que cada orden de voz de los guiones es inequívoca
(`UiaNameUniquenessTests`), y que el «Recorrido» y la tabla de resultados de S1.md, S3.md y S4.md dicen exactamente lo
mismo que la tira-guía (`SpikeScriptDocumentTests`). El «Recorrido» de cada guion **se genera** desde
`SpikeScripts.cs`: tras cambiar un texto del laboratorio, ejecuta las pruebas una vez con
`CLICALO_UPDATE_SPIKE_ROUTES=1` y la prueba lo reescribe entre sus marcadores. Ninguna prueba muestra ventanas ni
inyecta entrada. Para ejecutarlas sueltas:

```powershell
dotnet test --project tools\SpikeLab\Tests\SpikeLab.Tests.csproj
```
