# Preparar el entorno de desarrollo

Pasos para compilar y probar Clícalo en un equipo con Windows. Todo se puede hacer con el teclado en
pantalla y el dictado; la guía [programar con pantalla táctil y voz](voice-and-touch-workflow.md) explica
cómo.

## 1. Requisitos

| Qué | Versión | Cómo instalarlo |
|---|---|---|
| Windows | Windows 10 22H2 o Windows 11 | — |
| SDK de .NET | 10.0.401 o un parche posterior de la banda 10.0.4xx | `winget install --id Microsoft.DotNet.SDK.10` |
| Git | Reciente | `winget install --id Git.Git` |
| Visual Studio Code | Reciente | `winget install --id Microsoft.VisualStudioCode` |

`global.json` fija el SDK 10.0.401 con `rollForward: latestPatch`: sirve cualquier parche igual o posterior
de la misma banda, pero no una banda distinta ni una versión preliminar. Compruébalo con:

```powershell
dotnet --list-sdks
```

Una pantalla táctil no es necesaria para compilar ni para las pruebas unitarias, pero sí para las pruebas
de integración de escritorio y la aceptación en hardware.

## 2. Una carpeta fuera de OneDrive

Clona el repositorio **fuera de cualquier carpeta sincronizada** (OneDrive, Dropbox, Google Drive). Una
compilación escribe miles de archivos en `artifacts/` y `obj/`, y el cliente de sincronización puede
bloquearlos a mitad de compilación o convertirlos en marcadores de posición; Macro Quick Access ya sufrió
fallos por vivir dentro de OneDrive (lección L-DAT-3 del catálogo). La ubicación recomendada es
`C:\dev\clicalo` ([deviations.md, D-05](../architecture/deviations.md#d-05--repositorio-en-cdevclicalo-fuera-de-onedrive)).

En Windows 11 puedes usar además una **Unidad de desarrollo** (Dev Drive), pensada para este tipo de carga.

## 3. Clonar

```powershell
git clone https://github.com/Michael-Coaguila/clicalo.git C:\dev\clicalo
cd C:\dev\clicalo
```

Mientras el repositorio sea privado necesitas acceso concedido por el mantenedor.

Si trabajas en varias ramas a la vez (o con varios agentes), usa *worktrees* hermanos en lugar de clonar
varias veces:

```powershell
git worktree add C:\dev\clicalo-wt\mi-tarea -b feat/mi-tarea main
```

## 4. `cl setup`

Desde la raíz del repositorio (en PowerShell se escribe `.\cl setup`; en `cmd`, `cl setup`):

```powershell
.\cl setup
```

Una sola orden que:

- restaura las herramientas locales de `.config/dotnet-tools.json` (CSharpier, `dotnet-CycloneDX` y `vpk`);
- ajusta git **solo en este repositorio**: `core.autocrlf=false`, `core.longpaths`, `pull.rebase`,
  `fetch.prune`, `push.autoSetupRemote` y `format.signOff`;
- instala el *hook* versionado `build/githooks/prepare-commit-msg` (con `core.hooksPath`), que añade la
  línea `Signed-off-by` del **DCO** a cada commit sin duplicarla. Al usar `cl setup` certificas el DCO de
  tus propios commits: lee antes [CONTRIBUTING.md](../../CONTRIBUTING.md#certificado-de-origen-dco-y-firma-de-commits);
- activa la **firma SSH** de commits y etiquetas solo si ya tienes una clave de firma configurada
  (`gpg.format=ssh` y `user.signingkey`). `cl setup` nunca crea ni lee claves: si falta, escribe los pasos
  en `artifacts\cl\setup.md`.

Se puede repetir cuando quieras; no cambia nada que ya esté bien.

### Si aún no tienes clave de firma

1. Tu identidad en Git:

   ```powershell
   git config --global user.name "Tu nombre"
   git config --global user.email "tu-correo@ejemplo.com"
   ```

2. Una clave SSH para firmar, que después añades en GitHub como **clave de firma** (*Signing Key*) en la
   configuración de claves SSH de tu cuenta:

   ```powershell
   ssh-keygen -t ed25519 -C "tu-correo@ejemplo.com"
   ```

3. Vuelve a ejecutar `.\cl setup`: detecta la clave y activa la firma. `artifacts\cl\setup.md` explica
   además cómo usar el agente SSH de Windows para no teclear la frase de contraseña en cada commit.

## 5. Visual Studio Code

Al abrir la carpeta, VS Code propone las extensiones de `.vscode/extensions.json` y aplica
`.vscode/settings.json`. Hay una tarea por verbo de `cl` (**Terminal › Ejecutar tarea**, o
`Ctrl+Mayús+P` y «Tasks: Run Task»).

### Extensiones recomendadas

| Extensión | Identificador | Para qué |
|---|---|---|
| C# Dev Kit | `ms-dotnettools.csdevkit` | Lenguaje (instala `ms-dotnettools.csharp`), explorador de soluciones y de pruebas. Su licencia es la de Visual Studio Community |
| CSharpier | `csharpier.csharpier-vscode` | Formato al guardar |
| EditorConfig | `editorconfig.editorconfig` | Aplica `.editorconfig` a los archivos que no son C# |
| markdownlint | `davidanson.vscode-markdownlint` | Revisa la documentación con `.markdownlint-cli2.jsonc` |
| Markdown Preview Mermaid Support | `bierner.markdown-mermaid` | Muestra los diagramas de la documentación |
| GitHub Actions | `github.vscode-github-actions` | Edita y sigue los *workflows* |
| YAML | `redhat.vscode-yaml` | Validación de los *workflows* y formularios de *issue* |
| Spanish - Code Spell Checker | `streetsidesoftware.code-spell-checker-spanish` | Ortografía de la documentación en español (con `streetsidesoftware.code-spell-checker`) |

Para instalarlas desde la terminal: `code --install-extension <identificador>`.

### Ajustes del repositorio

`.vscode/settings.json` guarda al cambiar el foco (`files.autoSave: onFocusChange`; con un retraso, VS Code
no da formato al guardar), da formato con CSharpier **solo en C#** (los JSON de `data/`, los esquemas y los
proyectos conservan su formato escrito a mano), usa LF y abre los informes de `artifacts/cl/` como vista
previa, para recorrerlos por encabezados con Narrador.

Si usas Narrador, activa además como ajuste de usuario `"editor.accessibilitySupport": "on"`.

## 6. Compilar y probar

```powershell
.\cl fast    # núcleo portátil (Core.slnf), para iterar
.\cl test    # las pruebas deterministas (sin escritorio, caos, rendimiento ni cuarentena)
.\cl fix     # formato C# con CSharpier
.\cl check   # lo mismo que la CI; todo PR termina con él
```

Cada orden termina en una sola línea, pensada para Narrador («cl check: correcto en…» o «cl check: falló
en build; detalle en artifacts\cl\last-error.md»). Si falla, `artifacts\cl\last-error.md` tiene el error
exacto con enlaces a la línea y VS Code lo abre solo. Los verbos, los pasos de `cl check` y las variables
de entorno están en [herramientas](../architecture/tooling.md#verbos-de-cl).

`cl check` compila en Release **sin advertencias** (`TreatWarningsAsErrors` y `-warnaserror`) y restaura en
modo bloqueado. La salida de la compilación va a `artifacts/`, nunca junto al código; `cl clean` la borra.

Sin `cl`, lo mismo a mano:

```powershell
dotnet build Clicalo.slnx -m:2 -nodeReuse:false
dotnet test --solution Clicalo.slnx
```

## 7. Problemas frecuentes

| Síntoma | Causa probable | Solución |
|---|---|---|
| «A compatible .NET SDK was not found» | Falta el SDK de la banda 10.0.4xx | Instala el SDK 10.0.401 o un parche posterior |
| PowerShell dice que `cl` no se reconoce | PowerShell no ejecuta programas de la carpeta actual | Escribe `.\cl check` (o usa `cmd`) |
| PowerShell no deja ejecutar `cl.ps1` | Directiva de ejecución `Restricted` | Usa `.\cl.cmd check`, que funciona siempre |
| `cl check` falla en `format` | Archivos C# sin el formato de CSharpier | `.\cl fix` y vuelve a ejecutar |
| `cl check` falla en `restore` con `NU1004` | Un `packages.lock.json` no está al día | `dotnet restore Clicalo.slnx --force-evaluate` y versiona los *lock files* |
| La restauración falla con `NU3034` | El propietario de un paquete nuevo no está en `nuget.config` | Añádelo como explica [herramientas](../architecture/tooling.md#paquetes-central-package-management) |
| Archivos bloqueados entre compilaciones | Nodos de MSBuild que siguen vivos | `cl` ya usa `-nodeReuse:false`; a mano, `dotnet build-server shutdown` |
| Errores intermitentes de acceso a archivos | El repositorio está dentro de OneDrive | Muévelo a `C:\dev\clicalo` |
| Un analizador falla en un archivo que no tocaste | Otro cambio introdujo la advertencia | No lo suprimas en `.editorconfig`: corrige la causa o usa `[SuppressMessage]` con una justificación real |
