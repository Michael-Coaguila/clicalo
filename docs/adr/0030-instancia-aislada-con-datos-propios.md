---
status: Aceptado
date: 2026-10-16
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario
consulted: catálogo SIS-003 y NFR-018; plano §3.4; ADR-0010
informed: colaboradores y agentes de M7, mediante este ADR, `docs/architecture/tooling.md` y `docs/architecture/blueprint.md`
---

# ADR-0030 · Un arranque con `--data` es una instancia propia

## Contexto y planteamiento del problema

La instancia única (SIS-003, ADR-0010) usa el mutex `Local\Clicalo.{sidHash}.Instance` y el *pipe*
`Clicalo.{sidHash}.{sessionId}`. Un arranque de desarrollo o de medición con `--data <carpeta>` usaba los mismos
nombres: si el Clícalo de la persona estaba abierto, la copia de pruebas no arrancaba y le enviaba «Show». Para
revisar la interfaz en vivo en el PC real del usuario, una copia de pruebas tiene que convivir con la suya sin
tocarla nunca.

## Factores de decisión

- Sin `--data` no cambia nada: los nombres de ADR-0010, la DACL y la verificación en los dos sentidos.
- La IPC no gana verbos.
- La misma carpeta escrita de otra forma es la misma instancia, y dos carpetas son dos instancias.
- Los nombres no revelan la ruta, igual que no revelan el SID.

## Opciones consideradas

1. Un sufijo derivado de la ruta completa normalizada de la carpeta de `--data`.
2. Una opción nueva (`--instance <nombre>`).
3. Desactivar la instancia única con `--data`.

## Resultado de la decisión

Opción 1. Con `--data`, el mutex y el *pipe* añaden tras `{sidHash}` el sufijo `.d` y los 16 primeros caracteres
hexadecimales (minúsculas) de SHA-256 sobre el UTF-8 de la ruta completa (`Path.GetFullPath`), sin separador final
y en mayúsculas, porque Windows no distingue mayúsculas en las rutas:

- `Local\Clicalo.{sidHash}.d{hash}.Instance`
- `Clicalo.{sidHash}.d{hash}.{sessionId}`

Una segunda copia con la misma carpeta encuentra la primera y le pide «Show» como siempre. `InstanceIdentityTests`
fija el valor de una ruta conocida para que un cambio de la derivación sea una decisión y no un accidente.

### Consecuencias

- Buena, porque `cl run`, `cl perf` y la revisión en vivo conviven con el Clícalo de la persona.
- Buena, porque no hay opción nueva ni verbo nuevo: el contrato de la línea de órdenes no cambia.
- Mala, porque una copia con `--data` y **sin** `--no-input` es un segundo motor sobre el mismo teclado. Es un
  arranque de desarrollo: `cl run` y las revisiones usan siempre `--no-input`, y solo la medición de la CI envía
  teclas, en una máquina sin otro Clícalo.
- Neutral, porque Sentinel, al relanzar Clícalo tras un fallo (ADR-0023), lo abría **sin** `--data`: una copia
  aislada que envía teclas volvía sobre la carpeta de datos normal y parecía haber perdido lo hecho en la suya. Se vio
  el 2026-10-22 en la vista previa del mantenedor: los cuelgues de la Pestaña la cerraban, Sentinel la relanzaba en la
  carpeta normal y un perfil creado allí aparecía y desaparecía. Lo corrige
  [ADR-0032](0032-relanzamiento-con-la-carpeta-de-datos.md): el contrato de Sentinel (protocolo 4) lleva la carpeta,
  Sentinel lee en ella el diario de fallos de la copia y la relanza con `--data`. El reinicio como administrador nunca
  se ofrece con `--data` (una copia aislada no es la instalada y `ElevatedRelaunch` responde `NotInstalled`), así que
  ahí no hay nada que perder.

## Pros y contras de las opciones

### Sufijo derivado de la carpeta

- Buena, porque el aislamiento de datos y el de instancia van juntos: nunca dos procesos sobre los mismos archivos.
- Mala, porque una herramienta que busque la instancia (pruebas de rendimiento) tiene que derivar el mismo sufijo.

### Opción `--instance`

- Buena, porque es explícita.
- Mala, porque permite dos instancias con la misma carpeta de datos escribiendo los mismos archivos.

### Sin instancia única con `--data`

- Buena, porque es lo más simple.
- Mala, porque dos copias con la misma carpeta escribirían a la vez el documento (REG-08).

## Criterios de reapertura

- Si aparece la IPC de `OpenUri` e `ImportFile`: hereda los mismos nombres.
- Si una copia aislada tiene que enviar teclas junto a la de la persona.
- Si algún día se habilita la elevación con `--data`: el reinicio como administrador tendría que llevar la carpeta.

## Más información

- Catálogo: SIS-003, NFR-018, REG-08.
- Plano: §3.4.
- ADR relacionados: [ADR-0010](0010-ipc-minima.md) y
  [ADR-0032](0032-relanzamiento-con-la-carpeta-de-datos.md).
