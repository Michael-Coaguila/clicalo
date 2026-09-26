# Fixtures v1 (Macro Quick Access)

Copias **anonimizadas** de los archivos reales de Macro Quick Access v1 del autor, para las pruebas de fidelidad de la
importación ([§6.6 del plano](../../../../docs/architecture/blueprint.md#66-migraciones-incluida-v1--v2),
[catálogo §7](../../../../docs/requirements/catalog.md#7-esquema-v1-exacto-macro-quick-access-y-conversión)).

## Cómo se generan

Con la orden `anonymize-v1` de `tools/Clicalo.DevCli`, que solo **lee** el original y nunca lo modifica:

```powershell
dotnet run --project tools/Clicalo.DevCli -- anonymize-v1 --in "<Documentos>\Macro Quick Access\dist\MacroQuickAccess\profiles.json" --out tests/Clicalo.Infrastructure.Tests/Fixtures/v1/profiles.dist-app.json
```

- **Se conservan tal cual:** la estructura y el orden de las claves, todas las combinaciones (`hotkey`, y `action`
  cuando es una combinación), los colores, los tipos, los números (`window_pos`, `window_opacity`, `button_size`…) y
  los recuentos.
- **Se conservan solo si son públicos:** los nombres de perfil y de botón y los procesos, cuando están en la lista
  revisada `tools/Clicalo.DevCli/AnonymizeV1/v1-public-names.json` (nombran una función o una app, nunca a una
  persona).
- **Se sustituye todo lo demás** por un marcador de la misma longitud y tipo (letra por letra, cifra por cifra;
  espacios y puntuación en su sitio): direcciones web después del esquema, órdenes de app, `_nota` y claves
  desconocidas. El mismo texto recibe siempre el mismo marcador, así que los nombres repetidos (MIG-008) y las
  referencias entre perfiles (`active_profile`) se mantienen.
- La orden comprueba su salida antes de escribirla (mismo esqueleto y ningún texto sustituido presente) y por consola
  solo muestra recuentos. La prueba `The_committed_fixtures_only_carry_public_names` de `Clicalo.DevCli.Tests` falla
  si aquí aparece un nombre que no está en la lista revisada.

Los originales usan CRLF; los fixtures, LF (`.gitattributes`). En estos seis archivos todos los nombres son públicos:
solo se sustituyó la `_nota` de la copia en español.

## Archivos y recuentos reales

Comprobados el 2026-09-26 sobre los originales (`Documentos\Macro Quick Access\`):

| Fixture | Original | Perfiles | Botones | Notas |
|---|---|---|---|---|
| `profiles.dist-app.json` | `dist\MacroQuickAccess\profiles.json` | 14 | **210** | El que se usa; el criterio 210 → 210 del catálogo (MIG-001, MIG-004) |
| `profiles.dist.json` | `dist\profiles.json` | 14 | 204 | `window_pos` fuera de la pantalla (x = 1963) |
| `profiles.root.json` | `profiles.json` (raíz) | 13 | 192 | |
| `profiles.backup.es.json` | `profiles.backup.es.json` | 12 | 163 | Con `_nota` |
| `profiles.backup.en.json` | `profiles.backup.en.json` | 12 | 160 | Incluye `ctrl+k z` (Zen Mode), que tampoco funcionaba en v1 |
| `profiles.backup.zip` | `profiles.backup.zip` | — | — | Contiene las dos copias por idioma y un `profiles.json` de 13 perfiles y 192 botones |

Todos los botones reales son de la variante (a) del catálogo §7.2: combinación sin `type`, sin separadores, URL ni
apps. Los colores personalizados reales (`#55ff00` ×2, `#5500ff`, `#00ffff`) están en el archivo de 210 botones.

**Diferencia con el encargo.** El encargo de M2 cita «los 3 archivos reales» como `profiles.json`,
`profiles.backup.es.json` y `profiles.backup.en.json`, pero la cifra 210 del catálogo es la de
`dist\MacroQuickAccess\profiles.json`; el `profiles.json` de la raíz tiene 192 botones y las copias por idioma 163 y
160. Por eso se versionan los seis archivos (los tres `profiles.json` reales, las dos copias por idioma y el zip, como
pide el §6.6 del plano), y las pruebas comprueban la cifra real de cada uno.

**Ubicación.** El encargo pedía `tests/Clicalo.TestKit/fixtures/v1/`, fuera de las rutas del paquete `migration`; se
usan las que le asigna `docs/testing/spikes/M2-ownership.md` (`tests/Clicalo.Infrastructure.Tests/Fixtures/v1/`).
