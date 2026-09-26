# Esquemas

Esquemas JSON Schema 2020-12 de los catálogos (`data/catalogs`) y del contenido (`data/content`). Cada archivo de datos declara el suyo en `$schema` con una ruta relativa, que usan tanto los editores como las pruebas.

- `common.schema.json` define los tipos compartidos: texto localizado, id de tecla, combinación, icono, categoría, duración con unidad…
- `shortcut.schema.json` define un atajo y su acción, con la jerarquía cerrada del dominio (plano §6.2).
- El `$id` de cada esquema es su URL en el repositorio; las referencias entre esquemas se resuelven siempre con los archivos locales, nunca por red.

Los esquemas describen la forma; las reglas que cruzan archivos (teclas, iconos y categorías que existen, combinaciones repetidas o bloqueadas, unicidad de umbrales) están en `tests/Clicalo.Data.Tests/Catalogs`.
