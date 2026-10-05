# Clícalo: catálogo de requisitos funcionales

**Versión 1.1 · 2026-10-03** (1.0 del 2026-09-25, más las decisiones del usuario de §6.2). Esta es la fuente de verdad funcional para reconstruir Clícalo, sucesor de Macro Quick Access, desde cero.

**Base del catálogo.** Integra lo que informaron seis analistas sobre el paquete `design_handoff_clicalo` (README, docs 01–10, Prototipo v4, Auditoría y `data/*.json`) y sobre la app antigua. Algunos datos se comprobaron directamente:
- las 8 reglas del README;
- el esquema de `docs/02`;
- los catálogos de teclas;
- los tres `profiles.json` v1 reales.

---

## 0. Cómo leer este documento

### 0.1 Jerarquía de fuentes, de mayor a menor autoridad

1. **Instrucción del usuario.** Las recomendaciones de arquitectura, tecnología, APIs Win32 concretas y plan de fases del paquete **no son vinculantes**. Este catálogo no prescribe tecnología. Cuando menciona una API, lo hace solo como ejemplo de una capacidad que hace falta.
2. **Las 8 reglas del README y su glosario.** Por ejemplo, «General se usa cuando la app no tiene perfil».
3. **Auditoría.** Contiene **62** hallazgos, aunque el README dice 60. Todos están aceptados salvo la elección de tecnología.
4. **Prototipo v4.** Manda en comportamiento, flujos, estados, textos, medidas y orden de los elementos. **Excepción:** no son requisito los defectos del prototipo, es decir, simulaciones, código muerto o fallos que violen una regla del README o un hallazgo aceptado. Cada caso se razona en §5.
5. **docs 01–10.** Si dos docs se contradicen, prevalece el de número menor. Rellenan lo que el prototipo no define.
6. **Decisiones de este catálogo.** Llevan la marca «Decisión», se usan donde ninguna fuente resuelve y se pueden revisar en §6.

### 0.2 Prioridades

- **MUST:** imprescindible para publicar.
- **SHOULD:** esperado en la primera versión pública; solo se aplaza con justificación escrita.
- **COULD:** deseable, se puede posponer.

### 0.3 Convenciones

- **Formato de cada requisito:** **ID** · prioridad · título. Después, la descripción verificable, **Acepta:** con el criterio de aceptación y ‹fuente›.
- **Referencias:**
  - `P4:n`: línea n del Prototipo v4.
  - `dNN`: docs/NN.
  - `AUD-nn`: hallazgo de la Auditoría numerado por orden de aparición.
  - `[clave]`: clave de `strings.*.json`.
  - `v1`: código o datos de Macro Quick Access.
- **Medidas:** todas en px **lógicos**. «Visual X / táctil 44» significa que el dibujo mide X y el área de toque mide al menos 44×44.
- **Temporizadores:** todos los tiempos y umbrales son constantes con nombre, se pueden ajustar y se prueban con reloj simulado (NFR-020).

### 0.4 Glosario

| Término | Significado |
|---|---|
| **Panel** | Ventana flotante de uso diario. Formas: Completa, Compacta, Pestaña (asa o barra), Burbuja u Oculto. |
| **Centro de control (CC)** | Ventana grande de configuración, activable. |
| **Perfil** | Conjunto de atajos, vinculado a un proceso o manual. Hay dos especiales: **General** (el de las apps sin perfil) y **Siempre visible** (una fila fija, nunca una pestaña). |
| **Frecuentes** | Vista automática con los atajos más usados. Nunca cambia sola. |
| **Auto / Fijo** | Indica si el perfil mostrado sigue a la app activa (Auto, azul) o se queda (Fijo, rojo). |
| **Atajo / botón** | Una acción. Tipos: Pulsar, Mantener, Alternar, Texto, Mouse, Macro, Web, App, más la acción de sistema añadida en EJE-016. |
| **Registro de pulsadas** | Todo lo que Clícalo mantiene pulsado: un Mantener, un Alternar activo, una tecla fija en estado 1 o 2, o el botón del mouse al arrastrar. |
| **Clave canónica** | Identidad normalizada de una combinación (REP-001). |
| **retProf** | Perfil al que se vuelve desde Frecuentes (PER-004). |

---

## 1. Reglas que no se pueden romper (requisitos transversales)

Cada regla tiene prioridad MUST y se verifica en cada versión.

- **REG-01 · MUST · El panel nunca quita el foco.** Tocar, arrastrar o hacer toque largo en el panel, la barra, el asa, la burbuja, los menús o las ventanas al costado nunca cambia la ventana en primer plano ni el foco de teclado. Las ventanas del panel están siempre encima, no se pueden activar y no aparecen en Alt+Tab ni en la barra de tareas. Hay una única excepción controlada, el campo de búsqueda (BUS-002). **Acepta:** con el Bloc de notas activo, 20 toques seguidos entre páginas, perfiles, atajos y menús escriben siempre en el Bloc de notas. Una herramienta de inspección confirma que la ventana en primer plano no cambia. ‹README R1; d09:6; AUD-48›
- **REG-02 · MUST · Objetivo táctil de al menos 44×44.** Todo control interactivo responde en al menos 44×44 aunque se dibuje más pequeño. En las filas con interruptor, la fila entera es tocable. Si dos áreas ampliadas se solapan, gana el control cuyo centro está más cerca del punto de contacto. **Acepta:** una auditoría automática sobre todas las superficies no encuentra ningún objetivo menor de 44. La lista de elementos que hoy incumplen está en ACC-002. ‹README R2; d07:35,38; AUD-14›
- **REG-03 · MUST · Siempre hay forma de soltar.** Existe el botón «Soltar todo». Además se suelta todo automáticamente:
  - al cambiar de app (si está activado);
  - al bloquear la sesión, suspender, cerrar o fallar;
  - al arrancar;
  - al vencer el tiempo máximo.
  Ninguna forma del panel (Completa, Compacta, Pestaña abierta o cerrada, Burbuja, Oculto) deja algo pulsado sin un control visible para soltarlo. **Acepta:** la batería de SEG pasa y, tras Win+L, suspender o matar el proceso, no queda ninguna tecla pulsada. ‹README R3; AUD-01›
- **REG-04 · MUST · Nada destructivo sin dos toques y sin deshacer.** Es **destructivo** lo que elimina una entidad del usuario o un conjunto de datos: un atajo, un perfil, un paso de macro, una aparición repetida, reiniciar Frecuentes, reemplazar al importar, restaurar una copia o volver a la versión anterior. Pide dos toques (etiqueta [delConfirm] durante 3,5 s) y ofrece Deshacer. Las **ediciones de valor** (quitar una tecla, limpiar la combinación, quitar de Frecuentes, cambiar un ajuste) no piden dos toques, pero sí se pueden deshacer. **Acepta:** ningún borrado ocurre con un solo toque y todos se pueden deshacer. ‹README R4; AUD-04›
- **REG-05 · MUST · Todo se hace sin teclado físico.** Escribir es la última opción. Todo campo de texto libre tiene 🎤 (dictado) y, cuando aplica, selección, biblioteca o IA. **Acepta:** un recorrido completo (bienvenida, crear perfil, crear atajo de cada tipo, vincular, probar) sin teclado físico, solo con toque y con Acceso por voz. ‹README R5; d07:45; AUD-47›
- **REG-06 · MUST · Todo control se expone a UI Automation.** Tiene nombre localizado, con el número de voz si está activo, rol, estado y patrones. Las regiones de aviso son *live*. **Acepta:** «mostrar números», «clic 4» y «clic Negrita» funcionan, y Narrador lee Auto/Fijo, ACTIVO, los avisos y el pánico. ‹README R6; d09:61-62; AUD-53›
- **REG-07 · MUST · Autoguardado y todo se puede deshacer.** No hay botón Guardar. Cada cambio de datos se puede deshacer, incluido editar un perfil (nombre, icono, modo compatible) y «Está bien así». ‹README R7; AUD-15›
- **REG-08 · MUST · Nunca se pierden datos.** Copia antes de migrar el esquema, actualizar, reemplazar o restaurar, copias versionadas y un documento ilegible nunca se sobrescribe. **Modificado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): se quita «Migración desde `profiles.json` v1 sin pérdidas», porque Clícalo no importa la configuración de Macro Quick Access; el resto de la regla no cambia. ‹README R8; AUD-42›

---

## 2. Catálogo por módulo

### 2.1 PAN · Panel: formas, geometría y estructura

- **PAN-001 · MUST · Formas y transiciones.** El panel adopta exactamente una forma a la vez: Completa, Compacta, Pestaña (asa cerrada o barra abierta), Burbuja u Oculto. Transiciones:
  - (a) «−» en la cabecera pasa a Burbuja y cierra Ajustes rápidos;
  - (b) un toque en la Burbuja sin arrastrar vuelve a la forma anterior;
  - (c) Ajustes rápidos › Vista cambia entre Completa, Compacta y Pestaña: cierra la hoja, vuelve a la página 1 y deja la Pestaña plegada, sin ventanas al costado;
  - (d) en la barra, Expandir y Buscar pasan la vista guardada a Completa, y Buscar abre además la búsqueda vacía;
  - (e) el icono de bandeja alterna entre Oculto y visible, y al mostrar sale de Burbuja.
  En la vista Pestaña no hay cabecera ni botón de minimizar. **Acepta:** una tabla de transiciones cubierta por pruebas. ‹P4:42-43,273,373,387; script 1637,1697-1707,1727,1753,1756›
- **PAN-002 · MUST · Ancho y posición.** Ancho = max(288, cols·w + (cols−1)·gap + 24), con w y gap de 72/6 en S, 92/8 en M y 116/10 en L. Por ejemplo, M con 3 columnas mide 316. Posición inicial arriba a la derecha: x = ancho útil − panel − 72; y = 40. El panel queda siempre **completo** dentro del área de trabajo del monitor, sin la barra de tareas y con un margen de 8 px. El alto máximo llega hasta 16 px por encima del borde inferior del área de trabajo. **Acepta:** en cualquier monitor, escala o posición de la barra de tareas, ningún píxel del panel sale del área de trabajo. ‹P4:43; script 1519-1521,1725; d04›
- **PAN-003 · SHOULD · Estilo.** Radio 18, sombra 0 18 50 al 45 %, fondo del token `panel` semitransparente y desenfoque del fondo de unos 14 px, adaptado a los controles nativos (fidelidad visual media). En alto contraste no hay transparencia ni desenfoque (TEM-004). ‹P4:43›
- **PAN-004 · MUST · Mover el panel.** El asa ⋮⋮ (visual 26, táctil 44, [move] «Mover panel») y la zona de icono y nombre del perfil arrastran el panel. Un gesto cuenta como arrastre cuando supera max(6 px, cancelMovePx). **Decisión:** es el mismo umbral que usan la burbuja y el asa de la Pestaña (§5 DIS-40). Un gesto que se convirtió en arrastre no cuenta como toque. La posición se guarda por monitor. ‹P4:45-46,374; script 1507-1513; AUD-11›
- **PAN-005 · SHOULD · Mover sin arrastrar.** Hay una forma de recolocar el panel sin arrastrar, para voz y conmutador: una opción «Mover panel» con posiciones predefinidas (las 4 esquinas y el centro de cada borde) o con flechas por pasos. ‹Hueco; §6 PQ-21›
- **PAN-006 · MUST · Posición por monitor y recolocación.** La posición del panel, la burbuja y la pestaña se guarda por monitor, con un identificador de monitor que se mantenga entre arranques. Ante cualquier cambio de tamaño, vista, escala, resolución, orientación, monitor o posición y autoocultado de la barra de tareas, todas las superficies se recolocan dentro del área de trabajo. Si el monitor guardado no existe, pasan al principal. **Acepta:** al desconectar el monitor que contiene el panel, el panel aparece completo en el principal. ‹d04:102-104; AUD-07; AUD-55›
- **PAN-007 · MUST · Orden vertical fijo de las capas.** De arriba abajo: cabecera, pánico, aviso de administrador, búsqueda, Ajustes rápidos, sugerencia, fila Siempre visible, Teclas fijas, selector de perfil, cuadrícula de perfiles, cuadrícula de atajos (con el menú contextual y los estados vacíos), fila inferior de Compacta o paginador, barra de avisos. ‹P4:44-269; d04 Estructura›
- **PAN-008 · MUST · Exclusión y precedencia de capas.**
  - Búsqueda y Ajustes rápidos se excluyen entre sí.
  - Minimizar, entrar en edición, abrir el CC y cambiar vista o tamaño cierran Ajustes rápidos.
  - Elegir un perfil cierra la cuadrícula de perfiles, la búsqueda y las ventanas al costado.
  - Cambiar de app cierra la búsqueda.
  - Mientras se busca con texto se ocultan la fila fija, las teclas fijas, el selector, la sugerencia, Auto/Fijo, la fila inferior de Compacta y la cuadrícula de perfiles.
  - **Decisión:** Ajustes rápidos y la cuadrícula de perfiles también se excluyen entre sí: abrir una cierra la otra.
  - El menú contextual se cierra al cambiar de perfil, de página o de app, y al tocar fuera.
  - Pánico, aviso de administrador y barra de avisos conviven con todo.
  **Acepta:** las reglas se evalúan con una función de visibilidad determinista, probada sin interfaz. ‹P4 script 1500-1502,1637,1703-1717; bordes EC-PAN›
- **PAN-009 · MUST · Estabilidad bajo el dedo.** Ningún cambio de disposición mueve un botón que tenga un contacto activo: aparición del pánico, del aviso de administrador, de avisos, de la sugerencia, del menú o de un cambio de perfil por cambio de app. Los cambios que desplazarían ese botón esperan a que terminen todos los contactos, o se dibujan superpuestos sin mover la cuadrícula. Un Mantener nunca se suelta por un cambio de disposición. **Acepta:** si se pulsa Mantener y aparece la franja de pánico, el botón no se mueve y el mantenido continúa. ‹Auditoría; EC-EJE-02›
- **PAN-010 · MUST · Esc.** Por orden de prioridad:
  1. mientras se graba con teclado, Esc cancela la grabación;
  2. con el foco en un campo, sale del campo;
  3. con un menú contextual abierto, lo cierra;
  4. con el CC abierto y sin la bienvenida, lo cierra y descarta el borrador vacío.
  En la bienvenida, Esc no cierra el asistente. ‹P4 script 1408,1413; d05:8-11; AUD-17›

### 2.2 CAB · Cabecera (vistas Completa y Compacta)

- **CAB-001 · MUST · Elementos de la cabecera.** Fila con padding 8/6/6/6 y separación 2, con estos elementos en orden:
  1. asa;
  2. icono en accent y nombre en negrita de 15 px con recorte «…»;
  3. Auto/Fijo (visual 36×36), oculto mientras se busca con texto;
  4. 🔍 Buscar ([searchA], fondo cardHi cuando está abierta);
  5. ✏ Editar ([edit]; en edición cambia a ✓ con fondo accent e icono check);
  6. Ajustes rápidos (icono `tune`, [quick], fondo cardHi cuando están abiertos);
  7. Minimizar (icono `remove`, [min]).
  Los botones miden hb de alto (36/40/46 en S/M/L) y 32 de ancho (40 en L), con área táctil de 44. El engranaje `settings` se reserva para el CC. ‹P4:44-56; script 1736›
- **CAB-002 · MUST · Título dinámico.** Muestra «Buscar» con el icono search si hay búsqueda con texto; «Frecuentes» con star en Frecuentes; en otro caso, el nombre y el icono del perfil mostrado. Si el perfil mostrado es el de la app activa y no se busca, se añade un indicador «auto» (punto) con nombre accesible. ‹P4:1575,1701›
- **CAB-003 · MUST · Botón Auto/Fijo.**
  - Auto: fondo accentWash, borde e icono autorenew en accent, nombre y tooltip [autoA].
  - Fijo: fondo rojo lavado, borde e icono lock en el token de peligro, nombre y tooltip [lockA].
  El comportamiento está en PER-006. El estado también se lee sin color, por el icono y el nombre accesible con patrón Toggle. ‹P4:51,295; script 1643,1715›

### 2.3 BUS · Búsqueda

- **BUS-001 · MUST · Abrir y cerrar.** 🔍 abre y cierra el campo (44 px, marcador [search]). Abrir o cerrar **vacía** el texto y cierra Ajustes rápidos y el selector. Al cambiar de app, la búsqueda se cierra y se vacía. ‹P4:52,71-75; script 1500,1703›
- **BUS-002 · MUST · Escribir sin romper REG-01.** El campo es la única superficie del panel que acepta foco de teclado. Al abrirlo:
  - (a) se memoriza la ventana en primer plano anterior;
  - (b) el campo toma el foco con el cursor dentro, para el teclado táctil o el dictado;
  - (c) antes de ejecutar un resultado, o al cerrar la búsqueda, el foco vuelve a la ventana memorizada y se comprueba antes de enviar nada;
  - (d) si no se puede devolver el foco, no se envía y se avisa.
  El teclado táctil no debe tapar el panel: si lo tapa, el panel se recoloca (EC-BUS-01). **Acepta:** buscar «negr» desde Word y tocar el resultado aplica Negrita en Word y nunca escribe en el panel. ‹AUD-06; AUD-48; §6 PQ-01›
- **BUS-003 · MUST · Dictado en la búsqueda.** El campo tiene un botón 🎤 que pone el foco y abre el dictado de Windows. ‹AUD-06; d07:45; DIS-24›
- **BUS-004 · MUST · Qué se busca.** Todos los perfiles y Siempre visible, en su orden, por subcadena del nombre ES, del nombre EN y de la combinación mostrada, **sin distinguir mayúsculas ni acentos** («numero» encuentra «Número»). **Decisión:** no se busca en el contenido de los botones Texto (privacidad). ‹P4 script 1541-1542; §5 DIS-41›
- **BUS-005 · MUST · Resultados.** Con texto: la cuadrícula muestra los resultados paginados y numerados como la normal. Bajo cada nombre aparece el perfil de origen («Siempre visible» si es global). Se aplican las ocultaciones de PAN-008 y la cabecera muestra «Buscar». Sin coincidencias se ve [noResults]. Con el campo abierto y vacío, el panel se ve igual que sin buscar. Tocar un resultado ejecuta la acción con su perfil de origen. ‹P4:191; script 1575,1714-1723›
- **BUS-006 · SHOULD · Búsqueda desde la barra.** 🔍 en la barra de la Pestaña abre la vista Completa con la búsqueda. **Decisión:** es una expansión **temporal**: al ejecutar un resultado o cerrar la búsqueda, se vuelve a la Pestaña. Expandir ⤢ sí cambia la vista guardada. ‹P4 script 1753; §5 DIS-42›

### 2.4 CUA · Cuadrícula de atajos, fichas y paginación

- **CUA-001 · MUST · Columnas y filas.**
  - Columnas: 2, 3 o 4 (3 por defecto), con separación 6/8/10 en S/M/L.
  - Filas máximas: la preferencia elegida (1, 2 o 3). En Auto son 2 en S y 3 en M/L.
  - Filas visibles = max(1, min(filas máximas, filas **completas** que caben)). «Las que caben» se **mide** sobre el espacio real hasta el borde inferior, así que bajar el panel reduce las filas y crea páginas.
  **Acepta:** una prueba con medidas simuladas cubre S/M/L, 100–150 % de texto y distintas posiciones. ‹P4 script 1549-1556; d04 §10›
- **CUA-002 · MUST · Solo filas enteras, sin desplazamiento.** La cuadrícula nunca muestra filas cortadas ni se desplaza. Si falta espacio, solo se encoge la zona de la cuadrícula. Si no cabe ni una fila entera, se aplica CUA-003 y, si sigue sin caber, el panel se desplaza hacia arriba hasta que quepa una fila. ‹d04; d09:122; §5 DIS-11›
- **CUA-003 · MUST · Falta de espacio con alerta.** Si se ve el pánico o el aviso de administrador y no cabe una fila entera (alto útil + 2 < alto de ficha + 6), se ocultan la fila Siempre visible y el selector de perfil hasta que desaparezca la alerta. Se decide midiendo. ‹P4 script 1420-1422; d04 §10›
- **CUA-004 · MUST · Paginación en Completa.**
  - Por página caben columnas × filas atajos. La página actual se ajusta si deja de existir.
  - Con más de una página se muestran: ◀ (visual 48×40), los puntos y ▶.
  - Punto activo: 24×10 en accent. Los demás: 10×10 en el color line, con transición de ancho de 200 ms.
  - Cada punto tiene el nombre «[pageN] n» y salta a su página. Su área táctil es de 44, aunque la fila de puntos sea más estrecha visualmente.
  - Las flechas se atenúan en la primera y la última página.
  ‹P4:243-259; script 1555-1558›
- **CUA-005 · MUST · Deslizar para cambiar de página.** Un deslizamiento horizontal de más de 60 px, con |dy| < 0,6·|dx|, cambia de página: hacia la izquierda, la siguiente; hacia la derecha, la anterior; sin salirse de los límites. El gesto no ejecuta el botón en el que empezó ni abre su menú. Durante los 300 ms siguientes se ignoran los toques de botones. ‹P4 script 1439,1712; v1 overlay.py:541-581›
- **CUA-006 · MUST · Vuelta a la página 1.** Se vuelve a la página 1 al cambiar de perfil (también por cambio de app), de vista, de columnas o de filas visibles. ‹P4 script 1502,1568,1637,1767›
- **CUA-007 · MUST · Anatomía de la ficha.** Botón de radio 12 y alto 66/78/98 en S/M/L (16 menos en Compacta), que crece con la escala de texto (CUA-011). Contiene:
  - **Icono:** 20/28/34 en el color de su categoría.
  - **Nombre:** 12/14/16 en negrita, por la escala de texto, en líneas equilibradas.
  - **Línea de teclas:** monoespaciada, en muted, con recorte; solo con «Mostrar teclas» activo y fuera de Compacta.
    - Texto: «…» con hasta 16 caracteres. **Decisión:** si el texto está marcado como privado, se muestra «Texto» sin contenido (§6 PQ-30).
    - Mouse: el nombre de la acción. Web y App: el destino. Macro: «—».
    - En Frecuentes y en la búsqueda, el perfil de origen.
  - **Insignia** arriba a la derecha: MANTENER, ALTERNAR/ACTIVO, número de pasos, WEB, APP, TXT o 📌 si está fijado en Frecuentes. Pulsar y Mouse no llevan insignia.
  - **Número de voz** arriba a la izquierda (ACC-010).
  ‹P4:208-216; script 1529-1547›
- **CUA-008 · MUST · Abreviaturas en S.** En S, la línea de teclas se abrevia: sin espacios alrededor de «+», Shift→⇧, Ctrl→Ctl. El nombre accesible siempre usa los nombres completos. ‹P4 script 1547›
- **CUA-009 · MUST · Estados visuales.**
  - Normal: fondo card y borde de 1 px.
  - Mantener presionado: escala 0,95 (80 ms), fondo de su categoría y borde de 2 px en su color.
  - Alternar activo: fondo de categoría, borde de 2 px en su color e insignia ACTIVO.
  - Armado: fondo de categoría y borde de 2 px en warn.
  - Destello: fondo de categoría durante 240 ms.
  - Modo prueba: ✓ o ⊘ durante 700 ms.
  - Edición: × de borrado.
  El fondo cambia en 150 ms (0 con reducir movimiento). Todos los estados llevan además texto o icono y se exponen como estado accesible, también en la barra y la fila fija, donde hoy solo hay color o un punto. ‹P4:208-214; d07:44; DIS-27›
- **CUA-010 · MUST · Estados vacíos.**
  - **Perfil sin atajos**, fuera de Frecuentes y de la búsqueda: tarjeta con borde discontinuo, icono inbox, [emptyProfT], [emptyProfS] con {p} y el botón accent de 44 px «+ [addShortcut]», que abre el CC con un atajo nuevo en blanco en el editor.
  - **Sin resultados:** [noResults].
  - **Frecuentes vacío:** tarjeta con el texto nuevo [freqEmptyT/S] («Aún no hay frecuentes. Los atajos que uses aparecerán aquí.») y el botón «Volver a {perfil}».
  ‹P4:191-199; AUD-02; §9›
- **CUA-011 · MUST · Escala de texto.** El nombre, la línea de teclas y la etiqueta de la fila fija valen round(base × escala). El alto de la ficha pasa a h + (escala − 1) × etiqueta × 1,6, y así lo exige docs/04, que el prototipo no aplica. En S, al 150 %, el nombre se ajusta en hasta 2 líneas con recorte «…», y la línea de teclas se oculta si no cabe. ‹d04:14-16; P4 script 1518; DIS-08›
- **CUA-012 · MUST · Modo edición en el panel.**
  - ✏ activa el modo, cierra Ajustes rápidos y muestra el aviso fijo [editHint]; el botón pasa a ✓.
  - Tocar una ficha, incluida la fila fija, abre el CC en Atajos con ese botón en el editor.
  - Cada ficha de la cuadrícula lleva arriba a la derecha una × roja (visual 32, táctil 44) con dos toques: el primero cambia a «[delConfirm]» en rojo oscuro durante 3,5 s y el segundo borra, con aviso [deleted] y Deshacer.
  - Al final aparece la ficha discontinua «+ [add]», salvo en Frecuentes y en la búsqueda. Abre la biblioteca, empezando en «Para {perfil}» si el perfil tiene plantilla.
  - En edición no hay Mantener, toque largo ni Repetir.
  - ✓ sale y quita el aviso.
  ‹P4:53,215-220; script 1536-1537,1723-1724,1918›
