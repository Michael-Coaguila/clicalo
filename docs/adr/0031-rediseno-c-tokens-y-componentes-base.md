---
status: Aceptado
date: 2026-10-16
decision-makers: Michael Coaguila (dueño del producto y mantenedor); la forma concreta, por delegación del usuario
consulted: decisiones D12 a D15 del catálogo (§6.2); maqueta C (`docs/design/redesign-c/`); ADR-0001, ADR-0025
informed: los agentes del rediseño de M7, mediante este ADR y `docs/design/sistema.md`
---

# ADR-0031 · El rediseño C cambia los tokens y los controles base

## Contexto y planteamiento del problema

El usuario revisó la galería de superficies reales y encontró botones mal dimensionados, texto cortado o
superpuesto, cuatro formas distintas de marcar lo elegido y medidas inventadas en cada vista (unos 1.900 números
sueltos en `Clicalo.UI.Wpf`). Eligió la dirección C, «Evolución del prototipo» (D12): los colores, las fuentes y
los iconos del prototipo v4 con un sistema estricto. El tema de los tokens (`data/tokens`) solo tenía colores,
contraste, categorías y movimiento; no había escala de espacio, alturas, radios ni escala tipográfica, y los
controles base (`TouchButton` con seis apariencias, `Chip`, `CcButton`, `CcToggle`, `TextField`…) dejaban que
cada vista eligiera rellenos, radios y alturas.

¿Cómo se hace que la interfaz entera use solo las medidas y el aspecto de C, sin editar la copia fiel del
paquete de diseño (`theme-palettes.json`) ni rebajar ningún requisito?

## Factores de decisión

- D12: rediseño visual con la dirección C; siguen vinculantes las funciones, los flujos, los textos, los 44 × 44
  (REG-02), UI Automation (REG-06) y no quitar el foco (REG-01).
- TEM-002 y TEM-004: todo color es un token y todo par de contraste se comprueba al compilar.
- Cero medidas sueltas: solo la escala de espacio, las tres alturas, los tres radios y las cajas de icono.
- Un solo aspecto para «elegido» y cuatro botones.
- Una persona mantiene el producto: la regla tiene que vigilarse sola.

## Opciones consideradas

1. Medidas y colores de C como **datos** (`design-system.json` y la sección `redesign` de `extra-tokens.json`),
   generados como constantes, y controles base nuevos construidos solo con ellos; una prueba de arquitectura con
   línea base impide medidas nuevas.
2. Un `ResourceDictionary` XAML de estilos con las medidas escritas a mano.
3. Retocar los controles existentes sin tokens de medida.

## Resultado de la decisión

Opción elegida: la 1, porque mantiene la fuente de verdad en `data/` como el resto del sistema de temas, deja que
el compilador y las pruebas vigilen la regla y no toca la copia fiel del paquete.

- **Datos.** `data/tokens/design-system.json` (con `data/schemas/design-system.schema.json`): espacio 4·8·12·16·
  24·32·48, alturas 44·52·64, radios 8·12·16 y la pastilla, cajas de icono 16·20·24·32, grosores (línea 1,
  anillo de elegido 2, anillo de foco 3 separado 2), medidas propias de componente (interruptor 48 × 28, barra de
  desplazamiento, barra de título de 56) y una escala tipográfica cerrada de once estilos. Los radios y el anillo
  de foco salen de `extra-tokens.json` y pasan aquí.
- **Colores.** La sección `redesign` de `extra-tokens.json` cambia, con su valor original en `from` y la decisión
  D12, los valores del paquete que C cambia (superficies, `card` blanca en claro, `line` como límite de control,
  `border` como filete, `muted`, `warn` claro); se aplica antes que las correcciones de TEM-004, que se rehacen
  sobre ella. Nuevo token `accentInk` para el texto y los iconos de acento. Cada color, un solo significado:
  `accent` interactivo o elegido, `warn` atención, `danger` destructivo y pánico, `success` confirmación.
- **Generador.** `TokenGenerator` emite `Space`, `ControlHeight`, `Radii`, `IconBox`, `Stroke`, `ComponentSize`,
  `FocusRing`, `TextStyleToken`, `TextStyleSpec` y `TextStyles`; `ThemeResources` publica el tamaño y el
  interlineado de cada estilo (también escalados, CUA-011) y el grosor de la línea de reposo.
