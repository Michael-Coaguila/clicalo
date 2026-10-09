---
status: Aceptado
date: 2026-10-05
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisión D4 del usuario del 2026-10-05 (catálogo §6.2); documentación oficial de Microsoft de Word, Excel, PowerPoint y Outlook en español
informed: colaboradores y agentes, mediante el catálogo, `data/catalogs/README.md` y el CHANGELOG
---

# ADR-0026 · Acciones comunes adaptativas: una tabla de datos por app e idioma de los programas

## Contexto y planteamiento del problema

Los atajos universales de General y de la biblioteca (Guardar Ctrl+S, Buscar Ctrl+F, Seleccionar todo Ctrl+A…)
hacen otra cosa en Office en español: Ctrl+S subraya, Ctrl+A abre un archivo y Ctrl+F no busca. Las variantes
por idioma de las plantillas (CAT-005) no lo resuelven, porque esos atajos viven en General y se usan en cualquier
app. ¿Cómo envía Clícalo la combinación correcta sin pedir a la persona que mantenga un atajo por app?

## Factores de decisión

- Decisión D4 del usuario: tabla versionada «acción → combinación estándar + excepciones por familia de apps e
  idioma de los programas»; cualquier app fuera de la tabla recibe la estándar; los atajos con una combinación
  propia se envían tal cual; la ficha muestra lo que se enviará; **sin** botón «No funcionó en esta app».
- Las combinaciones de Office en español se verifican con la documentación oficial de Microsoft y se citan.
- Simplicidad: nada nuevo en el documento del usuario; el formato persistido no cambia.

## Opciones consideradas

- Tabla de datos `data/catalogs/common-actions.json` con su esquema, leída al arrancar y resuelta en el motor.
- Variantes por app guardadas en cada atajo del documento.
- Botón «No funcionó en esta app» que aprende la combinación (descartado por el usuario).

## Resultado de la decisión

Se elige la **tabla de datos**:

- `data/catalogs/common-actions.json` (esquema `common-actions.schema.json`) lista las acciones (`save`, `find`,
  `selall`, `bold`, `italic`, `underline`, `open`, `new`), su combinación estándar y sus excepciones por familia
  (`word`, `excel`, `powerpoint`, `outlook`) e idioma de los programas, cada una con sus fuentes oficiales.
- Un atajo es una acción común si es un Pulsar cuya referencia de catálogo (DAT-004) viene de `seed` o `library`
  con el id de una acción y su combinación guardada sigue siendo una de la tabla. Si la persona la cambió, se envía
  tal cual.
- `CommonActionTable.ChordToSend` (dominio, puro) decide la combinación con el proceso en primer plano y el ajuste
  «idioma de los programas»; el motor la usa al enviar y la ficha al pintar, así que muestran lo mismo.
- Un archivo ausente o roto deja la tabla vacía: todo se envía como está guardado (LOG-006).

### Consecuencias

- Bueno: arregla Guardar, Buscar y Seleccionar todo de General en Office en español sin tocar el documento.
- Bueno: añadir una excepción es un cambio de datos revisable con su fuente.
- Malo: solo cubre lo que la tabla lista; una app mal resuelta se corrige editando el atajo en el panel (el usuario
  prefirió esto a un botón nuevo).

### Confirmación

`CommonActionTableTests`, `CommonActionTapTests` (dominio), `RuntimeCatalogFilesTests` (los datos reales) y
`CommonActionsCatalogTests` (fuentes, familias y coherencia con la semilla y la biblioteca), con
`[Trait("Req", "EJE-018")]`.

## Pros y contras de las opciones

### Tabla de datos

- Bueno: un único lugar, versionado y citado; el documento no cambia.
- Malo: la tabla viaja con el ejecutable y se actualiza con él.

### Variantes por app en cada atajo

- Malo: cambia el formato persistido y multiplica los datos de cada atajo.

## Criterios de reapertura

Si la persona usuaria pide adaptar atajos más allá de las acciones comunes, o si una app cambia sus combinaciones
según algo distinto del idioma de los programas.
