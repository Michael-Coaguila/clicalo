# 10 · Plan de fases recomendado

Cada fase termina con su parte de `docs/09` en verde.

1. **Fundamentos del motor**
   - ForegroundWatcher e InputSender (VK, lados, Unicode, distribución de teclado).
   - KeySafety completo.
   - Store v2, migración v1 y copias.
   - *Sin UI*: un panel de prueba mínimo que no roba el foco.
2. **Panel base**
   - Ventana no activable con entrada táctil nativa y TouchFilter.
   - Vista completa con tamaños, páginas, fila fija, selector de perfil (★ + perfil + Auto/Fijo), barra de avisos, pánico y aviso de administrador.
   - Opacidad y atenuado. UIA en cada botón.
3. **Tipos de acción completos**: Mantener, Alternar, Texto, Mouse, Macro (ejecución), Web y App. Teclas fijas y Repetir.
4. **Vistas compacta y pestaña**, burbuja, ajustes rápidos y posición por monitor.
5. **Centro de control I**: ventana, menú lateral, Atajos (perfiles, vinculación, cuadrícula, biblioteca, editor completo con repetidos y Probar).
6. **Centro de control II**: General y panel, Precisión táctil, Sistema y Acerca de y contacto.
7. **Plantillas e IA**: plantillas locales con variantes, perfil vacío, sugerencias, IA con consentimiento, cuota y errores, e importar o compartir perfiles.
8. **Bienvenida** y detección de la configuración v1.
9. **Pulido y publicación**: frecuentes con toque largo, guía de la pestaña, reducir movimiento, actualizaciones firmadas, instalador y pruebas con Acceso por voz y Narrador.

## Pruebas automáticas mínimas
- `InputSender`: una ventana de prueba registra los eventos recibidos. Comprobar el orden, los lados, Unicode, la distribución ES y EN y los scancodes.
- `KeySafety`: cada condición de soltado.
- `Migration`: 3 archivos v1 reales (incluido uno dañado).
- `TouchFilter`: tablas de entrada y salida para cada preset.
- `ProfileResolver`: tabla de estados (tab × lock × app).
- `DupDetector`: los casos de `docs/03 §9`.
