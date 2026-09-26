# 05 · Centro de control

Ventana normal, redimensionable (por defecto 1120×680, mínimo 760×520), que recuerda su tamaño.
- **Barra de título** (52 px, fondo `side`):
  - Logotipo, «Clícalo» › «Centro de control» › nombre de la sección.
  - Selector de idioma ES/EN, siempre visible.
  - ✕ de 44×40, que se pone rojo al pasar el cursor.
- **Esc**:
  - Si el foco está en un campo, solo sale del campo.
  - Si hay un menú abierto, lo cierra.
  - Si no, cierra la ventana.
- **Menú lateral** (220 px; por debajo de 1240 px de ancho, solo iconos de 72 px, con los contadores en la esquina). Tiene 6 secciones, con un separador antes de Sistema:
  1. **Atajos** (`tune`), con un contador amarillo del número de repetidos.
  2. **Plantillas** (`auto_awesome`).
  3. **General y panel** (`display_settings`).
  4. **Precisión táctil** (`touch_app`).
  5. **Sistema** (`verified_user`), con contador 1 si hay actualización.
  6. **Acerca de y contacto** (`favorite`).
- **Barra de estado** (48 px): último mensaje, con [Deshacer] si procede.

---

## 1. Atajos
Tres columnas: perfiles (180), atajos (flexible) y editor (400). En pantallas estrechas: 150 / flexible / 340.

### Aviso de repetidos
Chip amarillo en la barra superior, «N combinaciones repetidas · Revisar». Al tocarlo, se abre la **primera** repetida en el editor.

### Columna de perfiles
- Lista: Siempre visible (📌, «todas las apps»), General y el resto de perfiles con su proceso o «manual».
- Al final, **+ Nuevo perfil**, que lleva a Plantillas.

### Columna de atajos
- **Cabecera**:
  - Icono y nombre del perfil, con ✏ para editarlo (salvo en Siempre visible).
  - Subtítulo: «Se activa solo al abrir X», «Se elige a mano…» o la descripción de Siempre visible.
  - Botón **+ Añadir**.
- **Editar perfil** (✏): tarjeta con:
  - Nombre.
  - Cuadrícula de iconos.
  - Interruptor **Modo compatible**.
  - Botones [Compartir] · [Listo] · [Eliminar perfil] (doble toque; no se muestra en General).
- **Vinculación** (salvo en General y en Siempre visible): una fila con 4 estados:
  - «Se activa solo con Word · WINWORD.EXE» · Cambiar.
  - «No se activa solo» (amarillo) · Vincular.
  - «Esperando una app…» · Cancelar.
  - General: «Se usa en cualquier app que no tenga perfil propio».
  - Al desplegarla: chips con las apps abiertas y su proceso, [Capturar la próxima app que uses] y [Ninguna (solo manual)].
- **Cuadrícula** de fichas de 88 px, igual que en el panel.
  - Cada ficha lleva un ⚠ si está repetida, el icono de su tipo, el número de voz y la etiqueta «Incompleto».
  - Se pueden **arrastrar** para reordenar.
  - La última ficha es «Biblioteca».
  - Debajo, una nota: se tocan para editar y se reordenan con botones o arrastrando.

### Columna del editor
Un único panel desplazable (`grid-template-columns:minmax(0,1fr)`, sin scroll horizontal). Puede estar en uno de estos estados:

**A. Añadir atajo** (desde + Añadir):
- Título y ✕.
- Tarjeta principal **«Crear el mío»**: fondo `accent`, [createOwnD] y →.
- Separador «o elige una acción lista».
- Categorías en chips: **Para {perfil}** (primera, si el perfil tiene plantilla) · Edición · Ventanas · Mouse · Voz · Textos · Sistema.
- Filas de acción: icono, nombre, teclas y ⊕, que pasa a ✓ si ya está añadida. Al tocar una, se añade al perfil.

**B. Editor de un atajo**, de arriba abajo:
1. **Aviso de repetida** (si aplica). Plegado por defecto en una línea: «⚠ Combinación repetida en N sitios · i de T», con ‹ › y ▾.
   - Desplegado muestra cada aparición: icono, **nombre** · perfil y EDITANDO si es la actual. Tocar la fila la abre; el 🗑 pasa a «Confirmar» y la **elimina de ese perfil**.
   - Debajo, un consejo según el caso: [dupAdvG], [dupAdvSame] o [dupAdvDiff].
   - [Dejar solo en «Siempre visible»], si está en Siempre visible o si todas se llaman igual.
   - [Está bien así (no avisar más)], que la añade a `dupIgnored`.
