# M1 · Cierre de los spikes de riesgo

**Estado: M1 cerrado el 2026-09-26 por decisión del usuario («continuar»), sin rebajar ningún criterio.** Este
documento registra la evidencia real que había al cerrar, la decisión, dónde se resuelve cada spike que queda y la
lección de experiencia de uso del laboratorio. La desviación correspondiente es
[D-19](../../architecture/deviations.md#d-19--spikes-resecuenciados-al-cerrar-m1).

El criterio de salida de M1 ([§14 del plano](../../architecture/blueprint.md#14-hoja-de-ruta-por-hitos)) admite «una
decisión registrada» en lugar de todos los criterios superados. Esta es esa decisión: los criterios de
[§15.1](../../architecture/blueprint.md#151-spikes-con-criterio-de-éxito) siguen **intactos**, y lo que cambia es el
hito y la forma en que se miden.

## Evidencia real

### Automática (CI)

La CI (`desk (x64)` en `windows-2025`, validada en S0 con 10 de 10) pasa todas las pruebas automáticas de S1, S3 y S4:
no activación de las superficies con dedo, lápiz y mouse sintéticos, activación forzada detectada y revertida, UI
Automation dentro y fuera del proceso, avisos *live*, Axe, concesiones por origen y el atajo interno. El detalle de
cada ejecución local está en las tablas de «Resultados» de [S1](S1.md), [S3](S3.md) y [S4](S4.md).

### Manual (equipo táctil del usuario)

Informes de SpikeLab del 2026-09-26 en `%LOCALAPPDATA%\Clicalo.SpikeLab\reports` (pantalla táctil de 2400 × 1600 al
175 %, dedo, lápiz y mouse):

| Spike | Qué se hizo | Resultado |
|---|---|---|
| S1 | 473 toques reales (dedo, lápiz y mouse) sobre una `NonActivatingWindow`, la tira-guía de SpikeLab, con Word (360) y Chrome (113) en primer plano | **0** cambios de primer plano hacia SpikeLab, **0** violaciones `reg01`, latencia p95 **15,7 ms** (máximo 33,7 ms) |
| S1 | Toques sobre el panel, la Pestaña y la burbuja (filas 1 a 32) | **Ninguno.** La tira indujo a pulsar «Funcionó» 20 veces: 123 «Funcionó», 216 toques `NoTarget` y 78 `Debounced` sobre la propia tira, así que ninguna fila quedó registrada |
| S3 | Órdenes de Acceso por voz y Narrador (filas 1 a 13) | No se ejecutaron (solo navegación de la tira) |
| S4 | Dictado y orígenes de voz (filas 1 a 11) | No se ejecutaron (solo navegación de la tira) |

Lo que sí demuestra la parte manual: con hardware real y dos apps reales, una ventana no activable de WPF recibe cientos
de toques de dedo, lápiz y mouse sin quitar el primer plano ni una vez y con una latencia muy por debajo de los 50 ms
de NFR-002. Es la misma clase base (`NonActivatingWindow`) que usarán el panel, la Pestaña y la burbuja.

Lo que **no** demuestra: el panel, la Pestaña con su lateral y la burbuja reales; el IME japonés o chino; los menús
abiertos de la app objetivo; el arrastre entre monitores; Acceso por voz, Narrador y el Reconocimiento de voz de
Windows 10; y el dictado (Win+H, Wispr Flow, Typeless) con devolución del primer plano.

## Decisión del usuario

**Continuar con M2.** Las filas manuales de S1 (panel, Pestaña, burbuja, IME y menús), de S3 (Acceso por voz y
Narrador) y de S4 (dictado) se repiten **con el panel real** en la aceptación en hardware de M3
([§10.2](../../architecture/blueprint.md#102-pruebas-de-accesibilidad-y-aceptación-en-hardware)), **midiendo
automáticamente** en lugar de pedir al usuario que cuente (preferencia del usuario). Sus criterios no cambian: 20 de
20 por fila, ningún cambio de primer plano, IME sin cancelar, órdenes de voz sin activar la ventana, TSF y dictado en
todos los campos. Las reglas de decisión de cada guion siguen vigentes: si en M3 falla una fila de toque, se reabre
[ADR-0001](../../adr/0001-framework-ui-wpf.md) **antes** de seguir con funcionalidad.

El riesgo aceptado es construir M2 (motor, persistencia, Sentinel, panel mínimo) antes de tener la prueba manual del
panel real. Es acotado: M2 no añade superficies nuevas y todo lo que construye es independiente de la capa de UI
(ADR-0001, ADR-0002).

## Dónde se resuelve cada spike

Ningún criterio se rebaja: la tabla dice solo **cuándo** y **cómo** se cumple el de §15.1.

| Spike | Criterio (§15.1, sin cambios) | Dónde se resuelve | Cómo | Responsable |
|---|---|---|---|---|
| S1 · No activación (filas manuales) | 20 de 20 por superficie; IME sin cancelar; activación forzada detectada y revertida | Aceptación en hardware de M3 | Panel real, medición automática (ver la lección) | M3 |
| S2 residual · dispatcher con el CC cargado; conectar y desconectar la pantalla táctil (#2054) | p95 del panel dentro de ±10 % con el CC maquetando 2000 atajos con un dispatcher; sin cuelgues al conectar y desconectar | **Antes de M3** | Prueba de rendimiento con el CC real y prueba en el equipo táctil | Antes de M3 |
| S3 · UIA (filas manuales) | Todas las órdenes sin activar la ventana; invocar por UIA no cambia el primer plano | Aceptación en hardware de M3 | Panel real, medición automática | M3 |
| S4 · Texto y primer plano (filas manuales) | 20 de 20 ciclos por origen; TSF y dictado en todos los campos; F24 = 0 | Aceptación en hardware de M3 (y S4 sobre formularios reales en M4, como ya fija §14) | Panel real, medición automática | M3, M4 |
| **S5** · Arranque y memoria | Frío manual ≤ 1 s; al iniciar sesión ≤ 1 s; ≤ 120 MB; ≤ 0,5 % de CPU | **M2** | Pruebas en `tests/Clicalo.Performance` (CI y equipo táctil) | Paquete `app` |
| S6 · Capacidad visual | 60 fps; el margen no captura clics; ΔEOK < 0,02; decisión sobre el desenfoque | **Antes de M3** | Pruebas de renderizado y de *hit test* del margen | Antes de M3 |
| **S7** · Inyección y *hook* | 100 % de eventos correctos en InputProbe en ambos modos; el *hook* no se retira en 30 min de estrés | **M2** (el *hook* bajo GC se repite en M4, como fija §14) | `Platform.IntegrationTests/Engine` con InputProbe; las variantes con AltGr y Ctrl derecho solo en la CI | Paquete `engine` |
| S8 · Distribución y firma | Flujo automatizado salvo la firma del manifiesto; ninguna DLL sin firma; propietario correcto tras actualizar elevado | **Antes de M5** | Guion del spike antes de empezar M5 | Antes de M5 |
| **S9** · Guardián, *ledger* y valla | Liberaciones ≤ 200 ms en 50 de 50; modo seguro tras 3 fallos en 10 min; ninguna tecla pegada al reanudar un hilo congelado; escalada a reinicio | **M2** | `Clicalo.Sentinel.Tests` y propiedades del motor; las pruebas que matan procesos o congelan hilos con inyección real llevan `Requires=Desktop` y `Category=Chaos` y **solo corren en la CI** | Paquete `engine` |
| S10 · IPC y etiquetas | Detección 10 de 10; el cliente medio solo pide `Show` | **Antes de M5** | Pruebas de integración con servidor elevado | Antes de M5 |
| **S11** · Persistencia hostil | Ningún documento perdido o de fábrica; error visible si el bloqueo dura más de 3 s | **M2** | `Clicalo.Infrastructure.Tests` con bloqueos reales y `CrashingFileSystem` | Paquete `persistence` |
| S12 · Bloqueo y suspensión | Estado vacío al desbloquear o reanudar en 20 de 20 | Aceptación en hardware de M3 | Medición automática en el equipo táctil | M3 |
| S13 · Origen de la entrada | Distinción fiable entre hardware e inyectado | M7 (como ya fija §15.1) | Spike con uiAccess | M7 |
| S14 · Componente de sistema | Inicio elevado sin UAC en 20 de 20; primer frame ≤ 1 s sin sincronización; nada no verificado se ejecuta elevado | **Antes de M5** | Guion del spike antes de empezar M5 | Antes de M5 |
| S15 · Posición del puntero | Clic derecho, rueda y arrastre en el último punto externo en 20 de 20 por escenario | **Antes de M3** | `Platform.IntegrationTests` con toque sintético y el equipo táctil | Antes de M3 |

«Antes de M3» y «Antes de M5» significan que el hito siguiente no empieza hasta tener el spike superado o una decisión
registrada con la regla de su guion.

## Lección: el laboratorio cuenta solo

La tira-guía de SpikeLab pedía al usuario que contara repeticiones y que pulsara «Funcionó» o «Falló». Con una lesión
medular, dedo y voz, eso es trabajo que la herramienta debe hacer por él, y además desvió la sesión: la tira era la
superficie más visible, así que los toques acabaron en ella (216 `NoTarget` y 78 `Debounced`) y ninguna fila del panel
quedó registrada.

Reglas para todo laboratorio y para la aceptación en hardware desde M3:

1. **Contar solo.** La herramienta detecta qué superficie, dispositivo y app objetivo intervienen en cada toque u
   orden (`PointerInputSource`, `ActivationGuard`, `ForegroundOrchestrator`, InputProbe) y acumula las filas sin que
   el usuario diga cuántas van.
2. **Sin botones de veredicto.** Nada de «Funcionó» ni «Falló»: el veredicto sale de lo medido (primer plano, foco,
   texto recibido en la sonda, `reg01.violations`). Donde de verdad hace falta el juicio humano (Narrador leyó bien
   un aviso), se pregunta **una vez al final**, no en cada repetición.
3. **No pedir 20 repeticiones seguidas.** El uso normal del panel real durante una sesión de trabajo es la muestra;
   la herramienta cuenta en segundo plano y avisa cuando una fila llega a 20 de 20.
4. **La herramienta no es un objetivo.** Ninguna superficie de guía compite con las superficies que se miden: los
   toques sobre la guía no cuentan y se muestran como ignorados de forma discreta; la guía se puede ocultar.
5. **Informe primero, preguntas después.** El informe dice qué filas quedan y por qué, sin exigir que el usuario lo lea
   para seguir.