- **CUA-013 · MUST · × en Frecuentes y en la búsqueda.** **Decisión:** en Frecuentes, la × de edición **quita de Frecuentes**; no borra el atajo de su perfil. En la búsqueda no hay ×. ‹EC; §5 DIS-43›
- **CUA-014 · MUST · Menú contextual.**
  - **Cómo se abre:**
    - con el dedo quieto 600 ms sobre una ficha que no sea Mantener, fuera del modo edición, en la cuadrícula, la fila fija, la barra o la ventana Fijos;
    - también con clic derecho, con la tecla Menú o Mayús+F10 si el foco está en la ficha, y con la acción secundaria accesible («clic derecho {nombre}» por voz).
  - **Aspecto:** se abre dentro del propio panel, sobre la cuadrícula, con fondo win, borde accent y cabecera con el icono y el nombre del botón. Tiene filas de 44 px:
    1. [ctxPin]/[ctxUnpin]; fijar también quita la ocultación;
    2. [ctxHide], solo en Frecuentes y si no está fijado;
    3. [edit];
    4. [cancel].
  - **Avisos:** [ctxPinT], [ctxUnpinT] y [ctxHideT]. Quitar se puede deshacer.
  - **Cómo se cancela:** con Esc, al tocar fuera o al cambiar de contexto.
  - El toque que abre el menú no ejecuta la acción. El toque largo se cancela si el dedo sale de la ficha.
  ‹P4:200-205; script 1408,1440,1465-1471; AUD-38›
- **CUA-015 · MUST · Fijar y quitar de Frecuentes los atajos Mantener.** Los atajos de tipo Mantener, que no tienen toque largo, se fijan o se quitan de Frecuentes con clic derecho o la acción secundaria accesible de CUA-014, y también desde el editor (EDI-018). ‹AUD-38; AUD-59›

### 2.5 FIJ · Fila Siempre visible y fila de Teclas fijas

- **FIJ-001 · MUST · Cuándo se ve la fila Siempre visible.** Con `showStripRow` activo, sin búsqueda con texto y sin la ocultación de CUA-003. Su etiqueta es 📌 [always] en mayúsculas de 12 px, oculta en Compacta. ‹P4:138-154›
- **FIJ-002 · MUST · Rejilla de la fila.** 4 columnas.
  - Alto: 44/60/72 en S/M/L y 40 visual (44 táctil) en Compacta.
  - Icono: 18/22/26.
  - Nombre: 10/11/13 × escala, con un mínimo de 11 (TEM-007); oculto en S y en Compacta.
  - Los tipos que no son Pulsar ni Mouse llevan un punto de 8 px en su color **y** su estado accesible.
  ‹P4:145-148; seed SIZES›
- **FIJ-003 · MUST · Límite y paginación.** Caben 4 atajos en S o en Compacta y 8 en M o L. Si hay más, se muestran límite − 1 fichas y una ficha «··· i/N» ([stripMoreA]) que avanza de página y vuelve a la primera tras la última. **Acepta:** en S, con 4 atajos no hay paginación; con 5 se ven 3 por página más «··· 1/2». ‹P4 script 1546,1747; AUD-58›
- **FIJ-004 · MUST · Comportamiento de la fila Siempre visible.** Mismos estados, filtro, ejecución, toque largo y menú que la cuadrícula. Tiene número de voz (ACC-010). ‹DIS-14›
- **FIJ-005 · MUST · Fila de Teclas fijas.** Se ve con el interruptor activo y sin búsqueda con texto. Muestra Ctrl, Alt, Shift y Win en monoespaciada de 13 px (táctil 44). Estados:
  - 0 = suelta: fondo card y borde;
  - 1 = una vez: fondo accent y texto onAccent;
  - 2 = bloqueada: como 1, más un candado.
  Cada toque avanza 0→1→2→0 y avisa «{tecla} · [modOnce]», «· [modLock]» o «· [modOff]». Se expone como Toggle de tres estados. Apagar la fila suelta todas las teclas. ‹P4:156-160; script 1453,1560,1569›
- **FIJ-006 · MUST · Cómo se aplican las teclas fijas.** Las teclas fijas activas forman parte del registro de pulsadas (SEG-001). En la siguiente acción se combinan así:
  - (a) Pulsar: se envían Ctrl, Alt, Shift, Win y después las teclas del botón, sin repetir. Aviso: «Ctrl + Shift + C [sent] {app}».
  - (b) Mouse del panel (clic derecho, doble, central y arrastrar): se envían con el clic (Ctrl+clic).
  - (c) Mantener y Alternar: se añaden mientras el botón está pulsado.
  - (d) Texto, Web, App y Macro: **no** se aplican y siguen pendientes.
  Después, las de estado 1 se sueltan y las bloqueadas siguen. ‹d03 §6; [modOnce]; §5 DIS-12›
- **FIJ-007 · SHOULD · Teclas fijas con un clic físico.** Un clic físico fuera del panel con teclas fijas en estado 1 las consume y las suelta. Requiere observar el clic del sistema solo mientras haya alguna tecla en estado 1. ‹d03 §6; §6 PQ-15›

### 2.6 AVI · Barra de avisos, Repetir y Deshacer (panel)

- **AVI-001 · MUST · Barra de avisos.** Franja al pie, de radio 10 y texto de 13 px, anunciada como región *polite*. Estados:
  - Reposo: icono info en muted y [ready] sobre fondo transparente.
  - Aviso: fondo card e icono en accent.
  - Advertencia: fondo warnWash e icono en warn.
  Alto mínimo: 40 en Completa y 34 visual en Compacta. ‹P4:261-269›
- **AVI-002 · MUST · Duración y cola de avisos.**
  - Un aviso normal dura 3,2 s y uno con Deshacer, 6 s.
  - Los avisos fijos duran hasta que otro los sustituye o termina su estado: Mantener en curso, modo edición, Modo prueba, captura de app y ejecución de macro.
  - Se ve un aviso a la vez. **Decisión:** los avisos de seguridad ([releasedAuto], [releasedSwitch], «no enviado: app elevada») y los que tienen Deshacer no se pierden: si llega otro, quedan en cola y se muestran al terminar el actual.
  ‹P4 script 1431; EC-AVI›
- **AVI-003 · MUST · Botón Deshacer.** [undo] en accent aparece solo si el aviso corresponde a una operación que se puede deshacer y la pila no está vacía. Deshacer restaura el estado anterior y avisa [restoredU]. **Decisión:** Deshacer sigue accesible después de que el aviso desaparezca: la última operación se puede deshacer desde el CC (barra de estado) y por voz, con un nombre accesible estable. ‹P4:265; WCAG 2.2.1; NFR-A11Y›
- **AVI-004 · MUST · Repetir.**
  - ↻ ([repeat]; visual 36×32, táctil 44) vuelve a ejecutar la última acción con su perfil de origen.
  - Está en la barra de avisos, visible si hay una última acción y no se está editando, y en las herramientas de la barra de la Pestaña, gris si no hay última acción.
  - Cuentan Pulsar, Texto, Mouse, Macro, Web, App y Sistema ejecutados desde el panel. **No cuentan** Mantener ni Alternar.
  - Pasa por el filtro táctil de su propio botón ↻ y **vuelve a pedir** confirmación si el atajo la tiene.
  - El atajo se busca **por id** en el momento de repetir: si se editó, se usa la versión actual; si se borró, ↻ se desactiva.
  ‹P4:266,319; script 1404,1713,1753; §5 DIS-19›
- **AVI-005 · MUST · Deshacer desde el panel.** Todo cambio de datos hecho desde el panel muestra Deshacer durante 6 s: borrar con la ×, instalar desde la sugerencia, crear un perfil, fijar, quitar. La pila guarda 20 estados (DAT-006). ‹P4:1432-1433›

### 2.7 VCO · Vista Compacta

- **VCO-001 · MUST · Qué cambia frente a Completa.** Conserva todo lo de Completa, con estas diferencias:
  - no hay selector de perfil de 2 columnas, aunque `showTabsRow` esté activo;
  - no hay línea de teclas ni etiqueta «Siempre visible»;
  - la fila fija mide 40 visual, lleva solo iconos y tiene un límite de 4;
  - las fichas son 16 px más bajas;
  - el paginador va en la fila inferior;
  - la barra de avisos solo aparece con un aviso o una última acción; si no, queda un margen de 8 px.
  ‹P4:224-249; script 1549,1700›
- **VCO-002 · MUST · Fila inferior**, oculta mientras se busca con texto. Contiene:
  - ★ (visual 44×40);
  - ◀ (36×44), solo si hay varias páginas;
  - puntos (8 de alto; el activo de 24 de ancho);
  - ▶;
  - botón de perfil de 40 de alto, de 44 como mínimo a un máximo del 40 % del ancho, con icono, nombre de 12 px y ▾, o ✕ si su cuadrícula está abierta. En Frecuentes muestra ↶ (DIS-23).
  Desde Frecuentes, el botón de perfil vuelve en un toque. En otro caso, abre la cuadrícula de perfiles encima de la fila, desplazable, con un máximo del 40 % de la pantalla. ‹P4:241-249; script 1717-1718›

### 2.8 SEL · Selector y cuadrícula de perfiles

- **SEL-001 · MUST · Selector (vista Completa).** Se ve con `showTabsRow` activo, sin búsqueda con texto y sin la ocultación de CUA-003. Tiene 2 columnas del mismo ancho y 44 de alto:
  - **★ [freq]:** fondo accent y texto onAccent si Frecuentes está activo; si no, borde.
  - **Botón de perfil:**
    - muestra el perfil actual o, en Frecuentes, retProf;
    - lleva icono de 20, nombre de 15 en negrita con recorte y un punto de 8 si es el perfil de la app activa;
    - al final, ▾ cerrado, ▴ abierto o ↶ en Frecuentes;
    - estilos: activo = fondo accent; abierto = cardHi; en Frecuentes = card con borde;
    - se expone como ExpandCollapse.
  ‹P4:162-172; script 1644,1716-1719›
- **SEL-002 · MUST · Tocar el botón de perfil.** Desde Frecuentes, vuelve a retProf (PER-004). Si no, abre o cierra la cuadrícula de perfiles. Es igual en las tres vistas. ‹RN-04›
- **SEL-003 · MUST · Cuadrícula de perfiles.** Se abre bajo el selector en Completa, sobre la fila inferior en Compacta y al costado de la barra en la Pestaña. Tiene una ficha por perfil, en el orden guardado, sin Frecuentes ni Siempre visible:
  - fichas de 76 de alto, con un mínimo de 88 de ancho en Completa, 80 en Compacta y 96 en la Pestaña;
  - icono en accent y nombre de 13 con recorte;
  - el perfil actual lleva fondo accentWash y borde accent de 2; el de la app activa, un punto de 9 (onAccent sobre accent si coinciden).
  Fichas extra:
  - «Crear para {a}» (borde y fondo warn, add_circle), si la app activa no tiene perfil **y hay plantilla**. Al crear, la cuadrícula se cierra.
  - «+ [morePf]» con borde discontinuo, que abre el CC en Plantillas.
  Leyenda [activeLegend]. Se desplaza si supera el 46 % de la pantalla (Completa) o el 40 % (Compacta), sin que el desplazamiento dispare fichas (TAC-004). ‹P4:173-188,225-240,352-368; script 1709,1720,1755›
- **SEL-004 · MUST · Elegir un perfil.** Muestra ese perfil y, si no es Frecuentes, pasa a ser lastProfile. Vuelve a la página 1 en el panel y en la barra, y cierra la cuadrícula, la búsqueda y las ventanas al costado. No cambia Auto/Fijo. ‹P4 script 1502›
- **SEL-005 · SHOULD · Aviso de sugerencia en el selector.** Si hay una sugerencia de perfil pendiente para la app activa, el botón de perfil muestra un punto amarillo con su nombre accesible. ‹AUD-09; DIS-24›
- **SEL-006 · MUST · Acceso sin la fila del selector.** Con `showTabsRow` desactivado en Completa, se llega a Frecuentes y a otros perfiles tocando el título de la cabecera, que abre la cuadrícula de perfiles e incluye ★ Frecuentes. ‹EC; §6 PQ-27›

### 2.9 PES · Vista Pestaña (asa, barra y ventanas al costado)

- **PES-001 · MUST · Asa cerrada.**
  - **Forma y posición:** asa de 32×116 en los lados izquierdo y derecho, o de 128×32 arriba y abajo, centrada en la posición guardada **para ese lado** (8–92 %, 50 por defecto). Radio solo hacia dentro, fondo panel, borde y sombra. Nombre [openBar].
  - **Contenido:** chevron de 22 hacia dentro de la pantalla, icono del perfil mostrado (★ en Frecuentes, keyboard si no hay perfil) y un punto warn de 9 si hay **cualquier** elemento pulsado, teclas fijas incluidas.
  - **Márgenes:** en el lado derecho, con «Respetar la barra de desplazamiento», deja 18 px libres. Abajo, queda sobre la barra de tareas.
  ‹P4:273-278; script 1745,1761; DIS-13›
- **PES-002 · MUST · Tocar y arrastrar el asa.** Tocar abre la barra y la despierta. Arrastrar a partir de max(6 px, cancelMovePx) la mueve a lo largo del borde, guarda la posición por lado y no abre la barra. Se evitan los gestos de borde de Windows: la zona de arrastre no empieza en el píxel del borde (EC-PES-03). ‹P4 script 1462-1463,1503-1505›
- **PES-003 · SHOULD · Bloquear la posición del asa.** Opción en General › Modo pestaña que impide arrastrar el asa; el toque sigue abriendo la barra. Necesita textos nuevos (§9). ‹AUD-11; DIS-24›
- **PES-004 · MUST · Opacidad del asa.** **Decisión:** el asa usa la opacidad del usuario y el atenuado, con un mínimo del 55 % en ambos casos, igual que la burbuja. ‹P4:1761; d04:99; §5 DIS-07›
- **PES-005 · MUST · Barra abierta.**
  - Vertical: 76/88/108 de ancho en S/M/L, centrada en su borde, con un alto máximo igual al área útil − 24.
  - Horizontal: 58/66/78 de alto, centrada, a 12 px del borde (abajo, sobre la barra de tareas), con un ancho máximo igual a la pantalla − 40.
  - Radio 18, padding 8 y separación 6. Misma opacidad y atenuado que el panel.
  - Cuatro zonas separadas por divisores:
    1. controles: cerrar, con el chevron hacia el borde y [hideBar], y Expandir (open_in_full, [expand]);
    2. Qué ver;
    3. atajos;
    4. herramientas.
  Cerrar pliega la barra y cierra las ventanas al costado. Expandir pasa a Completa. ‹P4:280-343; script 1615-1620›
- **PES-006 · MUST · Zona «Qué ver».** Grupo segmentado (patrón Selection) con:
  - ★ y el texto [freq];
  - el botón de perfil: icono de 22, nombre, ▾ o ✕, ↶ en Frecuentes (DIS-23) y el punto de la app activa. Muestra el perfil actual o retProf.
  Debajo, la pastilla Auto/Fijo (PER-006), con icono y el texto «Auto» o «Fijo». **Decisión:** para no confundirlo con el candado de plegado, el candado se rotula «Se pliega» / «Abierta» (DIS-44). ‹P4:287-295›
- **PES-007 · MUST · Atajos de la barra.** Muestra los mismos atajos que mostraría la cuadrícula.
  - **Por página:** max(1, min(preferencia de General: 4/5/6/8, 5 por defecto; atajos que caben enteros según la medida real)).
  - **Fichas:** sin teclas ni insignias, pero con su estado accesible.
    - Vertical: alto de 50/58/70.
    - Horizontal: 62×58, 72×66 y 88×78.
    - Icono de 20/22/26 y nombre de 11 como mínimo, con recorte.
  - **Paginador:** «▲ i/N ▼» en vertical, o ◀ ▶ a los lados en horizontal (táctil 44).
  - La numeración de voz continúa entre páginas.
  ‹P4:297-314; script 1624-1626›
- **PES-008 · MUST · Herramientas de la barra.** En este orden:
  1. 🔍 Buscar (BUS-006);
  2. ↻ Repetir;
  3. 📌 [alwaysShort] «Fijos»: solo si la fila fija está activa y tiene atajos; alterna su ventana y, activo, lleva fondo accentWash y borde accent de 2;
  4. Teclas fijas: solo si el interruptor está activo; abre una ventana al costado con Ctrl, Alt, Shift y Win y sus tres estados (AUD-13);
  5. candado de plegado: [pinOff] «Se pliega» o [pinOn] «Abierta», con fondo accent, [pinOffA]/[pinOnA] y patrón Toggle;
  6. ⏶ Subir y ⏷ Bajar: desplazamiento de tipo Mantener, que cuenta como pulsado mientras se mantiene.
  ‹P4:316-343; AUD-13; DIS-24›
- **PES-009 · SHOULD · Ajustes rápidos desde la barra.** Botón `tune` en herramientas que abre Ajustes rápidos al costado, incluida la fila Lado de la pestaña (AJR-005). ‹DIS-22; §6 PQ-07›
- **PES-010 · MUST · Ventana «Fijos».** Ventana de 230 px pegada al botón 📌, alineada por abajo, a 16 px hacia el interior y con un alto máximo igual a la pantalla − 140, con desplazamiento. Lleva la cabecera 📌 [always] y los atajos en 2 columnas de 68 (icono de 24, nombre de 12). Tras usar un atajo se cierra, salvo con Mantener o Alternar. Admite toque largo. Mientras está abierta, la barra no se atenúa. ‹P4:321-335›
- **PES-011 · MUST · Cuadrícula de perfiles al costado.** Ventana de 236 px pegada a la barra, alineada por arriba, a 8 px, con un alto máximo igual a la pantalla − 140. Lleva la cabecera [pickProfile] y el mismo contenido que SEL-003 (fichas de 96 como mínimo). Mientras está abierta, oculta la guía y la barra no se atenúa. ‹P4:352-368›
- **PES-012 · MUST · Repliegue automático.** Con «Se pliega», la barra se pliega 900 ms después de ejecutar una acción, incluido soltar un Mantener. No se pliega al activar o desactivar un Alternar ni con el primer toque de una confirmación. Con «Abierta», nunca se pliega sola. En General aparece como [autoHide], con la lógica invertida. ‹P4 script 1404,1448-1449,1766›
- **PES-013 · MUST · Pánico en la Pestaña.**
  - Barra abierta y algo pulsado: aparece un botón flotante rojo en forma de pastilla, de 44, con ⚠ y [releaseAll], a 110 px del borde (180 desde abajo en el lado inferior), centrado y siempre al 100 %. Suelta todo.
  - **Barra cerrada:** el mismo botón aparece junto al asa (un toque, REG-03).
  ‹P4:281,276; DIS-36›
- **PES-014 · MUST · Avisos en la vista Pestaña.** Hace falta una superficie no activable junto a la barra, o junto al asa si está cerrada, para los avisos que en Completa van en la barra de avisos:
  - confirmación armada «Toca otra vez…»;
  - motivo del soltado automático;
  - Modo prueba (✓ o ⊘ también en las fichas);
  - aviso de app elevada con su botón;
  - aviso fijo de captura con Cancelar;
  - Deshacer.
  Tiene la misma semántica *live*. **Acepta:** cada aviso de AVI-002 es visible en la Pestaña. ‹EC-PES-01; §5 DIS-45›
- **PES-015 · SHOULD · Guía de primera vez.**
  - **Cuándo aparece:** al abrir la barra mientras `coachDone` sea falso y no haya ventanas al costado.
  - **Forma:** tarjeta accent de 260 al costado, alineada con la parte superior de la barra.
  - **Contenido:** «i / 3», los pasos [co1t]/[co1d], [co2t]/[co2d] y [co3t]/[co3d], y dos botones de 44: [coachSkip] y [next] (en el paso 3, [understood]).
  - **Al terminar u omitir:** coachDone = verdadero.
  - Se puede volver a ver desde General › Primeros pasos, con el texto nuevo «Ver la guía de la pestaña».
  ‹P4:344-351; AUD-12›
- **PES-016 · MUST · Varios monitores.** La barra vive en el monitor donde está el panel. La posición del asa se guarda por monitor y por lado. ‹§6 PQ-18›

### 2.10 BUR · Burbuja y bandeja

- **BUR-001 · MUST · Burbuja.** Círculo de 64 en la posición del panel, con fondo panel, borde de 1, sombra, icono keyboard de 30 en accent y nombre [restore]. Un toque restaura el panel y un arrastre (umbral de PAN-004) la mueve. La posición es compartida: al restaurar, el panel aparece donde quedó la burbuja. ‹P4:373-375›
- **BUR-002 · MUST · Opacidad de la burbuja.** Usa la opacidad del panel con un **mínimo del 55 %** en cualquier estado, también sin atenuar. Con pánico se ve al 100 % con un anillo rojo de 3. **Decisión:** con pánico aparece también a su lado el botón flotante [releaseAll] (un toque). ‹AUD-08; DIS-07; DIS-36›
- **BUR-003 · MUST · Icono de bandeja.**
  - **Clic:** muestra u oculta el panel en su forma actual; si estaba minimizado, lo muestra restaurado.
  - **Estado oculto:** el icono lo indica visualmente, a 55 %, y en su texto accesible.
  - **Menú:** Mostrar/Ocultar, Centro de control, **Soltar todo** (activo si hay algo pulsado), Pausar y Salir.
  - **Ocultar:** si se oculta con algo pulsado, **se suelta todo** con [releasedAll].
  - **Salir:** suelta todo.
  ‹P4:385-387; d01:62; DIS-36›
- **BUR-004 · SHOULD · Pausar.** Pausar oculta el panel, suspende el cambio automático de perfil y bloquea todo envío hasta «Reanudar». Al pausar se suelta todo, y el icono de bandeja muestra el estado. Hacen falta textos nuevos. ‹d01:62; §6 PQ-19›
- **BUR-005 · MUST · Recuperar el panel sin teclado.** Cualquier forma de ocultar el panel se puede deshacer con el dedo y por voz: desde el icono de bandeja o con la orden «clic Clícalo». Existe un atajo global opcional, que no está en conflicto con Ctrl+Shift+M (Silenciar en Teams). ‹v1 lección; §6 PQ-22›

### 2.11 AJR · Ajustes rápidos

- **AJR-001 · MUST · Hoja de Ajustes rápidos.** Hoja bajo la búsqueda, con un alto máximo del 62 % de la pantalla y desplazamiento interno. En este orden:
  1. Tarjeta «Centro de control» ([openCC] + [ccSub], 56, borde accent). Abre el CC en Atajos con el perfil mostrado, o General si se está en Frecuentes.
  2. Vista: Completa, Compacta y Pestaña (52; view_agenda, view_compact, view_sidebar).
  3. Opacidad (AJR-002).
  4. Tamaño: S, M y L.
  5. Lado de la pestaña, solo en la vista Pestaña: izquierda, arriba, abajo y derecha, con iconos y nombre accesible.
  6. Tema en 2×2: Auto, Oscuro, Claro y Alto contraste.
  7. Cuatro filas con interruptor, tocables en toda su superficie: Modo prueba (30 s), [autoDim], [stickyMods] y [voiceNums].
  Lo elegido lleva fondo accent y texto onAccent **y** estado de selección accesible.
  - **Cierran la hoja:** Vista, Tamaño, Lado, activar el Modo prueba, abrir la búsqueda, entrar en edición, minimizar y abrir el CC.
  - **No la cierran:** Tema, Opacidad, Atenuar, Teclas fijas y Números.
  ‹P4:77-126; script 1563-1569,1637-1639,1706-1708›
- **AJR-002 · MUST · Opacidad.** Etiqueta [opacity] con el valor en %. Tiene −, un deslizador de 30 a 100 en pasos de 5 y +. Los botones suben o bajan un 10 % y redondean al múltiplo de 5, sin salir de 30–100. Por defecto, 92 %. Se aplica en vivo al panel, la barra, la burbuja y el asa (con los mínimos de BUR-002 y PES-004). ‹P4:90-97; script 1401,1762›
- **AJR-003 · MUST · Tamaño.** S, M y L. Al cambiar, se cierra la hoja, se vuelve a la página 1 y la barra se pliega. ‹P4 script 1781›
- **AJR-004 · MUST · Tema.** Aplica al instante en todas las ventanas (TEM-001). ‹P4:113›
- **AJR-005 · MUST · Lado de la pestaña.** Cambia de lado, restaura la posición guardada para ese lado y deja la barra plegada. ‹P4:104›

### 2.12 EJE · Ejecución de acciones

- **EJE-001 · MUST · Flujo común de un toque.**
  1. Filtro táctil (TAC-002).
  2. Si está en modo edición, abre el editor.
  3. Si está en Modo prueba, marca ✓ o ⊘ y **no envía nada** (TAC-008).
  4. Si la app destino está elevada, no envía nada (EJE-013).
  5. Confirmación (EJE-002).
  6. Ejecución según el tipo.
  7. Efectos posteriores (EJE-011).
  **Acepta:** existe una única implementación de este flujo, usada por la cuadrícula, la fila fija, la barra, las ventanas al costado, la búsqueda, Frecuentes y Repetir. ‹d03 §4; AUD-37; lección L-ARQ›
- **EJE-002 · MUST · Confirmación de ejecución.** Si el atajo tiene `confirm` (por ejemplo, Alt+F4), el primer toque aceptado lo arma:
  - borde warn y aviso de advertencia «[confirmClose] — {nombre}» durante 3 s;
  - un segundo toque aceptado en el mismo botón dentro del plazo lo ejecuta; al vencer, se desarma;
  - armar otro atajo sustituye al anterior, y ejecutar cualquier otro desarma.
  **Decisión:** el antirrebote del botón armado no se aplica al segundo toque de confirmación (EC-TAC-03). ‹P4 script 1449-1450; d03:46›
- **EJE-003 · MUST · Pulsar.** Envía la combinación en el **orden de pulsación guardado**, con pulsación y liberación explícitas, y luego suelta en orden inverso. Aviso: «{teclas} [sent] {app}». ‹d02; d03›
- **EJE-004 · MUST · Mantener.**
  - **Con el dedo:** al apoyar (después del contacto mínimo del filtro) presiona y muestra el aviso fijo «[holding] {teclas} — [releaseH]», con escala 0,95.
  - **Se suelta** al levantar el dedo o al cancelarse el contacto, con el aviso «{teclas} [released]», el destello y el repliegue de la barra. **Decisión:** salir del botón no suelta mientras el contacto siga dentro del área extra del propio botón. Más allá de ella, sí suelta.
  - No tiene menú de toque largo. En edición no presiona nada.
  ‹P4 script 1470-1473; DIS-20›
