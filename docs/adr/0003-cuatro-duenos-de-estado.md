---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: crítica del plano 1.0 (hueco «Estado sin dueño entre superficies»)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0003 · Cuatro dueños de estado inmutable y UI proyectada por funciones puras

## Contexto y planteamiento del problema

En Macro Quick Access el estado vivía en los controles: el perfil activo era el texto de un combo y el
modo era una cadena repartida en muchos `show()` y `hide()` (lección L-ARQ-2). El prototipo repetía el
patrón con un objeto de estado y una función de más de 400 líneas.

Clícalo tiene cuatro clases de estado con ciclos de vida, hilos y reglas de persistencia distintos:

- el **documento** del usuario, persistente y con deshacer (REG-07, DAT-006);
- la **sesión** del panel, transitoria, que no debe admitir estados inválidos (PAN-001, PAN-008);
- la **interacción** transversal a varias superficies: avisos que leen el panel, la Pestaña y el
  Centro de control (AVI-002, PES-014, CCM-003), captura de proceso (ATJ-008), Modo prueba (TAC-008),
  atenuado (GEN-009) y «Probar ahora»;
- el **motor**: teclas pulsadas y ejecución en curso.

La versión 1.0 del plano no daba dueño al estado transversal. ¿Quién es el dueño de cada estado, en qué
hilo, y cómo llega a la UI sin que ninguna regla de producto viva en un ViewModel?

## Factores de decisión

- Un único escritor por estado: sin carreras entre hilos (§3.2 del plano).
- Reglas de producto puras y probables sin UI (NFR-012): ninguna vive en un ViewModel.
- Todo cambio de datos se puede deshacer, y deshacer no debe perder usos registrados después (REG-07,
  FRE-004).
- Estado transversal con un dueño explícito, visible igual en todas las superficies.
- Proyección completa en 2 ms o menos con 50 perfiles y 2000 atajos (NFR-016).
- Posibilidad de separar la UI en dos dispatchers si S2 lo exige, sin reescribir Presentation.

## Opciones consideradas

- Cuatro dueños (Documento, Sesión, Interacción y Motor), inmutables, con proyecciones puras y deshacer
  por porciones
- Un único *store* tipo Redux
- MVVM clásico con entidades mutables e `INotifyPropertyChanged`
- Para el deshacer: *event sourcing*
- Para el deshacer: comandos con operación inversa

## Resultado de la decisión

Opción elegida: **«Cuatro dueños inmutables con proyecciones puras y deshacer por porciones»**, porque
cada estado tiene su ciclo de vida, su hilo y sus reglas de persistencia, y el estado transversal
necesita un dueño explícito.

| Estado | Dueño e hilo | Persistencia | Deshacer |
|---|---|---|---|
| `UserDocument` | `DocumentStore`: escritor único con un *lock* corto, lectura sin *lock* | Sí (documento y archivo de uso, ADR-0007) | Sí, por porciones |
| `PanelSession` | `SessionStore`: solo el rol Surfaces | No, salvo lo que es ajuste | No |
| `InteractionState` | `InteractionStore`: solo el rol Surfaces, que publica `InteractionSnapshot` inmutable hacia Workspace | No | No |
| `EngineState` | `EngineHost`: solo el hilo Engine (ADR-0004) | No; lo que debe sobrevivir a un fallo va al *ledger* | No |

- **Entre hilos solo cruzan objetos inmutables.** Workspace nunca escribe en `InteractionState`: envía
  `InteractionAction` al rol Surfaces, que la aplica y publica.
- **Proyecciones puras:** `PanelProjector.Project` combina documento, sesión, interacción, motor y
  métricas en un `PanelModel` inmutable, memoizado por revisión. El ViewModel solo aplica el modelo y
  reenvía intenciones.
- **Deshacer por porciones** con compartición estructural: 20 entradas, agrupación por clave y
  restauración solo de las porciones tocadas. Así, deshacer «borrar atajo» no borra los usos
  registrados después.
