# Capturas de referencia del Prototipo v4

Capturas PNG del [Prototipo v4](<../handoff/prototype/Prototipo v4.dc.html>) para comparar a ojo el aspecto
de las superficies de WPF durante M3 (colores de tema, tipografía, tamaños de los objetivos táctiles, radios,
sombras y orden de los elementos).

**Son referencia visual, no instantáneas exactas.** El prototipo es HTML renderizado por Edge: el
suavizado de las fuentes, los colores `oklch`, el desenfoque de fondo y la posición exacta de cada píxel no
coinciden con WPF, y la fidelidad vinculante en lo visual es **media** ([qué es vinculante](../handoff/LEEME-VINCULANTE.md)).
Ninguna prueba las compara píxel a píxel. Si una captura y el prototipo discrepan, manda el prototipo; si el
prototipo y el [catálogo](../../requirements/catalog.md) discrepan, manda el catálogo.

## Cómo se obtuvieron

- **Navegador:** Microsoft Edge 154.0.4258.53 instalado, sin interfaz, con un perfil temporal, a través de
  `playwright-core` 1.63.0 (`channel: 'msedge'`). Las herramientas vivieron solo en la carpeta temporal; el
  script no está en el repositorio porque exige Node.
- **Ventana:** 1371 × 914 px lógicos, la pantalla del usuario (2400 × 1600 al 175 %), idioma `es-ES`. Cada
  estado se captura dos veces: en [`1x/`](1x) con escala 1 y en [`1.75x/`](1.75x) con escala 1,75 (los
  mismos px lógicos con 1,75 px físicos cada uno).
- **Fuentes:** las del propio prototipo (Atkinson Hyperlegible 400 y 700, JetBrains Mono 500 y Material
  Symbols Rounded), servidas por Google Fonts. El script fuerza su carga con `document.fonts.load`, espera a
  `document.fonts.ready` y aborta si alguna no está disponible. React y Babel se cargan de unpkg, como indica
  `support.js`.
- **Estabilidad:** se desactivan las transiciones y animaciones con una hoja de estilo inyectada; antes de
  cada captura se cierra lo abierto (`toast`, búsqueda, Ajustes rápidos, menús, centro de control,
  bienvenida), se vuelve a la vista completa, tamaño M, tema oscuro, perfil Word y app activa Word, y la
  superficie se mantiene despierta (`awake`) para que la atenuación automática no la aclare.
- **Estado:** se cambia por la interfaz del prototipo cuando hay un botón directo (barra inferior, cabecera
  del panel, pestaña, toque largo). Para el tema, el tamaño y la vista se escribe en el estado documentado
  de la lógica (`class Component extends DCLogic`, campos `theme`, `size`, `density`, `dockSide`…), que
  `support.js` guarda como `.logic` del componente React anfitrión; el script lo localiza recorriendo la
  fibra de React desde el botón de la bandeja «Clícalo».
- **Recorte:** cada PNG se recorta al rectángulo de la superficie (el panel, la pestaña, la barra de la
  pestaña, la burbuja, la ventana del centro de control o el diálogo de bienvenida), sin la sombra exterior.
  En las esquinas redondeadas asoma el escritorio simulado del prototipo, que no es parte del diseño.
- **Tamaño:** PNG sin pérdida tal como los codifica Edge (volver a comprimirlos con Pillow apenas ahorraba
  un 0,2 %). Unos 0,8 MB en `1x/` y 1,6 MB en `1.75x/`. No se cuantizan colores para no falsear el tema.

## Lista

Los mismos nombres de archivo en [`1x/`](1x) y [`1.75x/`](1.75x). Medidas en px lógicos.

