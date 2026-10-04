# Latencia del toque en el panel (NFR-001) · cómo se mide en la CI

`PanelDesktopTests.Taps_of_finger_pen_and_mouse_reach_the_engine_and_never_take_the_foreground` mide la parte del
panel del presupuesto de NFR-001: **desde que Windows registra el levantamiento del dedo** (el `PerformanceCount` del
fotograma `WM_POINTERUP`) **hasta que la activación está en el buzón del motor**. El tramo completo «toque →
`SendInput`» lo mide `TouchToSendInputTests` (`Clicalo.Performance`, app publicada) y, como requisito, la aceptación en
el equipo táctil ([§10.3 del plano](../architecture/blueprint.md#103-presupuestos-de-rendimiento)). Esta página explica
qué hacía fallar la prueba de forma intermitente en la CI de escritorio, qué se cambió y con qué criterio pasa ahora.

## Resumen

- **No era el producto.** En los picos el hilo de UI del panel estaba **ocioso**: ningún mensaje en la cola, ninguna
  operación del *dispatcher* y menos de 6 ms de CPU en 500 ms. El trabajo del panel, de `WM_POINTERUP` al buzón, es de
  0,04 ms (p50) en un toque normal y de 3–8 ms en el primero del proceso (JIT en Debug).
- **Era el arnés.** La prueba creaba un dispositivo sintético nuevo (`CreateSyntheticPointerDevice`) en **cada**
  toque. Windows anuncia cada dispositivo nuevo a todas las ventanas (`WM_TABLET_ADDED`, `0x02C8`; al destruirlo,
  `WM_TABLET_DELETED`, `0x02C9`) y retiene su primer contacto hasta entonces: 15–40 ms de costumbre en los *runners*
  alojados, hasta 200 ms. Una pantalla táctil es un único dispositivo durante toda la sesión: ese coste no existe en un
  toque real.
- **Primer contacto en una ventana recién mostrada.** Aun con el dispositivo ya creado, el primer contacto de cada
  dispositivo sobre el panel recién compuesto llega tarde (20–50 ms el dedo; el lápiz, hasta 615 ms con la suite
  completa): Windows envía `WM_NCHITTEST` y `WM_POINTERACTIVATE` y entrega el contacto solo después, con el hilo de UI
  ocioso. Pasa una vez por ventana y por dispositivo; el panel vive toda la sesión.
- **Criterio de la CI.** Un dispositivo por tipo para todo el ciclo y **un toque de calentamiento por dispositivo**
  sobre el mismo mosaico, que tiene que llegar al motor como cualquier otro y cuya latencia se informa pero no se
  juzga; después, **21 toques medidos** (7 de dedo, 7 de lápiz y 7 de ratón) juzgados con los números de
  `TouchToSendInput` de [`data/catalogs/budgets.json`](../../data/catalogs/budgets.json): **p95 ≤ 50 ms** (rango más
  próximo) con al menos 20 muestras. El presupuesto **no cambia** ni hay un umbral propio de la CI: con este criterio el
  p95 de la CI queda muy por debajo de 50 ms (tabla de abajo).

## Cómo se midió

La prueba parte cada toque en tramos ([`TapSegments`](../../tests/Clicalo.Windowing.IntegrationTests/Panel/TapSegments.cs)):

| Tramo | De | A |
|---|---|---|
| `queue` | Windows registra el levantamiento (`PerformanceCount`) | El procedimiento de ventana del panel recibe `WM_POINTERUP` (un *hook* de `HwndSource` que corre antes que la capa de punteros) |
| `handling` | `WM_POINTERUP` recibido | La activación en el buzón (`PointerInputSource` → `GestureRecognizer` → mosaico → `PanelInteractionController`) |

Además, por toque: colecciones del GC por generación y pausa, métodos compilados por el JIT y su tiempo, la prioridad
del hilo de UI y, para los toques de más de 5 ms, la **línea de tiempo del hilo de UI**
([`PanelTapTimeline`](../../tests/Clicalo.Windowing.IntegrationTests/Panel/PanelTapTimeline.cs)): cada mensaje que toma
de la cola, cada mensaje que recibe el panel (también los enviados), cada operación del *dispatcher* con su prioridad y
su duración, y los ciclos de CPU del hilo entre una entrada y la siguiente. Cada ejecución escribe
`artifacts/cl/test-results/panel-tap-latency-*.json`, que el trabajo de escritorio sube como artefacto, y la
aserción incluye los tramos de los toques lentos.

El tramo motor → `SendInput` no está en esta prueba (el motor es un registrador): el buzón despierta al hilo Engine con
un `SemaphoreSlim` y su vuelta no espera a ningún temporizador; lo mide de extremo a extremo `TouchToSendInputTests`.

### Ejecuciones (`s0.yml` en una rama de medición, `windows-2025`, compilación Debug como `cl desk`)

| Ejecución | Condición | Ejecuciones de la prueba | Fallos | p95 por ejecución (mediana · máx.) | Dedo p50 |
|---|---|---|---|---|---|
| `s0` 37155289126 … 37159852354 (antes, rama `m2/fix-desk`) | Suite completa, dispositivo nuevo por toque | 50 | 6 | — · 816 ms (máx. de un toque) | 9–22 ms |
| 37162447600 | Sola, dispositivo nuevo por toque | 60 | 1 | 16,9 · 144,0 ms | 13,8 ms |
| 37164219850 | Sola, dispositivos creados antes (primer contacto en InputProbe) | 60 | 0 | 5,0 · 21,8 ms | 0,95 ms |
| 37164966961 | Suite completa (`cl desk`), ídem | 10 | 0 | 6,2 · **47,4 ms** | 0,9 ms |
| 37167016441 | **Criterio final**, suite del módulo (`dotnet test` con los filtros de `cl desk`) | 30 | 0 | 2,1 · 14,5 ms | 0,85 ms |
| 37167016441 | **Criterio final**, sola | 30 | 0 | 2,0 · 5,9 ms | 0,86 ms |

Con el criterio final, en las 60 ejecuciones: el trabajo del panel (`handling`) tiene p50 0,04 ms y máximo 2,5 ms; los
toques medidos de dedo y de lápiz, p99 de 6,5 ms y 1,8 ms en la suite; los de ratón, máximo 13 ms. Los toques de
calentamiento siguen enseñando el efecto que se deja fuera: dedo hasta 38 ms (sola) y lápiz hasta 433 ms (suite), con
el hilo de UI ocioso.

La ejecución 37164966961 enseñó el segundo efecto: con dos primeros contactos lentos en la misma ejecución (dedo 47 ms
y lápiz 615 ms), el p95 de 21 muestras (el segundo valor más alto) se quedó a 2,6 ms del presupuesto.

### Qué se ve en la línea de tiempo

Toque de dedo con dispositivo nuevo (ejecución 37162447600; +50 ms es cuando Windows registra el levantamiento):

```
+28.65 ms message 0x02C8 taken from the queue      ← WM_TABLET_ADDED (×5)
+59.04 ms message 0x0249 taken from the queue      ← WM_POINTERENTER
+65.40 ms message 0x02C9 taken from the queue      ← WM_TABLET_DELETED (×5)
+68.05 ms WM_POINTERDOWN received (thread priority Normal)
+68.17 ms WM_POINTERUP received (thread priority AboveNormal)
```

Ninguna operación del *dispatcher* entre medias: el `WM_POINTERDOWN` llega 18 ms **después** de que se registrara el
levantamiento, y el panel tarda 0,03 ms en llevar la activación al buzón.

Primer toque de lápiz sobre el panel en la suite completa (37166010499):

```
+24.46 ms 0x0200 received by the panel             ← WM_MOUSEMOVE (el lápiz sobrevuela)
+25.31 ms 0x0084 received by the panel             ← WM_NCHITTEST
+504.60 ms 0x0084 received by the panel [UI thread +16.23 Mcycles]
+504.66 ms 0x024B received by the panel            ← WM_POINTERACTIVATE
+504.73 ms WM_POINTERDOWN received by the panel
```

479 ms sin mensajes y con unos 16 millones de ciclos de CPU del hilo de UI (≈5 ms): el hilo esperaba en su cola.

## Por qué el criterio no rebaja el requisito

- **El presupuesto es el mismo** (`TouchToSendInput`, p95 ≤ 50 ms, ≥ 20 muestras), leído del catálogo en lugar de
  repetido en la prueba.
- **Lo que se deja fuera no es un toque de la persona:** es la llegada de un dispositivo sintético y el primer contacto
  de ese dispositivo sobre una ventana recién mostrada, que en el uso real ocurren una vez por sesión. El calentamiento
  se ejecuta, se comprueba (llega al motor, no quita el primer plano ni el foco) y su latencia queda en la salida.
- **El código frío no se esconde:** el primer toque del proceso cuesta 3–8 ms de JIT en Debug (dentro de `handling`),
  visible en los datos de la ejecución sola, y la app publicada usa ReadyToRun. El coste de arranque del primer toque
  es asunto de `TouchToSendInputTests` sobre la app publicada.
- **El requisito se verifica en el equipo táctil**, con hardware real, en la aceptación en hardware de M2/M3.

## Qué vigilar

- Si un p95 de la CI vuelve a acercarse a 50 ms, la salida de la prueba y su JSON dicen en qué tramo está el tiempo:
  `queue` con el hilo ocioso es Windows o el *runner*; `handling` o trabajo del *dispatcher* en la línea de tiempo es el
  producto.
- `TouchToSendInputTests` crea su dedo en el primer toque de la serie: ese toque carga con la llegada del dispositivo y
  con el primer contacto en la ventana del panel. Con 20 muestras el p95 tolera un valor atípico; un toque de
  calentamiento como el de esta prueba lo dejaría fuera sin tocar el presupuesto.
- El retraso del primer contacto del **lápiz** (hasta 615 ms con la suite completa, con `WM_TABLET_QUERYSYSTEMGESTURESTATUS`
  recibido justo antes) no se ha visto con el dedo ni con el ratón. Ocurre fuera del proceso y una vez por ventana, pero
  merece comprobarse con un lápiz real en S2 (gestos del sistema para lápiz) antes de descartarlo.
