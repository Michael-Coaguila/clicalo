# 01 · Producto y arquitectura

## Usuarios y contexto
- **Principal**: personas con movilidad reducida en las manos (lesión medular, ELA, artritis, temblor) que usan **pantalla táctil** y/o **acceso por voz de Windows**. Pueden tener poca precisión, toques dobles involuntarios, contacto breve o arrastres accidentales.
- **Secundario**: cualquier persona que quiera atajos a un toque (uso con tableta, presentaciones, edición).
- **Plataforma**: Windows 10 22H2+ y Windows 11. Pantallas táctiles, a veces varias pantallas con distinta escala.
- **Idiomas**: español e inglés (bilingüe completo, conmutable en caliente).

## Principios de producto
1. **Primero la accesibilidad**: cada decisión se evalúa pensando en alguien que solo usa el dedo, con temblor, o solo la voz.
2. **No estorbar**: el panel se atenúa, se pliega en el borde, cabe en tamaño S y nunca bloquea la barra de desplazamiento de otra app.
3. **Predecible**: lo que se ve es lo que pasa. Nada cambia de sitio solo, salvo el perfil en modo Auto, y eso siempre es visible.
4. **Explicar en lugar de suponer**: cada ajuste tiene una descripción en lenguaje llano; cada tipo de acción se puede probar y ver.
5. **Recuperable**: deshacer, confirmación en dos toques, copias, volver a la versión anterior.

## Arquitectura recomendada

Separar en dos procesos, o al menos en dos capas bien aisladas:

```
┌─────────────────────────── UI ───────────────────────────┐
│ Panel flotante (no activable)   Centro de control (normal)│
│ Bienvenida                      Bandeja del sistema        │
└──────────────▲───────────────────────────▲────────────────┘
               │ comandos / eventos (IPC o bus interno)
┌──────────────┴──────────── MOTOR ─────────┴────────────────┐
│ ForegroundWatcher  → app activa (proceso, título, elevada)  │
│ ProfileResolver    → qué perfil mostrar (Auto/Fijo, Frec.)  │
│ InputSender        → teclas VK/scancode, Unicode, mouse     │
│ ActionRunner       → tipos de acción, macros, esperas       │
│ KeySafety          → teclas pulsadas, timeouts, pánico      │
│ Store              → perfiles JSON versionado, deshacer     │
│ Backup/Migration   → copias, importación, migración v1      │
│ Updater, Logger (con datos personales ocultos), AIClient    │
└─────────────────────────────────────────────────────────────┘
```

- El **motor** puede reutilizar la lógica existente en Python del proyecto original (detección y envío ya funcionan), refactorizada según `docs/03`.
- La **UI** debe rehacerse con una tecnología que cumpla los requisitos de abajo.

## Requisitos que debe cumplir la tecnología de UI (criterio de elección)
| Requisito | Por qué |
|---|---|
| Ventanas `WS_EX_NOACTIVATE` + `TOPMOST` con entrada táctil real | Regla nº 1 |
| Entrada táctil nativa (`WM_POINTER`): contacto, presión, multitoque, sin el círculo visual de Windows | Filtros de precisión táctil |
| Exposición completa a **UI Automation** (nombre, rol, estado de cada botón) | Acceso por voz y Narrador |
| DPI por monitor (PerMonitorV2) | Varias pantallas |
| Transparencia de ventana (opacidad 30–100 %) y animación de opacidad | Atenuado |
| Fuentes personalizadas (Atkinson Hyperlegible) e iconos (Material Symbols Rounded) | Diseño |
| Rendimiento: panel visible en < 1 s desde el arranque y respuesta al toque < 50 ms | Uso diario |

Candidatas razonables: WinUI 3, WPF (.NET 8), Qt 6 (con su accesibilidad activada), Tauri/WebView2 con la ventana del panel en Win32 nativo. **La decisión es del autor.**

## Ventanas del sistema
| Ventana | Activable | Siempre encima | Barra de tareas | Notas |
|---|---|---|---|---|
| Panel (completa/compacta) | **No** | Sí | No | Arrastrable, opacidad, atenuado |
| Pestaña/barra de borde | **No** | Sí | No | Pegada a un borde; asa movible |
| Burbuja minimizada | **No** | Sí | No | Círculo de 64 px |
| Centro de control | Sí | No | Sí | Redimensionable, mínimo 760×520 |
| Bienvenida | Sí | Sí (modal) | Sí | Primer arranque o desde General |
| Icono de bandeja | — | — | — | Clic: mostrar u ocultar panel. Menú: Centro de control, Pausar, Salir |

## Instancia única
Mutex con nombre `Global\Clicalo`. Abrir la app otra vez solo muestra el panel existente (opción «Una sola ventana» en Sistema, activa por defecto).
