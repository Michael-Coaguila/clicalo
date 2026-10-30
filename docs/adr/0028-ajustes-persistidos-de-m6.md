---
status: Aceptado
date: 2026-10-09
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario
consulted: decisiones D9 y D10 del usuario del 2026-10-09 (catálogo §6.2); ADR-0007 y ADR-0018 (formato del documento); requisitos PES-016, CCM-001, BUR-005, ACC-006 y BIE-010
informed: colaboradores y agentes de M6, mediante este ADR, `data/schemas/document.schema.json` y `data/catalogs/README.md`
---

# ADR-0028 · Ajustes persistidos de M6: documento 1.1 con campos opcionales

## Contexto y planteamiento del problema

El último hito (M6) cierra cinco requisitos que necesitan recordar algo entre reinicios y que el documento
1.0 no guarda:

- **PES-016** (MUST): la posición del asa de la Pestaña por monitor y por lado. La decisión D9 del usuario
  lo confirma.
- **CCM-001** (MUST): el centro de control recuerda su tamaño; D9 añade el monitor.
- **BUR-005** (MUST): un atajo global opcional para mostrar u ocultar el panel. La decisión D10 lo deja
  **apagado** por defecto, con una combinación elegida de una **lista cerrada**.
- **ACC-006** (SHOULD): la confirmación de dos toques y la duración de los avisos se alargan ×1, ×2 o ×3.
- **BIE-010** (SHOULD): una bienvenida repetida muestra preseleccionadas las respuestas guardadas y aplica
  los efectos del paso 1 solo a los ajustes que nadie cambió a mano después.

Cinco paquetes de trabajo van a implementar esos comportamientos en paralelo. Si cada uno añadiera su
campo, el formato cambiaría cinco veces y chocarían en los mismos archivos (DTO, mapeador, esquema y
*fixtures*). ¿Cómo se añaden de una vez, sin perder datos (REG-08) y sin romper la lectura de los
documentos ya escritos ni la de una versión anterior que lea uno nuevo?

## Factores de decisión

- REG-08 y DAT-003: ningún documento existente deja de leerse y ningún valor dañado impide cargar.
- ADR-0007: un *minor* es un cambio aditivo; una versión anterior con el mismo *major* conserva los miembros
  que no conoce al reescribir (`[JsonExtensionData]` en la raíz, los ajustes, el perfil, el atajo y
  `onboarding`, pero **no** en grupos como `dock`).
- DAT-006 (§6.3 y §6.4 del plano): las posiciones son *placement* y nunca se deshacen; los ajustes de
  comportamiento sí.
- Un solo cambio de formato para los cinco paquetes, con los valores por defecto y la validación en el
  Domain (`SettingsSchema`), como el resto de ajustes.
- Simplicidad: nada de migraciones si basta con miembros opcionales.

## Opciones consideradas

- Documento **1.1** con miembros opcionales en `settings` y `onboarding`.
- Un documento 2.0 con migración.
- Un archivo aparte para la colocación de ventanas (como `usage.json`).
- Que cada paquete añada su campo cuando lo necesite.

## Resultado de la decisión

Opción elegida: **«Documento 1.1 con miembros opcionales en `settings` y `onboarding`»**, porque es
aditiva (no hay migración ni copia `pre-migrate`), la lee cualquier versión 1.x y deja el formato cerrado
antes de que empiecen los paquetes.

`DocumentFormats.DocumentSchema` pasa de 1.0 a **1.1**. Un documento 1.0 se lee con los valores por defecto
de los miembros nuevos; una versión 1.0 que lea un 1.1 conserva esos miembros al reescribirlo, porque todos
cuelgan directamente de `settings` o de `onboarding`, que tienen `[JsonExtensionData]`. Por eso la posición
del asa por monitor no va dentro de `dock`, que no lo tiene.