2. **Identidad**:
   - Ficha de vista previa de 84 px, con icono, nombre y un lápiz en la esquina. Al tocarla se abre o cierra el selector de iconos.
   - Campo **Nombre**, con placeholder «Ej.: Guardar como PDF» y el foco automático si está vacío. Al lado, 🎤 Dictar.
   - Línea: «✨ Icono elegido según el nombre · toca el icono para cambiarlo» o «Icono elegido por ti…».
   - El icono se recalcula al escribir mientras `autoIcon` esté activo. Elegir uno a mano lo desactiva.
3. **Selector de iconos** (plegable):
   - Sugeridos según el nombre y las teclas, sin etiqueta.
   - Buscador con 🔍 («guardar», «voz», «pdf»…).
   - Cuadrícula de 40 px, con scroll de 180 px.
   - Fuente: `ICONLIB`, cada icono con sus etiquetas ES/EN, y `KEYICON`.
4. **Qué hace**: 8 tipos en una cuadrícula de 4×2 (Pulsar, Mantener, Alternar, Texto, Mouse, Macro, Web, App), cada uno con su descripción.
   - Al cambiar a un tipo que no es Mouse, se **borra** la acción de mouse.
5. **Combinación** (Pulsar, Mantener, Alternar, o un paso de teclas de una macro):
   - **Recuadro**:
     - Franja amarilla «Reemplazando X…» con [Mantener la anterior], si se llegó desde un repetido.
     - Zona de fichas en **orden de pulsación**, con «+» entre ellas. Cada ficha lleva × para quitarla.
     - Vacío muestra [comboEmpty].
     - **Pie fijo** («N teclas · se guarda solo») con ⌫ (solo icono, 44 px) y ↺ **Limpiar**, que tiene deshacer.
   - **Aviso** de combinación bloqueada (rojo) o especial (amarilla).
   - **Aviso** «Editando las teclas del paso N» con [Listo], si se edita una macro.
   - **Modificadores**: Ctrl · Alt · Shift · Win (44 px). Tocar alterna añadir o quitar, al final del orden.
   - **Grupos**: Izq. / Der. · Letras · Números · F1–F12 · Especiales · Teclado num. · Multimedia.
   - **Cuadrícula de teclas**: 7 columnas en letras y números, 6 en F1–F12 y ≥ 92 px con el **nombre completo** en el resto.
   - Nota del orden: [orderHint2].
   - [Grabar con teclado] solo si el usuario no marcó «No puedo usar el teclado». Se captura con un hook de teclado de bajo nivel.
6. **Campos según el tipo**:
   - **Texto**: área de texto y 🎤.
   - **Mouse**: 8 acciones en 2 columnas, y la velocidad en las de desplazamiento.
   - **Macro**: lista de pasos.
     - Cada paso: número, icono, descripción y ✏, ↑ ↓ y ✕.
     - Al editarlo: las teclas usan el recuadro de combinación; la espera usa − / + de 100 ms; el texto, un área de texto; el mouse, chips.
     - Botones: + Teclas · + Esperar · + Texto · + Mouse.
   - **Web**: campo de dirección, validado (borde amarillo y aviso si no es válida), y chips con las páginas abiertas en el navegador.
   - **App**: campo de programa y chips «Elegir programa» (apps instaladas y abiertas).
7. **Fijar en «Siempre visible»**: fila-interruptor con [pinAllOff2] o [pinAllOn2]. Mueve el atajo.
8. **Más opciones** (plegable, con «Posición i/N» a la derecha):
   - Posición: Al inicio · Antes · Después · Al final. Los que no aplican aparecen deshabilitados.
   - Soltar sola tras (30 s / 1 min / 2 min / Nunca), solo en Mantener y Alternar.
   - Cómo se escribe (Escribir / Pegar) y el aviso «Se guarda cifrado», solo en Texto.
   - Número de voz, con la frase que hay que decir.
   - «Cómo usar los números por voz» (plegable): 3 pasos [vh1–vh3].
