---
status: Propuesto
date: 2026-09-26
decision-makers: Michael Coaguila (mantenedor)
consulted: ADR-0004, ADR-0018, plano §3.2 (regla 6), §3.6, §7.5 y §7.6; verificación de M2 (grupo «engine»)
informed: integración de M2, mediante docs/architecture/deviations.md (D-22)
---

# ADR-0019 · Valla en las escrituras del motor, reenvío de liberaciones rechazadas y emergencia sin guardián

## Contexto y planteamiento del problema

[ADR-0004](0004-motor-ledger-valla-y-sentinel.md) decidió el *ledger* con escritura adelantada, la valla de generación
y la emergencia que escala a reiniciar el proceso. La verificación de M2 encontró cuatro huecos en cómo se aplicó:

1. El latido y las marcas del *ledger* se escribían fuera de la valla. Un motor que se colgó fuera de ella y se
   reanuda tras la emergencia seguía escribiendo el latido del motor que lo sustituyó y ocultaba su cuelgue: una tecla
   pulsada quedaba sin emergencia (REG-03).
2. Una liberación que rechaza el escritorio seguro solo se reenviaba al desbloquear o reanudar. UAC y Ctrl+Alt+Supr
   muestran el escritorio seguro sin bloquear la sesión, y ni «Soltar todo» reenviaba lo rechazado.
3. Los acordes internos (derechos de primer plano, Win+H) se enviaban desde otro hilo con la generación vigente del
   *ledger*, así que la valla nunca los detenía, y un `SendInput` parcial dejaba sus modificadores pulsados sin titular.
4. La emergencia terminaba el proceso aunque no hubiera guardián que soltara ni relanzara.

¿Cómo se cierran sin cambiar el formato del *ledger* ni el contrato de Sentinel?

## Factores de decisión

- REG-03, SEG-003, SEG-006 y SEG-007: siempre hay forma de soltar las teclas.
- INV-3 e INV-11 ([§7.5 del plano](../architecture/blueprint.md#75-invariantes-de-seguridad-de-teclas)).
- El *ledger* v2 y el contrato de Sentinel de [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md) no cambian.
- Sin nuevos hilos ni esperas en el hilo del motor.

## Opciones consideradas

- Comprobar la generación en `EngineHost` antes de escribir, fuera de la valla.
- Escribir latido y marcas **dentro** de la valla, con la generación del motor.
- Para el escritorio seguro: sondear `OpenInputDesktop` con un temporizador fijo, o reaccionar a
  `EVENT_SYSTEM_DESKTOPSWITCH` con comprobaciones acotadas.
- Para los acordes internos: pasar a `InternalKeyEffects` la generación del motor activo, o convertirlos en eventos del
  motor.

## Resultado de la decisión

- **Latido y marcas bajo la valla** (`InjectionGate.TryWriteHeartbeat`, `TryUpdateMarks`): la comprobación y la
  escritura no pueden cruzarse con la emergencia, y el plano ya lo pedía («escritura del *ledger*» dentro de `TryRun`).
  Un latido rechazado detiene al anfitrión al momento.
- **Reenvío de lo rechazado** con «Soltar todo», con cada evento terminal y en cuanto vuelve el escritorio de entrada
  (`EVENT_SYSTEM_DESKTOPSWITCH` más comprobaciones acotadas de `OpenInputDesktop`); `SessionResumed` reenvía además lo
  que el *ledger* físico guarda en `ReleasePending` (`InjectionGate.TryReleasePending`), sin cambiar su formato.
- **Lotes equilibrados en la valla** (`TryInjectBalanced`, y `TryInjectChord` con la misma compensación).
- **Acordes internos como eventos del motor** (`InternalChordRequested` → `SendInternalChord`), como anunciaba D-14.
- **Emergencia sin guardián**: nunca termina el proceso; suelta y reinicia el motor en cuanto toma la valla, con esperas
  crecientes. Con guardián, la regla 6 no cambia.

### Consecuencias

- Buena, porque un zombi ya no puede ocultar un cuelgue, y lo rechazado se reenvía aunque no llegue un desbloqueo.
- Buena, porque todo lo que inyecta Clícalo pasa por el motor y su generación.
- Mala, porque el latido toma el *lock* de la valla en cada vuelta (sin contención salvo durante un `SendInput`).
- Mala, porque sin guardián un `SendInput` retenido indefinidamente deja el proceso esperando en lugar de reiniciarse;
  sin guardián, reiniciar tampoco soltaría nada.

### Confirmación

`EngineHostFenceTests`, `ZombieEngineTests` (el zombi reanudado no oculta el segundo cuelgue y la emergencia escala),
`BlockedReleasesTests`, `EngineHostSecureDesktopTests`, `SecureDesktopReleaseTests`, `InjectionGateTests` (acordes y
lotes parciales), `EngineKeyEffectsTests` y `EmergencyReleaserTests` (sin guardián), con `[Trait("Req", "REG-03")]` y
`SEG-006`. Cada prueba falla si se retira su corrección.

## Pros y contras de las opciones

### Comprobar la generación fuera de la valla

- Buena, porque no toma el *lock*.
- Mala, porque entre la comprobación y la escritura cabe una emergencia: el zombi puede escribir un latido más.

### `EVENT_SYSTEM_DESKTOPSWITCH` frente a un sondeo fijo

- Buena, porque reacciona al instante y no gasta nada mientras no hay cambios de escritorio.
- Neutral, porque se añaden comprobaciones acotadas por si el evento llega antes de que el escritorio se pueda abrir.

### Generación del motor activo en `InternalKeyEffects`

- Buena, porque cambia menos código.
- Mala, porque un segundo hilo seguiría inyectando junto al motor, y la generación del motor activo coincide casi
  siempre con la del *ledger*: la valla seguiría sin detener nada.

## Criterios de reapertura

- Si S9 en la CI muestra que el *hook* de cambio de escritorio no llega tras UAC o Ctrl+Alt+Supr.
- Si el usuario decide que Sentinel espere al escritorio de entrada con la sesión bloqueada (pendiente en ADR-0018):
  cambiaría el contrato de arranque de Sentinel.

## Más información

- Plano: [§3.2, regla 6](../architecture/blueprint.md#32-modelo-de-hilos), [§3.6](../architecture/blueprint.md#36-foregroundorchestrator-el-único-dueño-de-los-cambios-de-primer-plano),
  [§7.5](../architecture/blueprint.md#75-invariantes-de-seguridad-de-teclas), [§7.6](../architecture/blueprint.md#76-eventos-terminales-seg-007).
- Desviación: [D-22](../architecture/deviations.md#d-22--correcciones-del-motor-tras-verificar-m2).
- ADR relacionados: [ADR-0004](0004-motor-ledger-valla-y-sentinel.md), [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md).