- **EJE-005 · MUST · Mantener por voz, teclado o conmutador.** Invocado sin duración de contacto (patrón Invoke), un Mantener se comporta como un Alternar: la primera invocación presiona, con aviso fijo, pánico y límite de tiempo, y la segunda suelta. ‹EC; §6 PQ-02›
- **EJE-006 · MUST · Varios contactos.** Cada contacto se sigue por separado. Se admiten varios Mantener a la vez (por ejemplo, Shift mantenido más otro botón), y cada uno se suelta al terminar **su** contacto. **Acepta:** dos dedos en dos Mantener; levantar el primero suelta solo el primero. ‹EC-EJE-01›
- **EJE-007 · MUST · Alternar.** Cada toque aceptado invierte el estado:
  - activo: teclas presionadas, insignia ACTIVO, borde de 2 y aviso «{nombre} · [latched]» (icono lock);
  - inactivo: se sueltan, aviso «· [unlatched]».
  Arrastrar (mouse) es un Alternar del botón izquierdo. ‹P4 script 1448›
- **EJE-008 · MUST · Texto.**
  - [tmType], el método por defecto: carácter a carácter en Unicode, sin depender de la distribución, con ñ, tildes y emoji.
  - [tmPaste]: guarda el portapapeles, pega y lo restaura a los 500 ms. **Decisión:** el contenido pegado se marca para que no entre en el historial ni en la sincronización del portapapeles de Windows, y se restauran todos los formatos anteriores.
  - Salto de línea: **decisión**, se envía como Enter.
  - Aviso: «[typed] «{16 primeros caracteres}…»», o «Texto escrito» si es privado.
  ‹d03:33-35; AUD-23›
- **EJE-009 · MUST · Mouse.** Hay 8 acciones: clic derecho, doble clic, clic central, arrastrar (Alternar) y desplazar ↑ ↓ ← →. Los desplazamientos repiten cada 60, 40 o 25 ms (lento, normal, rápido) con aceleración progresiva mientras se mantienen. **Punto objetivo (decisión):** la última posición del puntero **fuera** de las ventanas de Clícalo. Tras el toque, el puntero vuelve allí antes de actuar, y la rueda actúa sobre la ventana bajo ese punto. Aviso: el nombre de la acción. ‹d03:36-39; AUD-25; §6 PQ-03›
- **EJE-010 · MUST · Macro.**
  - Ejecuta los pasos en orden: teclas, espera, texto o mouse.
  - Mientras dura, el botón muestra el estado «ejecutando» (paso i/n) con un aviso fijo.
  - Un segundo toque la **cancela** y suelta todo lo que tuviera pulsado.
  - También se cancela con «Soltar todo», al cambiar de app o si la app destino pasa a estar elevada.
  - Aviso final: «[ranMacro]: {nombre} ({n})». Hacen falta textos nuevos para «ejecutando» y «cancelada».
  ‹d03:51; DIS-18›
- **EJE-011 · MUST · Web y App.**
  - **Web:** solo http o https, con el navegador predeterminado; «ejemplo.com» se completa a https://. Aviso: «[opened] {url}».
  - **App:** abre un ejecutable, una app de la Tienda (por su identificador) o un documento, **sin** pasar por un intérprete de comandos.
  - **Decisión:** si Clícalo está elevado, las apps y las webs se abren sin elevación.
  - Se rechazan las rutas de red (UNC) salvo confirmación.
  ‹d03:40; AUD-24; LOG-008›
- **EJE-012 · MUST · Después de ejecutar.** Destello de 240 ms si está activo y sonido suave si está activo, sin retrasar el envío. Se suma el uso a Frecuentes (FRE-002), se guarda como última acción (AVI-004) y la barra se repliega (PES-012). ‹d03 §4›
- **EJE-013 · MUST · App elevada.**
  - Si la app en primer plano está elevada y Clícalo no: **no se envía nada** y se muestra la tarjeta warnWash (admin_panel_settings) con [adminMsg] ({a} = app) y [adminBtn]. Cada toque avisa con el texto nuevo «No se envió: {a} es de administrador».
  - [adminBtn] relanza Clícalo elevado, conservando el estado, y avisa [adminOn].
  - La tarjeta desaparece al cambiar a una app no elevada o al quedar Clícalo elevado.
  - Si no se puede saber si la app está elevada (proceso protegido), no se muestra aviso de administrador: se intenta el envío y, si falla, se avisa.
  ‹P4:65-70; d03:41; DIS-15; AUD-49›
- **EJE-014 · MUST · Combinaciones bloqueadas en el panel.** Un atajo cuya clave es «bloqueada» (Ctrl+Alt+Supr) no envía nada y avisa [blockedB] al tocarlo. Win+L se sustituye por la acción de sistema «Bloquear equipo» (EJE-016). ‹AUD-20›
- **EJE-015 · MUST · Atajo incompleto en el panel.** Tocarlo no envía nada y avisa con el texto nuevo «Este atajo está incompleto · Toca ✏ para completarlo». ‹AUD-16; §9›
- **EJE-016 · SHOULD · Acciones de sistema.** Tipo interno para acciones que no se pueden enviar como teclas: Bloquear equipo, y brillo + y − en los equipos que lo permitan (si no, se ocultan). Se ofrecen en la biblioteca, en lugar de Win+L y de las teclas Brillo. ‹AUD-20; seed l_lock; §5 DIS-35›
- **EJE-017 · MUST · Primer toque sobre el panel atenuado.** El primer toque despierta el panel **y** ejecuta (comportamiento del prototipo). ‹P4; §6 PQ-11›

### 2.13 SEG · Seguridad de teclas

- **SEG-001 · MUST · Registro de pulsadas.** Hay un único registro de todo lo pulsado: cada Mantener, cada Alternar activo (incluido arrastrar), cada tecla fija en estado 1 o 2, los modificadores de una macro en curso, y Subir o Bajar de la barra. Cada elemento guarda el momento en que se pulsó y su origen. ‹P4 script 1645; d03 §5›
- **SEG-002 · MUST · Franja de pánico.**
  - **Cuándo:** en Completa y Compacta, bajo la cabecera, mientras el registro no esté vacío.
  - **Aspecto:** fondo del token de peligro, icono warning y [panicMsg] «Pulsado: {k}», donde {k} lista la combinación del botón, su nombre si no tiene combinación, o el modificador, separados por «, ».
  - **Botón:** [releaseAll] blanco con texto rojo, de 44 como mínimo.
  - **Accesibilidad:** se anuncia como alerta *assertive*.
  - **Atenuado:** no se atenúa ni deja atenuar el panel.
  ‹P4:58-64; script 1645-1646,1748›
- **SEG-003 · MUST · Soltar todo.** Suelta todas las teclas y botones del mouse del registro, vacía Mantener, Alternar y teclas fijas, cancela las macros y avisa [releasedAll] (lock_open). ‹P4 script 1464›
- **SEG-004 · MUST · Soltado por tiempo.**
  - Límite global: 30 s, 1 min (por defecto), 2 min o Nunca.
  - Cada Mantener o Alternar puede tener su propio límite ([autoRelease]); si no lo tiene, usa el global.
  - **Decisión:** el plazo cuenta **por elemento** desde que se pulsó. Cambiar el límite recalcula los plazos en curso.
  - Al vencer se suelta ese elemento y se avisa [releasedAuto] con {n} = segundos.
  - Si el dedo sigue sobre un Mantener cuando vence, se suelta igualmente y el mantenido no se reinicia hasta un nuevo contacto.
  ‹d02:68; d03:69; DIS-17›
- **SEG-005 · MUST · Soltado al cambiar de app.** Si [safeSwitch] está activo (por defecto) y hay algo pulsado cuando la app en primer plano cambia **de verdad** (no por ventanas de Clícalo, del shell ni del teclado táctil), se suelta todo y se avisa [releasedSwitch]. «Probar ahora» no cuenta como cambio del usuario (PRB-006). ‹P4 script 1499; d03:70›
- **SEG-006 · MUST · Soltado por eventos del sistema.**
  - Se suelta **siempre** al bloquear la sesión, suspender, cerrar la sesión, salir de Clícalo o producirse un fallo.
  - Al arrancar se sueltan preventivamente todos los modificadores.
  - Hay un mecanismo independiente que suelta las teclas si el proceso principal muere.
  - Si el proceso muere con la sesión bloqueada (o con otro escritorio seguro delante), ese mecanismo reintenta soltar en cada latido hasta que el escritorio lo acepte (al desbloquear) y solo entonces relanza Clícalo; el proceso nuevo nunca pulsa una tecla que el mecanismo vaya a soltar después. **Modificado por decisión del usuario del 2026-10-03** (D3, [ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md), punto 6): antes se soltaba una vez y el rechazo del escritorio seguro dejaba la tecla pulsada al desbloquear.
  - Tras volver de un bloqueo o suspensión, un aviso explica el motivo (textos nuevos: «Se soltaron las teclas al bloquear o suspender el equipo»).
  ‹d03:71-72; d08:8-10; AUD-01; D3›
- **SEG-007 · MUST · Ningún camino deja algo pulsado.** Comprende: levantar el dedo, salir del botón, cancelar el contacto, cambiar de app, vencer el plazo, bloquear, suspender, cerrar, fallar, morir con la sesión bloqueada (D3), relanzar elevado, ocultar desde la bandeja, cambiar de vista y un error a mitad de un envío. **Acepta:** batería de pruebas con un receptor de entrada simulado y un «estado físico» final vacío en todos los casos. ‹NFR; lección v1›
- **SEG-008 · SHOULD · Evitar las Teclas especiales de Windows.** No se envía Shift 5 veces seguidas en menos de 1 s por uso de teclas fijas o Alternar. Si ocurre, se inserta una pausa. ‹EC-SEG›

### 2.14 PER · Resolución de perfil y Auto/Fijo

- **PER-001 · MUST · Estado del perfil.** El panel mantiene:
  - la vista actual (Frecuentes o un id de perfil), nunca Siempre visible;
  - `lockProfile` (Auto o Fijo);
  - `lastProfile`.
  ‹P4:1398; d03:11›
- **PER-002 · MUST · Perfil de un proceso.** profileFor(proceso) devuelve el primer perfil, en orden, **alguno de cuyos procesos** no vacíos coincide con el ejecutable en primer plano **sin distinguir mayúsculas**. General nunca coincide. Las apps de la Tienda se resuelven a su proceso real. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): un perfil puede vincular varios procesos (PQ-45), para que una plantilla como Navegador siga a todos sus programas. ‹d03:17; D2›
- **PER-003 · MUST · Cambio de app en Auto.** Cuando cambia la app en primer plano (ignorando las ventanas propias, las del shell y las del teclado táctil o Acceso por voz):
  1. soltado (SEG-005);
  2. vinculación pendiente (ATJ-008);
  3. si está en Auto y fuera de Frecuentes, la vista pasa a profileFor(proceso) **o a General** si la app no tiene perfil;
  4. si la vista cambió, vuelve a la página 1 en el panel y en la barra;
  5. se cierra y vacía la búsqueda.
  Frecuentes y Fijo no cambian solos. El cambio no reordena bajo el dedo (PAN-009). **Acepta:**
  - Word→Chrome muestra Navegador en la página 1;
  - Word→Administrador de tareas muestra General;
  - en Fijo o en Frecuentes no cambia nada.
  ‹d03:13-16; README glosario; §5 DIS-01›
- **PER-004 · MUST · Perfil de retorno (retProf).** En orden:
  1. el perfil de la app activa, si está en Auto y existe;
  2. lastProfile, si sigue existiendo;
  3. el perfil de la app activa, si existe;
  4. General.
  Se usa en el botón de perfil estando en Frecuentes. ‹P4 script 1644; DIS-04›
- **PER-005 · MUST · Elección manual.** Se mantiene mientras la app activa no cambie. ‹v1; P4›
- **PER-006 · MUST · Auto ↔ Fijo.**
  - **Auto→Fijo:** fija el perfil mostrado o, si está en Frecuentes, retProf. **Decisión:** en ese caso se guarda lastProfile = retProf y se queda en Frecuentes. Aviso «[profLocked]: {perfil}».
  - **Fijo→Auto:** si no está en Frecuentes, salta al perfil de la app activa o a General, en la página 1, y avisa «[profAuto]: {perfil}».
  - El interruptor «Cambiar con la app activa» de General es **el mismo ajuste** y hace la misma transición.
  ‹P4 script 1643,1715,1768; d03:23-24; DIS-02,03›
- **PER-007 · MUST · Instalar la plantilla de la app activa.** Instalarla cambia la vista a ese perfil **solo** si está en Auto y fuera de Frecuentes. ‹DIS-46›
- **PER-008 · MUST · Borrar o renombrar el perfil mostrado.**
  - Si se borra el perfil mostrado, el de retorno o lastProfile, el panel y el editor pasan a General y lastProfile se corrige.
  - Si se borra el perfil fijado en modo Fijo, el panel pasa a General y sigue en Fijo.
  - Renombrar un perfil no cambia su id.
  ‹P4 script 1876; EC›
- **PER-009 · MUST · Sugerencia de perfil.**
  - **Cuándo:** la app activa no tiene perfil, «Detectar» (autoSuggestProfiles) está activo, esa app no se descartó en la sesión, no se está buscando y **existe plantilla** que incluya su proceso entre los suyos. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): una plantilla puede tener varios procesos y cualquiera de ellos cuenta (`StarterContent.TemplateFor`).
  - **Tarjeta:** accentWash con borde accent, «**{app}** [suggest]», [create] (principal) y [notNow].
  - **Crear perfil:** instala la plantilla con deshacer y el aviso «[installedT]: {app}», y la muestra.
  - **Ahora no:** descarta la app hasta cerrar Clícalo.
  - Aparece en Completa y Compacta; en la Pestaña, como ficha «Crear para» (SEL-003).
  ‹P4:128-136; script 1709,1755; d04 §6; DIS-05›

### 2.15 FRE · Frecuentes

- **FRE-001 · MUST · Composición.**
  1. Primero los fijados, en el orden en que se fijaron, si siguen existiendo.
  2. Después los no fijados y no ocultos con al menos un uso en los **últimos 30 días**, de más a menos usos. Desempate: el uso más reciente y, después, el orden de los perfiles.
  Se muestran como máximo 9 en total. Si hay más de 9 fijados, se muestran los 9 primeros y el menú de fijar avisa del límite (texto nuevo). Entran atajos de cualquier perfil y de Siempre visible. **Decisión:** se excluyen los que ya se ven en la fila Siempre visible, para no mostrarlos dos veces (§6 PQ-23). ‹d03:86-87; P4 script 1543›
- **FRE-002 · MUST · Registro de uso.** Cada ejecución efectiva guarda una marca de tiempo del atajo, dentro del documento de datos para que funcione Deshacer.
  - Cuentan Pulsar, Texto, Mouse, Macro, Web, App y Sistema. **Decisión:** Mantener cuenta una vez al soltar y Alternar al activarse.
  - No cuentan los toques filtrados, el primer toque de confirmación, el Modo prueba ni «Probar ahora».
  - Las marcas de más de 30 días y las de atajos borrados se purgan.
  ‹d02:46; d03:55; DIS-19›
- **FRE-003 · MUST · Presentación.** Bajo el nombre se muestra el perfil de origen. Los fijados llevan la insignia 📌 en lugar de la del tipo. No hay ficha «+ Añadir». Al tocarlo se ejecuta con su perfil de origen. Frecuentes nunca cambia sola. ‹P4 script 1543,1723›
- **FRE-004 · MUST · Reiniciar Frecuentes.** En General, con dos toques ([delConfirm] durante 3,5 s). Vacía usos, fijados y ocultos, avisa [resetFreqT] y Deshacer restaura **los tres**. ‹d03:89; DIS-47›
- **FRE-005 · MUST · Borrar un atajo fijado.** El atajo desaparece de los fijados, y Deshacer lo devuelve a su posición entre ellos. ‹EC›

### 2.16 REP · Combinaciones repetidas

- **REP-001 · MUST · Clave canónica.** Solo tienen clave los atajos Pulsar, Mantener y Alternar con al menos una tecla y sin mouse.
  - Los **modificadores** se comparan como conjunto, sin importar el orden, incluido su lado (izquierdo, derecho o cualquiera).
  - Las **teclas principales** se comparan por identidad canónica y en orden.
  - Los alias (Supr, Delete, Del) equivalen.
  - «Ctrl» sin lado equivale a «Ctrl izq.» al comparar con combinaciones bloqueadas.
  La clave se usa para repetidos, «ya añadido», «ya está» y combinaciones bloqueadas. ‹d03:92; DIS-48›
- **REP-002 · MUST · Regla de repetido.** Dos atajos distintos con la misma clave son repetidos si:
  - (a) uno está en Siempre visible, o
  - (b) están en la misma lista, o
  - (c) tienen el mismo nombre, comparado en ES **y** en EN (el estado no cambia con el idioma).
  Las claves marcadas «Está bien así» (dupIgnored) no se marcan. ‹d03 §9; P4 script 1524-1527›
- **REP-003 · MUST · Contadores y chip.** El menú Atajos cuenta las combinaciones repetidas distintas. El chip warn «N [dupSummary] · [review]» abre la primera, eligiendo su aparición fuera de Siempre visible si la hay. Cada ficha repetida lleva ⚠. ‹P4:415-425; script 1576-1580,1758›
- **REP-004 · MUST · Tarjeta de repetidos en el editor (plegada).** Tarjeta warn «[dupHead]», contando **solo** las apariciones que son repetidas, con ▾ y ‹ › ([prevDup]/[nextDup]) que recorren las repetidas en círculo. ‹P4:515-520›
- **REP-005 · MUST · Tarjeta de repetidos (desplegada).**
  - Una fila por aparición: icono, nombre · perfil, y «[editingNow]» con borde warn en la actual. Tocar una fila la abre.
  - 🗑 «[delFrom]»: dos toques (3,5 s) y deshacer. Si se borra la actual, se abre otra, preferentemente fuera de Siempre visible.
  - Consejo: [dupAdvG] si alguna está en Siempre visible, [dupAdvSame] si todas se llaman igual, [dupAdvDiff] en otro caso.
  - «[moveAlways]» (solo en los dos primeros casos): conserva la de Siempre visible, o mueve allí la actual, y borra las de los perfiles **que tengan el mismo nombre en algún idioma**. **Decisión:** no borra atajos con otro nombre (§5 DIS-49). Tiene deshacer y avisa [moved].
  - «[itsFine2]»: añade la clave a dupIgnored, con deshacer, avisa [dupKept] y pasa a la siguiente.
  ‹P4:521-546; script 1482-1491,1822-1830›
- **REP-006 · SHOULD · Cambiar la combinación desde un repetido.** «Cambiar la combinación» ([useOther]) guarda la combinación actual, vacía las teclas y muestra la franja «[replaceMsg]» con [keepOld], que la restaura y avisa [keptOld]. ‹P4:587-593; d05; DIS-33›
- **REP-007 · SHOULD · Rendimiento.** La detección de repetidos se indexa por clave canónica y escala a miles de atajos sin comparar todos contra todos. ‹NFR›

### 2.17 TAC · Filtro táctil, Precisión táctil y Modo prueba

- **TAC-001 · MUST · Presets.** Valores de antirrebote ms / área extra px / cancelar si deslizas px / contacto mínimo ms:
  - Estándar: 150/8/45/0.
  - **Temblor leve (por defecto):** 300/14/35/0.
  - Temblor fuerte: 600/24/28/80.
  - Personal: los valores actuales del usuario.
  Se aplica a todos los perfiles y vistas. ‹seed PRESETS; d03:105-109›
- **TAC-002 · MUST · Algoritmo único del filtro.** Por botón, al terminar un contacto:
  - (1) si el desplazamiento supera **cancelar si deslizas**, el toque no cuenta («ignorado: deslizaste», texto nuevo);
  - (2) si el contacto mínimo es mayor que 0 y la duración es menor, se ignora como [tShort];
  - (3) si el antirrebote es mayor que 0 y hubo un toque **aceptado** en ese mismo botón hace menos del antirrebote, se ignora como [tDouble];
  - (4) si no, se acepta y se guarda su hora.
  Los toques ignorados no reinician la ventana y los demás botones nunca se bloquean. **Área extra:** un toque en un hueco a menos del área extra activa el botón más cercano; si hay empate, el de centro más próximo. El filtro se evalúa antes que la edición, la confirmación y Alternar. En Mantener, el contacto mínimo retrasa el inicio del mantenido, y el antirrebote se aplica al inicio. **Acepta:** la misma tabla de entrada y salida da el mismo resultado en el panel, la barra, las ventanas al costado, la zona de prueba y el Modo prueba. ‹d03:99-104; AUD-37 (bloqueante); DIS-20›
- **TAC-003 · SHOULD · Respuesta ante un toque ignorado.** **Decisión:** un toque ignorado en uso normal no ejecuta nada, pero da una respuesta discreta (un leve contorno o vibración de 150 ms, sin sonido), para que quien tiene temblor sepa que no se envió. Se puede desactivar. ‹§6 PQ-12›
- **TAC-004 · MUST · Desplazar sin disparar.** En las zonas que se desplazan (cuadrícula de perfiles, ventanas al costado, CC), un gesto que supera cancelar si deslizas desplaza y no activa nada. ‹EC›
- **TAC-005 · MUST · Sección Precisión táctil del CC.** Título [touchTitle] y [touchSub]. 4 tarjetas de preset con nombre ([pStd]…) y descripción ([dStd]…), con selección también indicada sin color. Deslizadores con nombre, valor monoespaciado en accent, explicación y botones − / + de 44:
  - antirrebote: 0–1000, en pasos de 50;
  - área extra: 0–40, en pasos de 2;
  - cancelar si deslizas: 0–80, en pasos de 5;
  - contacto mínimo: 0–300, en pasos de 10.
  El 0 se muestra con [off] «Desactivado». Mover cualquier deslizador cambia el preset a Personal. Elegir Personal no cambia los valores. Los valores de un preset que no caen en un paso (Temblor fuerte con 28) se conservan exactos; − y + los redondean al paso más cercano. ‹P4:1040-1053; script 1672-1674; DIS-50›
- **TAC-006 · MUST · Zona de prueba.** Columna de 340 (se apila en ventanas estrechas).
  - Etiqueta [testZone].
  - **Varios** objetivos «Toca aquí» separados por un hueco, para comprobar también el área extra, con un área mínima de 200 de alto.
  - El fondo cambia a accentWash si se registra y a warnWash si se ignora.
  - Contadores Registrados e Ignorados, con botón para reiniciarlos.
  - Mensaje del último toque: [testIdle], [tOk], [tShort], [tDouble] o «deslizaste».
  - Usa **el mismo** filtro que TAC-002.
  ‹P4:1055-1063; DIS-51›
- **TAC-007 · SHOULD · Alcance del filtro fuera del panel.** **Decisión:** el filtro **no** se aplica al CC ni a la bienvenida, que son ventanas normales. Sus objetivos grandes y la confirmación en dos toques bastan. ‹§6 PQ-17›
- **TAC-008 · MUST · Modo prueba (30 s).**
  - **Al activarlo:** desde Ajustes rápidos; cierra la hoja y muestra el aviso fijo [tmStart].
  - **Durante 30 s:** todo toque pasa el filtro y **no se envía nada**, tampoco con Mantener, Alternar, teclas fijas ni Repetir. Cada toque marca la ficha 700 ms con ✓ en blanco sobre verde, o ⊘ sobre rojo, y avisa [tmOk], [tShort] o [tDouble]. Los aceptados cuentan para el antirrebote.
  - **Indicador:** una marca permanente («Modo prueba · {s} s») sigue visible aunque otro aviso sustituya a [tmStart].
  - **Al terminar:** se apaga solo con [tmEnd]. Apagarlo a mano quita el aviso.
  - Funciona también en la Pestaña (PES-014).
  ‹P4:214; script 1441-1447,1569; d03:110; DIS-20›

### 2.18 CCM · Centro de control: marco

- **CCM-001 · MUST · Ventana.** Ventana normal que se puede activar, mover y redimensionar: 1120×680 por defecto, mínimo 760×520, y recuerda su tamaño.
  - **Barra de título** (52, fondo side): logotipo, «Clícalo › [cc] › {sección}» (la sección se oculta por debajo de 1240 de ancho), selector ES/EN siempre visible y ✕ (visual 44×40, táctil 44; [close]; tooltip [closeEsc]).
  - **Al cerrar:** se descarta el borrador vacío.
  - **Pantallas pequeñas:** al 150 % en 1366×768, el mínimo efectivo baja al área de trabajo disponible y el contenido se desplaza verticalmente, nunca en horizontal.
  ‹P4:390-401; d01:60; d05:3; AUD-18›
- **CCM-002 · MUST · Menú lateral.** Secciones en este orden, con un separador antes de Sistema:
  1. Atajos (tune, con el contador warn de repetidos);
  2. Plantillas (auto_awesome);
  3. General y panel (display_settings);
  4. Precisión táctil (touch_app);
  5. Sistema (verified_user, contador 1 si hay actualización);
  6. Acerca de y contacto (favorite).
  Elementos de 46 visual (44 táctil) con texto de 15 en negrita. El seleccionado lleva fondo accentWash y texto accent, **y** estado de selección accesible. Por debajo de 1240 de ancho, el menú mide 72 y muestra solo iconos, con el contador en la esquina y los nombres accesibles completos. ‹P4:402-411; d05:12›
