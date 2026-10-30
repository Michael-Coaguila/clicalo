---
status: Aceptado
date: 2026-10-10
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario
consulted: catálogo NFR-010, ACE-004, LOG-006, LOG-008, REG-04 y REG-08, propuesta P6 (§6.1) y decisiones D7, D8 y D11 (§6.2); plano §3.3, §6.5 y §9.3; ADR-0010, ADR-0012, ADR-0017 y ADR-0027
informed: colaboradores y agentes de M6, mediante este ADR, `architecture/sensitive-paths.json` y `architecture/destructive-operations.json`
---

# ADR-0029 · Desinstalar y reinstalar conservando los datos, copias tratadas como contenido importado y correo de opinión solo al proyecto

## Contexto y planteamiento del problema

Tres requisitos MUST del catálogo cruzan límites de confianza y la auditoría de M6 los encontró a medias:

- **NFR-010** pide un «desinstalador que pregunta si conservar los datos». La propuesta P6 lo concreta: desde
  Configuración de Windows no se puede mostrar interfaz y siempre se conservan los datos; la pregunta se hace en
  Sistema › Desinstalar y, al reinstalar, en la bienvenida. [ADR-0027](0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md)
  dejó escrito que esa pregunta «no entra en la 2.0». **El catálogo manda sobre un ADR** (catálogo §0.1) y la
  decisión D8 del usuario, que enumera lo aplazado a después de la 2.0, no incluye NFR-010: esa frase de ADR-0027
  queda sustituida por este ADR.
- **ACE-004** pide que «Enviar por correo» abra la app de correo con el asunto y el cuerpo. El hilo Shell solo
  abría http y https (LOG-008), así que el mensaje se copiaba siempre. Abrir `mailto:` amplía lo que Clícalo
  puede iniciar: es un límite de confianza.
- **LOG-008** pide que los atajos Web, App y Macro importados se confirmen uno a uno. Sistema › Importar y
  Restaurar aplicaban una copia propia sin revisarla, pero un archivo de copia puede haberse editado o venir de
  otra persona.

¿Cómo se cumplen los tres sin perder datos (REG-08), sin nada destructivo con un solo toque (REG-04) y sin que
un atajo o un archivo importado puedan abrir algo nuevo?

## Factores de decisión

- REG-08: los datos nunca se pierden. Borrarlos solo puede pasar si la persona lo pide y tiene una copia.
- REG-04: todo lo destructivo necesita dos toques y, donde es posible, deshacer.
- LOG-006 y LOG-008: el contenido importado no es de confianza y nada se ejecuta al importar; las apps nunca
  pasan por un intérprete de comandos.
- Decisión D11 del usuario: el correo del proyecto aún no existe y no se inventa.
- Simplicidad: usar lo que Velopack ya trae (`Update.exe --uninstall`, el *hook* de desinstalación y el de
  primer arranque) y no añadir formatos ni comandos de dominio nuevos.

## Opciones consideradas

- Desinstalar desde Sistema con el desinstalador de Velopack, un marcador para borrar los datos y la pregunta
  de la bienvenida al reinstalar; `mailto:` solo hacia la dirección fija del proyecto; copias revisadas como
  contenido importado.
- Dejar NFR-010 como en ADR-0027 (los datos siempre se conservan y no hay pregunta).
- Borrar los datos desde la propia instancia antes de lanzar el desinstalador.
- Abrir cualquier `mailto:` por el mismo camino que las direcciones web.

## Resultado de la decisión

Opción elegida: **la primera**, porque cumple el catálogo con lo que ya existe y cada cosa nueva que se puede
iniciar o borrar queda detrás de una comprobación propia y de una prueba.

**Desinstalar desde Sistema › Inicio y estabilidad › «Desinstalar Clícalo»** (operación destructiva
`UninstallKeepOrDeleteData`):

- Dos toques. **Por defecto los datos se conservan**; «Borrar también mis atajos y ajustes» viene apagado.
- Con esa opción encendida, antes se abre el mismo diálogo de Exportar: la persona guarda una copia donde
  elija. Si cancela o falla, **no se desinstala ni se borra nada**. Tampoco si elige un sitio dentro de las
  carpetas de datos, que se borran: la copia tiene que sobrevivir al desinstalador (REG-08).
- La instancia nunca borra datos por sí misma. Escribe un marcador (`delete-data-on-uninstall`, en
  `%LocalAppData%\Clicalo`), sale por `IAppLifetime.ExitAsync` (suelta todo y guarda) y, justo antes de terminar,
  inicia `%LocalAppData%\Clicalo.App\Update.exe --uninstall`: el mismo comando que ejecuta Configuración de
  Windows, sin *shell* ni intérprete. Solo se inicia si el proceso es exactamente el `current\Clicalo.exe`
  instalado y no hay enlaces en la ruta (la comprobación de ADR-0027 para «Reabrir como administrador»).
- El *hook* de desinstalación (`Program.Main`, sin interfaz, 30 s) quita la entrada de «Iniciar con Windows»
  y, **solo si encuentra el marcador**, borra `%AppData%\Clicalo`, `%LocalAppData%\Clicalo` y la clave de IA del
  Administrador de credenciales. Nunca borra la raíz de una unidad, una carpeta colgada directamente de ella ni
  una carpeta que sea un enlace.
- **Desde Configuración de Windows nunca hay marcador**: ese camino conserva siempre los datos (P6). Un marcador
  que ningún desinstalador atendió se retira al fallar el inicio del desinstalador y en cada arranque normal.

**Reinstalar** (operación destructiva `StartFromScratchOnReinstall`):

