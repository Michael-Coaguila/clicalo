---
status: Propuesto
date: 2026-10-23
decision-makers: Michael Coaguila (mantenedor); la forma concreta, por delegación del usuario, pendiente de ratificar
consulted: decisión D5 del usuario (catálogo §6.2); ADR-0007, ADR-0008, ADR-0014 y ADR-0028; condiciones adicionales de la API de Gemini (efectivas desde el 2026-03-23), su página de modelos y su página de claves, consultadas el 2026-10-23
informed: colaboradores y agentes, mediante este ADR, PRIVACY.md, docs/security/privacy.md y el modelo de amenazas (T8)
---

# ADR-0033 · Gemini de Google como segundo proveedor de IA con clave propia: cliente REST propio detrás de `IChatClient`, proveedor elegido en el documento 1.2 y consentimiento por proveedor

## Contexto y planteamiento del problema

Desde la decisión D5 del usuario (2026-10-09), «Crear con IA» solo funciona con la clave de API de la propia
persona, sin servidor de Clícalo ni cuota gratuita, y el único proveedor es Claude Haiku 5.5 de Anthropic
([ADR-0014](0014-ia-con-clave-propia.md)). Una clave de Anthropic es de pago. El mantenedor usa en otra app una
clave **gratuita** de Google AI Studio y pidió poder usar un proveedor gratuito: la capa gratuita de la API de
Gemini (modelos Flash y Flash-Lite, con límites de uso por proyecto) lo permite sin cambiar D5, porque la clave
sigue siendo de la persona y Clícalo sigue sin servidor.

La capa gratuita tiene un precio en privacidad. Según las condiciones adicionales de la API de Gemini, con los
servicios no de pago Google usa lo que se envía y las respuestas para dar, mejorar y desarrollar sus productos y
sus tecnologías de aprendizaje automático, y **personas revisoras pueden leer, anotar y procesar** la entrada y la
salida; Google pide no enviar información sensible, confidencial ni personal. Con los servicios de pago, Google no
usa los *prompts* ni las respuestas para mejorar sus productos y solo los registra un tiempo limitado para detectar
abusos. En el Espacio Económico Europeo, Suiza y el Reino Unido, las condiciones de datos de pago se aplican a
**todos** los servicios, también a la capa gratuita; y quien pone un «API Client» a disposición de usuarios de esas
regiones solo puede usar servicios de pago. Las condiciones exigen además tener 18 años o más.

Añadir Gemini abre un **límite de confianza nuevo** (otra empresa recibe los datos, con otras condiciones), un
**destino de red nuevo** (`generativelanguage.googleapis.com`), una **credencial nueva** en el Administrador de
credenciales y un **ajuste persistido nuevo** (qué proveedor se eligió). ¿Cómo se añade sin enviar ningún dato más
que los 4 de PLA-008, sin que un consentimiento dado para Anthropic valga para Google, sin una dependencia nueva que
haya que confiar y sin perder datos al volver a una versión anterior?

## Factores de decisión

- **PLA-008 (MUST):** a Google también se envían **exactamente** el nombre del programa, la distribución del
  teclado, el idioma de los programas y el de Clícalo ([aiPrivacy4], [consentD4]), más la instrucción fija
  versionada; nada más (LOG-002).
- **PLA-004 (MUST):** el consentimiento dice a quién van los datos y qué puede hacer con ellos. Las condiciones de
  Google y las de Anthropic son distintas, así que el consentimiento de uno no vale para el otro.
- **LOG-006:** la respuesta es contenido no confiable: se valida contra `ai-template.v1` y `TemplateSchema`, nunca
  se ejecuta y nada se instala sin la vista previa.
- **ADR-0008 y CLC0003:** la clave es un tipo secreto (`Sensitive<string>` con `RedactionKind.Secret`, el mismo
  que la de Anthropic), vive en el Administrador de credenciales, no se vuelve a mostrar y nunca llega a un
  registro.
