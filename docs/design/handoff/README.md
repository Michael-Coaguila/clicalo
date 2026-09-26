# Handoff: Clícalo — panel táctil de atajos para Windows

> **Lo que quieras hacer, clícalo.**
> Panel flotante, pensado primero para la accesibilidad, que permite ejecutar atajos de teclado, macros, textos y acciones de mouse **solo con la pantalla táctil o la voz**, sin necesidad de teclado físico. Lo creó una persona con una lesión medular que no puede mover los dedos.

## Cómo usar este paquete

1. Lee este README completo: es el índice y contiene las reglas que no se pueden romper.
2. Implementa por fases siguiendo `docs/10-plan-de-fases.md`.
3. Cada documento de `docs/` es autosuficiente para su área. Si dos documentos se contradicen, prevalece el de número menor.
4. Al terminar cada fase, verifica su lista de `docs/09-criterios-de-aceptacion.md`.

## Sobre los archivos de diseño

`prototype/Prototipo v4.dc.html` es una **referencia de diseño hecha en HTML**: un prototipo interactivo que muestra el aspecto y el comportamiento esperados. **No es código de producción** ni se debe copiar directamente. La tarea es **recrear este diseño y su comportamiento en una aplicación nativa de Windows**. El autor elegirá la tecnología (ver `docs/01`, requisitos que debe cumplir).

- Para abrirlo, abre `Prototipo v4.dc.html` en un navegador (necesita `support.js` en la misma carpeta). La barra inferior simula la barra de tareas: cambia la app activa (Word, Chrome, VS Code, Excel, Admin. de tareas), abre la bienvenida y el centro de control.
- `prototype/Auditoría.dc.html` contiene los 60 hallazgos que dieron forma a la v4. **Todos se aceptaron**, salvo la elección de tecnología.
- `data/` contiene datos extraídos del prototipo, listos para usar:
  - `strings.es.json` / `strings.en.json`: **todos** los textos de la interfaz (669 claves, paridad completa). Úsalos como archivos de idioma.
  - `theme-palettes.json`: los tres temas (oscuro, claro, alto contraste) con todos sus colores.
  - `seed-and-catalogs.json`: perfiles de ejemplo, plantillas, biblioteca de acciones, grupos de teclas, iconos con etiquetas de búsqueda, combinaciones bloqueadas, tamaños y ajustes de precisión táctil.

## Fidelidad

**Alta fidelidad en comportamiento, estructura y textos. Fidelidad media en lo visual.**
- Flujos, estados, reglas, textos, tamaños de objetivos táctiles, orden de los elementos y colores de tema: **recrearlos tal cual**.
- Radios, sombras y espaciados: respetarlos como guía, adaptándolos a los controles nativos de la tecnología elegida.
- El icono de la app es provisional; el autor lo sustituirá en desarrollo. El logotipo con la «í» se mantiene (ver `docs/07`).

## Reglas que no se pueden romper

1. **El panel nunca quita el foco a la app en primer plano.** Si se activa, el atajo se envía al propio panel. Ventana siempre encima y no activable (Win32: `WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`).
2. **Todo objetivo táctil mide al menos 44×44 px lógicos.** Si el elemento visual es menor, el área de toque sigue siendo de 44.
3. **Siempre hay forma de soltar teclas pulsadas**: botón «Soltar todo», soltado automático al cambiar de app, al bloquear o suspender el equipo y tras un tiempo máximo.
4. **Nada destructivo sin confirmación en dos toques y sin deshacer.**
5. **Todo se puede hacer sin teclado físico.** Escribir es la última opción: siempre hay biblioteca, selección, dictado o IA.
6. **Cada botón expone su nombre (y su número de voz) a UI Automation**, para que funcionen el acceso por voz de Windows y Narrador.
7. **Los cambios se guardan solos.** No hay botón Guardar; todo cambio se puede deshacer.
8. **Nunca se pierden los datos del usuario**: hay migración desde `profiles.json` v1, copia antes de migrar o actualizar, y copias versionadas.

## Índice

| Documento | Contenido |
|---|---|
| `docs/01-producto-y-arquitectura.md` | Principios, usuarios, arquitectura motor/UI, requisitos de la tecnología |
| `docs/02-modelo-de-datos.md` | Esquema JSON versionado, entidades y migración desde v1 |
| `docs/03-motor.md` | Detección de la app en primer plano, envío de teclas, tipos de acción, seguridad, reglas |
| `docs/04-panel-flotante.md` | Vista completa, compacta, pestaña, burbuja, ajustes rápidos, menús y avisos |
| `docs/05-centro-de-control.md` | Ventana de configuración: las 6 secciones, pantalla por pantalla |
| `docs/06-bienvenida.md` | Asistente de primer arranque (5 pasos) |
| `docs/07-diseno-accesibilidad-idioma.md` | Tokens, temas, tipografía, iconos, marca, accesibilidad, i18n |
| `docs/08-sistema-ia-y-distribucion.md` | Actualizaciones, copias, arranque, administrador, registros, IA, privacidad |
| `docs/09-criterios-de-aceptacion.md` | Lista de verificación por área |
| `docs/10-plan-de-fases.md` | Orden de implementación recomendado |

## Glosario

- **Panel**: la ventana flotante de uso diario (vistas completa, compacta o pestaña).
- **Centro de control**: la ventana grande de configuración.
- **Perfil**: conjunto de atajos. Puede estar vinculado a un proceso (`WINWORD.EXE`) o ser manual. Hay dos especiales: **General** (se usa cuando la app no tiene perfil) y **Siempre visible** (fila fija en todas las apps).
- **Frecuentes**: pestaña automática con los atajos más usados. Nunca cambia sola al cambiar de app.
- **Auto / Fijo**: si el perfil mostrado sigue a la app activa (Auto, azul) o se queda (Fijo, rojo).
- **Atajo / botón**: una acción. Tipos: Pulsar, Mantener, Alternar, Texto, Mouse, Macro, Web, App.
