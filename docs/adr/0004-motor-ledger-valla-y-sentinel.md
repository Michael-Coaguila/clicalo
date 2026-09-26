---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuestas de dominio y de fiabilidad; crítica del plano 1.0 (hueco «Motor / REG-03»)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0004 · Motor funcional, *ledger* con escritura adelantada, valla de generación y Sentinel

## Contexto y planteamiento del problema

Clícalo mantiene teclas pulsadas en nombre del usuario: un Mantener, un Alternar, las teclas fijas,
el botón del mouse al arrastrar. En Macro Quick Access, un fallo a mitad del envío podía dejar Win o
AltGr pulsadas, y no había registro de lo pulsado ni soltado al arrancar o al cerrar (lección
L-ENV-2). Para quien no puede pulsar una tecla física para soltarla, una tecla pegada bloquea el
equipo.

El catálogo lo exige sin excepciones: siempre hay forma de soltar (REG-03), hay un registro de
pulsadas (SEG-001) y ningún camino deja algo pulsado (SEG-007), tampoco una excepción (NFR-005). Hay
tres escenarios difíciles:

1. **El proceso muere** (fallo, `TerminateProcess`): quien debería soltar ya no existe.
2. **El hilo del motor se cuelga** (por ejemplo, un `SendInput` retenido por *hooks* de terceros) y
   **vuelve** después de que se haya soltado todo: podría volver a pulsar teclas. En .NET no se puede
   «reiniciar» un hilo colgado.
3. **Efectos que bloquean** (`ShellExecute`, WMI) no pueden retrasar un «Soltar todo».

¿Cómo se garantiza, por construcción y con pruebas, que nunca queda una tecla pulsada?

## Factores de decisión

- Las invariantes de seguridad de teclas deben poder probarse como propiedades.
- Un proceso muerto no responde: el estado de lo pulsado tiene que sobrevivirle.
- Soltar a ciegas deja teclas pegadas y abre menús: hay que soltar exactamente lo pulsado, en el mismo
  modo (VK o scancode).
- Un hilo zombi no puede inyectar nada después de una liberación de emergencia.
- El guardián debe arrancar en milisegundos, no depender de WPF y ocupar 5 MB o menos.
- Soltar nunca espera detrás de una macro ni de un `ShellExecute` lento.

## Opciones consideradas

- Motor funcional (`EngineReducer` puro) ejecutado por un actor, *ledger* v2 en memoria compartida,
  valla de generación en cada efecto externo y guardián `Clicalo.Sentinel` Native AOT
- Servicios con estado mutable protegidos con bloqueos
- Guardián como modo `--guardian` del mismo ejecutable
- Soltar «todos los modificadores» a ciegas, o preguntar el estado por IPC
- Un servicio de Windows (sesión 0) como guardián
- Reiniciar siempre el proceso ante cualquier cuelgue del motor

## Resultado de la decisión

Opción elegida: **«Motor funcional con actor, *ledger* v2, valla de generación y Sentinel Native
AOT»**, porque convierte las garantías en propiedades comprobables sobre una función pura y cubre con
la valla el único caso que el modelo puro no cubre: un hilo colgado que vuelve.

- **Núcleo puro.** `EngineReducer.Reduce(estado, evento) → (estado', efectos)` vive en Domain e
  incorpora `ActivationPolicy` y los planificadores por tipo de acción. `EngineHost` (Application) es un
  actor de un solo hilo con un buzón de dos carriles: `Priority` (Soltar todo, eventos terminales, fin
  de contacto) siempre se vacía antes que `Normal`. Las macros son máquinas de estado con
  temporizadores, nunca `Thread.Sleep`.
- ***Ledger* v2 con escritura adelantada** (`Clicalo.Platform.Core.KeyLedger`): una sección de 4 KiB
  sin nombre, heredable solo por el guardián, con 128 ranuras (vk, scancode, marcas extendida y modo
  scancode, estado y recuento), la generación del motor, el último latido y las marcas `CleanShutdown`,
  `NoRelaunch`, `EngineAlive` y `EmergencyRestart`. Protocolo: `BeginDown` → `SendInput` → `Commit`.
  No existe ningún instante en que una tecla esté pulsada sin estar registrada.
- **Valla de generación.** Todo efecto con consecuencias externas (inyección, escritura del *ledger*,
  pegar, `Launch`, `SystemCommand`) pasa por `InjectionGate.TryRun(g, …)`, que compara la generación y
  envía bajo un mismo *lock*. Un hilo con generación vieja recibe `Fenced` y no inyecta nada.
- **Emergencia.** Si el latido no avanza en 2 s, `EmergencyReleaser` (hilo SysEvents) intenta tomar la
  valla durante 250 ms. Si la toma, sube la generación, suelta todo lo registrado dentro del *lock* y
  crea un motor nuevo. Si no la toma, marca `EmergencyRestart`, escribe el `crash-journal` y termina el
  proceso: Sentinel suelta desde el *ledger* y relanza. Un segundo cuelgue en 10 minutos escala
  directamente a reinicio.
- **Sentinel** (`Clicalo.Sentinel.exe`, Native AOT, 5 MB o menos, misma integridad): hereda solo tres
  *handles* (el proceso padre, el *ledger* en solo lectura y un extremo de un pipe anónimo). Al morir el
  padre suelta lo registrado en el modo registrado, con máscara de menú, y relanza salvo `CleanShutdown`,
  `NoRelaunch` o bucle de fallos (3 en 10 minutos, en `timings.json`).
