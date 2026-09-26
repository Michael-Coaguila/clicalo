# InputProbe

Ventana Win32 pura (sin WPF ni WinForms, con CsWin32) que registra todo lo que recibe y lo emite, una línea JSON por evento, a un *named pipe*. Es la «verdad física» de las pruebas de integración (blueprint §10.1): lo que ve InputProbe es lo que vería una aplicación real.

## Uso

```
InputProbe.exe --pipe <nombre>
```

- El **cliente** (la prueba) crea el pipe dúplex `\\.\pipe\<nombre>` con `PipeOptions.CurrentUserOnly`, lanza la sonda y le concede `AllowSetForegroundWindow`. `InputProbeSession` (en `tests/Clicalo.TestKit.Windows`) hace todo esto.
- La **sonda** se conecta (espera como mucho 10 s), crea la ventana, registra Raw Input de teclado, se muestra, pide el primer plano y emite `ready`.
- Si el cliente cierra el pipe, la sonda se cierra. También se cierra con el comando `quit` o cerrando la ventana.
- Códigos de salida: `0` normal, `2` argumentos incorrectos, `3` sin conexión al pipe, `4` no se pudo crear la ventana o registrar Raw Input.

## Eventos (sonda → cliente)

Cada línea es un objeto JSON. Los nombres de campo están en `Protocol/ProbeFields.cs` y los tipos en `Protocol/ProbeEventKinds.cs`; esa carpeta se compila también en `Clicalo.TestKit.Windows`, así que el contrato tiene una sola definición.

Campos comunes: `kind`, `seq` (1, 2, 3… sin huecos, en el orden en que la ventana los trató), `qpc` (marca monotónica de `QueryPerformanceCounter`; la frecuencia llega en `ready`) y `fg` (`GetForegroundWindow` en ese instante).

Los eventos que vienen de un mensaje añaden `msg`, `name`, `time` (`GetMessageTime`), `wParam`, `lParam` y `extra` (`GetMessageExtraInfo`, o `RAWKEYBOARD.ExtraInformation` en Raw Input: el `dwExtraInfo` con el que se inyectó).

| `kind` | Mensajes | Campos propios |
|---|---|---|
| `ready` | — | `protocol`, `hwnd`, `pid`, `tid`, `session`, `qpcFrequency`, `hkl` |
| `key` | `WM_KEYDOWN/UP`, `WM_SYSKEYDOWN/UP` | `vk`, `vkEx` (lado exacto: `VK_LCONTROL`/`VK_RCONTROL`…), `scan`, `ext`, `repeat`, `alt`, `prev`, `up`, `mods` |
| `char` | `WM_CHAR`, `WM_SYSCHAR`, `WM_DEADCHAR`, `WM_SYSDEADCHAR` | `unit` (unidad UTF-16), `text` (carácter completo; en un emoji solo aparece en la segunda mitad del par suplente), `scan`, `ext`, `repeat`, `alt`, `prev`, `up`, `mods` |
| `unichar` | `WM_UNICHAR` | `codePoint`, `text` |
| `rawKey` | `WM_INPUT` de teclado | `scan` (*make code*), `flags` (`RI_KEY_BREAK`, `RI_KEY_E0`, `RI_KEY_E1`), `vk`, `rawMsg`, `device` (0 si es inyectado), `sink` |
| `mouseButton` | botones izquierdo, derecho, central, X1 y X2 (pulsar, soltar, doble clic) | `button`, `down`, `dbl`, `x`, `y` (cliente, píxeles físicos), `keys` |
| `wheel` | `WM_MOUSEWHEEL`, `WM_MOUSEHWHEEL` | `horizontal`, `delta`, `x`, `y` (pantalla), `keys` |
| `activate` | `WM_ACTIVATE` | `state` (0, 1, 2), `minimized`, `other` |
| `activateApp` | `WM_ACTIVATEAPP` | `active`, `otherThread` |
| `focus` | `WM_SETFOCUS`, `WM_KILLFOCUS` | `gained`, `other` |
| `inputLanguage` | `WM_INPUTLANGCHANGE` | `charset`, `hkl` |
| `message` | `WM_IME_*`, `WM_SYSCOMMAND`, `WM_NCACTIVATE`, `WM_MOUSEACTIVATE` | solo los comunes |
| `pong`, `foreground` | respuestas a comandos | `id`; `foreground` añade `hwnd` y `ok` |
| `error` | fallo interno (la sonda sigue viva) | `detail` (nunca contenido del usuario) |
| `exit` | la ventana se destruye | — |

`mods` son los modificadores de cada lado según `GetKeyState` mientras se trata el mensaje: bit 0 Mayús izq., 1 Mayús der., 2 Ctrl izq., 3 Ctrl der., 4 Alt izq., 5 Alt der., 6 Win izq., 7 Win der.

## Comandos (cliente → sonda)

`{"cmd":"ping","id":1}`, `{"cmd":"foreground","id":2}` (opcionalmente con `"hwnd"` para otra ventana, útil en la prueba negativa de `ActivationGuard`) y `{"cmd":"quit"}`.

## Decisiones de diseño

- **Se comporta como una aplicación normal:** `TranslateMessage` genera los `WM_CHAR` y `DefWindowProc` hace su trabajo. Hay dos excepciones, para que la entrada registrada no cambie el estado de la sonda: se descarta `SC_KEYMENU` (Alt o F10 solos la dejarían en el bucle invisible del menú de sistema, que se come las teclas siguientes) y no se reenvía `WM_SYSCHAR` (sonaría un aviso por mnemónico inexistente).
- **Raw Input sin `RIDEV_INPUTSINK`:** la sonda solo recibe teclas mientras está en primer plano. Nunca puede registrar lo que se escribe en otras aplicaciones.
- **Por monitor (DPI v2):** las coordenadas del mouse son píxeles físicos.
- **El pipe es asíncrono (*overlapped*):** con un *handle* síncrono Windows serializa la E/S y una lectura pendiente bloquearía las escrituras. Los eventos se encolan desde el hilo de la ventana y los escribe un bucle aparte, así que un lector lento nunca retrasa el tratamiento de mensajes.
- **Distribución de teclado:** la sonda informa de la suya (`hkl`), pero no la cambia. `LoadKeyboardLayout` con `KLF_ACTIVATE` cambia la distribución de toda la sesión del usuario cuando está activada la opción predeterminada de Windows. Cargar es-ES, en-US y es-419 de forma aislada es trabajo del S7 completo.
