---
status: Propuesto
date: 2026-10-05
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisión del usuario del 2026-10-05 (simplicidad primero); decisión D3 del usuario (2026-10-03); ADR-0004, ADR-0018 y ADR-0019; plano §3.1, §3.2, §7.4 a §7.6 y §7.10; catálogo REG-03, SEG-003, SEG-006, SEG-007, NFR-005 y SIS-004
informed: colaboradores y agentes, mediante el plano, deviations.md (D-25), contracts.md y AGENTS.md
---

# ADR-0022 · Guardián simple: soltar lo que Windows dice que está pulsado

## Contexto y planteamiento del problema

[ADR-0004](0004-motor-ledger-valla-y-sentinel.md), [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md) y
[ADR-0019](0019-valla-en-las-escrituras-del-motor-y-reenvio-de-liberaciones.md) protegían REG-03 («siempre hay forma de
soltar las teclas») con una maquinaria grande: un *ledger* de 4 KiB en memoria compartida con escritura adelantada,
una valla de generación bajo *lock* en cada efecto externo, un latido del motor, `EmergencyReleaser` con reinicio del
motor o del proceso, la detección de hilos zombis y un protocolo de arranque de Sentinel con tres *handles* heredados,
un *pipe* de latido y siete argumentos. Cada pieza tenía sus pruebas de modelo (muerte en cada paso, congelar y
reanudar) y sus correcciones (ADR-0019), y el conjunto era la parte más cara de mantener del motor.

El 2026-10-05 el usuario decidió trabajar con **la solución más simple que cumpla el requisito del catálogo**, sin
capas para escenarios teóricos y sin rebajar ningún requisito. La persona que usa Clícalo no usa teclado físico: todo
lo que Windows da por pulsado lo pulsó Clícalo. Con ese hecho, Windows ya sabe exactamente lo que hay que soltar.

¿Cómo se mantiene REG-03 (Soltar todo, cambio de app, bloqueo, suspensión, cierre, fallo o cierre forzado y tiempo
máximo) con la mínima maquinaria?

## Factores de decisión

- REG-03, SEG-003, SEG-006, SEG-007 y NFR-005: ningún camino deja algo pulsado, tampoco si el proceso muere o el motor
  se cuelga.
- Decisión D3 del usuario (2026-10-03): si el proceso muere con la sesión bloqueada, se suelta antes de relanzar.
- SIS-004: relanzar tras un fallo y entrar en modo seguro en un bucle de fallos (3 en 10 minutos).
- REG-01: nada de esto puede quitar el foco al panel.
- Coste de operación para una sola persona y pruebas que bloquean un PR deterministas (sin escritorio, sin tiempos
  reales, sin azar).

## Opciones consideradas

- Mantener el *ledger*, la valla y la emergencia (ADR-0004, ADR-0018 y ADR-0019).
- **Guardián simple: preguntar a Windows qué está pulsado y soltarlo todo** (elegida).
- Soltar solo los modificadores a ciegas.

## Resultado de la decisión

Opción elegida: «Guardián simple», porque cumple los mismos requisitos con una fracción del código y de las pruebas,
y su única suposición (lo pulsado lo pulsó Clícalo) es un hecho del usuario para el que se diseña el producto.

1. **Un único soltado de lo pulsado** (`Clicalo.Platform.Core.Injection.PressedInputRelease`). Lee `GetAsyncKeyState`
   de `0x01` a `0xFE`; suelta primero los botones del mouse (el izquierdo y el derecho juntos si Windows da por pulsado
   cualquiera de los dos, por si los botones están intercambiados), después las teclas y al final los modificadores.
   Cada tecla sube en modo VK con el código de exploración de `MapVirtualKey(MAPVK_VK_TO_VSC_EX)` y
   `KEYEVENTF_EXTENDEDKEY` si es extendido; Alt y Win llevan antes la máscara de menú (`VK 0xE8` pulsada y soltada) para
   que no se abra Inicio ni una barra de menús. Shift, Ctrl y Alt genéricos solo suben si ningún lado está pulsado. Si
   el escritorio de entrada no es del proceso (`OpenInputDesktop` falla: sesión bloqueada, UAC, Ctrl+Alt+Supr),
   Windows no deja leer ni enviar, y el intento cuenta como no completado.
2. **Sentinel mínimo** (`Clicalo.Sentinel`, Native AOT). Espera al principal con `WaitForSingleObject`, lee su código
   de salida y suelta con el punto 1. Mientras no se complete, lo intenta otra vez cada
   `Timings.Guardian.ReleaseRetryInterval` (1 s), **sin límite**, leyendo cada vez el estado de nuevo (decisión D3). Solo
   después relanza `Clicalo.exe`, y solo si el código de salida no es 0, con `Timings.App.CrashLoop` (al tercer fallo en
   10 minutos, en modo seguro; si el relanzamiento en modo seguro también falla dentro de la ventana, deja de
   relanzar para no entrar en bucle). Sentinel sigue sin escribir archivos: el principal relanzado registra el fallo con
   `--after-crash`.
