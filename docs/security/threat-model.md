# Modelo de amenazas

Documento vivo del modelo de amenazas de Clícalo. Parte de
[§12 del plano](../architecture/blueprint.md#12-seguridad-y-privacidad) y recoge lo que cambiaron los ADR
posteriores. **Revisión completa del hito M6 (2026-10-10), para la versión 2.0.0.**

Para informar de una vulnerabilidad, sigue [SECURITY.md](../../SECURITY.md). Lo que sale del equipo está en
[privacidad](privacy.md).

## Qué cambió respecto al plano

La 2.0 es más pequeña que la que describe §12 del plano. Cuatro decisiones del usuario lo explican:

| Decisión | Qué quita o cambia | Registro |
|---|---|---|
| D5 · IA solo con la clave propia | No hay servidor ni cuota gratuita: T13 no aplica | [ADR-0014](../adr/0014-ia-con-clave-propia.md) |
| D6 · Versión sin firma de código | No hay Authenticode, manifiesto ECDSA ni llave de hardware: T5 queda sin su control principal | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| D7 · «Reabrir como administrador» bajo demanda | No hay componente de sistema, lanzador elevado ni tarea programada: T3 desaparece | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| D8 · Aplazados | No hay enlace `clicalo://` (DAT-008) ni gancho global de mouse (FIJ-007) | Catálogo §6.2 |

Y dos caminos nuevos hacia fuera de Clícalo, los dos en ADR-0029 ([índice de ADR](../adr/README.md)): abrir la
app de correo para «Enviar por correo» (T16) e iniciar el desinstalador desde Sistema (T17).

## Por qué importa

Clícalo inyecta entrada de teclado y mouse en otras apps y, cuando la persona lo pide, se ejecuta elevado.
Quien lo controle controla el teclado del usuario, a veces también en apps de administrador. Por eso cada
límite de confianza tiene su control y su prueba.

En la 2.0 Clícalo **no** tiene uiAccess, **no** instala nada fuera de la carpeta del usuario y **no** instala
*hooks* globales de teclado ni de mouse: «Grabar con teclado» lee las teclas de la ventana del Centro de
control mientras tiene el foco, y el atajo global opcional (D10) usa `RegisterHotKey` con una combinación de
una lista cerrada.

## Activos

| Id | Activo |
|---|---|
| A1 | La capacidad de inyectar entrada, también en apps elevadas mientras Clícalo esté elevado |
| A2 | Los textos guardados |
| A3 | La clave de IA del usuario |
| A4 | La integridad del documento y de las copias |
| A5 | El canal de actualizaciones: en la 2.0, la cuenta de GitHub que publica las versiones |
| A6 | La privacidad de uso (qué apps se usan) |
| A7 | La cadena de compilación y el equipo del mantenedor, donde se empaqueta |
| A8 | La copia instalada (`%LocalAppData%\Clicalo.App`), la única que se eleva o se desinstala |

## Límites de confianza

- Entre procesos del mismo usuario (integridad media).
- Entre integridad media y alta (UIPI), que solo se cruza con «Reabrir como administrador» y la confirmación
  de UAC.
- Entre la copia instalada y cualquier otra copia del ejecutable.
- Entre el equipo y la red: las GitHub Releases y el proveedor de IA que elige el usuario.
- Entre el contenido importado (perfiles, copias, respuestas de la IA) y el documento.
- Entre Clícalo y los programas que abre: el navegador, las apps de los atajos, la app de correo y el
  desinstalador.
- Entre un PR de un *fork* y la CI.

## Amenazas y controles

| # | Amenaza | Vector | Controles en la 2.0 | Riesgo residual | Decisión |
|---|---|---|---|---|---|
| T1 | Elevación mediante un perfil malicioso | Perfil compartido con App `cmd /c`, Web `file://` o una macro | Vista previa obligatoria con las acciones de riesgo desmarcadas; App sin intérprete (se rechazan `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.wsf`, `.scr` y `.lnk` a intérpretes); Web solo http(s); rutas UNC con confirmación; límites de tamaño, profundidad y recuento; importar nunca ejecuta | Aceptación deliberada del usuario | [ADR-0017](../adr/0017-sin-plugins-de-codigo.md) |
| T2 | Suplantación de la IPC | Pipe ocupado o cliente malicioso | DACL con el SID; el servidor exige la misma sesión y el mismo usuario y lee la integridad del cliente; `FIRST_PIPE_INSTANCE`; el cliente comprueba que quien sirve el pipe es uno de los ejecutables de Clícalo y, si no, no envía nada (`ipc.squat_detected`); **el único verbo es `show`**: ninguno inyecta ni edita | Otro proceso del usuario puede mostrar el panel | [ADR-0010](../adr/0010-ipc-minima.md) |
| T3 | Elevación por la tarea de inicio | Tarea `Highest` que apunta a un binario escribible | **No aplica en la 2.0:** no hay tarea programada, lanzador ni componente de sistema. «Iniciar con Windows» es una entrada `Run` del usuario, con el ejecutable instalado entre comillas y sin argumentos, y arranca siempre sin elevación | — | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) (sustituye a [ADR-0009](../adr/0009-elevacion-y-componente-de-sistema.md)) |
| T4 | Elevación por UIA con uiAccess | *Malware* de integridad media invoca un botón con una app elevada delante | **No aplica en la 2.0:** no hay uiAccess. Hacia una app elevada no se envía nada si Clícalo no lo está, y el panel lo dice | — | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| T5 | Manipulación de la actualización | Cuenta de GitHub o *feed* comprometidos | HTTPS hacia las GitHub Releases del repositorio; la suma SHA del paquete que comprueba Velopack; nunca se baja de versión sin pedirlo; copia del documento antes de instalar; una instancia elevada nunca descarga ni instala | **Sin mitigar en la 2.0:** la suma la publica el mismo *feed*, así que quien controle la cuenta de GitHub controla el canal. Aceptado por D6 | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) (sustituye para la 2.0 a [ADR-0013](../adr/0013-firma-de-codigo-y-manifiesto-firmado.md)) |
| T6 | Binarios manipulados en `%LocalAppData%` | *Malware* del mismo usuario cambia `Clicalo.exe` y espera a «Reabrir como administrador» | Antes de pedir UAC se comprueba que el proceso es exactamente la copia instalada, que el archivo existe y que ni él ni ninguna carpeta de su ruta es un enlace; solo se eleva el propio ejecutable y solo cuando la persona lo pide; UAC cada vez; `SetDefaultDllDirectories(APPLICATION_DIR \| SYSTEM32)`; autocontenido | **Aceptado en la 2.0:** sin firma, la comprobación de la ruta no distingue un archivo sustituido, y la ventana de UAC dice «Editor desconocido» también para el legítimo. Sin elevar es el mismo usuario | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| T7 | Filtración por los registros | Registro adjunto a un mensaje, *issues* | Tipos sensibles, analizador CLC0003, canarios; la sección «Acerca de» enseña línea a línea el registro exacto que se adjuntaría, ya depurado | — | [ADR-0008](../adr/0008-secretos-dpapi-y-administrador-de-credenciales.md) |
| T8 | Filtración o abuso vía IA | Inyección de *prompt*, datos de más o robo de la clave | **Exactamente 4 datos** (PLA-008) y una prueba de que no sale nada más; consentimiento la primera vez; respuesta validada con su esquema, solo acciones Pulsar, combinaciones filtradas y marcadas, y nada se instala sin la vista previa; la clave es `SecretText`, vive en el Administrador de credenciales, no se vuelve a mostrar y no llega a un registro | Nombres engañosos, visibles en la vista previa. Otro proceso del mismo usuario puede leer el Administrador de credenciales | [ADR-0014](../adr/0014-ia-con-clave-propia.md) |
| T9 | Percepción de *keylogger* o de *malware* por el antivirus | Inyección de entrada desde un binario sin firma | Sin *hooks* globales de teclado ni de mouse; seguimiento del puntero por WinEvent; `RegisterHotKey` para el atajo global; sin empaquetadores ni ofuscación; código abierto y empaquetado local repetible con `cl package` | **Mayor que en el plano:** sin firma no hay reputación, y SmartScreen avisa al instalar | [ADR-0006](../adr/0006-capa-de-punteros-propia.md), [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| T10 | Denegación de servicio | Documento enorme, copia o perfil gigantes, *spam* por IPC | Límites de tamaño al leer e importar, 4 instancias de pipe, 2 s de tiempo máximo y 10 peticiones por segundo | — | [ADR-0007](../adr/0007-documento-json-versionado.md), [ADR-0010](../adr/0010-ipc-minima.md) |
| T11 | Portapapeles | Historial o nube del portapapeles | Formatos de exclusión y restauración condicionada al número de secuencia | Lecturas de terceros durante 500 ms | — |
| T12 | Cadena de suministro | Paquete o acción comprometidos | *Lock files* en modo bloqueado, `packageSourceMapping`, `trustedSigners`, NuGetAudit, acciones fijadas por SHA, Renovate con revisión y sin fusión automática en dependencias de runtime, Scorecard, CodeQL, commits firmados | Dependencias con un solo mantenedor. El paquete se crea en el equipo del mantenedor, sin atestación de origen | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md) |
| T13 | Abuso del proxy de IA | Cuota gratuita usada como LLM genérico | **No aplica en la 2.0:** no hay proxy ni cuota (D5) | — | [ADR-0014](../adr/0014-ia-con-clave-propia.md) |
| T14 | PR malicioso en la CI | *Fork* | Sin secretos, sin `pull_request_target`, laboratorio solo con etiqueta y aprobación, CodeQL; la CI no publica nada | — | — |
| T15 | Motor colgado con teclas pulsadas | Cuelgue del hilo del motor | «Soltar todo» de la bandeja suelta lo que Windows dice que está pulsado sin el motor; al terminar el proceso, Sentinel hace lo mismo y, con la sesión bloqueada, reintenta hasta que el escritorio lo acepta (D3) | — | [ADR-0023](../adr/0023-guardian-simple.md) |
| T16 | Abuso de la app de correo | Un `mailto:` hacia otro destinatario, con copias, adjuntos o cabeceras, o usado desde un atajo | Entrada propia del lanzador, aparte de la de los atajos: solo `mailto:`, con **un único destinatario que es exactamente el correo fijo del proyecto**, y nada más que asunto y cuerpo; sin correo de proyecto (D11) no se abre nada y el mensaje se copia; los atajos Web siguen aceptando solo http(s); con `--no-input` no existe esa entrada | La app de correo enseña el borrador y es la persona quien lo envía. El cuerpo lleva lo que la vista previa mostró | ADR-0029, ACE-004 |
| T17 | Abuso del desinstalador | Hacer que Clícalo ejecute otro `Update.exe` o borre datos sin querer | Solo se inicia `Update.exe --uninstall` de la carpeta instalada, tras la misma comprobación de T6 (proceso instalado, sin enlaces en la ruta), sin *shell* ni intérprete y sin elevación; una copia no instalada no ofrece nada; borrar los datos es una opción aparte, apagada, que exige guardar antes una copia donde la persona elija (REG-08) | Un `Update.exe` sustituido por *malware* del mismo usuario se ejecutaría sin elevación: no gana nada que no tuviera | ADR-0029, NFR-010 |
| T18 | Instalador falso | Alguien reparte un `Setup.exe` modificado; la persona ya está acostumbrada a pasar el aviso de SmartScreen | Un único origen, las GitHub Releases del repositorio, que la guía de usuario nombra como el único válido; GitHub publica la suma de cada archivo de la versión; instalación por usuario y sin UAC | **Aceptado en la 2.0** hasta que haya firma: SmartScreen no distingue el instalador legítimo de otro | [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md), [guía de usuario](../guides/guia-de-usuario.md#1-instalar) |

### Nota sobre T5, T6 y T18: lo que la firma cerrará

Las tres comparten causa: la 2.0 no lleva firma de código (D6). Cuando el usuario active la firma con SignPath
Foundation ([guía de publicación](../guides/release.md#firmar-más-adelante-con-signpath)):

- T18 baja, porque SmartScreen y el propio instalador muestran el editor.
- T6 se cierra con la comprobación del editor (`WinVerifyTrust`) antes del UAC, además de la ruta instalada.
- T5 sigue necesitando un manifiesto firmado fuera de GitHub para resistir una cuenta comprometida: es el
  criterio de reapertura de [ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md).

### Nota sobre T12 y la visibilidad del repositorio

El repositorio es público desde el 2026-10-05: CodeQL y Scorecard se ejecutan en cada PR
([D-04](../architecture/deviations.md#d-04--repositorio-privado-al-inicio), cerrada).

## Riesgos residuales aceptados fuera de la tabla

- **Intervalo sin guardián al arrancar** (menos de 200 ms): lo cubren `EngineHost`, «Soltar todo» de la
  bandeja y el soltado preventivo del siguiente arranque.
- **«Finalizar árbol de procesos»** mata a la vez a `Clicalo.exe` y a Sentinel: lo cubre el soltado
  preventivo del siguiente arranque. No se usan trucos de *re-parenting*, que son frágiles y los antivirus
  tratan como sospechosos.
- **Una instancia elevada sigue elevada hasta que se cierra.** Mientras tanto envía atajos a apps de
  administrador. No se actualiza, no transfiere estado a la instancia normal y el siguiente inicio de sesión
  arranca sin elevación.

## Cuándo se actualiza

- En cada ADR que toque un límite de confianza, un formato persistido o un contrato público.
- Cuando el usuario active la firma de código.
- Antes de cada versión estable.

Documentos previstos que completarán este modelo: `docs/runbooks/bad-release.md` (qué hacer si se publica
una versión dañada) y `docs/architecture/dependencies.md` (planes de salida de las dependencias con un solo
mantenedor).
