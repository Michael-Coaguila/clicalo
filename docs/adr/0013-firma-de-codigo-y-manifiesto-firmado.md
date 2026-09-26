---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: verificación adversarial «Sostenibilidad»; crítica del plano 1.0 (hueco «Seguridad / firma del manifiesto»)
informed: colaboradores y agentes, mediante AGENTS.md y CODE_SIGNING_POLICY.md
---

# ADR-0013 · Firma de código Authenticode y manifiesto de actualización firmado fuera de GitHub

## Contexto y planteamiento del problema

Sin firma de código, SmartScreen bloquea la instalación y las actualizaciones pierden la confianza del
usuario. El catálogo exige un binario firmado y una actualización firmada (NFR-009) y una actualización
segura (ACT-003). Clícalo inyecta entrada y puede ejecutarse elevado: quien controle su canal de
actualizaciones controla el teclado del usuario (activo A5).

Tres hechos condicionan la decisión:

- Velopack solo comprueba el hash que publica el propio *feed*; por sí solo no verifica ninguna firma.
- Azure Artifact Signing solo admite particulares de EE. UU. y Canadá; SignPath Foundation es gratuita
  para proyectos con licencia OSI, pero exige aprobación manual de cada versión y MFA; un certificado OV
  en HSM en la nube dura como máximo 460 días y la reputación en SmartScreen puede reiniciarse al
  renovarlo.
- La versión 1.0 del plano firmaba el manifiesto con una clave en KMS mediante OIDC desde GitHub
  Actions. La crítica lo refutó: con OIDC, una cuenta de GitHub comprometida puede lanzar el workflow,
  aprobar el entorno y firmar.

¿Cómo se firma el código y cómo se protege el canal de actualizaciones para que una cuenta de GitHub
comprometida no baste para distribuir una versión maliciosa?

## Factores de decisión

- Una cuenta de GitHub comprometida no debe bastar para firmar ni publicar (amenaza T5).
- Anti-rollback del manifiesto sin impedir la reversión legítima que pide el usuario (ACT-005).
- Interruptores de emergencia como datos firmados, nunca como código remoto.
- Coste asumible para un proyecto de código abierto con un solo mantenedor.
- El mantenedor debe poder operar la llave con su movilidad (pantalla táctil, lápiz capacitivo, voz).

## Opciones consideradas

- Authenticode con SignPath Foundation (o alternativas) y manifiesto firmado con ECDSA P-256 solo con una
  llave de hardware del mantenedor, fuera de GitHub
- Clave en KMS con OIDC desde GitHub Actions
- Clave en KMS con aprobación en la consola de la nube
- Firma del manifiesto con minisign o ed25519
- Confiar solo en el hash del *feed*

## Resultado de la decisión

Opción elegida: **«Authenticode con SignPath Foundation y manifiesto firmado con una llave de hardware
fuera de GitHub»**, porque separa las dos firmas de la cuenta de GitHub: comprometerla no da acceso ni a
la aprobación de SignPath (con su propio MFA) ni a la llave física del mantenedor.

- **Authenticode:** SignPath Foundation como primera opción; si no es posible, Azure Artifact Signing
  (si el país lo permite) o un certificado OV en HSM en la nube. Se firman `Clicalo.exe`, Sentinel,
  Launcher, las DLL (también tras R2R), `Setup.exe`, `Update.exe` y los paquetes, con como máximo dos
  aprobaciones por versión. La aprobación usa la cuenta de SignPath con MFA, fuera de GitHub.
- **Manifiesto y catálogo de archivos** (`releases.{canal}.json` y `release-files.json`): la CI los
  produce **sin firmar**. El mantenedor ejecuta `cl sign-manifest vX.Y.Z`, que descarga los artefactos,
  verifica la atestación y los hashes, muestra en una línea legible por Narrador la versión, el canal, el
  `seq` y los hashes abreviados, y firma con ECDSA P-256 en una llave PIV (PIN en el teclado en pantalla y
  toque en la llave). `release-publish.yml` verifica la firma con las claves públicas fijadas antes de
  publicar. **La CI nunca tiene acceso a la clave.**
- **Verificación en el cliente** (`SignedManifestSource`): firma con dos claves públicas fijadas en
  `Platform.Core.Trust` (la actual y la siguiente); rechazo si el `seq` es menor que el último visto
  (anti-rollback del manifiesto) o si `expiresUtc` venció (anti-freeze); SHA-256 del paquete; en
  *staging*, `WinVerifyTrust` con el editor fijado por su sujeto (CN y O) y su raíz, no por la huella, y
  hashes iguales a `release-files.json`.
- **Interruptores de emergencia** firmados: `revoked[]`, `minSafeVersion` y `disabledFeatures[]`.
- **Accesibilidad de la llave:** un modelo *nano* conectado de forma permanente, PIN obligatorio y toque
  obligatorio con caché de 15 s; el sensor capacitivo se activa con el lápiz capacitivo o con cualquier
  contacto de piel. Se valida con el mantenedor antes de M5. Si no puede hacer el toque, se usa la
  política de toque `never` con un equipo de firma dedicado.