- **Efectos bloqueantes fuera del motor.** `Launch` y `SystemCommand` se ejecutan en el hilo Shell, con
  la generación, y su resultado vuelve al buzón.
- **`InjectionMode` por plan** (D24): `VirtualKey` o `ScanCode` viaja del perfil al *ledger*, y cada
  liberación usa el mismo modo, vk y scancode que su pulsación (INV-12).

### Consecuencias

- Buena, porque INV-1 a INV-12 ([§7.5 del plano](../architecture/blueprint.md#75-invariantes-de-seguridad-de-teclas))
  se verifican como propiedades en cada paso.
- Buena, porque un proceso muerto o un hilo zombi ya no pueden dejar teclas pegadas.
- Buena, porque el mismo `KeyLedger` lo usan el motor, Sentinel y las pruebas («muerte en cada paso»).
- Mala, porque hay un proceso más que construir, firmar y mantener, y un proyecto compatible con AOT
  (`Clicalo.Platform.Core`).
- Mala, porque queda un riesgo residual aceptado: el intervalo sin guardián al arrancar (menos de
  200 ms, medido en S9). Lo cubren `EngineHost`, `EmergencyReleaser` y el soltado preventivo del
  siguiente arranque.
- Mala, porque «Finalizar árbol de procesos» mata a los dos; lo cubre el soltado preventivo del
  siguiente arranque, sin trucos de *re-parenting*.

### Confirmación

- Pruebas basadas en modelo (CsCheck) con secuencias de hasta 200 eventos que comprueban INV-1 a
  INV-12 en cada paso; los contraejemplos reducidos se guardan como regresiones.
- «Muerte en cada paso» y «congelar y reanudar» (incluido dentro de `TryRun`) con un
  `PhysicalStateInjector` que permite bloquear un `SendInput` concreto.
- Arnés con InputProbe en es-ES, en-US, es-419 y AltGr, en los dos modos de inyección.
- Criterio de M2: propiedades en verde con 10 000 casos y caos de Sentinel en 50 de 50.
- Spike S9: liberaciones en 200 ms o menos en 50 de 50, modo seguro tras 3 fallos en 10 minutos, ninguna
  tecla pegada al reanudar un hilo congelado y escalada a reinicio cuando no se toma la valla.

## Pros y contras de las opciones

### Motor funcional, *ledger* v2, valla y Sentinel

- Buena, porque las garantías son propiedades de una función pura más un protocolo pequeño.
- Buena, porque Sentinel en AOT arranca en milisegundos y no comparte los riesgos del proceso principal.
- Mala, porque es el diseño más elaborado de las opciones y exige pruebas de caos en hardware real.

### Servicios con estado mutable y bloqueos

- Buena, porque es el enfoque más directo.
- Mala, porque las invariantes no se pueden probar como propiedades y los bloqueos invitan a
  interbloqueos en el camino de soltar.

### Guardián `--guardian` en el mismo ejecutable

- Buena, porque evita un proyecto más.
- Mala, porque cargaría un runtime autocontenido de decenas de MB y compartiría los riesgos del
  proceso principal.

### Soltar a ciegas o preguntar por IPC

- Buena, porque no necesita memoria compartida.
- Mala, porque un proceso muerto no responde por IPC.
- Mala, porque soltar «todos los modificadores» a ciegas deja teclas no modificadoras pegadas, ignora
  el modo scancode y puede abrir menús.

### Servicio de Windows (sesión 0)

- Buena, porque sobreviviría a la sesión del usuario.
- Mala, porque desde la sesión 0 no se inyecta en el escritorio del usuario y añade un servicio con
  privilegios que operar.

### Reiniciar siempre el proceso ante un cuelgue

- Buena, porque es simple.
- Mala, porque reinicia la UI por cualquier cuelgue transitorio; con la valla solo se reinicia cuando
  no se puede tomar (o en el segundo cuelgue en 10 minutos).

## Criterios de reapertura

- Si S9 no cumple sus criterios de éxito ([§15.1](../architecture/blueprint.md#151-spikes-con-criterio-de-éxito)),
  se replantea este ADR.
- Si S7 muestra que el *hook* de grabación se retira bajo presión, se traslada a Sentinel (plan B
  previsto; `IKeyboardRecorder` no cambia) y no hace falta un ADR nuevo.

## Más información

- Plano: [§1.2 (D3, D4, D5 y D24)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§3.1](../architecture/blueprint.md#31-vista-de-procesos),
  [§3.2, regla 6](../architecture/blueprint.md#32-modelo-de-hilos),
  [§7.3 a §7.7](../architecture/blueprint.md#73-núcleo-funcional-y-actor),
  [§7.10](../architecture/blueprint.md#710-grabación-de-combinaciones-y-verificación).
- Catálogo: REG-03 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  SEG-001 a SEG-008 en [§2.13](../requirements/catalog.md#213-seg--seguridad-de-teclas); NFR-004 y
  NFR-005 en [§3](../requirements/catalog.md#3-requisitos-no-funcionales).
- Registro: [critique.json](../architecture/decision-record/critique.json), huecos «Motor / REG-03»,
  «Motor / modo compatible» y «Efectos del motor que bloquean»;
  [techDecision.json](../architecture/decision-record/techDecision.json), afirmación crítica sobre el
  *hook* `WH_KEYBOARD_LL` y `LowLevelHooksTimeout`.
- Evidencia: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
  (tiempo máximo del *hook* de teclado de bajo nivel).
- ADR relacionados: [ADR-0003](0003-cuatro-duenos-de-estado.md),
  [ADR-0009](0009-elevacion-y-componente-de-sistema.md).
