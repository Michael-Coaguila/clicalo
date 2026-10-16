---
status: Propuesto
date: 2026-10-05
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: permiso del usuario del 2026-10-05 para descargar las fuentes del prototipo de sus repositorios oficiales; TEM-005; docs/07 «Tipografía»; ADR-0015
informed: colaboradores y agentes, mediante la guía de tokens de diseño
---

# ADR-0025 · Fuentes de terceros empaquetadas como recursos de `Clicalo.UI.Wpf`

## Contexto y planteamiento del problema

TEM-005 exige Atkinson Hyperlegible 400/700, JetBrains Mono 500 y Material Symbols Rounded con el eje FILL,
«todo empaquetado, sin red, con versión fijada y licencias incluidas». El prototipo las carga de Google Fonts, y
Clícalo no puede depender de la red ni de las fuentes instaladas. Empaquetarlas añade a un producto MIT
(ADR-0015) archivos con otras licencias (SIL OFL 1.1 y Apache 2.0), que se distribuyen con cada versión, y toca
`Clicalo.UI.Wpf.csproj`, ruta sensible. ¿Cómo se incluyen sin comprometer la licencia ni la reproducibilidad?

## Factores de decisión

- TEM-005 (MUST): sin red, versión fijada y licencias incluidas; todo nombre de icono de los datos existe.
- WPF no dibuja bien las fuentes variables: hacen falta fuentes estáticas.
- La licencia del producto sigue siendo MIT (ADR-0015).
- Simplicidad: un script que se ejecuta a mano y una salida versionada; nada nuevo en `cl check`.

## Opciones consideradas

- Fuentes estáticas como recursos WPF del ensamblado, generadas por un script fijado por hash.
- Fuentes como archivos sueltos junto al ejecutable.
- Seguir cargándolas de la red, o usar las fuentes del sistema (Segoe UI, Segoe Fluent Icons).

## Resultado de la decisión

Opción elegida: «recursos WPF del ensamblado», porque cumple TEM-005 sin red ni instalación, no se puede
desincronizar con el código y no añade pasos de despliegue.

- `tools/fonts/sources.json` fija repositorio, *commit* y SHA-256 de cada archivo; `tools/fonts/build_fonts.py`
  (fontTools fijado, entorno virtual fuera del repositorio) escribe `assets/fonts` de forma determinista.
- Atkinson Hyperlegible y JetBrains Mono se copian **sin modificar** (estáticas oficiales, OFL 1.1 sin nombres
  reservados). Material Symbols Rounded (Apache 2.0) se instancia (FILL 0 y 1, wght 400, GRAD 0, opsz 24), se
  recorta a los iconos que usan los datos y el prototipo y pierde las ligaduras; el cambio queda anotado en la
  descripción de cada fuente, como pide la sección 4 de Apache 2.0.
- Las licencias viajan junto a las fuentes en `assets/fonts` y deben acompañar a los paquetes que se publiquen.

### Consecuencias

- Buena, porque la app se ve igual sin red y en cualquier equipo, con la tipografía pensada para baja visión.
- Buena, porque cambiar de versión es editar `sources.json`, ejecutar el script y revisar el diff.
- Mala, porque el ensamblado crece unos 520 KB.
- Mala, porque un icono nuevo que no aparezca en los datos ni en el prototipo exige volver a ejecutar el script;
  hasta entonces se dibuja el de respaldo.

### Confirmación

`BundledFontTests` (`[Trait("Req", "TEM-005")]`): cada familia se resuelve a su archivo empaquetado, todo nombre
de icono de los datos está incluido y tiene glifo en las dos variantes, y hay un icono de respaldo. Revisión: el
paquete de publicación (M5) incluye las licencias de `assets/fonts`.

### Actualización del 2026-10-16: decisión D14 del usuario, la colección completa

