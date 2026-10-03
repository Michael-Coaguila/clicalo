# Qué es vinculante de este paquete

Esta carpeta contiene el paquete de diseño original de Clícalo, **de solo lectura**: no se edita, ni
siquiera para corregir erratas. Las correcciones se registran en el
[catálogo de requisitos](../../requirements/catalog.md) (secciones 5 y 9). Esta nota, que no forma parte del
paquete original, explica qué partes obligan a la reconstrucción y cuáles no, **por instrucción expresa del
usuario**.

## Vinculante

Todo lo que define **qué hace el producto y cómo se ve y se usa**:

| Qué | Dónde |
|---|---|
| Funcionalidades, comportamientos, flujos y estados | [Prototipo v4](<prototype/Prototipo v4.dc.html>) y [docs 02–09](docs/02-modelo-de-datos.md) |
| Textos de la interfaz: las 669 claves en español e inglés, salvo las retiradas por una decisión del usuario (abajo) | [strings.es.json](data/strings.es.json) y [strings.en.json](data/strings.en.json) |
| Medidas táctiles (objetivos de 44×44 px lógicos como mínimo) y orden de los elementos | Prototipo v4, [docs/04](docs/04-panel-flotante.md) y [docs/07](docs/07-diseno-accesibilidad-idioma.md) |
| Colores de los temas, tamaños y ajustes de precisión táctil | [theme-palettes.json](data/theme-palettes.json) y [seed-and-catalogs.json](data/seed-and-catalogs.json) |
| Contenido inicial: perfiles de ejemplo, plantillas, biblioteca, grupos de teclas, iconos y combinaciones bloqueadas | [seed-and-catalogs.json](data/seed-and-catalogs.json) |
| Las 8 reglas que no se pueden romper y el glosario | [README.md](README.md) del paquete |
| Los hallazgos de la Auditoría, todos aceptados salvo la elección de tecnología | [Auditoría](<prototype/Auditoría.dc.html>) |
| Criterios de aceptación por área | [docs/09](docs/09-criterios-de-aceptacion.md) |

Grado de fidelidad, según el propio paquete: **alta** en comportamiento, estructura, textos, objetivos
táctiles, orden de los elementos y colores de tema; **media** en lo visual (radios, sombras y espaciados son
una guía que se adapta a los controles nativos). El icono de la app es provisional; el logotipo con la «í» se
mantiene.

## No vinculante

Todo lo que prescribe **cómo se construye**. Se decide desde primeros principios en el
[plano de arquitectura](../../architecture/blueprint.md) y en los [ADR](../../adr/README.md):

- la arquitectura recomendada, incluida la separación en procesos de motor e interfaz
  ([docs/01](docs/01-producto-y-arquitectura.md));
- reutilizar la lógica en Python de Macro Quick Access;
- la lista de tecnologías candidatas (la elegida es WPF sobre .NET 10 LTS,
  [ADR-0001](../../adr/0001-framework-ui-wpf.md));
- las APIs de Win32 concretas que se citan (por ejemplo `WS_EX_TOOLWINDOW`): lo vinculante es la capacidad
  que describen (no aparecer en Alt+Tab ni en la barra de tareas, no activarse), no la API;
- el plan de fases de [docs/10](docs/10-plan-de-fases.md), sustituido por la
  [hoja de ruta por hitos del plano](../../architecture/blueprint.md#14-hoja-de-ruta-por-hitos);
- la instrucción del [README.md](README.md) del paquete de implementar «por fases siguiendo
  `docs/10-plan-de-fases.md`».

## Retirado por decisión del usuario

El usuario puede retirar partes del paquete que sí eran vinculantes. Cada decisión se registra en la
[sección 6.2 del catálogo](../../requirements/catalog.md#62-decisiones-del-usuario), y este paquete sigue sin
editarse.

- **D1 · 2026-10-03 · Sin migración desde Macro Quick Access.** Clícalo no se basa en nada de la app anterior:
  no lee su `profiles.json` v1. Deja de ser vinculante todo lo que existía solo para esa migración: la pantalla
  previa de la bienvenida ([docs/06](docs/06-bienvenida.md)), la tarjeta de migración de Sistema › Copias del
  Prototipo v4 con sus textos `migT` y `migD`, y lo que [docs/02](docs/02-modelo-de-datos.md),
  [docs/09](docs/09-criterios-de-aceptacion.md) y [docs/10](docs/10-plan-de-fases.md) dicen de importar la v1.
  Todo lo demás del Prototipo v4 sigue siendo vinculante, incluidas la importación y la exportación del formato
  propio (copias, Combinar o Reemplazar, compartir un perfil) y las migraciones entre versiones del esquema propio
  ([ADR-0020](../../adr/0020-sin-migracion-desde-macro-quick-access.md)).

Tampoco son requisito los **defectos del prototipo**: simulaciones (escritorio y barra de tareas falsos,
usos y copias simulados), código muerto y fallos que violan una regla o un hallazgo aceptado. El catálogo los
razona uno por uno ([§5](../../requirements/catalog.md#5-discrepancias-resueltas) y
[§10](../../requirements/catalog.md#10-fuera-de-alcance-no-son-requisitos)).

## Cuando el paquete y el catálogo no coinciden

Manda el [catálogo](../../requirements/catalog.md): integra el paquete, la Auditoría y la app antigua, y
resuelve cada contradicción con la jerarquía de fuentes de su sección 0.1. Dos ejemplos:

- El README del paquete habla de 60 hallazgos; la Auditoría tiene 62 y el prototipo usa 669 claves de texto
  (DIS-89).
- Donde el prototipo y los documentos discrepan, la sección 5 del catálogo indica qué fuente manda en cada
  caso.

## Cómo se usa en el código

- Los archivos de `data/` son la **fuente** de los datos del producto: se importan una vez a `data/` en la
  raíz del repositorio (por ejemplo, los textos con `cl i18n-import`) y desde ahí los generan y validan las
  herramientas.
- Las pruebas localizan esta carpeta con `RepoPaths.Handoff` de `Clicalo.TestKit`; por ejemplo, la prueba
  de «texto visible idéntico» compara los textos que muestra la app con los de este paquete.
