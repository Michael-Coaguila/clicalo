# 07 · Diseño, accesibilidad e idioma

## Marca
- **Nombre**: **Clícalo**, siempre con tilde en la interfaz. Sin tilde (`clicalo`) solo en dominio, repositorio, carpetas (`%APPDATA%\Clicalo`), mutex y el esquema de enlaces `clicalo://`.
- **Eslogan**: «Lo que quieras hacer, clícalo.» / EN: «Whatever you want to do, clícalo.». El nombre no se traduce.
- **Logotipo**: «Cl» + **ı** (sin punto) + «calo». Sobre la ı va una **tilde en `accent`** (barra inclinada 30°, 0,13 em × 0,34 em, extremos redondeados). La tilde es la firma visual de la marca.
- **Icono**: provisional. Cuadrado redondeado `accent` con la ı en `onAccent` y la tilde convertida en chispa de clic, más un pequeño rayo. El autor lo sustituirá.
- **Burbuja minimizada**: icono `keyboard`, no el logotipo.

## Tipografía
- UI: **Atkinson Hyperlegible** 400/700, diseñada para baja visión.
- Teclas y procesos: **JetBrains Mono** 500.
- Iconos: **Material Symbols Rounded**, relleno 0 (1 en estados activos si se desea), grosor 400.
- Escala usada: 11 · 12 · 13 · 14 · 15 · 16 · 18 · 20 · 24 · 28 · 30 · 40 px, con altura de línea de 1,3 a 1,55 en párrafos.

## Colores (ver `data/theme-palettes.json`)
Tres temas; **Auto** sigue a Windows (claro/oscuro y alto contraste del sistema).
| Token | Oscuro | Uso |
|---|---|---|
| `panel` | oklch(0.2 0.012 260 / 0.96) | Fondo del panel |
| `win` | oklch(0.2 0.012 260) | Fondo del centro de control |
| `side` | oklch(0.175 0.01 260) | Menú lateral, barras |
| `card` / `cardHi` | 0.27 / 0.33 | Superficies / seleccionado |
| `text` / `muted` | 0.97 / 0.76 | Texto |
| `accent` | oklch(0.80 0.11 200) | Principal, Auto, selección |
| `onAccent` | oklch(0.2 0.03 220) | Texto sobre `accent` |
| `warn` | oklch(0.83 0.13 80) | Avisos, repetidos, números de voz |
| peligro | oklch(0.62 0.18 25) | Eliminar, Fijo, pánico |
- Colores de categoría: hue según `CAT` (`edit` 230, `hist` 60, `file` 150, `sel` 300, `win` 25, `voice` 190, `nav` 270, `fmt` 330, `web` 200, `text` 120). Luminosidad `tintL`; fondo de estado activo con alfa `washA`.
- **Alto contraste**: negro, blanco y amarillo #FFE600 puros; bordes de 2 px; todos los iconos de categoría en amarillo.

## Formas
- Radios: 6–8 en controles pequeños, 10 en botones, 12 en fichas y tarjetas, 14–16 en tarjetas grandes, 18 en el panel, 20 en modales.
- Sombras: panel `0 18px 50px /45 %`, modal `0 30px 80px /50 %`, menús flotantes `0 14px 40px /45 %`.
- Interruptores de 48×28 (knob de 20). Toda la fila es tocable.

## Accesibilidad (obligatorio)
- **Objetivos táctiles ≥ 44×44** en todo.
- **Contraste**: texto ≥ 4,5:1 y elementos gráficos ≥ 3:1 en los tres temas.
- **Anillo de foco**: 3 px en `accent`, offset de 2, en todo elemento interactivo; amarillo en alto contraste.
- **UI Automation** en cada control: Name (con el número de voz si está activo), ControlType, Toggle/Selection/ExpandCollapse según corresponda. Los avisos (barra de avisos, pánico) como regiones live (`LiveSetting = Polite/Assertive`).
- **Orden de tabulación** lógico y operación completa por teclado y conmutador (switch access).
- **Reducir movimiento**: seguir `SPI_GETCLIENTAREAANIMATION` y el ajuste propio.
- **Sin dependencia del color**: los estados llevan también icono o texto (Auto/Fijo, ACTIVO, ✓/⊘).
- **Dictado**: 🎤 junto a todo campo de texto libre; abre el dictado de Windows (Win+H) con el foco en el campo.
- Probar con **Acceso por voz de Windows** («clic 4», «mostrar números») y con **Narrador** antes de publicar.

## Idioma
- Todos los textos están en `strings.es.json` / `strings.en.json`, con las mismas claves. Las variables van entre llaves: `{a}` app, `{p}` perfil, `{n}` número, `{k}` teclas, `{i}` índice, `{t}` total, `{v}` versión, `{x}` nombre.
- El cambio de idioma es en caliente y afecta a todas las ventanas.
- Los nombres de atajos creados por el usuario se guardan en los dos idiomas con el mismo texto.
- Formatos: decimales con coma en español («0,5 s») y punto en inglés.