El usuario decidió (D14, [catálogo §6.2](../requirements/catalog.md#62-decisiones-del-usuario)) que el selector
ofrezca **toda** la colección de Material Symbols Rounded, con categorías y búsqueda, y que los ~600 iconos más útiles
lleven palabras en español. Cambia el recorte; el resto de la decisión (instancias estáticas, sin ligaduras, fuentes
fijadas por hash, licencias junto a las fuentes) se mantiene.

- **Fuente completa.** `build_fonts.py` ya no recorta a los iconos de los datos: las dos instancias (FILL 0 y 1,
  wght 400, GRAD 0, opsz 24) llevan los **4001 glifos** del archivo de puntos de código de
  `google/material-design-icons` en el *commit* `49d4db35df873165d6bd6ba09b063c7dafbac2f4` (2026-10-09), y
  `codepoints.json` lleva sus **4301 nombres**, alias incluidos, así que cualquier nombre de un perfil importado se
  dibuja. Cada fuente pesa **1,0 MB** (1 024 156 y 1 062 368 bytes) en lugar de unos 70 KB: el ensamblado crece unos
  2 MB en total. No hace falta reducirlas (el umbral acordado era unos 3 MB por archivo); si algún día hiciera falta,
  el primer paso sería quitar los 76 glifos retirados que el selector ya no ofrece, sin perder ningún icono útil.
- **Metadatos oficiales.** Las categorías y las palabras clave en inglés salen de los metadatos de Material Symbols
  que publica Google (`fonts.google.com/metadata/icons`, la misma fuente que lee el propio script
  `update/update_symbols.py` del repositorio oficial; el repositorio no los incluye). `sources.json` los fija por
  SHA-256; como el servicio no tiene versiones, cuando el hash deje de coincidir se descargan otra vez, se revisa el
  diff de `icons.json` y se fija el hash nuevo. De ellos solo se copian el nombre de la categoría y palabras sueltas.
- **`data/catalogs/icons.json`** pasa a tener dos partes (D14). A mano: `catalogVersion` (2), los iconos por defecto,
  `featured`, `profileFeatured` y los **744 iconos curados**, primero y en su orden (los 128 del prototipo y 616
  nuevos de edición, archivos, comunicación, multimedia, navegación, dispositivos, apps, accesibilidad, oficina y
  programación), con sus palabras en español y en inglés. Lo escribe el script: las **17 categorías** con su nombre
  en los dos idiomas, la categoría y las palabras clave (`keywords`) de cada icono, y el resto de la colección, del más
  popular al menos. Un glifo se ofrece una sola vez (**3925 iconos**); los 76 glifos retirados que la fuente conserva
  por compatibilidad no se ofrecen, salvo `brightness_high` y `brightness_low`, que usan los datos. Una palabra clave
  que comparte más del 3 % de los iconos («symbol», «data») no los distingue y se omite: el archivo pesa 1,1 MB y se
  lee una vez, fuera del hilo de la interfaz.
- **Selector.** Hasta que se rediseñe por categorías, la rejilla construye como mucho `IconPickerMaxShown` (128, el
  tamaño de la biblioteca anterior) iconos: los primeros de la biblioteca o de la búsqueda. La búsqueda recorre los
  3925 iconos y las sugerencias (EDI-005) pliegan las palabras una sola vez al leer el catálogo.

Consecuencias nuevas:

- Buena, porque ningún icono de Material Symbols se dibuja con el de respaldo y un icono nuevo de los datos ya no
  exige volver a ejecutar el script (la consecuencia mala del recorte desaparece).
- Buena, porque la persona encuentra iconos por categoría y por palabras en los dos idiomas.
- Mala, porque el ensamblado y los datos crecen unos 3 MB, y la búsqueda en inglés devuelve más resultados que la
  española, que solo cubre los iconos curados.

Confirmación nueva: `ContentIntegrityTests.The_library_offers_every_icon_of_the_font_once_with_its_category` y
`Icons_are_unique_and_searchable_in_both_languages` (Clicalo.Data.Tests) y
`EditorCatalogFilesTests.The_shipped_library_has_every_icon_by_category_and_finds_them_in_both_languages`
(Clicalo.Infrastructure.Tests). Criterio de reapertura nuevo: que Google retire los metadatos o los publique con
condiciones que no permitan copiar sus categorías y palabras.

## Pros y contras de las opciones

### Recursos WPF del ensamblado

- Buena, porque no hay archivos que perder ni rutas que resolver al arrancar.
- Mala, porque WPF solo resuelve fuentes `pack://` después de registrar el paquete de recursos de la aplicación
  (`AppFonts` lo provoca).

### Archivos sueltos junto al ejecutable

- Buena, porque se pueden sustituir sin compilar.
- Mala, porque se pueden borrar o desincronizar, y el manifiesto firmado tendría que cubrirlos.

### Red o fuentes del sistema

- Buena, porque no añade archivos de terceros.
- Mala, porque incumple TEM-005 y el aspecto vinculante del prototipo.

## Criterios de reapertura

- Que WPF dibuje bien las fuentes variables (se podrían incluir las variables completas).
- Que una licencia de origen cambie o reserve nombres que obliguen a renombrar las fuentes modificadas.

## Más información

- Requisitos: TEM-005 y TEM-007 en el [catálogo](../requirements/catalog.md).
- Guía: [tokens de diseño, §14](../guides/design-tokens.md#14-fuentes).
- ADR relacionados: [ADR-0001](0001-framework-ui-wpf.md), [ADR-0015](0015-licencia-mit-y-dco.md).
