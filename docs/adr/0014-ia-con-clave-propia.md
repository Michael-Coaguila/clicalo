---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: crítica del plano 1.0 (huecos «Privacidad de la IA», «Sobreingeniería» y «Acoplamiento del proxy con Domain»)
informed: colaboradores y agentes, mediante AGENTS.md y PRIVACY.md
---

# ADR-0014 · IA solo con clave propia en la 2.0; proxy de cuota diseñado y diferido

## Contexto y planteamiento del problema

Clícalo puede generar plantillas de atajos para una app con IA (PLA-001 a PLA-017). Es opcional: las
plantillas locales funcionan siempre sin conexión. El contrato es estricto (PLA-008, MUST): solo se envían
el nombre de la app, la distribución de teclado, el idioma de los programas y el idioma de la interfaz; la
respuesta se valida contra un esquema y solo puede proponer combinaciones de teclas. El paquete de diseño
preveía 5 generaciones gratis al día por instalación (PLA-003, SHOULD), pero dejaba el proveedor y el coste
a decisión del autor, y la pregunta abierta PQ-48 concluye que sin un servicio intermedio propio no hay
cuota gratuita.

La versión 1.0 del plano proponía un proxy desde el día 1 con un identificador de instalación: un quinto
dato que contradice PLA-008, más un servicio con coste fijo que operar para una sola persona. ¿Cómo se
ofrece la IA en la 2.0 sin romper PLA-008 y sin coste de operación?

## Factores de decisión

- Exactamente los 4 datos de PLA-008; ningún otro dato sale del equipo (LOG-002).
- Consentimiento explícito la primera vez (PLA-004).
- Proveedor intercambiable sin tocar la interfaz.
- Coste de operación a la medida de una persona (idea 6 del plano).
- La respuesta de la IA es contenido no confiable (LOG-006).

## Opciones consideradas

- Puerto `ITemplateGenerator` sobre Microsoft.Extensions.AI con la clave propia del usuario, y el proxy de
  cuota diseñado con condiciones pero diferido
- Proxy de cuota desde el día 1
- Un proxy que reenvía los *prompts* del cliente
- Cuota por identificador de instalación

## Resultado de la decisión

Opción elegida: **«IA con clave propia en la 2.0 y proxy diferido con el diseño fijado»**, porque cumple
PLA-008 al pie de la letra y no añade coste de operación mientras ningún requisito MUST lo pida.

- **Puerto:** `ITemplateGenerator.GenerateAsync(TemplateRequest, CancellationToken)` en
  `Application.Ports`, con 15 s de tiempo máximo. `TemplateRequest` lleva **exactamente** `AppName`,
  `Layout`, `ProgramsLang` y `UiLang`.
- **Adaptadores** en `Infrastructure.Ai`: `ByoKeyTemplateGenerator` usa `IChatClient` de
  Microsoft.Extensions.AI con el adaptador del proveedor elegido por configuración y la clave del
  Administrador de credenciales (ADR-0008); el *prompt* se construye en el cliente a partir de una
  plantilla versionada. `CannedTemplateGenerator` simula en las pruebas los 6 tipos de fallo.
- **Validación en dos pasos** de toda respuesta: estructural contra
  `data/schemas/ai-template.v1.schema.json` y semántica con `TemplateSchema` de Domain (solo Pulsar,
  teclas del catálogo, nombres de 32 caracteres como máximo sin caracteres de control ni bidi,
  combinaciones bloqueadas filtradas y peligrosas marcadas en la vista previa).
- **Con clave propia no hay cuota.** La cuota gratuita (PLA-003) llega con el proxy (propuesta P3).
- **Condiciones del proxy diferido**, que debe cumplir cuando se construya:
  - contrato `POST /v1/templates` con `{ contractVersion, app, layout, programsLang, uiLang }`, **sin
    identificador de instalación** ni ningún otro dato persistente o enlazable;
  - validador compartido `Clicalo.Contracts.Templates`, sin dependencia de Domain;
  - techo antiabuso con `HMAC(IP, sal diaria)` y presupuesto global diario con corte automático;
  - cuota visible contada en el cliente, que se reinicia a medianoche local sin enviar la zona horaria;
  - *prompt* construido en el servidor; la cuota se descuenta solo si la respuesta es válida;
  - retención solo de contadores agregados, nunca `app` junto a la IP;
  - interruptor `disabledFeatures: ["ai.proxy"]` en el manifiesto firmado (ADR-0013).

### Consecuencias

- Buena, porque ningún dato sale del equipo salvo los 4 de PLA-008, y solo con consentimiento.
- Buena, porque no hay servicio que operar ni coste fijo.
- Buena, porque cambiar de proveedor es cambiar de adaptador.
- Mala, porque PLA-003 (SHOULD) no se cumple en la 2.0: mientras no se ratifique P3, los flujos con cuota
  no se muestran y el resto de PLA sí.
- Mala, porque quien quiera IA necesita su propia clave de un proveedor, con su coste.

### Confirmación

- Prueba de Infrastructure que intercepta la petición HTTP del adaptador y exige que el cuerpo contenga
  solo esos 4 valores más la plantilla fija del *prompt* (criterio de M5).
- Pruebas con `CannedTemplateGenerator` para los 6 tipos de fallo y el orden de estados de PLA-005.

## Pros y contras de las opciones

### Clave propia y proxy diferido

- Buena, porque respeta PLA-008 y no tiene coste de operación.
- Mala, porque aplaza la cuota gratuita hasta que el usuario decida.

### Proxy desde el día 1

- Buena, porque daría la cuota gratuita de PLA-003.
- Mala, porque añade un servicio con coste fijo y operación sin requisito MUST que lo pida.

### Proxy que reenvía *prompts*

- Buena, porque sería el proxy más simple.
- Mala, porque convierte la cuota gratuita en un LLM genérico abusable (amenaza T13).

### Cuota por identificador de instalación

- Buena, porque facilita contar la cuota en el servidor.
- Mala, porque es un quinto dato persistente y enlazable que contradice PLA-008.

## Criterios de reapertura

Activar el proxy exige que el usuario ratifique la propuesta P3 y un ADR nuevo con el proveedor y el
coste, que debe respetar las condiciones fijadas aquí. Cambiar cualquiera de esas condiciones (por
ejemplo, añadir un identificador) exige sustituir este ADR.

## Más información

- Plano: [§1.2 (D16)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§1.4 (P3)](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario),
  [§4.2 (contratos de red)](../architecture/blueprint.md#42-proyectos-y-reglas-de-dependencia),
  [§9.2](../architecture/blueprint.md#92-ia).
- Catálogo: PLA-001 a PLA-017 en [§2.22](../requirements/catalog.md#222-pla--plantillas-e-ia); LOG-002 y
  LOG-006 en [§2.31](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad); PQ-48 en
  [preguntas abiertas](../requirements/catalog.md#6-preguntas-abiertas).
- Privacidad: [PRIVACY.md](../../PRIVACY.md) y [docs/security/privacy.md](../security/privacy.md).
- Registro: [critique.json](../architecture/decision-record/critique.json).
- Evidencia: [Microsoft.Extensions.AI en NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI).
- ADR relacionados: [ADR-0008](0008-secretos-dpapi-y-administrador-de-credenciales.md),
  [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md).
