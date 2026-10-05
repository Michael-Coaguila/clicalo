---
status: Aceptado
date: 2026-10-05
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisión del usuario del 2026-10-05 (simplicidad primero, pruebas deterministas en `cl check`); ADR-0005; plano §3.5 y §3.6; spike S1 (hallazgos 6 a 15)
informed: colaboradores y agentes, mediante el plano, S1.md y el CHANGELOG
---

# ADR-0024 · Vigilante de foco simple: una restauración por activación sin concesión

## Contexto y planteamiento del problema

ADR-0005 fija cómo se cumple REG-01 en producción: `ActivationGuard` ve los mensajes de activación de las
superficies y, si no hay concesión, el `ForegroundOrchestrator` devuelve el primer plano. Durante S1 y la CI de
M2, los fallos de una prueba de escritorio con carreras de milisegundos llevaron a `ActivationGuard` una máquina de
estados (violaciones abiertas, ráfagas, mensajes tardíos y un juicio aplazado con `ActivationRecheck`) y al
orquestador una confirmación de la restauración por el monitor. Eran unas 450 líneas difíciles de razonar, y
varios de los hallazgos de S1 los causaba esa misma máquina (una violación abierta que se tragaba la activación
forzada siguiente). ¿Se puede cumplir REG-01 con una regla simple y probarla sin escritorio?

## Factores de decisión

- REG-01 (el panel nunca quita el foco) no se rebaja: toda activación sin concesión se detecta y se revierte.
- Simplicidad primero (decisión del usuario del 2026-10-05).
- Pruebas deterministas en `cl check`; las de escritorio, cada noche y antes de publicar.
- No tocar lo que funciona: `NonActivatingWindow`, los analizadores, las concesiones y la escalera de derechos.

## Opciones consideradas

- Regla simple: cada activación sin concesión pide una restauración, coalescida mientras haya una en cola
- Mantener la máquina de estados de M1 y seguir ajustándola con la CI

## Resultado de la decisión

Opción elegida: **la regla simple**, porque cumple REG-01 con menos piezas y se puede probar sin escritorio.

- Si una superficie recibe `WM_ACTIVATE` activo, `WM_NCACTIVATE(TRUE)` o `WM_ACTIVATEAPP(TRUE)` (este último solo
  si la superficie es la que está en primer plano) y no hay una concesión activa para ella, `ActivationGuard`
  vuelve a aplicar `WS_EX_NOACTIVATE`, suma uno a `reg01.violations`, deja constancia y pide **una**
  restauración. La petición se encola en el *dispatcher*, fuera del procedimiento de ventana; las activaciones
  que llegan mientras sigue en cola se suman a ella. Por eso la restauración siempre llega después de todas las
  activaciones que cubre, y ya no hay violaciones abiertas que se traguen la siguiente.
- El orquestador restaura desde el grupo de hilos al último primer plano externo verificado (o al destino de la
  concesión activa): `SetForegroundWindow` verificado con `GetForegroundWindow` durante
  `Timings.Foreground.RestoreRetryDelay` (la activación entre hilos es asíncrona, S4), un reintento y, si los dos
  fallan, `FlashWindowEx`. Solo se conserva lo necesario para no deshacer una elección del usuario: no se
  restaura si el monitor verificó otra app externa después del aviso y ninguna superficie sigue delante, ni se
  reintenta si el usuario cambió de app durante el primer intento.
- Se retiran `Timings.Windowing.ActivationRecheck`, la confirmación de la restauración por el informe del monitor
  e `IForegroundOrchestrator.RestoreAfterViolationAsync`, que nadie usaba.

### Consecuencias

- Buena, porque `ActivationGuard` pasa de unas 440 líneas a unas 190 y la regla se prueba en `cl check` sin
  escritorio (`ActivationGuardTests`, `ActivationArbiterTests`).
- Buena, porque una activación forzada que llega antes de que el hilo de UI vuelva a su *dispatcher* queda
  cubierta por la restauración en cola en vez de perderse.
- Mala, porque un mensaje tardío de una activación ya revertida puede contar una violación de más y pedir una
  restauración inofensiva (devuelve el primer plano a la app que ya lo tiene). El contador puede sobrestimar,
  nunca subestimar.
- Mala, porque la petición espera a que el hilo de UI vuelva a su *dispatcher*; S1 midió hasta 25 ms, dentro de
  `Timings.Windowing.ViolationRestoreBudget` (200 ms).

### Confirmación

- Sin escritorio, en `cl check`, con `[Trait("Req", "REG-01")]`: activación sin concesión → una restauración
  pedida; con concesión → ninguna; varias activaciones seguidas → una sola petición en cola; el usuario cambió de
  app → no se restaura.
- De escritorio, cada noche y obligatorias antes de publicar: `ActivationGuardNegativeTests` y
  `OrchestratedRestoreTests` (activación forzada detectada y revertida en ≤ 200 ms, 20 ciclos) y
  `NonActivationTests` (tocar nunca cambia el primer plano).

## Pros y contras de las opciones

### Regla simple

- Buena, porque cada paso se entiende leyendo un método y se prueba sin escritorio.
- Mala, porque el contador puede sobrestimar con mensajes tardíos.

### Máquina de estados de M1

- Buena, porque contaba exactamente una violación por activación forzada en la CI.
- Mala, porque cada carrera nueva añadía un estado, y algunas de esas reglas rompieron REG-01 (S1, hallazgos 11
  y 13).

## Criterios de reapertura

- Una activación forzada que la ejecución nocturna no detecta o no revierte en ≤ 200 ms por un defecto de la regla.
- Un `reg01.violations` distinto de cero en uso normal causado por mensajes tardíos y no por activaciones reales.