- **CCM-003 · MUST · Barra de estado.** Franja inferior de 48 como mínimo con icono y el último mensaje. En reposo muestra [saved]. [undo] (táctil 44) aparece si el mensaje se puede deshacer y la pila no está vacía; deshacer avisa [restoredU]. Duraciones como AVI-002. Los mensajes del panel también se ven aquí, porque el estado de mensajes es compartido. ‹P4:1223-1227›
- **CCM-004 · MUST · Relación con el panel.** **Decisión:** mientras el CC está abierto, el panel sigue visible y encima, no se atenúa y no tapa los controles del CC: si se solapan, el CC se abre desplazado. Al cerrar el CC, el foco vuelve a la app que estaba en primer plano antes de abrirlo. ‹DIS-39; §6 PQ-16›
- **CCM-005 · MUST · Diseño adaptable.** Umbral de 1240 px. Por debajo, las columnas se apilan (General, Plantillas, Acerca de) o se estrechan (Atajos: 180/flexible/400 pasa a 150/flexible/340). Nada se corta en el ancho mínimo. ‹d05:24; EC-CC›

### 2.19 ATJ · Centro de control › Atajos (perfiles, vinculación y biblioteca)

- **ATJ-001 · MUST · «Tu panel muestra».** Barra superior con «[panelShows]» y chips unidos por «+»:
  - 📌 Siempre visible (n);
  - el perfil de la app activa o [noProf], con «n · [activeApp]»;
  - ★ Frecuentes [autoW].
  Chip de repetidos (REP-003). ‹P4:415-425; script 1610›
- **ATJ-002 · MUST · Columna de perfiles.** 📌 [always] con [allApps] y cada perfil en orden, con icono y proceso en monoespaciada, o [noProcess]. Filas de 52, la seleccionada con fondo cardHi. Elegir uno selecciona el perfil y su primer atajo. Al final, «+ [newProfile]», que abre Plantillas. ‹P4:427-436›
- **ATJ-003 · MUST · Cabecera de la lista.** Icono de 40, nombre de 18, ✏ de 44 ([editProf], salvo en Siempre visible) y el subtítulo [globalSub], «[opensWith] {proceso}» o [manualSub]. Botón accent «+ [add]», que abre la biblioteca. ‹P4:437-445›
- **ATJ-004 · MUST · Editar un perfil.** La tarjeta de edición tiene:
  - nombre de 44 con 🎤, que se guarda igual en ES y EN;
  - rejilla de iconos de 40 visual (44 táctil): el actual más 28 de base;
  - fila con interruptor Modo compatible ([compatT]/[compatD]), tocable completa;
  - [shareProf];
  - [done];
  - [delProf], salvo en General: dos toques, deshacer y aviso [profDeleted] (PER-008).
  Todo cambio (nombre, icono, compatible) se puede deshacer. Un nombre vacío no se acepta: vuelve al anterior. ‹P4:446-458; script 1865-1876; DIS-34›
- **ATJ-005 · MUST · Vinculación: estados.** Salvo en Siempre visible:
  - General: [genLinkT]/[genLinkS], sin acción.
  - Esperando: [waitingT]/[waitingS] con Cancelar, sobre accentWash.
  - Vinculado: «[linkedT]» + proceso y [change].
  - Sin vincular: [unlinkedT]/[unlinkedS] en warn, con [linkBtn].
  ‹P4:459-470›
- **ATJ-006 · MUST · Vinculación: opciones.** Al desplegar:
  - [linkOpenApps]: chips con el nombre y el proceso de cada app abierta, marcando la vinculada;
  - [linkDetect]: modo captura;
  - [linkNone]: solo manual, con aviso [linkRemoved].
  Todo cambio tiene deshacer. ‹P4:471-477; script 1878-1882›
- **ATJ-007 · MUST · Proceso único.** Un perfil puede vincular varios procesos, pero **cada proceso** pertenece a un solo perfil. Vincular un proceso ya usado por otro perfil pide confirmación (textos nuevos: «{proceso} ya abre «{p}». ¿Pasarlo a este perfil?») y lo quita del anterior, que conserva sus demás procesos, con deshacer. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): la unicidad pasa a ser por proceso (PQ-45), porque un perfil puede vincular varios. ‹d02:79; DIS-32; D2›
- **ATJ-008 · MUST · Modo captura.** Muestra el aviso fijo [waitingApp] **con Cancelar en el panel**. La siguiente app válida en primer plano queda vinculada (su proceso se añade a los del perfil): no cuentan las ventanas de Clícalo, el shell, el teclado táctil ni el cambio de «Probar ahora». Aviso «[linkedTo] {proceso}». Si la app deseada ya está en primer plano detrás del CC, la lista [linkOpenApps] la ofrece marcada «activa». **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): la captura añade un proceso a los del perfil en lugar de sustituir el único que había, porque un perfil puede vincular varios (PQ-45). ‹P4 script 1498-1499; d03:8; DIS-16; D2›
- **ATJ-009 · MUST · Cuadrícula del perfil.**
  - **Fichas:** 88 de alto y 96 de ancho como mínimo, con icono de 26, nombre de 13 y teclas (o «n pasos»).
  - **Indicadores:**
    - ⚠ si está repetida;
    - icono del tipo si no es Pulsar;
    - número de voz;
    - «[incomplete]» si no tiene nombre, si es Pulsar, Mantener o Alternar sin teclas ni mouse, si es Texto vacío, Web o App vacíos o no válidos, o si es Macro sin pasos o con un paso de teclas vacío.
  - **Seleccionada:** fondo accentWash y borde accent de 2.
  - **Reordenar:** arrastrando, incluido soltar al final, con deshacer.
  - **Última ficha:** «[fromLib]».
  - **Nota:** [orderHint], con el texto corregido para que diga que se reordena con los botones o arrastrando.
  ‹P4:478-491; DIS-28; DIS-52›
- **ATJ-010 · MUST · Biblioteca «Añadir atajo».**
  - **Cabecera:** [addTitle] con ✕ (táctil 44).
  - **Tarjeta accent «[createOwn]»** ([createOwnD]): crea un borrador en blanco (bolt, autoIcon) y abre el editor, con aviso [newCreated].
  - **Separador:** «[orPick]».
  - **Categorías** en chips de 44: «[lcFor] {perfil}» (primera y por defecto si el perfil tiene plantilla y no es Siempre visible), [lcEdit], [lcWin], [lcMouse], [lcVoice], [lcText] y [lcSys].
  - **Filas de 56:** icono, nombre, teclas y ⊕, o ✓ si ya está: mismo origen de catálogo, misma clave, misma acción de mouse o mismo texto. **Decisión:** «mismo origen» se identifica por una referencia de catálogo, no por prefijo de id.
  - **Tocar:** añade al final con un id nuevo, deshacer y aviso «[addedTo] {perfil}: {nombre}». La biblioteca sigue abierta.
  ‹P4:494-513; script 1606-1608,1918-1923; AUD-27›
- **ATJ-011 · MUST · Borrador.** Un atajo sin nombre en ningún idioma, sin teclas, de tipo Pulsar y sin mouse se descarta en silencio, sin dejar rastro en la pila de deshacer, al seleccionar otro atajo o perfil, al abrir la biblioteca o al cerrar el CC. ‹d02:80; AUD-16›

### 2.20 EDI · Editor de atajos

- **EDI-001 · MUST · Vista previa e identidad.**
  - Ficha de vista previa de 84 con un lápiz accent: muestra el icono y el nombre, o [namePh2]. Al tocarla abre o cierra el selector de iconos, y queda marcada con borde accent.
  - Nombre: campo de 44 con [namePh], con el cursor dentro si está vacío.
  - 🎤 [dictName] de 44, con aviso [dictNameT].
  - Línea [iconAuto] o [iconManual].
  ‹P4:548-562›
- **EDI-002 · MUST · Nombre en los dos idiomas.** Escribir el nombre de un atajo creado por el usuario lo guarda en ES y EN a la vez. **Decisión:** si el atajo viene de un catálogo con traducciones distintas, editarlo sobrescribe los dos idiomas con lo escrito. ‹d02:54; d07:51; DIS-29›
- **EDI-003 · MUST · Icono automático.** Mientras autoIcon está activo, cada cambio de nombre pone la primera sugerencia, si la hay. Elegir un icono a mano desactiva autoIcon. ‹P4 script 1649-1650›
- **EDI-004 · MUST · Selector de iconos.**
  - Hasta 6 sugeridos de 44, calculados con suggestIcons.
  - Buscador 🔍 con [iconSearch]: busca en las etiquetas ES y EN y en el nombre del icono, sin distinguir tildes.
  - Rejilla de iconos de 40 visual (44 táctil) con desplazamiento de 180. Sin búsqueda, muestra los 24 de ICONS y después el resto de ICONLIB.
  - El elegido se marca con accentWash, borde accent y estado accesible.
  ‹P4:563-573; script 1346-1347,1647›
- **EDI-005 · MUST · Algoritmo suggestIcons(nombre, teclas).**
  1. Minúsculas y sin diacríticos.
  2. Palabras alfanuméricas de más de 2 letras.
  3. Si la combinación está en el mapa combinación→icono **del idioma de las apps**, ese icono va primero.
  4. Se recorre ICONLIB en orden y se añade cada icono sin repetir si alguna etiqueta x empieza por la palabra w, o si w empieza por x y x tiene más de 3 letras.
  5. Máximo 6.
  El icono de un perfil nuevo sin sugerencia es `apps`; el de un atajo nuevo, `bolt`. ‹P4 script 1343-1346,1883; DIS-53›
- **EDI-006 · MUST · Tipo de acción.**
  - **Tipos:** [tTap], [tHold], [tToggle], [tText], [tMouse], [tMacro], [tUrl] y [tApp], en una rejilla de 4×2 con fichas de 56 e icono. El elegido lleva accentWash, borde de 2 y estado accesible. Debajo, la descripción [dTap…].
  - **Al cambiar de tipo:**
    - si el nuevo tipo no es Mouse, se borra la acción de mouse;
    - Mouse sin acción empieza con clic derecho.
  - **Decisión:** los demás tipos empiezan **vacíos**, con un ejemplo solo como texto guía, y quedan «Incompleto» hasta rellenarlos. No se ponen valores reales como «ejemplo.com», «notepad.exe» o [Ctrl].
  - **Macro:** empieza sin pasos y ofrece los botones de añadir.
  ‹P4:575-581; script 1590; DIS-54›
- **EDI-007 · MUST · Recuadro de combinación.**
  - **Contenido:** la franja [replaceMsg] con [keepOld] si procede, [recording] mientras se graba, [comboEmpty] si está vacío, o las fichas de tecla en orden de pulsación unidas con «+», cada una con su × (táctil 44).
  - **Pie:** [comboN] (con plural) o [comboNone], ⌫ [backKey] y ↺ [clearKeys]. Limpiar tiene deshacer y avisa [comboCleared]. Ambos se desactivan si no hay teclas.
  - **Avisos:** combinación bloqueada (rojo, icono block, [blockedB], con la alternativa si existe) o especial (warn, icono info, [blockedS]).
  - Al editar un paso de macro, se muestra [stepEditMsg] con [done].
  ‹P4:583-609; script 1592-1600,1841-1842›