- **D5:** solo clave propia, sin servidor de Clícalo.
- **T12 (cadena de suministro):** `nuget.config` solo acepta paquetes de propietarios listados; un propietario
  nuevo es una decisión de confianza que conviene evitar si no aporta.
- **REG-08 y ADR-0007:** el cambio de formato es aditivo y una versión anterior conserva el ajuste al reescribir.
- Simplicidad: un proveedor más es un adaptador más, sin tocar `ByoKeyTemplateGenerator`, el puerto ni el contrato
  de la respuesta.

## Opciones consideradas

Para hablar con Gemini:

- **A.** Un `IChatClient` propio (Microsoft.Extensions.AI.Abstractions, que ya está) sobre la API REST nativa
  `generateContent`, con `HttpClient` y System.Text.Json.
- **B.** El extremo compatible con OpenAI de Gemini con `Microsoft.Extensions.AI.OpenAI` y el SDK `OpenAI`.
- **C.** El SDK oficial de Google para .NET, `Google.GenAI`.
- **D.** No añadir Gemini.

Para guardar qué proveedor se eligió:

- **1.** `settings.aiProvider`, al nivel de `settings`, en un documento 1.2.
- **2.** `settings.ai.provider`, dentro del grupo `ai`.
- **3.** Deducirlo de `ai.apiKeyRef` (`Clicalo/ai/anthropic` o `Clicalo/ai/gemini`).

## Resultado de la decisión

Opción elegida: **«A» y «1»**, con el consentimiento ligado al proveedor, porque es la única forma de añadir Gemini
sin un propietario nuevo en `nuget.config`, con control exacto de cada byte que sale (PLA-008) y sin perder la
elección al volver a una versión 1.1.

### Proveedor y petición

- **Adaptador:** `GeminiProvider` (`Infrastructure.Ai`, implementa `IAiProvider`) crea un `GeminiChatClient`, un
  `IChatClient` pequeño que solo implementa la respuesta completa (la transmisión por partes no se usa y lanza
  `NotSupportedException`); `ChosenProviderGenerator` elige en cada generación el generador del proveedor del
  documento, y `ChosenProviderKeyStore` (Application) hace lo mismo con la clave. `ByoKeyTemplateGenerator`, `ITemplateGenerator`, `TemplateRequest`, `AiPrompt` y
  `AiResponseReader` no cambian.
- **Modelo:** `gemini-3.5-flash-lite`, estable, con capa gratuita, el que Google recomienda para proyectos nuevos y
  con el razonamiento al mínimo por defecto: rápido y económico para una respuesta corta y estructurada, como Haiku.
  Se fija en código; no se usa el alias `gemini-flash-latest`, porque Google lo cambia de modelo sin una versión de
  Clícalo.
- **Dirección y clave:** `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent`,
  fija en código. La clave va en la cabecera `x-goog-api-key`, **nunca en la URL**, porque una URL acaba en
  registros y *proxies*. Ninguna variable de entorno cambia la dirección ni añade una credencial. Sin reintentos.
- **Cuerpo:** `systemInstruction` con `AiPrompt.Instruction`, un único `contents` de rol `user` con
  `AiPrompt.UserMessage` (los 4 valores) y `generationConfig` con `maxOutputTokens`
  (`Timings.Ai.AiMaxOutputTokens`) y `responseMimeType: application/json`. Nada más: ni `safetySettings`, ni
  herramientas, ni `cachedContent`, ni etiquetas, ni `store`, ni ningún identificador de la instalación o de la
  persona.
- **Respuesta:** se leen como mucho `Timings.Ai.AiResponseMaxBytes` del cuerpo; el texto es la unión de las partes
  de `candidates[0].content.parts` que no son razonamiento (`thought`). Un *prompt* bloqueado
  (`promptFeedback.blockReason`), una respuesta sin candidatos o un cuerpo mayor son «invalid». El texto pasa por
  `AiResponseReader` y `TemplateSchema` como el de Anthropic.
