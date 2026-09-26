# Contenido

Atajos que Clícalo trae de serie. Son datos que se cargan de forma perezosa en tiempo de ejecución (D17) y se tratan como contenido no confiable: se validan contra su esquema al compilar y al cargar (LOG-006).

| Archivo | Contenido |
|---|---|
| `seed.json` | Documento inicial: la fila Siempre visible y el perfil General (CAT-003, PQ-44) |
| `library.json` | Biblioteca «Añadir atajo», por secciones (ATJ-010) |
| `templates/<id>.json` | Una plantilla de perfil por archivo (CAT-006): Word, Navegador, VS Code, Excel, PowerPoint, Videollamada, Explorador, Correo y Bloc de notas |

## Atajos

Cada atajo tiene un `id` estable dentro de su archivo (forma parte de la referencia de catálogo, DAT-004), nombre ES/EN, icono de `icons.json`, categoría de `categories.json` y una acción cuyo `type` es `tap`, `hold`, `toggle`, `text`, `mouse`, `macro`, `web`, `app` o `system`. Las teclas se escriben con los ids de `data/catalogs/keys.json` en orden de pulsación.

## Plantilla nueva

1. Crea `templates/<id>.json`; el `id` debe coincidir con el nombre del archivo.
2. Rellena `version` (sube con cada cambio), `authors`, `appsLanguages` (idiomas de los programas revisados), `name`, `icon`, `processes` (ejecutables en minúsculas) y `shortcuts`.
3. Si un atajo cambia con el idioma de los programas, pon la combinación en español en `keys` y la de otro idioma en `variants` (CAT-005).
4. Evita combinaciones repetidas y bloqueadas: las pruebas de datos lo comprueban.

## Decisiones sobre el paquete de diseño

Las diferencias con `docs/design/handoff/data/seed-and-catalogs.json` están enumeradas, con su requisito, en `tests/Clicalo.Data.Tests/Catalogs/HandoffFidelityTests.cs`. Una diferencia nueva hace fallar la prueba hasta que se documenta allí.
