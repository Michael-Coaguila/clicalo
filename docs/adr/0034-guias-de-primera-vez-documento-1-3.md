---
status: Propuesto
date: 2026-10-23
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario, pendiente de que ratifique R-39
consulted: revisión pantalla por pantalla D21 (catálogo §6.2) y su hallazgo de bienvenida más grave; la petición del usuario de una guía interactiva y visual para todas las pantallas; requisito PES-015 y propuesta R-39 (§6.1); ADR-0007, ADR-0018, ADR-0028 y ADR-0033 (formato del documento)
informed: colaboradores y agentes de M7, mediante este ADR, `data/schemas/document.schema.json` y el catálogo
---

# ADR-0034 · Guías de primera vez de cada pantalla: documento 1.3 con `settings.guidesDone`

## Contexto y planteamiento del problema

La revisión D21 encontró que la única guía de primera vez era la de la Pestaña (PES-015). Quien elegía la
vista Completa o la Compacta en la bienvenida no recibía ninguna orientación, y el centro de control se abría
directamente en el editor de un atajo. El usuario pidió después una guía completa, interactiva y visual en
todas las pantallas: el panel en Completa y en Compacta, la Pestaña, la burbuja y cada sección del centro de
control, que señale cada control y espere a que la persona lo pruebe, y que se pueda repetir desde General.
La propuesta R-39 describe esas guías.

Cada guía sale una vez y no vuelve al terminarla u omitirla, así que el documento tiene que recordar qué
guías están hechas. El documento 1.2 (ADR-0033) solo guarda `dock.coachDone`, el de la Pestaña. ¿Cómo se
guardan nueve guías sin perder datos (REG-08), sin romper la lectura de los documentos ya escritos ni la de
una versión anterior que lea uno nuevo, y dejando sitio para guías futuras?

## Factores de decisión

- REG-08 y DAT-003: ningún documento existente deja de leerse y ningún valor dañado impide cargar.
- ADR-0007 y ADR-0028: un *minor* es aditivo, y una versión anterior con el mismo *major* conserva lo que no
  conoce al reescribir, pero solo en la raíz, en `settings`, en el perfil, en el atajo y en `onboarding`;
  `dock` no lo tiene.
- ADR-0033 ya llevó el documento a 1.2 con `settings.aiProvider` y está aceptado (D22): no se edita.
- Las guías son independientes: terminar la de la Pestaña no dice nada del panel, ni la del panel de Atajos.
- General vuelve a mostrarlas con `SetSetting`, como ya hacía con `dock.coachDone`: el indicador debe ser un
  ajuste con su descriptor.
- Habrá más guías (el editor, Probar): añadir una no debe cambiar el formato otra vez.
- Simplicidad: nada de migraciones si basta con un miembro opcional.

## Opciones consideradas

- Documento **1.3** con una lista opcional `settings.guidesDone` de identificadores.
- Un booleano por guía en `settings` (`panelCoachDone`, `bubbleCoachDone`…).
- Reutilizar `dock.coachDone` para todas las guías.
- Guardarlo en `onboarding`.
- No guardarlo: cada guía sale en cada arranque hasta que se omite en esa sesión.

## Resultado de la decisión

Opción elegida: **«Documento 1.3 con la lista `settings.guidesDone`»**, porque es aditiva, la lee cualquier
versión 1.x, separa las guías y admite guías nuevas sin otro cambio de formato.

`DocumentFormats.DocumentSchema` pasa de 1.2 a **1.3**, encima del 1.2 de ADR-0033.

| Miembro persistido | Domain | Por defecto | Validación | Ámbito |
|---|---|---|---|---|
| `settings.guidesDone`: lista de cadenas | `UserSettings.GuidesDone` (`SettingPaths.GuidesDone`), ids de `GuideProgress` | `[]`: todas las guías salen una vez | Cada id una vez, no vacío y de `Timings.Guides.MaxIdLength` (32) caracteres como mucho, hasta `Timings.Guides.MaxDoneIds` (32) ids; un id desconocido se conserva | Presentación, sin deshacer (como `dock.coachDone`) |

