# 03 · Motor

## 1. Detección de la app en primer plano (`ForegroundWatcher`)
- Usar `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, **no sondear** periódicamente.
- Ignorar: ventanas propias (panel, pestaña, burbuja, centro de control), menú Inicio, centro de notificaciones, conmutador Alt+Tab, escritorio y barra de tareas. En esos casos la app activa sigue siendo la anterior.
- Apps de la Tienda: si el proceso es `ApplicationFrameHost.exe`, resolver la ventana hija real y su proceso.
- Emitir `{ process, title, hwnd, elevated }`. `elevated` se obtiene con `OpenProcessToken` + `TokenElevation`; si no se puede abrir el proceso, suponer que está elevado.
- **Modo captura** («Capturar la próxima app que uses»): el siguiente evento válido asigna su `process` al perfil que espera y sale del modo, con el aviso «Vinculado a X». Mientras espera, el panel muestra el aviso fijo «Abre la app que quieres vincular…» con la opción Cancelar.

## 2. Resolución del perfil mostrado (`ProfileResolver`)
Estado: `tab ∈ {freq, <profileId>}`, `lockProfile`, `lastProfile`.

- **Al cambiar la app activa**:
  - Si `tab == freq`, no cambia nada (Frecuentes nunca cambia sola).
  - Si `lockProfile == true` (Fijo, rojo), no cambia nada.
  - En otro caso (Auto, azul), `tab = profileFor(process) ?? "general"`.
- `profileFor(process)` busca el perfil cuyo `process` coincide, sin distinguir mayúsculas.
- **Botón ★ Frecuentes**: `tab = freq`.
- **Botón de perfil**:
  - Si `tab == freq`, vuelve con un toque a `retProf`: la app activa si está en Auto y tiene perfil; si no, `lastProfile`; si no, `general`.
  - Si `tab` es un perfil, abre el selector de perfiles.
- **Elegir un perfil en el selector**: `tab = id`, `lastProfile = id`.
- **Pulsar Auto → Fijo**: fija el perfil actual (o `retProf` si se está en Frecuentes). Aviso «Fijo en Word».
- **Pulsar Fijo → Auto**: si no está en Frecuentes, salta al perfil de la app activa. Aviso «Auto: sigue la app activa».
- Si la app activa no tiene perfil y `autoSuggestProfiles` está activo y existe una plantilla para ella, mostrar la sugerencia (ver `docs/04`).

## 3. Envío de entrada (`InputSender`)
- **Teclas**: `SendInput` con virtual keys, presionando en el **orden guardado** y soltando en orden inverso.
  - Pulsar = presionar todas y soltarlas enseguida (~15 ms entre eventos).
  - La letra se traduce con `VkKeyScanEx` usando la **distribución del hilo en primer plano** (`GetKeyboardLayout(GetWindowThreadProcessId(fg))`), no la del panel.
  - Las variantes izquierda/derecha usan sus VK propias: `VK_LCONTROL`/`VK_RCONTROL`, `VK_LSHIFT`/`VK_RSHIFT`, `VK_LMENU`; AltGr es `VK_RMENU`. «Ctrl» genérico = izquierdo.
- **Modo compatible** (perfil con `compat:true`): enviar scancodes (`KEYEVENTF_SCANCODE`, más `EXTENDEDKEY` cuando corresponda) para juegos y DirectInput. Documentar en la ayuda que algunos anti-trampas lo bloquean.
- **Texto**:
  - `type`: `KEYEVENTF_UNICODE`, carácter a carácter (respeta ñ, acentos y emoji sin depender del teclado).
  - `paste`: guardar el portapapeles, poner el texto, enviar Ctrl+V y restaurar el portapapeles a los 500 ms.
- **Mouse**:
  - `rclick`, `dbl`, `mid`: en la posición actual del puntero.
  - `drag`: Alternar; el primer toque hace botón izquierdo abajo, el segundo lo suelta.
  - `sup`/`sdn`/`sleft`/`sright`: `MOUSEEVENTF_WHEEL`/`HWHEEL`; mientras se mantiene, repetir cada 60/40/25 ms (lenta/normal/rápida) con aceleración progresiva.
- **Web**: `ShellExecute` de la URL, añadiendo `https://` si falta. **App**: `ShellExecute` de la ruta o el ejecutable.
- **Apps elevadas (UIPI)**: si `elevated && !selfElevated`, no enviar nada y mostrar el aviso de administrador (ver `docs/04`).

## 4. Tipos de acción (`ActionRunner`)
| Tipo | Toque | Mantener el dedo | Notas |
|---|---|---|---|
| **Pulsar** (tap) | Presiona y suelta | — | Si `confirm`, el primer toque arma («Toca otra vez para confirmar», 3 s) |
| **Mantener** (hold) | — | Presiona al bajar el dedo, suelta al levantarlo o si el dedo sale del botón | Sin menú de toque largo |
| **Alternar** (toggle) | 1.º: presiona y queda pulsado · 2.º: suelta | — | El botón se ve activo con la etiqueta ACTIVO |
| **Texto** | Escribe el texto | — | Método type/paste |
| **Mouse** | Ejecuta la acción | Desplazar: repite mientras se mantiene | — |
| **Macro** | Ejecuta los pasos en orden | — | Pasos: keys / wait (100–10 000 ms) / text / mouse. **Cancelable**: un 2.º toque durante la ejecución la detiene |
| **Web** / **App** | Abre | — | — |

