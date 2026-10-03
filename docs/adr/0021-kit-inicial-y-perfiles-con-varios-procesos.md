---
status: Aceptado
date: 2026-10-03
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisión D2 del usuario del 2026-10-03; ADR-0007, ADR-0017 y ADR-0018; plano §1.2 (D17), §6.2 y §9.1; catálogo BIE-003, BIE-005, BIE-006, CAT-003, CAT-006, ATJ-007, DAT-005, PQ-39, PQ-44 y PQ-45; documentación oficial de Chrome, Edge, Firefox, Brave, Opera, Outlook y Zoom
informed: colaboradores y agentes, mediante el catálogo, data/content/README.md y el CHANGELOG
---

# ADR-0021 · Kit inicial como dato versionado y perfiles vinculados a varios procesos

## Contexto y planteamiento del problema

El 2026-10-03 el dueño del producto tomó y ratificó la decisión **D2** sobre la primera instalación: en el paso
«¿Qué apps usas más?» de la bienvenida se ofrece un kit inicial elegible. «Básicos» (los atajos universales de General
y de Siempre visible del *seed*) va **marcado** por defecto y las 9 plantillas del prototipo (Word, Excel, PowerPoint,
Navegador, VS Code, Explorador, Bloc de notas, Correo y Videollamada) van **sin marcar**. Se puede desmarcar todo y
empezar vacío, y «Omitir» aplica el valor por defecto. Además, las plantillas reconocen varios programas: Navegador es
Chrome, Edge, Firefox, Brave y Opera, y Correo es Outlook clásico y el nuevo Outlook.

Esto cambia requisitos del catálogo: BIE-003 decía que «Omitir» no instala nada del paso 2, PQ-39 que no hay nada
preseleccionado, CAT-003 que General trae siempre sus 12 atajos y CAT-006 y PQ-45 dejaban pendiente que un perfil
vinculara varios procesos. También toca rutas sensibles: `data/schemas/**` (un esquema nuevo de contenido y la
descripción del de plantillas), y obliga a decidir si el formato persistido 1.x del documento cambia.

¿Cómo se describe el kit, quién construye el documento inicial y qué cambia en el modelo y en el formato para que un
perfil siga a varios programas?

## Factores de decisión

- D17 y CAT-001: el contenido es **dato** versionado con esquema que se carga en ejecución; contribuir una plantilla no
  debe exigir tocar C#.
- LOG-006 y ADR-0017: el contenido es no confiable y se valida otra vez al cargarlo; nunca se ejecuta.
- REG-07 y DAT-004: lo que se instala lleva ids nuevos y una referencia de catálogo a su origen.
- ATJ-007 e I5: un proceso pertenece a un solo perfil, sin distinguir mayúsculas.
- CAT-005, PLA-009, PLA-013 y EC-PLA-04: las combinaciones dependen del idioma de los programas, se instala la
  variante de ese idioma y los atajos instalados no cambian si el idioma cambia después.
- Aún no hay ninguna versión publicada, pero el formato 1.0 ya está fijado por ADR-0018 y sus *fixtures* inmutables.

## Opciones consideradas

- **A.** Kit como dato (`data/content/starter.json` con su esquema), construcción pura en Domain y Application, y el
  formato 1.x sin cambios (los perfiles ya guardan una lista `processes`).
- **B.** Kit escrito en código (la lista de opciones y su valor por defecto en la bienvenida).
- **C.** Un perfil por programa (Chrome, Edge, Firefox… cada uno con su copia de la plantilla).

## Resultado de la decisión

Opción elegida: **A**, porque mantiene el contenido como dato validado (D17, LOG-006), no cambia ningún formato
persistido y cumple D2 sin duplicar atajos.

