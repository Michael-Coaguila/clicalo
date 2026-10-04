# Cómo leer el catálogo de requisitos

El [catálogo](catalog.md) es la fuente de verdad funcional de Clícalo: qué tiene que hacer el producto y
cómo se acepta. No prescribe tecnología; el cómo está en el [plano de arquitectura](../architecture/blueprint.md).
Esta página explica cómo está organizado, cómo se cita, cómo se traza hasta las pruebas y en qué estado
están las propuestas abiertas al usuario.

## Jerarquía de fuentes

De mayor a menor autoridad ([§0.1 del catálogo](catalog.md#01-jerarquía-de-fuentes-de-mayor-a-menor-autoridad)):

1. La instrucción del usuario: la arquitectura, la tecnología, las APIs concretas y el plan de fases del
   paquete de diseño **no son vinculantes**.
2. Las 8 reglas del README del paquete y su glosario.
3. La Auditoría (62 hallazgos, todos aceptados salvo la elección de tecnología).
4. El Prototipo v4: manda en comportamiento, flujos, estados, textos, medidas y orden de los elementos,
   salvo en sus defectos (simulaciones, código muerto o fallos que violan una regla).
5. Los documentos 01–10 del paquete; si se contradicen, prevalece el de número menor.
6. Las decisiones del propio catálogo, marcadas «Decisión».

Qué es vinculante del paquete está resumido en
[LEEME-VINCULANTE](../design/handoff/LEEME-VINCULANTE.md).

## Estructura

| Sección | Contenido |
|---|---|
| [0](catalog.md#0-cómo-leer-este-documento) | Cómo leer: fuentes, prioridades, convenciones y glosario |
| [1](catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales) | Las 8 reglas que no se pueden romper (REG-01 a REG-08), todas MUST |
| [2](catalog.md#2-catálogo-por-módulo) | Requisitos por módulo (PAN, CAB, BUS… CAT) |
| [3](catalog.md#3-requisitos-no-funcionales) | Requisitos no funcionales (NFR-001 a NFR-020), con la tabla de tiempos y umbrales |
| [4](catalog.md#4-casos-límite-y-comportamiento-esperado) | Casos límite (EC-…) |
| [5](catalog.md#5-discrepancias-resueltas) | Discrepancias entre fuentes y cómo se resolvieron (DIS-…) |
| [6](catalog.md#6-preguntas-abiertas) | Preguntas abiertas con su propuesta por defecto (PQ-01 a PQ-51), propuestas pendientes (6.1) y [decisiones del usuario](catalog.md#62-decisiones-del-usuario) (6.2) |
| [7](catalog.md#7-esquema-v1-exacto-macro-quick-access-y-conversión) | Esquema exacto de Macro Quick Access (v1) y su conversión |
| [8](catalog.md#8-lecciones-de-la-app-antigua-y-del-prototipo) | Lecciones de la app antigua y del prototipo (L-…) |
| [9](catalog.md#9-textos-correcciones-y-claves-nuevas-necesarias) | Correcciones de textos y claves nuevas necesarias |
| [10](catalog.md#10-fuera-de-alcance-no-son-requisitos) | Fuera de alcance |

> **Numeración.** Las preguntas abiertas son la sección **6** y las propuestas pendientes de ratificar, la
> **6.1**; las discrepancias, la **5**. Algunos documentos antiguos citan «§7 PQ-nn» o «§6 DIS-nn»: léelos
> como la pregunta PQ-nn de la sección 6 y la discrepancia DIS-nn de la sección 5.

## Formato de un requisito

```text
- **EJE-003 · MUST · Pulsar.** Descripción verificable. **Acepta:** criterio de aceptación. ‹fuentes›
```

- **Identificador:** prefijo del módulo y número de tres cifras. Los prefijos transversales son `REG`
  (reglas), `NFR` (no funcionales), `EC` (casos límite), `DIS` (discrepancias), `PQ` (preguntas abiertas) y
  `L` (lecciones).
- **Prioridad:** **MUST**, imprescindible para publicar; **SHOULD**, esperado en la primera versión
  pública y solo aplazable con justificación escrita; **COULD**, deseable.
- **Fuentes** entre ‹…›: `P4:n` (línea n del Prototipo v4), `dNN` (docs/NN del paquete), `AUD-nn`
  (hallazgo de la Auditoría), `[clave]` (clave de `strings.*.json`) y `v1` (código o datos de Macro Quick
  Access).
- **Medidas** en píxeles lógicos. «Visual X / táctil 44» significa que el dibujo mide X y el área de toque
  al menos 44×44.
- **Tiempos y umbrales**: son constantes con nombre, ajustables y probadas con reloj simulado (NFR-020); en
  el código viven en `data/catalogs/timings.json`.

## Del requisito a la prueba

- Una prueba que verifica un requisito lleva `[Trait("Req", "<ID>")]`, con el identificador exacto del
  catálogo. Los detalles están en la
  [estrategia de pruebas](../architecture/testing-strategy.md#trazabilidad-requisito--prueba).
- `cl trace` generará `traceability.md` en esta carpeta a partir del catálogo y de los resultados de la CI.
  Ese archivo **no se versiona**.
- Desde el hito RC, ningún MUST puede quedar sin prueba automática o sin una entrada en el guion manual o
  en la aceptación en hardware.

## Cambiar un requisito

**Nadie rebaja un requisito por su cuenta: ni el plano, ni un ADR, ni una persona colaboradora, ni un
agente.** Si un requisito parece inviable o inseguro:

1. Se reúne la evidencia (normalmente, un spike con su criterio de éxito).
2. Se formula una **propuesta al usuario**: el requisito, el problema, la evidencia, lo que se propone y lo
   que se aplica mientras no se decida.
3. La propuesta se registra en la sección de preguntas abiertas del catálogo y en
   [§1.4 del plano](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario).
4. **Solo el usuario la ratifica.** Mientras no lo haga, el requisito sigue vigente tal cual.

Cuando el usuario decide (por iniciativa propia o al ratificar una propuesta), la decisión se registra en la
[sección 6.2 del catálogo](catalog.md#62-decisiones-del-usuario) con su fecha y su motivo, y cada requisito afectado
se marca en su sitio como **«Modificado por decisión del usuario del AAAA-MM-DD»** o **«Retirado por decisión del
usuario del AAAA-MM-DD»**, con el identificador de la decisión y el motivo. Un requisito retirado conserva su
identificador con la prioridad «Retirado» y su título; el identificador no se reutiliza. Si la decisión cambia un
límite de confianza, un formato persistido o un contrato público, va con su ADR (por ejemplo, la decisión D2 del
2026-10-03 con [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)) y una línea en el
`CHANGELOG.md`.

Los textos de producto nuevos o corregidos entran por un PR de i18n con las dos lenguas
(`strings.es.json` y `strings.en.json`).

## Propuestas pendientes de ratificar (P1–P6)

Estado a 2026-09-25: **ninguna está ratificada.** Todas están descritas en
[§1.4 del plano](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario)
y todavía no figuran en el catálogo.

| # | Requisito | Propuesta | Mientras no se ratifique |
|---|---|---|---|
| P1 | NFR-001 (panel en menos de 1 s) al iniciar sesión | Ninguna todavía. Solo si el spike S5 demuestra que no se puede cumplir, se propondrá mostrar primero la burbuja | NFR-001 es puerta de publicación sin excepciones |
| P2 | PQ-35 y la fila «Una sola ventana» de Sistema | La instancia única es obligatoria por seguridad (dos motores serían dos dueños de las mismas teclas); se propone quitar la fila y marcar su texto como no usado | La fila no se construye; el comportamiento es el de la opción activada, su valor por defecto |
| P3 | PLA-003 (SHOULD, 5 generaciones gratis al día) y PQ-48 | La 2.0 sale solo con clave propia; la cuota gratuita llega con el proxy de IA ([ADR-0014](../adr/0014-ia-con-clave-propia.md)) | Los flujos de IA con cuota no se muestran; el resto de PLA sí |
| P4 | SIS-002 (MUST, iniciar elevado sin UAC) | No se rebaja: se cumple en la 2.0 con el componente de sistema ([ADR-0009](../adr/0009-elevacion-y-componente-de-sistema.md)). Solo si S14 fracasa se pediría rebajarlo a SHOULD en la 2.0 | Se cumple |
| P5 | ARM64 en la distribución | Publicar ARM64 en beta desde el principio y en estable cuando pase la aceptación en un equipo ARM64 físico | ARM64 solo en beta |
| P6 | NFR-010 al desinstalar desde Configuración de Windows | Ese camino no puede mostrar UI y siempre conserva los datos; la pregunta se hace en Sistema › Desinstalar y, al reinstalar, en la bienvenida. Requiere dos textos nuevos en ES y EN | Se implementa así; los textos entran por PR de i18n |
