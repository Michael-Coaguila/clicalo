# Política de firma de código · Code signing policy

[Español](#español) · [English](#english)

## Español

Esta política describe qué firma Clícalo, quién lo aprueba y qué se hace si algo sale mal. Sigue los
requisitos de [SignPath Foundation](https://signpath.org/), la vía de firma elegida en
[ADR-0013](docs/adr/0013-firma-de-codigo-y-manifiesto-firmado.md).

### Estado

Clícalo todavía **no publica binarios firmados**. Esta política entra en vigor con la primera beta firmada
(hito M5). La admisión en SignPath Foundation se solicitará cuando el repositorio sea público. Una vez
admitido, cada página de descarga y de versión mostrará:

> Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
> [SignPath Foundation](https://signpath.org/).

Si SignPath Foundation no fuera posible, las alternativas previstas son Azure Artifact Signing (si el país
del mantenedor lo permite) o un certificado OV en un HSM en la nube, con esta misma política.

### Equipo y funciones

| Función | Qué hace | Personas |
|---|---|---|
| *Committers* | Pueden modificar el código fuente sin revisión | Michael Coaguila |
| Revisores | Revisan todas las contribuciones externas antes de fusionarlas | Michael Coaguila |
| Aprobadores | Autorizan cada solicitud de firma | Michael Coaguila |

- Todas las personas del equipo usan autenticación de varios factores en GitHub y en SignPath.
- La aprobación se hace en la cuenta de SignPath, con su propio MFA, **fuera de GitHub**: una cuenta de
  GitHub comprometida no basta para firmar.
- Esta tabla se actualiza en el mismo PR que añada o retire a una persona del equipo.

### Qué se firma

- Solo artefactos **compilados a partir del código de este repositorio** por el workflow automatizado de
  publicación (`release-core.yml`) sobre una etiqueta `vX.Y.Z` o `vX.Y.Z-beta.N` de `main`: `Clicalo.exe`,
  `Clicalo.Sentinel.exe`, `Clicalo.Launcher.exe` y los ensamblados propios (`Clicalo.*.dll`), también después
  de la compilación R2R.
- Todos los binarios firmados llevan el nombre de producto «Clícalo» y la versión de esa compilación en sus
  metadatos.
- **Cuestión abierta hasta el spike S8.** El plano prevé firmar también las bibliotecas de terceros tras
  R2R y los binarios del instalador de Velopack (`Setup.exe` y `Update.exe`). Las condiciones de SignPath
  Foundation solo permiten firmar artefactos compilados desde el código propio. S8 lo resolverá con
  SignPath; si esos archivos no se pueden firmar, su integridad se verificará por su hash en el catálogo de
  archivos firmado (`release-files.json`), y la decisión quedará en un ADR que sustituya a ADR-0013.
- Nunca se firma nada compilado fuera de la CI, ni binarios de otros proyectos por su cuenta.

### Cómo se aprueba una firma

Antes de aprobar, la persona aprobadora comprueba que:

1. la solicitud viene de `release-core.yml` sobre una etiqueta protegida de `main`;
2. el último `lab.yml` sobre ese commit (o uno posterior) está en verde y, para una estable, el *issue*
   «Release verification» (guion manual de accesibilidad y aceptación en hardware) está cerrado;
3. la atestación de procedencia de la compilación (`actions/attest-build-provenance`) corresponde a ese
   commit.

Cada versión necesita como máximo dos aprobaciones: una para los ejecutables y bibliotecas, y otra para el
instalador.

### El manifiesto de actualización

Además de Authenticode, el manifiesto de actualización y el catálogo de archivos se firman con ECDSA P-256
mediante una llave de hardware del mantenedor, con `cl sign-manifest`, en su propio equipo. La CI nunca tiene
acceso a esa clave, y la app solo acepta manifiestos firmados con las dos claves públicas fijadas en su
código.

### Revocación e incidentes

Si se sospecha que se firmó algo que no debía (un binario malicioso, una compilación manipulada o una
credencial comprometida):

1. Se detienen todas las firmas y publicaciones.
2. Se avisa a SignPath Foundation, se colabora en la verificación, la investigación y el análisis de la
   causa, y se solicita la revocación del certificado si procede.
3. La versión afectada se añade a la lista `revoked[]` del manifiesto firmado y se sube `minSafeVersion`,
   de modo que las instalaciones no la instalen ni vuelvan a ella; si hace falta, se desactiva la función
   afectada con `disabledFeatures[]`.
4. Si la afectada es la llave del manifiesto, se pasa a la clave «siguiente», guardada en una segunda llave
   de hardware fuera del domicilio, según el procedimiento de compromiso de claves (previsto en
   `docs/runbooks/key-compromise.md`).
5. Se publica un aviso de seguridad en GitHub y una versión corregida.

### Privacidad y cambios en el sistema

- Qué datos salen del equipo está en [PRIVACY.md](PRIVACY.md): no hay telemetría.
- La instalación es por usuario y no pide permisos de administrador. El componente de sistema opcional
  (para iniciar como administrador sin UAC) solo se instala cuando la persona usuaria lo pide, con un aviso
  de UAC.
- Clícalo se desinstala desde Configuración de Windows o desde Sistema › Desinstalar en el Centro de control;
  el componente de sistema tiene su propia entrada de desinstalación.

## English

This policy describes what Clícalo signs, who approves it and what happens if something goes wrong. It
follows the requirements of [SignPath Foundation](https://signpath.org/), the signing route chosen in
ADR-0013.

### Status

Clícalo does **not ship signed binaries yet**. This policy takes effect with the first signed beta
(milestone M5). The SignPath Foundation application will be filed once the repository is public. After
admission, every download and release page will show:

> Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
> [SignPath Foundation](https://signpath.org/).

If SignPath Foundation is not possible, the planned alternatives are Azure Artifact Signing (if available in
the maintainer's country) or an OV certificate in a cloud HSM, under this same policy.

### Team roles

| Role | Responsibility | Members |
|---|---|---|
| Committers | May modify source code without review | Michael Coaguila |
| Reviewers | Review every external contribution before it is merged | Michael Coaguila |
| Approvers | Authorize each signing request | Michael Coaguila |

All team members use multi-factor authentication on GitHub and SignPath. Approval happens in the SignPath
account, with its own MFA, **outside GitHub**.

### What is signed

- Only artifacts **built from this repository's source code** by the automated release workflow
  (`release-core.yml`) from a `vX.Y.Z` or `vX.Y.Z-beta.N` tag on `main`: `Clicalo.exe`,
  `Clicalo.Sentinel.exe`, `Clicalo.Launcher.exe` and Clícalo's own assemblies (`Clicalo.*.dll`), including
  after ReadyToRun compilation. Signed binaries carry the product name "Clícalo" and the build's version.
- **Open question until spike S8:** the blueprint also plans to sign third-party libraries after R2R and
  Velopack's installer binaries (`Setup.exe`, `Update.exe`). SignPath Foundation's terms only allow signing
  artifacts built from the project's own source. S8 will settle this with SignPath; if those files cannot
  be signed, their integrity will be verified through their hash in the signed file catalog
  (`release-files.json`), recorded in an ADR superseding ADR-0013.

### Approval, revocation and incidents

Before approving, the approver checks that the request comes from `release-core.yml` on a protected tag,
that the lab run is green (and, for stable releases, that the release-verification issue is closed), and
that the build provenance attestation matches the commit. Each release needs at most two approvals.

If anything is suspected to have been signed wrongly, signing and publishing stop; SignPath Foundation is
notified and assisted in verification, investigation and root-cause analysis, and certificate revocation is
requested if appropriate; the affected version is added to the signed manifest's `revoked[]` list and
`minSafeVersion` is raised; if the manifest key is affected, the "next" key held on a second hardware key is
used; and a security advisory and a fixed release are published.

### Privacy and system changes

No data leaves the computer except as described in [PRIVACY.md](PRIVACY.md#english); there is no
telemetry. Installation is per user and needs no administrator rights; the optional system component is
installed only when the user asks for it, behind a UAC prompt, and has its own uninstall entry.