| Miembro persistido | Domain (`UserSettings` u `OnboardingState`) | Por defecto | Validación al cargar o escribir | Ámbito |
|---|---|---|---|---|
| `settings.handlePosByMonitor`: `[{monitor, side, pos}]` | `HandlePositionsByMonitor`: `ValueList<MonitorHandlePosition>` | `[]` | Sin monitor vacío ni lado desconocido; una entrada por monitor y lado (gana la primera); `pos` dentro de 8–92 | *Placement* |
| `settings.controlCenter`: `{monitor, x, y, width, height, maximized}` en DIP | `ControlCenter`: `ControlCenterPlacement?` | Ausente (`null`): se abre a 1120×680 | Monitor no vacío, números finitos y tamaño positivo; si no, se olvida | *Placement* |
| `settings.globalHotkey`: `{enabled, combo}` | `GlobalHotkey`: `GlobalHotkeySettings(Enabled, Combo)` | `enabled: false`, `combo` = la primera de la lista | `combo` debe ser un id de `data/catalogs/global-hotkeys.json`; si no, la primera | Comportamiento, se deshace |
| `settings.timeMultiplier` | `TimeMultiplier`: `int` | `1` | Entre 1 y 3 | Comportamiento, se deshace |
| `onboarding.uses`, `onboarding.kit`, `onboarding.baseline` | `OnboardingState.Answers`: `WelcomeAnswers?` | Ausente (`null`) | Solo se leen con un `baseline` completo; se descartan las opciones desconocidas y los ids vacíos | Fuera del historial (como `FinishOnboarding`) |

Detalles:

- **Identificador del monitor.** Una cadena opaca que da la plataforma y que el Domain solo compara. Debe
  ser estable entre reinicios y cambios de orden de las pantallas: la ruta del dispositivo del monitor de
  DisplayConfig (`DISPLAYCONFIG_TARGET_DEVICE_NAME.monitorDevicePath`), no el nombre GDI `\\.\DISPLAY1` que
  usa `panelPositions`. Si el monitor no se reconoce, `MonitorHandlePositions.PositionFor` devuelve la
  posición por lado que el documento 1.0 ya tenía (`dock.handlePosBySide`).
- **Lista cerrada del atajo global.** Es un dato: `data/catalogs/global-hotkeys.json`, con su criterio en
  `criteria` y su esquema. Cinco combinaciones: Ctrl+Alt+Espacio (la preseleccionada), Ctrl+Alt+Mayús+Espacio,
  Ctrl+Alt+F8, Ctrl+Alt+F10 y Ctrl+Alt+F11. Criterio: sin la tecla Windows; nunca Ctrl+Alt con una letra, un
  número o un símbolo, porque en las distribuciones en español y latinoamericana Ctrl+Alt es AltGr y escribe
  caracteres (@, #, €); ninguna de las que gestionan Windows o Escritorio remoto (Ctrl+Alt+Supr,
  Ctrl+Mayús+Esc, Ctrl+Alt+Inicio, Fin, Pausa y flechas) ni Ctrl+Alt+F12; ninguna de las Ctrl+Alt+F que usa
  Office (F1 y F2 en Word, F5 y F9 en Excel); nunca Ctrl+Mayús+M (Silenciar en Teams, BUR-005); y ninguna
  bloqueada o especial de `blocked-combos.json` ni de las que envían la semilla, la biblioteca o las
  plantillas, cosa que comprueba una prueba. Si otra app ya tiene registrada la combinación, registrarla
  falla y el producto lo dice y ofrece elegir otra de la lista. El Domain la expone en `GlobalHotkeys`, y una
  prueba compara las dos listas.
- **Respuestas de la bienvenida.** `WelcomeAnswers(Uses, Kit, Baseline)`: las opciones marcadas del paso 1,
  los ids marcados del paso 2 y `WelcomeBaseline`, los valores que la bienvenida dejó al terminar en los
  cuatro ajustes que cambia el paso 1 (`touch.preset`, `size`, `voiceNumbers` y `noKeyboardUser`). Una
  bienvenida repetida solo cambia los ajustes cuyo valor actual sigue siendo el del *baseline*, y avisa de
  los demás. Se guardan con `FinishOnboarding { Answers = … }`, que ahora conserva las respuestas
  anteriores cuando no recibe otras.