| Archivo | Estado | Cómo se obtuvo |
|---|---|---|
| `panel-full-S-dark.png` | Panel, vista completa, tamaño S, tema oscuro (288 × 436) | Estado `theme: 'dark'`, `size: 'S'` |
| `panel-full-M-dark.png` | Panel, vista completa, tamaño M, tema oscuro (316 × 520) | Estado `theme: 'dark'`, `size: 'M'` |
| `panel-full-L-dark.png` | Panel, vista completa, tamaño L, tema oscuro (392 × 602) | Estado `theme: 'dark'`, `size: 'L'` |
| `panel-full-S-light.png` | Panel, vista completa, tamaño S, tema claro | Estado `theme: 'light'`, `size: 'S'` |
| `panel-full-M-light.png` | Panel, vista completa, tamaño M, tema claro | Estado `theme: 'light'`, `size: 'M'` |
| `panel-full-L-light.png` | Panel, vista completa, tamaño L, tema claro | Estado `theme: 'light'`, `size: 'L'` |
| `panel-full-S-hc.png` | Panel, vista completa, tamaño S, alto contraste | Estado `theme: 'hc'`, `size: 'S'` |
| `panel-full-M-hc.png` | Panel, vista completa, tamaño M, alto contraste | Estado `theme: 'hc'`, `size: 'M'` |
| `panel-full-L-hc.png` | Panel, vista completa, tamaño L, alto contraste | Estado `theme: 'hc'`, `size: 'L'` |
| `panel-compact-M-dark.png` | Vista compacta, tamaño M | Estado `density: 'compact'` |
| `tab-right-closed-dark.png` | Pestaña cerrada en el borde derecho (32 × 116) | Estado `density: 'dock'`, `dockSide: 'right'` |
| `tab-right-open-dark.png` | Pestaña abierta en el borde derecho: barra vertical (88 × 724) | Como la anterior y toque en la pestaña; con `coachDone: true` para que la guía de tres pasos no tape la barra |
| `bubble-dark.png` | Burbuja (panel minimizado, 64 × 64) | Toque en «Minimizar» de la cabecera del panel |
| `quick-settings-open-M-dark.png` | Ajustes rápidos abiertos | Toque en «Ajustes rápidos» (`tune`) de la cabecera |
| `search-open-M-dark.png` | Búsqueda abierta, sin texto | Toque en «Buscar» de la cabecera |
| `panic-strip-M-dark.png` | Franja de pánico «Pulsado: Win + H · Soltar todo» | Estado `latched: { dict: true }` (Dictar fijado) |
| `long-press-menu-M-dark.png` | Menú de toque largo sobre «Dictar» | Pulsación de 800 ms con el ratón sobre el primer botón de la cuadrícula |
| `empty-profile-M-dark.png` | Estado vacío de un perfil sin atajos | Estado: perfil VS Code con `buttons: []` y `tab: 'code'` |
| `control-center-shortcuts-dark.png` | Centro de control, sección Atajos con el editor de «Negrita» (1121 × 680) | Toque en «Centro de control» de la barra inferior |
| `welcome-step0-dark.png` | Bienvenida, primer paso («paso 0» de [docs/06](../handoff/docs/06-bienvenida.md): historia e idioma) | Toque en «Primer arranque» (`waving_hand`) de la barra inferior |
| `welcome-step1-dark.png` | Bienvenida, «paso 1» de docs/06: «¿Cómo usas tu equipo?» | Como la anterior y toque en «Siguiente» |

Salvo las nueve del panel, todas son del tema oscuro y del tamaño M, los valores por defecto del prototipo.
El texto de la barra de estado del panel («Listo. Toca un botón para usarlo.») y el contenido (perfil Word,
semillas de ejemplo) son los del prototipo.

## Regenerarlas

1. En una carpeta temporal fuera del repositorio: `npm install playwright-core@1`.
2. Un script de Node que, para cada escala, cree un contexto con `viewport: { width: 1371, height: 914 }`,
   `deviceScaleFactor` 1 o 1,75 y `locale: 'es-ES'`, abra el prototipo con `chromium.launch({ channel: 'msedge' })`,
   espere al botón `aria-label="Clícalo"`, inyecte la hoja que quita transiciones, espere a las fuentes y,
   para cada fila de la tabla, prepare el estado y haga `page.screenshot({ clip })` con el
   `getBoundingClientRect()` de la superficie redondeado hacia fuera.
3. Copiar las carpetas `1x/` y `1.75x/` aquí y actualizar la versión de Edge de este archivo.
