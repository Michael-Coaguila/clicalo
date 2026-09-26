# Catálogos

Datos versionados que describen teclas, acciones, iconos, tiempos y medidas de Clícalo (CAT-001, D17). Cada archivo declara su esquema JSON Schema 2020-12 en `$schema` (carpeta `data/schemas/`) y las pruebas de `tests/Clicalo.Data.Tests/Catalogs` lo validan en cada PR.

| Archivo | Contenido | Se usa como |
|---|---|---|
| `keys.json` | Toda tecla que un atajo puede pulsar, en el orden del selector (EDI-008): id canónico, nombre de la constante, grupo, etiquetas ES/EN, abreviatura para el tamaño S, nombre para lectores de pantalla, familia de modificador y lado | Código generado: `KeyIds`, `KeyGroup`, `KeyDefinitions` (`Clicalo.Domain.Keys`) |
| `keys.win32.json` | Tecla virtual y código de rastreo (set 1) de cada tecla fija en la distribución de referencia `00000409`; las teclas `char:` se resuelven al enviar con la distribución de la ventana en primer plano | Plataforma Windows; el generador comprueba que coincide con `keys.json` |
| `mouse.json` | Las 8 acciones de mouse y las velocidades de desplazamiento | Datos en tiempo de ejecución |
| `categories.json` | Las 10 categorías de color; los colores son tokens de `data/tokens` | Datos en tiempo de ejecución |
| `icons.json` | Biblioteca de iconos con palabras de búsqueda ES/EN, iconos destacados y por defecto | Datos en tiempo de ejecución |
| `combo-icons.es.json`, `combo-icons.en.json` | Icono sugerido para una combinación según el idioma de los programas | Datos en tiempo de ejecución |
| `blocked-combos.json` | Combinaciones bloqueadas o especiales, con su alternativa | Datos en tiempo de ejecución |
| `system-commands.json` | Acciones que no se pueden enviar como teclas (bloquear, brillo) | Datos en tiempo de ejecución |
| `touch-presets.json` | Presets del filtro táctil | Código generado: `TouchPresets` (`Clicalo.Domain.Catalog`) |
| `sizes.json` | Medidas de los tamaños S, M y L y del resto del panel (docs/04) | Código generado: `PanelSizes`, `PanelSize` (`Clicalo.Domain.Catalog`) |
| `timings.json` | Todos los tiempos y umbrales con nombre (NFR-020) | Código generado: `Timings.<Grupo>.<Entrada>` (`Clicalo.Domain.Timing`) |

## Reglas

- **Nada de valores sueltos en el código.** Un tiempo o un umbral nuevo se añade a `timings.json` y se usa desde `Timings`. Cada entrada tiene un solo tipo de valor, una descripción en inglés (se convierte en la documentación de la constante) y cita su requisito (`req`) o su fuente (`source`).
- **Unidades explícitas.** Los tiempos se escriben con unidad (`600ms`, `2.5s`, `10min`, `24h`, `7d`) y los tamaños en bytes también (`16KiB`). Los nombres de los valores en píxeles terminan en `Px` y los de bytes en `Bytes`.
- **Un umbral, un nombre.** El nombre de una entrada no se repite en otro grupo: así no pueden convivir dos valores del mismo umbral.
- **Ids canónicos y estables.** Se guardan en los documentos del usuario: nunca se renombran. Las teclas usan minúsculas ASCII separadas por puntos (`num.add`) o `char:` más un único carácter en minúscula (`char:ñ`) para los símbolos que dependen de la distribución (PQ-49).
- **Textos de producto.** Los nombres que ya existen en `data/i18n` se referencian por clave (`labelKey`); las etiquetas de teclas, acciones e iconos van en el catálogo con español e inglés obligatorios.

## Errores de compilación de los datos

El generador `CatalogGenerator` (`generators/Clicalo.Generators/Catalogs`) convierte los errores de datos en errores de compilación de `Clicalo.Domain`, con la línea y la columna exactas del archivo JSON.

| Id | Significado |
|---|---|
| CLCC001 | JSON no válido |
| CLCC002 | Id o nombre de constante repetido |
| CLCC003 | Id no canónico |
| CLCC004 | Tiempo o umbral negativo |
| CLCC005 | Tiempo o tamaño sin unidad |
| CLCC006 | Tecla sin entrada en `keys.win32.json` |
| CLCC007 | Entrada de `keys.win32.json` para una tecla que no existe |
| CLCC008 | Nombre que no puede ser una constante de C# |
| CLCC009 | Estructura no válida (miembro ausente, referencia rota) |
| CLCC010 | Falta un catálogo en la compilación |

## Tecla nueva

1. Añádela a `keys.json` en su grupo, con `id`, `codeName`, `label` ES/EN y, si su etiqueta es un símbolo, `spoken`.
2. Añade su entrada a `keys.win32.json`: `vk`, `vkName`, `scan` y `extended` según las tablas de Microsoft «Virtual-Key Codes» y «About Keyboard Input», o `{ "resolve": "character" }` si es un carácter.
3. Ejecuta `dotnet test` sobre `tests/Clicalo.Data.Tests`: en Windows, la prueba compara el código de rastreo con `MapVirtualKeyExW` para en-US.