3. **Contrato de arranque mínimo: protocolo 3.** Un único *handle* heredado, el del principal
   (`SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION`), y cuatro argumentos: `--protocol=3`, `--parent=0x…`,
   `--retry-ms=<n>` y `--crash-loop=<n>/<ms>` ([contracts.md](../architecture/contracts.md)). Sin *pipe*: el principal
   comprueba cada `Timings.Guardian.WatchInterval` que Sentinel sigue vivo y lo relanza con
   `Timings.Guardian.RestartBackoff` y `RestartLoop`, como antes. Un cierre normal, el fin de la sesión, una
   actualización o el relevo elevado salen con código 0, así que no hacen falta marcas.
4. **Dentro de Clícalo no cambia lo que se suelta.** El reductor sigue soltando en cada evento terminal (cambio de app,
   bloqueo, suspensión, cierre), con «Soltar todo» del panel y al vencer el tiempo máximo, y sigue reenviando lo que el
   escritorio seguro rechazó (`BlockedReleases`) cuando vuelve el escritorio de entrada. Lo que cambia:
   - una excepción en el motor (NFR-005) suelta con el punto 1 (`IInputInjector.ReleasePressed`), no desde su estado,
     que puede ser la parte rota; si se rechaza, conserva lo que tenía el último estado bueno para reenviarlo;
   - **«Soltar todo» de la bandeja** publica `ReleaseAll` en el motor y además suelta con el punto 1 en el *ThreadPool*,
     así que funciona con el motor colgado. No hay valla ni generaciones: un motor colgado no se sustituye;
   - el soltado preventivo del arranque usa el punto 1 (todo lo pulsado, no solo los modificadores).
5. **Un solo envío.** `LowLevelInjector` (Platform.Core/Injection) sigue siendo el único `SendInput` del producto,
   detrás de `IInputInjector` (`InputInjector`, Platform.Windows) y de `PressedInputRelease`. `InputInjector` conserva el
   equilibrado de los lotes que `SendInput` acepta solo en parte y, en los acordes internos, no vuelve a pulsar ni suelta
   una tecla que Windows ya da por pulsada.

**Qué sustituye** (los ADR aceptados no se editan):

- De [ADR-0004](0004-motor-ledger-valla-y-sentinel.md): el *ledger* v2 con escritura adelantada, la valla de
  generación, la emergencia con reinicio del motor o del proceso y los tres *handles* de Sentinel. Siguen vigentes el
  núcleo puro con actor, los efectos bloqueantes en el hilo Shell, `InjectionMode` por plan y Sentinel como proceso
  Native AOT aparte.
- De [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md): el punto 1 (protocolo 2), el punto 2 (*ledger* v2)
  y la forma del punto 6 (reintento solo de lo que no salió, distinción entre el rechazo del escritorio seguro y los
  demás, `RefusedReleaseWait`). La decisión D3 se mantiene y se simplifica: cualquier rechazo se reintenta sin límite.
  Siguen vigentes el envoltorio 1.0, la clave canónica y el relanzamiento con `--after-crash` (puntos 3 a 5).
- De [ADR-0019](0019-valla-en-las-escrituras-del-motor-y-reenvio-de-liberaciones.md) (propuesto): el latido y las marcas
  bajo la valla, el reenvío desde el *ledger* físico, los lotes equilibrados «en la valla» y la emergencia sin
  guardián. Siguen vigentes el reenvío de lo rechazado con «Soltar todo», los terminales y el regreso del escritorio de
  entrada (`InputDesktopWatch`), y los acordes internos desde el motor.
- En el plano se retiran las invariantes INV-2 (cobertura por el *ledger*) e INV-11 (valla) y las propiedades «muerte en
  cada paso» y «congelar y reanudar». Las demás invariantes del reductor no cambian.

### Consecuencias

- Buena, porque desaparecen la memoria compartida, el *lock* de la valla, el latido, el *pipe*, la emergencia y el
  reinicio del motor, con sus casos límite (motores zombis, la valla tomada dentro de `SendInput`, la emergencia sin
  guardián).
- Buena, porque Sentinel ya no depende de que el principal haya registrado bien lo que pulsó: suelta lo que de verdad
  está pulsado, incluido lo que dejó a medias un `SendInput` parcial o un estado roto.
- Buena, porque las pruebas que bloquean un PR son deterministas (un estado de teclas y un `SendInput` falsos) y la
  muerte real del proceso queda en la prueba de caos nocturna.
