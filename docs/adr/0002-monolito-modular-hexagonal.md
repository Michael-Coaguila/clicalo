---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuestas de arquitectura de dominio y modularidad, fiabilidad y seguridad, y equipo y DX (síntesis en el plano)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0002 · Monolito modular hexagonal con 8 ensamblados

## Contexto y planteamiento del problema

Macro Quick Access, la versión 1, era un monolito sin capas: una ventana de unas 1050 líneas mezclaba
interfaz, persistencia, inyección, sondeo de procesos y paginación. De ahí salieron casi todas sus
incoherencias (lección L-ARQ-1 del catálogo). Clícalo exige que cada regla de negocio exista una sola
vez, sea determinista y no dependa de la interfaz ni del sistema operativo (NFR-012), y que añadir un
ajuste, una sección, un idioma o una plantilla no toque lo demás (NFR-015).

A la vez, el panel tiene que aparecer en menos de 1 s desde el arranque y responder al toque en menos
de 50 ms (NFR-001). Cada ensamblado añade tiempo al arranque en frío, y cualquier IPC entre motor y UI
se mete en el camino crítico. ¿Cómo se estructura el código para que escale a un equipo sin pagar ese
coste?

## Factores de decisión

- Reglas de negocio implementadas una sola vez y probables sin UI ni Windows (NFR-012, NFR-013).
- Fronteras que se comprueben de forma automática: un agente o una persona reciben el mismo error.
- Arranque en frío en menos de 1 s y toque → `SendInput` p95 ≤ 50 ms (NFR-001).
- Capa de UI sustituible (ADR-0001).
- Coste de operación a la medida de una persona, con la puerta abierta a un equipo.
- Navegación del código por voz: «¿quién reacciona a X?» debe responderse con «Ir a definición».

## Opciones consideradas

- Monolito modular hexagonal: capas como ensamblados (8 en el proceso principal) y capacidades como
  espacios de nombres
- Un ensamblado por módulo de capacidad
- Procesos separados para el motor y la UI (la separación que recomendaba el paquete de diseño)
- Clean Architecture con CQRS y MediatR

## Resultado de la decisión

Opción elegida: **«Monolito modular hexagonal con 8 ensamblados»**, porque impone las fronteras con
pruebas y analizadores sin pagar ensamblados de más en el arranque ni IPC en el camino de menos de
50 ms.

