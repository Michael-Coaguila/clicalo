---
status: Aceptado
date: 2026-10-22
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario
consulted: catálogo SIS-003, SIS-004 y REG-08; plano §3.1; ADR-0023 y ADR-0030; el fallo de la vista previa del mantenedor del 2026-10-22
informed: colaboradores y agentes de M7, mediante este ADR, `docs/architecture/contracts.md` y `docs/architecture/blueprint.md`
---

# ADR-0032 · Sentinel relanza una copia aislada con su carpeta de `--data` (protocolo 4)

## Contexto y planteamiento del problema

Un arranque con `--data <carpeta>` guarda todo dentro de esa carpeta y es una instancia propia
([ADR-0030](0030-instancia-aislada-con-datos-propios.md)). Pero Sentinel, al relanzar `Clicalo.exe` tras un fallo
([ADR-0023](0023-guardian-simple.md)), solo le pasaba `--after-crash=…` y `--safe-mode`: una copia aislada que envía
teclas volvía sobre la carpeta de datos de la persona. El 2026-10-22, los cuelgues de la Pestaña cerraban la vista
previa del mantenedor, Sentinel la relanzaba en `%APPDATA%\Clicalo` y un perfil creado allí aparecía y desaparecía según
cómo se abría la app.

Hay dos obstáculos más. Sentinel lee el diario de fallos de `%LocalAppData%\Clicalo`, pero la copia aislada lo escribe
dentro de su carpeta: si el relanzamiento conservara la carpeta sin más, los fallos de la copia nunca se contarían y un
bucle de fallos no llegaría nunca al modo seguro (SIS-004). Y `GuardianProcess.Start` rechazaba cualquier argumento con
espacios o comillas, así que una carpeta como `C:\Users\Ana\Mis datos` no podía viajar.

El reinicio como administrador no tiene este problema: una copia con `--data` nunca es la instalada y no lo ofrece
(`SystemLifecycle` no le da el ejecutable instalado y `ElevatedRelaunch` responde `NotInstalled`).

¿Cómo vuelve una copia aislada a su propia carpeta tras un fallo sin abrir una vía para colar argumentos en la línea de
órdenes?

## Factores de decisión

- REG-08 y SIS-003: una instancia trabaja siempre sobre una sola carpeta; nunca parece haber perdido lo hecho.
- SIS-004: tras un fallo, la copia se reabre igual que la de la persona, con el modo seguro al llegar al bucle de fallos.
- Sin `--data` no cambia nada: los mismos cuatro argumentos (con el número de versión nuevo), la carpeta normal y el
  diario de fallos en `%LocalAppData%\Clicalo`.
- Nada de una ruta puede añadir otro argumento (`--safe-mode`, un segundo `--data`): Clícalo nunca usa un intérprete.
- Simplicidad: una sola opción `--data` en `Clicalo.exe` y una sola regla para escribir líneas de órdenes.

## Opciones consideradas

1. La ruta como un *token* simple (Base64url o hexadecimal de su UTF-8) en el contrato de Sentinel y en el
   relanzamiento.
2. **Un entrecomillado correcto en un único sitio, con argumentos que no admiten comillas ni caracteres de control**
   (elegida).
3. La carpeta por otra vía: una variable de entorno heredada o un archivo junto a Sentinel.

## Resultado de la decisión

Opción elegida: la 2, porque la ruta viaja dos veces (del principal a Sentinel y de Sentinel al principal relanzado) y
el segundo receptor, `AppOptions`, ya lee `--data <carpeta>` como una ruta normal. Un *token* obligaría a añadir a
`Clicalo.exe` una segunda forma de `--data`, o a entrecomillar igualmente en el segundo salto. Un solo entrecomillado
sirve para los dos saltos.

1. **`CommandLineArguments`** (`Platform.Core/Guardian`) es la regla con la que `GuardianProcess.Start` escribe la línea
   de órdenes de Sentinel y del principal relanzado. Un argumento con una comilla o un carácter de control se rechaza:
   es un defecto, y ninguna ruta de Windows puede contenerlos. Un argumento con un espacio, o vacío, va entre comillas, y
   se duplican las barras invertidas de su final para que no escapen la comilla de cierre. Sin comillas dentro de un
   argumento, `CommandLineToArgvW`, el *runtime* de C de `Clicalo.exe` y el arranque de Native AOT lo parten igual.
2. **Protocolo 4 de Sentinel.** Los cuatro argumentos del protocolo 3 con `--protocol=4`, y un quinto,
   `--data=<carpeta>`, solo si el principal arrancó con `--data`. Sentinel rechaza el protocolo 3 y un quinto argumento
   que no sea `--data=` con una ruta completa (`Path.IsPathFullyQualified`) sin comillas ni caracteres de control.
