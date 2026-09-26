# 09 · Criterios de aceptación

Marca cada casilla probando en un equipo real con pantalla táctil.

## Motor
- [ ] Tocar cualquier botón del panel **no cambia la ventana activa** (Word sigue con el foco y recibe el atajo).
- [ ] El perfil cambia al pasar a Word, Chrome o VS Code en modo Auto; no cambia en Fijo ni estando en Frecuentes.
- [ ] Desde Frecuentes, el botón de perfil vuelve al perfil correcto con un toque.
- [ ] Pulsar, Mantener, Alternar, Texto (ñ, á, emoji), Mouse (clics y desplazamiento en las 4 direcciones), Macro (con esperas, cancelable), Web y App funcionan.
- [ ] Ctrl der. y AltGr se envían como teclas distintas de Ctrl y Alt.
- [ ] Los atajos con letras funcionan con las distribuciones de teclado ES y EN.
- [ ] El modo compatible envía scancodes.
- [ ] Con una app elevada aparece el aviso; tras «Iniciar como administrador», funciona.
- [ ] «Capturar la próxima app que uses» vincula el proceso correcto, también con apps de la Tienda.
- [ ] Ctrl+Alt+Supr y Win+L muestran el aviso rojo; Win+G, el amarillo.

## Seguridad de teclas
- [ ] Con una tecla pulsada aparece la franja o botón «Soltar todo», el panel no se atenúa y la burbuja muestra el anillo rojo.
- [ ] Las teclas se sueltan solas: tras el tiempo configurado, al cambiar de app, al bloquear la sesión (Win+L), al suspender y al cerrar la app a la fuerza. Tras relanzarla, no queda ninguna tecla pegada.

## Panel
- [ ] Las tres vistas y los tres tamaños se ven como en el prototipo. Ningún botón se ve cortado a la mitad en pantallas de 768 px de alto.
- [ ] Páginas: al deslizar y con ◀ ▶; los puntos se actualizan.
- [ ] En la vista compacta, la barra de avisos aparece tras cada acción, con Repetir.
- [ ] Toque largo: el menú funciona (fijar, quitar, editar); no aparece en botones de tipo Mantener.
- [ ] En modo edición, la × pide confirmación y se puede deshacer.
- [ ] Un perfil vacío muestra el estado vacío con «Añadir atajo».
- [ ] Siempre visible respeta el límite de 4 o de 8, con paginación.
- [ ] En la pestaña:
  - [ ] Funciona en los 4 lados y se arrastra por el borde.
  - [ ] Guarda la posición por lado.
  - [ ] Las ventanas al costado se abren junto a su botón.
  - [ ] La guía aparece la primera vez.
  - [ ] Se pliega sola o se queda abierta según el candado.
- [ ] El atenuado y la opacidad aplican al panel, la barra y la burbuja. Las excepciones de la lista de `docs/04` funcionan.
- [ ] La posición se guarda por monitor y se corrige al desconectar un monitor.

## Centro de control
- [ ] Las 6 secciones corresponden a `docs/05`, sin elementos duplicados.
- [ ] Todo objetivo táctil ≥ 44 px (verificar con Accessibility Insights).
- [ ] Esc: primero sale del campo y después cierra la ventana.
- [ ] La ventana es redimensionable, con mínimo 760×520.
- [ ] En el editor:
  - [ ] El orden de las teclas se respeta.
  - [ ] Limpiar tiene deshacer.
  - [ ] El icono se asigna solo según el nombre.
  - [ ] Los pasos de macro se editan y reordenan.
  - [ ] Un borrador vacío se descarta al salir.
  - [ ] Eliminar pide doble toque.
- [ ] Repetidos: la navegación ‹ › funciona; eliminar de un perfil y «Está bien así» funcionan.
- [ ] Probar: la animación es correcta para los 8 tipos; «Probar ahora» vuelve con la pregunta.
- [ ] Plantillas:
  - [ ] Consentimiento, cuota y los tres errores.
  - [ ] Vista previa editable, sin solapamientos.
  - [ ] Plantilla ya instalada: «Añadir N que faltan».
  - [ ] Perfil vacío con icono.
  - [ ] Importar perfil.
- [ ] Sistema: las 3 pestañas; migración visible; importar con Combinar o Reemplazar; Volver a la versión anterior.

## Accesibilidad e idioma
- [ ] Acceso por voz: «mostrar números» y «clic 4» ejecutan el botón correcto; «clic Negrita» también.
- [ ] Narrador lee el nombre, el estado (Auto/Fijo, ACTIVO) y los avisos.
- [ ] Los tres temas y el alto contraste de Windows cumplen el contraste; el tema Auto sigue a Windows.
- [ ] El cambio ES↔EN es en caliente y no deja ningún texto sin traducir (comparar las claves de los JSON).

## Datos
- [ ] Migración desde un `profiles.json` v1 real, sin pérdidas, con la copia original guardada.
- [ ] Un `data.json` corrupto se recupera desde la última copia.
- [ ] Los textos se guardan cifrados; el registro no contiene textos ni títulos.