Los 8 ensamblados del proceso principal son `Clicalo.Domain`, `Clicalo.Application`,
`Clicalo.Presentation`, `Clicalo.UI.Wpf`, `Clicalo.Platform.Core`, `Clicalo.Platform.Windows`,
`Clicalo.Infrastructure` y `Clicalo.App`. Fuera de él están `Clicalo.Sentinel` y `Clicalo.Launcher`
(Native AOT) y los proyectos de compilación. Las reglas de dependencia están en la
[tabla de §4.2 del plano](../architecture/blueprint.md#42-proyectos-y-reglas-de-dependencia) y los
módulos de capacidad en [§4.3](../architecture/blueprint.md#43-módulos-por-capacidad).

Seis mecanismos, todos bloqueantes en la CI, hacen cumplir las reglas
([§4.4](../architecture/blueprint.md#44-cómo-se-hacen-cumplir-las-reglas)):

1. Lista blanca de referencias (`architecture/allowed-dependencies.json` y el target
   `ClicaloVerifyReferences`), que falla antes de compilar.
2. ArchUnitNET en `tests/Clicalo.Architecture.Tests`, incluida la matriz de módulos
   (`architecture/domain-modules.json`, acíclica).
3. APIs prohibidas por capa (`BannedSymbols.<Capa>.txt`).
4. Analizadores propios `CLC*`.
5. Prueba de facetas de `ActionKind`: cada tipo de acción tiene planificador, validador, mapeador,
   editor, vista y textos en ES y EN.
6. Pruebas de reglas de producto R4, R5 y R7.

**Criterios de extracción** (forman parte de esta decisión): un módulo pasa a su propio ensamblado
cuando supera unas 15 000 líneas, cuando la compilación incremental pasa de 60 s o cuando tiene un
equipo propio. Los espacios de nombres ya tienen su forma final (`Clicalo.Domain.KeySafety`…), así que
extraer no cambia dependencias.

### Consecuencias

- Buena, porque el dominio es puro (`net10.0`, solo BCL) y se prueba en milisegundos.
- Buena, porque las fronteras las imponen máquinas, no la disciplina.
- Buena, porque no hay IPC ni mediador en el camino del toque a la acción.
- Buena, porque `Core.slnf` compila solo Domain, Application, Presentation y sus pruebas (`cl fast`).
- Mala, porque la frontera entre módulos dentro de un mismo ensamblado no la ve el compilador: la
  vigilan ArchUnit (ningún módulo usa el `.Internal` de otro) y la matriz de módulos.
- Mala, porque ensamblados grandes compilan más despacio que muchos pequeños; los criterios de
  extracción ponen el límite.

### Confirmación

- Criterio de salida de M0: ArchUnit falla ante una referencia prohibida (prueba negativa).
- `ModuleMatrixTests` compara el código con `architecture/domain-modules.json`; una arista no
  declarada hace fallar la CI.
- `ClicaloVerifyReferences` rechaza cualquier `ProjectReference` o `PackageReference` fuera de la
  lista blanca.

## Pros y contras de las opciones

### Monolito modular hexagonal con 8 ensamblados

- Buena, porque equilibra el coste de arranque con fronteras verificables.
- Buena, porque la UI queda en una sola capa sustituible.
- Neutral, porque exige mantener al día la lista blanca y la matriz de módulos (son datos versionados).
- Mala, porque parte de las fronteras se comprueban con pruebas y no con el compilador.

### Un ensamblado por módulo de capacidad

- Buena, porque el compilador impondría cada frontera.
- Mala, porque cada ensamblado añade tiempo al arranque en frío, que ya es el punto débil de WPF.
- Mala, porque multiplica proyectos, referencias y *lock files* para una sola persona.

### Procesos separados para motor y UI

- Buena, porque aísla fallos entre motor e interfaz.
- Mala, porque mete IPC en el camino de menos de 50 ms.
- Mala, porque el aislamiento que importa (soltar teclas si el proceso muere) ya lo da un guardián
  diminuto con el *ledger* compartido (ADR-0004), sin partir el producto.

### Clean Architecture con CQRS y MediatR

- Buena, porque es un patrón conocido.
- Mala, porque el mediador y la reflexión hacen opaca la pregunta «¿quién reacciona a X?», esencial al
  programar por voz; se usan enrutadores explícitos (D19).
- Mala, porque MediatR tiene licencia comercial.

## Criterios de reapertura

No se han fijado. Extraer un módulo a su propio ensamblado según los criterios anteriores no reabre
esta decisión. Partir el proceso principal (por ejemplo, separar motor y UI en procesos) sí exige un
ADR nuevo.

## Más información

- Plano: [§1.2 (D2 y D19)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§1.3, fila «Número de ensamblados»](../architecture/blueprint.md#13-contradicciones-entre-las-propuestas-y-cómo-se-resolvieron),
  [§4](../architecture/blueprint.md#4-arquitectura-lógica).
- Catálogo: NFR-012, NFR-013 y NFR-015 en
  [§3](../requirements/catalog.md#3-requisitos-no-funcionales); lección L-ARQ-1 en
  [§8](../requirements/catalog.md#8-lecciones-de-la-app-antigua-y-del-prototipo).
- Registro: [techDecision.json](../architecture/decision-record/techDecision.json) (apartado de
  arquitectura de la justificación).
- ADR relacionados: [ADR-0001](0001-framework-ui-wpf.md), [ADR-0003](0003-cuatro-duenos-de-estado.md).