3. **Sentinel usa la carpeta.** Lee el diario de fallos de `<carpeta>\crash-journal.json`, donde lo escribe la copia
   aislada, y relanza `Clicalo.exe` con `--after-crash=…`, `--safe-mode` si toca y `--data <carpeta>`.
4. El nombre `--data` vive en `CrashJournal.DataArgument`, junto a los demás argumentos del relanzamiento, y
   `AppOptions.DataOption` lo reutiliza.
5. **El reinicio como administrador no cambia.** No se ofrece con `--data`, así que no hay carpeta que perder; llevarla
   sería código que nunca se ejecuta en un límite de confianza.

**Qué sustituye** (los ADR aceptados no se editan): de ADR-0023, el punto 3 (protocolo 3, cuatro argumentos sin
espacios); lo demás de ADR-0023 sigue vigente. Resuelve la segunda consecuencia «Mala» y el tercer criterio de
reapertura de ADR-0030, que se corrigen en el mismo cambio porque ese texto aún no se había publicado.

### Consecuencias

- Buena, porque una copia aislada vuelve a su carpeta y a su instancia (el mismo sufijo de ADR-0030) tras un fallo, y
  llega al modo seguro con sus propios fallos.
- Buena, porque `Clicalo.exe` sigue teniendo una sola opción `--data` y las líneas de órdenes se leen tal cual en el
  Administrador de tareas.
- Buena, porque la regla del entrecomillado es una sola y pequeña: sin comillas dentro, solo importan los espacios y
  las barras del final.
- Mala, porque Sentinel ya no acepta solo *tokens* simples: un argumento puede llevar espacios. Lo compensan la lista
  cerrada de lo que no se admite y las pruebas de ida y vuelta con el divisor real de Windows.
- Neutral, porque el principal y Sentinel se publican juntos: el cambio de protocolo no exige compatibilidad hacia
  atrás.

### Confirmación

- `Clicalo.Sentinel.Tests` (`[Trait("Req", "SIS-004")]` y `REG-08`): el contrato del protocolo 4 y su ida y vuelta por
  `CommandLineToArgvW` con rutas con espacios, tildes, barra final, raíz de unidad y UNC; el rechazo de una carpeta
  relativa o con comillas; `RelaunchArguments` con y sin carpeta; el bucle relanza con la carpeta y lee el diario donde
  lo escribe el principal; `CommandLineArguments` devuelve los mismos argumentos y rechaza comillas y controles.
- `Clicalo.Platform.IntegrationTests`: `SentinelSupervisor` da la carpeta a Sentinel solo con `--data`.
- `Clicalo.App.Tests`: la línea que escribe Sentinel, partida por Windows y leída por `AppOptions`, da la misma
  carpeta, la misma instancia y el mismo diario de fallos; sin `--data`, la carpeta normal.

## Pros y contras de las opciones

### Ruta como *token* simple

- Buena, porque `GuardianProcess` seguiría aceptando solo *tokens* sin espacios.
- Mala, porque el principal relanzado necesitaría una segunda forma de `--data` en su contrato público, o habría que
  entrecomillar igualmente en ese salto.
- Mala, porque la línea de órdenes deja de ser legible al diagnosticar.

### Entrecomillado correcto (elegida)

- Buena, porque una sola regla sirve para el arranque de Sentinel y para el relanzamiento.
- Buena, porque prohibir las comillas la reduce a lo que una ruta necesita.
- Mala, porque un error en esa regla partiría un argumento: por eso se prueba contra `CommandLineToArgvW`.

### Otra vía (entorno o archivo)

- Buena, porque la línea de órdenes no cambiaría.
- Mala, porque Sentinel no escribe archivos y una variable de entorno heredada es un canal oculto que también llega a
  las apps que Clícalo abre.

## Criterios de reapertura

- Si algún día se habilita la elevación con `--data`: el reinicio como administrador tendría que llevar la carpeta, con
  la misma regla de entrecomillado.
- Si un argumento legítimo necesita comillas o caracteres de control.

## Más información

- Plano: [§3.1](../architecture/blueprint.md#31-vista-de-procesos).
- [contracts.md](../architecture/contracts.md) (protocolo 4 y relanzamiento).
- ADR relacionados: [ADR-0023](0023-guardian-simple.md) y [ADR-0030](0030-instancia-aislada-con-datos-propios.md).
- Documentación de Win32: `CommandLineToArgvW` y `CreateProcess`.
