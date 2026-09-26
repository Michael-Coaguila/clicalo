# 04 · Panel flotante

Tres **vistas** (Ajustes rápidos → Vista): **Completa**, **Compacta** y **Pestaña**. Hay además tres **tamaños**, S, M y L, que aplican a las tres vistas, y la **burbuja** minimizada. Textos exactos en `data/strings.*.json` (entre corchetes van las claves).

## Medidas por tamaño (`SIZES`)
| | Ancho del botón | Alto del botón | Icono | Etiqueta | Teclas | Botones de cabecera | Alto de la fila fija | Separación |
|---|---|---|---|---|---|---|---|---|
| S | 72 | 66 | 20 | 12 | 9 | 36 | 44 | 6 |
| M | 92 | 78 | 28 | 14 | 10 | 40 | 60 | 8 |
| L | 116 | 98 | 34 | 16 | 11 | 46 | 72 | 10 |

- Ancho del panel: `max(288, cols·w + (cols−1)·gap + 24)`.
- Vista compacta: botones 16 px más bajos.
- La escala de texto (100–150 %) multiplica etiqueta, teclas y etiqueta de la fila fija, y el alto del botón crece en consecuencia.

## Estructura de la vista completa (de arriba abajo)
1. **Cabecera** (fila, gap 2, padding 8/6/6/6):
   - Asa ⋮⋮ de 26 px, que arrastra el panel.
   - Icono y nombre del perfil (también arrastran).
   - Botón **Auto/Fijo** (36×36, borde y icono azul `autorenew` o rojo `lock`).
   - 🔍 Buscar, ✏ Editar, ⚙ Ajustes rápidos, − Minimizar.
2. **Franja de pánico** (solo si hay teclas pulsadas): fondo rojo `oklch(0.62 0.18 25)`, texto blanco «Pulsado: {teclas}», botón blanco **Soltar todo**. No se atenúa nunca.
3. **Aviso de administrador** (si la app activa está elevada y Clícalo no): tarjeta amarilla con texto [adminMsg] y botón [adminBtn].
4. **Búsqueda** (si está abierta): campo de 44 px [search]. Busca en todos los perfiles por nombre ES/EN y por combinación. Los resultados muestran el perfil de origen en lugar de las teclas.
5. **Ajustes rápidos** (si están abiertos), en este orden:
   - Tarjeta destacada «Centro de control» [openCC + ccSub].
   - **Vista**: Completa, Compacta o Pestaña, con icono.
   - **Opacidad**: − · deslizador del 30 al 100 % · +.
   - **Tamaño**: S, M o L.
   - **Lado de la pestaña**: solo en la vista Pestaña; izquierda, arriba, abajo o derecha.
   - **Tema**: Auto, Oscuro, Claro o Alto contraste, en cuadrícula de 2×2.
   - Tres filas-interruptor, cada una tocable completa: Modo prueba (30 s) · Atenuar cuando no lo uso · Teclas fijas · Números para voz.
6. **Sugerencia** (app sin perfil con plantilla disponible): «**Excel** no tiene perfil. ¿Creo uno con atajos listos?», con los botones [Crear perfil] y [Ahora no]. «Ahora no» deja de sugerir esa app en esta sesión.
7. **Fila Siempre visible** (si `showStripRow`):
   - Etiqueta «📌 SIEMPRE VISIBLE», oculta en la vista compacta.
   - 4 columnas.
   - Límite de 4 atajos en S o compacta y 8 en M o L. Si hay más, se muestran límite − 1 y una ficha «··· 1/2» que pasa a la página siguiente.
   - En S, solo iconos.
8. **Teclas fijas** (si están activas): 4 botones Ctrl · Alt · Shift · Win; con 🔒 cuando están bloqueadas.
9. **Selector de perfil** (si `showTabsRow` y en vista completa), en 2 columnas **del mismo ancho**:
   - **★ Frecuentes**.
   - **Botón de perfil**: icono, nombre, punto si es la app activa y ▾, o ↶ si se está en Frecuentes.
   - Al tocarlo se abre debajo la **cuadrícula de perfiles**: fichas de 76 px con icono y nombre, un punto azul en la app activa, la ficha «Crear para {app}» si hay sugerencia y la ficha **+ Más** (lleva a Plantillas). Debajo, la leyenda del punto.
10. **Cuadrícula de atajos**:
    - `cols` columnas; filas = min(preferencia, las que caben), con un máximo de 3 (2 en S).
    - Si no caben todas: **páginas**, sin scroll. Se cambia deslizando más de 60 px o con ◀ ▶ y puntos (el activo mide 24 px de ancho y el resto 10).
    - Si falta espacio vertical, la zona de la cuadrícula es la única que se encoge. Si una fila entera no cabe mientras se ve el pánico o el aviso de administrador, se ocultan la fila fija y el selector hasta que desaparezcan. Esto se decide **midiendo**, no estimando.
11. **Barra de avisos** (40 px, `aria-live=polite`): icono y mensaje, con [Deshacer] si procede y ↻ Repetir si hay una última acción. En reposo muestra [ready].

