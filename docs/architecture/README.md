# Documentación de arquitectura

Mapa de los documentos que describen cómo está construido Clícalo y por qué. Todo está en español; el
código, los identificadores y los comentarios, en inglés.

## Por dónde empezar

1. [overview.md](overview.md): visión general en formato arc42 ligero, con diagramas C4 (contexto,
   contenedores y componentes por capa). Es la puerta de entrada.
2. [blueprint.md](blueprint.md): el plano normativo completo. Manda sobre cualquier otro documento de
   arquitectura.
3. [Índice de ADR](../adr/README.md): las decisiones difíciles de revertir, con sus alternativas y su
   evidencia.
4. [deviations.md](deviations.md): dónde el repositorio se aparta del plano, con el motivo.

## Documentos

| Documento | Qué contiene | Carácter |
|---|---|---|
| [blueprint.md](blueprint.md) | Plano de arquitectura 1.1: stack, procesos, hilos, capas, dominio, persistencia, motor, presentación, servicios, calidad, distribución, seguridad, convenciones, hitos, riesgos y ADR iniciales | Normativo. Se cambia solo con un ADR cuando la decisión lo tiene |
| [overview.md](overview.md) | Resumen arc42 con diagramas C4 en Mermaid | Descriptivo; si discrepa del plano, manda el plano |
| [testing-strategy.md](testing-strategy.md) | Pirámide de pruebas, proyectos, trazabilidad `[Trait("Req", …)]` e instantáneas propias de TestKit | Reversible |
| [tooling.md](tooling.md) | Verbos de `cl`, Central Package Management, *lock files*, analizadores, CSharpier y CI | Reversible |
| [contracts.md](contracts.md) | Contratos de línea de órdenes entre ejecutables: arranque de Sentinel (protocolo 3), relanzamiento y códigos de salida | Contrato público: se cambia con un ADR |
| [deviations.md](deviations.md) | Desviaciones del plano con su motivo | Registro vivo |
| [../adr/](../adr/README.md) | ADR en formato MADR 4 | Inmutables una vez aceptados |

Páginas previstas por el plano ([§5](blueprint.md#5-estructura-del-repositorio-y-de-la-solución)) que se
escribirán cuando el código correspondiente exista: `threading.md`, `windowing.md`, `foreground.md`,
`engine.md` y `persistence.md`. [contracts.md](contracts.md) ya recoge los contratos entre `Clicalo.exe` y Sentinel;
la línea de órdenes de `Clicalo.exe`, `clicalo://` y la IPC se añadirán cuando existan.

## Registro de la decisión tecnológica

La carpeta [decision-record/](decision-record/) conserva, sin editar, los insumos con los que se tomó la
decisión de stack y se revisó el plano. Son la evidencia que citan los ADR.

| Archivo | Contenido |
|---|---|
| [techEvaluations.json](decision-record/techEvaluations.json) | Nueve evaluaciones de candidatas (WPF, WinUI 3, Avalonia, Tauri, Qt C++, PySide6, Flutter, Electron y nativo) con 12 criterios ponderados, requisitos duros, riesgos, mitigaciones y fuentes |
| [techVerification.json](decision-record/techVerification.json) | Tres verificaciones adversariales de la elección (foco y táctil, accesibilidad y sostenibilidad), con hallazgos sostenidos, refutados y no verificados, y las mitigaciones exigidas |
| [techDecision.json](decision-record/techDecision.json) | La decisión: ganadora, finalista, clasificación ponderada, justificación, stack, afirmaciones críticas, descartes y spikes obligatorios |
| [critique.json](decision-record/critique.json) | La crítica del plano 1.0: 23 huecos con severidad, problema, corrección y requisitos afectados; su resolución está en el registro de revisión del plano |

## Documentos relacionados

- Requisitos: [cómo leer el catálogo](../requirements/README.md) y el
  [catálogo](../requirements/catalog.md).
- Seguridad y privacidad: [modelo de amenazas](../security/threat-model.md) y
  [privacidad](../security/privacy.md).
- Guías: [preparar el entorno](../guides/dev-setup.md) y
  [programar con pantalla táctil y voz](../guides/voice-and-touch-workflow.md).
- Diseño: [qué es vinculante del paquete de diseño](../design/handoff/LEEME-VINCULANTE.md).
- Para agentes: [AGENTS.md](../../AGENTS.md).

## Cómo se cambia

- Una decisión con ADR se cambia con un ADR nuevo que la sustituya, y después se actualiza el plano en el
  mismo PR.
- Una decisión reversible se cambia con un PR normal que actualiza la página correspondiente de esta
  carpeta.
- Cuando el repositorio se aparta del plano sin cambiar una decisión con ADR, la desviación se registra en
  [deviations.md](deviations.md).