9. **Probar** (tarjeta que se abre sobre los botones del pie):
   - «Qué hará este atajo»: las teclas como teclas de teclado, con separadores + o →.
   - [Ver] reproduce la animación **según el tipo**: Pulsar (juntas y se sueltan), Mantener (tocas, siguen pulsadas mientras mantienes, levantas y se sueltan), Alternar (1.er toque, quedan pulsadas, 2.º toque, se sueltan), Macro («Paso i de n»), Texto, Web, App o Mouse. Con reducir movimiento, salta al final.
   - Una línea de fase con icono y una frase explicativa por tipo.
   - «Probar en»: chips de las apps abiertas, con la activa por defecto.
   - [Probar ahora en {app}]: oculta el centro de control, activa la app, envía la acción y vuelve a los ~2,5 s mostrando «¿Hizo lo esperado en {app}?», con [Sí, funcionó] y [No].
   - Si la respuesta es No, se muestran las 4 causas posibles [tip1–tip4].
10. **Pie**: [Probar] (principal) · [Duplicar] · [Eliminar] (el 1.er toque pasa a «Confirmar» en rojo, 3,5 s).

**C. Vacío**: [pickOne].

---

## 2. Plantillas («Empieza con atajos listos»)
Dos columnas: contenido (flexible) y vista previa (340, o 260 en pantallas estrechas).

1. **Crear con IA** (tarjeta destacada con borde `accent`):
   - Título [aiHero] y [aiHeroD].
   - Campo de 52 px, 🎤 y [Generar con IA].
   - Chips: Photoshop, Spotify, Teams, Canva, WhatsApp, OBS.
   - Línea de privacidad 🛡 [aiPrivacy].
   - Cuota «IA gratis: n de 5 hoy», con [Usar mi clave] y un campo para la clave cuando se pide.
   - **Consentimiento** la primera vez: [consentT/D], con [Aceptar y generar] y [No usar IA].
   - **Errores**: sin conexión, límite diario o IA desactivada. Cada uno ofrece 3 salidas: Reintentar / Usar mi clave / Activar IA · Perfil vacío · Ver plantillas.
   - **Teclado**: «Para Español (Latinoamérica) · Office en español», con [kbWhy] y Cambiar.
     - Cambiar despliega la distribución (4 opciones) y el idioma de los programas (2), con la etiqueta DETECTADO en la opción detectada.
2. **Perfil vacío** (plegable):
   - Icono elegible (según el nombre o manual) y [Nombre del perfil].
   - «Se activa con»: apps abiertas · Capturar al usarla · Ninguna.
   - [Crear y añadir atajos] lleva a Atajos con la biblioteca abierta.
3. **Apps abiertas sin perfil**:
   - Cabecera con un punto amarillo e interruptor **Detectar** (= `autoSuggestProfiles`).
   - Por cada app: tarjeta amarilla «Tienes Excel abierto · 9 atajos listos», con iconos y [Vista previa] · [Instalar].
   - Si no hay: [noSugOn] o [noSugOff].
4. **Plantillas disponibles**: solo las no instaladas. Cada tarjeta muestra icono, nombre, número de atajos, 6 iconos y [Instalar]. Si no queda ninguna: [allInstalled].
5. **Tus perfiles**: botones con icono, nombre y número de atajos, que llevan a Atajos.
   - [Importar perfil], con la nota de cómo compartir uno.

**Vista previa** (columna derecha):
- Icono, nombre y proceso.
- Si la IA no conoce el programa: aviso amarillo [unknownMsg] con [Mejor, crear vacío].
- Si ya está instalado: [instNoteAll] o [instNoteSome].
- Línea del teclado.
- Si falta la variante del idioma: [onlyEs].
- Lista de atajos, cada uno con:
  - casilla (desactivada y «ya está» si ya existe);
  - icono;
  - nombre y teclas **en dos líneas**, con ajuste de texto;
  - ✏ para renombrarlo antes de instalar.
- Botón final:
  - «Instalar N atajos».
  - «Crear perfil con N» (IA).
  - «Añadir N que faltan» (instalado incompleto).
  - «Editar atajos» (completo).

---