## Anatomía de un botón de atajo
- Icono en el color de su categoría (`CAT` hue, luminosidad según el tema), nombre y teclas (si `showKeys`; en S, en formato abreviado).
- Insignia arriba a la derecha:
  - MANTENER / ALTERNAR / ACTIVO;
  - número de pasos (macro);
  - WEB / APP / TXT;
  - 📌 si está fijado en Frecuentes.
- Número de voz arriba a la izquierda, en amarillo.
- Estados:
  - Normal: fondo `card`, borde de 1 px.
  - Presionado (Mantener): escala 0.95 y fondo de su categoría.
  - Activo (Alternar): borde de 2 px en su color.
  - Armado (confirmar): borde de 2 px en `warn`.
  - Destello de 240 ms tras ejecutar.
- Modo edición (✏): toque = abrir en el editor; la **×** roja arriba a la derecha (32 px) pasa a «Confirmar» con el 1.er toque (3,5 s) y borra con el 2.º, con deshacer. Al final aparece la ficha «+ Añadir».
- Toque largo de 600 ms, salvo los de tipo Mantener: abre el menú contextual en el propio panel, con Fijar/Dejar de fijar, Quitar de Frecuentes, Editar y Cancelar (filas de 44 px).
- **Perfil vacío**: tarjeta de borde discontinuo con icono `inbox`, [emptyProfT], [emptyProfS] y el botón **Añadir atajo**, que abre el editor con un atajo nuevo.

## Vista compacta
Igual que la completa, pero **sin**: pestañas, teclas bajo el nombre, etiqueta «Siempre visible» ni barra de avisos (aparece solo cuando hay un mensaje o una última acción que repetir). Los botones son más bajos.
- La fila inferior contiene: **★** (44 px) · ◀ · puntos de página · ▶ · **botón de perfil**. Este botón vuelve en un toque desde Frecuentes y, si ya estás en un perfil, abre la cuadrícula de perfiles encima de la fila.

## Vista pestaña (barra de borde)
- **Cerrada**: asa de 32×116 (vertical) o 128×32 (horizontal) en el borde elegido.
  - Contiene un chevron y el icono del perfil actual, con un punto amarillo si hay teclas pulsadas.
  - Se **arrastra** a lo largo del borde, con posición **guardada por lado** (8–92 %). El umbral de arrastre es `cancelMovePx`.
  - Si `gutter` está activo, deja 18 px libres en el borde derecho para la barra de desplazamiento de la app.
- **Abierta**: barra de 76/88/108 px de ancho en S/M/L (verticales) o de 58/66/78 px de alto (horizontales). Zonas, separadas por divisores:
  1. **Controles**: cerrar (chevron) y expandir a vista completa.
  2. **Qué ver**:
     - Grupo segmentado ★ Frecuentes / perfil. El perfil vuelve en un toque desde Frecuentes y, si no, abre la cuadrícula de perfiles **al costado**.
     - Debajo, la pastilla **Auto/Fijo** en azul o rojo.
  3. **Atajos**: los que caben **enteros** (se mide el espacio real), con paginador «▲ 1/3 ▼» (vertical) o ◀ ▶ (horizontal). Por página: 4, 5, 6 u 8, según General.
  4. **Herramientas**:
     - 🔍 Buscar (abre la vista completa con la búsqueda) y ↻ Repetir.
     - 📌 **Fijos**: abre los atajos de Siempre visible en una ventana **al costado del propio botón, alineada por abajo**. Se cierra tras usar uno, salvo con Mantener o Alternar.
     - 🔓 Auto / 🔒 Abierta: si la barra se pliega sola tras usar un botón.
     - ⏶ Subir / ⏷ Bajar: desplazamiento de tipo Mantener.
- **Pánico**: botón flotante rojo «Soltar todo» junto a la barra.
- **Guía de primera vez** (3 pasos, tarjeta al costado): 1 «Arriba: cerrar y expandir», 2 «Qué ver», 3 «Abajo: herramientas». Botones [No volver a mostrar] y [Siguiente/Entendido].

## Burbuja minimizada
Círculo de 64 px, fondo `panel`, icono `keyboard` de 30 px en `accent`.
- Arrastrable; un toque restaura el panel.
- **Respeta la opacidad y el atenuado**, con un mínimo del 55 %.
- Con pánico: opacidad del 100 % y anillo rojo de 3 px.

## Opacidad y atenuado
- Panel, barra y burbuja usan `opacity`. Con `autoDim` activo, pasan a `dimTo` 2,5 s después de que el dedo o el puntero salgan, y recuperan la opacidad al tocarlos o pasar el cursor.
- **Nunca se atenúan** con: pánico, ajustes rápidos, menú contextual, cuadrícula de perfiles, búsqueda, modo edición, ventanas al costado de la pestaña, centro de control o bienvenida abiertos.
- Transición de 350 ms (0 con `reduceMotion`).

## Posición
- Posición del panel **guardada por monitor**.
- En cada cambio de tamaño, vista, resolución o monitor, se ajusta al área de trabajo visible (sin la barra de tareas). Si el monitor ya no existe, pasa al principal.