- Mala, porque un motor colgado no se sustituye solo: la persona usa «Soltar todo» de la bandeja y, si hace falta,
  «Salir»; Sentinel suelta otra vez al terminar el proceso. Una macro en curso no se recupera.
- Mala, porque una tecla de un teclado físico pulsada en ese instante también se suelta (el soltado preventivo de
  SEG-006 ya lo aceptaba).
- Mala, porque una tecla pulsada en modo *scancode* sube en modo VK con el código de exploración que da la distribución
  actual; si una app solo mirara el código y la distribución lo tradujera a otro, quedaría pulsada para esa app hasta
  el siguiente soltado. Se revisa con la prueba de caos y la aceptación en hardware.
- Neutral, porque Sentinel puede vivir tanto como dure un bloqueo de sesión (un intento por segundo, bloqueado en una
  espera entre medias) y Clícalo no vuelve hasta el desbloqueo: es lo que pidió D3.

### Confirmación

- `Clicalo.Sentinel.Tests` (`[Trait("Req", "SEG-006")]`, `SEG-007`, `REG-03`, `SIS-004`), deterministas y en
  `cl check`: el guardián suelta exactamente lo pulsado, pone la máscara antes de Alt o Win, suelta en modo VK con la
  marca extendida, reintenta mientras se rechaza o no puede leer el estado, relanza solo tras una salida anómala y
  respeta el bucle de fallos; el contrato del protocolo 3.
- `Clicalo.Platform.IntegrationTests`: `InputInjector` (lotes equilibrados, acordes internos, `ReleasePressed`) y
  `SentinelSupervisor` con un lanzador y un reloj falsos.
- `Clicalo.Application.Tests`: una excepción del motor suelta con `ReleasePressed` y conserva lo rechazado.
- `S9ChaosTests` (`Category=Chaos`, nocturna y obligatoria antes de publicar una versión): `TerminateProcess` del
  principal con Ctrl+Shift, un arrastre o una macro; nada pulsado 1 s después de la muerte, 50 de 50.
- `architecture/banned-api-exceptions.json` sigue limitando `SendInput` a `Platform.Core/Injection`.

## Pros y contras de las opciones

### Mantener el *ledger*, la valla y la emergencia

- Buena, porque ya estaba escrito y probado con modelos.
- Mala, porque su coste (memoria compartida, *lock*, latido, zombis, emergencia, protocolo de siete argumentos) protege
  contra escenarios que el usuario no tiene: nadie más pulsa teclas en su equipo.
- Mala, porque sus pruebas de modelo y de caos son lentas y su verificación de M2 ya obligó a un ADR de correcciones.

### Guardián simple (elegida)

- Buena, porque Windows es la fuente de verdad de lo pulsado y no hay nada que mantener sincronizado.
- Buena, porque el contrato con Sentinel es mínimo y el principal no comparte memoria con él.
- Mala, por las consecuencias de arriba (motor colgado sin reemplazo, teclado físico, modo *scancode*).

### Soltar solo los modificadores a ciegas

- Buena, porque es aún más simple.
- Mala, porque deja pegadas las teclas que no son modificadores (un Mantener de «A», un arrastre) y no cumple SEG-007.

## Criterios de reapertura

- Si la prueba de caos nocturna de S9 encuentra una tecla pulsada 1 s después de la muerte en alguno de los 50 intentos.
- Si el catálogo admite un teclado físico como entrada habitual del usuario: soltar todo dejaría de ser inocuo.
- Si la aceptación en hardware muestra que una tecla pulsada en modo *scancode* no sube con el soltado en modo VK.

## Más información

- Plano: [§3.1](../architecture/blueprint.md#31-vista-de-procesos),
  [§3.2, regla 6](../architecture/blueprint.md#32-modelo-de-hilos),
  [§7.4](../architecture/blueprint.md#74-registro-de-pulsadas),
  [§7.5](../architecture/blueprint.md#75-invariantes-de-seguridad-de-teclas),
  [§7.6](../architecture/blueprint.md#76-eventos-terminales-seg-007) y
  [§7.10](../architecture/blueprint.md#710-grabación-de-combinaciones-y-verificación).
- [contracts.md](../architecture/contracts.md) (protocolo 3) y [deviations.md, D-25](../architecture/deviations.md#d-25--guardián-simple).
- ADR relacionados: [ADR-0004](0004-motor-ledger-valla-y-sentinel.md),
  [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md) y
  [ADR-0019](0019-valla-en-las-escrituras-del-motor-y-reenvio-de-liberaciones.md).
- Documentación de Win32: `GetAsyncKeyState`, `MapVirtualKey`, `OpenInputDesktop` y `SendInput`.
