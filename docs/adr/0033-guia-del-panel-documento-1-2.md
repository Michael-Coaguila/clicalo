---
status: Propuesto
date: 2026-10-23
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario, pendiente de que ratifique R-38
consulted: revisión pantalla por pantalla D21 (catálogo §6.2), su hallazgo de bienvenida más grave; requisito PES-015 y propuesta R-38 (§6.1); ADR-0007, ADR-0018 y ADR-0028 (formato del documento)
informed: colaboradores y agentes de M7, mediante este ADR, `data/schemas/document.schema.json` y el catálogo
---

# ADR-0033 · Guía de primera vez del panel: documento 1.2 con `settings.panelCoachDone`

## Contexto y planteamiento del problema

La revisión D21 encontró que la única guía de primera vez es la de la Pestaña (PES-015). Quien elige la
vista Completa o la Compacta en la bienvenida, que es lo primero que ve casi todo el mundo, no recibe
ninguna orientación, y «Ver la guía de la pestaña» de General no hace nada por él. La propuesta R-38 añade
una guía de tres pasos junto al panel, con la misma forma que la de la Pestaña.

Como la de la Pestaña, la guía sale una vez, se puede omitir y no vuelve a salir al terminarla. Eso exige
recordar entre reinicios que se terminó, y el documento 1.1 solo guarda `dock.coachDone`, que es el de la
Pestaña. ¿Dónde se guarda que la guía del panel terminó, sin perder datos (REG-08) y sin romper la lectura
de los documentos escritos ni la de una versión anterior que lea uno nuevo?

## Factores de decisión

- REG-08 y DAT-003: ningún documento existente deja de leerse y ningún valor dañado impide cargar.
- ADR-0007 y ADR-0028: un *minor* es aditivo y una versión anterior con el mismo *major* conserva lo que no
  conoce al reescribir, pero solo en la raíz, en `settings`, en el perfil, en el atajo y en `onboarding`,
  que tienen `[JsonExtensionData]`; `dock` no lo tiene.
- Las dos guías son independientes: quien terminó la de la Pestaña y pasa a la vista Completa necesita la
  del panel, y al revés.
- «Ver la guía» de General vuelve a poner el indicador en falso con `SetSetting`, como ya hace con
  `dock.coachDone`: el indicador debe ser un ajuste con su descriptor.
- Simplicidad: nada de migraciones si basta con un miembro opcional.

## Opciones consideradas

- Documento **1.2** con el miembro opcional `settings.panelCoachDone`.
- Reutilizar `dock.coachDone` para las dos guías.
- Un miembro nuevo dentro de `dock` o en `onboarding`.
- No guardarlo: la guía sale en cada arranque hasta que se omite en esa sesión.

## Resultado de la decisión

Opción elegida: **«Documento 1.2 con `settings.panelCoachDone`»**, porque es aditiva (sin migración ni copia
`pre-migrate`), la lee cualquier versión 1.x y separa las dos guías.

`DocumentFormats.DocumentSchema` pasa de 1.1 a **1.2**.

| Miembro persistido | Domain | Por defecto | Validación | Ámbito |
|---|---|---|---|---|
| `settings.panelCoachDone`: booleano | `UserSettings.PanelCoachDone` (`SettingPaths.PanelCoachDone`) | `false`: la guía sale la primera vez que el panel está a la vista | Booleano, como el resto de ajustes | Presentación, sin deshacer (como `dock.coachDone`) |

Detalles:

- **Lectura de documentos anteriores.** Un documento 1.0 o 1.1 se lee con `panelCoachDone` en falso. Quien
  ya usaba la vista Completa o la Compacta ve la guía una vez después de actualizar; se cierra con un toque y
  no vuelve. Se acepta porque la 2.0 aún no se ha publicado.
- **Versión anterior que lee un 1.2.** El miembro cuelga de `settings`, que tiene `[JsonExtensionData]`, así
  que una 1.1 lo conserva al reescribir. Por eso no va dentro de `dock`.
- **Escritura.** Siempre se escribe, como `dock.coachDone`.
- **Quién lo escribe.** Terminar u omitir la guía lo pone en verdadero. «Ver la guía del panel» de General lo
  pone en falso, y la guía sale junto al panel en cuanto está a la vista. El estado del paso en pantalla no se
  guarda: vive en `InteractionState`, como el de la Pestaña.

### Consecuencias

- Buena, porque las dos guías se recuerdan por separado y General puede volver a mostrar cualquiera de las dos.
- Buena, porque no hay migración: una vuelta atrás a una 1.1 conserva el indicador.
- Neutral, porque a una persona que ya conoce el panel se le muestra la guía una vez tras actualizar.
- Mala, porque el formato cambia por un solo booleano. Se acepta frente a mezclar las dos guías en un único
  indicador.

### Confirmación

- `SchemaMinor2Tests`: el *fixture* inmutable `Fixtures/schema/1.2/document.json` se lee con su *hash* y sin
  miembros desconocidos; un documento 1.1 se lee con `panelCoachDone` en falso; el indicador sobrevive a la ida
  y vuelta; y una versión 1.1 lo conserva como miembro desconocido de `settings`.
- `SchemaValidationTests`: los *fixtures* 1.0, 1.1 y 1.2 validan contra `document.schema.json`.
- Pruebas de la guía con `[Trait("Req", "PES-015")]`: sale con el panel a la vista, se omite, se termina y no
  vuelve, y General la vuelve a mostrar.

## Pros y contras de las opciones

### Documento 1.2 con `settings.panelCoachDone`

- Buena, porque cumple ADR-0007 tal cual: un *minor* aditivo que lee cualquier 1.x.
- Buena, porque reutiliza el camino de los ajustes: descriptor, valor por defecto y `SetSetting` desde General.
- Mala, porque el indicador queda en `settings` y no junto al de la Pestaña, en `dock`.

### Reutilizar `dock.coachDone`

- Buena, porque no cambia el formato.
- Mala, porque quien terminó una guía nunca vería la otra, y «Ver la guía» mostraría las dos a la vez.

### Un miembro dentro de `dock` o en `onboarding`

- Buena, en `dock`, porque quedaría junto al indicador de la Pestaña.
- Mala, porque `dock` no tiene `[JsonExtensionData]` y una versión 1.1 perdería el miembro al reescribir.
- Mala, en `onboarding`, porque no es un ajuste: General no podría volver a ponerlo en falso con `SetSetting`
  y quedaría fuera de los descriptores.

### No guardarlo

- Buena, porque no toca el formato.
- Mala, porque la guía saldría en cada arranque: incumple «una vez» y molesta a quien ya la terminó.

## Criterios de reapertura

- Que haya más guías de primera vez (por ejemplo, del centro de control) y convenga guardarlas juntas en un
  solo miembro.
- Que el usuario no ratifique R-38: el miembro se queda sin uso y se retira en el siguiente *minor*.

## Más información

- [ADR-0007](0007-documento-json-versionado.md), [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md) y
  [ADR-0028](0028-ajustes-persistidos-de-m6.md): envoltorio, *minor* aditivo y miembros desconocidos.
- Catálogo: PES-015, D21 (§6.2) y la propuesta R-38 (§6.1).
- `data/schemas/document.schema.json`.