- **Errores** (R-26): 401, 402, 403, 429 y un 400 por clave no válida (`API_KEY_INVALID`) son «badkey» (clave
  rechazada o límite del proveedor, como la cuota diaria gratuita); cualquier otro 4xx y los 5xx son
  «unavailable»; sin red o sin respuesta en 15 s, «offline». El cuerpo de un error nunca se registra.

### Clave, elección y consentimiento

- **Una credencial por proveedor:** `Clicalo/ai/anthropic` y `Clicalo/ai/gemini`, con `CredentialKeyStore`
  (o en memoria con `--data`). Borrar una no toca la otra. Desinstalar con borrado de datos borra las dos.
  `ai.apiKeyRef` guarda el destino de la clave del proveedor elegido.
- **`settings.aiProvider`:** `"anthropic"` o `"gemini"`, al nivel de `settings` (que tiene `[JsonExtensionData]`),
  no dentro de `ai` (que no lo tiene), igual que ADR-0028 dejó `handlePosByMonitor` fuera de `dock`. Falta o es
  desconocido: `anthropic`, el comportamiento de hoy. `DocumentFormats.DocumentSchema` pasa a **1.2**, con su
  *fixture* inmutable.
- **El consentimiento es del proveedor:** cambiar de proveedor (en General › IA o en Plantillas) borra
  `ai.consent` en el **mismo** comando, así que se deshace de una vez. La tarjeta de Plantillas vuelve a pedirlo
  antes del primer intento, con el texto del proveedor nuevo: [consentD4] nombra al destinatario y, con Gemini,
  añade [aiGeminiFree].
- **Aviso de la capa gratuita** [aiGeminiFree]: «Con una clave gratuita, Google puede usar lo que se envía y sus
  respuestas para mejorar sus productos, y personas revisoras pueden leerlo. Con una clave de pago, no.» Se
  muestra en la tarjeta de consentimiento, bajo la elección de General › IA y junto al campo de la clave. Es el
  caso peor: no distingue regiones, porque Clícalo no sabe ni pregunta dónde vive la persona.
- **Textos** nuevos y revisados en la sección `added` de `handoff-import.json`, pendientes de ratificar (R-38).

### Consecuencias

- Buena, porque la IA puede usarse gratis, con los límites de uso de Google, sin servidor de Clícalo (D5).
- Buena, porque a Google salen los mismos 4 datos que a Anthropic, y una prueba lo comprueba sobre el cuerpo HTTP.
- Buena, porque no entra ninguna dependencia ni ningún propietario nuevo en `nuget.config`.
- Buena, porque quien no cambia nada sigue con Anthropic, su clave y su consentimiento.
- Mala, porque con una clave gratuita los 4 datos pueden usarse para entrenar y leerse por personas de Google:
  por eso el aviso explícito y el consentimiento aparte.
- Mala, porque el cliente HTTP de Gemini es código propio que mantener: si Google cambia la forma de
  `generateContent`, lo dirá una prueba manual con una clave real, nunca la CI.
- Mala, porque cambiar de proveedor pide el consentimiento otra vez, aunque se vuelva al anterior.

### Confirmación

- `GeminiProviderTests` (Infrastructure, `[Trait("Req", "PLA-008")]`): con un manejador HTTP falso, la petición va
  a `generativelanguage.googleapis.com`, lleva la clave solo en `x-goog-api-key` (ni en la URL ni en el cuerpo) y
  el cuerpo contiene solo la instrucción fija, los 4 valores y `generationConfig`; los códigos HTTP se convierten en
  sus tarjetas (PLA-006); un *prompt* bloqueado, un cuerpo enorme o sin candidatos son «invalid». Ninguna prueba
  llama a la API real.
- Pruebas de Application: cambiar de proveedor borra el consentimiento en un solo paso de deshacer y la clave
  guardada es la del proveedor elegido; de Domain y Persistence: `aiProvider` se lee del *fixture* 1.2, un 1.1 se
  lee con `anthropic` y un valor desconocido se repara.
- CLC0003 impide pasar la clave a un registro.

## Pros y contras de las opciones

