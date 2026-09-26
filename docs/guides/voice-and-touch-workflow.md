# Programar con pantalla táctil y voz

Clícalo lo mantiene una persona que programa con la pantalla táctil y la voz, sin teclado físico. El
repositorio está diseñado para eso: cada tarea es una orden de una palabra, la salida termina en una línea
que Narrador lee de una vez, el formato no depende de la sangría y ningún paso exige un diseñador visual.
Esta guía reúne el flujo de trabajo recomendado. Sirve igual para cualquier persona que prefiera no usar el
teclado.

## Herramientas del sistema

| Herramienta | Para qué | Dónde |
|---|---|---|
| Acceso por voz | Controlar VS Code y la terminal por voz: «mostrar números», «clic 4», «clic Ejecutar tarea» | Windows 11 |
| Reconocimiento de voz de Windows | Lo mismo en Windows 10, donde no existe Acceso por voz | Windows 10 |
| Escritura por voz (Win+H) | Dictar texto en el editor, la terminal o un campo de texto | Windows 10 y 11 |
| Teclado táctil | Escribir lo que el dictado no resuelve bien (símbolos, identificadores) | Windows 10 y 11 |
| Narrador | Leer la salida de las órdenes, los errores y la documentación | Windows 10 y 11 |
| Clícalo (o Macro Quick Access hasta la 2.0) | Un botón por cada atajo de VS Code o tarea de `cl` | — |

## Una orden por tarea: `cl`

Todo el trabajo diario cabe en pocas palabras: `cl fast` mientras iteras, `cl fix` para dar formato y
`cl check` antes de abrir un PR. La lista completa está en
[tooling.md](../architecture/tooling.md#verbos-de-cl).

- **Termina siempre con `cl check`.** Hace exactamente lo mismo que la CI: si pasa en tu equipo, pasa en el
  PR.
- **La salida termina en una línea resumen** legible por Narrador (por ejemplo, si todo está en verde o
  cuántos errores hay). No hace falta recorrer cientos de líneas.
- **Los errores largos se escriben en un archivo Markdown** que se abre en VS Code, donde se leen por
  encabezados con Narrador en lugar de en la terminal.

Mientras `cl` se construye en M0, los equivalentes son `dotnet build Clicalo.slnx -m:2 -nodeReuse:false` y
`dotnet test --solution Clicalo.slnx`.

## Tres formas de lanzar `cl check`

1. **Con una tarea de VS Code** (la más fiable por voz). Está previsto que `.vscode/tasks.json` tenga una
   tarea por verbo. Abre la paleta de comandos, elige «Tasks: Run Task» y después la tarea; con Acceso por
   voz: «mostrar números» y «clic» con el número de la tarea. Si asignas a la tarea un atajo de teclado en
   VS Code, puedes lanzarla con un solo toque desde un botón de Clícalo.
2. **Dictando en la terminal.** Pon el foco en la terminal integrada, pulsa Win+H y dicta la orden. Revisa lo
   escrito antes de confirmar: el dictado puede poner mayúsculas o un punto final («Cl check.»). Si te pasa
   a menudo, desactiva la puntuación automática en la configuración de la escritura por voz.
3. **Con un botón de Clícalo** que escriba el texto `cl check` y pulse Intro: una macro de Texto más Pulsar
   Intro, sin dictado.

## Leer los errores con Narrador

- **Resumen:** la última línea de `cl` dice si todo está en verde o cuántos errores hay.
- **Panel de problemas de VS Code:** «View: Toggle Problems» desde la paleta de comandos (o su atajo)
  muestra la lista de errores; «Go to Next Problem» salta al siguiente y Narrador lee el mensaje.
- **Vista accesible de VS Code:** el comando «Open Accessible View» muestra el contenido del elemento
  enfocado (un mensaje, la salida de la terminal) como texto que se recorre línea a línea.
- **Señales de accesibilidad:** VS Code puede sonar cuando una tarea termina o falla y cuando la línea actual
  tiene un error (ajustes `accessibility.signals.*`). Así no hace falta mirar la terminal.
- **Modo lector de pantalla:** activa `editor.accessibilitySupport` (ver
  [preparar el entorno](dev-setup.md#ajustes-recomendados)).
- **Errores de datos:** los generadores dan la ruta, la línea y la columna exactas del JSON con el problema,
  así que «Go to Next Problem» te lleva directamente al sitio que hay que corregir.

## Escribir código por voz

- **No cuides la sangría ni los saltos de línea.** CSharpier da el formato al guardar (o con `cl fix`) y es
  determinista.
- **Navega con «Ir a definición».** No hay mediador ni mensajería global: «¿quién reacciona a X?» se
  responde saltando a la definición, sin buscar texto.
- **Los nombres del código están en inglés.** Si el dictado en español los deforma, dicta en inglés solo el
  identificador o escríbelo con el teclado táctil.
- **Deja que las máquinas vigilen las reglas.** Los analizadores y las pruebas de arquitectura te avisan de
  lo que se te escape; no hace falta recordar todas las reglas de [AGENTS.md](../../AGENTS.md).

## Trabajar con un agente de código

Dictar la intención a un agente y revisar el resultado es a menudo más rápido que escribir el código. El
agente lee [AGENTS.md](../../AGENTS.md) (y `CLAUDE.md` apunta a él) y recibe los mismos errores de los
analizadores que una persona. Pídele siempre que termine con `cl check` y revisa el *diff* por voz antes de
fusionar.

## Commits y PR sin teclado

- En la vista de control de código de VS Code, con `git.alwaysSignOff` activado, cada commit lleva el
  `Signed-off-by` del DCO sin escribirlo.
- Dicta el título del commit en inglés con el formato de Conventional Commits (`fix(touch): …`).
- `cl note` crea la nota de novedades para usuarios y `cl pr` abre el PR.

## Un perfil de Clícalo para programar

Mientras Clícalo 2 no esté publicado, Macro Quick Access cumple la misma función. Un perfil vinculado a
VS Code (`Code.exe`) con botones para lo que más se repite ahorra casi todo el dictado: guardar todo, abrir
la paleta de comandos, mostrar u ocultar la terminal, ir al siguiente problema, abrir la vista accesible,
ejecutar las tareas de `cl` a las que hayas asignado atajo, deshacer y rehacer.
