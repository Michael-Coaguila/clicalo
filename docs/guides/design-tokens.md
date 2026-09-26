# Tokens de diseño

Guía de los colores, formas y tiempos de movimiento de Clícalo: de dónde salen, cómo se convierten, cómo se garantiza el contraste y cómo se consumen desde WPF.

- **Fuente de verdad:** `data/tokens/` (cinco archivos JSON).
- **Código generado:** espacio de nombres `Clicalo.UI.Wpf.Theming.Generated`, producido por `TokenGenerator` (perfil `UiWpf`). Nunca se edita a mano.
- **Matemática:** `generators/Clicalo.Design.Math` (netstandard2.0, sin dependencias), enlazada en el generador y usada por las pruebas.
- **Requisitos:** TEM-001 a TEM-009, ACC-003, ACC-009 y NFR-007 del catálogo. **Plano:** §8.4 y §3.2 regla 8.

## 1. Flujo

```
data/tokens/theme-palettes.json   copia fiel del paquete de diseño
data/tokens/extra-tokens.json     tokens nuevos, categorías, correcciones, formas
data/tokens/contrast-pairs.json   pares texto/fondo y gráfico/fondo con su mínimo
data/tokens/hc-system-map.json    colores de sistema del alto contraste de Windows
data/tokens/motion.json           duraciones y su valor con reducir movimiento
      │
      ▼  TokenGenerator (Clicalo.Generators, perfil UiWpf)
      │    lee y valida → corrige → resuelve alias → OKLCH → sRGB (mapeo de gama CSS Color 4)
      │    → 8 bits → mide cada par sobre el fondo compuesto real → errores CLCT001…CLCT007
      ▼
Clicalo.UI.Wpf.Theming.Generated
  ColorToken · CategoryToken · ThemePalette · ThemePalettes · SystemHighContrastPalette
  Radii · FocusRing · ShadowSpec · Shadows · MotionToken · Motion
      │
      ▼  pegamento escrito a mano (Clicalo.UI.Wpf.Theming)
  ThemeId · ThemeCatalog.GetPalette(ThemeId)
```

Un error en los datos es un error de compilación con el archivo, la línea y la columna exactos. El contraste insuficiente también: no se puede compilar Clícalo con un par ilegible.

## 2. Archivos de `data/tokens`

Los miembros cuyo nombre empieza por `$` son comentarios. Las claves de datos son identificadores camelCase; el generador los convierte a PascalCase.

### 2.1 `theme-palettes.json`

Copia fiel de `docs/design/handoff/data/theme-palettes.json` (solo se añade el salto de línea final). Tiene los temas `dark`, `light` y `hc` con los tokens del prototipo, `tintL` y `washA` (categorías) y `"hc": true` en el tema de alto contraste. `border` usa la forma abreviada `1px solid <color>`: el generador separa el color (token `border`) y el grosor (`ThemePalette.BorderThickness`).

**No se edita.** Una prueba compara su contenido con el del paquete. Los cambios de valor se hacen como correcciones documentadas en `extra-tokens.json` (§6).

### 2.2 `extra-tokens.json`

