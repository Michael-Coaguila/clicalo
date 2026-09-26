---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: paquete de diseño (docs/08 y ACE-001); condiciones de SignPath Foundation
informed: colaboradores, mediante CONTRIBUTING.md
---

# ADR-0015 · Licencia MIT y Developer Certificate of Origin (DCO)

## Contexto y planteamiento del problema

Clícalo es código abierto. El paquete de diseño lo presenta con licencia MIT y la tarjeta de la app
muestra «v{versión} · MIT · código abierto» (ACE-001). Dos hechos más pesan en la decisión:

- La vía de firma de código elegida, SignPath Foundation (ADR-0013), exige una licencia aprobada por la
  OSI y sin doble licencia comercial en ningún componente.
- Se busca que organizaciones de accesibilidad puedan adoptar, redistribuir y adaptar la herramienta sin
  fricción legal.

Además, cada contribución debe tener un origen claro: quien la envía declara que tiene derecho a hacerlo.
¿Con qué licencia se publica y cómo se certifica el origen de las contribuciones?

## Factores de decisión

- Licencia aprobada por la OSI, sin doble licencia comercial (requisito de SignPath Foundation).
- Adopción sin fricción por organizaciones de accesibilidad y por otros proyectos.
- Coherencia con los textos vinculantes del producto (ACE-001).
- Un mecanismo de origen de las contribuciones ligero, que se pueda cumplir por voz con una sola opción de
  Git.

## Opciones consideradas

- MIT con DCO y commits firmados
- GPL
- Un acuerdo de licencia de colaborador (CLA)

## Resultado de la decisión

Opción elegida: **«MIT con DCO y commits firmados»**, porque es la licencia que ya muestra el producto,
cumple la condición de SignPath Foundation y permite la adopción más amplia; el DCO certifica el origen
de cada contribución sin papeleo.

- `LICENSE` contiene la licencia MIT («Copyright (c) 2026 Michael Coaguila and Clícalo contributors») y
  los proyectos declaran `PackageLicenseExpression` MIT.
- Cada commit lleva la línea `Signed-off-by:` del DCO, que se añade con `git commit -s`. El trabajo `dco`
  de `pr.yml` la exige.
- Los commits se firman con SSH. `cl setup` configura ambas cosas.

### Consecuencias

- Buena, porque cualquier persona u organización puede usar, adaptar y redistribuir Clícalo.
- Buena, porque el DCO no exige firmar un contrato aparte ni mantener un registro de firmantes.
- Mala, porque MIT permite derivados cerrados que no devuelvan mejoras.
- Mala, porque sin CLA no se puede cambiar la licencia del código aportado por terceros sin su acuerdo.

### Confirmación

- El trabajo `dco` de `pr.yml` rechaza commits sin `Signed-off-by`.
- `CONTRIBUTING.md` explica el DCO y cómo firmar.

## Pros y contras de las opciones

### MIT con DCO

- Buena, porque es simple, conocida y compatible con la firma gratuita de SignPath Foundation.
- Buena, porque coincide con el texto vinculante de ACE-001.
- Mala, porque no obliga a publicar los cambios de los derivados.

### GPL

- Buena, porque garantiza que los derivados sigan siendo libres.
- Mala, porque contradice la licencia que muestra el producto y frena la integración en otros proyectos y
  organizaciones.

### CLA

- Buena, porque daría al mantenedor derecho a cambiar la licencia en el futuro.
- Mala, porque añade fricción legal y administrativa a cada colaborador, justo lo contrario de lo que
  necesita un proyecto que empieza con una persona.

## Criterios de reapertura

No se han fijado.

## Más información

- Plano: [§1.2 (D22)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§13, «Gobierno»](../architecture/blueprint.md#13-convenciones-de-ingeniería).
- Catálogo: ACE-001 en [§2.27](../requirements/catalog.md#227-ace--acerca-de-y-opinión).
- Proyecto: [LICENSE](../../LICENSE), [CONTRIBUTING.md](../../CONTRIBUTING.md).
- Evidencia: [condiciones de SignPath Foundation](https://signpath.org/terms) (licencia OSI sin doble
  licencia comercial).
- Referencia: [texto del Developer Certificate of Origin](https://developercertificate.org/).
- ADR relacionados: [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md).
