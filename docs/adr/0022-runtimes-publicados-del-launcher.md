---
status: Propuesto
date: 2026-10-05
decision-makers: Michael Coaguila (mantenedor)
consulted: ADR-0009; plano §11 y P5; docs/architecture/tooling.md («Runtimes publicados»); ejecución s0 37325950329 (10 de 10 trabajos arm64 parados en NU1004)
informed: colaboradores y agentes, mediante docs/architecture/tooling.md y el CHANGELOG
---

# ADR-0022 · `Clicalo.Launcher` restaura y publica para los dos runtimes distribuidos

## Contexto y planteamiento del problema

`Clicalo.Launcher` es el lanzador del componente de sistema ([ADR-0009](0009-elevacion-y-componente-de-sistema.md)):
comprueba la copia protegida y solo arranca esa. Se publica con `PublishAot`, igual que `Clicalo.Sentinel`, pero desde
el andamiaje (64f6697) no declaraba los runtimes que Clícalo distribuye (`ClicaloRuntimeIdentifiers` =
`win-x64;win-arm64`, [§11 del plano](../architecture/blueprint.md#11-distribución-versionado-y-publicación)). Con
`PublishAot`, el SDK añade solo el runtime del equipo que restaura, así que su *lock file* guardaba únicamente el grafo
`win-x64`. En cuanto el repositorio pasó a ser público y la CI empezó a ejecutar ARM64, la restauración bloqueada del
*runner* `windows-11-arm` falló con NU1004 en los 10 trabajos arm64 de la ejecución `s0` 37325950329 (y fallaría igual
en el trabajo `verify (arm64)` de `pr.yml`).

`src/Clicalo.Launcher/**` es una ruta sensible (`architecture/sensitive-paths.json`, límite de confianza), y
[tooling.md](../architecture/tooling.md#lock-files) ya anotaba que este cambio debía ir con su ADR. ¿Cómo se configura el
Launcher para que restaure, compile y publique en los dos runtimes sin tocar su límite de confianza?

## Factores de decisión

- P5 y §11: ARM64 se publica en beta desde el principio, así que el Launcher tiene que publicarse para `win-arm64`.
- NFR-014 (builds reproducibles): el *lock file* no puede depender del equipo que restauró, y la CI restaura en modo
  bloqueado.
- ADR-0009: el lanzador verifica después de copiar y solo ejecuta la copia protegida; nada de eso puede cambiar.
- Un solo patrón para los tres ejecutables publicados, comprobado por una prueba.

## Opciones consideradas

- A. Las dos propiedades de «Shipped runtimes» de `Clicalo.App` y `Clicalo.Sentinel` también en el Launcher.
- B. Desactivar el modo bloqueado o los *lock files* para el Launcher.
- C. Publicar el Launcher con `-r <rid>` en cada trabajo.

## Resultado de la decisión

Opción elegida: «A». `Clicalo.Launcher.csproj` declara `RuntimeIdentifiers` = `$(ClicaloRuntimeIdentifiers)` y toma su
`RuntimeIdentifier` de `ClicaloRuntimeIdentifier` cuando una publicación lo pide, exactamente como Sentinel; su
`packages.lock.json` guarda los grafos `win-x64` y `win-arm64`. Es la única opción que cumple P5 y NFR-014 sin una
excepción. Solo cambia la configuración de compilación: ni el código, ni las comprobaciones, ni la ubicación protegida,
ni lo que el Launcher ejecuta.

### Consecuencias

- Buena, porque la restauración bloqueada funciona en x64 y en ARM64 y el Launcher se publica para los dos runtimes
  con la misma orden que el resto (`-p:ClicaloRuntimeIdentifier=<rid>`, nunca `-r`).
- Buena, porque el límite de confianza de ADR-0009 queda igual.
- Mala, porque el *lock file* crece con el grafo ARM64 (el paquete del compilador AOT de ese runtime).

### Confirmación

- `ShippedRuntimeTests` (`Clicalo.Architecture.Tests`, `[Trait("Req", "NFR-014")]`): todo ejecutable de `src/` declara
  `$(ClicaloRuntimeIdentifiers)`, toma su runtime de `ClicaloRuntimeIdentifier` y su *lock file* tiene el grafo de cada
  runtime distribuido. Fallaba con el Launcher antes del cambio.
- La restauración bloqueada de `cl desk` en `windows-11-arm` (`s0.yml`) y el trabajo `verify (arm64)` de `pr.yml`.

## Pros y contras de las opciones

### A. Las propiedades de «Shipped runtimes»

- Buena, porque es el patrón ya probado en `Clicalo.App` y `Clicalo.Sentinel`.
- Neutral, porque añade dos líneas al proyecto y un grafo al *lock file*.

### B. Sin modo bloqueado para el Launcher

- Buena, porque no cambia el proyecto.
- Mala, porque rompe NFR-014 justo en un ejecutable que cruza un límite de confianza, y NuGet rechaza desactivar los
  *lock files* en la línea de órdenes mientras existan (NU1005).

### C. `-r <rid>` al publicar

- Mala, porque `-r` es una propiedad global que llega a la restauración de todos los proyectos referenciados, cuyos
  *lock files* no tienen grafo por runtime: falla con NU1004 en la CI ([tooling.md](../architecture/tooling.md#lock-files)).
- Mala, porque no arregla la restauración de `cl desk` y de `verify`, que no publican.

## Criterios de reapertura

- Si Clícalo deja de distribuir uno de los dos runtimes o añade otro, cambia `ClicaloRuntimeIdentifiers` y este ADR se
  revisa junto con §11.

## Más información

- Plano: [§11](../architecture/blueprint.md#11-distribución-versionado-y-publicación); propuesta P5.
- Herramientas: [tooling.md, *lock files*](../architecture/tooling.md#lock-files).
- ADR relacionados: [ADR-0009](0009-elevacion-y-componente-de-sistema.md).
