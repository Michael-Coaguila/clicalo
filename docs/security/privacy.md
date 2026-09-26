# Privacidad: qué datos salen del equipo y qué se guarda en él

Documento técnico de privacidad de Clícalo, extraído de
[§12.3 del plano](../architecture/blueprint.md#123-privacidad-datos-que-salen-del-equipo-log-002) y de las
secciones que desarrollan sus controles. La versión para el público general está en
[PRIVACY.md](../../PRIVACY.md).

## Principio

**Nada sale del equipo sin una acción explícita del usuario, salvo la comprobación de actualizaciones, que
se puede desactivar. No hay telemetría ni informes de fallo automáticos** (LOG-002, NFR-008).

## Qué sale del equipo

| Destino | Qué se envía | Cuándo |
|---|---|---|
| *Feed* de actualizaciones (GitHub Releases) | Una petición GET anónima; la dirección IP es inevitable | Al arrancar y cada 24 h; se puede desactivar |
| Proveedor de IA elegido por el usuario, con su propia clave | **Solo los 4 datos de PLA-008:** `appName`, `keyboardLayout`, `appsLang` y `uiLang` | Solo al pedir una plantilla con IA, tras dar el consentimiento |
| Correo o GitHub (opinión) | Lo que el usuario ve en la vista previa exacta | Solo cuando el usuario lo envía |

### IA

- Es opcional y está desactivada hasta que el usuario da su consentimiento la primera vez (PLA-004). Las
  plantillas locales funcionan siempre sin conexión.
- La petición contiene exactamente el nombre de la app, la distribución de teclado, el idioma de los
  programas y el idioma de la interfaz, más una plantilla fija de *prompt*. Nunca documentos, títulos de
  ventana ni atajos del usuario.
- En la 2.0 solo funciona con la clave propia del usuario, guardada en el Administrador de credenciales de
  Windows ([ADR-0014](../adr/0014-ia-con-clave-propia.md)).
- Control: una prueba de Infrastructure intercepta la petición HTTP del adaptador de IA y exige que el cuerpo
  solo contenga esos 4 valores más la plantilla fija del *prompt*.
- El proxy de cuota, si algún día se construye, tampoco enviará ningún identificador de instalación ni otro
  dato persistente o enlazable.

### Actualizaciones

- La comprobación descarga el manifiesto firmado del canal (estable o beta) y, si hay versión nueva, el
  paquete. No envía ningún identificador ni dato de uso.

## Qué se guarda en el equipo

| Dato | Dónde | Protección |
|---|---|---|
| Perfiles, atajos y ajustes | `%AppData%\Clicalo\clicalo.json` (y `.prev`) | Los textos de los atajos Texto y de los pasos de texto de macro, cifrados con DPAPI ligada a la cuenta de Windows (LOG-003) |
| Uso de cada atajo (para Frecuentes) | `%AppData%\Clicalo\usage.json` | Solo local |
| Copias de seguridad | `%AppData%\Clicalo\backups\` | Mismo cifrado que el documento |
| Clave de la IA | Administrador de credenciales de Windows, persistencia local | No viaja con el perfil itinerante; la interfaz nunca la vuelve a mostrar |
| Registros | `%AppData%\Clicalo\logs\clicalo.log`, rotación de 5 × 1 MB | Redactados por tipos (ver abajo) |
| Diagnóstico, guardado de emergencia y registro de fallos | `%LocalAppData%\Clicalo\` | Solo local |

### Registros

Según [§9.4 del plano](../architecture/blueprint.md#94-registros-y-diagnóstico), los registros **nunca**
contienen el texto inyectado, lo que captura el *hook* de grabación, los títulos de ventana, las claves ni
las posiciones del puntero. Lo garantizan:

- los tipos sensibles (`Sensitive<T>` y `SecretText`), que se redactan al convertirse en texto
  (`[oculto · N caracteres]`, `[título oculto]`);
- el analizador CLC0003, que impide en compilación que un valor sensible llegue al registro o a una
  excepción;
- una política de Serilog que elimina las rutas con el nombre de usuario;
- una **prueba canario** en la CI: cinco valores marcados como texto de atajo, proceso, título de ventana,
  búsqueda y clave recorren los flujos E2E y se buscan en registros, trazas ETW y el paquete de diagnóstico.
  Cualquier coincidencia hace fallar la CI.

Las métricas se quedan en el equipo (sin exportador) y las trazas ETW solo llevan identificadores, nunca
contenido.

### Paquete de diagnóstico y volcados

- El paquete de diagnóstico solo se genera a petición del usuario. Contiene los registros, el entorno
  (versión de Windows y de la app, monitores, digitalizador, distribución), los ajustes **sin** perfiles ni
  textos, las métricas y las últimas 200 transiciones del motor (tipos y códigos, sin teclas de Texto). Se
  muestra entero antes de guardarse (ACE-003) y se guarda como zip local; no se envía solo.
- Los volcados de memoria están desactivados por defecto. Si el usuario los activa, se toman sin el
  contenido del *heap*.

## Al desinstalar

Los datos de `%AppData%\Clicalo` se conservan salvo que el usuario elija borrarlos en Sistema › Desinstalar,
con confirmación en dos toques. Desinstalar desde Configuración de Windows siempre los conserva
([ADR-0012](../adr/0012-velopack-canales-y-datos.md)).

## Documentos relacionados

- [Modelo de amenazas](threat-model.md), en especial T7 (registros), T8 (IA) y T11 (portapapeles).
- Requisitos LOG-001 a LOG-008 en el
  [catálogo](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad).
