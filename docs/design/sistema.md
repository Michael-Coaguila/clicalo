# Sistema de diseño de Clícalo (dirección C)

Desde el 2026-10-16 manda la dirección C, «Evolución del prototipo» (decisión **D12** del
[catálogo](../requirements/catalog.md#62-decisiones-del-usuario)): los colores, las fuentes y los iconos del prototipo v4
con un sistema estricto. La referencia visual es la [maqueta C](redesign-c/index.html) y sus capturas a 2x
([oscuro](redesign-c/maqueta-c-oscuro.png), [claro](redesign-c/maqueta-c-claro.png)). Siguen vinculantes las funciones,
los flujos, los textos de `data/i18n`, los objetivos de 44 × 44 (REG-02), UI Automation (REG-06) y no quitar el foco
(REG-01). La decisión técnica está en [ADR-0031](../adr/0031-rediseno-c-tokens-y-componentes-base.md); los tokens de
color, su contraste y su generador, en la [guía de tokens](../guides/design-tokens.md).

Esta guía dice qué tokens existen, qué componentes base hay, cómo es cada uno y cuándo se usa, y cómo se compone una
pantalla nueva con ellos.

## 1. Reglas que no se rompen

1. **Cero medidas sueltas.** Solo la escala de espacio, las tres alturas de control, los tres radios, las cajas de
   icono, los grosores y las medidas de componente de §2. Si algo no cabe, se rediseña con el sistema; nunca se mete
   un número. `LooseMeasureTests` lo vigila (§7).
2. **Un solo aspecto para «elegido».** Fondo lavado (`accentWash`), anillo de 2 por dentro (`accent`) e icono relleno
   en `accentInk`, o una ✓ si la opción no tiene icono. El control nunca cambia de tamaño: el borde va por dentro. Los
   botones de modo (Buscar, Editar, Ajustes rápidos) usan el mismo aspecto mientras su modo está activo.
3. **Cuatro botones:** primario, secundario, terciario y peligro (§3.1).
4. **Una sola etiqueta de sección:** `Overline` (§3.6).
5. **Monoespaciada solo para teclas,** con la notación «Ctrl+Shift+L».
6. **Cada color, un solo significado** (§2.7).
7. **Dos niveles de elevación** (superficie → control) y nunca más de dos bordes anidados.
8. **Los estados temporales son capas encima,** nunca bloques que empujan; las ventanas tienen tamaño fijo por diseño.
9. **Desplazamiento diseñado:** barra fina con tema en un canal reservado y fundido abajo (§3.9).
10. **Texto:** una línea con «…» (o dos donde se indique); los identificadores (WINWORD.EXE) nunca se parten (§5).
11. **El centro de control es una ventana de Windows de verdad** (D13, §3.14).

## 2. Tokens

Fuente de verdad: [`data/tokens/design-system.json`](../../data/tokens/design-system.json) (con su esquema
[`design-system.schema.json`](../../data/schemas/design-system.schema.json)) para las medidas y la tipografía, y
`theme-palettes.json` + `extra-tokens.json` para los colores. `TokenGenerator` los emite en
`Clicalo.UI.Wpf.Theming.Generated`; nunca se edita lo generado. Todas las medidas son píxeles lógicos (DIP).

### 2.1 Espacio · `Space`

| Token | Valor | Uso típico |
|---|---|---|
| `Space.S1` | 4 | Hueco icono-texto en columna, entre las filas de una ficha, separación interna mínima |
| `Space.S2` | 8 | Hueco entre fichas y botones, icono-texto en fila, barra fina |
| `Space.S3` | 12 | Margen del panel, relleno lateral de opciones y filas |
| `Space.S4` | 16 | Relleno de botones y tarjetas, separación entre secciones de una hoja |
| `Space.S6` | 24 | Relleno de las secciones del centro de control, separación entre bloques |
| `Space.S8` | 32 | Separación entre grupos grandes |
| `Space.S12` | 48 | Separación de pantallas |

Un margen negativo solo se escribe con un token (`-Space.S1`). La mitad de un token (`Space.S1 / 2`) es válida para
repartir un hueco entre dos vecinos.

### 2.2 Alturas de control · `ControlHeight` y `ControlSize`

| Token | Valor | `ControlSize` | Para |
|---|---|---|---|
| `ControlHeight.Small` | 44 | `Small` | Botón de icono, chip, tecla, botón de texto, segmento interior |
| `ControlHeight.Medium` | 52 | `Medium` | Campo, fila de lista, fila con interruptor, segmentado con su pista |
| `ControlHeight.Large` | 64 | `Large` | Tarjeta de opción, fila de navegación, ficha de la fila fija |

`ControlSizes.Height(ControlSize)` da el valor. Ninguna altura de control baja de 44 (REG-02).

### 2.3 Radios · `Radii`

| Token | Valor | Para |
|---|---|---|
| `Radii.Small` | 8 | Tecla, segmento interior, insignia de texto, ventana de Windows, caja del icono de una fila de navegación |
| `Radii.Medium` | 12 | Botón, ficha, campo, tarjeta de opción, fila |
| `Radii.Large` | 16 | Panel, tarjeta grande (`Card`) |
| `Radii.Pill` | 999 | Interruptor, chip, contador |

### 2.4 Cajas de icono · `IconBox`

El contenedor del icono es siempre un cuadrado fijo con el glifo centrado; nunca toma el tamaño del glifo.

| Token | Valor | Para |
|---|---|---|
| `IconBox.Badge` | 16 | Tipo de la ficha, ✓ del interruptor |
| `IconBox.Small` | 20 | Icono de un botón de texto, de una opción en fila, de la barra de estado y de la barra de título |
| `IconBox.Medium` | 24 | Fichas, cabecera, riel, botones de icono, filas |
| `IconBox.Large` | 32 | Vista previa del editor |

Peso 400; relleno (`SymbolIcon.IsFilled`) solo para «elegido» o «pulsado».

### 2.5 Grosores · `Stroke` y `FocusRing`

| Token | Valor | Para |
|---|---|---|
| `Stroke.Line` | 1 | Línea de reposo, siempre por dentro (2 en alto contraste: recurso `ThemeKeys.LineThickness`) |
| `Stroke.Selected` | 2 | Anillo interior de «elegido» y «pulsado»; contorno del interruptor apagado |
| `FocusRing.Thickness` / `Stroke.FocusRing` | 3 | Anillo de foco, en `focusRing` (TEM-009) |
| `FocusRing.Offset` / `Stroke.FocusOffset` | 2 | Separación del anillo de foco, por fuera |

### 2.6 Medidas de componente · `ComponentSize`

Medidas que la maqueta fija para un componente concreto y que no salen de las escalas.

| Token | Valor | Componente |
|---|---|---|
| `SwitchWidth` × `SwitchHeight` | 48 × 28 | Pista del interruptor |
| `SwitchKnobOff` / `SwitchKnobOn` | 16 / 20 | Pulgar apagado / encendido |
| `SliderTrack` / `SliderKnob` | 6 / 24 | Pista y pulgar del deslizador |
| `ScrollBar` / `ScrollThumbMin` / `ScrollFade` | 8 / 32 / 32 | Barra fina, largo mínimo de su pulgar y altura del fundido |
| `TitleBar` / `CaptionButton` | 56 / 56 | Barra de título y zonas de minimizar, maximizar y cerrar |

### 2.7 Tipografía · `TextStyles`

Familia `ui` = Atkinson Hyperlegible; `keys` = JetBrains Mono, solo para combinaciones de teclas. Ningún texto baja de
11 (TEM-007).

| `TextStyleToken` | Familia, peso, tamaño/interlineado | Para |
|---|---|---|
| `Display` | ui 700 28/34 | Título de una pantalla de bienvenida |
| `Title` | ui 700 20/26 | Título de una sección o de un perfil |
| `Heading` | ui 700 16/22 | Título de una hoja, nombre en la cabecera |
| `Body` | ui 400 15/22 | Texto corriente, campos, filas con interruptor |
| `BodySmall` | ui 400 14/20 | Descripciones, barra de estado |
| `Label` | ui 700 14/18 | Botones, opciones, nombres de ficha, títulos de fila |
| `LabelSmall` | ui 700 12/16 | Nombres de la fila fija, etiquetas del riel, contadores |
| `Caption` | ui 400 12/16 | Subtítulos de fila, ayudas |
| `Overline` | ui 700 12/16, versalitas, +0,06 em | La única etiqueta de sección (`Overline`) |
| `Keys` | keys 500 12/16 | Teclas en filas y textos |
| `KeysTile` | keys 500 11/14 | Línea de teclas de una ficha |

Recursos de tema (se repintan al cambiar el tema o la escala de texto): `ThemeKeys.FontSize(style)`,
`ThemeKeys.LineHeight(style)`, sus versiones escaladas `ThemeKeys.FontSize(style, scaled: true)` (solo nombres y teclas
de fichas y la fila fija, CUA-011) y `ThemeKeys.FontFamily(style)`. `StyledText.Apply(textBlock, style)` aplica todo a
cualquier `TextBlock`.

### 2.8 Colores

Cada color tiene un solo significado. En código siempre `ThemeBrushKey.For(ColorToken.X)` (referencia dinámica).

| Papel en C | `ColorToken` | Significado único |
|---|---|---|
| surface (nivel 0) | `Win` (`Panel` translúcido en el panel) | Fondo de ventanas y del panel |
| surface-2 (nivel 0b) | `Side` | Barra de título, riel, barra de estado, pista del segmentado |
| card (nivel 1) | `Card` | Controles y fichas, siempre por encima de su fondo |
| card-hover | `CardHi` | Estado «encima» (opcional en táctil) |
| field | `Field` | Fondo de campos |
| line | `Border` | Filete decorativo interior de reposo |
| line-strong | `Line` | Límite de control que debe verse (≥ 3:1): botón secundario y de peligro, campo, interruptor apagado |
| text / muted | `Text` / `Muted` | Texto principal y secundario |
| accent | `Accent` | Interactivo o elegido: relleno del primario, anillo de elegido, interruptor encendido |
| accent-ink | `AccentInk` | Texto e iconos de acento: terciario, icono elegido, enlaces |
| on-accent | `OnAccent` | Texto sobre `Accent` |
| accent-wash | `AccentWash` | Fondo de lo elegido o pulsado |
| warn / warn-ink / warn-wash / on-warn | `Warn` / `WarnText` / `WarnWash` / `OnWarn` | Atención: contadores, números de voz, repetidos, ficha armada |
| danger / danger-ink / danger-wash / on-danger | `Danger` / `DangerText` / `DangerWash` / `OnDanger` | Destructivo y pánico |
| success / on-success | `Success` / `OnSuccess` | Confirmación (✓ de guardado, enviado) |
| focus | `FocusRing` | Anillo de foco (amarillo en alto contraste) |

Los colores de categoría (`CategoryBrushKey.Tint(category)`) son solo para el icono de una ficha. El contraste de todos
los pares se comprueba al compilar (`contrast-pairs.json`, CLCT002).

## 3. Componentes base

Todos viven en `Clicalo.UI.Wpf.Controls` (salvo `TitleBar`, en `Clicalo.UI.Wpf.Windowing`; `OptionTemplates.For` da la plantilla de una opción a un control con su propio par de UI Automation), se construyen solo
con tokens, responden al menos en 44 × 44, muestran el anillo de foco de 3 y se exponen a UI Automation con un solo
patrón. Las plantillas son selladas y se crean en código. Un texto que se pasa como `Content` es texto de producto ya
localizado.

### 3.1 Botón · `TouchButton`

- **Propiedades:** `Content` (texto), `Symbol` (icono antes del texto, caja `IconBox.Small`), `Variant`
  (`ButtonVariant`), `Size` (`ControlSize`, fija `Height`), `MaxLines` (1 o 2), `IsExpanded` (`bool?`),
  `IsOn` (`bool?`), `IsActive` (`bool`). Constante `TouchButton.FillPart`.
- **Variantes:** `Primary` (relleno `Accent`, texto `OnAccent`), `Secondary` (`Card` con línea `Line`, texto `Text`),
  `Tertiary` (sin relleno ni línea, texto `AccentInk`; relleno `AccentWash` encima) y `Danger` (`Card` con línea
  `Line`, texto `DangerText`).
- **Anatomía:** radio `Radii.Medium`, relleno horizontal `Space.S4` (`Space.S3` en terciario), icono con hueco
  `Space.S2`, texto `Label` en una línea con «…».
- **Estados:** encima (lavado), pulsado o en modo (`IsOn` o `IsActive`: el aspecto único de «elegido»), deshabilitado
  (sin relleno ni línea y con el contenido en `Muted`, como en Windows, D19). Nunca cambia de tamaño.
- **UI Automation:** Button con Invoke; con `IsExpanded`, ExpandCollapse; con `IsOn`, Toggle. `IsActive` solo dibuja.
- **Cuándo:** una acción por lugar es primaria; las demás, secundarias; los enlaces y acciones discretas, terciarias;
  borrar o quitar, peligro (con dos toques, REG-04).

### 3.2 Botón de icono · `IconButton`

`TouchButton` sin texto: cuadrado de `ControlSize.Small` (44) o `Medium` (52), icono en `IconBox.Medium`. Terciario por
defecto con el icono en `Text`; `Secondary` añade la tarjeta y su línea (🎤, −, +). **Siempre** se le pone
`AutomationProperties.Name` con la acción localizada. Para la cabecera del panel: `IsActive` mientras su modo está
abierto.

### 3.3 Opción elegible · `OptionButton`, `SegmentedControl`/`SegmentedItem`

- **`OptionButton`** (`ToggleButton`; el estado es del modelo de vista, un toque solo lanza `Click`). Propiedades:
  `Kind` (`OptionKind`), `IsSelected` (= `IsChecked`), `Symbol`, `Caption` (fila de lista), `Count` (riel),
  `IsMonospace` (teclas), `Role` (`OptionRole`). Constante `OptionButton.FillPart`.
- **`OptionKind`:** `Tile` (tarjeta de 44 con su línea, icono o ✓ y texto en fila), `Card` (tarjeta de 64, icono
  sobre el texto), `Chip` (pastilla de 44), `Segment` (segmento sin relleno dentro de una pista), `ListRow` (fila de 52
  sin relleno: icono, título y subtítulo a la izquierda) y `Rail` (elemento del riel: icono con el contador al lado y
  etiqueta de hasta dos líneas).
- **Elegido:** `AccentWash`, anillo `Stroke.Selected` en `Accent` por dentro, icono relleno en `AccentInk`; si no hay
  icono y el contenido es texto, una ✓. Mismo tamaño en todos los estados.
- **Tema · `ThemeSwatch`:** la opción de tema es la misma en Ajustes rápidos (panel y Pestaña), «General y panel» y la
  bienvenida: `Tile` estirado con la muestra del tema (`IconBox.Small`) y el nombre a la izquierda. La muestra hace de
  icono, así que el tema elegido la «rellena»: disco `Accent` con ✓ en `OnAccent`, como el pulgar de un interruptor
  encendido; no se reserva hueco para una ✓ al final. Ajustes rápidos usa dos columnas mientras todos los nombres caben
  enteros (`ThemeSwatch.WholeWidth`) y, si no, una.
- **Línea discontinua · `DashedLine`:** una sola raya (`Stroke.Selected` por unidad de grosor, en `Line`) solo para
  los huecos de añadir, nunca para un control deshabilitado (D19) («+ Añadir» de edición, «Biblioteca», «+ Más» de los perfiles,
  la rejilla vacía). Un hueco de añadir lleva el icono en `AccentInk` encima del nombre en `Text`.
- **`OptionRole`:** `Option` (RadioButton con SelectionItem, por defecto en `OptionButton`), `Toggle` (Toggle) y
  `Expander` (ExpandCollapse).
- **`SegmentedControl`:** grupo de una sola elección (List con Selection; cada `SegmentedItem` es ListItem con
  SelectionItem). `Kind = Segment` (por defecto) dibuja la pista de `ControlHeight.Medium` en `Side` con su línea y
  segmentos de 44 a `Space.S1`; `Tile` o `Card` dibuja tarjetas a `Space.S2`. `Columns` reparte en rejilla.
- **Cuándo:** elegir uno de varios (Vista, Tamaño, Tema, el tipo de un atajo), marcar teclas, perfiles de una lista,
  secciones del riel.

### 3.4 Interruptor · `ToggleSwitch`

Fila entera tocable de al menos `ControlHeight.Medium`: icono `Symbol` en `Muted` (caja 24), texto `Body` (hasta dos
líneas), `Description` opcional en `Caption` y `Muted`, y la pista de 48 × 28 al final. Apagado: contorno de 2 en
`Line` y pulgar de 16 en `Muted`; encendido: pista `Accent` y pulgar de 20 en `OnAccent` con ✓. UI Automation: Button
con Toggle. `FollowsViewModel = true` cuando el estado es del modelo de vista (un toque solo lanza `Click`).
`IsDrawingOnly` lo esconde de UI Automation cuando es solo el dibujo de otra fila.

### 3.5 Deslizador · `StepSlider`

`IconButton` secundarios − y + de 44, pista de 6 a `Space.S3` de cada botón (parte llena `Accent`, resto `Line`) y
pulgar de 24 en `Accent`. Slider con RangeValue; − y + se nombran con `DecreaseName` e `IncreaseName`. El valor se
muestra fuera, a la derecha del `Overline` de su sección, en `Label`.

### 3.6 Etiqueta de sección · `Overline`

`new Overline(texto)`: estilo `Overline` en `Muted`, en mayúsculas con +0,06 em; una línea con «…». UI Automation la
lee como texto con las palabras tal como se escribieron. Es la **única** etiqueta de sección: nada de títulos en gris
de otra forma.

### 3.7 Campo de texto · `TextInput`

`new TextInput(nombre, textoDeEjemplo, multiline)`: `ControlHeight.Medium` (o tres líneas), radio `Radii.Medium`,
fondo `Field` con su línea `Line` por dentro, texto `Body`. `Symbol` pone un icono al principio (la lupa de una
búsqueda). `DictateName` muestra 🎤 (secundario de 52, a `Space.S2`) que lleva el teclado al campo y lanza
`DictateRequested`. Con el teclado dentro, la línea pasa a `Accent` y aparece el anillo de foco de 3, separado 2
(TEM-009). `Warn(true)` pinta la línea en `WarnText`. API: `Box`, `Text`, `Show(texto, force)`, `SetPlaceholder`,
`Changed`, `Left`, `FocusRing`, `Dictate`.

### 3.8 Fila de navegación · `NavRow`

Botón de `ControlHeight.Large` en `Card` con su línea: caja de 44 con fondo `AccentWash` y el icono `Symbol` en
`AccentInk`, título (`Content`, `Label`, una línea) con `Caption` (dos líneas) y un chevron en `Muted`. Invoke; el
subtítulo es su texto de ayuda. Para ir a otro lugar (Ajustes rápidos → Centro de control).

### 3.9 Desplazamiento · `ThemedScrollViewer`

Zona vertical con la barra fina de `ComponentSize.ScrollBar` en `Line`, sin flechas, con un pulgar de al menos
`ComponentSize.ScrollThumbMin` aunque el contenido sea muy largo, en un canal reservado a la derecha
aunque no haya barra (nada se mueve cuando aparece), y fundido de `ComponentSize.ScrollFade` abajo mientras queda
contenido debajo (`IsFading`). La barra no es un control para UI Automation; la zona se desplaza con el patrón Scroll.
`PansWithMouse` (por defecto, el centro de control) desplaza con el dedo como ratón promovido; las superficies del panel
lo apagan porque las desplaza su capa de punteros. `CancelPan()` lo suelta cuando un arrastre de ficha toma el gesto.

### 3.10 Ficha de atajo · `ShortcutTile` (`Clicalo.UI.Wpf.Automation`)

Anatomía de huecos reservados que nunca se pisan: número de voz arriba a la izquierda (`VoiceNumber`, pastilla `Warn`
con `OnWarn`), tipo arriba a la derecha (`TypeSymbol`, icono de 16 en `Muted`; relleno en `AccentInk` si la ficha
está elegida o mantenida y en `WarnText` si está armada), icono en su caja (`IconSize`, 24 por defecto) con el tinte de su `Category`, nombre
(`AccessibleName`, `Label`, una línea con «…»; `MaxNameLines = 2` donde CUA-011 lo pide) y línea de teclas (`Keys`, en
JetBrains Mono y `Muted`, solo si cabe). El estado en palabras (`AccessibleState`: «Manteniendo», «ACTIVO», «Paso
2/5», ACC-003 y EJE-010) ocupa el lugar de la línea de teclas, en la fuente de la interfaz y `AccentInk` (`WarnText`
si está armada), y nunca el hueco de arriba, demasiado estrecho junto al número y al icono. Fondo `Card` con su línea por dentro, radio `Radii.Medium`. Estados:
Alternar activo y mantener (el aspecto único de elegido; mantener además escala 0,95), armado (`WarnWash` y anillo
`WarnText`), destello (`AccentWash`), tecla fija bloqueada (relleno `Accent`), menú abierto (anillo). Tamaños de la
maqueta, aligerados en D19: rejilla de 84 × 68, 100 × 76 y 124 × 92 en S, M y L, y fila fija de 64 con nombre en M y L
o de 44 con solo iconos en S y en Compacta.

### 3.11 Barra de estado · `StatusBar`

Fila de `ControlHeight.Small` en `Side` con su línea arriba: icono según `Tone` (`StatusTone.Success` = ✓
`check_circle` en `Success`; `Warning`; `Danger`; `Neutral` sin icono), mensaje en `BodySmall` en una línea con «…»
(`Text`, y `Message` para el `LiveAnnouncer`) y `Action` al final (Deshacer, Repetir). Es su propia fila: nunca tapa el
contenido.

### 3.12 Insignia y contador · `Badge`, `Counter`

`Counter` (`Count`; 0 no ocupa nada): pastilla de 20 en `Warn` con el número en `OnWarn`. `Badge` (`Text`, `Tone`):
palabra corta de 20 de alto, radio `Radii.Small`; `BadgeTone.Neutral` (`Card` y `Muted`), `Accent` (`Accent` y
`OnAccent`) y `Warn`. Los dos son decorativos para UI Automation: su control dice lo mismo en su estado o su nombre. Van
en un hueco propio, nunca encima de un icono.

### 3.13 Tarjeta y texto · `Card`, `StyledText`

`Card` (`Tone`: `Neutral`, `Accent`, `Warn`, `Danger`): contenedor de radio `Radii.Large` y relleno `Space.S4`, segundo
nivel de elevación; nunca dentro de otra tarjeta. `StyledText` (`TextStyle`, `Ink`, `MaxLines`, `IsScaled`): todo texto
de la escala con su regla de desbordamiento.

### 3.14 Barra de título · `TitleBar` (`Clicalo.UI.Wpf.Windowing`) y `CaptionButton`

`TitleBar` de `ComponentSize.TitleBar` en `Side`: `Leading` (logo y ruta, arrastra la ventana), `Trailing` (idioma) y
los `CaptionButton` `Minimize`, `Maximize` y `Close` en zonas de 56 × 56 (cerrar en `Danger` encima). `Attach(window,
close)` pone el `WindowChrome`, responde `HTMAXBUTTON` sobre maximizar (menú de Snap de Windows 11), redimensiona por los
bordes y por `ResizeGrip`, y con la ventana maximizada mantiene el contenido dentro del área de trabajo de su monitor.
`SetNames(min, maximizar, restaurar, cerrar)` nombra los botones con [min], [winMaximize] o [winRestore] y [close]. La
ventana conserva el WindowPattern (maximizar, restaurar, minimizar) y nunca se activa sola.

## 4. Capas, elevación y ventanas

- **Dos niveles:** superficie (`Win`, `Panel`, `Side`) → control o ficha (`Card`). En claro las fichas son blancas sobre
  gris; en oscuro, más claras que su fondo. Nunca más de dos bordes anidados: una hoja va al mismo nivel que el panel y
  sus controles son el segundo nivel.
- **Estados temporales en capas:** Ajustes rápidos, menús, avisos y sugerencias se dibujan encima del contenido (otra
  fila del mismo `Grid` con `Visibility`), nunca lo empujan. Los botones no se mueven bajo el dedo.
- **Tamaño fijo por diseño:** cada ventana o superficie tiene su tamaño; el contenido se adapta dentro (con
  `ThemedScrollViewer`), nunca con `SizeToContent` sobre lo que cambia.
- **Sombras:** solo las del marco de la ventana (DWM o la sombra precalculada de la superficie).

## 5. Desbordamiento del texto

| Caso | Regla | Cómo |
|---|---|---|
| Nombre, título, botón, opción | Una línea con «…» | `StyledText` (`MaxLines = 1`), texto de un control |
| Donde la maqueta lo indica (descripciones, riel, nombre de ficha en S al 150 %) | Dos líneas con «…» | `MaxLines = 2`, `ShortcutTile.MaxNameLines = 2` |
| Párrafos (Acerca de, ayudas largas) | Sin límite, en una zona que se desplaza | `MaxLines = 0` dentro de `ThemedScrollViewer` |
| Identificadores (WINWORD.EXE, rutas, teclas) | Nunca se parten | Una línea con «…»; en `Keys` si son teclas |
| Palabra de un nombre de ficha del panel que no cabe con texto grande | La rejilla pasa a 2 columnas y el nombre se ve entero (D17) | `GridMetrics.Columns`; la vista mide con `TileNameFit` |

El nombre completo siempre llega a UI Automation, aunque se vea con «…». La auditoría falla si un texto sin «…» se
dibuja cortado.

## 6. Cómo se compone una pantalla nueva

1. **Estructura fija:** decide el tamaño de la ventana o la superficie y sus filas fijas (cabecera, contenido con
   `ThemedScrollViewer`, `StatusBar`). Las capas temporales van encima.
2. **Espaciado:** márgenes y huecos solo con `Space.*`; agrupa con `StackPanel`/`Grid` y separa grupos con
   `Space.S4`–`S6`.
3. **Secciones:** cada una con su `Overline` arriba (a `Space.S2` del contenido).
4. **Acciones:** una primaria por lugar, el resto secundarias o terciarias; destructivas en `Danger` con dos toques.
5. **Elecciones:** `SegmentedControl` u `OptionButton` del `OptionKind` que corresponda; nunca un color propio para
   marcar lo elegido.
6. **Textos:** `StyledText` con su estilo y su regla de §5; la monoespaciada solo para teclas.
7. **Colores:** solo `ThemeBrushKey.For(ColorToken.*)` con el significado de §2.8.
8. **Comprueba:** `cl check` (incluye `LooseMeasureTests` y el contraste) y la galería en los tres temas (§7).

```csharp
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

var size = new SegmentedControl { Kind = OptionKind.Tile, Columns = 3 };
foreach (var label in labels) { size.Items.Add(new SegmentedItem { Content = label }); }

var section = new StackPanel();
section.Children.Add(new Overline(vm.SizeTitle) { Margin = new Thickness(0, 0, 0, Space.S2) });
section.Children.Add(size);

var save = new TouchButton { Content = vm.Try, Symbol = "play_arrow" };                // primario
var copy = new TouchButton { Content = vm.Duplicate, Variant = ButtonVariant.Secondary };
var name = new TextInput(vm.NameLabel, vm.NamePlaceholder) { DictateName = vm.DictateName };
```

## 7. Cómo se comprueba

- **`cl check`:** contraste de todos los pares (CLCT002), escalas de C (`DesignSystemDataTests`), controles base
  (`BaseControlTests`, `ShortcutTileLookTests`, `TitleBarTests`) y la guardia de medidas sueltas
  (`LooseMeasureTests`): `architecture/loose-measures.json` guarda los números que quedan por archivo y solo puede
  bajar. Cuando una pantalla mejora, la prueba pide bajar su cifra en el mismo cambio.
- **Galería** (`CLICALO_GALLERY`, ver [tooling](../architecture/tooling.md#galería-de-superficies)): cada estado en
  oscuro, claro y alto contraste; la hoja «Sistema de diseño» muestra todos los componentes base
  (`DesignSystemAuditTests`). Nada se da por bueno sin verlo en una captura.

## 8. Estado de la migración

Los controles antiguos (`ButtonAppearance`, `Chip`, `CcButton`, `CcToggle`, `CcChrome`, `TextField`,
`TouchPanScrollViewer`, `SurfaceScrollViewer`) ya no existen: las vistas usan los componentes de §3, a menudo a través de
los atajos `Ui.Button`, `Ui.Choice`, `Ui.SwitchRow` y `Ui.Caption` del centro de control, que traducen los rellenos
antiguos a variantes (`Ui.VariantOf`). Las medidas sueltas que quedan en las vistas y su composición se recorren pantalla
a pantalla en M7.