- **EDI-008 · MUST · Selección de teclas.**
  - Modificadores Ctrl, Alt, Shift y Win (44).
  - Grupos [kgSides], [kgLetters] (con Ñ), [kgNums], F1–F12, [kgSpecial], [kgNumpad] y [kgMedia] (con F13–F24).
  - Rejilla de 7 columnas para letras y números, 6 para F1–F12 y celdas de 92 como mínimo con el nombre completo en el resto. Todas las celdas y los grupos, de 44 táctil.
  - Tocar una tecla la añade al final; si ya está, la quita. La elegida lleva accentWash, borde accent y estado accesible.
  - Nota [orderHint2].
  - El catálogo incluye además ` \ [ ] ' #. Se conservan aunque MIG-005, que los pedía para la v1, está retirado (D1, §6.2).
  ‹P4:610-613; seed KEYG›
- **EDI-009 · MUST · Lado del modificador.** Cada modificador elegido admite Cualquiera, Izquierda o Derecha. En Alt: «Alt izq.» o, según la distribución, «AltGr» / «Alt der.». Etiquetas: «Ctrl izq.» en ES y «Left Ctrl» en EN. El lado forma parte de la clave canónica y se envía tal cual. **Decisión:** existe un solo mecanismo para el lado (un atributo de la tecla); las teclas con lado del grupo Izq./Der. son atajos de ese mismo mecanismo. Cambiar las teclas no borra en silencio el lado de los modificadores que siguen. ‹P4 script 1389-1395,1832; DIS-55›
- **EDI-010 · SHOULD · Grabar con teclado.** «[recPhys]» con [recHint]. Solo se ve si el usuario no marcó «No puedo usar el teclado», o si ya se está grabando.
  - Pulsar solo modificadores no termina la grabación. La primera tecla que no es modificador la cierra con los modificadores activos más esa tecla, **en orden de pulsación** y con su lado.
  - Esc cancela. Aviso «[recorded]: {teclas}».
  - La observación del teclado solo está activa mientras se graba.
  - **Decisión:** para grabar Esc o combinaciones con Win que Windows intercepta, se usa el selector.
  ‹P4:614; script 1407-1416; DIS-56›
- **EDI-011 · MUST · Campo de Texto.** Área de 3 líneas ([textLabel]) con 🎤 y la nota [textHint]. Aviso de privacidad [textEnc]. ‹P4:617-622; DIS-25›
- **EDI-012 · MUST · Campo de Mouse.** 8 acciones en 2 columnas de 48 con icono. En los desplazamientos, velocidad [spSlow], [spNormal] o [spFast] (táctil 44). ‹P4:623-629›
- **EDI-013 · MUST · Pasos de macro.**
  - **Cada paso:** número, icono, descripción («Pulsar Ctrl + C», «Esperar 0,5 s» con coma en ES y punto en EN, «Escribir «…»» o la acción de mouse), ✏, ↑ ↓ (desactivados en los extremos) y ✕.
  - **Borrar un paso:** dos toques y deshacer (REG-04).
  - **Edición de cada tipo:**
    - Teclas: recuadro de combinación.
    - Espera: − / + de 100 ms, de 100 a 10 000.
    - Texto: área con 🎤, cifrada como los Texto.
    - Mouse: chips.
  - **Añadir:** «+ [stKeys]», «+ [stWait]» (500 ms), «+ [stText]» y «+ [stMouse]» (clic derecho). El paso nuevo queda en edición.
  - Todos los pasos tienen `kind` válido.
  ‹P4:630-649; script 1835-1841; DIS-35›
- **EDI-014 · MUST · Web y App.**
  - Campo monoespaciado con [url] o [appPath] y 🎤.
  - Chips [openTabs] o [pickProgram]. «Elegir programa» lista las apps instaladas y abiertas reales, incluidas las de la Tienda. «Páginas abiertas» es COULD (§6 PQ-31).
  - Validación de Web: se admiten http y https, dominios con ñ, localhost, IP, puertos y parámetros. Si no es válida, borde warn y [badUrl].
  ‹P4:650-657; script 1847-1848; DIS-57›
- **EDI-015 · MUST · Fijar en Siempre visible.** Fila completa tocable con 📌, [pinAll2], la descripción [pinAllOn2]/[pinAllOff2] y un interruptor (Toggle).
  - **Activar:** **mueve** el atajo al final de Siempre visible y recuerda su perfil de origen.
  - **Desactivar:** lo devuelve al origen si existe; si no, al perfil mostrado; si no, a General.
  Deshacer y aviso [saved]. ‹P4:659-663; script 1477-1481; DIS-30›
- **EDI-016 · MUST · Más opciones** ([moreOpts], plegable, con «[position] i/N»):
  - **Posición:** [first], [before], [after] y [lastPos] de 52, desactivados cuando no aplican, con deshacer.
  - **[autoRelease]:** 30 s, 1 min, 2 min, [never] o «Como en General» (por defecto), con [autoReleaseD] corregido para que no prometa soltar siempre al cambiar de app. Solo en Mantener y Alternar.
  - **[textMethod]:** [tmType] o [tmPaste], con 🔒 [textEnc]. Solo en Texto.
  - **Número de voz:** el número y «[voiceLine1] «clic n» [voiceLine2] «clic {nombre}»», o [voiceOff].
  - **Plegable «[voiceHowT]»:** [vh1], [vh2] y [vh3], adaptado a Windows 10 (§6 PQ-29).
  ‹P4:665-682; script 1843-1845›
- **EDI-017 · SHOULD · Pedir confirmación.** Interruptor «Pedir confirmación antes de ejecutar» (`confirm`) en Más opciones. Hoy solo lo traen la semilla y las plantillas. Necesita textos nuevos. ‹§6 PQ-32›
- **EDI-018 · SHOULD · Frecuentes desde el editor.** Interruptor «Fijado en Frecuentes» en Más opciones, que da acceso a los atajos Mantener (CUA-015). ‹AUD-38›
- **EDI-019 · MUST · Pie del editor.** Tres botones de 48 en proporción 1,2 : 1 : 1,2:
  - [Probar] (principal, play_arrow): abre y cierra la tarjeta Probar.
  - [Duplicar]: inserta la copia justo detrás, con [copySuffix] en cada idioma y un id nuevo; la selecciona y avisa con deshacer.
  - [Eliminar]: dos toques (warning, «Confirmar», rojo, 3,5 s) y [deleted] con deshacer; después selecciona el primero de la lista o el estado vacío.
  Armar Eliminar y cambiar de atajo lo desarma. ‹P4:720-724; script 1787,1818,1851›
- **EDI-020 · MUST · Estado vacío del editor.** Sin atajo seleccionado ni biblioteca abierta, se muestra [pickOne] centrado. ‹P4:726›
- **EDI-021 · MUST · Ediciones y deshacer.** Las ediciones seguidas de un atajo (nombre, icono, tipo, teclas, texto u opciones) forman un solo paso de deshacer, que se cierra al pasar a otro atajo. El aviso dice «Deshacer cambios en {nombre}» (texto nuevo). ‹d02:81; AUD-15›

### 2.21 PRB · Probar

- **PRB-001 · MUST · Tarjeta Probar.**
  - Cabecera [testTitle] con ✕ de 44.
  - Secuencia mostrada como teclas unidas con «+», o con «→» en las macros.
  - [playSeq] «Ver» (44) y la frase [tw*] del tipo.
  - Línea de fase con icono.
  ‹P4:684-698›
- **PRB-002 · MUST · Animación por tipo.**
  - **Pulsar:** una tecla cada 260 ms; a los 500 ms, [phReleasedAll].
  - **Mantener:** [phTouch]; teclas cada 260 ms; +200 ms [phHolding]; +1600 ms [phLift]; +900 ms [phDone].
  - **Alternar:** [phTap1]; teclas cada 260 ms; +200 ms [phLatched]; +1600 ms [phTap2]; +500 ms [phReleased].
  - **Macro:** [phStep] cada 650 ms; +700 ms [phDone].
  - **Texto, Web, App y Mouse:** su frase y +1100 ms [phDone].
  Con reducir movimiento, salta directamente al final. Volver a reproducir cancela la anterior. ‹P4 script 1798-1814›
- **PRB-003 · MUST · «Probar en».** Chips (44) con las apps abiertas. Por defecto, la **última app en primer plano distinta de Clícalo**. La elección se mantiene entre atajos. Si no hay ninguna app, texto nuevo «Abre la app donde quieras probarlo». ‹P4:699-700; script 1803›
- **PRB-004 · MUST · Probar ahora.** Botón de 48 con «[testLive2]» y la explicación [testHow].
  1. Se oculta el CC, se activa la app, aparece el aviso fijo [switching] y a los ~0,9 s se envía la acción.
  2. A los ~2,6 s vuelve el CC con el mismo perfil y atajo, y la pregunta [testAskQ].
  3. [yesWorked] responde [testOk]; [noWorked] muestra [tipsTitle] con tip1–tip4.
  - Mantener se mantiene 1,5 s y se suelta.
  - Alternar se activa y se desactiva a los 1,5 s.
  - Un atajo con confirmación pide confirmarlo en el CC antes de probar.
  - Un atajo bloqueado no se prueba.
  ‹P4:701-717; script 1816-1817; §6 PQ-33›
- **PRB-005 · MUST · Cancelar la prueba.** Cerrar la tarjeta o seleccionar otro atajo cancela la animación, los temporizadores y la pregunta. ‹P4 script 1587,1798›
- **PRB-006 · MUST · Efectos laterales de la prueba.** La prueba no cuenta para Frecuentes, no cambia la última acción de Repetir, no vincula un perfil que está en modo captura, no dispara el soltado por cambio de app y no cambia el perfil del panel. ‹EC-PRB›
- **PRB-007 · MUST · Destinos especiales.**
  - Destino elevado: se muestra el aviso de administrador y no la pregunta.
  - Destino cerrado: aviso de que la app ya no está abierta.
  - Si Windows no deja recuperar el primer plano, el CC se señala en la barra de tareas y la pregunta sigue esperando.
  ‹EC-PRB›

### 2.22 PLA · Plantillas e IA

- **PLA-001 · MUST · Estructura.** Contenido y vista previa de 340 (260 por debajo de 1240). Título [tplTitle] y subtítulo [tplSub2]. Orden: Crear con IA, Perfil vacío, Apps abiertas sin perfil, Plantillas disponibles, Tus perfiles. ‹P4:890-895›
- **PLA-002 · SHOULD · Crear con IA.**
  - Tarjeta con borde accent: [aiHero] y [aiHeroD], campo de 52 con [aiPh], 🎤 de 52 y [Generar con IA], que pasa a «Generando…». **Decisión:** Generar está desactivado con el campo vacío.
  - Chips Photoshop, Spotify, Teams, Canva, WhatsApp y OBS (44), que solo rellenan el campo.
  - Línea de privacidad [aiPrivacy], corregida para listar los 4 datos que se envían.
  ‹P4:896-906; DIS-58›
- **PLA-003 · SHOULD · Cuota y clave propia.** «IA gratis: {n} de 5 hoy» ([quotaFree]) o [quotaKey]. [keyUse]/[keyChange] despliegan el campo de clave: oculto, con botón **Pegar**, [keyPh] y [Listo]. La clave nunca vuelve a mostrarse en claro. La cuota se reinicia a medianoche local y **solo se descuenta si la generación tiene éxito**. ‹P4:904-906; d08:32; DIS-59›
- **PLA-004 · SHOULD · Consentimiento.** Antes del primer intento se muestran [consentT] y [consentD] con [Aceptar y generar] o [No usar IA], que desactiva la IA y lleva al error «off». En General › (sección IA, textos nuevos) se puede revocar y volver a dar el consentimiento, y desactivar la IA. ‹P4:907-913; AUD-32›
- **PLA-005 · SHOULD · Orden de comprobación al generar.**
  1. IA desactivada → «off».
  2. Sin consentimiento → pedirlo.
  3. Sin conexión, o 15 s sin respuesta → «offline».
  4. Sin cuota ni clave → «limit».
  5. Servicio caído → «unavailable».
  6. Respuesta inválida → «invalid».
  7. Clave rechazada → «badkey».
  8. Si no, resultado en la vista previa.
  ‹P4 script 1924-1931; d08:89›
- **PLA-006 · SHOULD · Errores de la IA.** Tarjeta amarilla con icono, título y descripción, y tres salidas:
  - offline: [Reintentar] (**repite la misma petición**) · [Perfil vacío] (con el nombre ya puesto) · [Ver plantillas];
  - limit: [Usar mi clave] · Perfil vacío · Ver plantillas;
  - off: [Activar IA] · Perfil vacío · Ver plantillas;
  - unavailable, invalid y badkey (textos nuevos): Reintentar o Cambiar clave · Perfil vacío · Ver plantillas.
  Los errores se anuncian como *assertive*. ‹P4:914-920; AUD-29; DIS-60›
- **PLA-007 · SHOULD · Programa desconocido.** Si la respuesta dice `known:false`, la vista previa muestra [unknownMsg] con [unknownBlank]: crea el perfil vacío en modo captura y abre la biblioteca. El proceso nunca se supone como «nombre.exe»: sale de la captura o de la elección del usuario. ‹P4:1008-1013; script 1887›
- **PLA-008 · MUST · Contrato de la IA.** Solo se envían el nombre de la app, la distribución de teclado, el idioma de los programas y el idioma de la interfaz. La respuesta se valida contra un esquema y **solo puede proponer combinaciones de teclas** (Pulsar), sin Web, App, Macro ni Texto. Cualquier otra cosa se rechaza como «invalid». El proveedor se puede cambiar sin tocar la interfaz. ‹d08:28-37; AUD-60›
- **PLA-009 · MUST · Línea de teclado.** Botón-fila «Para {distribución} · {idioma de programas}» ([kbFor]) con [kbWhy] y Cambiar/Listo.
  - Distribuciones: Español (Latinoamérica), Español (España), Inglés (EE. UU.) e Inglés internacional.
  - Programas: en español o en inglés.
  - La opción detectada lleva [detected]. El idioma de los programas decide qué variante se instala.
  ‹P4:921-931; script 1660-1664›
- **PLA-010 · MUST · Perfil vacío.**
  - Sección plegable: cabecera de 60 o más con [blankTitle], [blankSub] y ▾/▴, patrón ExpandCollapse.
  - Icono de 48 con ✏, que abre la rejilla del actual más 28. El icono se propone según el nombre hasta que se elige a mano.
  - Nombre [blankPh] con 🎤.
  - «Se activa con» [blankLink]: apps abiertas, [linkDetectShort] o [linkNoneShort] (por defecto).
  - [blankCreate], desactivado sin nombre: crea el perfil con el mismo nombre en ES y EN, al final del orden, con aviso [profCreated] y deshacer, y abre Atajos con la biblioteca. Con Capturar, pasa a ATJ-008.
  ‹P4:933-948; script 1883-1886›
- **PLA-011 · MUST · Apps abiertas sin perfil.**
  - Cabecera con punto amarillo, [tplSuggested], [asShort] y un interruptor (autoSuggestProfiles, compartido con el panel, fila tocable).
  - Por cada app abierta con plantilla local y sin perfil: tarjeta amarilla con «[sugLine]» (con plural), 6 iconos, [Vista previa] e [Instalar].
  - Si no hay ninguna: [noSugOn] o [noSugOff].
  - Con Detectar desactivado, esas plantillas aparecen en Plantillas disponibles.
  ‹P4:952-971; DIS-61›
- **PLA-012 · MUST · Plantillas disponibles.** Tarjetas de 200 o más con icono, nombre, «N atajos» y 6 iconos. Tocar la tarjeta la abre en la vista previa, con borde accent. [Instalar] instala todo sin vista previa, con deshacer. Si no queda ninguna: [allInstalled]. ‹P4:972-989›
- **PLA-013 · MUST · Instalar.** Instala los marcados (por defecto todos) con id nuevo, el nombre editado en ES y EN, y la variante del idioma de los programas si existe. Crea el perfil con el nombre, icono y **procesos** de la plantilla, al final. Si alguno de sus procesos ya está vinculado, se aplica ATJ-007 a ese proceso. Si ya existe un perfil con ese id de plantilla, **nunca** se sobrescribe: se ofrece «Añadir los que faltan». Aviso [installedT] con deshacer. Después, PER-007. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): el perfil recibe **todos** los procesos de la plantilla (antes, uno) y la unicidad se aplica por proceso. ‹P4 script 1492-1496; EC; D2›
- **PLA-014 · MUST · Tus perfiles e importar.** Título [tplInst] con el recuento. Botones de 48 por perfil (incluido General; no Siempre visible) con nombre, número de atajos y ›, que abren Atajos. [importProf] con la nota [shareHint]: el perfil importado se abre en la vista previa **sin instalar** ([importedPv]). ‹P4:990-999›
- **PLA-015 · MUST · Vista previa.**
  - **Cabecera:** vacía, icono y [pvEmpty]. Con contenido: icono, nombre, proceso monoespaciado recortado, [instNoteAll]/[instNoteSome], línea de teclado y [onlyEs] si los programas están en inglés y la plantilla no tiene variantes.
  - **Filas de 48 o más:**
    - casilla (marcada por defecto), icono, y nombre y teclas en dos líneas;
    - las que ya están ([alreadyIn], ✓) se ven atenuadas y no se pueden cambiar;
    - ✏ [rename] edita el nombre en el sitio con 🎤 y foco, y un nombre vacío vuelve al original.
  - **Acciones de riesgo:** las Web, App, Macro y Texto de un perfil importado se marcan con un aviso y se desmarcan por defecto.
  ‹P4:1002-1029; LOG-008›
- **PLA-016 · SHOULD · Editar las teclas en la vista previa.** Cada fila permite cambiar las teclas con el recuadro de combinación antes de instalar. ‹AUD-30; DIS-24›
- **PLA-017 · MUST · Botón final de la vista previa.** N = marcados que faltan. Etiquetas:
  - «Instalar N atajos»;
  - «[createWith]» si viene de la IA;
  - «[addMissing]», que aplica nombres y variantes como PLA-013;
  - «[editShortcuts]» (secundario) si está instalada y completa.
  Desactivado con N = 0. ‹P4:1030; script 1891-1894; DIS-62›

### 2.23 GEN · General y panel

- **GEN-001 · MUST · Disposición.** Título [panelTitle] y [panelSub]. Cuadrícula de 2×2 con tarjetas de igual alto en este orden: Idioma, Tema, Tamaño, Vista. Debajo, dos columnas desde 1240 de ancho y una por debajo. A la izquierda, Disposición y Transparencia. A la derecha, Modo pestaña, Confirmación al tocar, Seguridad de teclas y Primeros pasos. ‹P4:733-759; DIS-63›
- **GEN-002 · MUST · Idioma.** «Idioma · Language», con dos botones de 64 o más (ES/Español, EN/English). Se aplica al instante. ‹P4:739-740›
- **GEN-003 · MUST · Tema.** 4 tarjetas con muestra (fondo, superficie y acento): [themeAuto], Oscuro, Claro y Alto contraste. ‹P4:741-750›
- **GEN-004 · MUST · Tamaño y texto.** Tarjetas S, M y L ([sizeS/M/L]) con miniatura. Elegir una pliega la barra. «[textSize]» con − / + de 44: 100–150 % en pasos de 10. ‹P4:751-753›
- **GEN-005 · MUST · Vista.** 3 miniaturas con la descripción [dFullD]/[dCompactD]/[dDockD]. El cambio aplica PAN-001(c). ‹P4:754-757›
- **GEN-006 · MUST · Filas visibles.** Tarjetas Auto ([rAuto2]; su miniatura dibuja 2 filas en S y 3 en M/L), 1, 2 y 3, con [rowsVisD]. Cambiar reinicia la paginación. ‹P4:760-770›
- **GEN-007 · MUST · Interruptores de Disposición.** [followApp/D] (el mismo estado que Auto/Fijo, PER-006), [showStripT/D] y [showTabsT/D], este último con una nota de cómo llegar a Frecuentes sin selector (SEL-006). ‹P4:771-777›
- **GEN-008 · MUST · Columnas y Mostrar teclas.** Columnas: 2, 3 o 4 con miniatura; cambiar reinicia la paginación. Mostrar teclas: tarjetas «Con teclas» y «Solo nombre», con textos que pasan a los archivos de idioma. ‹P4:778-798›
- **GEN-009 · MUST · Transparencia.**
  - Opacidad: − / + (±10) y deslizador de 30–100 en pasos de 5, con vista previa sobre «[behind]».
  - [autoDim/D].
  - [dimLevel]: 10–80 en pasos de 5, con − / +. **Decisión:** la opacidad efectiva al atenuar es min(dimTo, opacidad).
  ‹P4:801-826; DIS-64›
- **GEN-010 · MUST · Modo pestaña.**
  - [barExplain].
  - Lado: Izquierda, Arriba, Abajo y Derecha, con miniatura que refleja [gutter]. Elegir lado desde aquí **no** cambia la vista.
  - [handlePos/D] con dos botones de 44: ↑/↓ en lados verticales o ←/→ en horizontales, ±10 dentro de 8–92, desactivados en el límite.
  - Bloquear posición (PES-003).
  - [dockCount]: 4, 5, 6 u 8 (reinicia la página de la barra).
  - [autoHide].
  - [gutter].
  ‹P4:830-860›
- **GEN-011 · MUST · Confirmación al tocar.** [fbSound/D] y [fbFlash/D], activos por defecto. ‹P4:863-870›
- **GEN-012 · MUST · Seguridad de teclas.** [safeMax] (30 s, 1 min por defecto, 2 min o Nunca) con [safeMaxD], y [safeSwitch/D] (activo). «Nunca» no desactiva Soltar todo ni el soltado por eventos del sistema. ‹P4:873-875›
- **GEN-013 · MUST · Reducir movimiento y Reiniciar Frecuentes.** [reduceM/D] (TEM-006) y [resetFreq/D] (FRE-004). **Decisión:** se reubican en una tarjeta «Accesibilidad y datos», en lugar de quedar bajo Seguridad de teclas. ‹P4:876-877; §6 PQ-34›
- **GEN-014 · MUST · Primeros pasos.** [seeWelcome/D] cierra el CC y abre la bienvenida en el paso 0. Incluye además «Ver la guía de la pestaña» (PES-015) y el ajuste «No puedo usar el teclado» (BIE-005). ‹P4:879-884›
- **GEN-015 · SHOULD · Sección IA.** Estado del consentimiento, activar o desactivar la IA, clave propia y cuota (PLA-003/004). Textos nuevos. ‹AUD-32›

### 2.24 SIS · Sistema: pestañas e Inicio y estabilidad

- **SIS-001 · MUST · Pestañas.** [sysTitle] y [sysSub]. Tres pestañas (Tab/TabItem): Actualizaciones (por defecto), Copias de seguridad e Inicio y estabilidad. Cada una muestra su estado:
  - «v{ver} · al día» o «Nueva versión» en amarillo con new_releases;
  - «Última: {fecha}», o el texto nuevo «Sin copias aún»;
  - «Inicia con Windows» o «Inicio manual».
  La pestaña activa lleva el fondo del contenido, una barra accent de 3, el icono destacado y ▴. ‹P4:1070-1082›
- **SIS-002 · MUST · Inicio y estabilidad.** Filas con interruptor, en este orden: [rStart/D], [rAdmin/D], [rSingle/D] y [rCrash/D], este último con el texto alineado entre ES y EN.
  - [rAdmin] relanza elevado. Si se cancela el UAC o no hay credenciales, el interruptor vuelve a apagado con una explicación.
  - Iniciar con Windows y como administrador a la vez: arranca elevado **sin** pedir UAC en cada inicio.
  ‹P4:1159-1167; d08:56-62; AUD-49›
- **SIS-003 · MUST · Instancia única.** Una sola instancia por **sesión de usuario**. Una segunda ejecución entrega sus argumentos a la instancia en marcha, que se muestra. Ver PQ-35 sobre desactivarlo. ‹d01:64-65; EC›
- **SIS-004 · MUST · Recuperación automática.** Tras un fallo, la app se vuelve a abrir, suelta las teclas y, si el documento está dañado, restaura la última copia válida con un aviso. ‹d08:8-10›
- **SIS-005 · SHOULD · Convivencia con Macro Quick Access.** Si detecta Macro Quick Access en ejecución o en el inicio de Windows, ofrece cerrarlo y desactivar su inicio, para evitar doble inyección. ‹§6 PQ-36›

### 2.25 ACT · Actualizaciones

- **ACT-001 · SHOULD · Estados.** Tarjeta con icono, título, subtítulo y botón de 48:
  - Al día · v{x}, «Última comprobación» → [Buscar actualizaciones];
  - Buscando (inactivo);
  - Nueva versión · v{x}, con el subtítulo de copia previa → [Instalar ahora];
  - Instalando, con progreso;
  - Actualizado · v{x} → [Listo], con su aviso;
  - **Error** (textos nuevos: descarga dañada, firma inválida, sin espacio, sin conexión, interrumpida) → Reintentar.
  El contador 1 aparece en Sistema mientras haya versión nueva. ‹P4:1084-1091; script 1910-1917›
- **ACT-002 · SHOULD · Preferencias.** [updAuto/D], [updAsk/D] y [updBackup/D], activas por defecto. Canal Estable (por defecto) o Beta, con [channelD]. ‹P4:1093-1104›
- **ACT-003 · MUST · Actualización segura.** El paquete y el manifiesto están firmados y se verifica firma y hash antes de instalar. Nunca se instala con algo pulsado ni con el panel en uso (se espera a 5 min sin uso). Siempre se hace una copia antes. ‹d08:13-16,66›
- **ACT-004 · SHOULD · Novedades.** Lista por versión: número, fecha, [newBadge] en la última y los cambios con ✓ en el idioma de la interfaz, servidos por el manifiesto y no escritos en el código. ‹P4:1106-1112›
- **ACT-005 · SHOULD · Volver a la versión anterior.** Solo en los 7 días siguientes a una actualización y si se conserva la versión anterior. Si no, la fila se oculta. Muestra [rollbackT] y [rollbackD] con [Volver] y dos toques (en color de aviso). La reversión se completa al reiniciar. **Decisión:** si la versión nueva migró el esquema, se restaura la copia previa a la actualización y se informa de qué cambios posteriores se perderían antes de confirmar. ‹P4:1115-1121; d08:67; DIS-65›

### 2.26 COP · Copias de seguridad

- **COP-001 · Retirado · Tarjeta de migración.** **Retirado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): sin importación desde Macro Quick Access no hay migración que anunciar; [migT] y [migD] no se importan (sección `retired` de `data/i18n/handoff-import.json`). ‹P4:1123-1126; d02:90; DIS-66›
- **COP-002 · MUST · Carpeta y acciones.** Línea con la carpeta de copias y «JSON con versión». Acciones:
  - [Crear copia ahora]: añade «Manual · Hoy, HH:MM» arriba y avisa [backupDone].
  - [Exportar].
  - [Importar]: **decisión** de orden: primero se elige el archivo, se muestra un resumen (perfiles y atajos, versión, avisos) y después se elige [impMerge] o [impReplace] (con deshacer y copia previa).
  - Combinar: si coincide un id, gana el existente; los botones importados con id repetido reciben un id nuevo, no se pierden.
  ‹P4:1127-1138; d02:96; DIS-67›
- **COP-003 · MUST · Copia automática.** [rAuto/D], con el texto corregido para decir «a los 30 s de cada cambio». Se hace una copia 30 s después de cada cambio, con un máximo de 12 automáticas. Las manuales y las previas a actualizar o migrar **no** rotan con las automáticas. ‹d02:95; DIS-68›
- **COP-004 · MUST · Historial.** [backupHist] con fecha relativa localizada, tipo (Automática, Manual, Antes de actualizar, Antes de migrar) y los recuentos **de esa copia**. [Restaurar]: dos toques con [confirmB] durante 3,5 s. Restaurar hace antes una copia del estado actual, avisa «Copia restaurada: {fecha}» y ofrece deshacer. Si no hay copias: «Sin copias aún». ‹P4:1140-1156; DIS-69›
- **COP-005 · MUST · Esquemas y textos cifrados.** Una copia o importación de un esquema más nuevo no se aplica: avisa. Los textos cifrados que no se pueden descifrar en este equipo o usuario se importan como «Texto no disponible · vuelve a escribirlo», con el atajo marcado incompleto. ‹d02; COP; §6 PQ-37›

### 2.27 ACE · Acerca de y opinión

- **ACE-001 · MUST · Historia y tarjeta de la app.** [aboutTitle] y [aboutSub]; [story1] de 18, [story2] y la firma (MC, «Michael Coaguila», [creator] corregido a «Creador de Clícalo»). Tarjeta de la app: logotipo, «Clícalo», «v{versión} · MIT · código abierto», [GitHub] y [LinkedIn], que abren el navegador, y [shareShort], que copia la URL del repositorio y avisa [shared]. Dos columnas (1,4 : 1) desde 1240. ‹P4:1176-1191; DIS-70›
- **ACE-002 · MUST · Opinión.** [fbTitle]; 4 tipos en 2×2 (Sugerencia por defecto, Algo falla, Nueva función, Agradecimiento); área de 5 líneas con [fbPh] y 🎤 de 44. ‹P4:1192-1198›
- **ACE-003 · MUST · Opciones del envío.** [fbLog/D], con el nombre del registro corregido a clicalo.log, y [fbSys/D]. [logPvT] despliega el registro **exacto** que se enviaría, ya depurado. ‹P4:1199-1207›
- **ACE-004 · MUST · Enviar.** [Enviar por correo] (52) abre el correo con asunto «[Clícalo] {tipo}» y un cuerpo con el mensaje, «—», la versión de la app y de Windows reales. Como un enlace de correo no adjunta archivos, **decisión**:
  - el registro depurado se guarda como archivo y se abre su carpeta con el archivo seleccionado;
  - [fbSendD] indica que hay que adjuntarlo;
  - alternativa: «Reportar en GitHub».
  Si no hay app de correo, se copia el mensaje al portapapeles y se avisa. ‹P4:1208-1209; DIS-71; §6 PQ-38›
- **ACE-005 · MUST · Escríbeme directamente.** [fbDirect], el correo con botón para copiarlo ([copiedEmail], táctil 44), [fbPromise], [fbGh/D] y [fbContrib/D]. Las URL y el correo son configurables y se verifican antes de publicar. ‹P4:1211-1219; AUD-44-45›

### 2.28 BIE · Bienvenida

- **BIE-001 · MUST · Apertura.** Se abre automáticamente en el primer arranque, y si no se completó, en el siguiente. También desde [seeWelcome]. Es modal y siempre encima: 600 de ancho, padding 32, radio 20 y 5 barras de progreso. Esc no la cierra. Cerrarla con Alt+F4 equivale a Omitir. ‹P4:1234-1237; d06:3›
- **BIE-002 · Retirado · Pantalla previa de migración.** **Retirado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): la bienvenida no busca ni importa la configuración de Macro Quick Access; empieza siempre en el paso 0. ‹d06:14; DIS-72›
- **BIE-003 · MUST · Navegación.** Botones de 52: [Atrás] (oculto en el paso 0), [Omitir] (secundario) y [Siguiente], que en el paso 4 dice [finish]. **Omitir** cierra el asistente conservando lo ya aplicado en vivo (idioma, efectos del paso 1, vista, tamaño y tema) y aplica **el valor por defecto del kit inicial** del paso 2 (solo «Básicos», BIE-006), sin instalar ninguna plantilla. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): antes Omitir no instalaba nada del paso 2; ahora General y Siempre visible no se quedan vacíos para quien omite. ‹P4:1277-1282; DIS-73; D2›
- **BIE-004 · MUST · Paso 0.** Logotipo, «Clícalo», [tagline], [ob0t] y [ob0b], tarjeta con [story1] y la firma, y los botones Español y English (52), que cambian el idioma al instante. **Decisión:** el idioma inicial es el de Windows (es* → ES; cualquier otro → EN). ‹P4:1238-1247›
- **BIE-005 · MUST · Paso 1.** [ob1t] y [ob1b]. 5 opciones de selección múltiple (84 o más, en 2 columnas, con icono y estado accesible): Pantalla táctil, Control por voz, No puedo usar el teclado, Tengo temblor y Mouse o trackball.
  - **Efectos:**
    - preset Temblor fuerte si marca temblor; si no, Temblor leve si marca táctil o sin teclado; si no, Estándar;
    - números de voz = Control por voz;
    - noKeyboardUser = No puedo usar el teclado;
    - tamaño L si marca temblor.
  - Volver con Atrás y cambiar las opciones recalcula los efectos.
  - **Decisión:** para un usuario nuevo no hay nada preseleccionado (§6 PQ-39).
  - **«No puedo usar el teclado»** oculta [recPhys], pone el preset leve, destaca la biblioteca y la IA frente a escribir (la biblioteca se abre en lugar del recuadro vacío al crear) y garantiza 🎤 en todos los campos. Se puede cambiar después en General.
  ‹P4:1248-1254; script 1903; AUD-47›
- **BIE-006 · MUST · Paso 2: kit inicial.** [ob2t] y [ob2b]. Línea del teclado detectado, que se puede cambiar (PLA-009). Chips de selección múltiple (48), en el orden de `data/content/starter.json`:
  - primero **«Básicos»** ([kitBasics], descripción [kitBasicsD]), **marcado por defecto**: los atajos universales de Siempre visible y de General del contenido inicial (CAT-003);
  - después las 9 plantillas locales, **sin marcar**: Word, Navegador, VS Code, Excel, PowerPoint, Zoom (la antigua Videollamada), Explorador, Correo y Bloc de notas.

  Lo marcado se instala **al pulsar Empezar**, en un único paso de deshacer y un único aviso; cada plantilla crea un perfil vinculado a **todos** sus procesos (Navegador: Chrome, Edge, Firefox, Brave y Opera; Correo: Outlook clásico y el nuevo Outlook) con la variante de teclas del idioma de los programas (PLA-013). Se puede desmarcar todo: General y Siempre visible existen siempre y se empieza vacío. **Acepta:** sin tocar nada, Empezar u Omitir dejan solo «Básicos»; todo desmarcado deja General y Siempre visible vacíos; con Navegador marcado, Edge y Firefox abren el mismo perfil. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): antes solo había chips de plantillas, sin «Básicos» y sin nada preseleccionado (PQ-39), y cada plantilla tenía un proceso; así quien omite ya tiene atajos útiles, quien quiere empezar de cero puede hacerlo, y Navegador y Correo sirven con el programa de cada persona. ‹P4:1255-1262; script 1905; DIS-74; D2›
- **BIE-007 · MUST · Paso 3.** [obVt] y [obVb]; 3 tarjetas con miniatura, nombre y descripción. Se aplica al tocar. ‹P4:1263-1269›
- **BIE-008 · MUST · Paso 4.** [ob3t]. 3 tamaños con un botón «Copiar» de muestra a escala real, rotulados [sizeS/M/L], y tema en una fila de 4 botones de 44 ([themeAutoS] y los demás). Se aplica al instante. ‹P4:1270-1276; DIS-75›
- **BIE-009 · MUST · Empezar.** Guarda todo, instala lo marcado, cierra el asistente, muestra el panel sin minimizar y avisa [welcome]. **Decisión:** no concatena [ready]. ‹P4 script 1902-1907; DIS-76›
- **BIE-010 · SHOULD · Al repetir la bienvenida.** Se muestran preseleccionadas las respuestas guardadas. Desmarcar una plantilla ya instalada, o «Básicos», no la desinstala. Los efectos del paso 1 solo se aplican a los ajustes que el usuario no cambió a mano después (con un aviso de qué cambia). **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): «Básicos» es una opción más del paso 2 y, como una plantilla, desmarcarla no borra lo instalado (REG-08). ‹EC-BIE; D2›

### 2.29 DAT · Datos e integridad

- **DAT-001 · MUST · Documento de datos.** Toda la configuración del usuario (ajustes, Siempre visible, perfiles, orden, Frecuentes, dupIgnored, estado de la guía) va en un documento con número de esquema, en la carpeta de datos del usuario (`%APPDATA%\Clicalo`), nunca junto al programa. Contenido mínimo: los ajustes enumerados en d02 (idioma, tema, vista, tamaño, columnas, filas, escala de texto, opacidad, atenuado, mostrar teclas, números de voz, filas fija, selector y teclas fijas, reducir movimiento, sonido y destello, Auto/Fijo, lastProfile, pestaña, posiciones por monitor, filtro táctil, seguridad de teclas, sugerencias, teclado, IA, inicio y estabilidad, actualizaciones y noKeyboardUser). ‹d02›
- **DAT-002 · MUST · Escritura atómica y agrupada.** Tras un corte, el documento es la versión anterior íntegra o la nueva íntegra. Diez cambios en 1 s producen como máximo una escritura. Hay reintentos ante bloqueos del sistema de archivos o de la sincronización, y un fallo persistente se muestra al usuario, nunca solo en el registro. ‹d02:3; lección v1›
- **DAT-003 · MUST · Documento ilegible.** Un documento corrupto, truncado o de un esquema futuro nunca se sobrescribe. Se aparta, se restaura la última copia válida y se avisa. ‹REG-08; lección v1›
- **DAT-004 · MUST · Identificadores.** Los ids son únicos en todo el documento, opacos y estables: no dependen del nombre ni de la hora. Duplicar, añadir, instalar, importar o generar crea siempre ids nuevos. Renombrar no cambia el id. Los elementos de catálogo llevan una referencia de origen (plantilla, versión y elemento). ‹d02:53; DIS-77›
- **DAT-005 · MUST · Pertenencia.** Un botón vive en una sola lista. General y Siempre visible no se pueden borrar. General no tiene proceso. Un perfil puede tener varios procesos y cada proceso pertenece a un solo perfil (ATJ-007). **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): antes, un proceso por perfil; la unicidad pasa a ser por proceso (PQ-45). ‹d02:76-79; D2›
- **DAT-006 · MUST · Deshacer.** Pila de 20 estados que abarca **todo el documento**, incluidos Frecuentes y dupIgnored. Las ediciones seguidas de un mismo botón forman un paso. Los ajustes de presentación (tema, tamaño, opacidad) **no** entran en la pila, porque se revierten con el mismo control. Las operaciones masivas (Reemplazar, Restaurar, Reiniciar Frecuentes) dejan además una copia persistente para sobrevivir a un reinicio. ‹d02:81; DIS-78›
- **DAT-007 · MUST · Compartir un perfil.** Exportar genera `clicalo-perfil-<id>.json` con un solo perfil y `"type":"profile-share"`, con versión de esquema. Los textos cifrados se excluyen, con aviso, salvo que el usuario elija incluirlos en claro. Se importa desde Plantillas con vista previa. ‹d02:98; DIS-79›
- **DAT-008 · COULD · Compartir con enlace.** Enlace `clicalo://perfil/...` que **contenga** el perfil codificado y se valide como no confiable. Si no puede transportar el contenido, no se ofrece. ‹P4 script 1866; DIS-79›

### 2.30 MIG · Migración desde la v1 (retirada)

**Retirado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): Clícalo no se basa en nada de la app anterior y no lee su `profiles.json` v1, sus respaldos por idioma ni su `.zip`. Todos los requisitos de esta sección quedan retirados y sus identificadores no se reutilizan. Siguen vigentes la importación y la exportación del formato propio (COP-002, COP-005, DAT-007) y las migraciones entre versiones del esquema propio (NFR-006). El texto anterior está en la versión 1.0 del catálogo (historial de git).

- **MIG-001 · Retirado · Localizar la configuración v1.** (D1).
- **MIG-002 · Retirado · Esquema v1 completo.** (D1).
- **MIG-003 · Retirado · Tokenizador de combinaciones.** (D1).
- **MIG-004 · Retirado · Sin pérdidas y con informe.** (D1).
- **MIG-005 · Retirado · Catálogo de teclas para migrar.** (D1).
- **MIG-006 · Retirado · Conversión de ajustes.** (D1).
- **MIG-007 · Retirado · Atajos especiales.** (D1).
- **MIG-008 · Retirado · Repetidos de la migración.** (D1).
- **MIG-009 · Retirado · Importar archivos v1 en cualquier momento.** (D1).

### 2.31 LOG · Registros, privacidad y seguridad

- **LOG-001 · MUST · Registro.** `clicalo.log` en la carpeta de datos, con rotación de 5 × 1 MB. **Nunca** contiene:
  - textos de atajos o de pasos de macro ([oculto · N caracteres]);
  - títulos de ventana ([título oculto]);
  - búsquedas;
  - URL completas (solo el dominio);
  - rutas de App (sin el nombre de usuario);
  - la clave de la IA.
  Se depura al escribir, no al enviar. ‹d08:18-21; AUD-43›
