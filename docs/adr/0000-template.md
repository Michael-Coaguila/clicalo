---
# Metadatos MADR 4. «status»: Propuesto | Aceptado | Rechazado | Obsoleto | Sustituido por ADR-NNNN.
status: Propuesto
date: AAAA-MM-DD
decision-makers: Michael Coaguila (mantenedor)
consulted: personas o documentos consultados
informed: a quién se comunica
---

# ADR-NNNN · Título corto que nombre el problema y la solución elegida

## Contexto y planteamiento del problema

Describe el problema en dos o tres párrafos: qué fuerza la decisión ahora, qué requisitos del catálogo
(`docs/requirements/catalog.md`) están en juego y qué parte del plano (`docs/architecture/blueprint.md`)
afecta. Si se puede, formúlalo como una pregunta.

## Factores de decisión

- Factor 1: por ejemplo, un requisito MUST con su ID (REG-01).
- Factor 2: por ejemplo, el coste de operación para una sola persona.
- …

## Opciones consideradas

- Opción A
- Opción B
- …

## Resultado de la decisión

Opción elegida: «Opción A», porque (motivo que responde a los factores de decisión).

### Consecuencias

- Buena, porque…
- Mala, porque…

### Confirmación

Cómo se comprueba que la decisión se cumple: pruebas (con `[Trait("Req", "<ID>")]` si verifican un
requisito), reglas de ArchUnit, analizadores `CLC*`, spikes con criterio de éxito o revisión.

## Pros y contras de las opciones

### Opción A

- Buena, porque…
- Neutral, porque…
- Mala, porque…

### Opción B

- Buena, porque…
- Mala, porque…

## Criterios de reapertura

Hechos medibles que obligarían a escribir un ADR nuevo que sustituya a este. Si no hay ninguno,
se indica «No se han fijado».

## Más información

- Secciones del plano y requisitos relacionados, con enlace relativo.
- Evidencia: solo enlaces que figuren en `docs/architecture/decision-record/` o fuentes primarias
  comprobadas. Nunca se inventa un enlace.
- Un ADR aceptado no se edita: se sustituye por otro nuevo que lo declare en su estado.
