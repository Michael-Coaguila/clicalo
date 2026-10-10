# Clícalo

> **Lo que quieras hacer, clícalo.** · *Whatever you want to do, clícalo.*

[Español](#español) · [English](#english)

## Español

### Qué es

Clícalo es un panel flotante para Windows, pensado primero para la accesibilidad, que ejecuta atajos de
teclado, macros, textos y acciones de mouse **solo con la pantalla táctil o la voz**, sin teclado físico.
El panel nunca quita el foco a la app en la que trabajas: tocas un botón y la combinación llega a Word, al
navegador o al editor como si la hubieras tecleado.

Está pensado para personas con movilidad reducida en las manos (lesión medular, ELA, artritis, temblor) que
usan la pantalla táctil, Acceso por voz, Narrador o un conmutador, y para cualquiera que quiera atajos a un
toque.

### Por qué existe

Clícalo sucede a **Macro Quick Access**, la herramienta que su creador, Michael Coaguila, construyó después
de una lesión medular: solo usa la pantalla táctil porque no puede mover los dedos, y necesitaba hacer con un
toque lo que otras personas hacen con el teclado. La comparte para que más personas con movilidad reducida
puedan usar su computadora con más rapidez e independencia.

La versión 2 se reconstruye desde cero con las lecciones de la primera: ninguna tecla puede quedarse
pulsada, ningún dato puede perderse y todo tiene que poder hacerse sin teclado físico.

### Estado

**Hito M6 · Cierre de la versión 2.0** (en curso, octubre de 2026). **Todavía no hay ninguna versión
publicada.** Lo que ya está construido y probado:

- el motor que envía los atajos sin dejar nunca una tecla pulsada, con su guardián (`Clicalo.Sentinel`);
- el panel con todas sus vistas (Completa, Compacta, Pestaña y burbuja), la búsqueda, los perfiles automáticos
  por app, Frecuentes y las teclas fijas;
- el Centro de control con sus seis secciones: Atajos (con el editor y «Probar»), Plantillas (con IA
  opcional, solo con la clave propia de cada persona), General y panel, Precisión táctil, Sistema
  (actualizaciones, copias de seguridad e inicio) y Acerca de;
- la bienvenida, el autoguardado con deshacer y las copias de seguridad;
- el instalador y las actualizaciones con Velopack, empaquetados en local con `cl package`.

Lo que falta para publicar la 2.0.0: terminar e integrar los últimos requisitos de M6, repetir en reposo la
medición del arranque ([S5](docs/testing/spikes/S5.md#resultados)) y pasar el
[guion de aceptación manual](docs/guides/aceptacion-manual.md) con la persona usuaria y en una máquina limpia.

La 2.0 se publicará **sin firma de código** (Windows avisará al instalar), se actualizará desde las GitHub
Releases de este repositorio y pedirá la confirmación de Windows cada vez que se reabra como administrador
([ADR-0027](docs/adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md)). La hoja de ruta está en
[§14 del plano](docs/architecture/blueprint.md#14-hoja-de-ruta-por-hitos) y lo que el repositorio hace distinto
del plano, en [desviaciones](docs/architecture/deviations.md).

### Usar Clícalo

Cuando haya una versión, estará en la [página de versiones](https://github.com/Michael-Coaguila/clicalo/releases).
La [guía de usuario](docs/guides/guia-de-usuario.md) explica, con frases cortas y para quien usa la pantalla
táctil o la voz, cómo instalarlo, crear atajos, soltar teclas, hacer copias y usarlo con Narrador y Acceso por
voz.

### Compilar

Requisitos: Windows 10 22H2 o Windows 11, el SDK de .NET 10.0.401 y Git. La guía completa, con VS Code y
las extensiones recomendadas, está en [preparar el entorno](docs/guides/dev-setup.md).

Clona **fuera de OneDrive** (por ejemplo en `C:\dev\clicalo`). Todo se hace con una sola orden dictable,
`cl`:

```powershell
.\cl setup   # prepara el equipo (herramientas, ajustes de git y DCO)
.\cl check   # lo mismo que valida la CI: versiones, formato, compilación, pruebas e i18n
```

En PowerShell se escribe `.\cl`; en `cmd`, `cl`. Cada orden termina en una línea que Narrador lee de una
vez; si falla, el detalle está en `artifacts\cl\last-error.md`. Otras órdenes útiles: `cl run` abre la app sin
envío de teclas y con datos aislados, `cl trace` lista cada requisito con sus pruebas y `cl package` crea el
instalador sin publicarlo. Todos los verbos están en [herramientas](docs/architecture/tooling.md#verbos-de-cl).

### Estructura

| Carpeta | Contenido |
|---|---|
| `src/` | El producto: Domain, Application, Presentation, UI.Wpf, Platform.Core, Platform.Windows, Infrastructure, App, Sentinel y Launcher |
| `generators/` | Generadores y analizadores de Roslyn (`CLC*`) y la matemática de color compartida |
| `tests/` | Pruebas por capa, las mediciones de rendimiento y `Clicalo.TestKit` |
| `data/` | Fuente de verdad que no es código: textos, tokens de tema, catálogos, contenido y esquemas |
| `architecture/` | Reglas de arquitectura como datos (dependencias permitidas, módulos, APIs prohibidas) |
| `build/` | Los destinos de `cl` |
| `tools/` | InputProbe (ventana de prueba Win32), SpikeLab (laboratorio de los spikes) y `Clicalo.DevCli` (textos, ADR y trazabilidad) |
| `assets/` | Fuentes tipográficas empaquetadas (texto e iconos), con sus licencias |
| `changes/` | Novedades para usuarios de la próxima versión (`cl note`) |
| `docs/` | Arquitectura, ADR, requisitos, seguridad, guías y el paquete de diseño original |

### Documentación

- Para quien usa Clícalo: la [guía de usuario](docs/guides/guia-de-usuario.md).
- [Visión general de la arquitectura](docs/architecture/overview.md), el
  [plano completo](docs/architecture/blueprint.md) y sus [desviaciones](docs/architecture/deviations.md).
- [Decisiones de arquitectura (ADR)](docs/adr/README.md).
- [Catálogo de requisitos](docs/requirements/catalog.md) y [cómo leerlo](docs/requirements/README.md).
- [Preparar el entorno](docs/guides/dev-setup.md) y
  [programar con pantalla táctil y voz](docs/guides/voice-and-touch-workflow.md).
- [Publicar una versión](docs/guides/release.md) y el
  [guion de aceptación manual](docs/guides/aceptacion-manual.md).
- [Modelo de amenazas](docs/security/threat-model.md) y [privacidad](docs/security/privacy.md).
- [Qué es vinculante del paquete de diseño](docs/design/handoff/LEEME-VINCULANTE.md).
- Para agentes de código: [AGENTS.md](AGENTS.md).

### Participar

- [Cómo contribuir](CONTRIBUTING.md) · [Código de conducta](CODE_OF_CONDUCT.md) ·
  [Ayuda](SUPPORT.md) · [Seguridad](SECURITY.md) · [Privacidad](PRIVACY.md) ·
  [Política de firma de código](CODE_SIGNING_POLICY.md) · [Cambios](CHANGELOG.md).

### Licencia

[MIT](LICENSE) © 2026 Michael Coaguila y colaboradores de Clícalo. Mantenedor principal: Michael Coaguila.

## English

### What it is

Clícalo is an accessibility-first floating panel for Windows that runs keyboard shortcuts, macros, text
snippets and mouse actions **with just the touchscreen or your voice**, no physical keyboard needed. The
panel never steals focus from the app you are working in: tap a button and the key combination reaches
Word, your browser or your editor as if you had typed it.

It is built for people with reduced hand mobility (spinal cord injury, ALS, arthritis, tremor) who use a
touchscreen, Voice Access, Narrator or a switch, and for anyone who wants one-tap shortcuts.

### Why it exists

Clícalo succeeds **Macro Quick Access**, the tool its creator, Michael Coaguila, built after a spinal cord
injury: he only uses the touchscreen because he cannot move his fingers, and he needed to do with one tap
what others do with a keyboard. He shares it so more people with reduced mobility can use their computer
faster and more independently.

Version 2 is being rebuilt from scratch with the lessons of the first one: no key may ever stay stuck, no
data may ever be lost, and everything must be doable without a physical keyboard.

### Status

**Milestone M6 · Closing version 2.0** (in progress, October 2026). **No version has been published yet.**
Built and tested so far: the engine that sends shortcuts without ever leaving a key stuck, with its guardian
(`Clicalo.Sentinel`); the panel with all its views (full, compact, tab and bubble), search, per-app automatic
profiles and frequents; the Control Center with its six sections (shortcuts and editor, templates with
optional bring-your-own-key AI, general, touch precision, system and about); the welcome wizard, autosave
with undo and backups; and the installer and updates with Velopack, packaged locally with `cl package`.

Before 2.0.0 ships: the last M6 requirements, a start-up measurement on an idle machine and the
[manual acceptance script](docs/guides/aceptacion-manual.md) (in Spanish) with the user and on a clean machine.

Version 2.0 will ship **without code signing** (Windows warns when you install it), update from this
repository's GitHub Releases and ask for the Windows confirmation every time it reopens as administrator
([ADR-0027](docs/adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md), in Spanish). The roadmap is in
[§14 of the blueprint](docs/architecture/blueprint.md#14-hoja-de-ruta-por-hitos) (in Spanish).

### Using Clícalo

Once there is a release, it will be on the
[releases page](https://github.com/Michael-Coaguila/clicalo/releases). The
[user guide](docs/guides/guia-de-usuario.md) is in Spanish for now.

### Building

You need Windows 10 22H2 or Windows 11, the .NET 10.0.401 SDK and Git. Clone **outside OneDrive** (for
example into `C:\dev\clicalo`). Everything runs through a single dictable command, `cl`:

```powershell
.\cl setup   # prepares the machine (tools, git settings and DCO)
.\cl check   # the same checks CI runs: pins, formatting, build, tests and i18n
```

Type `.\cl` in PowerShell and `cl` in `cmd`. Every command ends with one line that Narrator reads at once;
when it fails, the details are in `artifacts\cl\last-error.md`.

### Documentation

Project documentation is written in Spanish; code, identifiers and comments are in English. Start with the
[user guide](docs/guides/guia-de-usuario.md), the [architecture overview](docs/architecture/overview.md), the
[ADRs](docs/adr/README.md) and [AGENTS.md](AGENTS.md).

### Getting involved

- [Contributing](CONTRIBUTING.md#english) · [Code of conduct](CODE_OF_CONDUCT.md#english) ·
  [Support](SUPPORT.md#english) · [Security](SECURITY.md#english) · [Privacy](PRIVACY.md#english) ·
  [Code signing policy](CODE_SIGNING_POLICY.md#english) · [Changelog](CHANGELOG.md).

### License

[MIT](LICENSE) © 2026 Michael Coaguila and Clícalo contributors. Lead maintainer: Michael Coaguila.