- **LOG-002 · MUST · Nada sale sin acción del usuario.** Nada sale del equipo sin una acción explícita del usuario: no hay telemetría. La IA, con consentimiento, envía solo lo de PLA-008. ‹d08›
- **LOG-003 · MUST · Textos cifrados en reposo.** Los textos de Texto y de los pasos de texto de macro se cifran ligados a la cuenta de Windows. La clave de la IA va al almacén de credenciales del sistema. Nada de eso aparece en claro en el documento, las copias ni el registro. ‹d02:3; d08:23-24; AUD-56›
- **LOG-004 · MUST · Privacidad del Texto.** Un atajo Texto puede marcarse «privado» (por defecto, activado si parece una contraseña o correo, y a elección del usuario). Si es privado, no se muestra en la ficha, en los avisos ni en la búsqueda. ‹EC; §6 PQ-30›
- **LOG-005 · MUST · Datos de ejemplo sin datos personales.** Los textos de ejemplo de la biblioteca (correo, firma, dirección) son marcadores para rellenar, no datos del autor. ‹DIS-81›
- **LOG-006 · MUST · Contenido importado no confiable.** Perfiles compartidos, copias ajenas, plantillas de la comunidad y respuestas de la IA se validan contra el esquema, con límites de tamaño y tipos permitidos. Nada se ejecuta al importar. ‹NFR›
- **LOG-007 · MUST · Elevación solo a petición.** Clícalo solo se eleva cuando el usuario lo pide. La comunicación entre instancias no acepta órdenes de otros usuarios ni de procesos menos privilegiados. ‹NFR›
- **LOG-008 · MUST · Acciones de riesgo importadas.** Los atajos Web, App y Macro importados se muestran explícitamente y requieren confirmación uno a uno antes de instalarse. App nunca pasa por un intérprete de comandos. Web solo acepta http y https. ‹lección v1›

### 2.32 TEM · Temas y diseño

- **TEM-001 · MUST · Temas.** Auto (sigue el claro u oscuro de Windows en caliente), Oscuro, Claro y Alto contraste. **Decisión:** si Windows tiene un tema de contraste activo, Clícalo usa **siempre** alto contraste, con los colores del sistema, sea cual sea el tema elegido. Por defecto: Auto. ‹d07:16-30; AUD-34; DIS-38; DIS-82›
- **TEM-002 · MUST · Tokens completos.** La paleta de cada tema incluye los tokens existentes (fondos, texto, muted, line, accent, accentWash, onAccent, warn, warnWash, panel, side, win…) **más**: peligro, texto sobre peligro, éxito, texto sobre aviso, velo, sombra y anillo de foco. No hay colores escritos a mano fuera de los tokens (hoy hay 38). ‹d07:28; DIS-83›
- **TEM-003 · MUST · Colores por categoría.** 10 categorías (CAT) con su tono.
  - Tinte: oklch(tintL 0,12 tono), con tintL de 0,82 en oscuro y 0,5 en claro.
  - Fondo activo: oklch(0,72 0,12 tono / washA), con washA de 0,22 en oscuro y 0,16 en claro.
  - Alto contraste: #FFE600 sobre #333000, o los colores del sistema.
  - Conversión a sRGB con un mapeo de gamut definido, porque 11 colores quedan fuera.
  ‹seed CAT; theme-palettes›
- **TEM-004 · MUST · Contraste.** Texto a 4,5:1 o más y elementos gráficos a 3:1 o más en los 3 temas, medidos sobre el fondo compuesto real (panel semitransparente al 100 % de opacidad del usuario). Hoy fallan en el tema claro warn, accent sobre cardHi, peligro y los bordes line: se corrigen conservando el tono. En alto contraste no hay transparencia ni desenfoque. El estado atenuado queda exento mientras dure, porque vuelve a la opacidad normal al tocarlo (§6 PQ-42). ‹d07:39; DIS-84›
- **TEM-005 · MUST · Tipografía e iconos incluidos en la app.** Atkinson Hyperlegible 400/700 para la interfaz, JetBrains Mono 500 para teclas y procesos, y Material Symbols Rounded con el eje FILL para los estados activos. Todo empaquetado, sin red, con versión fijada y licencias incluidas. Cada nombre de icono de los datos existe en la versión incluida y hay un icono de respaldo. ‹d07:9-14; DIS-85›
- **TEM-006 · MUST · Reducir movimiento.** Con el ajuste propio o con las animaciones de Windows desactivadas, todas las transiciones pasan a 0 ms: opacidad 350, escala 80, fondo 150, puntos 200, interruptores 150, bienvenida, progreso y Probar. El destello de 240 ms se mantiene como cambio de color sin animación. ‹d07:43; AUD-35›
- **TEM-007 · MUST · Tamaño mínimo de texto.** Ningún texto baja de 11 px lógicos al 100 %. Las insignias de 8, las teclas en S de 9 y las etiquetas de 10 suben a 11, y el recorte o el alto se ajustan. ‹d07:14; DIS-27›
- **TEM-008 · MUST · Marca.** «Clícalo» con tilde en la interfaz; «clicalo» sin tilde solo en carpetas, identificadores y el esquema de enlaces. Logotipo «Cl» + ı sin punto + «calo» con la tilde en accent. El icono de la app es provisional. No quedan restos de «Macro Quick Access» en textos, enlaces ni nombres de archivo, salvo en la convivencia con la app anterior (SIS-005). **Modificado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): ya no hay migración de la que hablar. ‹d07:3-8; AUD-62›
- **TEM-009 · MUST · Anillo de foco.** Anillo de 3 px en accent, separado 2 px, en todo control, incluidos los campos. En alto contraste, amarillo. ‹d07:40; AUD-19›

### 2.33 IDI · Idioma

- **IDI-001 · MUST · Paridad ES/EN.** Español e inglés con el mismo conjunto de claves (hoy 669, más las nuevas de §9). El cambio es instantáneo en todas las ventanas, sin reiniciar y sin perder lo que se está escribiendo en un campo. ‹d07:48-50›
- **IDI-002 · MUST · Ningún texto fuera de los archivos de idioma.** Incluidos: «Con teclas» y «Solo nombre», los tamaños, «Automática» y «Manual», «off», los pasos de macro, «Hoy» y «Ayer», los grupos de teclas, las etiquetas de las apps de ejemplo, las novedades y «clic». Las etiquetas de teclas, de acciones de mouse, de categorías, de plantillas y de iconos se resuelven por su identificador en los archivos de idioma. ‹DIS-86›
- **IDI-003 · MUST · Nombres de tecla traducidos.** Se muestran en el idioma de la interfaz en fichas, avisos y nombres accesibles (Supr/Delete, Retroceso/Backspace, AvPág/PgDn, «Ctrl izq.»/«Left Ctrl»). ‹d07:49; DIS-87›
- **IDI-004 · MUST · Formato de mensajes.** Variables con nombre semántico ({app}, {profile}, {count}, {keys}, {index}, {total}, {version}, {name}), plurales y selección por idioma («1 atajo» / «2 atajos»), decimales y fechas según el idioma, y comillas «» en ES y “” en EN. Nada de mensajes armados concatenando fragmentos. ‹d07:49,52; DIS-88›
- **IDI-005 · MUST · Claves huérfanas.** Las 49 claves sin uso se eliminan o se implementan tras decidirlo (§10). Una comprobación automática detecta claves huérfanas, variables distintas entre idiomas y textos escritos en el código. ‹DIS-89›
- **IDI-006 · SHOULD · Otro idioma sin código.** Añadir un tercer idioma consiste en añadir un archivo, y el diseño tolera textos más largos (la relación ES/EN llega hoy a 2,7×). ‹NFR›

### 2.34 ACC · Accesibilidad y voz

- **ACC-001 · MUST · UI Automation.** Cada control expone su nombre localizado (con el número de voz si está activo), su rol y sus patrones:
  - Invoke en fichas y botones;
  - Toggle con estado en Auto/Fijo, interruptores, teclas fijas (tres estados), candado y Alternar;
  - SelectionItem en los grupos de tarjetas y segmentos;
  - ExpandCollapse en el botón de perfil, Ajustes rápidos y los plegables;
  - RangeValue en los deslizadores;
  - Tab/TabItem en Sistema.
  Regiones *live*: la barra de avisos y la barra de estado son *polite*; el pánico y los errores, *assertive*. Los glifos (←, ↑, ⌫, ★) tienen nombres textuales que se pueden decir. ‹d07:41; AUD-53›
- **ACC-002 · MUST · Elementos que hoy no llegan a 44.** Deben responder en 44 (visual → táctil 44):
  - **Panel:**
    - cabecera 32×36/40, Auto/Fijo 36, asa 26;
    - Soltar todo 40, botón de administrador 36, sugerencia 40;
    - Opacidad − / + 40, Tamaño, Lado y Tema 40;
    - fila fija de Compacta 40, Teclas fijas 40;
    - puntos 10 y 8, ◀ ▶ 48×40, ★ de Compacta 44×40, perfil de Compacta 40;
    - Deshacer 32, Repetir 36×32, × de edición 32.
  - **Pestaña:** asa 32; controles 30; pastilla 28; ◀ ▶ 26; ▲ ▼ 24×30; candado 34; Fijos 38; guía 40.
  - **Centro de control:**
    - idioma 32, ✕ 44×40;
    - biblioteca ✕ 40;
    - combinación: fichas de tecla 38, ⌫ 40×34, grupos 36, teclas 40, Grabar 36, Listo 32;
    - velocidad 40, ↑ ↓ ✕ de pasos 40×44, chips de destino 40;
    - interruptor de «Fijar» 48×28 (la fila entera es tocable);
    - Ver 40, «Probar en» 40, «Soltar tras» 40;
    - chips de IA 36, «Usar mi clave» 32, opciones de teclado 40, iconos 40;
    - «Se activa con» 40, renombrar 40, canal 40, Restaurar 40, copiar correo 40, Deshacer 32;
    - interruptor «Detectar» 48×28.
  ‹Analistas; REG-02›
- **ACC-003 · MUST · El estado nunca solo por color.** Toda selección, estado o alerta lleva además icono, texto o marca, y un estado accesible: tarjetas de opción, ACTIVO, armado, Auto/Fijo, 📌 y teclas fijas. ‹d07:44›
- **ACC-004 · MUST · Todo operable sin puntero.** Con teclado y conmutador: orden de tabulación lógico en el CC y la bienvenida. En el panel no activable, operación completa por UI Automation (Invoke, acción secundaria del menú) y por voz. ‹d07:42›
- **ACC-005 · MUST · Deslizadores sin arrastrar.** Todos tienen − / + discretos, teclado y RangeValue. ‹NFR analista 2›
- **ACC-006 · SHOULD · Tiempos ajustables.** La ventana de confirmación (3 s y 3,5 s) y la duración de los avisos se pueden alargar (×1, ×2, ×3) en General, y Deshacer sigue disponible (AVI-003). ‹WCAG 2.2.1; §6 PQ-43›
- **ACC-007 · MUST · Entrada táctil nativa.** El toque se trata como toque y no como mouse emulado: contacto, varios dedos, sin el círculo de toque de Windows y sin el clic derecho que simula mantener el dedo. Tocar el panel no abre el teclado táctil, salvo en el campo de búsqueda. **Decisión:** un contacto con un área muy grande (palma) se ignora. ‹AUD-54›
- **ACC-008 · MUST · DPI por monitor.** Nitidez y tamaño lógico correctos en cada monitor. Al pasar de uno a otro se recalcula sin verse borroso. Un panel a caballo entre dos monitores usa la escala del que contiene su centro. ‹AUD-55›
- **ACC-009 · MUST · Números de voz.** Con [voiceNums] activo, cada ficha de la cuadrícula, la barra, la fila fija y la ventana Fijos muestra su número (fondo warn con texto oscuro) y su nombre accesible pasa a ser «{n} {nombre}». ‹d03:112-113; DIS-14›
- **ACC-010 · MUST · Numeración de voz.**
  - Cuadrícula y barra: posición en la lista de la vista actual, **continua entre páginas** (la página 2 empieza en por-página + 1).
  - Búsqueda y Frecuentes: por orden de resultado.
  - **Decisión:** la fila Siempre visible y la ventana Fijos continúan tras el total de la lista actual (N+1…N+k), con números estables al paginar.
  - El editor muestra el número de la posición en el perfil.
  ‹P4 script 1533,1626; §6 PQ-10›
- **ACC-011 · MUST · Dictado.** Hay un botón 🎤 junto a todo campo de texto libre:
  - búsqueda, nombre, texto, Web, App, pasos, nombre de perfil, renombrar en vista previa, IA y opinión;
  - en la clave de la IA, en su lugar, un botón **Pegar**.
  ‹d07:45; DIS-25›

### 2.35 CAT · Catálogos y contenido inicial

- **CAT-001 · MUST · Catálogos versionados con identificadores neutros.** Teclas, acciones de mouse, categorías, iconos con etiquetas por idioma, la tabla combinación→icono por idioma de programas, las combinaciones bloqueadas o especiales (con acción alternativa), la biblioteca, las plantillas, los presets y los tamaños son datos versionados con esquema. Se cargan de una sola fuente, no se copian en el código y las etiquetas van en los archivos de idioma. ‹DIS-90›
- **CAT-002 · MUST · Cobertura de iconos.** ICONLIB cubre el 100 % de los iconos usados, incluidos los 32 que faltan, como `apps`, `history`, `link` y `favorite`, y tiene etiquetas con tildes separadas por idioma. ‹DIS-91›
- **CAT-003 · MUST · Contenido inicial.** General y Siempre visible existen siempre; su contenido es la opción «Básicos» del kit inicial (BIE-006), marcada por defecto, y sin ella empiezan vacíos.
  - Siempre visible («Básicos»):
    - Dictar Win+H, **como Pulsar**, porque Win+H ya alterna el dictado;
    - Hablar Ctrl+Espacio (Mantener);
    - Silenciar Win+Alt+K;
    - Escritorio Win+D.
  - General con 12 atajos (Cerrar ventana con confirmación), también de «Básicos».
  - Word, Navegador y VS Code pasan a ser plantillas instalables con variantes por idioma de los programas. Se instalan según la bienvenida.
  - El primer arranque sin bienvenida (hasta M4) aplica el valor por defecto del kit: solo «Básicos».
  - El contenido inicial no genera repetidos: se elimina el `copyb` duplicado del Navegador.
  - Plantillas: Excel 9, PowerPoint 6, Zoom 6 (la antigua Videollamada: sus atajos son de Zoom), Explorador 5, Correo 5 y Bloc de notas 4.
  - Biblioteca: Edición 7, Ventanas 7, Mouse 5, Voz 4, Textos 3 y Sistema 6, con «Bloquear» como acción de sistema.
  **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): antes General y Siempre visible traían siempre ese contenido; ahora es la opción «Básicos», marcada por defecto, y se puede empezar vacío. ‹seed; DIS-92; §6 PQ-44; D2›
- **CAT-004 · MUST · Datos del catálogo válidos.** Todo botón del catálogo usa teclas válidas:
  - «Clic izq.» pasa a mouse:drag;
  - «ñ» pasa a «Ñ»;
  - «\\» y «%» se añaden al catálogo o se reescriben según la distribución;
  - los pasos de macro llevan `kind`.
  Una prueba automática valida el catálogo. ‹DIS-93›
- **CAT-005 · SHOULD · Variantes por idioma.** Las plantillas que dependen del idioma de Office (Word, PowerPoint, Correo, Excel) tienen variante EN. El mapa combinación→icono se separa por idioma de programas (en ES, Ctrl+N es negrita; en EN, Ctrl+B). ‹DIS-53; DIS-94›
- **CAT-006 · COULD · Plantillas como archivos independientes.** Cada plantilla es un archivo con versión, autoría, idiomas revisados y procesos, para aceptar contribuciones mediante revisión. Un perfil puede vincular **varios** procesos: Navegador vincula `chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe` y `opera.exe`, y Correo `outlook.exe` (clásico) y `olk.exe` (el nuevo Outlook). Los atajos de una plantilla con varios programas deben figurar en la documentación oficial de todos ellos. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): resuelve PQ-45 para que Navegador y Correo sirvan con el programa que use cada persona. ‹escala; D2›
- **CAT-007 · MUST · Explorador de archivos.** El perfil de `explorer.exe` solo se activa con ventanas de carpeta, no con el escritorio ni la barra de tareas. ‹EC; v1›

---

## 3. Requisitos no funcionales

- **NFR-001 · MUST · Rendimiento.** Panel visible en menos de 1 s desde el arranque. Del toque al envío, menos de 50 ms. El destello y el aviso no retrasan el envío. ‹d01:50›
- **NFR-002 · SHOULD · Latencia del cambio de perfil.** Menos de 300 ms desde el cambio de ventana en primer plano (la v1 tardaba hasta 1,5 s), sin parpadeo. ‹§6 PQ-46›
- **NFR-003 · MUST · Nunca bloquear la interfaz.** Enviar teclas, generar con IA, copiar, importar, descargar o enumerar apps nunca bloquea la interfaz del panel ni la del CC. Todas son operaciones asíncronas que muestran su estado.
- **NFR-004 · MUST · Una única ruta de envío.** Pulsación y liberación explícitas, lados, tecla extendida, Unicode y traducción con la **distribución de la ventana en primer plano**. Hay un intervalo entre eventos medido y ajustable, cuyo valor inicial está por validar: unos 15 ms según docs y 20 ms según la experiencia de v1. Las combinaciones con Win mantienen Win toda la combinación y nunca la dejan sola. Las combinaciones de solo modificadores (Alt der., Ctrl der., Ctrl+Win) funcionan. La posición del cursor no afecta al envío. ‹lecciones v1; d03›
- **NFR-005 · MUST · Robustez ante excepciones.** Ninguna excepción en el manejo de un evento cierra la app. Si algo falla a mitad de un envío, se suelta todo.
- **NFR-006 · MUST · Integridad de datos.** DAT-002, DAT-003 y las copias previas. Migraciones puras, probadas e idempotentes.
- **NFR-007 · MUST · Accesibilidad verificada.** Auditoría automática de 44 px, contraste por par de tokens, nombres y roles en UI Automation. Pruebas manuales con Narrador, Acceso por voz y Accessibility Insights en cada versión.
- **NFR-008 · MUST · Privacidad por diseño.** LOG-001 a LOG-005, sin telemetría y con el mínimo de datos a la IA.
- **NFR-009 · MUST · Seguridad.**
  - Binario firmado, sin empaquetadores que disparen falsos positivos.
  - Actualización firmada.
  - Contenido importado no confiable.
  - Sin intérpretes de comandos.
  - Ninguna observación global del teclado permanente (solo mientras se graba).
  - Comunicación entre instancias restringida al usuario.
  - Datos en la carpeta del usuario.
- **NFR-010 · MUST · Actualizable y recuperable.** Canal Estable o Beta, instalación en reposo, versión anterior conservada 7 días, instalador y desinstalador que pregunta si conservar los datos, y una versión visible en la app.
- **NFR-011 · MUST · Compatibilidad.** Windows 10 22H2+ y Windows 11, pantallas táctiles, varios monitores, escalas mixtas, barra de tareas en cualquier borde o con autoocultado, y equipos 2 en 1 que rotan.
- **NFR-012 · MUST · Separación de responsabilidades (atributo de calidad, sin prescribir tecnología).** Las reglas de negocio se implementan una sola vez y son deterministas, sin depender de la interfaz ni del sistema operativo:
  - resolución de perfil, Frecuentes, repetidos, filtro táctil, teclas fijas, registro de pulsadas, confirmaciones, visibilidad de capas, disposición (filas y capacidad de la barra), numeración de voz, migraciones del esquema y deshacer.
  El estado de dominio, los ajustes, el estado del motor y el estado transitorio de la interfaz tienen cada uno su dueño. ‹lecciones v1 y prototipo›
- **NFR-013 · MUST · Testabilidad.**
  - Reloj y planificador simulables.
  - Fuente de app activa, receptor de entrada, servicio de IA, red y enumeración de ventanas simulables.
  - Pruebas automáticas de: envío (orden, lados, Unicode, ES/EN, códigos físicos), seguridad de teclas, filtro táctil, resolución de perfil, repetidos, migraciones del esquema e importación del formato propio (copias, perfiles compartidos y casos dañados), paridad de idiomas, esquemas de catálogos, que el panel no se active, y UI Automation.
  - Todo en integración continua obligatoria.
  **Modificado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): las pruebas de migración de los archivos v1 se sustituyen por las del formato propio. ‹d10:22-28; AUD-57›
- **NFR-014 · MUST · Builds reproducibles.** Dependencias con versión fija, inventario de componentes y solo dependencias declaradas (la v1 pesaba 147 MB con paquetes que no usaba).
- **NFR-015 · MUST · Mantenibilidad y escala.** Catálogos, textos, tokens y ajustes se declaran como datos (clave, tipo, rango, texto y descripción), de modo que añadir un ajuste, una sección, un idioma o una plantilla no toca lo demás. Una capacidad tiene un solo modelo y una sola orden. Los artefactos generados no se editan a mano. Los cambios pasan por ramas, revisión y un historial de cambios.
- **NFR-016 · MUST · Escala de datos.** Funciona con 50 perfiles y 2000 atajos sin degradación perceptible. El usuario real tiene 14 y 210.
- **NFR-017 · MUST · Escalado por monitor.** Medidas en px lógicos. Se recalcula al cambiar de monitor, escala o resolución.
- **NFR-018 · MUST · Instancia única.** Una por sesión de usuario, sin doble inyección.
- **NFR-019 · MUST · Estabilidad bajo el dedo.** PAN-009.
- **NFR-020 · MUST · Tiempos y umbrales definidos una sola vez.** Todos como constantes con nombre:

| Constante | Valor |
|---|---|
| Toque largo | 600 ms |
| Confirmar ejecución | 3 s |
| Confirmar borrado | 3,5 s |
| Aviso normal / con Deshacer | 3,2 s / 6 s |
| Destello | 240 ms |
| Marca del Modo prueba | 700 ms |
| Modo prueba | 30 s |
| Repliegue de la barra | 900 ms |
| Atenuado / su transición | 2,5 s / 350 ms |
| Deslizar para paginar / bloqueo posterior | 60 px / 300 ms |
| Umbral de arrastre | max(6 px, cancelMovePx) |
| Soltado por tiempo | 30 / 60 / 120 s, Nunca |
| Esperas de macro | 100–10 000 ms |
| Repetición del desplazamiento | 60 / 40 / 25 ms |
| Restaurar portapapeles | 500 ms |
| Tiempo máximo de la IA | 15 s |
| Probar ahora (envío / vuelta) | 0,9 s / 2,6 s |
| Copia automática | 30 s tras el cambio |
| Actualizar en reposo | 5 min |
| Reversión | 7 días |

---

## 4. Casos límite y comportamiento esperado

**Panel y disposición**
- **EC-PAN-01.** Pantalla de 768 px, tamaño L, con Ajustes rápidos y una alerta abierta y el panel abajo: se aplica CUA-003. Si no basta, el panel sube hasta que cabe una fila (CUA-002).
- **EC-PAN-02.** Monitor desconectado, cambio de resolución o de escala a mitad de un arrastre, barra de tareas lateral o con autoocultado: todas las superficies vuelven al área de trabajo (PAN-006). El arrastre se cancela limpiamente.
- **EC-PAN-03.** Nombres largos al 150 % en S: ajuste de 2 líneas y recorte (CUA-011).
- **EC-PAN-04.** Más de 12 perfiles: la cuadrícula de perfiles se desplaza sin disparar fichas (TAC-004).
- **EC-PAN-05.** El mismo atajo visible dos veces (Frecuentes y Siempre visible): FRE-001 lo excluye de Frecuentes. En la búsqueda, ambas apariciones comparten estado y antirrebote, porque son el mismo id.
- **EC-PAN-06.** Opacidad al 30 % con atenuado al 10 %: el efectivo es min(10, 30) = 10 % mientras está atenuado. La burbuja y el asa no bajan del 55 %. El panel sigue capturando toques (PQ-11).

**Ejecución y seguridad de teclas**
- **EC-EJE-01.** Dos dedos en dos Mantener, o Mantener más Subir/Bajar: se siguen por contacto (EJE-006).
- **EC-EJE-02.** Aparece el pánico mientras se mantiene: PAN-009.
- **EC-EJE-03.** Mantener con cambio de app y el dedo aún sobre el botón: se suelta al cambiar. Al levantar el dedo no se envía otra liberación ni aparece un aviso de «soltado».
- **EC-EJE-04.** Soltado por cambio de app durante un arrastre (Alternar del botón izquierdo): se suelta, con [releasedSwitch]. Queda documentado como comportamiento esperado.
- **EC-EJE-05.** Clícalo pasa a primer plano (Inicio, notificaciones) con Ctrl pulsado: no hay cambio de app, así que no se suelta. El pánico sigue visible.
- **EC-EJE-06.** Teclas fijas con un atajo que ya incluye ese modificador: se deduplica, y el orden queda Ctrl, Alt, Shift, Win y luego las teclas del atajo.
- **EC-EJE-07.** Cursor en una esquina de la pantalla: sin efecto en el envío (NFR-004).
- **EC-EJE-08.** Destino elevado (el Administrador de tareas en Windows 11): EJE-013.
- **EC-EJE-09.** Win+L o Ctrl+Alt+Supr escritos en otro orden, con lado o con alias: se detectan (REP-001).
- **EC-EJE-10.** Ctrl+Ñ, Ctrl+«\», Ctrl+«+» o Shift+«%» con distribución US o latinoamericana: se traducen con la distribución destino. Si la tecla no existe en ella, no se envía nada y se avisa (texto nuevo «Esta tecla no existe en tu teclado actual»).
- **EC-EJE-11.** En latinoamericano, AltGr = Ctrl izq. + Alt der. sintéticos: para herramientas que escuchan Alt derecho solo se envía Alt der. (PQ-47).

**Pestaña**
- **EC-PES-01.** Pestaña sin barra de avisos: PES-014.
- **EC-PES-02.** Barra cerrada, burbuja u oculto con teclas pulsadas: PES-013, BUR-002 y BUR-003.
- **EC-PES-03.** Asa junto al borde (gestos de notificaciones y widgets): PES-002.
- **EC-PES-04.** Posición del asa en los extremos: los botones ± se desactivan.

