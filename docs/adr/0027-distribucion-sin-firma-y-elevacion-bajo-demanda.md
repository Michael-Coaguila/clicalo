---
status: Aceptado
date: 2026-10-09
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisiones D6 y D7 del usuario (2026-10-09); ADR-0009, ADR-0012 y ADR-0013; plano §3.3, §9.3 y §11; catálogo SIS-001, SIS-002, SIS-004, ACT-001 a ACT-005, COP-002 a COP-005, LOG-007, NFR-009, NFR-010 y NFR-014
informed: colaboradores y agentes, mediante el catálogo (§6.2), docs/guides/release.md y AGENTS.md
---

# ADR-0027 · Versión 2.0 sin firma de código y «Reabrir como administrador» bajo demanda

## Contexto y planteamiento del problema

[ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md) exigía, para publicar, la firma Authenticode con SignPath
Foundation y un manifiesto de actualización firmado con ECDSA P-256 por una llave de hardware del mantenedor, fuera de
GitHub. [ADR-0009](0009-elevacion-y-componente-de-sistema.md) cumplía «iniciar con Windows como administrador sin
UAC» (SIS-002) con un componente de sistema instalado en `%ProgramFiles%`, un lanzador Native AOT, una tarea
programada y un catálogo de archivos firmado. [ADR-0012](0012-velopack-canales-y-datos.md) apoyaba las
actualizaciones en ese manifiesto (`SignedManifestSource`).

El 2026-10-09 el usuario decidió:

- **D6.** La versión 2.0 se publica **sin firma de código**, con todo preparado para firmar más adelante con SignPath
  Foundation: sin llave de hardware ni manifiesto ECDSA en la 2.0.
- **D7.** «Reabrir como administrador» **bajo demanda, con la confirmación de UAC cada vez**, sin componente de
  sistema ni inicio elevado sin UAC. SIS-002 queda modificado por decisión del usuario.

¿Qué queda de la distribución, las actualizaciones y la elevación, y con qué controles, sin firma ni componente?

## Factores de decisión

- Decisiones D6 y D7 del usuario; el resto quedó delegado: lo más simple y coherente con el Prototipo v4.
- NFR-010: canal Estable o Beta, instalación en reposo, versión anterior durante 7 días, versión visible.
- ACT-003 y REG-08: copia del documento antes de instalar; los datos nunca se pierden al desinstalar.
- LOG-007: Clícalo solo se eleva cuando la persona lo pide, y nunca eleva un ejecutable que no sea el suyo.
- Un solo mantenedor que trabaja con pantalla táctil y voz: publicar a mano con pocas órdenes, sin GitHub Actions.

## Opciones consideradas

- Velopack con GitHub Releases y la verificación de paquetes de Velopack, sin firma; elevación bajo demanda con UAC
  sobre el ejecutable instalado
- Mantener el manifiesto ECDSA sin Authenticode
- Publicar sin instalador (ZIP portátil)

## Resultado de la decisión

Opción elegida: **«Velopack con GitHub Releases, sin firma; elevación bajo demanda»**, porque cumple D6 y D7 con lo que
Velopack ya trae, sin claves que custodiar, y deja un único punto donde entrará la firma.

**Distribución (sustituye en lo necesario a ADR-0012 y ADR-0013):**

- Velopack por usuario y sin UAC, `packId Clicalo.App`, instalación en `%LocalAppData%\Clicalo.App` y datos en
  `%AppData%\Clicalo` (sin cambios respecto a ADR-0012). Desinstalar solo borra la carpeta de la instalación: **los
  datos se conservan por defecto**, y el *hook* de desinstalación quita además la entrada `Run` de «Iniciar con
  Windows». La pregunta de borrar los datos (Sistema › Desinstalar, P6) no entra en la 2.0.
- Canales `stable` y `beta` de `vpk pack --channel`. Origen: las GitHub Releases del repositorio público por HTTPS
  (`GithubSource`; Beta lee también las versiones preliminares). La única verificación es la de Velopack: la suma
  SHA del paquete que publica el propio *feed*. **No hay** `SignedManifestSource`, manifiesto ECDSA, `seq`,
  `minSafeVersion`, `revoked`, `rollbackAllowed` ni catálogo `release-files.json` en la 2.0; tampoco `cl
  sign-manifest`.
- Nunca se baja de versión de forma implícita: una oferta más antigua que la versión instalada (Beta → Estable) se
  ignora hasta que el canal la alcance.
- Instalación: solo a petición ([Instalar ahora]) o, con «Actualizar automáticamente» y sin «Avisar antes», tras
  `Timings.Updates.UpdateIdleRequired` (5 min) sin usar el panel. Antes, la copia `pre-update` (si «Copia antes de
  actualizar» está activada; por defecto lo está) escrita por el hilo de persistencia; sin copia no se instala. Luego
  `Update.exe` espera a que la instancia salga por `IAppLifetime.ExitAsync` (lo suelta todo, guarda y sale con código
  0, así que Sentinel no relanza) y abre la versión nueva.
- «Volver a la versión anterior»: durante `Timings.Backups.PreviousVersionRetention` (7 días) desde el primer arranque
  de una versión nueva, con dos toques; descarga exactamente la versión anterior de las GitHub Releases
  (`AllowVersionDowngrade` solo en ese flujo). El estado (`update.json`, en la carpeta local) recuerda la versión
  anterior y la fecha.
- Novedades (ACT-004): viajan dentro del paquete (`vpk pack --releaseNotes`), en el formato `## es` / `## en` que
  escribe `cl package` a partir de `changes/unreleased`, nunca en el código.