## 3. General y panel
Arriba, una cuadrícula de 2×2 con tarjetas de la misma altura:
- **Idioma · Language**: 2 botones grandes que ocupan toda la tarjeta.
- **Tema**: Como Windows · Oscuro · Claro · Alto contraste, con muestra de color.
- **Vista**: 3 miniaturas y su descripción.
- **Tamaño**: S, M o L en miniatura, y el tamaño de texto con − / +.

Debajo, en 2 columnas:

**Izquierda**:
- **Disposición**:
  - Filas visibles (Auto, 1, 2 o 3, en miniatura).
  - Cambiar con la app activa (el mismo ajuste que Auto/Fijo).
  - Fila «Siempre visible».
  - Selector de perfil.
  - Columnas (2, 3 o 4, en miniatura).
  - Mostrar teclas (con o sin teclas, en miniatura).
- **Transparencia**:
  - Opacidad con vista previa sobre un «documento».
  - Atenuar cuando no lo uso.
  - Opacidad al atenuar.

**Derecha**:
- **Modo pestaña**:
  - Explicación.
  - Lado, 4 tarjetas con miniatura.
  - Posición de la pestaña (flechas y arrastre).
  - Botones por página (4, 5, 6 u 8).
  - Ocultar tras usar un botón.
  - Respetar la barra de desplazamiento.
- **Confirmación al tocar**: sonido suave y destello.
- **Seguridad de teclas**:
  - Soltar tras 30 s, 1 min, 2 min o Nunca.
  - Soltar al cambiar de app.
  - Reducir movimiento.
  - Reiniciar Frecuentes (doble toque).
- **Primeros pasos**: Ver la bienvenida otra vez.

---

## 4. Precisión táctil
- **Izquierda**:
  - 4 presets (Estándar / Temblor leve / Temblor fuerte / Personal) con su descripción.
  - 4 deslizadores con valor y explicación: antirrebote de 0 a 1000 ms · área extra de 0 a 40 px · cancelar si deslizas de 0 a 80 px · contacto mínimo de 0 a 300 ms.
- **Derecha**:
  - Zona de prueba grande, «Toca aquí».
  - Contadores de toques registrados e ignorados.
  - Mensaje del último toque.
  - Usa el **mismo** TouchFilter que el panel.

---

## 5. Sistema
Tres pestañas unidas a su contenido en una misma tarjeta. La pestaña activa tiene el mismo fondo que el contenido, una línea `accent` de 3 px debajo y el icono relleno; las demás, fondo `side`, borde inferior y ▾. Cada pestaña muestra su estado:
- **Actualizaciones** (v2.0.0 · al día / Nueva versión, en amarillo):
  - Tarjeta de estado (al día → buscando → nueva versión → instalando con barra → actualizado).
  - Actualizar automáticamente.
  - Avisar antes de instalar.
  - Copia antes de actualizar.
  - Canal Estable / Beta.
  - **Volver a la versión anterior** (7 días, doble toque).
  - Novedades por versión.
- **Copias de seguridad** («Última: hoy 09:12»):
  - Tarjeta de migración.
  - Carpeta y formato.
  - [Crear copia ahora] · [Exportar] · [Importar]. Importar pregunta Combinar o Reemplazar.
  - Copia automática.
  - Historial, donde Restaurar pide doble toque.
- **Inicio y estabilidad**:
  - Iniciar con Windows.
  - Iniciar como administrador.
  - Una sola ventana.
  - Recuperación automática.

---

## 6. Acerca de y contacto
- Título y subtítulo.
- **Tarjeta de historia**: [story1], [story2], firma «MC · Michael Coaguila · Creador».
- **Tarjeta de la app**: logotipo, «Clícalo», «v2.1.0 · MIT · código abierto», GitHub, LinkedIn y [Compartir con alguien].
  - ⚠ Sustituir las URL por las reales.
- **Opinión**:
  - 4 tipos (Sugerencia, Algo falla, Nueva función, Agradecimiento).
  - Mensaje con 🎤.
  - Adjuntar registro técnico e incluir versión y sistema.
  - [Ver qué se envía en el registro]: vista previa con los textos y títulos de ventana **ocultos**.
  - [Enviar por correo] (mailto con asunto «[Clícalo] tipo» y cuerpo).
- **Escríbeme directamente**: correo copiable y la promesa. También Reportar en GitHub (issues) y Colaborar con el código.
