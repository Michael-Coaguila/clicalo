---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuesta de fiabilidad y seguridad; modelo de amenazas (§12 del plano)
informed: colaboradores y agentes, mediante AGENTS.md y CONTRIBUTING.md
---

# ADR-0017 · Sin *plugins* de código de terceros: la extensibilidad es solo por datos validados

## Contexto y planteamiento del problema

El proceso de Clícalo instala *hooks* temporales, inyecta entrada en otras apps y puede ejecutarse
elevado o, en el futuro, con uiAccess. Cualquier código que se cargue dentro de él hereda esas
capacidades (activo A1 del modelo de amenazas). En Macro Quick Access, el tipo App ejecutaba cualquier
línea de comandos e importar reemplazaba sin vista previa: un perfil malicioso ejecutaba código con un
toque (lección L-SEG-1).

Aun así, el producto necesita crecer con contribuciones: plantillas por app (CAT-006), idiomas nuevos sin
tocar código (IDI-006) y catálogos. Todo contenido importado es no confiable (LOG-006). ¿Cómo se extiende
Clícalo sin abrir su proceso a código de terceros?

## Factores de decisión

- Ningún código ajeno dentro de un proceso con inyección y posible elevación.
- Contribuir una plantilla o un idioma no debe exigir escribir C#.
- Todo contenido externo se valida contra un esquema, con límites de tamaño, y nunca se ejecuta al
  importar (LOG-006, LOG-008).
- Cadena de suministro única y auditable.

## Opciones consideradas

- Extensibilidad solo por datos validados (plantillas, idiomas y catálogos)
- Carga dinámica de ensamblados (*plugins*)
- Un motor de *scripting* integrado

## Resultado de la decisión

Opción elegida: **«Extensibilidad solo por datos validados»**, porque cubre las necesidades reales
(plantillas, idiomas, catálogos) sin introducir código de terceros en el proceso.

- Las plantillas son archivos de datos (`data/content/templates/*.json`), uno por plantilla, con versión,
  autoría, idiomas revisados, procesos y variantes. Se validan contra el esquema al compilar y al cargar, y
  su instalación pasa por una vista previa editable con las acciones de riesgo desmarcadas.
- Un idioma nuevo es un archivo más una entrada en `locales.json` (ADR-0011).
- Si algún día hace falta código de terceros, irá **fuera de proceso y sin privilegios**, con su propio
  ADR y su modelo de amenazas.

### Consecuencias

- Buena, porque ningún código ajeno hereda la capacidad de inyectar ni la elevación.
- Buena, porque contribuir una plantilla o una traducción es editar JSON y abrir un PR.
- Mala, porque no hay acciones programables por el usuario más allá de los tipos de acción del producto;
  un tipo nuevo exige cambiar el núcleo (y superar la prueba de facetas de `ActionKind`).

### Confirmación

- Las plantillas y los catálogos se validan contra sus esquemas en `Clicalo.Data.Tests` y al cargar.
- La importación siempre pasa por vista previa con límites de tamaño, profundidad y recuento (amenaza T1).
- La revisión de código rechaza cualquier carga dinámica de ensamblados o intérprete de *scripts*.

## Pros y contras de las opciones

### Solo datos validados

- Buena, porque mantiene el proceso cerrado a código ajeno.
- Buena, porque los datos se pueden revisar, validar y comparar en un PR.
- Mala, porque limita lo que la comunidad puede extender.

### Carga dinámica de ensamblados

- Buena, porque permitiría acciones nuevas sin tocar el núcleo.
- Mala, porque un *plugin* ejecutaría con las capacidades de inyección y, si Clícalo está elevado, con
  privilegios de administrador.

### Motor de *scripting*

- Buena, porque daría flexibilidad a usuarios avanzados.
- Mala, porque reintroduce el riesgo de la lección L-SEG-1: un perfil compartido podría ejecutar código.

## Criterios de reapertura

Admitir código de terceros, aunque sea fuera de proceso, exige un ADR nuevo con su modelo de amenazas.

## Más información

- Plano: [§9.1](../architecture/blueprint.md#91-plantillas),
  [§12.1](../architecture/blueprint.md#121-activos-y-límites-de-confianza),
  [§12.2 (T1)](../architecture/blueprint.md#122-amenazas-y-controles).
- Catálogo: LOG-006 y LOG-008 en [§2.31](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad);
  CAT-006 en [§2.35](../requirements/catalog.md#235-cat--catálogos-y-contenido-inicial); IDI-006 en
  [§2.33](../requirements/catalog.md#233-idi--idioma); lección L-SEG-1 en
  [§8](../requirements/catalog.md#8-lecciones-de-la-app-antigua-y-del-prototipo).
- Seguridad: [modelo de amenazas](../security/threat-model.md).
- ADR relacionados: [ADR-0010](0010-ipc-minima.md), [ADR-0011](0011-formato-i18n.md).