- Empaquetado local y reproducible con `cl package [--channel stable|beta] [--version …]`: Clícalo autocontenido con
  ReadyToRun para el runtime del equipo (`-p:ClicaloRuntimeIdentifier`, nunca `-r`) y **Sentinel autocontenido sin
  Native AOT** en la misma carpeta (Native AOT exige el enlazador de C++ de Visual Studio, que no está en el equipo del
  mantenedor; el arranque de Sentinel sigue fuera de la ruta crítica del primer frame). Sin deltas. La publicación es
  manual con `gh release` ([guía de publicación](../guides/release.md)); no hay *workflows* de publicación.

**Elevación (sustituye a ADR-0009):**

- No hay componente de sistema, lanzador elevado, tarea programada ni inicio elevado sin UAC. `Clicalo.Launcher` no
  se distribuye en el paquete de la 2.0.
- «Iniciar con Windows» es la entrada `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` con el ejecutable instalado
  entre comillas y sin argumentos: **siempre arranca sin elevación**. Una copia no instalada no se registra.
- «Reabrir como administrador» es una fila con botón en Sistema › Inicio y estabilidad. Antes de pedir nada se
  comprueba que el proceso es exactamente `%LocalAppData%\Clicalo.App\current\Clicalo.exe` (el instalado), que el
  archivo existe y que ni él ni ninguna carpeta de su ruta es un enlace (unión o vínculo simbólico); esta comprobación
  sustituye a `WinVerifyTrust` mientras no haya firma. Después, `ShellExecuteEx` con `runas` (UAC) y
  `--handover=<pid>`; la instancia actual sale por `ExitAsync` y la elevada espera hasta
  `Timings.App.HandoverMutexWait` a que termine antes de tomar la instancia única. No se transfiere estado.
  Cancelar el UAC deja todo igual y lo explica en la barra de estado.
- La fila «Una sola ventana» no se construye (propuesta P2) y «Recuperación automática» es informativa: el guardián
  de [ADR-0023](0023-guardian-simple.md) la cumple siempre.

**Preparado para firmar (SignPath Foundation, cuando el usuario lo decida):** el punto de entrada es la carpeta
publicada por `cl package` antes de `vpk pack`, y `vpk pack --signTemplate` o `--signParams` para `Setup.exe` y
`Update.exe`. Ver la [guía de publicación](../guides/release.md#firmar-más-adelante-con-signpath).

### Consecuencias

- Buena, porque publicar es `cl package` y `gh release`, sin llaves que custodiar ni aprobaciones externas.
- Buena, porque desaparecen el componente de sistema, el lanzador elevado y la tarea programada, con su superficie
  de escalada de privilegios.
- Mala, porque SmartScreen avisa al instalar un binario sin firma y la reputación empieza de cero al firmar.
- Mala, porque quien controle la cuenta de GitHub controla el canal de actualizaciones (amenaza T5 sin mitigar en la
  2.0): la suma SHA la publica el mismo *feed*.
- Mala, porque iniciar como administrador pide UAC cada vez (SIS-002 modificado por D7).
- Neutral, porque los datos y su formato no cambian.

### Confirmación

- `UpdateServiceTests` (fuente falsa): estados, nunca bajar de versión sola, copia y salida limpia antes de instalar,
  espera de 5 min sin uso, ventana de 7 días y dos toques de [Volver].
- `StartupRegistrationTests` (registro falso) y `ElevatedRelaunchTests` (lanzador y disco falsos).
- `SystemSectionTests`: las tres pestañas y sus acciones.
- `PackageTests` y `cl package --channel beta`, que deja `Setup.exe` en `artifacts/package/beta`.

## Pros y contras de las opciones

### Velopack con GitHub Releases, sin firma; elevación bajo demanda

- Buena, porque reutiliza lo que Velopack trae (canales, verificación SHA, `Update.exe`, *hooks*).
- Mala, porque la integridad del canal depende de la cuenta de GitHub.

### Mantener el manifiesto ECDSA sin Authenticode

- Buena, porque protegería el canal frente a una cuenta de GitHub comprometida.
- Mala, porque exige la llave de hardware y `cl sign-manifest`, que D6 excluye de la 2.0.

### ZIP portátil sin instalador

- Buena, porque es lo más simple de publicar.
- Mala, porque pierde las actualizaciones, los canales y la reversión de NFR-010.

## Criterios de reapertura

- Cuando el usuario active la firma con SignPath Foundation, un ADR nuevo fija qué se firma y cómo se verifica.
- Si una cuenta de GitHub comprometida llegara a publicar un paquete, se reabre el manifiesto firmado.
- Si el usuario pide de nuevo iniciar como administrador sin UAC, se reabre el componente de sistema de ADR-0009.

## Más información

- Catálogo: SIS-002 (modificado por D7), §6.2 (D6 y D7), ACT-001 a ACT-005, COP-002 a COP-005.
- Plano: §3.3, §9.3 y §11 (las partes de firma, manifiesto y componente de sistema quedan sustituidas por este ADR).
- [Guía de publicación](../guides/release.md).
- Velopack: [canales](https://docs.velopack.io/packaging/channels),
  [versión concreta](https://docs.velopack.io/integrating/specific-version),
  [desinstalación](https://docs.velopack.io/integrating/uninstalling),
  [firma](https://docs.velopack.io/packaging/signing).
- ADR relacionados: [ADR-0009](0009-elevacion-y-componente-de-sistema.md) (sustituido),
  [ADR-0012](0012-velopack-canales-y-datos.md) (sustituido en parte),
  [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md) (sustituido para la 2.0),
  [ADR-0023](0023-guardian-simple.md).