- El primer arranque tras instalar (el *hook* `OnFirstRun` de Velopack), si la carpeta de datos ya tenía un
  documento con la bienvenida terminada, abre la bienvenida con una pregunta en el paso 0: «Conservar mis datos»
  (lo predeterminado, no hace nada) o «Empezar de cero».
- «Empezar de cero» necesita dos toques y se despacha como el comando `RestoreBackup` con el documento de una
  instalación nueva en el idioma en uso: antes se guarda una copia del estado actual (carpeta `pre-restore`,
  que Sistema › Copias muestra como «Antes de un cambio grande») y el paso se puede deshacer. No se añade un
  tipo de copia `pre-reset` ni un comando nuevo: el formato persistido no cambia.
- Un arranque que relanza Sentinel o uno con datos aislados (`--data`) nunca pregunta.

**Copias como contenido importado (LOG-008):** al importar o restaurar una copia, sus atajos Web, App y Macro
cuya acción no tenga ya ningún atajo del documento actual se muestran uno a uno, desmarcados, con lo que abren
o ejecutan; solo los marcados se instalan. Restaurar una copia de los propios datos no pregunta nada salvo que
traiga una acción de riesgo que el documento no tiene.

**Correo de opinión (ACE-004):**

- El hilo Shell tiene una entrada propia, `ShellExecutor.OpenMailAsync`, que solo acepta lo que apruebe
  `LaunchSafety.CheckMail`: esquema `mailto`, **un único destinatario idéntico a la dirección fija del
  proyecto** (`AboutLinks.Current.Email`) y solo los campos `subject` y `body`, uno de cada como mucho. Nada de
  `cc`, `bcc`, `to`, `attach` ni otros esquemas. Si Clícalo está elevado, se abre sin elevación.
- `LaunchSafety.Check`, la regla de los atajos, sigue aceptando solo http y https: ningún atajo ni archivo
  importado puede llegar a un `mailto:`.
- Mientras la dirección del proyecto esté vacía (D11), no se abre ninguna app: el mensaje se copia y la nota
  bajo el botón lo dice. Con `--no-input` tampoco se abre nada.

### Consecuencias

- Buena, porque NFR-010 se cumple entero: se puede desinstalar desde la app, se pregunta por los datos y al
  reinstalar se elige entre conservarlos o empezar de cero.
- Buena, porque borrar los datos exige dos toques y una copia guardada, y nada lo hace sin esa petición.
- Buena, porque una copia editada a mano o ajena ya no puede instalar en silencio un atajo que abra una
  dirección o un programa.
- Mala, porque quien borra sus datos tiene que pasar por el diálogo de guardar una copia.
- Mala, porque el borrado de datos ocurre en el *hook* del desinstalador: si falla a medias, quedan archivos y
  no hay interfaz para decirlo.
- Neutral, porque «Empezar de cero» deja la copia en `pre-restore` y no en la carpeta `pre-reset` que nombraba
  el plano.

### Confirmación

- `UninstallDataWipeTests` (carpeta temporal): sin marcador no se toca nada; con él se borran solo las carpetas
  de datos; un enlace no se sigue.
- `UninstallerLauncherTests` (disco y arranque falsos): solo la copia instalada inicia su `Update.exe`.
- `SystemUninstallTests`: salir antes de desinstalar, marcador solo si se pidió, y retirada si no arranca.
- `SystemSectionTests`: dos toques, copia obligatoria antes de borrar y revisión de atajos de riesgo al
  importar y al restaurar.
- `WelcomeSessionTests` y `WelcomeWindowTests`: la pregunta de la reinstalación y sus dos toques.
- `ImportReviewTests`: qué necesita confirmación y qué se instala.
- `LaunchSafetyTests` y `ShellExecutorTests`: el correo solo hacia la dirección del proyecto.
- Desinstalar de verdad y abrir la app de correo no se prueban de forma automática: se comprueban a mano antes
  de publicar ([guía de publicación](../guides/release.md)).

## Pros y contras de las opciones

### Desinstalador de Velopack con marcador, pregunta al reinstalar, correo solo al proyecto

- Buena, porque no añade formatos ni procesos y reutiliza la comprobación de la copia instalada.
- Mala, porque depende de que el instalador abra la app tras instalar para detectar la reinstalación.

### Dejar NFR-010 como en ADR-0027

- Buena, porque no hay código nuevo.
- Mala, porque incumple un MUST del catálogo sin una decisión del usuario que lo aplace.

### Borrar los datos desde la propia instancia

- Buena, porque no hace falta un marcador.
- Mala, porque la instancia tiene abiertos el registro y el documento, y un fallo dejaría la app instalada y sin
  datos.

### Abrir cualquier `mailto:`

- Buena, porque es más simple.
- Mala, porque un atajo o un archivo importado podría abrir un correo hacia cualquier destinatario.

## Criterios de reapertura

- Si el usuario pide borrar los datos también desde Configuración de Windows.
- Si aparece otro destino de correo (soporte, lista de novedades): se amplía la regla con otro ADR.
- Si Velopack cambia su comando de desinstalación o sus *hooks*.

## Más información

- Catálogo: NFR-010, P6, ACE-004, ACE-005, LOG-006, LOG-008, COP-002, COP-004, BIE-010.
- Plano: §3.3 (lanzamientos), §6.5 (ubicaciones) y §9.3 (desinstalación).
- ADR relacionados: [ADR-0027](0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) (sustituido en la frase
  que aplazaba la pregunta de los datos), [ADR-0012](0012-velopack-canales-y-datos.md),
  [ADR-0010](0010-ipc-minima.md) y [ADR-0017](0017-sin-plugins-de-codigo.md).
- Velopack: [desinstalación](https://docs.velopack.io/integrating/uninstalling) y
  [*hooks*](https://docs.velopack.io/integrating/hooks).
