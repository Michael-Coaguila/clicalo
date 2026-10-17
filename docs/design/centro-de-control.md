# Centro de control con la dirección C

Cómo se compone el centro de control con el [sistema de diseño](sistema.md) (D12, D13, D14). Las decisiones que
cambian algo del catálogo están en R-35 ([§6.1 del catálogo](../requirements/catalog.md#61-propuestas-pendientes-de-ratificar)).

## Estructura fija

| Parte | Medida | Qué lleva |
|---|---|---|
| Ventana | 1120 × 680 por defecto, 760 × 520 como mínimo | Ventana de Windows de verdad: minimizar, maximizar con Snap, cerrar y bordes de redimensionado (D13) |
| Barra de título | `ComponentSize.TitleBar` (56) | Logo de 32, «Clícalo › Centro de control › sección» en todos los anchos, ES/EN de 44 sobre su tarjeta |
| Riel | 88 (`ControlCenterWindow.RailWidth`) | `OptionKind.Rail`: icono, contador al lado y etiqueta de hasta dos líneas; se desplaza si la ventana es baja |
| Barra de estado | `ControlHeight.Small` (44) | `StatusBar`: ✓ en `success` (⚠ para un aviso), mensaje en una línea, Deshacer terciario y el asa ↘ |

## «Atajos»

- **Arriba:** «Tu panel muestra» en una línea. Las fichas ceden ancho de la más ancha a la más estrecha y acaban en
  «…» (`ShareRow`); el texto y los «+» no ceden.
- **Tres columnas:** perfiles (216), atajos (flexible) y editor (376). El aviso de combinaciones repetidas va arriba
  de los perfiles. Las fichas son las del panel (`GridTiles`): número de voz o ⚠ arriba a la izquierda, tipo arriba a
  la derecha, «Incompleto» en el sitio de las teclas.
- **Editor:** las regiones se desplazan con su fundido; el pie (Probar, Duplicar, Eliminar) es fijo y reparte su
  ancho para que ningún texto se corte mientras otro botón tiene sitio.
- **Capas encima** (nunca bloques que empujan): la tarjeta del perfil y las opciones de su vinculación sobre la
  rejilla; «Probar» sobre las regiones del editor; el selector de iconos sobre los atajos y el editor.

### Puntos de corte

| Ancho del contenido | Diseño |
|---|---|
| Caben las tres columnas (unos 1040 de ventana o más) | Tres columnas; maximizada, la rejilla gana columnas (5 a 1371) |
| No caben (la ventana mínima) | Los perfiles son un selector de ancho completo junto a ✏ y «Añadir»; su lista es una capa. Al tocar una ficha, «Añadir» o «Revisar», el editor se abre como página con «Volver a {perfil}»; Esc vuelve |

## Selector de iconos (D14)

Una capa con el icono en uso, el título y ✕; la búsqueda con 🎤, que encuentra por las palabras en español y en inglés;
las categorías a la izquierda con «Todos» primero; y a la derecha los sugeridos y la rejilla. La rejilla (`IconGrid`)
recibe todos los iconos y solo construye las filas que se ven: es una pila virtualizada de filas que se reutilizan al
desplazarse. El editor de un atajo, la tarjeta de un perfil y «Perfil vacío» usan la misma capa con su `IIconPicker`.

## «Plantillas»

Una columna que se desplaza: la tarjeta de IA compacta (consentimiento y errores tras una línea, nunca en una tercera
caja), las apps abiertas sin perfil, las plantillas, «Perfil vacío», el teclado y los perfiles instalados. La vista
previa (376) solo aparece cuando hay algo en ella, con ✕ y el botón de instalar fijo abajo; en la ventana mínima es una
página.

## Cómo se comprueba

`ControlCenterAuditTests` recorre cada estado a 1371 × 859 (maximizada en la pantalla del usuario), 1300 × 760,
1120 × 680 y 760 × 520, en español y en inglés, en oscuro, claro y alto contraste con el texto al 150 %. Con
`CLICALO_GALLERY` guarda las capturas ([tooling](../architecture/tooling.md#galería-de-superficies)).