- **Controles base** en `Clicalo.UI.Wpf.Controls`: `TouchButton` (cuatro variantes y tres alturas), `IconButton`,
  `OptionButton` y `SegmentedControl` (el único aspecto de «elegido»), `ToggleSwitch`, `StepSlider`, `TextInput`
  con 🎤, `Overline`, `NavRow`, `ThemedScrollViewer`, `StatusBar`, `Badge`, `Counter`, `Card`, `StyledText` y la
  ficha `ShortcutTile` con huecos reservados. Se retiran `ButtonAppearance`, `Chip`, `CcButton`, `CcToggle`,
  `CcChrome`, `TextField`, `TouchPanScrollViewer` y `SurfaceScrollViewer`.
- **Ventana** (D13): `Windowing/TitleBar` con minimizar, maximizar o restaurar (con Snap: `HTMAXBUTTON` en
  `WM_NCHITTEST`) y cerrar, bordes y asa de redimensionado. No es un verbo nuevo de la IPC ni cambia el modelo de
  procesos: la ventana sigue sin activarse sola (REG-01).
- **Guardia.** `LooseMeasureTests` cuenta con Roslyn los números escritos donde va una medida en `Clicalo.UI.Wpf`;
  `architecture/loose-measures.json` guarda la línea base por archivo, que solo puede bajar.

### Consecuencias

- Buena, porque las vistas componen con controles que no pueden salirse del sistema y un número suelto nuevo
  rompe la compilación de pruebas.
- Buena, porque el contraste de C se comprueba igual que antes, sobre el fondo compuesto real.
- Mala, porque las vistas existentes conservan, hasta que se recompongan, sus medidas sueltas (línea base de
  1.825 literales en 53 archivos) y algunos aspectos intermedios: los cuatro agentes del rediseño las recorren.
- Neutral, porque `theme-palettes.json` sigue siendo la copia fiel del paquete y la diferencia queda escrita
  token a token en `redesign`.

### Confirmación

- `Clicalo.Data.Tests/Tokens` (`DesignSystemDataTests`, `TokenDataTests`): escalas exactas de C, alturas ≥ 44,
  texto ≥ 11, monoespaciada solo en teclas, decisiones D12 documentadas desde el valor del paquete y correcciones
  mínimas.
- `Clicalo.Generators.Tests/Tokens`: el código generado y los diagnósticos.
- `Clicalo.Windowing.IntegrationTests`: `BaseControlTests`, `DesignSystemAuditTests` (REG-02, REG-06 y texto sin
  cortar en los tres aspectos), `ShortcutTileLookTests`, `TitleBarTests`.
- `Clicalo.Architecture.Tests/LooseMeasureTests`.
- La galería (`CLICALO_GALLERY`) con la hoja «Sistema de diseño» en oscuro, claro y alto contraste.

## Pros y contras de las opciones

### Opción 1 · Datos generados y controles nuevos

- Buena, porque reutiliza el generador, sus diagnósticos y sus pruebas.
- Mala, porque exige recomponer las vistas.

### Opción 2 · Diccionario XAML de estilos

- Buena, porque es habitual en WPF.
- Mala, porque las medidas quedarían en código escrito a mano, sin prueba ni contraste, y el proyecto construye
  sus plantillas en código.

### Opción 3 · Retocar los controles existentes

- Buena, porque cambia menos.
- Mala, porque deja abiertas las seis apariencias y las medidas libres que causaron los defectos.

## Criterios de reapertura

- Que el usuario no apruebe la galería del antes y el después (D15).
- Que una medida de C no se pueda cumplir en S, en L o con el texto al 150 % sin cortar nada: se propone un token
  nuevo, nunca un número suelto.

## Más información

- Sistema de diseño: [`docs/design/sistema.md`](../design/sistema.md); maqueta: [`docs/design/redesign-c/`](../design/redesign-c/).
- Tokens: [`docs/guides/design-tokens.md`](../guides/design-tokens.md).
- Decisiones D12 a D15: [catálogo §6.2](../requirements/catalog.md#62-decisiones-del-usuario).
