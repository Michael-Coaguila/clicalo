# Guía de usuario de Clícalo

Clícalo es un panel flotante para Windows. Tiene botones grandes. Cada botón hace un atajo de teclado, escribe
un texto o hace una acción del mouse. Lo usas con el dedo o con la voz. No hace falta teclado.

Esta guía es para la versión 2.0. Usa frases cortas y pasos numerados. Los nombres entre «comillas» son los
que ves en pantalla y los que dices con la voz.

## Contenido

1. [Instalar](#1-instalar)
2. [Primer arranque y bienvenida](#2-primer-arranque-y-bienvenida)
3. [El panel y sus vistas](#3-el-panel-y-sus-vistas)
4. [Crear atajos](#4-crear-atajos)
5. [Plantillas e inteligencia artificial](#5-plantillas-e-inteligencia-artificial)
6. [Soltar teclas y la franja roja](#6-soltar-teclas-y-la-franja-roja)
7. [Copias de seguridad](#7-copias-de-seguridad)
8. [Actualizaciones y volver a la versión anterior](#8-actualizaciones-y-volver-a-la-versión-anterior)
9. [Iniciar con Windows y programas de administrador](#9-iniciar-con-windows-y-programas-de-administrador)
10. [Desinstalar](#10-desinstalar)
11. [Narrador y Acceso por voz](#11-narrador-y-acceso-por-voz)
12. [Si algo no va bien](#12-si-algo-no-va-bien)
13. [Privacidad](#13-privacidad)

## 1. Instalar

Necesitas Windows 10 (versión 22H2) o Windows 11.

1. Abre la página de versiones: <https://github.com/Michael-Coaguila/clicalo/releases>.
2. En la versión más reciente, descarga el archivo que termina en `Setup.exe`.
3. Abre el archivo descargado.

### Qué dice Windows al instalar

El instalador de la 2.0 **no lleva firma de código**. Por eso Windows muestra un aviso azul de SmartScreen:
«Windows protegió su PC». Es normal en esta versión.

1. Toca «Más información».
2. Comprueba que el nombre del archivo es el que descargaste.
3. Toca «Ejecutar de todas formas».

Con Acceso por voz: di «clic Más información» y después «clic Ejecutar de todas formas».

Descarga el instalador solo de la página oficial de arriba. Si el aviso aparece con un archivo que llegó por
otro camino, no lo abras.

### Qué hace el instalador

- Instala Clícalo solo para tu usuario. No pide permisos de administrador.
- El programa queda en `%LocalAppData%\Clicalo.App`.
- Tus atajos y ajustes quedan en `%AppData%\Clicalo`. Son tuyos: ninguna actualización los borra.
- Al terminar, Clícalo se abre solo.

## 2. Primer arranque y bienvenida

La primera vez se abre la ventana «Bienvenida a Clícalo». Son pocos pasos. En cada uno puedes tocar
«Omitir».

1. **«Tus atajos, a un toque».** Explica qué es el panel.
2. **«¿Cómo usas tu equipo?»** Puedes marcar varias: «Pantalla táctil», «Control por voz», «No puedo usar el
   teclado», «Tengo temblor» y «Mouse o trackball». Clícalo ajusta el tamaño del panel y la precisión del
   toque por ti.
3. **«¿Qué apps usas más?»** «Básicos» viene marcado: copiar, pegar, deshacer, dictar y más, para cualquier
   app. Marca además las apps que uses (Word, Excel, Navegador, Correo y otras). Cada una instala sus
   atajos. Puedes desmarcar todo y empezar vacío.
4. **«¿Cómo quieres el panel?»** Elige la vista. La puedes cambiar después.

Al terminar verás «¡Todo listo!».

- Si tocas «Omitir», Clícalo instala solo «Básicos».
- Para repetirla: Centro de control › «General y panel» › «Ver la bienvenida otra vez». Repetirla nunca
  quita atajos: solo añade lo que marques.

## 3. El panel y sus vistas

### Lo más importante

**El panel nunca te quita la app.** Tocas un botón y el atajo llega a Word, al navegador o a la app que
tengas delante. El cursor sigue donde estaba.

### Partes del panel

De arriba abajo, en la vista «Completa»:

- **Cabecera.** El asa ⋮⋮ mueve el panel. Al lado están el nombre del perfil, el botón «Auto» o «Fijo», la
  lupa de «Buscar», el engranaje de «Ajustes rápidos» y «Minimizar».
- **Selector de perfil.** ★ «Frecuentes» y tu perfil.
- **Fila «Siempre visible».** Botones que se ven en cualquier app.
- **Botones del perfil.** Si no caben, hay páginas.
- **«Teclas fijas».** Ctrl, Alt, Shift y Win. Tocas una y se suma al siguiente botón.
- **Avisos.** Dicen qué pasó. Ahí están «Repetir la última acción» y «Deshacer».

### Usar un botón

- **Un toque** lo ejecuta.
- **Toque largo** abre su menú: «Editar», «Fijar en Frecuentes» y más.
- Un botón «Mantener» pulsa las teclas mientras tu dedo está encima. Al levantar, las suelta.
- Un botón «Alternar» pulsa con el primer toque y suelta con el segundo.

### Perfiles: «Auto» y «Fijo»

Un perfil es un grupo de botones para una app.

- **«Auto» (azul).** El panel cambia de perfil solo cuando cambias de app.
- **«Fijo» (rojo).** El perfil se queda aunque cambies de app. Toca otra vez para volver a «Auto».
- **«General»** se usa en las apps que no tienen perfil.
- **★ «Frecuentes»** muestra los botones que más usas.

### Buscar

Toca la lupa y escribe o dicta el nombre. Busca en todos los perfiles. Es el único sitio del panel donde se
escribe.

### Vistas

Se cambian en «Ajustes rápidos» › «Vista».

| Vista | Cómo es | Para qué |
|---|---|---|
| «Completa» | El panel entero | Verlo todo |
| «Compacta» | Botones más bajos, sin la línea de teclas y con la fila fija solo con iconos | Ocupar poco |
| «Pestaña» | Un asa fina en el borde de la pantalla. La tocas y sale una barra de botones | No tapar el trabajo |

Hay dos formas más de apartarlo:

- **«Minimizar»** lo convierte en una burbuja redonda. Un toque en la burbuja lo devuelve. Arrástrala para
  moverla.
- **«Ocultar panel»** lo esconde del todo. Se recupera desde el icono de Clícalo junto al reloj.

Clícalo recuerda dónde dejaste el panel y la pestaña en cada pantalla.

### «Ajustes rápidos»

El engranaje abre una hoja corta con lo que más se cambia: «Centro de control», «Vista», «Opacidad», «Tamaño»
(S, M o L), «Lado de la pestaña», «Tema» (Auto, Oscuro, Claro y Alto contraste) y cuatro interruptores: «Modo
prueba (30 s)», atenuar el panel cuando no lo usas, «Teclas fijas» y «Números para control por voz».

En «Modo prueba» tocas botones para ver si el toque cuenta. No se envía nada.

### El icono junto al reloj

Clícalo tiene un icono en la bandeja de Windows, junto al reloj. Su menú tiene:

- «Mostrar panel» u «Ocultar panel».
- «Centro de control».
- «Soltar todo».
- Pausar y reanudar. Al pausar, el panel se oculta, se suelta todo y no se envía nada.
- «Salir». Antes de salir, suelta todo.

Un toque en el icono, sin abrir el menú, muestra u oculta el panel.

### Recuperar el panel

Si no ves el panel:

1. Toca el icono de Clícalo junto al reloj y elige «Mostrar panel».
2. Con la voz: «clic Clícalo».
3. Opcional: un atajo de teclado global. Viene apagado. Se activa en Centro de control › «General y panel» ›
   «Atajo de teclado para mostrar u ocultar el panel», y eliges la combinación de una lista.

## 4. Crear atajos

Todo se hace en el **Centro de control**. Se abre desde «Ajustes rápidos» o desde el icono junto al reloj. Es
una ventana normal: la puedes mover, agrandar y cerrar.

No hay botón «Guardar». Cada cambio se guarda solo. Abajo verás «Cambios guardados». Casi todo tiene
«Deshacer».

### Añadir un atajo

1. Abre el Centro de control › «Atajos».
2. Toca el perfil en la columna izquierda.
3. Toca «+ Añadir».
4. Elige una acción de la «Biblioteca de acciones». Un toque la añade. No hay que escribir nada.
5. O toca «Crear el mío».

### Crear el tuyo

1. **Nombre.** Escríbelo o toca el micrófono para dictarlo. Ese nombre es también el que dices con la voz.
2. **Qué hace.** Elige el tipo:

   | Tipo | Qué hace |
   |---|---|
   | «Pulsar» | Pulsa y suelta la combinación |
   | «Mantener» | Mantiene las teclas mientras tu dedo está en el botón |
   | «Alternar» | Un toque pulsa, otro toque suelta |
   | «Texto» | Escribe un texto guardado. Se guarda cifrado en tu equipo |
   | «Mouse» | Clic derecho, doble clic, arrastrar o desplazar |
   | «Macro» | Varios pasos seguidos con un solo toque |
   | «Web» | Abre una página en tu navegador |
   | «App» | Abre un programa o un archivo |

3. **Combinación.** Toca las teclas en pantalla. No necesitas teclado. «Grabar con teclado» es opcional.
4. **«Probar».** La tarjeta muestra qué hará. «Probar ahora en…» lo envía de verdad a la app que elijas y
   vuelve.

### Avisos del editor

- **Combinación reservada.** Windows no deja enviar algunas, como Ctrl+Alt+Supr. El editor lo avisa.
- **«Combinación repetida».** La misma combinación está en varios sitios. Puedes dejarla solo en «Siempre
  visible» o marcarla como intencional.
- **Atajo incompleto.** Le falta el nombre o la combinación. En el panel se ve marcado y no envía nada.

### Más cosas que puedes hacer

- **Vincular un perfil a una app.** En el perfil, toca «Vincular» y elige una app abierta. El panel cambiará
  solo al usarla.
- **«Fijar en "Siempre visible"».** El botón se ve en cualquier app.
- **«Fijar en Frecuentes».** El botón se queda en ★ «Frecuentes».
- **«Pedir confirmación antes de ejecutar».** El primer toque lo prepara y el segundo lo ejecuta.
- **Cambiar el orden.** Con los botones de «Más opciones». No hay que arrastrar.
- **«Duplicar» y «Eliminar».**

### Eliminar siempre pide dos toques

Al eliminar un atajo, un perfil o un paso, el botón cambia a «Confirmar». Tocas otra vez y se elimina. Después
puedes «Deshacer». Nada se borra con un solo toque.

Si necesitas más tiempo para el segundo toque: «General y panel» › «Más tiempo para confirmar y leer avisos»
(×1, ×2 o ×3).

## 5. Plantillas e inteligencia artificial

Centro de control › «Plantillas».

### Plantillas

Una plantilla es un perfil ya hecho para una app. Están probadas y se adaptan a tu teclado y al idioma de tus
programas.

1. Toca la tarjeta de la app.
2. Mira la vista previa. Desmarca lo que no quieras.
3. Instala.

Instalar nunca quita atajos: solo añade.

### «Perfil vacío»

Crea un perfil sin atajos para cualquier programa o tarea. Le pones nombre, eliges con qué app se activa y
añades los tuyos.

### Compartir e importar un perfil

- **Compartir.** Abre el perfil en «Atajos», toca el lápiz y «Compartir». Se crea un archivo. Los textos
  privados no se incluyen si no lo pides.
- **«Importar perfil».** Siempre verás antes una vista previa. Los atajos que abren programas o páginas
  vienen desmarcados: marca solo los que reconozcas. Importar nunca ejecuta nada.

### Crear atajos con IA, con tu propia clave

La IA propone atajos para cualquier programa. Es opcional. Todo lo demás funciona sin ella.

**Necesitas una clave de API de Anthropic que sea tuya.** Clícalo no trae una y no tiene servicio propio. El
uso de esa clave lo cobra el proveedor, no Clícalo.

1. En «Plantillas», toca «Usar mi clave» y pega la clave.
2. La clave se guarda en el Administrador de credenciales de Windows. No se vuelve a mostrar.
3. La primera vez, Clícalo te pide permiso y te dice qué se envía.
4. Escribe o dicta el nombre del programa y toca «Generar con IA».
5. Revisa la propuesta. Marca lo que quieras guardar. Nada se instala sin que lo confirmes.

**Qué se envía:** solo cuatro datos. El nombre del programa, la distribución del teclado, el idioma de tus
programas y el idioma de Clícalo. Nada de tus documentos ni de tus atajos.

Para quitarla: «Borrar clave». Pide dos toques.

Si algo falla, una tarjeta te dice por qué: falta la clave, la clave no vale, no hay conexión o la respuesta
no sirve. Siempre puedes seguir con plantillas o con un perfil vacío.

## 6. Soltar teclas y la franja roja

Clícalo nunca deja una tecla pulsada sin que lo veas.

### La franja roja

Cuando hay algo pulsado (un «Mantener», un «Alternar» activo o una tecla fija) aparece una franja roja con
«Pulsado:» y las teclas. Tiene el botón **«Soltar todo»**.

- Un toque en «Soltar todo» suelta todas las teclas y los botones del mouse.
- El aviso dice «Todas las teclas soltadas».
- La franja se ve en todas las vistas. En la burbuja, aparece un anillo rojo y el botón «Soltar todo» al
  lado.

También está en el icono junto al reloj › «Soltar todo». Esa opción funciona aunque el panel no responda.

### Cuándo se sueltan solas

- **Por tiempo.** Tras el tiempo de «Soltar teclas pulsadas tras» (en «General y panel»). Cada atajo puede
  tener el suyo.
- **Al cambiar de app**, si «Soltar al cambiar de app» está activado. Viene activado.
- **Al bloquear o suspender el equipo.**
- **Al cerrar Clícalo**, al actualizarlo y al pausarlo.

### Si Clícalo falla

Un pequeño programa aparte vigila a Clícalo. Si Clícalo se cierra por un error:

1. Suelta las teclas que quedaron pulsadas.
2. Vuelve a abrir Clícalo.
3. Si hace falta, Clícalo recupera tu última copia buena.

## 7. Copias de seguridad

Centro de control › «Sistema» › «Copias de seguridad».

- **Copia automática.** Clícalo guarda una copia 30 segundos después de cada cambio. Conserva las últimas 12.
- **«Crear copia ahora».** Una copia manual. No se borra con las automáticas.
- **«Exportar».** Guarda una copia en la carpeta que elijas. Sirve para llevarla a otro equipo.
- **«Importar».** Te pregunta cómo:
  - «Combinar» añade lo que falta.
  - «Reemplazar» sustituye todo. Antes guarda una copia y hay «Deshacer».
- **«Historial».** La lista de copias con su fecha y su tipo. «Restaurar» pide dos toques. Antes de restaurar,
  Clícalo guarda una copia de cómo estaba todo. También hay «Deshacer».

Dos cosas que conviene saber:

- Los textos privados se cifran para tu usuario de Windows. En otro equipo no se pueden leer: el atajo llega
  marcado como incompleto y lo vuelves a escribir.
- Una copia hecha con una versión más nueva no se aplica en una más antigua. Clícalo lo avisa y no cambia
  nada.

## 8. Actualizaciones y volver a la versión anterior

Centro de control › «Sistema» › «Actualizaciones».

- **«Buscar actualizaciones»** comprueba si hay una versión nueva.
- **«Actualizar automáticamente».** Se instala sola cuando llevas 5 minutos sin usar el panel.
- **«Avisarme antes de instalar».** Clícalo avisa y tú eliges el momento con «Instalar ahora».
- **«Copia antes de actualizar».** Guarda una copia de tus perfiles antes de instalar. Viene activada. Si la
  copia no se puede guardar, Clícalo no instala.
- **«Canal».** «Estable» es el normal. «Beta» recibe las novedades antes, con algún fallo posible.

Durante la instalación Clícalo suelta todo, guarda, se cierra y se abre en la versión nueva. Tus atajos no
cambian. La lista de novedades de cada versión está en esa misma pantalla.

Las actualizaciones se descargan de la página oficial de versiones. No llevan firma de código en la 2.0.

### Volver a la versión anterior

Durante 7 días después de actualizar aparece la fila «Volver a la versión…». Pide dos toques.

- En la 2.0.0 esa fila **no aparece**: es la primera versión y no hay otra anterior.
- Volver cambia el programa, no tus datos. Tus perfiles se conservan.
- Si lo que quieres es recuperar tus atajos de antes, usa «Copias de seguridad» › «Historial» y restaura la
  copia «Antes de actualizar».

## 9. Iniciar con Windows y programas de administrador

Centro de control › «Sistema» › «Inicio y estabilidad».

- **«Iniciar con Windows».** Clícalo se abre al iniciar sesión. Siempre arranca sin permisos de
  administrador.
- **«Reabrir como administrador».** Hace falta solo si quieres enviar atajos a un programa que se ejecuta como
  administrador. Windows no deja enviarle teclas a un programa así desde uno normal.

### Qué pasa al tocar «Reabrir como administrador»

1. Windows muestra su ventana de confirmación (Control de cuentas de usuario).
2. Si tocas «Sí», Clícalo se cierra y se abre de nuevo con permisos de administrador.
3. Si tocas «No», todo sigue igual. Clícalo lo dice en la barra de estado.

Windows lo pregunta **cada vez**. Al reiniciar el equipo, Clícalo vuelve a abrirse sin esos permisos.

La ventana de confirmación de Windows es del sistema: Clícalo no puede tocarla por ti. Tócala con el dedo o
dile «clic Sí» a Acceso por voz.

Cuando un atajo no llega porque la app de delante es de administrador, el panel lo avisa: «No se envió: … es
de administrador».

## 10. Desinstalar

Tienes dos caminos. Hacen lo mismo.

- **Desde Clícalo:** Centro de control › «Sistema» › «Desinstalar Clícalo».
- **Desde Windows:** Configuración › Aplicaciones › Aplicaciones instaladas › Clícalo › Desinstalar.

Qué pasa con tus datos:

- **Por defecto se quedan.** Tus atajos y ajustes siguen en `%AppData%\Clicalo`. Si vuelves a instalar,
  Clícalo los encuentra y te pregunta si quieres conservarlos o empezar de cero.
- **Si quieres borrarlos**, marca «Borrar también mis atajos y ajustes» al desinstalar desde Clícalo. Antes te
  pide guardar una copia donde tú elijas. Sin esa copia no se borra nada.

Desinstalar también quita «Iniciar con Windows». Si guardaste una clave de IA, bórrala antes con «Borrar
clave».

## 11. Narrador y Acceso por voz

### Acceso por voz

Acceso por voz viene con Windows 11. En Windows 10 se usa el Reconocimiento de voz de Windows.

- **Por nombre.** Di «clic» y el nombre del botón: «clic Copiar», «clic Negrita», «clic Soltar todo».
- **Por número.** Activa «Números para control por voz» en «Ajustes rápidos». Cada botón muestra un número. Di
  «clic 4».
- **Con los números de Windows.** Di «mostrar números» y después el número.
- **Para traer el panel.** Di «clic Clícalo».

Consejo: pon nombres cortos y distintos a tus botones. El nombre que escribes es el que dices.

### Dictar

Junto a cada campo de texto hay un micrófono. Lo tocas y dictas con el dictado de Windows (Win+H). Sirve para
el nombre de un atajo, un texto guardado, la búsqueda y tu mensaje en «Opinión y contacto».

### Narrador

Narrador lee todo el panel y el Centro de control.

- Cada botón dice su nombre y su estado: por ejemplo «ACTIVO» en un «Alternar» pulsado.
- Dice si el perfil está en «Auto» o en «Fijo».
- Lee los avisos cuando aparecen.
- Lee la franja roja y qué teclas están pulsadas.

Ningún estado se indica solo con color: siempre hay además un icono o un texto.

### Otros ajustes que ayudan

En Centro de control › «General y panel»:

- **«Alto contraste»** en «Tema». Si Windows tiene un tema de contraste activado, Clícalo usa siempre alto
  contraste, con los colores de Windows.
- **«Tamaño de texto».**
- **«Reducir movimiento».** Quita las animaciones.
- **«No puedo usar el teclado».** Da prioridad a la biblioteca, la IA y el dictado.
- **«Sonido suave» y «Destello en el botón».** Confirman que el atajo se envió.

En Centro de control › «Precisión táctil» eliges cuánto perdona el panel un toque impreciso: «Estándar»,
«Temblor leve», «Temblor fuerte» o «Personal». La «Zona de prueba» te deja probar sin enviar nada.

## 12. Si algo no va bien

| Qué pasa | Qué hacer |
|---|---|
| No veo el panel | Icono junto al reloj › «Mostrar panel». O di «clic Clícalo» |
| Una tecla se quedó pulsada | «Soltar todo» en la franja roja o en el icono junto al reloj |
| El atajo no hace nada en una app | Mira el aviso del panel. Si dice que la app es de administrador, usa «Reabrir como administrador» |
| El atajo hace otra cosa | Las teclas cambian con el idioma del programa. Negrita es Ctrl+N en español y Ctrl+B en inglés. Revisa «Idioma de tus programas» en «Plantillas» |
| Toco y no cuenta, o cuenta dos veces | Ajusta «Precisión táctil». Prueba en la «Zona de prueba» |
| El panel cambia de perfil cuando no quiero | Toca «Auto» para dejarlo en «Fijo» |
| Borré algo sin querer | «Deshacer». Si ya pasó tiempo, «Copias de seguridad» › «Historial» |
| Windows no deja instalar | Mira [Qué dice Windows al instalar](#qué-dice-windows-al-instalar) |
| La protección de teclas está desactivada | Sal de Clícalo y ábrelo de nuevo |

Para contarnos un fallo: Centro de control › «Acerca de y contacto» › «Reportar en GitHub». Puedes ver antes,
línea por línea, el registro técnico que se adjuntaría.

## 13. Privacidad

- Clícalo no envía datos de uso. No hay telemetría.
- Tus atajos y ajustes están solo en tu equipo.
- Los textos guardados se cifran para tu usuario de Windows.
- Solo sale algo a internet en dos casos, y en los dos lo decides tú: al buscar actualizaciones y al usar la
  IA con tu clave.

El detalle está en [privacidad](../security/privacy.md).
