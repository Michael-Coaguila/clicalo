---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuestas de dominio y de equipo y DX
informed: colaboradores, traductores y agentes, mediante AGENTS.md y CONTRIBUTING.md
---

# ADR-0011 · Formato de i18n: JSON plano con marcadores con nombre, plurales CLDR y claves tipadas

## Contexto y planteamiento del problema

Los textos del producto son vinculantes: `strings.es.json` y `strings.en.json` del paquete de diseño
contienen las 669 claves de la interfaz con paridad completa. Pero tienen defectos de formato: marcadores
de una letra (`{p}`, `{n}`, `{a}`…), sin plurales, con `{p}` usado a veces como perfil y a veces como
número (DIS-88), y 49 claves huérfanas.

El catálogo exige paridad ES/EN (IDI-001), ningún texto fuera de los archivos de idioma (IDI-002), nombres
de tecla traducidos (IDI-003), variables semánticas en los mensajes (IDI-004), comprobar las claves
huérfanas (IDI-005) y poder añadir otro idioma sin tocar código (IDI-006, SHOULD). El idioma se cambia en
caliente en todas las ventanas. ¿Qué formato de textos se usa y cómo se garantiza que el texto visible
sigue siendo el del paquete?

## Factores de decisión

- El texto visible debe ser idéntico al del paquete (es vinculante).
- Errores de paridad y de marcadores detectados al compilar, no en producción.
- Plurales correctos en ES y EN, y preparados para un tercer idioma.
- Compatibilidad con una plataforma de traducción comunitaria (Weblate).
- Cambio de idioma en caliente sin reiniciar y sin perder lo que se está editando.

## Opciones consideradas

- JSON plano con marcadores con nombre, sufijos de plural CLDR y claves tipadas generadas
- Archivos `.resx`
- ICU MessageFormat completo
- Conservar los marcadores originales de una letra

## Resultado de la decisión

Opción elegida: **«JSON plano con marcadores con nombre, plurales CLDR y claves tipadas generadas»**,
porque conserva los archivos del paquete como fuente, da paridad en compilación y es compatible con
Weblate.

- **Conversión única y revisada a mano** con `cl i18n-import`: `{p}` → `{profile}` o `{profiles}`,
  `{n}` → `{count}`, `{i}` → `{index}`, `{t}` → `{total}`, `{a}` → `{app}`, `{k}` → `{keys}`,
  `{v}` → `{version}` y `{x}` → `{name}`; sufijos `_one` y `_other` en las claves con plural que marca el
  catálogo (`comboN`, `instNoteSome`, `sugLine`, `dupHead`, `twMacro`, `migT`, `addMissing`); y
  eliminación de las 49 claves huérfanas.
- **Condición vinculante:** una prueba de instantánea verifica que el texto que se muestra con los
  argumentos de muestra es idéntico al del paquete.
- **`LocalizationGenerator`** (generador incremental de Roslyn) genera `MessageKey` en Domain y el API
  tipado (`L.MigT(profiles, shortcuts)`), y da error de compilación ante falta de paridad, marcadores
  distintos entre idiomas, un marcador desconocido o la falta de `_other`. `XamlLocRefValidator`
  comprueba las referencias `{loc:T clave}` del XAML.
- **En ejecución:** `ILocalizer` es una instantánea inmutable; `LocalizationSource` (una por dispatcher)
  notifica el cambio en caliente. Los campos en edición conservan su texto.
- **Tercer idioma:** con Weblate (formato i18next JSON v4); añadir un idioma es añadir un archivo y una
  entrada en `locales.json`.

### Consecuencias

- Buena, porque un error de traducción o de marcador rompe la compilación.
- Buena, porque los marcadores con nombre hacen los mensajes comprensibles para traductores y revisores.
- Buena, porque la pseudolocalización (`qps-ploc`, +40 % de longitud) detecta textos que se desbordan.
- Mala, porque la conversión inicial exige una revisión manual cuidadosa, que protege la prueba de
  instantánea.
- Mala, porque el generador es código propio que hay que mantener.

### Confirmación

- Criterio de salida de M0: las 669 claves importadas con paridad y la instantánea de «texto visible
  idéntico» en verde.
- Pruebas del generador: cada diagnóstico tiene su identificador y su ubicación exacta (línea y columna)
  en el archivo JSON.
- Los textos nuevos (por ejemplo, los de P6) entran por PR de i18n en ambos idiomas.

## Pros y contras de las opciones

### JSON con marcadores con nombre y plurales CLDR

- Buena, porque el formato es el del paquete, legible y fácil de revisar en un PR.
- Buena, porque el generador convierte los errores de datos en errores de compilación.
- Mala, porque los plurales solo cubren las categorías CLDR, no otras variaciones como el género.

### `.resx`

- Buena, porque es el formato nativo de .NET con herramientas conocidas.
- Mala, porque obliga a convertir la fuente vinculante a XML y pierde la correspondencia directa con los
  archivos del paquete.
- Mala, porque no resuelve los plurales ni la paridad por sí solo.

### ICU MessageFormat completo

- Buena, porque cubre plurales, selección y género.
- Mala, porque exige un analizador en tiempo de ejecución y es desproporcionado para siete claves con
  plural.

### Conservar los marcadores de una letra

- Buena, porque no requiere conversión.
- Mala, porque `{p}` ya se usa con dos significados (DIS-88) y no hay forma de expresar plurales.

## Criterios de reapertura

Si un idioma nuevo necesita formas que no cubren los sufijos de plural CLDR (por ejemplo, concordancia de
género), se escribe un ADR nuevo.

## Más información

- Plano: [§1.2 (D18)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§1.3, fila «Marcadores de i18n»](../architecture/blueprint.md#13-contradicciones-entre-las-propuestas-y-cómo-se-resolvieron),
  [§8.5](../architecture/blueprint.md#85-i18n).
- Catálogo: IDI-001 a IDI-006 en [§2.33](../requirements/catalog.md#233-idi--idioma); correcciones y
  claves nuevas en [§9](../requirements/catalog.md#9-textos-correcciones-y-claves-nuevas-necesarias);
  claves huérfanas en [§10](../requirements/catalog.md#10-fuera-de-alcance-no-son-requisitos).
- Paquete de diseño: [strings.es.json](../design/handoff/data/strings.es.json) y
  [strings.en.json](../design/handoff/data/strings.en.json).
- ADR relacionados: [ADR-0002](0002-monolito-modular-hexagonal.md).
