# Modelo de amenazas

Documento vivo del modelo de amenazas de Clícalo. Se extrae de
[§12 del plano](../architecture/blueprint.md#12-seguridad-y-privacidad) y no añade controles que no estén en
él. Se revisa en cada ADR que toque un límite de confianza y de forma completa en el hito M6.

Para informar de una vulnerabilidad, sigue [SECURITY.md](../../SECURITY.md). Lo que sale del equipo está en
[privacidad](privacy.md).

## Por qué importa

Clícalo inyecta entrada de teclado y mouse en otras apps, instala *hooks* temporales y puede ejecutarse
elevado (y, en el futuro, con uiAccess). Quien lo controle controla el teclado del usuario, a veces también
en apps de administrador. Por eso cada límite de confianza tiene su control y su prueba.

## Activos

| Id | Activo |
|---|---|
| A1 | La capacidad de inyectar entrada, también en apps elevadas si Clícalo está elevado o tiene uiAccess |
| A2 | Los textos guardados |
| A3 | La clave de IA |
| A4 | La integridad del documento y de las copias |
| A5 | El canal de actualizaciones, la identidad del editor y la llave de firma del manifiesto |
| A6 | La privacidad de uso (qué apps se usan) |
| A7 | La cadena de compilación |
| A8 | La copia protegida del componente de sistema |

## Límites de confianza

- Entre procesos del mismo usuario (integridad media).
- Entre integridad media y alta (UIPI).
- Entre la carpeta escribible por el usuario y `%ProgramFiles%`.
- Entre el equipo y la red.
- Entre el contenido importado y el documento.
- Entre un PR de un *fork* y la CI.
- Entre GitHub y la firma del manifiesto.

## Amenazas y controles

| # | Amenaza | Vector | Controles | Riesgo residual | Decisión |
|---|---|---|---|---|---|
| T1 | Elevación mediante un perfil malicioso | Perfil compartido con App `cmd /c`, Web `file://` o una macro | Vista previa obligatoria con las acciones de riesgo desmarcadas y confirmadas una a una; App sin intérprete (se rechazan `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.wsf`, `.scr` y `.lnk` a intérpretes); Web solo http(s); rutas UNC con confirmación; límites de tamaño, profundidad y recuento | Aceptación deliberada del usuario | [ADR-0017](../adr/0017-sin-plugins-de-codigo.md) |
| T2 | Suplantación de la IPC | Pipe ocupado o cliente malicioso | DACL con el SID, rechazo remoto, `FIRST_PIPE_INSTANCE`, verificación de PID, ruta y firma en ambos sentidos, **sin verbos que inyecten** | Mostrar el panel o una vista previa | [ADR-0010](../adr/0010-ipc-minima.md) |
| T3 | Elevación por la tarea de inicio | Tarea `Highest` que apunta a un binario escribible; sustitución de archivos durante la sincronización | La tarea solo apunta a `%ProgramFiles%\Clicalo\Clicalo.Launcher.exe` (prueba de ACL); el lanzador verifica **después** de copiar a una ubicación protegida, contra un catálogo firmado y Authenticode; `minSafeVersion` monótono; sin argumentos | Un proceso medio del mismo usuario puede arrancar el Clícalo oficial elevado; UIPI y la IPC sin verbos que inyecten impiden que lo controle. Un documento manipulado por ese *malware* sigue sujeto a T1 y a la exigencia de un toque del usuario | [ADR-0009](../adr/0009-elevacion-y-componente-de-sistema.md) |
| T4 | Elevación por UIA con uiAccess (M7) | *Malware* de integridad media edita el documento e invoca un botón con una app elevada en primer plano | Hacia destinos elevados solo se acepta entrada de hardware (`IMO_HARDWARE`) o una opción explícita del usuario | La opción explícita, documentada | [ADR-0009](../adr/0009-elevacion-y-componente-de-sistema.md) |
| T5 | Manipulación de la actualización | Cuenta de GitHub, workflow o *feed* comprometidos | Manifiesto ECDSA firmado **solo** con una llave de hardware fuera de GitHub (PIN y toque); la CI no tiene acceso a la clave; `release-publish` verifica con las claves fijadas; anti-rollback del manifiesto, anti-freeze, `rollbackAllowed`; Authenticode con editor fijado (SignPath aprueba con su propio MFA); hash por archivo | Compromiso simultáneo del equipo del mantenedor, su PIN y el toque físico, **y** de la firma de código | [ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md) |
| T6 | Binarios manipulados en `%LocalAppData%` | *Malware* del mismo usuario | Firma verificada antes de elevar con UAC; la ejecución elevada sin UAC solo usa la copia protegida; `SetDefaultDllDirectories(APPLICATION_DIR \| SYSTEM32)`; autocontenido | Se acepta en la ejecución no elevada: es el mismo usuario | [ADR-0009](../adr/0009-elevacion-y-componente-de-sistema.md) |
| T7 | Filtración por los registros | Paquete de diagnóstico, *issues* | Tipos sensibles, analizador CLC0003, canarios, vista previa exacta | — | [ADR-0008](../adr/0008-secretos-dpapi-y-administrador-de-credenciales.md) |
| T8 | Filtración o abuso vía IA | Inyección de *prompt* o datos de más | **Exactamente 4 datos** (PLA-008) y prueba de que no sale nada más; validación con esquema; solo Pulsar; filtrado y marcado de combinaciones | Nombres engañosos, visibles en la vista previa | [ADR-0014](../adr/0014-ia-con-clave-propia.md) |
| T9 | Percepción de *keylogger* por el antivirus | *Hook* de bajo nivel | *Hook* de teclado temporal, con indicador visible, 30 s como máximo y sin registrar nada; seguimiento del puntero por WinEvent (y `WH_MOUSE_LL` pasivo solo como repliegue); binario firmado, sin empaquetadores | — | [ADR-0006](../adr/0006-capa-de-punteros-propia.md) |
| T10 | Denegación de servicio | Documento enorme, `clicalo://` gigante, *spam* por IPC | Límites de tamaño, 4 instancias de pipe, 2 s de tiempo máximo y 10 peticiones por segundo | — | [ADR-0007](../adr/0007-documento-json-versionado.md), [ADR-0010](../adr/0010-ipc-minima.md) |
| T11 | Portapapeles | Historial o nube del portapapeles | Formatos de exclusión y restauración condicionada al número de secuencia | Lecturas de terceros durante 500 ms | — |
| T12 | Cadena de suministro | Paquete o acción comprometidos | *Lock files* en modo bloqueado, `packageSourceMapping`, `trustedSigners`, NuGetAudit, acciones fijadas por SHA, Renovate con revisión y sin fusión automática en dependencias de runtime, Scorecard, SBOM, atestación, commits firmados | Dependencias con un solo mantenedor (planes de salida en `docs/architecture/dependencies.md`, previsto) | [ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md) |
| T13 | Abuso del proxy de IA | Cuota gratuita usada como LLM genérico | **No aplica en la 2.0** (proxy diferido). Cuando exista: *prompt* en el servidor, entrada enumerada, límites, presupuesto con corte, interruptor firmado | — | [ADR-0014](../adr/0014-ia-con-clave-propia.md) |
| T14 | PR malicioso en la CI | *Fork* | Sin secretos, sin `pull_request_target`, laboratorio solo con etiqueta y aprobación, CodeQL | — | — |
| T15 | Hilo del motor zombi | Cuelgue y reanudación tras la emergencia | Valla de generación bajo *lock*; escalada a reinicio del proceso si no se puede tomar la valla | — | [ADR-0004](../adr/0004-motor-ledger-valla-y-sentinel.md) |

### Nota sobre T9: repliegue `WH_MOUSE_LL` pasivo

Si el spike S15 muestra huecos en el seguimiento del puntero por WinEvent (por ejemplo, apps que no generan
el evento de cursor), se activa un `WH_MOUSE_LL` **pasivo y permanente** en el hilo Hook, que solo copia la
posición a una variable atómica y llama a `CallNextHookEx`
([§7.11 del plano](../architecture/blueprint.md#711-posición-del-puntero-para-acciones-de-mouse-eje-009)).

- **Coste:** una llamada por cada movimiento del mouse en el sistema.
- **Riesgo con antivirus:** bajo, porque es de mouse, el binario está firmado y no registra nada.

### Nota sobre T12 y la visibilidad del repositorio

Mientras el repositorio sea privado, CodeQL y Scorecard no se ejecutan en cada PR (ver
[deviations.md, D-04](../architecture/deviations.md#d-04--repositorio-privado-al-inicio)). El resto de
controles de T12 y T14 se mantienen.

## Riesgos residuales aceptados fuera de la tabla

- **Intervalo sin guardián al arrancar** (menos de 200 ms, medido en S9): lo cubren `EngineHost`,
  `EmergencyReleaser` y el soltado preventivo del siguiente arranque.
- **«Finalizar árbol de procesos»** mata a la vez a `Clicalo.exe` y a Sentinel: lo cubre el soltado
  preventivo del siguiente arranque. No se usan trucos de *re-parenting*, que son frágiles y los antivirus
  tratan como sospechosos.

## Cuándo se actualiza

- En cada ADR que toque un límite de confianza, un formato persistido o un contrato público.
- Cuando un spike cambie un control (por ejemplo S10, S13, S14 o S15).
- En la revisión completa del hito M6, antes de la 2.0.0.

Documentos previstos que completarán este modelo: `docs/runbooks/key-compromise.md` (compromiso o pérdida de
la llave de firma), `docs/runbooks/bad-release.md` y `docs/architecture/dependencies.md` (planes de salida
de las dependencias con un solo mantenedor).