**Búsqueda**
- **EC-BUS-01.** El teclado táctil tapa el panel: el panel se recoloca por encima mientras la búsqueda está abierta.
- **EC-BUS-02.** Buscar desde la Pestaña: BUS-006.

**Frecuentes y repetidos**
- **EC-FRE-01.** Más de 9 fijados: FRE-001.
- **EC-FRE-02.** Deshacer el borrado de un atajo fijado: FRE-005.
- **EC-REP-01.** «Dejar solo en Siempre visible» con nombres distintos (Ctrl+S Guardar global frente a Ctrl+S Subrayar en Word ES): no se borra el de nombre distinto (REP-005).
- **EC-REP-02.** Duplicar crea un repetido al instante en el mismo perfil: se marca ⚠. Es esperado.

**Perfiles**
- **EC-PER-01.** Modo captura con la app deseada ya en primer plano detrás del CC: ATJ-008.
- **EC-PER-02.** App sin perfil y sin plantilla: no hay sugerencia (PER-009). La ficha «Crear para» tampoco aparece (PQ-14).
- **EC-PER-03.** Proceso protegido que no se puede abrir: no se da un falso aviso de administrador (EJE-013).
- **EC-PER-04.** Apps que comparten proceso (PWA, Terminal): el perfil se vincula por proceso y la limitación se documenta. Un perfil puede tener varios procesos, pero no distingue dos apps que comparten uno. **Modificado por decisión del usuario del 2026-10-03** (D2, [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md)): se añade que un perfil puede tener varios procesos (PQ-45).

**Editor**
- **EC-EDI-01.** Cambiar las teclas con lados elegidos: se conservan en los modificadores que siguen (EDI-009).
- **EC-EDI-02.** Grabar Esc o una combinación con Win: EDI-010.
- **EC-EDI-03.** El signo menos U+2212 del catálogo frente a «-» escrito o de la v1: son el mismo identificador canónico.
- **EC-EDI-04.** Deshacer tras el descarte de un borrador: el borrador no reaparece (ATJ-011).

**Probar**
- **EC-PRB-01.** Destino cerrado, lista vacía, destino elevado o imposibilidad de recuperar el primer plano: PRB-003 y PRB-007.
- **EC-PRB-02.** Probar con teclas fijas activas: se aplican como en uso normal. No hay soltado por cambio de app (PRB-006).

**Plantillas e IA**
- **EC-PLA-01.** Campo de la IA vacío: Generar desactivado.
- **EC-PLA-02.** Nombres no latinos («微信») o con símbolos («C++ Builder»): los ids son opacos, no se derivan del nombre.
- **EC-PLA-03.** Instalar con 0 atajos marcados: el botón se desactiva.
- **EC-PLA-04.** Cambiar el idioma de los programas después de instalar: los atajos instalados no cambian. Se ofrece «Actualizar a la variante {idioma}» desde la vista previa (SHOULD).
- **EC-PLA-05.** Cuota con cambio de hora, zona horaria o suspensión a medianoche: el reinicio se calcula a partir de la fecha local actual al consultar.

**Sistema, copias y bienvenida**
- **EC-SIS-01.** Actualización fallida: ACT-001 (estado de error).
- **EC-SIS-02.** Varios usuarios con sesión abierta: una instancia por sesión (SIS-003).
- **EC-SIS-03.** Pantalla de bloqueo o escritorio seguro de UAC: el panel no está disponible. Al volver, se aplica SEG-006.
- **EC-COP-01.** Rotación: 12 automáticas; las manuales y las previas no se pierden por la rotación (COP-003).
- **EC-BIE-01.** Atrás al paso 1: se recalculan los efectos. Omitir tras el paso 1: se conserva lo aplicado.

**Migración**

**Retirados por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): sin importación desde Macro Quick Access no tienen efecto. Los identificadores se conservan y no se reutilizan.
- **EC-MIG-01.** Retirado · `profiles.json` y datos de Clícalo a la vez (D1).
- **EC-MIG-02.** Retirado · Archivo v1 dañado (D1).
- **EC-MIG-03.** Retirado · Perfil fijado o activo inexistente (D1).
- **EC-MIG-04.** Retirado · Sin perfil General o General con proceso (D1).
- **EC-MIG-05.** Retirado · Colores v1 no estándar (D1).

---

## 5. Discrepancias resueltas

Criterio: §0.1. «Manda» indica la fuente que prevalece.

| ID | Tema | docs | Prototipo v4 / otro | Manda | Resolución |
|---|---|---|---|---|---|
| DIS-01 | App sin perfil en Auto | General (d03:16) | Se queda en el anterior | README glosario + docs | General (PER-003). Evita enviar atajos de Word a Excel. (Sin informe de migración desde la decisión D1 del usuario del 2026-10-03.) |
| DIS-02 | Fijo→Auto sin perfil | Salta a la app | No cambia | docs | Salta a General (PER-006). |
| DIS-03 | Auto→Fijo en Frecuentes | Fija retProf | Anuncia sin guardar | docs + coherencia | lastProfile = retProf; se queda en Frecuentes. |
| DIS-04 | Orden de retProf | App/último/General | Añade «perfil de la app» | Prototipo | PER-004. |
| DIS-05 | Sugerencia sin plantilla | Solo con plantilla | La muestra; Crear no hace nada | docs (fallo del prototipo) | PER-009. |
| DIS-06 | Excepciones al atenuado | Incluye el menú | Sin menú ni guía | Unión | Todas: pánico, Ajustes rápidos, menú, cuadrícula de perfiles, búsqueda, edición, Mantener, ventanas al costado, guía, CC y bienvenida. |
| DIS-07 | Mínimo del 55 % en la burbuja | Siempre | Solo al atenuar | Auditoría | Siempre; también el asa (BUR-002, PES-004). |
| DIS-08 | La escala de texto hace crecer la ficha | Sí | La calcula y no la usa | docs | CUA-011. |
| DIS-09 | 3 filas en S | Máx. 2 en S | Manual permite 3 | Prototipo | En Auto, 2 en S; en manual, hasta 3 si caben (CUA-001). |
| DIS-10 | Medir o estimar | Medir | Estima con constantes | docs | Se mide (CUA-001). |
| DIS-11 | Desplazamiento en la cuadrícula | Sin scroll | overflow auto | docs + d09 | CUA-002. |
| DIS-12 | Teclas fijas con el mouse | Sí (Ctrl+clic) | Excluidas | docs + [modOnce] | FIJ-006/007. |
| DIS-13 | Punto del asa con teclas fijas | «Teclas pulsadas» | Las ignora | docs | Las incluye (PES-001). |
| DIS-14 | Números de voz en la fila fija y en Fijos | «Cada botón» | Sin número | docs + REG-06 | ACC-009/010. |
| DIS-15 | App elevada | No enviar | Dice «enviado» | docs + Auditoría | EJE-013, con texto nuevo. |
| DIS-16 | Cancelar la captura desde el panel | Con Cancelar | Sin Cancelar | docs | ATJ-008. |
| DIS-17 | maxHold por botón | Sí | Solo global | docs (el editor del prototipo ya lo pide) | SEG-004, por elemento. |
| DIS-18 | Macro cancelable | Segundo toque la detiene | Instantánea | docs | EJE-010 y textos nuevos. |
| DIS-19 | Uso y Repetir de Mantener/Alternar | Actualizar ambos | Ninguno | Mixto | Cuentan uso; **no** son Repetir (FRE-002, AVI-004). |
| DIS-20 | Filtro y Modo prueba en Mantener | Filtro total; nada se envía | Mantener se salta ambos | docs + Auditoría (bloqueante) | TAC-002, TAC-008. |
| DIS-21 | Número de interruptores | Dice 3, enumera 4 | 4 | Prototipo | 4 (AJR-001). |
| DIS-22 | Lado de la pestaña, inalcanzable | Descrito | Solo en la vista Pestaña, sin acceso | Decisión | Botón `tune` en la barra (PES-009), además de General. |
| DIS-23 | Indicador del botón de perfil en Frecuentes | — | ↶ solo en Completa | Coherencia | ↶ en las 3 vistas y punto de la app activa en la barra. |
| DIS-24 | Hallazgos aceptados ausentes de v4 | — | Faltan | Auditoría | SEL-005, PES-003, PES-008 (teclas fijas), BUS-003, PLA-016, «Deshacer cambios en X», «servicio no disponible», no abrir el teclado táctil, alto contraste siempre. |
| DIS-25 | 🎤 en los campos | Todos | Faltan en la búsqueda, Texto, etc. | docs + REG-05 | ACC-011. |
| DIS-26 | Rojo en alto contraste | Solo negro, blanco y amarillo | #FF6B6B | Decisión | Token de peligro propio del alto contraste que cumpla el contraste. Con el alto contraste de Windows, colores del sistema. Estado siempre con icono y texto. |
| DIS-27 | Tipografía menor de 11 | Mínimo 11 | 8, 9 y 10 | docs (público de baja visión) | TEM-007. |
| DIS-28 | [orderHint] «sin arrastrar» | Se puede arrastrar | Se puede arrastrar | docs + Prototipo | Se corrige el texto. |
| DIS-29 | Nombre en los dos idiomas | Ambos | Solo el activo (atajo) | docs | EDI-002. |
| DIS-30 | Fila entera tocable | Sí | Solo el interruptor | docs + REG-02 | EDI-015, PLA-011. |
| DIS-31 / DIS-79 | Compartir perfil | Archivo | Enlace sin contenido | docs | Archivo MUST; enlace COULD, solo si lleva el contenido (DAT-007/008). |
| DIS-32 | Proceso duplicado | Confirmar y desvincular | Sin comprobar | docs | ATJ-007. |
| DIS-33 | Flujo «Reemplazando» | Existe | Franja sin disparador | docs | REP-006 (SHOULD). |
| DIS-34 | Deshacer al editar el perfil | Todo | No | README R7 | ATJ-004. |
| DIS-35 | Win+L en la biblioteca y brillo | Alternativa del sistema | Ofrece teclas que no funcionan | docs + Auditoría | EJE-016, CAT-003. |
| DIS-36 | Pánico con la barra cerrada, burbuja u oculto | «Junto a la barra» | Solo punto o anillo | README R3 | Botón flotante de un toque; soltar al ocultar; Soltar todo en el menú de bandeja. |
| DIS-37 | Nombre del ajuste de números | «Números para voz» | [voiceNums] «…control por voz» | Decisión | Se unifica en «Números para voz» en todas las claves. |
| DIS-38 | Tema Auto | Sigue a Windows | Pinta oscuro | docs | TEM-001. |
| DIS-39 | CC como ventana | Ventana normal | Modal simulado | docs | CCM-001/004. |
| DIS-40 | Umbral de arrastre del panel y la burbuja | — | 4 px | Auditoría (temblor) | max(6, cancelMovePx) en todo. |
| DIS-41 | Acentos en la búsqueda | — | Los distingue | Coherencia con el buscador de iconos | Sin acentos; no busca en el contenido de Texto. |
| DIS-42 | Buscar desde la barra | — | Cambia la vista para siempre | Decisión | Temporal (BUS-006). |
| DIS-43 | × en Frecuentes | — | Borra en el origen | Decisión (seguridad) | Quita de Frecuentes (CUA-013). |
| DIS-44 | Dos «Auto» en la barra | — | Auto/Fijo y candado «Auto» | Decisión | Candado «Se pliega» / «Abierta». |
| DIS-45 | Avisos en la Pestaña | — | No hay superficie | README R3/R4 | PES-014. |
| DIS-46 | Instalar cambia la vista | Frecuentes nunca cambia sola | Cambia siempre | docs | PER-007. |
| DIS-47 | Deshacer Reiniciar Frecuentes | Deshacer | No restaura | docs | FRE-004; Frecuentes dentro del documento. |
| DIS-48 | Clave canónica | «Normalizada» | Depende del orden y del idioma | docs | REP-001. |
| DIS-49 | «Dejar solo en Siempre visible» | — | Borra también los de otro nombre | Decisión (seguridad) | Solo los del mismo nombre. |
| DIS-50 | Temblor fuerte con 28 frente al paso de 5 | — | Incoherente | Decisión | TAC-005. |
| DIS-51 | La zona de prueba evalúa 2 de 4 criterios | 4 | 2 | docs + Auditoría | TAC-006. |
| DIS-52 | Qué es «Incompleto» | — | Solo nombre y teclas | Decisión | ATJ-009 amplía los criterios. |
| DIS-53 | KEYICON mezcla ES y EN | — | Mezcla | Decisión | Mapa por idioma de programas (EDI-005). |
| DIS-54 | Valores iniciales por tipo | — | Valores reales de ejemplo | Decisión | Campos vacíos con guía (EDI-006). |
| DIS-55 | Lado: `sides` frente a nombres de tecla | Nombres | Ambos | Decisión | Un solo mecanismo (EDI-009). |
| DIS-56 | Orden al grabar | Orden de pulsación | Orden fijo | docs | EDI-010. |
| DIS-57 | Validación de Web | — | Rechaza localhost, IP, etc. | Decisión | EDI-014. |
| DIS-58 / 59 | IA: campo vacío; cuota antes del resultado | — | Genera «Photoshop»; descuenta antes | Decisión | PLA-002/003. |
| DIS-60 | Errores de la IA y [Reintentar] | 3 (d05) / 4 (d08) | 3; Reintentar vacía el campo | Auditoría + d08 | PLA-006. |
| DIS-61 | Plantillas de apps abiertas con Detectar desactivado | Solo no instaladas | Desaparecen | Decisión | Aparecen en Disponibles. |
| DIS-62 | «Añadir N» sin nombres ni variantes; instalar 0 | Se aplican | No se aplican; permite 0 | docs | PLA-017. |
| DIS-63 | Orden de General | Idioma, Tema, Vista, Tamaño | …Tamaño, Vista | Prototipo | GEN-001. |
| DIS-64 | Atenuar más opaco que la opacidad normal | — | Posible | Decisión | min(dimTo, opacidad). |
| DIS-65 | Versión anterior tras migrar el esquema | — | — | Decisión | ACT-005. |
| DIS-66 | Tarjeta de migración | Solo si hubo migración | Siempre | docs | COP-001, retirado por la decisión D1 del usuario (2026-10-03). |
| DIS-67 | Orden de Importar | — | Pregunta antes de elegir el archivo | Decisión (R8) | COP-002. |
| DIS-68 | Texto de la copia automática | A los 30 s | «Cada vez que cambias» | docs | Se corrige el texto. |
| DIS-69 | Confirmación de Restaurar | Doble toque | Sin plazo; recuentos actuales | Coherencia | 3,5 s y recuentos de la copia. |
| DIS-70 | Marca antigua | Clícalo | «Macro Quick Access», macro_quick_access.log, repositorio antiguo | Auditoría + d07 | TEM-008. |
| DIS-71 | Adjuntar el registro por correo | mailto con adjunto | Imposible | Decisión | ACE-004. |
| DIS-72 | Pantalla de migración | Antes del paso 0 | No existe | docs | BIE-002, retirado por la decisión D1 del usuario (2026-10-03). |
| DIS-73 | Omitir la bienvenida | Valores por defecto | Conserva lo aplicado | Prototipo | BIE-003. |
| DIS-74 | Cuándo se instala | Al pasar del paso 2 | Al pulsar Empezar | Prototipo | BIE-006. |
| DIS-75 | Nombres de tamaño | — | «Compacto/Normal/Grande» en la bienvenida | Decisión | [sizeS/M/L] en todo; «Compacto» chocaba con la vista Compacta. |
| DIS-76 | Aviso final de la bienvenida | — | «¡Todo listo! Listo…» | Decisión | Solo [welcome]. |
| DIS-77 / 78 | ids y alcance de deshacer | Únicos | Hechos con Date.now(); Frecuentes fuera | docs | DAT-004/006. |
| DIS-80 | Repetidos tras migrar | Regla del mismo nombre | ≈45 grupos | Decisión | MIG-008, retirado por la decisión D1 del usuario (2026-10-03). |
| DIS-81 | Datos personales en la biblioteca | — | Firma y dirección del autor | Privacidad | LOG-005. |
| DIS-82 | Alto contraste siempre o solo en Auto | Solo en Auto | — | Auditoría | TEM-001. |
| DIS-83 / 84 | Paleta incompleta; contraste del tema claro | 4,5:1 | Fallos calculados | docs | TEM-002/004. |
| DIS-85 | Fuentes | — | Cargadas de la red | Decisión | Incluidas en la app (TEM-005). |
| DIS-86 / 87 | Textos escritos en el código; nombres de tecla solo en ES | Todo traducible | 17 textos en el código; KEYG en ES | d07 | IDI-002/003. |
| DIS-88 | {p} usado como número en migT | {p} = perfil | Número | d07 | Variables semánticas (IDI-004). Sin efecto: migT se retiró por la decisión D1 del usuario (2026-10-03). |
| DIS-89 | Número de hallazgos y de textos | README: 60; Auditoría: 558 textos | 62 hallazgos; 669 claves | Datos | Referencia: 62 y 669. |
| DIS-90–94 | Catálogos: identificadores, iconos, semilla, teclas no válidas, variantes | — | Varios defectos | Decisión | CAT-001…005. |
| DIS-95 | Intervalo entre eventos | ~15 ms | v1 necesitó 20 ms | Medición | NFR-004, ajustable. |
| DIS-96 | Opacidad | Paso de 0,05 | v1 usaba pasos de 0,08 | docs | Paso de 0,05 (GEN-009). El redondeo al migrar desapareció con la decisión D1 del usuario (2026-10-03). |

---

## 6. Preguntas abiertas

Cada pregunta lleva una **propuesta por defecto** que se aplica si no hay respuesta, para no bloquear el desarrollo.

| ID | Pregunta | Propuesta por defecto | Afecta a |
|---|---|---|---|
| PQ-01 | ¿Cómo se escribe en la búsqueda sin romper REG-01? | Excepción controlada con devolución del foco (BUS-002). | BUS-002 |
| PQ-02 | Mantener por voz, teclado o conmutador | Primera invocación presiona y la segunda suelta (EJE-005). | EJE-005 |
| PQ-03 | Punto objetivo de las acciones de mouse | Última posición del puntero fuera de Clícalo (EJE-009). | EJE-009 |
| PQ-04 | ¿Qué significa Alternar? | «Teclas presionadas mientras está activo». Win+H y Alt+A pasan a ser Pulsar. | EJE-007, CAT-003 |
| PQ-05 | Versión de lanzamiento de Clícalo | 2.0.0, continuando la numeración de Macro Quick Access. La reversión solo se ofrece desde la primera actualización. | ACE, ACT |
| PQ-06 | ¿Cómo se representa pinned_profile de la v1 (perfil base de 27 atajos)? | Informe más la sugerencia de fijar sus 4–8 atajos más usados en Siempre visible. Se valora una función «perfil base» si el usuario la echa de menos. **Cerrada por la decisión D1 del usuario (2026-10-03):** no hay migración desde la v1. | MIG-006 |
| PQ-07 | ¿Acceso a Ajustes rápidos desde la barra? | Sí, con el botón `tune` (PES-009). | PES-009 |
| PQ-08 | Color personalizado por botón (usado por el autor en 4 botones) | Se mapea a la categoría más cercana y el hexadecimal va al informe. Posible campo de color libre más adelante. **Cerrada por la decisión D1 del usuario (2026-10-03):** no hay migración desde la v1. | MIG, TEM |
| PQ-09 | Separadores de la v1 | Se descartan con informe; no hay ninguno en los datos reales. **Cerrada por la decisión D1 del usuario (2026-10-03):** no hay migración desde la v1. | MIG |
| PQ-10 | Numeración de voz de la fila fija y de Fijos | Continúa tras N (ACC-010). | ACC-010 |
| PQ-11 | Primer toque sobre el panel atenuado | Despierta y ejecuta (prototipo). | EJE-017 |
| PQ-12 | ¿Respuesta a un toque ignorado? | Sí, discreta y desactivable (TAC-003). | TAC-003 |
| PQ-13 | ¿Alto contraste con los colores del sistema o con la paleta fija? | Colores del sistema si Windows está en alto contraste; paleta fija si solo lo elige la app. | TEM-001 |
| PQ-14 | Apps sin plantilla y sin perfil | Nada en el panel. En Plantillas se ofrece «Generar con IA» o «Perfil vacío». | PER-009 |
| PQ-15 | ¿Observar el clic físico para las teclas fijas? | Sí, solo mientras haya alguna en estado 1 (FIJ-007). | FIJ-007 |
| PQ-16 | Orden entre el panel y el CC | CCM-004. | CCM-004 |
| PQ-17 | ¿Filtro táctil en el CC y la bienvenida? | No (TAC-007). | TAC-007 |
| PQ-18 | Pestaña con varios monitores | Monitor del panel; posición por monitor y lado (PES-016). | PES-016 |
| PQ-19 | Qué hace «Pausar» | BUR-004. | BUR-004 |
| PQ-20 | Umbral de arrastre | max(6, cancelMovePx) en todo. | PAN-004 |
| PQ-21 | Mover el panel sin arrastrar | Posiciones predefinidas (PAN-005). | PAN-005 |
| PQ-22 | Atajo global para mostrar u ocultar | Opcional, desactivado por defecto, con combinación configurable que no sea Ctrl+Shift+M. | BUR-005 |
| PQ-23 | ¿Frecuentes excluye lo que ya está en Siempre visible? | Sí. | FRE-001 |
| PQ-24 | Macro «ejecutando»: textos y progreso | «Ejecutando {name}: paso {index} de {total} · toca para detener». | EJE-010 |
| PQ-25 | Relanzar elevado sin UAC para quien usa voz | Se ofrece arrancar elevado con Windows sin UAC. Mientras, avisar de que el UAC puede requerir otra forma de confirmar. | EJE-013, SIS-002 |
| PQ-26 | Compartir: archivo o enlace | Archivo (DAT-007). | DAT-007/008 |
| PQ-27 | Sin selector de perfil, ¿cómo se llega a Frecuentes? | Tocar el título de la cabecera (SEL-006). | SEL-006 |
| PQ-28 | Si no cabe ni una fila | Se suben el panel y la ocultación de CUA-003. | CUA-002 |
| PQ-29 | Instrucciones de voz en Windows 10 (sin Acceso por voz) | Variante de [vh1–3] para Reconocimiento de voz de Windows («mostrar números», «clic …»). | EDI-016 |
| PQ-30 | Textos sensibles: ocultar la vista previa y excluirlos de la búsqueda | Marca «privado» (LOG-004). | LOG-004 |
| PQ-31 | «Páginas abiertas» del navegador | COULD: solo con consentimiento; si no, sugerencias estáticas o historial propio de Clícalo. | EDI-014 |
| PQ-32 | ¿Control de «Pedir confirmación» en el editor? | Sí (EDI-017). | EDI-017 |
| PQ-33 | Probar Mantener, Alternar y atajos con confirmación | PRB-004. | PRB-004 |
| PQ-34 | ¿Dónde van Reducir movimiento y Reiniciar Frecuentes? | GEN-013. | GEN-013 |
| PQ-35 | ¿Se puede desactivar «Una sola ventana»? | Recomendado: quitar el interruptor y hacer la instancia única obligatoria. Mientras no se decida, el interruptor existe pero su desactivación pide confirmación. | SIS-003 |
| PQ-36 | ¿Detectar Macro Quick Access en ejecución? | Sí (SIS-005). | SIS-005 |
| PQ-37 | Portabilidad de los textos cifrados | Excluidos por defecto al exportar, con opción de incluirlos en claro y aviso. | COP-005, DAT-007 |
| PQ-38 | Vía para enviar el registro | ACE-004. ¿Se muestra el correo personal del autor en la app? Pendiente de confirmar. | ACE-004/005 |
| PQ-39 | Preselecciones de la bienvenida | Nada marcado para usuarios nuevos en el paso 1. En el paso 2, **cerrada por la decisión D2 del usuario (2026-10-03):** «Básicos» marcado y las plantillas sin marcar. | BIE-005/006 |
| PQ-40 | Los 8 atajos de la v1 que nunca funcionaron | Se migran con el significado del nombre y la marca «Revisar». **Cerrada por la decisión D1 del usuario (2026-10-03):** no hay migración desde la v1. | MIG-007 |
| PQ-41 | Repetidos de la migración | Se añaden a dupIgnored y se listan (MIG-008). **Cerrada por la decisión D1 del usuario (2026-10-03):** no hay migración desde la v1. | MIG-008 |
| PQ-42 | ¿El estado atenuado queda exento del contraste? | Sí. | TEM-004 |
| PQ-43 | ¿Tiempos de confirmación y avisos ajustables? | Sí, ×1, ×2 y ×3 (ACC-006). | ACC-006 |
| PQ-44 | Perfiles de ejemplo | **Cerrada por la decisión D2 del usuario (2026-10-03):** General y Siempre visible existen siempre y «Básicos» (marcado por defecto) los rellena; las 9 plantillas, solo si se eligen. | CAT-003, BIE-006 |
| PQ-45 | ¿Varios procesos por perfil? | **Cerrada por la decisión D2 del usuario (2026-10-03): sí.** La unicidad es por proceso (ATJ-007, DAT-005); Navegador vincula cinco navegadores y Correo los dos Outlook. | CAT-006, ATJ-007 |
| PQ-46 | Latencia máxima del cambio de perfil | < 300 ms. | NFR-002 |
| PQ-47 | Herramientas de dictado con Alt der. o Ctrl der.: ¿pulsar o mantener? ¿Qué reciben en latinoamericano? | Pulsar; se envía el lado exacto sin añadir Ctrl. Se valida con Spokenly, Voibe, Wispr Flow y el dictado de Windows. | EDI-009, NFR-004 |
| PQ-48 | IA: proveedor y quién paga la cuota gratuita | La IA es opcional; sin un servicio intermedio propio no hay cuota gratuita, solo clave propia. Las plantillas locales siempre funcionan. | PLA-* |
| PQ-49 | Identidad de tecla: física, virtual o carácter | Identificador canónico de tecla (posición y virtual) con traducción según la distribución destino. Los símbolos que dependen de la distribución se guardan como carácter y se resuelven al enviar. | CAT-001, NFR-004 |
| PQ-50 | Categoría de color: ¿la elige el usuario? | SHOULD: selector de las 10 categorías en el editor (textos nuevos). | EDI |
| PQ-51 | Claves huérfanas (vista «Tira», filtros de plantillas, detectar apps nuevas, modos seguir, fijo y frecuentes) | Se eliminan (§10). Mientras el usuario no lo ratifique, M0 las conserva en `data/i18n/allow-unused.txt` (ver R-02 en §6.1). | IDI-005 |

