---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: paquete de diseño (docs/08); propuestas de fiabilidad y seguridad
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0008 · Secretos con DPAPI, `SecretText` sin salida `string` y la clave de IA en el Administrador de credenciales

## Contexto y planteamiento del problema

Un atajo de tipo Texto puede guardar una firma, una dirección o una contraseña. El catálogo exige que
los textos de Texto y de los pasos de texto de macro se cifren ligados a la cuenta de Windows, que la
clave de la IA vaya al almacén de credenciales del sistema y que nada de eso aparezca en claro en el
documento, las copias ni el registro (LOG-003). Un texto marcado como privado no se muestra en la ficha,
los avisos ni la búsqueda (LOG-004). Si un texto no se puede descifrar en otro equipo o con otro
usuario, se importa como «Texto no disponible · vuelve a escribirlo» y el atajo queda incompleto
(COP-005).

Además, un secreto no puede filtrarse por el camino: ni al registro, ni a una excepción, ni a un
`string` internado que sobreviva en memoria. ¿Cómo se guardan y se manejan los secretos?

## Factores de decisión

- Cifrado en reposo ligado a la cuenta de Windows, sin gestionar claves propias (LOG-003).
- Que un error de programación no pueda escribir un secreto en el registro: los tipos deben impedirlo.
- Minimizar las copias en claro en memoria.
- La clave de la IA nunca se vuelve a mostrar y no viaja con el perfil itinerante.

## Opciones consideradas

- DPAPI con alcance del usuario y entropía propia para los textos, un tipo `SecretText` sin salida
  `string`, y la clave de IA en el Administrador de credenciales con persistencia local
- Cifrado con una clave propia guardada en disco
- Guardar la clave de la IA dentro del documento

## Resultado de la decisión

Opción elegida: **«DPAPI + `SecretText` + Administrador de credenciales»**, porque delega la custodia de
la clave en Windows, liga los secretos a la cuenta del usuario y convierte la redacción en una propiedad
de los tipos, comprobada al compilar.

- **Textos en disco:** `{"enc":"dpapi.v1","blob":"…","len":42,"private":true}`, con `CryptProtectData`
  de alcance CurrentUser y entropía `"Clicalo.Text.v1"`, aplicado en el mapeador de persistencia.
- **En memoria:** `SecretText` guarda el contenido en un `char[]` privado y **no tiene ningún método que
  devuelva `string`**. Se accede con `WithRevealed(estado, ReadOnlySpanAction)`. `ToString()` devuelve
  `[oculto · N caracteres]`. El motor copia el texto a búferes alquilados y los limpia con
  `CryptographicOperations.ZeroMemory` después de `SendInput`.
- **Clave de IA:** en el Administrador de credenciales, destino `Clicalo/ai/{providerId}`,
  `CRED_TYPE_GENERIC` y `CRED_PERSIST_LOCAL_MACHINE`. La UI solo permite pegarla o borrarla; nunca la
  vuelve a mostrar.
- **Redacción por tipos:** el analizador CLC0003 impide que un valor sensible (`SecretText`,
  `Sensitive<T>`, `WindowTitle`, `ApiKey`, `CapturedKey`, `SearchQuery`) llegue a `ILogger`, a una
  interpolación de registro o a una excepción.

### Consecuencias

- Buena, porque no hay claves propias que custodiar, rotar ni filtrar.
- Buena, porque una fuga al registro es un error de compilación, no un descuido que se descubre tarde.
- Mala, porque los textos cifrados no son portables a otro equipo u otro usuario: aparecen como «Texto
  no disponible» (COP-005), y al exportar se excluyen por defecto (PQ-37).
- Mala, porque hay límites documentados: el `char[]` interno de `SecretText` no se limpia (el objeto es
  inmutable y compartido entre versiones del documento), y el `TextBox` del editor necesita un `string`.
  Es el único llamador autorizado a construirlo y lo comprueba ArchUnit.

### Confirmación

- CLC0003 en compilación.
- Prueba canario en CI: 5 valores `CANARY-<guid>` como texto de atajo, proceso, título de ventana,
  búsqueda y clave; se recorren los flujos E2E, se exporta el diagnóstico y se buscan en registros, ETW y
  el paquete. Cualquier coincidencia hace fallar la CI.
- ArchUnit: solo `Execution` y el editor del Centro de control llaman a `SecretText.WithRevealed`.
- Pruebas de Infrastructure con DPAPI (cifrar, descifrar y el caso «no disponible»).

## Pros y contras de las opciones

### DPAPI + `SecretText` + Administrador de credenciales

- Buena, porque Windows custodia la clave y la liga a la cuenta.
- Buena, porque los tipos hacen visible cualquier intento de sacar un secreto.
- Mala, porque la portabilidad entre equipos se pierde por diseño.

### Cifrado con clave propia en disco

- Buena, porque permitiría portar los textos entre equipos.
- Mala, porque una clave guardada junto a los datos no protege nada frente a quien lee el disco, y
  custodiarla bien exige otra infraestructura.

### La clave de IA dentro del documento

- Buena, porque viajaría con las copias.
- Mala, porque pondría un secreto de pago en cada copia, exportación y paquete de diagnóstico.

## Criterios de reapertura

No se han fijado.

## Más información

- Plano: [§6.2 (límite de `SecretText`)](../architecture/blueprint.md#62-atajos-acciones-y-agregado),
  [§6.7](../architecture/blueprint.md#67-cifrado-y-secretos),
  [§9.4](../architecture/blueprint.md#94-registros-y-diagnóstico).
- Catálogo: LOG-003 y LOG-004 en [§2.31](../requirements/catalog.md#231-log--registros-privacidad-y-seguridad);
  COP-005 en [§2.26](../requirements/catalog.md#226-cop--copias-de-seguridad); PQ-37 en
  [preguntas abiertas](../requirements/catalog.md#6-preguntas-abiertas).
- Paquete de diseño: [08-sistema-ia-y-distribucion.md](../design/handoff/docs/08-sistema-ia-y-distribucion.md),
  apartado «Cifrado».
- Seguridad: amenaza T7 en el [modelo de amenazas](../security/threat-model.md).
- ADR relacionados: [ADR-0007](0007-documento-json-versionado.md),
  [ADR-0014](0014-ia-con-clave-propia.md).
