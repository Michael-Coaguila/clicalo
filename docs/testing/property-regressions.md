# Contraejemplos reducidos como regresiones

El plano pide en [§7.10](../architecture/blueprint.md#710-grabación-de-combinaciones-y-verificación) (verificación
del motor, punto 1) que **los contraejemplos reducidos se guarden como regresiones**: cuando una propiedad falla, el
caso que lo demuestra se reduce al más pequeño que sigue fallando y se queda en el repositorio, para que ese mismo caso
se compruebe en cada ejecución, aparte de los casos aleatorios.

## Dónde están

| Propiedad | Reducción | Regresiones |
|---|---|---|
| Modelo del motor, INV-1, INV-3 a INV-10 e INV-12 (`Clicalo.Domain.Tests`, `EngineModelTests`) | CsCheck: reduce el escenario (menos operaciones, valores menores) e imprime su semilla | `EngineModelRegressionTests`: semillas de CsCheck, cada una con lo que detectó |

«Muerte en cada paso» y «congelar y reanudar», con su reductor `Counterexamples`, se retiraron con el *ledger* y la
valla ([ADR-0023](../adr/0023-guardian-simple.md)).

## Qué hacer cuando una propiedad falla

1. Lee el caso reducido en el mensaje. CsCheck escribe `Set seed: "…"` seguido de las operaciones.
2. Añádelo a la teoría de regresiones de esa prueba, con una frase que diga qué defecto mostró (en inglés, como el
   resto del código).
3. Corrige el defecto. La regresión nueva y la propiedad pasan a verde.

## Cómo se obtuvieron las primeras

Ninguna propiedad había fallado en `m2/skeleton`, así que las primeras regresiones salen de mutaciones: se estropeó a
propósito el código en local, se ejecutó la propiedad, se guardó el caso reducido y se deshizo la mutación. Con la
mutación, la regresión falla; sin ella, pasa.

| Regresión | Mutación que la produjo |
|---|---|
| `3cnthrQVDtP1` (modelo) | `KeyboardLedger.Release` recorre `i >= 1` en lugar de `i >= 0`: olvida la primera tecla del titular |
| `4kPlB-gLWxb6` (modelo), `694-R_qiLQI7` (INV-12) | Una subida en modo *scancode* cambia la marca de tecla extendida |
| `fphVLHOKEBW4` (modelo) | `KeyboardLedger.ReleaseAll` no suelta los botones del mouse |

Una semilla de CsCheck representa el escenario que el generador (`EngineScenarios`) produce con ella. Si el generador
cambia, las semillas siguen pasando pero ya no reproducen el mismo caso: en ese cambio se vuelven a obtener con las
mismas mutaciones de esta tabla.
