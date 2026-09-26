---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: verificación adversarial «Sostenibilidad»; crítica del plano 1.0 (hueco «Elevación / MUST incumplido»)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0009 · Elevación como proceso completo elevado y componente de sistema opcional

## Contexto y planteamiento del problema

Windows impide que un proceso de integridad media envíe entrada a una app elevada (UIPI). Para que
Clícalo funcione sobre apps de administrador tiene que ejecutarse elevado, y solo cuando el usuario lo
pide (LOG-007). Si no lo está, no envía nada y avisa (EJE-013). Además, **iniciar con Windows como
administrador sin UAC es un requisito MUST** (SIS-002): quien controla el equipo por voz o con el dedo
puede no ser capaz de confirmar un diálogo de UAC en cada arranque (PQ-25).

El paquete de diseño proponía una tarea programada «con los privilegios más altos». La verificación
adversarial lo refutó: una tarea elevada que apunta a una instalación por usuario (escribible por el
propio usuario) es una escalada de privilegios directa (técnica T1574.010 de MITRE ATT&CK). La versión
1.0 del plano, para evitarla, aplazaba SIS-002 a después de la 2.0, algo que el plano no tiene autoridad
para hacer con un MUST. ¿Cómo se cumple SIS-002 en la 2.0 sin abrir una escalada de privilegios?

## Factores de decisión

- SIS-002 es MUST y el plano no rebaja requisitos por su cuenta.
- Ningún binario escribible por el usuario puede ejecutarse elevado sin UAC.
- Ningún proceso de integridad media puede ordenar inyecciones hacia apps elevadas.
- La instalación normal sigue siendo por usuario y sin UAC (ADR-0012).
- El coste en el arranque al iniciar sesión cuenta contra NFR-001 (1 s).

## Opciones consideradas

- Proceso completo elevado bajo demanda y un componente de sistema opcional, instalado una vez con UAC,
  con un lanzador en `%ProgramFiles%` que verifica después de copiar y ejecuta solo una copia protegida
- Un bróker elevado que acepta peticiones de inyección de la instancia media
- Una tarea `Highest` que apunta a la instalación en `%LocalAppData%`
- Aplazar SIS-002 hasta un MSI por máquina después de la 2.0
- uiAccess desde la 2.0

## Resultado de la decisión

Opción elegida: **«Proceso completo elevado y componente de sistema opcional»**, porque cumple SIS-002
en la 2.0 sin escalada de privilegios: lo único que se ejecuta elevado sin UAC es una copia verificada
en una ubicación que el usuario no puede escribir.

- **Por defecto** hay una sola instalación por usuario, sin UAC. «Relanzar elevado» usa
  `ShellExecuteEx("runas")` sobre la ruta en ejecución **después** de `WinVerifyTrust` y la comprobación
  del editor fijado: un binario manipulado nunca llega al diálogo de UAC con el nombre de Clícalo.