1. **`data/content/starter.json`** (esquema `data/schemas/starter.schema.json`, `version` 1): la lista ordenada de
   opciones del paso 2. Una opción `basics` (icono y textos `kitBasics`/`kitBasicsD` de `data/i18n`) y una opción
   `template` por cada archivo de `templates/`, con el `id` de la plantilla. `selected` marca las opciones por
   defecto: solo `basics`. Las pruebas de datos exigen exactamente un `basics`, primero y marcado, y cada plantilla una
   sola vez y sin marcar. El orden de las plantillas es el del prototipo (Word, Navegador y VS Code, y después las seis
   de `TPL`).
2. **Modelo** (`Clicalo.Domain.Templates`, módulo ya previsto en `domain-modules.json`): `StarterKit`,
   `StarterOption`, `StarterSelection`, `StarterContent`, `SeedContent`, `ProfileTemplate` y `TemplateShortcut`.
   `StarterLibrary.Build` es puro: General y Siempre visible existen siempre; «Básicos» los rellena con el *seed*;
   cada plantilla marcada crea, en el orden del kit, un perfil vinculado a **todos** sus procesos; un proceso que ya
   vincula un perfil anterior se deja fuera del siguiente (I5); una selección vacía da General y Siempre visible
   vacíos. `TemplateInstaller` da ids nuevos y `CatalogRef(origen, versión, elemento)` a cada atajo y a cada perfil.
3. **Idioma de los programas.** Un atajo de contenido guarda la combinación de los programas en español en `keys` y
   las demás en `variants`. Se instala con la combinación del idioma de los programas de los ajustes (PLA-013) y sin
   variantes, de modo que cambiar ese idioma después no cambia los atajos instalados (EC-PLA-04); la referencia de
   catálogo permite ofrecer luego «Actualizar a la variante» desde la vista previa.
4. **Caso de uso** (`Clicalo.Application.UseCases.FirstDocument`): `Create(contenido, selección, ajustes, ids)` para la
   bienvenida y `CreateDefault`, que aplica las opciones marcadas por defecto: es lo que hace «Omitir» y lo que hace el
   primer arranque mientras no exista la bienvenida (M4).
5. **Carga** (`Clicalo.Infrastructure.Catalogs`): `StarterContentFiles.Load` lee `starter.json`, `seed.json` y las
   plantillas del kit desde la carpeta `content` que se copia junto al ejecutable, y `StarterContentReader` las valida
   (JSON estricto, teclas del catálogo, esperas en rango, solo http y https, procesos sin ruta y sin repetir, textos de
   `data/i18n` existentes). Un atajo que no valida se omite; una plantilla que no valida desaparece del kit; sin kit o
   sin *seed* legibles el arranque se queda con General solo y lo registra (`startup.seed_unavailable`). Sustituye a
   `Infrastructure.Content.SeedDocument`.
6. **Varios procesos por perfil (PQ-45 resuelta: sí).** El dominio ya lo admitía: `AppBinding.Processes` es una lista,
   `ProfileFor` acepta cualquiera de ellos e I5 es **por proceso** (`Bind` con `takeOver`). La captura (ATJ-008) añade
   el proceso capturado a los del perfil, y la sugerencia (PER-009, PLA-011) encuentra la plantilla por cualquiera de
   sus procesos (`StarterContent.TemplateFor`). **El formato persistido no cambia:** el `payload` 1.0 ya guarda
   `processes` como lista (ADR-0018) y `document.schema.json` no se toca.
7. **Plantillas revisadas con documentación oficial** (fuentes en `data/content/README.md`):
   - Navegador (`browser`, versión 2): `chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe` y `opera.exe`. Recargar
     pasa de F5 a Ctrl+R, porque Opera solo documenta Ctrl+R; los demás atajos figuran en los cinco.
   - Correo (`outlook`, versión 2): `outlook.exe` y `olk.exe`. Solo combinaciones comunes a los dos Outlook: Nuevo
     correo Ctrl+N (sin variante) y Enviar Ctrl+Entrar, porque Alt+S solo existe en el clásico y Ctrl+U o Ctrl+Mayús+M
     no están en el nuevo.
   - Videollamada (`zoom`, versión 2): sus seis atajos son de Zoom y solo vincula `zoom.exe`, así que se llama «Zoom»
     en los dos idiomas. Hacerla genérica no es posible: Teams, Meet o Webex usan otras combinaciones.

