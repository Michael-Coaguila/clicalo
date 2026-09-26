# Política de seguridad · Security policy

[Español](#español) · [English](#english)

## Español

Clícalo inyecta entrada de teclado y mouse, instala *hooks* temporales y puede ejecutarse elevado. Una
vulnerabilidad puede dar a un atacante el control del teclado de una persona que no tiene otra forma de
usar su equipo. Nos la tomamos en serio.

### Versiones con soporte

| Versión | Soporte |
|---|---|
| 2.x (en desarrollo, hito M0) | Todavía no hay versiones publicadas |
| Macro Quick Access (1.x) | Sin soporte; la sustituye Clícalo 2 |

Cuando se publique la 2.0, recibirán correcciones de seguridad la última versión estable y la última beta.

### Cómo informar de una vulnerabilidad

**No abras un *issue* público.** Usa el informe privado de vulnerabilidades de GitHub: en el repositorio,
pestaña **Security**, botón **Report a vulnerability**
([enlace directo](https://github.com/Michael-Coaguila/clicalo/security/advisories/new);
[cómo funciona](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)).
GitHub solo ofrece ese informe privado en repositorios públicos. Mientras este repositorio sea privado, el
botón y el enlace no existen: si tienes acceso, avisa al mantenedor en privado, por el mismo canal por el que
te dio acceso, y nunca en un *issue*. Antes de hacer público el repositorio se activa el informe privado
(**Settings › Code security › Private vulnerability reporting**) y se añade aquí un correo de seguridad.

Incluye, si puedes:

- la versión de Clícalo y de Windows, y si Clícalo estaba elevado o con el componente de sistema;
- qué límite de confianza se cruza (por ejemplo, un proceso de integridad media que consigue inyectar en una
  app elevada, un canal de actualización que acepta un paquete no firmado o un perfil importado que ejecuta
  código);
- los pasos para reproducirlo y el impacto;
- si ya se conoce públicamente.

El modelo de amenazas, con los controles y los riesgos residuales aceptados, está en
[docs/security/threat-model.md](docs/security/threat-model.md).

### Plazos

| Paso | Plazo |
|---|---|
| Primera respuesta | 72 horas como máximo |
| Corrección de una vulnerabilidad crítica | 7 días como máximo |
| Divulgación coordinada | 90 días como máximo desde el informe |

Cuando un CVE afecta al runtime de .NET que Clícalo incluye (`Microsoft.NETCore.App` o
`Microsoft.WindowsDesktop.App`), se publica una beta con el parche en 72 horas como máximo y una estable en
7 días como máximo.

### Cómo se protegen las actualizaciones

Las versiones se firman con Authenticode y el manifiesto de actualización se firma con una llave de hardware
del mantenedor, fuera de GitHub. Los detalles están en la
[política de firma de código](CODE_SIGNING_POLICY.md) y en
[ADR-0013](docs/adr/0013-firma-de-codigo-y-manifiesto-firmado.md).

## English

Clícalo injects keyboard and mouse input, installs temporary hooks and may run elevated. A vulnerability
could hand an attacker the keyboard of someone who has no other way to use their computer. We take reports
seriously.

### Supported versions

| Version | Support |
|---|---|
| 2.x (in development, milestone M0) | No releases yet |
| Macro Quick Access (1.x) | Unsupported; superseded by Clícalo 2 |

Once 2.0 ships, the latest stable and the latest beta receive security fixes.

### Reporting a vulnerability

**Do not open a public issue.** Use GitHub private vulnerability reporting: **Security** tab, **Report a
vulnerability** ([direct link](https://github.com/Michael-Coaguila/clicalo/security/advisories/new);
[how it works](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)).
GitHub only offers private reporting on public repositories. While this repository is private, the button
and the link do not exist: if you have access, tell the maintainer privately, through the same channel they
used to give you access, and never in an issue. Before the repository goes public, private reporting is
turned on (**Settings › Code security › Private vulnerability reporting**) and a security email is added
here.

Please include the Clícalo and Windows versions (and whether Clícalo was elevated or had the system
component installed), the trust boundary that is crossed, reproduction steps, impact, and whether the issue
is already public. The threat model, including accepted residual risks, is in
[docs/security/threat-model.md](docs/security/threat-model.md) (in Spanish).

### Timelines

| Step | Target |
|---|---|
| First response | Within 72 hours |
| Fix for a critical vulnerability | Within 7 days |
| Coordinated disclosure | Within 90 days of the report |

When a CVE affects the bundled .NET runtime (`Microsoft.NETCore.App` or `Microsoft.WindowsDesktop.App`), a
patched beta ships within 72 hours and a patched stable release within 7 days.

### How updates are protected

Releases are Authenticode-signed and the update manifest is signed with the maintainer's hardware key,
outside GitHub. See the [code signing policy](CODE_SIGNING_POLICY.md#english).