- **Componente de sistema** (`Clicalo.SystemComponent`, desde Sistema › Inicio, un único UAC): copia
  `Clicalo.Launcher.exe` firmado a `%ProgramFiles%\Clicalo\` (que hereda la ACL de Program Files),
  registra la tarea `\Clicalo\ElevatedStart-{sidHash}` (principal el usuario, `HighestAvailable`, al
  iniciar sesión y bajo demanda, acción el lanzador **sin argumentos**), crea su entrada de desinstalación
  en HKLM y hace la primera sincronización.
- **El lanzador** (Native AOT, solo depende de `Platform.Core`): si la versión instalada por el usuario
  coincide con la copia protegida, la lanza (camino rápido). Si no, copia a `app\.staging\` (protegida) y
  **verifica la copia, no el original**: SHA-256 de cada archivo contra el catálogo firmado
  `release-files.json` y `WinVerifyTrust` con el editor fijado; la versión no puede ser menor que el
  `minSafeVersion` guardado, que solo sube. Como verifica después de copiar a una ubicación protegida, no
  hay ventana de sustitución (TOCTOU).
- **Reglas con prueba:** antes de registrar la tarea se comprueba con `GetEffectiveRightsFromAcl` que ni
  el usuario ni `Authenticated Users` pueden escribir, cambiar la DACL o borrar el lanzador, su carpeta o
  `app\`. Con Clícalo elevado, las apps y las webs se abren **sin** elevación (EJE-011). Una instancia
  elevada **nunca** escribe en la carpeta de instalación por usuario ni aplica actualizaciones (delega en
  una instancia media, ADR-0012).
- **Sin bróker elevado.** `IInputInjector` es la costura donde encajaría uno, pero hoy está prohibido.
- **uiAccess en M7**, sobre el mismo componente, con la mitigación T4: hacia destinos elevados solo se
  inyecta si el disparo viene de hardware (`IMO_HARDWARE`) o si el usuario lo activa expresamente.

### Consecuencias

- Buena, porque SIS-002 se cumple en la 2.0 y la regla «no rebajar requisitos» se respeta.
- Buena, porque la peor suplantación posible solo arranca el Clícalo oficial verificado: UIPI y la IPC sin
  verbos que inyecten (ADR-0010) impiden controlarlo.
- Mala, porque añade un ejecutable AOT, una tarea programada y una copia protegida que mantener.
- Mala, porque tras cada actualización hay una sincronización de unos 100 MB (en el flujo posterior a la
  actualización, no al iniciar sesión) y el lanzador añade hasta 300 ms al arranque al iniciar sesión.
- Mala, porque queda un riesgo residual aceptado (T3): un proceso medio del mismo usuario puede disparar
  la tarea bajo demanda, aunque solo consigue arrancar el Clícalo oficial elevado.
- Neutral, porque sin el componente, volver a modo administrador pide UAC cada vez.

### Confirmación

- Spike S14: inicio elevado sin UAC en 20 de 20; primer frame en 1 s o menos al iniciar sesión sin
  sincronización; ningún archivo no verificado se ejecuta elevado (incluido un archivo alterado entre la
  copia y la verificación).
- `Clicalo.Launcher.Tests`: verificación tras la copia, rechazo de un archivo alterado, `minSafeVersion`
  monótono, camino rápido y ausencia de argumentos.
- `Clicalo.Platform.IntegrationTests`: ACL de la tarea elevada; una prueba falla si la tarea apunta a una
  ruta que el usuario puede escribir.
- Criterio de M5: inicio elevado sin UAC en 20 de 20 inicios de sesión, dentro de NFR-001.

## Pros y contras de las opciones

### Proceso completo elevado y componente de sistema opcional

- Buena, porque lo elevado sin UAC es siempre una copia verificada y protegida.
- Buena, porque reutiliza la instalación por usuario y no necesita un segundo instalador.
- Mala, porque el componente es una pieza más con su propio ciclo de sincronización.

### Bróker elevado

- Buena, porque la UI seguiría en integridad media.
- Mala, porque un bróker que acepta «inyecta estas teclas» de un cliente medio convierte a cualquier
  código del mismo usuario en controlador de las ventanas de administrador.

### Tarea `Highest` sobre `%LocalAppData%`

- Buena, porque es lo más simple y era la propuesta del paquete.
- Mala, porque es una escalada de privilegios directa: cualquier proceso del usuario puede sustituir el
  binario que la tarea ejecutará elevado.

### Aplazar SIS-002 a un MSI por máquina

- Buena, porque el MSI por máquina resuelve la ubicación protegida.
- Mala, porque rebaja un MUST sin autoridad y obliga a mantener un segundo instalador completo.

### uiAccess desde la 2.0

- Buena, porque permitiría quedar por encima del menú Inicio.
- Mala, porque exige el binario firmado en una ubicación protegida (el mismo componente) y la mitigación
  T4, que depende de S13; se aborda en M7 sobre esta misma base.

## Criterios de reapertura

- Si S14 fracasa, se abre la propuesta P4-B al usuario: rebajar SIS-002 a SHOULD en la 2.0. Solo si el
  usuario la ratifica se sustituye este ADR.
- Si S13 no distingue de forma fiable la entrada de hardware de la inyectada, uiAccess llega sin
  inyección hacia apps elevadas.
- Introducir un bróker elevado exige un ADR nuevo.

## Más información

- Plano: [§1.2 (D12)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§1.4 (P4)](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario),
  [§3.3](../architecture/blueprint.md#33-elevación-uiaccess-y-componente-de-sistema),
  [§9.3, «Instancias elevadas»](../architecture/blueprint.md#93-actualizaciones).
- Catálogo: SIS-002 en [§2.24](../requirements/catalog.md#224-sis--sistema-pestañas-e-inicio-y-estabilidad);
  EJE-011 y EJE-013 en [§2.12](../requirements/catalog.md#212-eje--ejecución-de-acciones); LOG-007 en
  [§2.31](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad).
- Seguridad: amenazas T3, T4 y T6 en el [modelo de amenazas](../security/threat-model.md).
- Registro: [techVerification.json](../architecture/decision-record/techVerification.json) (enfoque
  «Sostenibilidad») y [critique.json](../architecture/decision-record/critique.json).
- Evidencia:
  [MITRE ATT&CK T1574.010](https://attack.mitre.org/techniques/T1574/010/),
  [modelo de seguridad de UI Automation (uiAccess)](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview),
  [configuración del Control de cuentas de usuario](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration),
  [instalador de Velopack](https://docs.velopack.io/packaging/installer).
- ADR relacionados: [ADR-0010](0010-ipc-minima.md), [ADR-0012](0012-velopack-canales-y-datos.md),
  [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md).