### A. `IChatClient` propio sobre `generateContent`

- Buena, porque no añade paquetes ni propietarios de confianza (T12).
- Buena, porque cada campo del cuerpo es explícito y la clave va en una cabecera.
- Buena, porque `responseMimeType` pide JSON y el error de clave no válida se reconoce aunque llegue como 400.
- Mala, porque es código propio frente a un SDK mantenido por otros.

### B. Extremo compatible con OpenAI

- Buena, porque `Microsoft.Extensions.AI.OpenAI` da el `IChatClient` hecho.
- Mala, porque necesita el paquete `OpenAI`, cuyo único propietario en nuget.org (`OpenAIOfficial`) no está en
  `nuget.config`, y `Microsoft.Extensions.AI.OpenAI` 10.11.0 obliga a subir `Microsoft.Extensions.AI` (10.10.0).
- Mala, porque un SDK pensado para OpenAI clasifica los errores de Google a su manera y añade su propia
  configuración de reintentos y variables de entorno que habría que anular una a una.

### C. `Google.GenAI`

- Buena, porque es el SDK oficial de Google.
- Mala, porque sus propietarios en nuget.org (`GoogleGenAI` y una cuenta personal) no están en `nuget.config`, y
  añade una dependencia grande para una sola petición.

### D. No añadir Gemini

- Buena, porque no hay límite de confianza nuevo.
- Mala, porque la IA sigue exigiendo una clave de pago.

### 1. `settings.aiProvider` (documento 1.2)

- Buena, porque una versión 1.x anterior lo conserva al reescribir y la elección existe aunque aún no haya clave.
- Mala, porque el campo vive fuera de su grupo `ai`.

### 2. `settings.ai.provider`

- Buena, porque está junto al resto de la IA.
- Mala, porque `ai` no tiene `[JsonExtensionData]`: una versión 1.1 lo borraría al reescribir (REG-08).

### 3. Deducirlo de `ai.apiKeyRef`

- Buena, porque no cambia el formato.
- Mala, porque sin clave no hay elección, y la elección es lo primero que se hace.

## Criterios de reapertura

- Google cambia las condiciones de datos de la capa gratuita o deja de ofrecerla para `gemini-3.5-flash-lite`.
- El usuario decide que la restricción del EEE, Suiza y el Reino Unido («solo servicios de pago al poner un API
  Client a disposición de usuarios») se aplica a Clícalo, aunque cada persona use su propia clave: entonces el
  aviso o la oferta de Gemini cambian con un ADR nuevo.
- Se quiere un tercer proveedor o elegir el modelo: se generaliza la elección con un ADR nuevo.

## Más información

- Catálogo: PLA-002 a PLA-008 en [§2.22](../requirements/catalog.md#222-pla--plantillas-e-ia), GEN-015, LOG-002 y
  LOG-006; D5 en [§6.2](../requirements/catalog.md#62-decisiones-del-usuario); R-26 y R-38 en
  [§6.1](../requirements/catalog.md#61-propuestas-pendientes-de-ratificar).
- Privacidad: [PRIVACY.md](../../PRIVACY.md), [docs/security/privacy.md](../security/privacy.md) y T8 del
  [modelo de amenazas](../security/threat-model.md).
- Evidencia (fuentes primarias consultadas el 2026-10-23):
  [condiciones adicionales de la API de Gemini](https://ai.google.dev/gemini-api/terms),
  [modelos de Gemini](https://ai.google.dev/gemini-api/docs/models),
  [precios y capa gratuita](https://ai.google.dev/gemini-api/docs/pricing),
  [claves de API](https://ai.google.dev/gemini-api/docs/api-key) y
  [`generateContent`](https://ai.google.dev/api/generate-content).
- ADR relacionados: [ADR-0007](0007-documento-json-versionado.md), [ADR-0008](0008-secretos-dpapi-y-administrador-de-credenciales.md),
  [ADR-0014](0014-ia-con-clave-propia.md), [ADR-0028](0028-ajustes-persistidos-de-m6.md).