- **Textos.** Los descriptores reutilizan [handlePos], [cc] y [keys], y añaden [globalHotkeyT],
  [globalHotkeyD], [timeMultiplierT] y [timeMultiplierD], que quedan pendientes de ratificar en §6.1 del
  catálogo.

### Consecuencias

- Buena, porque los cinco paquetes de M6 consumen campos ya persistidos, con sus valores por defecto, su
  validación y sus descriptores (deshacer, ámbito y textos), sin tocar el formato.
- Buena, porque no hay migración: ni copia `pre-migrate` ni riesgo de pérdida. Una vuelta atrás a una
  versión 1.0 conserva los miembros nuevos.
- Neutral, porque la posición del asa vive en dos sitios: por lado en `dock.handlePosBySide`, que es la de
  reserva, y por monitor y lado en `handlePosByMonitor`.
- Mala, porque la lista cerrada del atajo global no se ha contrastado todavía con la documentación oficial
  de cada programa (sin red en este cambio); se apoya en el criterio escrito y en el fallo controlado si la
  combinación ya está ocupada. El paquete de BUR-005 puede cambiar la lista antes de publicar la 2.0: los
  ids no se han publicado aún.

### Confirmación

- `SchemaMinor1Tests`: el *fixture* inmutable `Fixtures/schema/1.1/document.json` se lee con su *hash*, sin
  miembros desconocidos y sin reparaciones; un documento 1.0 se lee con los valores por defecto; cada campo
  sobrevive a la ida y vuelta, y los valores dañados se reparan o se descartan.
- `M6SettingsTests`, `GlobalHotkeysTests` y `SettingsSchemaTests`: valores por defecto, ámbitos, deshacer,
  rangos y reparación, con `[Trait("Req", …)]` de PES-016, CCM-001, BUR-005, ACC-006 y BIE-010.
- `GlobalHotkeyCatalogTests` y `SchemaValidationTests`: la lista cumple los criterios comprobables y los
  *fixtures* 1.0 y 1.1 validan contra `document.schema.json`.

## Pros y contras de las opciones

### Documento 1.1 con miembros opcionales

- Buena, porque cumple ADR-0007 tal cual: un *minor* aditivo que lee cualquier 1.x.
- Buena, porque todo queda en un solo cambio, con un solo *fixture* nuevo.
- Mala, porque obliga a poner los miembros nuevos donde una versión anterior los conserva, aunque
  semánticamente pertenezcan a `dock`.

### Documento 2.0 con migración

- Buena, porque permitiría reordenar (meter la posición por monitor en `dock`).
- Mala, porque una versión 1.0 no podría escribir el documento (solo lectura), haría falta copia
  `pre-migrate` y una cadena de migraciones para algo puramente aditivo.

### Archivo aparte para la colocación de ventanas

- Buena, porque la colocación no entraría en las copias ni en el deshacer.
- Mala, porque añade un formato persistido nuevo con su envoltorio, su recuperación y sus pruebas, y no
  resuelve el atajo global, los tiempos ni la bienvenida.

### Que cada paquete añada su campo

- Buena, porque cada campo llegaría con su comportamiento.
- Mala, porque serían cinco cambios de formato en paralelo sobre los mismos archivos, con conflictos y
  riesgo de pisarse.

## Criterios de reapertura

- Que haya que guardar la colocación de otra ventana o un dato por monitor que no quepa como miembro
  opcional de `settings`.
- Que la lista del atajo global necesite combinaciones que la persona escriba libremente (D10 la cierra).

## Más información

- [ADR-0007](0007-documento-json-versionado.md) y [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md):
  envoltorio, *minor* aditivo y miembros desconocidos.
- [Plano §6.3 a §6.6](../architecture/blueprint.md): descriptores, deshacer, persistencia y *fixtures*.
- Catálogo: PES-016, CCM-001, BUR-005, ACC-006, BIE-010 y las decisiones D9 y D10 de §6.2.
- `data/schemas/document.schema.json`, `data/catalogs/global-hotkeys.json` y
  `data/schemas/global-hotkeys.schema.json`.
