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

**Hito M0 · Cimientos y arnés** (en construcción). Todavía no hay versiones para usuarios. En este hito se
montan el esqueleto de la solución, la orden `cl`, los analizadores y generadores, la CI y la documentación
de arquitectura. La hoja de ruta completa está en
[§14 del plano](docs/architecture/blueprint.md#14-hoja-de-ruta-por-hitos).

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
vez; si falla, el detalle está en `artifacts\cl\last-error.md`. Todos los verbos están en
[herramientas](docs/architecture/tooling.md#verbos-de-cl).

### Estructura

| Carpeta | Contenido |
|---|---|
| `src/` | El producto: Domain, Application, Presentation, UI.Wpf, Platform.Core, Platform.Windows, Infrastructure, App, Sentinel y Launcher |
| `generators/` | Generadores y analizadores de Roslyn (`CLC*`) y la matemática de color compartida |
| `tests/` | Pruebas por capa y `Clicalo.TestKit` |
| `data/` | Fuente de verdad que no es código: textos, tokens de tema, catálogos, contenido y esquemas |
| `architecture/` | Reglas de arquitectura como datos (dependencias permitidas, módulos, APIs prohibidas) |
| `build/` | Los destinos de `cl` |
| `tools/` | InputProbe (ventana de prueba Win32) y `Clicalo.DevCli` |
| `assets/` | Fuentes tipográficas e iconos |
| `docs/` | Arquitectura, ADR, requisitos, seguridad, guías y el paquete de diseño original |

### Documentación

- [Visión general de la arquitectura](docs/architecture/overview.md) y el
  [plano completo](docs/architecture/blueprint.md).
- [Decisiones de arquitectura (ADR)](docs/adr/README.md).
- [Catálogo de requisitos](docs/requirements/catalog.md) y [cómo leerlo](docs/requirements/README.md).
- [Preparar el entorno](docs/guides/dev-setup.md) y
  [programar con pantalla táctil y voz](docs/guides/voice-and-touch-workflow.md).
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

**Milestone M0 · Foundations and harness** (in progress). There are no user releases yet. This milestone
sets up the solution skeleton, the `cl` command, analyzers and generators, CI and the architecture
documentation. The full roadmap is in
[§14 of the blueprint](docs/architecture/blueprint.md#14-hoja-de-ruta-por-hitos) (in Spanish).

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
[architecture overview](docs/architecture/overview.md), the [ADRs](docs/adr/README.md) and
[AGENTS.md](AGENTS.md).

### Getting involved

- [Contributing](CONTRIBUTING.md#english) · [Code of conduct](CODE_OF_CONDUCT.md#english) ·
  [Support](SUPPORT.md#english) · [Security](SECURITY.md#english) · [Privacy](PRIVACY.md#english) ·
  [Code signing policy](CODE_SIGNING_POLICY.md#english) · [Changelog](CHANGELOG.md).

### License

[MIT](LICENSE) © 2026 Michael Coaguila and Clícalo contributors. Lead maintainer: Michael Coaguila.
