# Guion de aceptación manual

Lo que **solo** se puede comprobar con la persona que usa Clícalo o en una máquina limpia. Todo lo demás lo
comprueban las pruebas automáticas (`cl check`, `cl desk`) y no se repite aquí.

No hay una orden `cl accept` ([D-29](../architecture/deviations.md#d-29--sin-cl-states-ni-cl-accept)): este
documento es el guion. `cl trace` lee los identificadores que aparecen aquí: un requisito MUST sin prueba
automática que este guion nombra figura como «solo en el guion manual».

## Cuándo y cómo

- **Cuándo.** Antes de cada versión estable. Para una beta bastan las partes 1, 2 y 9.
- **Quién.** Las partes 4 a 8 las hace la persona usuaria, con su pantalla táctil y su voz. Las partes 1 a 3
  las puede hacer cualquiera.
- **Dónde.** Las partes 1 a 3 y la 9, en una **máquina limpia**: una máquina virtual o un equipo de pruebas con
  Windows recién instalado, sin el SDK de .NET y sin ningún Clícalo anterior. Nunca sobre la instalación de uso
  diario. Hazlo una vez en Windows 11 y otra en Windows 10 22H2.
- **Con qué.** El `Setup.exe` que deja `cl package` ([guía de publicación](release.md)), antes de publicarlo.
- **Resultado.** En cada paso, «sí», «no» o «no aplica». Un «no» en un paso bloquea la versión hasta que se
  corrija o hasta que el usuario decida. Apunta el resultado en la tabla del final y cópiala en el PR de la
  versión.

Usa siempre datos inventados. No importes copias con datos personales.

## 1. Instalar en una máquina limpia

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 1.1 | Abre `Setup.exe` | SmartScreen avisa de que no reconoce la app. «Más información» y «Ejecutar de todas formas» lo abren. No pide permisos de administrador | NFR-009, NFR-010 |
| 1.2 | Espera a que termine | Clícalo se abre solo. El programa está en `%LocalAppData%\Clicalo.App` y aparece en Configuración › Aplicaciones | NFR-010 |
| 1.3 | Mira qué se abre | La bienvenida, en el paso inicial, y el panel | BIE-001 |
| 1.4 | Termina la bienvenida con «Básicos» marcado | «¡Todo listo!». El panel tiene los atajos básicos | BIE-003 |
| 1.5 | Abre el Bloc de notas, escribe una palabra, selecciónala y toca «Copiar» y «Pegar» en el panel | El texto se copia y se pega. El Bloc de notas sigue delante todo el tiempo | REG-01, EJE-001 |
| 1.6 | Sal de Clícalo desde el icono junto al reloj y ábrelo desde el menú Inicio | Se abre sin la bienvenida y con los mismos atajos. El panel se ve en menos de un segundo | NFR-001, REG-08 |
| 1.7 | Ábrelo otra vez desde el menú Inicio con uno ya abierto | No hay dos paneles: se muestra el que ya estaba | SIS-003 |
| 1.8 | Repite 1.1 a 1.6 en la otra versión de Windows (10 22H2 u 11) | Lo mismo | NFR-011 |

## 2. Actualizar

Hace falta una versión anterior instalada del mismo canal y una versión nueva publicada en una *release* de
prueba.

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 2.1 | Sistema › Actualizaciones › «Buscar actualizaciones» | Ofrece la versión nueva con sus novedades en el idioma de la interfaz. Sistema muestra el contador 1 | ACT-001, ACT-004 |
| 2.2 | «Instalar ahora» | Clícalo se cierra y se abre en la versión nueva. Los atajos no cambian. En Copias › Historial hay una copia «Antes de actualizar» | ACT-003, COP-004, REG-08 |
| 2.3 | Con «Actualizar automáticamente» y sin «Avisarme antes», deja el panel 5 minutos sin tocar con otra versión publicada | Se instala sola. Antes de esos 5 minutos no | ACT-002, ACT-003 |
| 2.4 | Mantén pulsado un botón «Mantener» y pide instalar | No instala con algo pulsado: lo suelta todo antes de salir | ACT-003, REG-03 |
| 2.5 | Desconecta la red y busca actualizaciones | Tarjeta de error «Sin conexión» con «Reintentar». Nada se rompe | ACT-001 |
| 2.6 | Cambia el canal a «Beta» y vuelve a «Estable» | Ofrece la beta más nueva. De vuelta en «Estable» no baja de versión sola | ACT-002, NFR-010 |
| 2.7 | Solo si hay una versión anterior conservada: «Volver a la versión…» con dos toques | Vuelve a la anterior al reiniciar. Los perfiles se conservan. En la 2.0.0 la fila no existe: «no aplica» | ACT-005 |

## 3. Desinstalar y volver a instalar

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 3.1 | Activa «Iniciar con Windows» y reinicia el equipo | Clícalo se abre solo al iniciar sesión, sin permisos de administrador | SIS-002 |
| 3.2 | Configuración › Aplicaciones › Clícalo › Desinstalar | Se desinstala sin preguntar nada. `%AppData%\Clicalo` sigue en su sitio. Ya no arranca con Windows | NFR-010, REG-08 |
| 3.3 | Instala de nuevo | Clícalo encuentra los atajos de antes y pregunta si conservarlos o empezar de cero | NFR-010, BIE-001 |
| 3.4 | Elige «Empezar de cero» | Empieza vacío y hay una copia de lo anterior en Copias › Historial | REG-08, COP-004 |
| 3.5 | Sistema › «Desinstalar Clícalo», sin marcar borrar | Se desinstala y los datos se quedan | NFR-010 |
| 3.6 | Instala, y desinstala desde Sistema marcando «Borrar también mis atajos y ajustes» | Pide guardar antes una copia. Si cancelas la copia, no se desinstala ni se borra nada | NFR-010, REG-08, REG-04 |

## 4. Programas de administrador y la confirmación de Windows

Hace falta la copia instalada. Abre como administrador un programa de prueba, por ejemplo el Bloc de notas
con «Ejecutar como administrador».

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 4.1 | Con ese programa delante, toca un botón del panel | No se envía nada. El panel avisa de que el programa es de administrador y ofrece reabrir Clícalo | EJE-013 |
| 4.2 | Sistema › «Reabrir como administrador» y confirma en la ventana de Windows | Clícalo se cierra y se abre de nuevo. El mismo botón ahora sí llega al programa | SIS-002, EJE-013 |
| 4.3 | Repite 4.2 y toca «No» en la ventana de Windows | Todo sigue igual. La barra de estado lo explica | SIS-002 |
| 4.4 | Confirma la ventana de Windows **sin teclado**: con el dedo y, otra vez, con Acceso por voz («clic Sí») | Se puede con las dos. Si con la voz no se puede en tu equipo, apúntalo: la guía de usuario lo avisa | REG-05, SIS-002 |
| 4.5 | Con Clícalo elevado, mantén un «Mantener» y cambia de app | Se suelta al cambiar, igual que sin elevar | REG-03, SEG-005 |
| 4.6 | Reinicia el equipo | Clícalo arranca sin permisos de administrador | SIS-002 |
| 4.7 | Desde una copia no instalada (`cl run`), busca «Reabrir como administrador» | No eleva: dice que solo la copia instalada puede hacerlo | SIS-002, LOG-007 |

## 5. Narrador

Activa Narrador (Ctrl + Win + Entrar o Configuración › Accesibilidad).

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 5.1 | Recorre el panel con el dedo (explorar tocando) | Cada botón dice su nombre y, si lo tiene, su número de voz. Ninguno dice solo «botón» | REG-06, ACC-001 |
| 5.2 | Activa un «Alternar» y pasa por él | Dice que está activo. Al soltarlo, deja de decirlo | REG-06, ACC-003 |
| 5.3 | Toca «Auto» | Dice «Fijo» y qué significa. Al tocar otra vez, «Auto» | REG-06, PER-006 |
| 5.4 | Mantén un «Mantener» | Lee la franja roja con las teclas pulsadas, sin que la busques. «Soltar todo» se puede activar con Narrador | REG-06, REG-03, SEG-002 |
| 5.5 | Provoca un aviso (elimina un atajo y deshaz) | Lee el aviso cuando aparece, una sola vez | REG-06, AVI-001 |
| 5.6 | Abre el Centro de control y recorre las seis secciones con Tab | El orden es lógico. Se sabe en qué sección estás. Ningún control queda sin nombre | ACC-004, NFR-007 |
| 5.7 | Repite la bienvenida con Narrador activo | Se puede terminar entera. Lee en qué paso estás y qué opciones están marcadas | REG-06, BIE-001 |
| 5.8 | Usa el panel en la vista «Pestaña» | Lee el asa, la barra y sus botones | REG-06, PES-001 |

## 6. Acceso por voz y recorrido sin teclado

En Windows 11, Acceso por voz. En Windows 10, Reconocimiento de voz de Windows. **No toques un teclado físico
en toda esta parte.**

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 6.1 | Di «mostrar números» con el panel visible | Cada botón del panel tiene su número de Windows | REG-06 |
| 6.2 | Di «clic» y el nombre de un botón, por ejemplo «clic Negrita», con Word delante | El atajo llega a Word. Word sigue delante | REG-06, REG-01 |
| 6.3 | Activa «Números para voz» y di «clic 4» | Se ejecuta el botón que muestra el 4. La numeración sigue en la página siguiente | ACC-009, ACC-010 |
| 6.4 | Oculta el panel y di «clic Clícalo» | El panel vuelve | BUR-005 |
| 6.5 | Minimiza a burbuja y recupéralo con la voz y, otra vez, con el dedo | Vuelve las dos veces | BUR-001, BUR-005 |
| 6.6 | Recorrido completo, solo con dedo y voz: repite la bienvenida, crea un perfil vacío, crea un atajo de cada uno de los ocho tipos, vincula el perfil a una app y prueba un atajo con «Probar ahora en…» | Todo se puede hacer. En ningún paso hace falta un teclado | REG-05 |
| 6.7 | Elimina uno de esos atajos con la voz | Pide el segundo toque («Confirmar») y después ofrece «Deshacer» | REG-04, REG-07 |
| 6.8 | Pon «Más tiempo para confirmar y leer avisos» en ×3 y repite 6.7 | Hay tiempo de sobra para decir la segunda orden | ACC-006 |

## 7. Dictado

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 7.1 | En el editor, toca el micrófono del nombre y dicta | Se abre el dictado de Windows y el nombre queda escrito en el campo | ACC-011, REG-05 |
| 7.2 | Haz lo mismo en un atajo «Texto», en un paso de macro, en el nombre de un perfil, en «Crear con IA» y en «Opinión y contacto» | En todos hay micrófono y en todos se dicta | ACC-011 |
| 7.3 | En el panel, toca la lupa y dicta | La búsqueda recibe el texto. Al cerrar la búsqueda, el foco vuelve a la app de antes y lo siguiente que dictes cae en ella | BUS-002, BUS-003, REG-01 |
| 7.4 | Crea un botón «Mantener» con la tecla de tu herramienta de dictado y úsalo como «pulsar para hablar» | Dicta mientras lo mantienes. Al levantar el dedo, se suelta | EJE-004, REG-03 |
| 7.5 | En el campo de la clave de IA | No hay micrófono: hay un botón «Pegar» | ACC-011 |

## 8. La pantalla táctil de verdad

Con el dedo, en el equipo táctil. Estas cosas no se pueden simular bien.

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 8.1 | Toca botones del panel con Word o el navegador delante, muchas veces seguidas | La app de delante nunca pierde el foco. No se abre el teclado táctil. No aparece el círculo de toque de Windows | REG-01, ACC-007 |
| 8.2 | Haz un toque largo en un botón | Se abre el menú del botón, no un clic derecho de Windows | ACC-007, CUA-014 |
| 8.3 | Apoya la palma sobre el panel | No se activa nada | ACC-007 |
| 8.4 | Mantén el dedo sobre un botón mientras el panel cambia (llega un aviso, cambias de app con la otra mano) | El botón bajo tu dedo no se mueve ni cambia por otro | NFR-019, PAN-009 |
| 8.5 | En la cuadrícula de perfiles, en una ventana al costado de la Pestaña y en el Centro de control, desliza para desplazar | Se desplaza y no se activa ningún botón | TAC-004 |
| 8.6 | Vista «Pestaña» en el borde derecho: desliza desde fuera de la pantalla hacia dentro, como para abrir las notificaciones de Windows, y después toca el asa | El gesto de Windows sigue funcionando. Un toque en el asa abre la barra. No se confunden | PES-001, PES-002 |
| 8.7 | Lo mismo con la pestaña en el borde izquierdo, arriba y abajo | Igual en los cuatro lados. La barra se abre hacia dentro y se ve entera | PES-001, PES-005 |
| 8.8 | Arrastra el asa a lo largo del borde, cierra Clícalo y ábrelo | El asa está donde la dejaste | PES-016 |
| 8.9 | En la vista «Pestaña», provoca cada aviso: arma un «Confirmar», deja que una tecla se suelte sola, entra en «Modo prueba» y deshaz algo | Cada aviso se ve junto a la barra o junto al asa | PES-014 |
| 8.10 | Con un temblor o un toque impreciso reales, prueba «Temblor leve» y «Temblor fuerte» en la «Zona de prueba» y en el panel | El filtro perdona lo que promete: no hay dobles toques ni toques perdidos | TAC-001, TAC-005 |
| 8.11 | Abre una carpeta en el Explorador, y después toca el escritorio y la barra de tareas | El perfil del Explorador solo aparece con la ventana de carpeta | CAT-007 |
| 8.12 | Si hay un teclado a mano: Esc con un menú del panel abierto, con el foco en un campo y con el Centro de control abierto | Cierra el menú, sale del campo y cierra el Centro de control, en ese orden. En la bienvenida, Esc no la cierra | PAN-010 |
| 8.13 | Vista «Pestaña»: abre el menú de un botón con un toque largo y toca otra app. Ábrelo otra vez y, si hay un teclado a mano, pulsa Esc. Después pulsa Esc en la app de delante | El menú se cierra las dos veces y la app de delante sigue delante. Con el menú ya cerrado, Esc vuelve a llegar a la app | CUA-014, REG-01 |
| 8.14 | Mira el icono de Clícalo junto al reloj con el panel visible, con el panel oculto y en pausa. Si puedes, con la pantalla al 100 % y al 150 o 200 % | Es el icono de Clícalo, nítido. Con el panel oculto o en pausa se ve atenuado y su texto lo dice | BUR-003, BUR-004 |
| 8.15 | Mira el icono de `Setup.exe`, el de Clícalo en el menú Inicio y el de Configuración › Aplicaciones | Es el icono de Clícalo en los tres | BUR-003, NFR-010 |
| 8.16 | Vista «Pestaña» con tantos «Fijos» que su ventana se desplace: desliza empezando sobre un «Mantener». Después deja el dedo quieto sobre él | Al deslizar se desplaza y no se pulsa nada. Con el dedo quieto, mantiene; al levantar, suelta | TAC-004, EJE-004, REG-03 |
| 8.17 | Vista «Pestaña» arriba y abajo, en S, M y L: mira la barra, el candado, «Auto» y «Fijos». Repite en los lados | Nada se corta. La barra queda a 12 px del borde y se ve entera | PES-005, PES-007, PES-008 |
| 8.18 | En pausa, abre el Centro de control desde el icono junto al reloj y entra en «Probar» de un atajo | El Centro de control se abre y Clícalo sigue en pausa. La tarjeta lo explica y su botón es «Reanudar»; al tocarlo vuelve el panel | BUR-004 |

## 9. Alto contraste, pantallas y escala

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 9.1 | Activa un tema de contraste de Windows (Configuración › Accesibilidad › Temas de contraste) | Clícalo pasa a alto contraste con los colores de Windows, sin transparencia. Todo se lee | TEM-001, TEM-004 |
| 9.2 | Con ese tema, mira un «Alternar» activo, «Auto» y «Fijo», la franja roja y un botón armado | Cada estado se distingue sin color: por un icono, un texto o un borde | ACC-003 |
| 9.3 | Vuelve al tema normal y elige «Alto contraste» en «Tema» | Lo mismo, sin tocar Windows | TEM-001 |
| 9.4 | Cambia Windows entre claro y oscuro con «Tema» en «Auto» | Clícalo cambia al momento, sin reiniciar | TEM-001 |
| 9.5 | Pon el «Tamaño de texto» al máximo | Nada se corta ni se sale del panel ni del Centro de control | ACC-008, NFR-017 |
| 9.6 | Con dos pantallas a distinta escala, arrastra el panel de una a otra | Se ve nítido y del mismo tamaño real en las dos. Nunca queda fuera de la pantalla | ACC-008, NFR-017, PAN-002 |
| 9.7 | Desconecta la segunda pantalla con el panel en ella | El panel vuelve a la pantalla que queda, entero | NFR-011, PAN-002 |
| 9.8 | Mueve la barra de tareas a otro borde o actívale el ocultado automático. En un 2 en 1, gira la pantalla | El panel y la pestaña se recolocan dentro del área útil | NFR-011 |
| 9.9 | Cambia el tamaño del Centro de control, muévelo a la otra pantalla, ciérralo y ábrelo | Se abre con el mismo tamaño y en la misma pantalla | CCM-001 |

## 10. Teclas que no se quedan pulsadas, con programas reales

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 10.1 | Activa un «Alternar» de Ctrl y bloquea la sesión (Win + L). Desbloquea y escribe en el Bloc de notas | Escribe letras normales: Ctrl no quedó pulsado. El panel dice que se soltó al bloquear | REG-03, SEG-006 |
| 10.2 | Lo mismo suspendiendo el equipo | Igual | REG-03, SEG-006 |
| 10.3 | Con un «Alternar» activo, termina `Clicalo.exe` desde el Administrador de tareas | La tecla se suelta y Clícalo se abre de nuevo solo | REG-03, SEG-007 |
| 10.4 | Lo mismo, pero bloquea la sesión justo antes de terminar el proceso | Al desbloquear, la tecla no está pulsada y Clícalo vuelve a abrirse | SEG-006, SEG-007 |
| 10.5 | Con el panel sin responder (si llega a pasar), icono junto al reloj › «Soltar todo» | Suelta las teclas | REG-03, SEG-003 |

## 11. IA con una clave real

Solo con una clave de API propia. Cuesta dinero al dueño de la clave: basta con dos generaciones.

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 11.1 | Sin clave, escribe un programa y toca «Generar con IA» | Tarjeta «Falta tu clave» con tres salidas. No se envía nada | PLA-006 |
| 11.2 | Pega la clave | Dice que se guardó en el Administrador de credenciales. La clave no se vuelve a ver en ningún sitio | PLA-003, LOG-003 |
| 11.3 | Genera para un programa conocido | La primera vez pide permiso y dice qué cuatro datos se envían. Después aparece la propuesta en vista previa | PLA-004, PLA-005, PLA-008 |
| 11.4 | Mientras genera, mueve el Centro de control y toca botones del panel | Nada se queda bloqueado. El botón dice «Generando…» | NFR-003 |
| 11.5 | Instala dos atajos de la propuesta y pruébalos en el programa | Hacen lo que dicen | PLA-005 |
| 11.6 | Cambia la clave por una falsa y genera | Tarjeta de clave no válida. Nada se instala | PLA-006 |
| 11.7 | «Borrar clave» | Pide dos toques. Después, en el Administrador de credenciales de Windows ya no está | PLA-003, REG-04 |
| 11.8 | Abre `clicalo.log` (Acerca de › vista previa del registro) | No aparecen ni la clave ni el nombre del programa pedido | LOG-001, NFR-008 |

## 12. Nada sale del equipo y el tamaño de los datos

| Paso | Qué haces | Qué debe pasar | Requisitos |
|---|---|---|---|
| 12.1 | En la máquina limpia, apaga «Actualizar automáticamente» y usa Clícalo diez minutos sin IA. Mira las conexiones de `Clicalo.exe` en el Monitor de recursos | Ninguna conexión de red | LOG-002, NFR-008 |
| 12.2 | Importa una copia de prueba con 50 perfiles y 2000 atajos inventados | El panel, la búsqueda y el Centro de control responden igual de rápido | NFR-016, NFR-003 |

## Registro del resultado

Copia esta tabla en el PR de la versión y rellénala.

| Versión | Fecha | Equipo y Windows | Quién | Partes hechas | Pasos con «no» | Decisión |
|---|---|---|---|---|---|---|
| | | | | | | |