- **Autoguardado:** 500 ms de *debounce* y 2 s de latencia máxima para las porciones significativas.

### Consecuencias

- Buena, porque las reglas se prueban como funciones puras con tablas de transiciones.
- Buena, porque el estado transversal se ve igual en el panel, la Pestaña y el Centro de control.
- Buena, porque el deshacer es barato y correcto por construcción: no hay que escribir la inversa de
  cada comando.
- Buena, porque separar Workspace en su propio dispatcher (si S2 lo exige) no cambia Presentation: con
  un dispatcher la acción es una llamada directa; con dos, un `BeginInvoke` con el mismo protocolo.
- Mala, porque hay más tipos (instantáneas, acciones, reductores) que en un MVVM clásico.
- Mala, porque la memoización y el *diff* por clave hacia los ViewModels son responsabilidad propia.

### Confirmación

- ArchUnit: solo el rol Surfaces escribe en `SessionStore` e `InteractionStore`; en Debug,
  `ThreadGuard` comprueba la afinidad aunque los roles compartan dispatcher.
- Regla R7: una prueba recorre todos los `IDocumentCommand` y exige `UndoIntent.Record`, salvo las
  exenciones justificadas de `architecture/undo-exemptions.json`.
- `DocumentStore`: pruebas de porciones, agrupación, límite de 20 entradas, borrador sin rastro y
  `ConfirmationToken`.
- Tabla de transiciones de `PanelSessionReducer` y tabla de excepciones de `DimPolicy` al 100 % (M3).
- *Benchmark* de proyección en CI: 2 ms o menos con 50 perfiles y 2000 atajos.

## Pros y contras de las opciones

### Cuatro dueños inmutables con proyecciones puras

- Buena, porque cada estado tiene un único escritor y un único hilo.
- Buena, porque la UI es una función del estado, sin reglas en los ViewModels.
- Mala, porque exige disciplina en la frontera entre roles, que se compensa con ArchUnit y `ThreadGuard`.

### Un único *store* tipo Redux

- Buena, porque es un patrón conocido y centraliza el estado.
- Mala, porque mezcla ciclos de vida distintos: el motor vive en su propio hilo con prioridad, y la
  persistencia solo afecta al documento.
- Mala, porque un único escritor para todo serializa el motor detrás de la UI.

### MVVM clásico con entidades mutables

- Buena, porque es lo habitual en WPF.
- Mala, porque reproduce la lección L-ARQ-2: las reglas acaban repartidas por los ViewModels.
- Mala, porque el estado mutable compartido entre hilos exige *locks* por todas partes.

### Deshacer con *event sourcing*

- Buena, porque conserva el historial completo.
- Mala, porque es caro de operar y de migrar para un documento pequeño que se carga entero.

### Deshacer con comandos con operación inversa

- Buena, porque cada comando sabe revertirse.
- Mala, porque exige escribir y probar a mano la inversa de cada comando, y un error ahí pierde datos.

## Criterios de reapertura

No se han fijado. Separar los roles Surfaces y Workspace en dos dispatchers, si S2 lo exige, está
previsto por esta decisión y no la reabre.

## Más información

- Plano: [§1.2 (D6 y D7)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§3.2](../architecture/blueprint.md#32-modelo-de-hilos),
  [§6.4](../architecture/blueprint.md#64-estado-cuatro-dueños-deshacer-y-autoguardado),
  [§8.2](../architecture/blueprint.md#82-flujo-mvvm).
- Catálogo: REG-07 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  NFR-012 y NFR-016 en [§3](../requirements/catalog.md#3-requisitos-no-funcionales).
- Registro: [critique.json](../architecture/decision-record/critique.json), hueco «Estado sin dueño
  entre superficies».
- ADR relacionados: [ADR-0002](0002-monolito-modular-hexagonal.md),
  [ADR-0004](0004-motor-ledger-valla-y-sentinel.md), [ADR-0007](0007-documento-json-versionado.md).
