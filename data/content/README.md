# Contenido

Atajos que Clícalo trae de serie. Son datos que se cargan de forma perezosa en tiempo de ejecución (D17) y se tratan como contenido no confiable: las pruebas de datos los validan contra su esquema en cada PR, y el cargador lo hará de nuevo al leerlos (LOG-006).

| Archivo | Contenido |
|---|---|
| `starter.json` | Kit inicial: las opciones del paso «¿Qué apps usas más?» de la bienvenida, en su orden, y cuáles van marcadas por defecto (BIE-006, decisión D2 del usuario) |
| `seed.json` | Contenido de la opción «Básicos»: los atajos universales de la fila Siempre visible y del perfil General (CAT-003, PQ-44) |
| `library.json` | Biblioteca «Añadir atajo», por secciones (ATJ-010) |
| `templates/<id>.json` | Una plantilla de perfil por archivo (CAT-006): Word, Navegador, VS Code, Excel, PowerPoint, Zoom, Explorador, Correo y Bloc de notas |

## Kit inicial

`starter.json` decide con qué empieza un documento nuevo (decisión D2 del usuario, 2026-10-03):

- General y Siempre visible existen siempre. La opción `basics` («Básicos», marcada por defecto) los rellena con `seed.json`; sin ella empiezan vacíos.
- Cada opción `template` instala `templates/<id>.json` como un perfil vinculado a **todos** sus procesos, con las teclas del idioma de los programas (CAT-005). Van sin marcar.
- Desmarcarlo todo empieza vacío; «Omitir» aplica las opciones marcadas por defecto. Mientras no exista la bienvenida (M4), el primer arranque aplica también las marcadas por defecto.
- Cada plantilla aparece una sola vez en el kit y toda plantilla está en el kit: lo comprueban las pruebas de datos.

## Atajos

Cada atajo tiene un `id` estable dentro de su archivo (forma parte de la referencia de catálogo, DAT-004), nombre ES/EN, icono de `icons.json`, categoría de `categories.json` y una acción cuyo `type` es `tap`, `hold`, `toggle`, `text`, `mouse`, `macro`, `web`, `app` o `system`. Las teclas se escriben con los ids de `data/catalogs/keys.json` en orden de pulsación.

## Plantilla nueva

1. Crea `templates/<id>.json`; el `id` debe coincidir con el nombre del archivo.
2. Rellena `version` (sube con cada cambio), `authors`, `appsLanguages` (idiomas de los programas revisados), `name`, `icon`, `processes` (ejecutables en minúsculas: todos los programas para los que se revisaron los atajos, como los cinco navegadores de `browser.json`; un proceso solo puede estar en una plantilla) y `shortcuts`.
3. Si un atajo cambia con el idioma de los programas, pon la combinación en español en `keys` y la de otro idioma en `variants` (CAT-005).
4. Evita combinaciones repetidas y bloqueadas: las pruebas de datos lo comprueban.
5. Añádela a `starter.json` (sin marcar) para que la bienvenida la ofrezca.
6. Cita en el PR la documentación oficial de cada programa de la que salen los atajos. Las fuentes de las plantillas actuales están en la sección siguiente.

## Fuentes de las plantillas

| Plantilla | Procesos | Fuente oficial de los atajos |
|---|---|---|
| `browser` | `chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe`, `opera.exe` | [Chrome](https://support.google.com/chrome/answer/157179), [Edge](https://support.microsoft.com/microsoft-edge/keyboard-shortcuts-in-microsoft-edge-50d3edab-30d9-c7e4-21ce-37fe2713cfad), [Firefox](https://support.mozilla.org/kb/keyboard-shortcuts-perform-firefox-tasks-quickly), [Brave](https://support.brave.app/hc/en-us/articles/360032272171-What-keyboard-shortcuts-can-I-use-in-Brave), [Opera](https://help.opera.com/en/latest/shortcuts/). Todos los atajos de la plantilla figuran en las cinco; Recargar es Ctrl+R porque Opera no documenta F5. |
| `outlook` | `outlook.exe` (Outlook clásico), `olk.exe` (el nuevo Outlook) | [Métodos abreviados de Outlook](https://support.microsoft.com/office/keyboard-shortcuts-for-outlook-3cdeb221-7ae5-4c1d-8c1d-9e63216c1efd) (ES y EN) y su versión de [accesibilidad](https://support.microsoft.com/es-es/accessibility/outlook/keyboard-shortcuts-for-outlook). Solo se usan combinaciones comunes a los dos Outlook: Nuevo correo Ctrl+N y Enviar Ctrl+Entrar (Alt+S solo existe en el clásico). Límite conocido: la página en español da Ctrl+D para Responder en el nuevo Outlook y Ctrl+R en el clásico; la plantilla usa Ctrl+R, que no borra nada si no responde. |
| `zoom` | `zoom.exe` | [Atajos de Zoom Workplace](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0067050): Alt+A, Alt+V, Alt+S, Alt+Y, Alt+H y Alt+Q son de Zoom y no de otras apps de videollamada, así que la plantilla se llama «Zoom». |


## Decisiones sobre el paquete de diseño

Las diferencias con `docs/design/handoff/data/seed-and-catalogs.json` están enumeradas, con su requisito, en `tests/Clicalo.Data.Tests/Catalogs/HandoffFidelityTests.cs`. Una diferencia nueva hace fallar la prueba hasta que se documenta allí.