### Consecuencias

- Buena, porque añadir una plantilla al kit es añadir una línea de datos, sin C#.
- Buena, porque «Omitir», la bienvenida y el primer arranque usan el mismo caso de uso puro y el mismo dato.
- Buena, porque ningún formato persistido cambia y los *fixtures* 1.0 siguen valiendo.
- Neutral, porque los atajos del *seed* reciben ahora ids nuevos (antes usaban el id del catálogo, como `copy`); su
  origen sigue en `CatalogRef`.
- Mala, porque un perfil con varios procesos comparte una sola variante por idioma: si dos programas de la misma
  plantilla difieren en un atajo, la plantilla solo puede usar los comunes. Límite conocido: la ayuda en español de
  Microsoft da Ctrl+D para Responder en el nuevo Outlook y Ctrl+R en el clásico; la plantilla usa Ctrl+R.
- Mala, porque el nombre «Zoom» se aparta del prototipo («Videollamada»); queda registrado en las pruebas de fidelidad
  con el paquete de diseño.

### Confirmación

- `tests/Clicalo.Domain.Tests/Templates/StarterLibraryTests.cs` (BIE-003, BIE-006, CAT-003, CAT-005, EC-PLA-04,
  PLA-013, PER-002, PER-009, ATJ-007, DAT-004, DAT-005).
- `tests/Clicalo.Application.Tests/UseCases/FirstDocumentTests.cs`.
- `tests/Clicalo.Infrastructure.Tests/Catalogs/StarterContentReaderTests.cs` (LOG-006) y
  `StarterContentFilesTests.cs`, con el contenido real: los cinco navegadores y los dos Outlook abren su perfil.
- `tests/Clicalo.Data.Tests/Catalogs/StarterKitTests.cs`, `SchemaValidationTests` (esquema nuevo),
  `HandoffFidelityTests` (procesos, nombre y acciones cambiados por decisión) e `I18nReferenceTests`.
- `tests/Clicalo.App.Tests/StartupDocumentsTests.cs`: un primer arranque recibe «Básicos» con ids nuevos.

## Pros y contras de las opciones

### A. Kit como dato, construcción pura y formato sin cambios

- Buena, porque cumple D17 y LOG-006 y no exige migración.
- Buena, porque el dominio ya cumplía I5 por proceso.
- Mala, porque añade un archivo y un esquema que mantener.

### B. Kit en código

- Buena, porque es un archivo menos.
- Mala, porque contradice D17 y CAT-001 y obliga a tocar C# para ofrecer una plantilla.

### C. Un perfil por programa

- Buena, porque cada programa podría tener sus propias combinaciones.
- Mala, porque duplica los atajos (cinco perfiles de Navegador), contradice D2 y llena de repetidos el selector.

## Criterios de reapertura

- Que una plantilla necesite combinaciones distintas por programa y no solo por idioma (haría falta una variante por
  proceso y un cambio del formato).
- Que la bienvenida (M4) necesite guardar la selección del paso 2 en el documento (BIE-010): sería un campo nuevo del
  `payload` y un *minor* nuevo con su ADR.

## Más información

- Catálogo: [§6.2 Decisiones del usuario](../requirements/catalog.md#62-decisiones-del-usuario) y los requisitos
  marcados con D2.
- Plano: [§9.1 Plantillas](../architecture/blueprint.md#91-plantillas).
- Contenido: [`data/content/README.md`](../../data/content/README.md), con las fuentes oficiales de cada plantilla.
- [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md): formato 1.0 del documento, que no cambia.