Después de ejecutar una acción:
- Actualizar `frequent.usage`.
- Guardarla como última acción para **Repetir**.
- Aplicar la confirmación configurada (destello de 240 ms y sonido suave).
- En la vista pestaña, sin «mantener abierta», replegar la barra a los 900 ms.

## 5. Seguridad de teclas (`KeySafety`) — obligatorio
- Llevar un registro de todo lo que está presionado: holds, toggles, teclas fijas y el botón del mouse en `drag`.
- Si el registro no está vacío:
  - Emitir el evento `panic:on` con la lista de teclas.
  - La UI muestra la franja roja **«Pulsado: Win + H · Soltar todo»**.
  - El panel **no se atenúa**.
  - La burbuja minimizada muestra un anillo rojo.
- **Soltar todo** envía key-up de todo lo registrado y vacía el registro.
- **Soltado automático**:
  - Tras `maxHold` del botón (o `keySafety.maxHoldSec`; 0 = nunca).
  - Al cambiar de app, si `releaseOnAppSwitch` está activo.
  - Al bloquear la sesión (`WTS_SESSION_LOCK`), al suspender (`PBT_APMSUSPEND`) y al cerrar la app o ante un fallo, siempre.
- Tras cada soltado automático, mostrar un aviso que explique el motivo.
- Al arrancar la app, enviar key-up preventivo de todos los modificadores.

## 6. Teclas fijas (fila opcional Ctrl · Alt · Shift · Win)
- 1.er toque: la tecla se suma a la **próxima** acción de teclas y se suelta sola después.
- 2.º toque: queda bloqueada (🔒) hasta un 3.er toque, que la suelta.
- Se aplica también al clic del mouse (Ctrl+clic para seleccionar varios archivos). Registrar en KeySafety.

## 7. Combinaciones bloqueadas o especiales
Tabla `BLOCKED` en `seed-and-catalogs.json`:
- `b` (bloqueada: Ctrl+Alt+Supr, Win+L): el editor muestra un aviso rojo y el botón no hace nada. Win+L puede implementarse con `LockWorkStation()` como alternativa, si el usuario la elige.
- `s` (especial: Win+G, Alt+Tab, Win+Tab, Ctrl+Shift+Esc): el editor muestra un aviso amarillo recomendando probarla.

## 8. Frecuentes
- `usage` guarda las marcas de tiempo de ejecución de los **últimos 30 días**.
- Orden: primero los `pins` (en su orden), luego el resto por número de usos, sin los `hidden`. Se muestran 9.
- El toque largo (600 ms) sobre un botón, salvo los de tipo Mantener, abre el menú: Fijar en Frecuentes / Dejar de fijar · Quitar de Frecuentes (solo en esa pestaña) · Editar · Cancelar.
- «Reiniciar Frecuentes» (General) vacía `usage`, `pins` y `hidden`, con doble toque y deshacer.

## 9. Detección de repetidos
Dos botones A y B con la misma combinación normalizada (teclas y lados, sin distinguir mayúsculas; excluye texto, mouse, web, app y macro) se marcan como repetidos si se cumple **alguna** de estas condiciones:
- uno está en Siempre visible;
- están en el mismo perfil;
- tienen el mismo nombre.

Las combinaciones en `dupIgnored` no se marcan. La misma combinación en perfiles distintos con nombres distintos **no** es un repetido: cada app la interpreta a su manera.

## 10. Filtro táctil (`TouchFilter`) — común al panel, la pestaña y la zona de prueba
Para cada botón, con los valores de `settings.touch`:
- **Contacto mínimo**: si el contacto dura menos de `minContactMs`, se ignora.
- **Antirrebote**: un segundo toque en el **mismo** botón antes de `debounceMs` se ignora. Los demás botones no se bloquean.
- **Área extra**: un toque en el hueco a menos de `hitSlopPx` de un botón lo activa.
- **Cancelar si deslizas**: si el dedo se mueve más de `cancelMovePx`, no cuenta como toque.
- Presets:
  - Estándar: 150 ms / 8 px / 45 px / 0 ms.
  - Temblor leve: 300 / 14 / 35 / 0.
  - Temblor fuerte: 600 / 24 / 28 / 80.
  - Personal: los valores del usuario.
- **Modo prueba (30 s)**: el filtro se aplica pero **no se envía nada**. Cada botón muestra ✓ en verde o ⊘ en rojo durante 700 ms.

## 11. Números de voz
Con `voiceNumbers` activo, el nombre accesible de cada botón es `"<n> <nombre>"`, y el número se dibuja en la esquina. El número depende de la posición en la vista actual y la página. Así Acceso por voz responde a «clic 4» o «clic Negrita».