### 6.1 Propuestas pendientes de ratificar

Propuestas del plano (P1–P6, [§1.4](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario)) y decisiones tomadas al construir M0 que cambian textos, datos o el aspecto del producto. **Ninguna rebaja un requisito.** Mientras el usuario no las ratifique o rechace, rige la columna «Mientras tanto».

| ID | Propuesta | Mientras tanto | Afecta a |
|---|---|---|---|
| P1 | NFR-001 (panel en menos de 1 s) en el arranque al iniciar sesión: sin excepción. Solo si S5 demuestra que no se puede cumplir, se propone mostrar primero la burbuja. | NFR-001 es puerta de publicación sin excepciones. | NFR-001 |
| P2 | Quitar la fila «Una sola ventana» [rSingle] de Sistema: la instancia única es obligatoria por seguridad. | La fila no se construye; [rSingle] está en `allow-unused.txt`. | PQ-35, SIS-003 |
| P3 | La 2.0 sale solo con clave propia de IA; la cuota gratuita llega con el proxy (ADR-0014). | Los flujos PLA con cuota no se muestran. | PLA-003, PQ-48 |
| P4 | SIS-002 se cumple en la 2.0 con el componente de sistema; solo si S14 fracasa se pediría rebajarlo a SHOULD. | Se cumple. | SIS-002 |
| P5 | ARM64 se publica en beta desde el principio y en estable tras la aceptación en un equipo ARM64 físico. | ARM64 solo en beta. | Distribución |
| P6 | Al desinstalar desde Configuración de Windows no hay UI y siempre se conservan los datos; la pregunta se hace en Sistema › Desinstalar y, al reinstalar, en la bienvenida. Requiere dos textos nuevos. | Se implementa así. | NFR-010 |
| R-01 | Textos de plural nuevos (forma `_one`, ES y EN) de comboN, instNoteSome, sugLine, dupHead, twMacro y addMissing; claves nuevas migTProfiles y migTShortcuts; migT pasa a «Importado desde tu versión anterior: {profiles} y {shortcuts}». twMacro_one omite «uno tras otro» (un solo paso). La parte de migT, migTProfiles y migTShortcuts quedó sin efecto por la decisión D1 del usuario (2026-10-03, §6.2). | Están en `data/i18n` (receta `handoff-import.json`); con los argumentos de muestra, el texto visible es idéntico al del paquete. | §9, IDI-001, IDI-004 |
| R-02 | Las claves huérfanas de §10 se conservan en M0 en `allow-unused.txt` en lugar de borrarse. §10 dice «49 claves», pero su lista tiene 58. | Se conservan hasta que se decida PQ-51. | PQ-51, IDI-005 |
| R-03 | Etiquetas nuevas de los catálogos: kgMods «Modificadores»/«Modifiers», kgFn «F1–F12», y las categorías catEdit «Edición»/«Editing», catHist «Historial»/«History», catFile «Archivo»/«File», catSel «Selección»/«Selection», catWin «Ventanas»/«Windows», catVoice «Voz»/«Voice», catNav «Navegación»/«Navigation», catFmt «Formato»/«Formatting», catWeb «Web» y catText «Texto»/«Text». | Están en `data/i18n`. | CAT-001, PQ-50 |
| R-04 | Etiquetas ES/EN de teclas, acciones de mouse, iconos y comandos de sistema como dato en `data/catalogs` (con nombre hablado para los glifos), frente a la letra de IDI-002 e IDI-006, que pide todo texto en `data/i18n`. | Viven en los catálogos. | IDI-002, IDI-006, UIA008 |
| R-05 | Corrección de contraste de TEM-004 también en el tema oscuro (líneas, texto sobre peligro, insignia de categoría activa); separar aviso y peligro en relleno y texto (warn/warnText, danger/dangerText); éxito blanco en alto contraste. Cambia el aspecto: las líneas pasan de α 0,14/0,16 a 0,34/0,43. | Corregido en `data/tokens` (correcciones mínimas documentadas en `extra-tokens.json`). | TEM-002, TEM-004 |
| R-06 | Subir y Bajar brillo en la sección Sistema de la biblioteca (EJE-016), lo que lleva Sistema de 6 a 8 elementos frente al recuento de CAT-003. | Los comandos existen en `system-commands.json`, pero no están en la biblioteca. | EJE-016, CAT-003 |
| R-07 | Variantes EN añadidas a las plantillas Word (Negrita Ctrl+B, Cursiva Ctrl+I, Subrayado Ctrl+U, Reemplazar Ctrl+H) y reparto de `combo-icons` por idioma (Ctrl+G guardar en ES y Ctrl+S en EN). La variante de Correo (Nuevo mensaje Ctrl+Shift+M) la sustituye la revisión de D2 (§6.2). | Así en `data/content` y `data/catalogs`. | CAT-004, CAT-005 |
| R-08 | Las combinaciones bloqueadas o especiales se comparan sin el lado del modificador (Ctrl der.+Alt+Supr está tan reservada como Ctrl+Alt+Supr), a diferencia de REP-001 para los repetidos. | Así en `blocked-combos.json` y sus pruebas, y en `CanonicalChord.ForBlockedComparison` (`tests/Clicalo.Domain.Tests/Keys/CanonicalChordTests.cs`). | EJE-014, REP-001 |
| R-09 | Autoría de las plantillas incluidas: el nombre personal del autor o «Clícalo». | `authors` es «Michael Coaguila». | CAT-006 |
| R-10 | Herramientas de dictado con gancho de teclado (Wispr Flow, Typeless) pueden tragarse el AltGr o el Ctrl derecho que inyecta Clícalo: se observó en la prueba S7-lite de M0. Propuesta: un aviso o una nota en la ayuda. | Se valida en S7 con el equipo sin esas herramientas. | PQ-47, NFR-004 |
| R-11 | Textos nuevos de M2 (ES/EN en `data/i18n`, receta `handoff-import.json`). Del esqueleto andante: hidePanel «Ocultar panel», exitApp «Salir», appName «Clícalo», trayHidden «Clícalo · panel oculto», actionUnavailable «Esta acción aún no está disponible». De la integración: processTaken, undoEditsIn, stepDeleted, itemGone, generalFixed, settingInvalid, backupDamaged, handleLock (dominio); engineFault, incompleteTap, elevatedRefused, keyMissing, releasedOnLock, macroRunning, macroCancelled, tapSent (motor); guardianUnstable (verificación de M2); saveFailT, saveFailD, saveReadOnly, dataUnreadable, textUnavailable, schemaNewer, importInvalid, importTooLarge, sharedTextsExcluded (persistencia); migFailT, migFailD, migRetry, migReportT (migración, retirados por la decisión D1 del usuario del 2026-10-03, §6.2). Marcadores nuevos `{process}`, `{setting}` y `{key}`. | Están en `data/i18n` con el texto que pidió cada paquete; los que aún no tienen quien los muestre (editor, bienvenida, resolución de teclas) esperan a M3. | IDI-001, IDI-002, DAT-002, EJE-003, EJE-010, EJE-013, EJE-015, SEG-006, NFR-005, MIG-004 |
| R-12 | Sin efecto: la sección 7, MIG-006 y EC-MIG-02 quedaron retirados por la decisión D1 del usuario (2026-10-03, §6.2). Proponía dos correcciones de la sección 7 al integrar la migración (el ejemplo de `window_pos` con la escala entera de Qt 5 y el BOM de EC-MIG-02). | — | MIG-006, EC-MIG-02 |
| R-13 | «Fijo/Automático» (`lockProfile`) y el último perfil (`lastProfile`) no se deshacen: son de colocación, como la posición del panel, para que deshacer no cambie la vista bajo el dedo. REG-07 y DAT-006 no los nombran. | Así en `SettingsSchema` (alcance de colocación). | REG-07, DAT-006, PER-001 |
| R-14 | «Dejar solo en Siempre visible» (REP-005) borra de un toque las apariciones repetidas con el mismo nombre, pero no está en la lista cerrada de `destructive-operations.json`, y REG-04 cuenta borrar una aparición repetida como destructivo. Propuesta: añadirlo a la lista (dos toques), o construirlo como movimientos más `DeleteDuplicate`. | No se implementa en M2. | REP-005, REG-04 |
| R-15 | Descripción del chip «Básicos» del kit inicial (decisión D2): kitBasicsD «Copiar, pegar, deshacer, dictar y más, en cualquier app»/«Copy, paste, undo, dictate and more, in any app». El nombre [kitBasics] es el de la decisión. **Ratificada por el usuario el 2026-10-03.** | Está en `data/i18n` (receta `handoff-import.json`); la mostrará la bienvenida de M4. | BIE-006, IDI-001 |

### 6.2 Decisiones del usuario

Decisiones que el usuario, dueño del producto, toma y ratifica sobre el propio catálogo. Mandan sobre todas las demás fuentes (§0.1, nivel 1). Cada requisito afectado lleva la marca «Modificado» o «Retirado por decisión del usuario» con la fecha y el motivo, y los identificadores retirados no se reutilizan.

| ID | Fecha | Decisión | Requisitos afectados | Registro |
|---|---|---|---|---|
| D1 | 2026-10-03 | Clícalo no se basa en nada de la app anterior (Macro Quick Access): se elimina solo la capacidad de leer su `profiles.json` v1 y las piezas que existían únicamente para ella (conversor e importador, sus pruebas y *fixtures*, la orden `anonymize-v1`, la opción `--migrate-v1`, los textos de migración, la pantalla previa de la bienvenida y la tarjeta de migración de Sistema › Copias). Se mantiene todo lo demás del Prototipo v4, incluida la importación y la exportación del formato propio y las migraciones del esquema propio. | Retirados: MIG-001 a MIG-009, BIE-002, COP-001, EC-MIG-01 a EC-MIG-05 y §7. Modificados: REG-08, NFR-013 y TEM-008. Cerradas: PQ-06, PQ-08, PQ-09, PQ-40 y PQ-41. Sin efecto: R-12, y R-01 y R-11 en su parte de migración. | [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md); plano §1.2 (D11) y §6.6; sección `retired` de `data/i18n/handoff-import.json` |
| D2 | 2026-10-03 | **Kit inicial elegible.** En el paso «¿Qué apps usas más?» se ofrece «Básicos» (los atajos universales de General y de Siempre visible del contenido inicial) **marcado** por defecto y las 9 plantillas **sin marcar**. Se puede desmarcar todo y empezar vacío; «Omitir» aplica el valor por defecto (solo «Básicos»), igual que el primer arranque mientras no exista la bienvenida. Las plantillas reconocen varios programas: Navegador = Chrome, Edge, Firefox, Brave y Opera; Correo = Outlook clásico y el nuevo Outlook. El nombre del chip, [kitBasics] «Básicos»/«Basics», es el de la decisión; su descripción [kitBasicsD] la ratificó el usuario el 2026-10-03 (R-15). **Motivo:** Quien omite la bienvenida ya tiene atajos útiles, quien quiere empezar de cero puede hacerlo, y una plantilla de Navegador o Correo sirve con el programa que use cada persona. Al aplicarla, las plantillas se revisaron con la documentación oficial de cada programa: Recargar del Navegador pasa a Ctrl+R (Opera no documenta F5); Correo usa Nuevo correo Ctrl+N y Enviar Ctrl+Entrar, comunes a los dos Outlook (la ayuda en español da Ctrl+D para Responder en el nuevo Outlook y Ctrl+R en el clásico: se mantiene Ctrl+R); y «Videollamada» pasa a llamarse «Zoom», porque sus seis atajos son de Zoom y solo vincula `zoom.exe`. Fuentes en `data/content/README.md`. | BIE-003, BIE-006, BIE-010, CAT-003, CAT-006, PER-002, PER-009, ATJ-007, ATJ-008, PLA-013, DAT-005, EC-PER-04, PQ-39, PQ-44, PQ-45, R-07 | [ADR-0021](../adr/0021-kit-inicial-y-perfiles-con-varios-procesos.md), `data/content/starter.json` |
| D3 | 2026-10-03 | **Soltado con la sesión bloqueada.** Si Clícalo muere con la sesión bloqueada (o con otro escritorio seguro delante) y una tecla pulsada, Sentinel reintenta soltarla en cada latido hasta que el escritorio la acepte (al desbloquear) y **solo entonces** relanza Clícalo, de modo que el proceso nuevo nunca pulsa una tecla que Sentinel vaya a soltar después. El rechazo del escritorio seguro se reintenta sin límite mientras dure; cualquier otro rechazo, como mucho `Timings.Guardian.RefusedReleaseWait` (30 s), y después relanza igualmente. El usuario delegó la forma concreta en el equipo. **Motivo:** antes Sentinel soltaba una sola vez y salía; con la sesión bloqueada el escritorio rechazaba el soltado y la tecla seguía pulsada al desbloquear, sin Clícalo que la soltara (REG-03, REG-05). | Modificados: SEG-006 y SEG-007. | [ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md) (punto 6, aceptado al integrar M2); [D-22](../architecture/deviations.md#d-22--correcciones-del-motor-tras-verificar-m2); [contracts.md](../architecture/contracts.md) |

---

## 7. Esquema v1 exacto (Macro Quick Access) y conversión

**Retirado por decisión del usuario del 2026-10-03** (D1, [ADR-0020](../adr/0020-sin-migracion-desde-macro-quick-access.md)): Clícalo no lee archivos de Macro Quick Access, así que esta sección (archivos reales, esquema v1, gramática de combinaciones, tabla de conversión y atajos con conversión especial) deja de ser requisito. El texto anterior está en la versión 1.0 del catálogo (historial de git). Las lecciones de la app antigua de la sección 8 siguen siendo válidas.

---

## 8. Lecciones de la app antigua (y del prototipo)

**Arquitectura y proceso**
- **L-ARQ-1.** Monolito sin capas: una ventana de unas 1050 líneas mezclaba interfaz, persistencia, inyección, sondeo de procesos y paginación. El prototipo repite el patrón: un objeto de estado y una función de más de 400 líneas. De ahí salieron casi todas las incoherencias: el filtro que no se aplica a Mantener, un solo Mantener a la vez, Alternar que no suma uso, tres variantes del botón de perfil. → NFR-012: cada regla en un solo sitio.
- **L-ARQ-2.** El estado se guardaba en los controles (el perfil activo era el texto de un combo) y el modo era una cadena repartida en muchos show() y hide(). → Estado explícito con una máquina de estados probada.
- **L-ARQ-3.** Sin pruebas, lint, tipos ni revisión. Hubo 21 commits en 9 días directos a main, y 5 meses de cambios sin commitear que cambiaban el comportamiento: Importar pasó a reemplazar sin copia y Restablecer a sustituir 14 perfiles por 3. El binario en uso no coincidía con el código y la app no mostraba su versión. → NFR-013/014/015 y versión visible.
- **L-ARQ-4.** Había dos mecanismos para lo mismo (el lado del modificador; Auto/Fijo como chip y como interruptor con lógica distinta) y código muerto con textos huérfanos. → Un modelo por capacidad y comprobación de claves huérfanas en integración continua.

**Datos**
- **L-DAT-1.** Esquema sin versión y la identidad del perfil era el nombre visible, así que renombrar rompía referencias como pinned_profile. → Id opaco (DAT-004).
- **L-DAT-2.** Carga destructiva: un JSON dañado se sustituía por los perfiles de fábrica y se guardaba al momento. → DAT-003.
- **L-DAT-3.** Los datos vivían junto al .exe y dentro de OneDrive: 3 copias separadas; fallos de guardado por bloqueos (WinError 5 y 32) que solo quedaban en el registro; al extraer un zip reaparecieron los perfiles de fábrica y el script de empaquetado podía publicar datos personales. → Carpeta del usuario, escritura agrupada con reintentos y fallos visibles (DAT-001/002).
- **L-DAT-4.** Escrituras en cada arrastre, cada paso de opacidad o cada tecla de un campo. → Escritura agrupada.
- **L-DAT-5.** Combinaciones guardadas como texto libre partido por «+»: 8 de 210 atajos nunca hicieron lo que decían, sin que nadie lo supiera. → Modelo estructurado de teclas y fallos visibles.
- **L-DAT-6.** El usuario hacía copias a mano (zip, respaldos por idioma). → Copias automáticas con historial.
- **L-DAT-7.** Las combinaciones cambian con el idioma de Office (Negrita Ctrl+N en ES, Ctrl+B en EN): el autor mantenía perfiles duplicados por idioma. → Las variantes por idioma son parte del modelo (CAT-005).

**Envío de entrada y seguridad de teclas**
- **L-ENV-1.** Tres rutas de envío con órdenes y retardos distintos, encontrados a prueba y error (60 ms antes de enviar, 20 ms entre eventos). Se usaba una librería de automatización de pruebas con una API obsoleta, sin marca de tecla extendida y con un mapa de símbolos calculado una sola vez con la distribución propia. → Una ruta única, con parámetros medidos y distribución del destino (NFR-004).
- **L-ENV-2.** Fallar a mitad del envío podía dejar Win o AltGr pulsadas; no había registro de lo pulsado ni soltado al arrancar o cerrar. → SEG-001/006/007.
- **L-ENV-3.** Win soltada demasiado pronto abría la Búsqueda. → Win se mantiene toda la combinación.
- **L-ENV-4.** El cursor en una esquina provocaba una excepción que cerraba la app. → NFR-004/005.
- **L-ENV-5.** Los modificadores derechos y las combinaciones de solo modificadores son **esenciales**: los usan las herramientas de dictado e IA por voz del autor, la categoría que más crece (+8 atajos desde abril). Hay que tratarlos como caso principal.

**Ventanas, foco y detección**
- **L-WIN-1.** El estilo de no activarse se aplicaba **después** de mostrar la ventana; el icono de bandeja activaba la ventana, en contra de la regla; había un campo de texto en una ventana no activable, así que el texto acababa en otra app. → REG-01 desde la creación y BUS-002.
- **L-WIN-2.** Sondeo de la app activa cada 1,5 s, fallos con procesos elevados, sin soporte de apps de la Tienda, y explorer.exe cubría también el escritorio. → Detección por eventos (NFR-002, CAT-007).
- **L-WIN-3.** La posición no se validaba al arrancar y los ajustes solo se hacían contra el monitor principal. → PAN-006.
- **L-WIN-4.** Ocultar con ✕, junto a ⊟, dejaba el panel difícil de recuperar sin teclado. → BUR-005.

**Táctil y accesibilidad**
- **L-ACC-1.** Mouse sintetizado, sin antirrebote, tolerancia al temblor ni toque largo; ayuda en tooltips, que no existen en táctil. → TAC-002, ACC-007.
- **L-ACC-2.** Objetivos de 20–32 px. El tamaño de letra dependía del alto del botón (8 y 7 px con h = 40). → REG-02, TEM-007.
- **L-ACC-3.** Ningún control tenía nombre accesible, así que Acceso por voz no veía los botones. → REG-06.
- **L-ACC-4.** Un control que se pulsa varias veces no debe moverse bajo el dedo (A+ reiterado). → PAN-009.
- **L-ACC-5.** En la v3, la precisión táctil solo se aplicaba a la zona de prueba (bloqueante), y el prototipo v4 aún aplica 2 de 4 criterios. → Un único filtro compartido y probado.

**Seguridad y distribución**
- **L-SEG-1.** El tipo App ejecutaba cualquier línea de comandos y el tipo URL abría cualquier ruta, incluidas rutas de red. Importar reemplazaba sin vista previa: un perfil malicioso ejecutaba código con un toque. → LOG-006/008.
- **L-SEG-2.** Una observación global del teclado activa siempre (como un keylogger) atraía alertas del antivirus, y su atajo global chocaba con Teams. → Observación del teclado solo mientras se graba (EDI-010).
- **L-SEG-3.** Sin firma, sin icono, sin instalador, sin actualizaciones y sin instancia única: dos procesos inyectaban a la vez. Empaquetado de 147 MB con dependencias no declaradas. → NFR-009/010/014/018.
- **L-SEG-4.** El registro crecía sin límite junto al .exe e incluía rutas con el nombre de usuario. → LOG-001.

**Diseño y producto**
- **L-UX-1.** Maquetación a mano con constantes mágicas, estilos duplicados, sin temas y con emojis como iconos. → Tokens y medidas como datos (TEM-002, CAT-001).
- **L-UX-2.** Textos en el código y en un solo idioma. → IDI.
- **L-UX-3.** Documentación desactualizada y códigos de un plan que no estaba en el repositorio. → La documentación se versiona con el código.
- **L-UX-4.** La Auditoría partió de un diagnóstico técnico falso (creía que la v1 usaba Tkinter; usaba PyQt5). → Los hechos técnicos se comprueban contra el código y los datos reales, como en §7.

---

## 9. Textos: correcciones y claves nuevas necesarias

**Correcciones**
- [creator] → «Creador de Clícalo».
- [fbLogD] y el cuerpo del correo → `clicalo.log`.
- [orderHint] → se reordena «con los botones o arrastrando».
- [rAutoD] → «a los 30 s de cada cambio (últimas 12)».
- [rCrashD] → el mismo significado en ES y EN.
- [autoReleaseD] → que no prometa soltar siempre al cambiar de app.
- [aiPrivacy] y [consentD] → los 4 datos enviados.
- [voiceNums] y [vh3] → un solo nombre.
- ~~migT → variables {profiles} y {shortcuts}.~~ Sin efecto: migT se retiró (D1, §6.2).
- «Auto» del candado → «Se pliega».
- Unificar «Copia automática» en EN ([rAuto] y [stBackup]).
- Pasar a los archivos de idioma los 17 textos escritos en el código (IDI-002).
- Con plural: comboN, instNoteSome, sugLine, dupHead, twMacro y addMissing (migT se retiró, D1).

**Claves nuevas (ES y EN)**
- **Frecuentes:** estado vacío (título, subtítulo y botón); límite de 9 fijados.
- **Seguridad de teclas:** soltado por bloqueo o suspensión; soltado por relanzar o salir.
- **App elevada:** «No se envió: {app} es de administrador».
- **Macro:** «Ejecutando {name} · paso {index} de {total} · toca para detener»; «Macro detenida».
- **Captura:** Cancelar en el aviso del panel.
- **Vinculación:** confirmar proceso duplicado.
- **Atajos:** «Este atajo está incompleto»; «Esta tecla no existe en tu teclado actual»; «Deshacer cambios en {name}».
- **Filtro táctil:** «Ignorado: deslizaste»; reiniciar contadores de la zona de prueba.
- **IA:** errores unavailable, invalid y badkey; sección IA en General (consentimiento y desactivar).
- **Actualizaciones:** estados de error.
- **Copias:** «Sin copias aún»; tipos «Antes de actualizar» y «Antes de migrar»; resumen de importación.
- ~~**Migración:** pantalla previa, «Reintentar migración», informe y «Revisar».~~ Retiradas por la decisión D1 del usuario (2026-10-03, §6.2).
- **Pestaña:** bloquear posición del asa; «Ver la guía de la pestaña»; «Se pliega».
- **Bandeja:** Pausar, Reanudar y Soltar todo.
- **Mover panel:** posiciones predefinidas.
- **Editor:** «Pedir confirmación antes de ejecutar»; «Fijado en Frecuentes»; «Texto privado»; categoría de color.
- **Probar ahora:** «Abre la app donde quieras probarlo»; destino cerrado; confirmar atajo peligroso.
- **Otros:** «Bloquear equipo» como acción de sistema; ajuste de tiempos (×1, ×2, ×3); convivencia con Macro Quick Access.

---

## 10. Fuera de alcance (no son requisitos)

- **Código muerto del prototipo:**
  - la vista «Tira» (showTira, tiraItems);
  - los modos de seguimiento app, Frecuentes y fijo, con su menú (setFollow, modeOpts, fixOpts, profMenu);
  - la fila de pestañas horizontal (tabs, fixTabs);
  - dockPick, dap, dockApp y cycleProfile;
  - el selector Izq./Der. por modificador (sideRows, toggleSides), sustituido por EDI-009;
  - sysStatus.
- **Claves huérfanas asociadas** (a eliminar): dStrip/D, mFollow/mFreq/mFixed, modeFollowT/FreqT/FixedT, fAll/fInst/fAvail/tplAll, asTitle/asDesc, aiTitle/aiSub/aiShort/aiPh2, dupTitle/dupBody/dupHint/dupIn/dupKeep/dupChangeT/itsFine/changeCombo, pinAll/pinAllOn/pinAllOff/pinBar, navRel/navUpd/navFb/navAbout, relTitle/relSub, updSub, fbSub, writeMe, shareD, testSafe, voiceHint, whatShows, fixWhich, protectT, secLook, libTitle, fromScratch, repeated, lockOn, activeShort, install, bSteps, tplSub, where, onlyHere, addStep y sidesA. Se conserva [useOther], que pasa a REP-006.
- **Simulación del prototipo:** el escritorio y la barra de tareas falsos, APPS, PROC, KNOWN, el token `desk`, los chips fijos de páginas y programas, los usos iniciales simulados, las copias simuladas, el marco `x-dc`/`support.js` y la carga de fuentes desde la red.
- **Prescripciones de arquitectura y tecnología de docs/01 y docs/10**, y las APIs concretas: no son vinculantes por instrucción del usuario. Solo se conservan como **necesidades** (§3).