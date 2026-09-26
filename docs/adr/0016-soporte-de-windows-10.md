---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: verificación adversarial «Accesibilidad» (guion de Acceso por voz en Windows 10)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0016 · Soporte completo de Windows 10 22H2 en la 2.x con revisión formal en 2027

## Contexto y planteamiento del problema

El catálogo exige compatibilidad con Windows 10 22H2 o posterior y con Windows 11 (NFR-011, MUST), y el
público principal a menudo usa equipos que no se renuevan con facilidad. A la vez, las actualizaciones de
seguridad de consumo de Windows 10 (ESU) terminan el 13-10-2026, y hay diferencias funcionales que
afectan al producto:

- **Acceso por voz no existe en Windows 10.** La verificación de accesibilidad refutó que el guion de
  pruebas y el spike S3 pudieran validarlo ahí; en Windows 10 se usa el Reconocimiento de voz de Windows,
  y las instrucciones de voz del producto necesitan una variante (PQ-29).
- El desenfoque del fondo con `DWMWA_SYSTEMBACKDROP_TYPE` solo existe en Windows 11 22H2 o posterior; en
  Windows 10 la única vía es una API no documentada, que está prohibida.

¿Hasta cuándo y con qué alcance se da soporte a Windows 10?

## Factores de decisión

- NFR-011 es MUST y el plano no rebaja requisitos por su cuenta.
- Coste de la matriz de pruebas para una sola persona.
- Las diferencias de accesibilidad entre Windows 10 y 11 tienen que estar cubiertas por pruebas reales.
- Revisar la decisión cuando haya datos, no antes.

## Opciones consideradas

- Soporte completo en la 2.x, con una VM de Windows 10 en el equipo de pruebas, guion con Reconocimiento
  de voz de Windows y revisión formal en la primera versión menor posterior a octubre de 2027
- Solo Windows 11
- Soporte indefinido de Windows 10

## Resultado de la decisión

Opción elegida: **«Soporte completo en la 2.x con revisión formal en 2027»**, porque cumple el requisito
vinculante y fija un momento concreto para reconsiderarlo con datos.

- El equipo táctil de laboratorio ejecuta una VM Hyper-V de Windows 10 22H2 (con puntero sintético), y
  `lab.yml` la incluye.
- El guion manual previo a cada versión estable usa en Windows 10 Narrador y el **Reconocimiento de voz
  de Windows** («mostrar números», «clic 4», «clic Negrita»); en Windows 11, Narrador y Acceso por voz.
- En Windows 10, y cuando el usuario desactiva los efectos de transparencia, no hay desenfoque: fondo
  sólido con la opacidad elegida (PAN-003 es SHOULD y la ausencia está justificada).
- Los TFM de Windows apuntan a `net10.0-windows10.0.19041.0` para disponer de las proyecciones WinRT
  necesarias.
- La revisión formal se hace en la primera versión menor posterior a octubre de 2027.

### Consecuencias

- Buena, porque quien no puede actualizar su equipo sigue teniendo Clícalo.
- Buena, porque las diferencias de voz entre sistemas quedan cubiertas por un guion real.
- Mala, porque la matriz de pruebas es más cara y pueden aparecer fallos exclusivos de Windows 10.
- Mala, porque las personas usuarias de Windows 10 no tendrán desenfoque ni Acceso por voz, y su sistema
  deja de recibir parches de Microsoft, algo que Clícalo no puede compensar.

### Confirmación

- `lab.yml` con la VM de Windows 10 22H2 antes de cada beta relevante y de cada estable.
- Guion manual `release-verification.yml` firmado en Windows 10 y en Windows 11, con y sin alto
  contraste.
- Spike S3 ejecutado también en Windows 10 con el Reconocimiento de voz de Windows.

## Pros y contras de las opciones

### Soporte completo en la 2.x con revisión en 2027

- Buena, porque cumple NFR-011 y deja la decisión futura para cuando haya datos de uso real.
- Mala, porque mantiene durante años un sistema operativo sin soporte de consumo.

### Solo Windows 11

- Buena, porque simplifica la matriz y permite usar solo las API modernas.
- Mala, porque incumple un requisito MUST.

### Soporte indefinido

- Buena, porque no deja a nadie atrás.
- Mala, porque el coste crece sin límite y bloquearía mejoras que dependan de Windows 11.

## Criterios de reapertura

La revisión formal de la primera versión menor posterior a octubre de 2027 decide, con un ADR nuevo, si
se mantiene el soporte. También se adelanta si el coste de la matriz o los fallos exclusivos de Windows 10
lo justifican ([§15.2 del plano](../architecture/blueprint.md#152-riesgos-abiertos)).

## Más información

- Plano: [§8.1 (desenfoque)](../architecture/blueprint.md#81-superficies-y-ventanas),
  [§10.2](../architecture/blueprint.md#102-pruebas-de-accesibilidad-y-aceptación-en-hardware),
  [§10.5](../architecture/blueprint.md#105-cicd),
  [§15.2](../architecture/blueprint.md#152-riesgos-abiertos).
- Catálogo: NFR-011 en [§3](../requirements/catalog.md#3-requisitos-no-funcionales); PQ-29 en
  [preguntas abiertas](../requirements/catalog.md#6-preguntas-abiertas).
- Registro: [techVerification.json](../architecture/decision-record/techVerification.json) (enfoque
  «Accesibilidad»).
- Evidencia:
  [configurar Acceso por voz](https://support.microsoft.com/en-us/accessibility/windows/voice-access/set-up-voice-access),
  [preguntas frecuentes de Acceso por voz](https://support.microsoft.com/en-US/accessibility/windows/voice-access/voice-access-frequently-asked-questions-faqs).
- ADR relacionados: [ADR-0001](0001-framework-ui-wpf.md).
