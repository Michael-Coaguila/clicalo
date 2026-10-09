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
