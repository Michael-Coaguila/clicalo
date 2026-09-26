---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuestas de fiabilidad y seguridad
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0010 · IPC mínima entre instancias: solo `Show`, `OpenUri` e `ImportFile`

## Contexto y planteamiento del problema

En Macro Quick Access no había instancia única: dos procesos inyectaban a la vez (lección L-SEG-3). El
catálogo exige una instancia por sesión de usuario sin doble inyección (SIS-003, NFR-018), que la
comunicación entre instancias quede restringida al usuario (NFR-009) y que no acepte órdenes de otros
usuarios ni de procesos menos privilegiados (LOG-007). Dos procesos serían dos motores y dos *ledgers*
sobre el mismo teclado, así que la instancia única es obligatoria (propuesta P2 sobre la fila «Una sola
ventana»).

Una segunda ejecución tiene que poder pedir a la primera que muestre el panel, abrir un enlace
`clicalo:` o abrir un archivo compartido. Un canal de comunicación, sin embargo, es una superficie de
ataque: otro proceso podría ocupar el nombre del canal o enviar órdenes. ¿Qué puede hacer la IPC y cómo
se protege?

## Factores de decisión

- Una sola instancia por sesión WTS: un único dueño del teclado.
- La peor suplantación posible debe ser inofensiva.
- Verificación en los dos sentidos: el cliente no confía en un servidor impostor y el servidor no
  confía en un cliente de otro usuario o de menor integridad.
- Resistencia a denegación de servicio (amenaza T10).
- Contratos compatibles con AOT, porque los comparte `Clicalo.Platform.Core`.

## Opciones consideradas

- Pipe con DACL, verificación en ambos sentidos y solo tres verbos que no inyectan
- Una IPC rica, con verbos de automatización (ejecutar atajos, editar el documento)
- Permitir varias instancias

## Resultado de la decisión

Opción elegida: **«IPC mínima con tres verbos que no inyectan»**, porque la peor suplantación posible
solo muestra el panel o una vista previa.

- **Nombres:** `sidHash` son los 16 primeros caracteres hexadecimales de `SHA-256(UserSid)`. El mutex es
  `Local\Clicalo.{sidHash}.Instance` (una instancia por sesión) y el pipe
  `\\.\pipe\Clicalo.{sidHash}.{sessionId}`.
- **Servidor:** `FILE_FLAG_FIRST_PIPE_INSTANCE`, `PIPE_REJECT_REMOTE_CLIENTS`, mensajes de 16 KiB como
  máximo, 4 instancias, 2 s de tiempo máximo y 10 peticiones por segundo. DACL solo para el SID del
  usuario, con acceso denegado a la red. Si el servidor está elevado, SACL con etiqueta de integridad
  media para que un cliente medio pueda entregar `Show`.
- **Cliente:** comprueba con `GetNamedPipeServerProcessId` que el servidor es una de las rutas propias
  (instalación por usuario o copia protegida del componente) **y** que su editor es el fijado. Si no,
  alguien ocupó el nombre: no envía nada, registra `ipc.squat_detected` y avisa.
- **Servidor:** comprueba que el cliente está en la misma sesión y con el mismo SID, y lee su integridad.
  Un cliente de menor integridad solo puede pedir `Show`.
- **Verbos:** `Show`; `OpenUri`, solo con esquema `clicalo:` y 8 KiB como máximo; e `ImportFile`, que
  abre una **vista previa** y nunca aplica nada. Todos llevan `ProtocolVersion`.
- **Invariante D13:** `Clicalo.Application.Ipc` solo depende de `IShellNavigator`; ArchUnit le prohíbe
  depender de `Clicalo.Application.Engine` y de `Clicalo.Application.Foreground`.

### Consecuencias

- Buena, porque ni un pipe ocupado ni un cliente malicioso pueden inyectar teclas ni cambiar datos.
- Buena, porque importar desde fuera siempre pasa por la vista previa, con las acciones de riesgo
  desmarcadas (LOG-006, LOG-008).
- Mala, porque no hay API de automatización para usuarios avanzados; añadirla exigiría un ADR nuevo con
  su modelo de amenazas.
- Neutral, porque la fila «Una sola ventana» del prototipo queda pendiente de la propuesta P2; mientras
  no se ratifique, la fila no se construye y el comportamiento es el de la opción activada, que es su
  valor por defecto.

### Confirmación

- Spike S10: servidor elevado con SACL media frente a cliente medio, y ocupación del pipe. Detección en
  10 de 10; el cliente medio solo puede pedir `Show`.
- ArchUnit: regla D13 sobre `Application.Ipc`.
- Los contratos públicos (CLI, `clicalo://`, IPC) se documentarán en `docs/architecture/contracts.md`;
  romperlos implica versión mayor del producto.

## Pros y contras de las opciones

### IPC mínima con tres verbos que no inyectan

- Buena, porque reduce la superficie de ataque a mostrar el panel o una vista previa.
- Mala, porque limita la integración con herramientas externas.

### IPC rica para automatización

- Buena, porque permitiría integrar Clícalo con otras herramientas.
- Mala, porque cualquier código del mismo usuario (o de menor integridad, si no se filtra bien) podría
  ordenar inyecciones, también hacia apps elevadas si Clícalo lo está.

### Varias instancias

- Buena, porque evita el canal por completo.
- Mala, porque serían dos motores y dos *ledgers* sobre el mismo teclado: el defecto que ya tuvo la v1.

## Criterios de reapertura

Cualquier verbo nuevo que inyecte entrada, modifique el documento o cambie el primer plano exige un ADR
nuevo que sustituya a este.

## Más información

- Plano: [§1.2 (D13)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§1.4 (P2)](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario),
  [§3.4](../architecture/blueprint.md#34-instancia-única-e-ipc).
- Catálogo: SIS-003 en [§2.24](../requirements/catalog.md#224-sis--sistema-pestañas-e-inicio-y-estabilidad);
  NFR-009 y NFR-018 en [§3](../requirements/catalog.md#3-requisitos-no-funcionales); LOG-007 en
  [§2.31](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad); PQ-35 en
  [preguntas abiertas](../requirements/catalog.md#6-preguntas-abiertas).
- Seguridad: amenazas T2 y T10 en el [modelo de amenazas](../security/threat-model.md).
- ADR relacionados: [ADR-0004](0004-motor-ledger-valla-y-sentinel.md),
  [ADR-0009](0009-elevacion-y-componente-de-sistema.md).
