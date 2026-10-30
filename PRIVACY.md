# Privacidad · Privacy

[Español](#español) · [English](#english)

## Español

Esta política describe cómo trata tus datos Clícalo 2. Todavía no hay versiones publicadas: la 2.0.0 está en
preparación y esta política se revisará antes de publicarla.

### En pocas palabras

- **Clícalo no tiene telemetría ni envía informes de fallo automáticos.**
- Solo salen datos del equipo en estos casos:
  1. **La comprobación de actualizaciones:** una petición anónima al servidor de descargas (GitHub
     Releases) al arrancar y cada 24 horas. Tu dirección IP es inevitable en cualquier conexión. Se puede
     desactivar.
  2. **La IA, solo si tú la activas con tu propia clave de un proveedor:** al pedir una plantilla se envían
     exactamente cuatro datos: el nombre de la app, la distribución de teclado, el idioma de los programas y
     el idioma de la interfaz. Nunca tus documentos, los títulos de tus ventanas ni tus atajos. Antes de la
     primera vez con cada proveedor se te pide consentimiento, y puedes desactivarla. Los datos van al
     proveedor que eliges, con sus propias condiciones:
     - **Claude (Anthropic)**, con una clave de pago.
     - **Gemini (Google)**, con una clave de Google AI Studio. **Con una clave gratuita, Google puede usar lo
       que se envía y sus respuestas para mejorar sus productos, y personas revisoras pueden leerlo**; con una
       clave de pago, no. Si vives en el Espacio Económico Europeo, Suiza o el Reino Unido, Google dice que
       aplica a todos sus servicios, también al gratuito, las condiciones de datos de pago. Sus condiciones
       piden tener 18 años o más.
  3. **Tu opinión, solo si tú la envías:** Clícalo prepara un correo o un informe con lo que ves en la
     vista previa, y eres tú quien lo envía.
- Todo lo demás se queda en tu equipo.

### Qué se guarda en tu equipo

- Tus perfiles, atajos, ajustes y copias de seguridad, en `%AppData%\Clicalo`. Los textos de los atajos de
  tipo Texto se guardan **cifrados** y ligados a tu cuenta de Windows.
- La clave de la IA, si la usas, en el Administrador de credenciales de Windows, una por proveedor. Clícalo
  nunca la vuelve a mostrar.
- Un registro técnico (`clicalo.log`, como máximo 5 archivos de 1 MB) que **nunca** contiene el texto que
  Clícalo escribe por ti, los títulos de tus ventanas, tus claves ni lo que se graba al capturar una
  combinación.
- El paquete de diagnóstico solo se crea si lo pides, se te muestra entero antes de guardarse y se guarda en
  tu equipo; no se envía solo.

### Borrar tus datos

Ninguna desinstalación borra tus datos sin preguntarte, para que no los pierdas por error:

- Si desinstalas desde Sistema › Desinstalar en el Centro de control, Clícalo te pregunta si quieres
  conservarlos; borrarlos pide confirmación en dos toques.
- Si desinstalas desde Configuración de Windows, los datos se conservan siempre, porque ese camino no
  puede mostrar preguntas. Si vuelves a instalar Clícalo, la bienvenida te ofrece conservarlos o empezar de
  cero.
- También puedes borrarlos tú eliminando la carpeta `%AppData%\Clicalo`.

### Más detalles

La versión técnica de esta política, con los controles y las pruebas que la garantizan, está en
[docs/security/privacy.md](docs/security/privacy.md). Para cuestiones de seguridad, ver
[SECURITY.md](SECURITY.md).

## English

This policy describes how Clícalo 2 handles your data. There are no releases yet: 2.0.0 is being
prepared and this policy will be reviewed before it is published.

### In short

- **Clícalo has no telemetry and sends no automatic crash reports.**
- Data leaves your computer only in these cases:
  1. **Update checks:** an anonymous request to the download server (GitHub Releases) at startup and every
     24 hours. Your IP address is unavoidable in any connection. You can turn this off.
  2. **AI, only if you enable it with your own provider key:** when you ask for a template, exactly four
     values are sent: the app name, the keyboard layout, the language of your programs and the interface
     language. Never your documents, window titles or shortcuts. You are asked for consent the first time
     with each provider and can turn it off. The data go to the provider you choose, under its own terms:
     - **Claude (Anthropic)**, with a paid key.
     - **Gemini (Google)**, with a Google AI Studio key. **With a free key, Google may use what is sent and
       its answers to improve its products, and human reviewers may read it**; with a paid key, it does not.
       If you live in the European Economic Area, Switzerland or the United Kingdom, Google says it applies
       its paid data terms to all its services, the free one included. Its terms require you to be 18 or
       older.
  3. **Feedback, only if you send it:** Clícalo prepares an email or report with what you see in the
     preview, and you are the one who sends it.
- Everything else stays on your computer.

### What is stored on your computer

- Your profiles, shortcuts, settings and backups, in `%AppData%\Clicalo`. Text shortcuts are stored
  **encrypted** and bound to your Windows account.
- The AI key, if you use one, in Windows Credential Manager, one per provider. Clícalo never shows it again.
- A technical log (`clicalo.log`, at most 5 files of 1 MB) that **never** contains the text Clícalo types for
  you, your window titles, your keys or what is recorded while capturing a key combination.
- A diagnostic package is created only on request, shown to you in full before it is saved, and kept on
  your computer; it is never sent on its own.

### Deleting your data

No uninstall deletes your data without asking, so you do not lose it by accident:

- Uninstalling from System › Uninstall in the Control Center asks whether to keep your data; deleting it
  needs a two-tap confirmation.
- Uninstalling from Windows Settings always keeps your data, because that path cannot show questions. If
  you reinstall Clícalo, the welcome flow offers to keep it or start from scratch.
- You can also delete it yourself by removing the `%AppData%\Clicalo` folder.

The technical version of this policy is in [docs/security/privacy.md](docs/security/privacy.md) (in
Spanish). For security matters, see [SECURITY.md](SECURITY.md#english).