| Sección | Contenido |
|---|---|
| `themes` | Nombre C# de cada tema: `dark` → `Dark`, `light` → `Light`, `hc` → `HighContrast`. |
| `colors` | Tokens que exige TEM-002 y que el paquete no tiene, por tema. Un valor que es el nombre de otro token del mismo tema es un **alias** (`"focusRing": "accent"`). |
| `categories` | Tonos CAT y fórmula de TEM-003: tinte `oklch(tintL tintChroma tono)`, fondo activo `oklch(washLightness washChroma tono / washA)`; los temas con `"hc": true` usan `highContrast` (#FFE600 sobre #333000). |
| `corrections` | Correcciones de contraste de TEM-004, con `from`, `to`, `requirement` y `reason` (§6). |
| `radii` | Radios de esquina (docs/07 «Formas»). |
| `shadows` | Sombras precalculadas: desplazamiento, desenfoque y opacidad. |
| `focusRing` | Grosor (3) y separación (2) del anillo de foco (TEM-009). |
| `gamutMapping` | `maxDeltaEOK`: cuánto puede mover el mapeo de gama un color fuera de sRGB (§3.3). |

### 2.3 `contrast-pairs.json`

| Sección | Contenido |
|---|---|
| `minimums` | `text` 4,5 (WCAG 1.4.3) y `graphic` 3 (WCAG 1.4.11). El generador rechaza valores menores. |
| `backdrops` | Escritorios que pueden verse a través del panel translúcido: los 8 vértices del cubo sRGB, de `#000000` a `#ffffff` (§4). |
| `decorative` | Tokens sin mínimo, cada uno con su motivo: `desk`, `border`, `scrim`, `shadow`. |
| `pairs` | `kind` (`text` o `graphic`), `foreground`, `backgrounds` y `use` (dónde se usa). |

Cada fondo es una pila de capas escrita de arriba abajo con `over`: `"accentWash over card"` es `accentWash` pintado sobre `card`. Los seudotokens `categoryTint` y `categoryWash` hacen que el par se compruebe para cada una de las diez categorías.

**Todo token necesita una decisión de contraste:** o aparece en algún par, o está en `decorative` con su motivo. Si no, CLCT005.

### 2.4 `hc-system-map.json`

Con un tema de contraste de Windows activo, Clícalo usa siempre los colores del sistema (TEM-001, PQ-13). El archivo declara los colores de sistema (`COLOR_*` y su propiedad de `System.Windows.SystemColors`), el color de sistema de cada token y de las categorías, y la lista de pares que el tema de Windows garantiza legibles (`guaranteedPairs`). Una prueba comprueba que **cada par de `contrast-pairs.json` cae en un par garantizado**.

| Papel | Color de sistema |
|---|---|
| Superficies y velos (`panel`, `win`, `card`, `field`, `*Wash`…) | `window` |
| Texto, bordes y texto semántico (`text`, `muted`, `line`, `warnText`, `dangerText`…) | `windowText` |
| Rellenos de acento, aviso, peligro y éxito; anillo de foco; tinte de categoría | `highlight` |
| Texto sobre esos rellenos (`onAccent`, `onWarn`, `onDanger`, `onSuccess`) | `highlightText` |

Los estados nunca dependen solo del color (ACC-003), así que el alto contraste del sistema puede fundir acento, aviso y peligro en `highlight`.

### 2.5 `motion.json`

Duraciones en milisegundos (`ms`) y su valor con reducir movimiento (`reducedMs`), que no puede ser mayor. Con reducir movimiento todo pasa a 0 salvo `flash`, que se mantiene 240 ms como cambio de color sin animación (TEM-006).

| Token | ms | Reducido | Uso |
|---|---|---|---|
| `panelOpacity` | 350 | 0 | Opacidad y atenuado del panel |
| `pressScale` | 80 | 0 | Escala de la ficha al pulsar |
| `background` | 150 | 0 | Cambio de fondo |
| `pageDots` | 200 | 0 | Indicadores de página |
| `progress` | 200 | 0 | Barras de progreso |
| `switchKnob` | 150 | 0 | Interruptores |
| `keyChip` | 120 | 0 | Teclas al grabar o probar |
| `flash` | 240 | 240 | Destello tras ejecutar |

`panelOpacity` es la misma duración que `Timings.Dimming.DimTransition` de `data/catalogs/timings.json`, que usa `DimPolicy`; y los tonos de `extra-tokens.json` → `categories.hues` son exactamente los ids de `data/catalogs/categories.json`. `CatalogConsistencyTests` (en `Clicalo.Data.Tests`) comprueba las dos cosas, para que un cambio en un archivo no deje al otro desfasado. Los tamaños S/M/L no son tokens: salen de `data/catalogs/sizes.json` (`Clicalo.Domain.Catalog.PanelSizes`).

## 3. Matemática del color (`Clicalo.Design.Math`)

### 3.1 Conversión

`OKLCH → OKLab → LMS′ → LMS → sRGB lineal → sRGB`, como en CSS Color 4:

1. **OKLCH → OKLab:** `a = C·cos h`, `b = C·sin h`.
2. **OKLab → sRGB lineal:** matrices publicadas por Björn Ottosson (las que adopta CSS Color 4): `M2⁻¹`, cubo, y la matriz LMS → sRGB lineal.
3. **sRGB lineal → sRGB:** función de transferencia sRGB (tramo lineal hasta 0,0031308), extendida simétricamente a negativos.

La inversa usa las matrices de Ottosson y raíz cúbica con signo. Las pruebas comparan la conversión con una implementación independiente por la ruta XYZ D65 de CSS Color 4 (tolerancia 1·10⁻⁷ en 5000 muestras), con vectores de referencia (`oklch(0.62796 0.25768 29.2339)` = `#FF0000`, blanco, negro, primarios) y con los valores hexadecimales de todas las paletas y categorías, calculados aparte con Python y fijados en la prueba.

### 3.2 Cuantización y composición

- El color que se genera es de **8 bits por canal** (`Rgba8`), que es lo que guarda y pinta `System.Windows.Media.Color`. Redondeo a la mitad hacia arriba, con recorte a [0, 1].
- La composición es **source-over sobre sRGB codificado con alfa directa**, la mezcla por defecto de WPF.

### 3.3 Mapeo de gama

Algoritmo de CSS Color 4 §13.2: si el color no cabe en sRGB se busca, a luminosidad y tono constantes, la mayor croma cuyo recorte quede a menos de una diferencia apenas perceptible (ΔEOK < 0,02, JND) del color reducido; búsqueda binaria con ε = 0,0001. `L ≥ 1` da blanco y `L ≤ 0` da negro; si además la croma no es 0, el color cuenta como fuera de gama y su ΔEOK hasta el blanco o el negro puede dar CLCT003. ΔEOK es la distancia euclídea en OKLab.

Trece colores de los datos quedan fuera de sRGB (TEM-003 contaba once en el paquete original; las correcciones cambian cuáles); el mapeo los mueve como mucho 0,040. `maxDeltaEOK` = 0,05 (2,5 JND) deja margen y detecta errores de verdad, como una croma de 0,3 escrita por descuido (CLCT003).

### 3.4 Luminancia y contraste

Luminancia relativa de WCAG 2.x (`0,2126 R + 0,7152 G + 0,0722 B` sobre canales lineales; umbral 0,04045, el de sRGB y de la fe de erratas de WCAG 2.2; con 8 bits da lo mismo que 0,03928) y contraste `(L1 + 0,05) / (L2 + 0,05)`, de 1 a 21.

## 4. Cómo se mide el contraste

1. Se usan los **colores de 8 bits** que se van a pintar, no los valores OKLCH ideales.
2. El fondo es la **pila real**: las capas se componen de abajo arriba y el primer plano, aunque sea translúcido (como `line`), se compone encima.
3. Si la pila no es opaca (el panel es translúcido, α 0,96 y 0,97), se compone **sobre cada backdrop** y cuenta el peor caso. Los backdrops son los 8 vértices del cubo sRGB (negro, blanco, primarios y secundarios):
   - La luminancia del compuesto crece con cada canal del escritorio, así que el negro y el blanco acotan la luminancia del fondo. Con un primer plano opaco, el peor caso está en uno de los dos extremos, salvo que el primer plano quede entre ambos.
   - Si el primer plano es más claro que el fondo sobre un backdrop y más oscuro sobre otro, algún escritorio intermedio los iguala: `ContrastEvaluator` lo detecta, busca ese escritorio por bisección y da 1:1.
   - Con un primer plano translúcido (`line` sobre `panel`), el primer plano también depende del escritorio y un escritorio de color puede dar algo menos que el negro o el blanco: `line` sobre `panel` en el tema oscuro es peor sobre `#00ffff` (3,108:1) que sobre el blanco (3,111:1). Por eso se usan los 8 vértices y no solo los dos extremos.
   - No se pueden recorrer los 16,7 millones de escritorios. Una prueba (`No_desktop_color_behind_the_panel_breaks_a_pair`) mide todos los pares sobre una rejilla de 729 escritorios, con los vértices incluidos, y confirma que ninguno queda por debajo del mínimo.
4. Se mide con la opacidad del usuario al 100 %. El estado atenuado queda exento mientras dura (TEM-004, PQ-42).
5. Los pares con `categoryTint` o `categoryWash` se miden para cada categoría.

Un par por debajo de su mínimo es CLCT002 en la entrada de `backgrounds` que falla.

## 5. Tokens y papeles

| Familia | Tokens | Papel |
|---|---|---|
| Superficies | `panel` (translúcido), `win`, `side`, `card`, `cardHi`, `field` | Fondos. `desk` es el escritorio simulado del prototipo (decorativo). |
| Texto | `text`, `muted` | Texto principal y secundario. |
| Acento | `accent`, `onAccent`, `accentWash` | Selección, Auto, enlaces; texto sobre acento; fondo de lo seleccionado. |
| Aviso | `warn`, `onWarn`, `warnWash`, `warnText` | Relleno y bordes de aviso (números de voz, ficha armada); texto sobre el relleno; fondo de avisos; texto e iconos de aviso sobre superficies. |
| Peligro | `danger`, `onDanger`, `dangerWash`, `dangerText` | Relleno y bordes (Eliminar armado, pánico); texto sobre el relleno; fondo de peligro; texto e iconos de peligro sobre superficies. |
| Éxito | `success`, `onSuccess` | Marca de acierto del Modo prueba e icono sobre ella. |
| Líneas | `line`, `border` | `line` es el límite que identifica un componente (contorno de campos, huecos para añadir) y cumple 3:1; `border` es un filete decorativo. |
| Foco | `focusRing` | Alias de `accent`; amarillo en alto contraste (TEM-009). |
| Decorativos | `scrim`, `shadow` | Velo tras un modal; color de las sombras. |

**Relleno y texto separados en aviso y peligro.** Un solo color no puede servir a la vez de relleno con texto encima y de texto sobre las superficies en todos los temas:

- En el tema claro, el texto oscuro de los números de voz (ACC-009: «fondo warn con texto oscuro») exige un `warn` claro, y el texto de aviso sobre superficies claras exige uno oscuro.
- En el tema oscuro, el texto blanco sobre el rojo de peligro exige un `danger` oscuro, y el texto rojo sobre superficies oscuras exige uno claro.

Por eso `warnText` y `dangerText` son tokens propios. En el tema oscuro `warnText` es un alias de `warn`. `dangerText` conserva el rojo de texto que el prototipo ya usaba aparte (`oklch(0.72 0.16 25)`).

## 6. Correcciones de TEM-004

**Método.** El mínimo cambio de luminosidad OKLCH (paso 0,001) que cumple todos los pares en los que interviene el token, conservando croma y tono. En las líneas neutras translúcidas (blanco o negro con alfa) la luminosidad no puede moverse en la dirección útil, así que se corrige el alfa (paso 0,01). Cada corrección guarda el valor original en `from`: si el original cambia, el generador da CLCT006 para obligar a revisarla. Una prueba comprueba que cada corrección es la mínima: un paso atrás hacia el original hace fallar algún par.

| Tema | Token | Antes → después | Par determinante | Contraste |
|---|---|---|---|---|
| Oscuro | `danger` | oklch(0.62 0.18 25) → **0.588** | `onDanger` sobre `danger` | 3,96 → 4,54 |
| Oscuro | `line` | oklch(1 0 0 / 0.14) → **/ 0.34** | `line` sobre `field` | 1,44 → 3,05 |
| Oscuro | `tintL` | 0.82 → **0.84** | insignia `categoryTint` sobre doble `categoryWash` (win) | 4,24 → 4,53 |
| Claro | `accent` | oklch(0.50 0.11 220) → **0.474** | `accent` sobre `accentWash over card` (y sobre `cardHi`) | 4,03 → 4,50 |
| Claro | `warn` | oklch(0.58 0.13 70) → **0.602** | `onWarn` sobre `warn` (números de voz) | 4,12 → 4,52 |
| Claro | `warnText` | oklch(0.58 0.13 70) → **0.503** | `warnText` sobre `cardHi` | 3,26 → 4,52 |
| Claro | `danger` | oklch(0.62 0.18 25) → **0.588** | `onDanger` sobre `danger` | 3,96 → 4,54 |
| Claro | `dangerText` | oklch(0.72 0.16 25) → **0.506** | `dangerText` sobre `dangerWash over card` | 1,89 → 4,51 |
| Claro | `line` | oklch(0 0 0 / 0.16) → **/ 0.43** | `line` sobre `card` | 1,45 → 3,07 |
| Claro | `tintL` | 0.5 → **0.468** | insignia `categoryTint` sobre doble `categoryWash` (voice → web) | 3,89 → 4,50 |

TEM-004 citaba los fallos del tema claro (warn, accent sobre cardHi, peligro y los bordes line). Medidos sobre el fondo compuesto real, también fallaban en el tema oscuro `line`, el texto blanco sobre `danger` y la insignia de una ficha activa; se corrigen igual. Para `onWarn` sobre `warn` se corrige `warn` (+0,022) y no `onWarn` (−0,055) porque es el cambio menor.

## 7. Alto contraste

- **Paleta propia (`hc`):** negro, blanco y #FFE600, bordes de 2 px, sin transparencias ni desenfoque. Peligro #FF6B6B con texto negro (DIS-26), éxito blanco con icono negro, velo opaco y sombra transparente (sin sombra).
- **Sin sombras:** en todo tema de alto contraste (`IsHighContrast`, también el del sistema, donde `shadow` se asigna a `window`), `ShadowSpec.ColorIn` devuelve un color totalmente transparente.
- **Colores del sistema:** `SystemHighContrastPalette` lee `SystemColors` al llamarse y `Capture()` congela una instantánea como `ThemePalette` (clave `system`). Tras `WM_SYSCOLORCHANGE` hay que tomar otra. Los pares garantizados `windowText`/`window` y `highlightText`/`highlight` los define todo tema de contraste de Windows; `highlight` sobre `window` es como Windows dibuja la selección y el foco. El `ThemeService` de M3 verificará el contraste con los colores reales del sistema.

## 8. Formas y foco

- **Radios** (`Radii`): `Compact` 6, `Control` 8, `Button` 10, `Tile` 12, `LargeCard` 14, `Window` 16, `Panel` 18, `Modal` 20.
- **Sombras** (`Shadows`): `Panel` 0 18 50 al 45 %, `Modal` 0 30 80 al 50 %, `Menu` 0 14 40 al 45 %. Se pintan precalculadas, nunca con `DropShadowEffect` (§8.1). `ShadowSpec.ColorIn(palette)` aplica la opacidad al token `shadow` del tema; en alto contraste devuelve transparente.
- **Anillo de foco** (`FocusRing`): 3 px separados 2 px del control, en `focusRing`.

## 9. Código generado

```csharp
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

var palette = ThemeCatalog.GetPalette(ThemeId.Dark);          // o ThemePalettes.Dark
Color accent = palette.Accent;                                // propiedad por token
Color same = palette.GetColor(ColorToken.Accent);             // acceso por enumeración
SolidColorBrush brush = palette.CreateBrush(ColorToken.Text); // congelado (Freeze)
Color tint = palette.GetCategoryTint(CategoryToken.Voice);
TimeSpan fade = Motion.Get(MotionToken.PanelOpacity, reduceMotion: true); // TimeSpan.Zero
```

| Tipo | Contenido |
|---|---|
| `ColorToken` | Un miembro por token; su documentación lista el valor de cada tema, su origen y sus correcciones. |
| `CategoryToken` | Las diez categorías. |
| `ThemePalette` | Colores de un tema: propiedades, `GetColor`, `GetCategoryTint`, `GetCategoryWash`, fábricas de pinceles congelados, `Key`, `Name`, `IsHighContrast`, `BorderThickness`. Inmutable. |
| `ThemePalettes` | `Dark`, `Light`, `HighContrast` y `All` (los temas en el orden de los datos). |
| `SystemHighContrastPalette` | Colores de sistema por token y `Capture()`. |
| `Radii`, `FocusRing`, `ShadowSpec`, `Shadows` | Formas. |
| `MotionToken`, `Motion` | Duraciones con y sin reducir movimiento. |

**Hilos (plano §3.2 regla 8).** Las paletas son inmutables y los pinceles salen congelados (`Freeze`): un `Freezable` congelado se puede compartir entre dispatchers. Crear pinceles es barato; el `ThemeService` de M3 los creará una vez por tema y dispatcher.

**Pegamento escrito a mano** (`src/Clicalo.UI.Wpf/Theming`): `ThemeId` (`Dark`, `Light`, `HighContrast`, `SystemHighContrast`) y `ThemeCatalog.GetPalette(ThemeId)`. «Auto» es una preferencia que el `ThemeService` resuelve (M3), no una paleta. La detección del tema de Windows llega en M3.

## 10. Diagnósticos

Todos son errores. El mensaje dice qué falla y cómo arreglarlo.

### CLCT001

**Color no válido.** El valor no es `oklch(L C H)`, `oklch(L C H / A)`, `#RRGGBB` ni `<n>px solid <color>`, o un componente está fuera de rango (L y A entre 0 y 1, C ≥ 0, H entre 0 y 360). No se admiten porcentajes, unidades ni `#RGB`/`#RRGGBBAA`. La posición apunta al carácter exacto dentro de la cadena. También cubre los tonos y los componentes derivados de las categorías.

### CLCT002

**Contraste por debajo del mínimo.** Indica par, fondo, tema, relación medida (truncada, nunca redondeada hacia arriba), categorías afectadas y el backdrop del peor caso. Se arregla corrigiendo el token con el mínimo cambio de luminosidad y documentándolo en `corrections` (§6); nunca bajando el mínimo.

### CLCT003

**Color demasiado fuera de gama.** Un color OKLCH fuera de sRGB que el mapeo de gama movería más de `maxDeltaEOK`. Se arregla bajando la croma.

### CLCT004

**Archivo mal formado.** JSON no válido (con la posición del error), un miembro que falta o de tipo equivocado, un miembro desconocido en un objeto de forma fija (una errata que se ignoraría en silencio), un nombre que no es camelCase, un mínimo de contraste por debajo de WCAG AA, una duración no entera, etc.

### CLCT005

**Token desconocido, repetido o ausente.** Un tema sin algún token, un token definido en los dos archivos, un alias a un token inexistente o en ciclo, un nombre desconocido en los pares o en el mapa de alto contraste, un token sin color de sistema, una propiedad de `SystemColors` que no existe o un token sin decisión de contraste.

### CLCT006

**Corrección desfasada.** El `from` de una corrección ya no coincide con el valor que corrige. Hay que volver a medir y actualizar o quitar la corrección.

### CLCT007

**Falta un archivo.** Uno de los cinco archivos no está entre los `AdditionalFiles` del proyecto con perfil `UiWpf`.

## 11. Cambiar o añadir un token

1. Añade el valor en `extra-tokens.json` → `colors`, en **los tres temas** (o un alias).
2. Decide su contraste: añade sus pares a `contrast-pairs.json` con su `use`, o decláralo en `decorative` con el motivo.
3. Añade su color de sistema en `hc-system-map.json` → `tokens`, de modo que sus pares caigan en pares garantizados.
4. Compila. Si hay CLCT002, corrige con el mínimo cambio de luminosidad y documenta la corrección.
5. Ejecuta las pruebas (`Clicalo.Data.Tests` y `Clicalo.Generators.Tests`).

Para cambiar un color del paquete no se toca `theme-palettes.json`: se añade una corrección con su motivo y su requisito.

## 12. Pruebas

| Proyecto | Carpeta | Qué cubre |
|---|---|---|
| `Clicalo.Data.Tests` | `Tokens/` | Conversión (vectores fijados, ruta XYZ de referencia, ida y vuelta de 8 bits), mapeo de gama, WCAG y composición, analizador CSS; y los datos leídos con System.Text.Json de forma independiente del generador: copia fiel, tokens completos, categorías, todos los pares cumplen, correcciones mínimas, alto contraste opaco, cobertura, movimiento, foco y mapa del sistema. |
| `Clicalo.Generators.Tests` | `Tokens/` | Cada diagnóstico CLCT con su posición, filtrado por perfil, salida determinista y el código generado compilado y ejecutado contra sustitutos de WPF (colores, pinceles congelados, temas, sistema, movimiento, sombras). |

La compilación de `Clicalo.UI.Wpf` compila además el código generado contra WPF real.

## 13. Decisiones respecto al plano

- **Numeración de diagnósticos.** El plano (§8.4) numeraba CLCT001 «fuera de gamut sin regla» y CLCT003 «token ausente». Aquí CLCT001 es el color no válido, CLCT003 la pérdida por mapeo de gama y el token ausente pasa a CLCT005, con CLCT004, CLCT006 y CLCT007 para los demás errores de datos.
- **Atenuado.** El plano menciona medir «con dimTo»; el catálogo (TEM-004, PQ-42) exime el estado atenuado. Se sigue el catálogo.
- **Escritorio.** «Escritorio claro y oscuro» se concreta en los 8 vértices del cubo sRGB: el negro y el blanco acotan la luminancia del fondo, y los vértices de color cubren el caso de un primer plano translúcido (§4).