- **Rotación:** la clave «siguiente» vive en una segunda llave guardada fuera del domicilio.

### Consecuencias

- Buena, porque distribuir una versión maliciosa exige comprometer a la vez el equipo del mantenedor, su
  PIN y el toque físico, **y** la firma de código.
- Buena, porque el anti-rollback protege el manifiesto y la reversión legítima sigue siendo posible con
  `rollbackAllowed` (ADR-0012).
- Buena, porque fijar el editor por sujeto y raíz sobrevive a la renovación del certificado.
- Mala, porque cada versión exige un paso manual (un solo verbo, `cl sign-manifest`) y la aprobación en
  SignPath.
- Mala, porque la publicación depende de una llave física y de su accesibilidad para el mantenedor.
- Mala, porque queda una cuestión abierta que S8 debe resolver: las condiciones de SignPath Foundation
  solo permiten firmar artefactos compilados a partir del código propio del proyecto, y esta decisión
  prevé firmar también las DLL de terceros tras R2R. Si no es compatible, un ADR nuevo definirá cómo se
  verifican esas DLL (por ejemplo, solo por su hash en el catálogo firmado `release-files.json`).

### Confirmación

- Spike S8: todo el flujo automatizado salvo la firma del manifiesto (un verbo); ninguna DLL sin firma;
  se rechaza un paquete sin la firma esperada; se valida que el mantenedor puede tocar la llave.
- Pruebas de `SignedManifestSource`: firma incorrecta, `seq` menor, bajada legítima y atacante, versión
  revocada y `minSafeVersion`.
- Criterio de M5: primera beta firmada, publicada con el manifiesto firmado fuera de GitHub.

## Pros y contras de las opciones

### SignPath Foundation y llave de hardware fuera de GitHub

- Buena, porque ninguna de las dos firmas depende solo de GitHub.
- Buena, porque SignPath Foundation es gratuita para proyectos con licencia OSI (ADR-0015).
- Mala, porque hay aprobaciones manuales y una dependencia física.

### KMS con OIDC desde GitHub Actions

- Buena, porque la publicación sería totalmente automática.
- Mala, porque una cuenta de GitHub comprometida puede lanzar el workflow, aprobar el entorno y firmar.

### KMS con aprobación en la consola de la nube

- Buena, porque saca la aprobación de GitHub.
- Mala, porque añade una cuenta de nube con coste y operación, y la aprobación en consola es menos
  accesible para el mantenedor que un verbo dictable.

### minisign o ed25519

- Buena, porque es simple y la propuso la verificación como mejora.
- Mala, porque la verificación de ECDSA P-256 ya está en la BCL (`ECDsa`) y las llaves PIV la admiten de
  forma nativa; añadir otra primitiva no aporta seguridad.

### Confiar solo en el hash del *feed*

- Buena, porque es lo que Velopack hace de serie.
- Mala, porque quien controle el *feed* controla el hash: no protege de nada.

## Criterios de reapertura

- Si ninguno de los tres proveedores de Authenticode es accesible, no se publica en estable y se escribe
  un ADR nuevo.
- Si S8 demuestra que las condiciones de SignPath Foundation no permiten firmar todas las DLL previstas,
  un ADR nuevo fija la verificación de las DLL de terceros.
- Si el mantenedor no puede operar la llave de hardware con ninguna política, se escribe un ADR nuevo.

## Más información

- Plano: [§1.2 (D15 y D22)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§9.3](../architecture/blueprint.md#93-actualizaciones),
  [§11](../architecture/blueprint.md#11-distribución-versionado-y-publicación).
- Catálogo: ACT-003 en [§2.25](../requirements/catalog.md#225-act--actualizaciones); NFR-009 en
  [§3](../requirements/catalog.md#3-requisitos-no-funcionales).
- Política pública: [CODE_SIGNING_POLICY.md](../../CODE_SIGNING_POLICY.md).
- Seguridad: amenazas T5 y T12 en el [modelo de amenazas](../security/threat-model.md).
- Registro: [techVerification.json](../architecture/decision-record/techVerification.json) (enfoque
  «Sostenibilidad») y [critique.json](../architecture/decision-record/critique.json).
- Evidencia:
  [preguntas frecuentes de Azure Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/faq),
  [condiciones de SignPath Foundation](https://signpath.org/terms),
  [firma en Velopack](https://docs.velopack.io/packaging/signing),
  [UpdateOptions de Velopack](https://docs.velopack.io/reference/cs/Velopack/UpdateOptions),
  [validez de los certificados de firma de código (Certum)](https://www.certum.eu/en/news/shortening-code-signing-certificate-validity/),
  [reputación en SmartScreen](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation),
  [reinicio de la reputación tras renovar un certificado](https://learn.microsoft.com/en-us/answers/questions/5900208/smartscreen-reputation-reset-following-ev-certific).
- ADR relacionados: [ADR-0009](0009-elevacion-y-componente-de-sistema.md),
  [ADR-0012](0012-velopack-canales-y-datos.md), [ADR-0015](0015-licencia-mit-y-dco.md).
