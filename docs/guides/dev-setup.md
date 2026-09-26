# Preparar el entorno de desarrollo

Pasos para compilar y probar Clícalo en un equipo con Windows. Todo se puede hacer con el teclado en
pantalla y el dictado; la guía [programar con pantalla táctil y voz](voice-and-touch-workflow.md) explica
cómo.

> **Estado en M0.** `cl setup` y el resto de verbos de `cl` se están construyendo en este hito. Cada paso
> indica cómo hacerlo a mano mientras tanto.

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

`cl setup` preparará el equipo en una sola orden: restaurará las herramientas locales y configurará la
firma SSH de los commits y el DCO. Hasta que exista, hazlo a mano.

### Firma de commits y DCO

1. Tu identidad en Git:

   ```powershell
   git config --global user.name "Tu nombre"
   git config --global user.email "tu-correo@ejemplo.com"
   ```

2. Una clave SSH para firmar (si no tienes una), que después añades en GitHub como **clave de firma**
   (*Signing Key*) en la configuración de claves SSH de tu cuenta:

   ```powershell
   ssh-keygen -t ed25519 -C "tu-correo@ejemplo.com"
   ```

3. Firma de commits con esa clave, solo en este repositorio:

   ```powershell
   git config gpg.format ssh
   git config user.signingkey "$env:USERPROFILE\.ssh\id_ed25519.pub"
   git config commit.gpgsign true
   git config tag.gpgsign true
   ```

4. **DCO:** cada commit lleva `Signed-off-by`. Desde la terminal, `git commit -s`. Desde VS Code, activa el
   ajuste `git.alwaysSignOff`. Ver [CONTRIBUTING.md](../../CONTRIBUTING.md#certificado-de-origen-dco-y-firma-de-commits).

### Herramientas

- El formato se aplica con CSharpier 1.3.0 sin instalar nada:
  `dotnet dnx csharpier@1.3.0 --yes -- format <rutas>`. Está previsto fijarlo, junto con
  `dotnet-cyclonedx` y `vpk`, en `.config/dotnet-tools.json`; a partir de entonces bastará
  `dotnet tool restore`.

## 5. Visual Studio Code

### Extensiones recomendadas

| Extensión | Identificador | Para qué |
|---|---|---|
| C# | `ms-dotnettools.csharp` | Lenguaje, depuración y navegación («Ir a definición») |
| C# Dev Kit (opcional) | `ms-dotnettools.csdevkit` | Explorador de soluciones y de pruebas. Su licencia es la de Visual Studio Community |
| CSharpier | `csharpier.csharpier-vscode` | Formato al guardar |
| EditorConfig | `editorconfig.editorconfig` | Aplica `.editorconfig` a los archivos que no son C# |
| markdownlint | `davidanson.vscode-markdownlint` | Revisa la documentación como lo hará la CI |
| Markdown Preview Mermaid Support | `bierner.markdown-mermaid` | Muestra los diagramas de la documentación |
| GitHub Actions | `github.vscode-github-actions` | Edita y sigue los *workflows* |
| YAML | `redhat.vscode-yaml` | Validación de los *workflows* y formularios de *issue* |
| Spanish - Code Spell Checker | `streetsidesoftware.code-spell-checker-spanish` | Ortografía de la documentación en español (requiere `streetsidesoftware.code-spell-checker`) |

Para instalarlas desde la terminal: `code --install-extension <identificador>`. Está previsto versionarlas
en `.vscode/extensions.json`, para que VS Code las proponga al abrir la carpeta.

### Ajustes recomendados

```jsonc
{
  "files.autoSave": "afterDelay",
  "editor.formatOnSave": true,
  "[csharp]": { "editor.defaultFormatter": "csharpier.csharpier-vscode" },
  "git.alwaysSignOff": true,
  "editor.accessibilitySupport": "on"
}
```

`editor.accessibilitySupport` optimiza el editor para lectores de pantalla; actívalo si usas Narrador. Está
previsto versionar estos ajustes en `.vscode/settings.json` y una tarea de VS Code por cada verbo de `cl` en
`.vscode/tasks.json`.

## 6. Compilar y probar

```powershell
dotnet build Clicalo.slnx -m:2 -nodeReuse:false
dotnet test --solution Clicalo.slnx
```

Ambas órdenes deben terminar sin errores **y sin advertencias** (`TreatWarningsAsErrors` está activo). Para
iterar más rápido sobre el núcleo: `dotnet test --solution Core.slnf`. Cuando exista `cl`, todo esto es
`cl check`.

La salida de la compilación va a `artifacts/`, nunca junto al código.

## 7. Problemas frecuentes

| Síntoma | Causa probable | Solución |
|---|---|---|
| «A compatible .NET SDK was not found» | Falta el SDK de la banda 10.0.4xx | Instala el SDK 10.0.401 o un parche posterior |
| La restauración falla con `NU1004` en la CI | Un `packages.lock.json` no está al día | `dotnet restore Clicalo.slnx --force-evaluate` y versiona los *lock files* |
| Archivos bloqueados entre compilaciones | Nodos de MSBuild que siguen vivos | Compila con `-nodeReuse:false` o ejecuta `dotnet build-server shutdown` |
| Errores intermitentes de acceso a archivos | El repositorio está dentro de OneDrive | Muévelo a `C:\dev\clicalo` |
| Un analizador falla en un archivo que no tocaste | Otro cambio introdujo la advertencia | No lo suprimas en `.editorconfig`: corrige la causa o usa `[SuppressMessage]` con una justificación real |