Detalles:

- **Identificadores.** `panel`, `bubble`, `shortcuts`, `templates`, `general`, `touch`, `system` y `about`
  (`GuideProgress.Id`). La guía de la Pestaña sigue en `dock.coachDone`, que el documento guarda desde 1.0, así
  que no se duplica el dato.
- **Ids desconocidos.** Se conservan: una versión posterior que añada una guía no la vería salir otra vez tras
  pasar por una versión anterior.
- **Lectura de documentos anteriores.** Un documento 1.0, 1.1 o 1.2 se lee con la lista vacía. Quien ya usaba
  Clícalo ve cada guía una vez después de actualizar; se omite con un toque y no vuelve. Se acepta porque la
  2.0 aún no se ha publicado.
- **Versión anterior que lee un 1.3.** El miembro cuelga de `settings`, que tiene `[JsonExtensionData]`, así que
  una 1.2 lo conserva al reescribir. Por eso no va dentro de `dock`.
- **Quién lo escribe.** Terminar u omitir una guía añade su id; «Ver las guías otra vez» de General vacía la
  lista y pone `dock.coachDone` en falso. El paso en el que va cada guía no se guarda: vive en la sesión.

### Consecuencias

- Buena, porque cada guía se recuerda por separado y una guía nueva solo necesita un id.
- Buena, porque no hay migración: una vuelta atrás a una 1.2 conserva la lista.
- Neutral, porque la Pestaña tiene su indicador aparte, por historia del formato.
- Mala, porque a una persona que ya conoce Clícalo se le muestran las guías una vez tras actualizar.

### Confirmación

- `SchemaMinor3Tests`: el *fixture* inmutable `Fixtures/schema/1.3/document.json` se lee con su *hash* y sin
  miembros desconocidos; un documento 1.2 se lee con la lista vacía; la lista sobrevive a la ida y vuelta, una
  dañada se repara y una versión 1.2 la conserva como miembro desconocido de `settings`.
- `GuideRulesTests`: la lista se repara al leerse y conserva los ids desconocidos; los ids son estables.
- `SchemaValidationTests`: los *fixtures* 1.0 a 1.3 validan contra `document.schema.json`.
- Las pruebas de las guías llevan `[Trait("Req", "PES-015")]`.

## Pros y contras de las opciones

### Documento 1.3 con la lista `settings.guidesDone`

- Buena, porque cumple ADR-0007 tal cual: un *minor* aditivo que lee cualquier 1.x.
- Buena, porque una guía nueva no cambia el formato.
- Mala, porque los ids son un contrato: cambiar el de una guía la haría salir otra vez.

### Un booleano por guía

- Buena, porque cada miembro se lee solo.
- Mala, porque cada guía nueva sería otro *minor* del documento.

### Reutilizar `dock.coachDone`

- Buena, porque no cambia el formato.
- Mala, porque quien terminó una guía no vería las demás, y repetir una las repetiría todas.

### Guardarlo en `onboarding`

- Buena, porque conserva lo que no conoce, como `settings`.
- Mala, porque no es un ajuste: General no podría vaciarla con `SetSetting` y quedaría fuera de los
  descriptores.

### No guardarlo

- Buena, porque no toca el formato.
- Mala, porque las guías saldrían en cada arranque: incumple «una vez» y molesta a quien ya las terminó.

## Criterios de reapertura

- Que haya que recordar el paso en el que se dejó una guía entre reinicios.
- Que el usuario no ratifique R-39: la lista se queda sin uso y se retira en el siguiente *minor*.

## Más información

- [ADR-0007](0007-documento-json-versionado.md), [ADR-0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md),
  [ADR-0028](0028-ajustes-persistidos-de-m6.md) y [ADR-0033](0033-gemini-como-segundo-proveedor-de-ia.md):
  envoltorio, *minor* aditivo, miembros desconocidos y el 1.2.
- Catálogo: PES-015, GEN-014, D21 (§6.2) y la propuesta R-39 (§6.1).
- `data/schemas/document.schema.json` y `data/catalogs/timings.json` (grupo `Guides`).
