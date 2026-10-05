# Cómo contribuir · Contributing

[Español](#español) · [English](#english)

## Español

Gracias por querer mejorar Clícalo. Es una herramienta de accesibilidad: cada cambio se mide pensando en
alguien que solo usa el dedo, tiene temblor o solo usa la voz. Al participar aceptas el
[código de conducta](CODE_OF_CONDUCT.md).

> El repositorio empieza siendo privado: mientras lo sea, se contribuye por invitación del mantenedor.

### Antes de empezar

- Lee la [visión general de la arquitectura](docs/architecture/overview.md) y, si vas a tocar código,
  [preparar el entorno](docs/guides/dev-setup.md).
- Si usas un agente de código, [AGENTS.md](AGENTS.md) resume las reglas que debe seguir.
- Para un cambio grande, abre antes un *issue* para acordar el enfoque.

### Formas de contribuir sin programar

- **Informar de una barrera de accesibilidad** o de un fallo, con los formularios de *issue*.
- **Plantillas de atajos** para una app: son archivos JSON en `data/content/templates/`, validados contra un
  esquema. No hace falta escribir C#.
- **Traducciones:** los textos viven en `data/i18n/strings.es.json` y `data/i18n/strings.en.json`.
- **Probar en hardware real** (pantallas táctiles, lápiz, Acceso por voz, Narrador) y contar qué pasa.

### Flujo de trabajo

- **Desarrollo en tronco (*trunk-based*).** `main` está siempre en verde, protegida y con historial lineal.
- Ramas cortas desde `main`: `feat/…`, `fix/…` o `spike/Sn-…`. Se borran al fusionar. No hay ramas de
  versión.
- **Fusión solo por *squash*.** El título del PR es el mensaje del commit final.

### Títulos de PR y commits: Conventional Commits

El título del PR sigue [Conventional Commits](https://www.conventionalcommits.org/es/v1.0.0/):
`tipo(ámbito): descripción en inglés, en imperativo`.

- **Tipos:** `feat`, `fix`, `perf`, `a11y`, `i18n`, `refactor`, `test`, `build`, `ci`, `docs`, `chore` y
  `revert`.
- **Ámbitos (lista cerrada, opcionales):** `panel`, `tab`, `bubble`, `cc`, `editor`, `templates`, `ai`,
  `engine`, `keysafety`, `touch`, `platform`, `windowing`, `foreground`, `data`, `migration`, `updates`,
  `launcher`, `i18n`, `theme`, `a11y`, `build` y `deps`. Si ninguno encaja, se omite el ámbito.
- `!` (por ejemplo `feat(ai)!:`) solo si se rompe un contrato público: formato del documento, formato para
  compartir, CLI, `clicalo://` o IPC.
- Ejemplos: `fix(keysafety): release scan-code keys in the mode they were pressed`,
  `i18n: add missing plural forms for comboN`, `docs: add ADR-0018 for the AI proxy`.

### Certificado de origen (DCO) y firma de commits

Cada commit debe llevar la línea `Signed-off-by:` del
[Developer Certificate of Origin](https://developercertificate.org/). Con ella certificas que tienes
derecho a aportar ese código bajo la licencia MIT del proyecto. Se añade así:

```powershell
git commit -s -m "fix(touch): ignore palm contacts larger than the threshold"
```

Además, los commits se firman con SSH. `cl setup` instala un *hook* que añade `Signed-off-by` a cada commit
y activa la firma si ya tienes una clave SSH de firma; los pasos están en
[preparar el entorno](docs/guides/dev-setup.md#4-cl-setup). El trabajo `dco` de la CI rechaza el PR si
alguno de sus commits no lleva `Signed-off-by` con el correo de su autor. Solo revisa los commits del PR,
no el historial de `main`: los commits de M0, anteriores a este trabajo, no llevan esa línea.

### Antes de abrir el PR

1. **`cl check` en verde** (`.\cl check` en PowerShell). Es exactamente lo mismo que valida el trabajo
   `verify` de la CI: versiones fijadas, formato, restauración bloqueada, compilación Release sin
   advertencias, pruebas e i18n. Si falla, el detalle está en `artifacts/cl/last-error.md`.
2. **Formato con CSharpier** en los archivos que tocaste (`cl fix`).
3. **Requisitos trazados.** Toda prueba que verifique un requisito del catálogo lleva
   `[Trait("Req", "<ID>")]`. Si tu cambio toca el comportamiento de un requisito, su prueba se actualiza o
   se crea en el mismo PR. Ver la [estrategia de pruebas](docs/architecture/testing-strategy.md).
4. **Textos en los dos idiomas.** Ningún texto de producto se escribe en el código ni en el XAML (el
   analizador CLC0006 lo impide): va en `data/i18n/strings.es.json` **y** en `data/i18n/strings.en.json`,
   con los mismos marcadores, a través de la receta `data/i18n/handoff-import.json` (ver la
   [guía de i18n](docs/guides/i18n.md)).
5. **Nota para usuarios.** Los PR `feat`, `fix`, `a11y` y `perf` que tocan `src/` añaden un fragmento de
   novedades en español e inglés con `cl note` (en `changes/unreleased/`), salvo que lleven la etiqueta
   `no-user-note`.
6. **Nada generado a mano.** Lo que producen los generadores no se edita: se cambia el dato o el generador.

### Qué comprueba la CI

- **En cada PR (bloquea):** `verify` (= `cl check`, solo pruebas deterministas: sin escritorio, caos,
  rendimiento ni cuarentena), `title`, `adr` y `dco`; con el repositorio público, también `verify (arm64)`,
  CodeQL y Scorecard.
- **Cada noche (no bloquea):** `nightly.yml` ejecuta `cl desk`, `cl perf` y `cl quarantine`. Si falla, abre o
  actualiza el *issue* con la etiqueta `nightly`. Para probar el escritorio de tu PR antes de fusionarlo:
  Actions › nightly › *Run workflow* con tu rama (o `refs/pull/<número>/head`).
- **Antes de publicar una versión (M5 en adelante):** todo en verde, incluido el nivel nocturno, el equipo
  táctil (`lab.yml`) y la aceptación en hardware.
- Una prueba inestable que no es un defecto del producto se pone en cuarentena con su *issue*, no se
  reintenta (ver [pruebas inestables](docs/architecture/testing-strategy.md#pruebas-inestables)).

### Cuándo hace falta un ADR

Si tu cambio toca un límite de confianza (IPC, elevación, actualizaciones, firma, contenido importado), un
formato persistido o un contrato público, el framework, el modelo de procesos o de estado, la licencia o la
firma, necesita un [ADR](docs/adr/README.md). El trabajo `adr` de la CI exige un ADR nuevo o cambiado en
`docs/adr/` cuando un PR toca una ruta de `architecture/sensitive-paths.json` (error `CLCA010`); en local
se comprueba con `cl adr-check --base main`.

### Requisitos: nunca se rebajan

Ni un PR ni un ADR pueden rebajar un requisito del catálogo. Si uno te parece inviable o inseguro, abre una
propuesta con la evidencia; **solo el usuario la ratifica**. El procedimiento está en
[cómo leer el catálogo](docs/requirements/README.md#cambiar-un-requisito).

### Idiomas

- Código, identificadores, comentarios, títulos de PR y `CHANGELOG.md`: **inglés**.
- Documentación de arquitectura, ADR, guías y *runbooks*: **español**.
- `README.md`, `CONTRIBUTING.md` y `SECURITY.md`: bilingües.

### Dependencias

Las versiones solo viven en `Directory.Packages.props` (Central Package Management) y son exactas. Los
proyectos usan `PackageReference` sin versión y los `packages.lock.json` se versionan con el cambio. Ver
[herramientas](docs/architecture/tooling.md#paquetes-central-package-management).

### Revisión

- Hoy basta con la CI en verde, porque hay un único mantenedor. Las rutas sensibles (plataforma, motor,
  Sentinel, Launcher, ventanas, primer plano, persistencia, actualizaciones, IPC, workflows, `nuget.config`,
  `Directory.Packages.props` y los analizadores) exigen la revisión de un mantenedor mediante `CODEOWNERS`.
- Las plantillas y las traducciones admiten revisores de la comunidad.
- Cuando haya un segundo mantenedor se escribirá `GOVERNANCE.md`: colaborador tras 3 PR significativos y
  mantenedor por invitación, con 2FA y commits firmados.

### Contribuir con accesibilidad

El proyecto se puede operar entero por voz y con pantalla táctil: una orden `cl` de una palabra por tarea,
una tarea de VS Code por orden y una salida que termina en una línea legible por Narrador. Si algo del
flujo de contribución no es accesible para ti, es un fallo: cuéntalo. Ver
[programar con pantalla táctil y voz](docs/guides/voice-and-touch-workflow.md).

## English

Thank you for helping improve Clícalo. It is an accessibility tool: every change is judged by how it works
for someone who only uses a finger, has a tremor or only uses their voice. By taking part you accept the
[code of conduct](CODE_OF_CONDUCT.md#english). Project documentation is in Spanish; code, identifiers,
comments, PR titles and the changelog are in English.

> The repository starts as private: while it is, contributions are by invitation from the maintainer.

### Ways to contribute without code

Report accessibility barriers or bugs with the issue forms, write app shortcut templates (JSON files in
`data/content/templates/`, validated against a schema), translate (`data/i18n/`), or test on real hardware
(touchscreens, pen, Voice Access, Narrator).

### Workflow

- **Trunk-based development.** `main` is always green, protected and linear. Use short branches
  (`feat/…`, `fix/…`, `spike/Sn-…`) that are deleted on merge.
- **Squash merges only.** The PR title becomes the commit message and follows
  [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/): `type(scope): imperative summary`.
- **Types:** `feat`, `fix`, `perf`, `a11y`, `i18n`, `refactor`, `test`, `build`, `ci`, `docs`, `chore`,
  `revert`. **Scopes** are optional and come from a closed list: `panel`, `tab`, `bubble`, `cc`, `editor`,
  `templates`, `ai`, `engine`, `keysafety`, `touch`, `platform`, `windowing`, `foreground`, `data`,
  `migration`, `updates`, `launcher`, `i18n`, `theme`, `a11y`, `build`, `deps`. Use `!` only when a public
  contract breaks.

### DCO and signed commits

Every commit needs a [Developer Certificate of Origin](https://developercertificate.org/) sign-off, added
with `git commit -s`. Commits are also signed with SSH. `cl setup` installs a hook that adds the sign-off
and turns on signing when you already have an SSH signing key. The CI `dco` job rejects a PR when any of its
commits lacks a `Signed-off-by` line with its author's email. It only checks the PR's own commits, not the
history of `main`: the M0 commits, which predate the job, do not carry it.

### Before opening a PR

1. Run **`cl check`** (`.\cl check` in PowerShell); it is exactly what the CI `verify` job runs. When it
   fails, the details are in `artifacts/cl/last-error.md`.
2. Format the files you touched with CSharpier (`cl fix`).
3. Tag every test that verifies a catalog requirement with `[Trait("Req", "<ID>")]`, and update or add it
   in the same PR when you change that behavior.
4. Put product text only in `data/i18n/strings.es.json` **and** `data/i18n/strings.en.json` (through the
   recipe `data/i18n/handoff-import.json`), never in code or XAML.
5. `feat`, `fix`, `a11y` and `perf` PRs that touch `src/` add a user-facing note in Spanish and English with
   `cl note`, unless labeled `no-user-note`.
6. Never edit generated code: change the data or the generator.

The CI gates a PR only with deterministic tests (`verify` = `cl check`, plus `title`, `adr` and `dco`). Desktop,
performance, chaos and quarantined tests run every night in `nightly.yml`, which opens or updates an issue labeled
`nightly` when they fail; run it by hand on your branch (Actions › nightly › Run workflow) to try the desktop tests
before merging. Before any release (M5 onwards) everything must be green, including the nightly tier, the touch lab
and the hardware acceptance.

### ADRs and requirements

Changes to a trust boundary, a persisted format or public contract, the UI framework, the process or state
model, the license or code signing need an [ADR](docs/adr/README.md). The CI `adr` job requires a new or
changed ADR in `docs/adr/` when a PR touches a path of `architecture/sensitive-paths.json` (error
`CLCA010`); run `cl adr-check --base main` to check it locally. No PR or ADR may lower a catalog
requirement: open a proposal with evidence instead; only the product owner can ratify it.

### Review

For now a green CI is enough (there is a single maintainer); sensitive paths require maintainer review via
`CODEOWNERS`. `GOVERNANCE.md` will be written when a second maintainer joins.
