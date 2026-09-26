# 06 · Bienvenida (primer arranque)

Ventana modal de 600 px de ancho, con padding de 32 y radio de 20. Arriba, 5 barras de progreso; abajo, [Atrás] · espacio · [Omitir] · [Siguiente] (en el último paso, [Empezar]). Se abre en el primer arranque y desde General → Ver la bienvenida otra vez.

| Paso | Contenido | Efecto al pasar al siguiente |
|---|---|---|
| **0 · Bienvenida** | Logotipo, «Clícalo» y el eslogan. [ob0t], [ob0b]. **Historia del creador** (tarjeta con [story1] y su firma). Idioma: Español / English | Cambia el idioma en caliente |
| **1 · Cómo usas tu equipo** | Opciones múltiples en chips: Pantalla táctil · Control por voz · **No puedo usar el teclado** · Tengo temblor · Mouse o trackball | Temblor → preset fuerte y tamaño L · táctil o sin teclado → preset leve · voz → números de voz activados · sin teclado → `noKeyboardUser` (oculta Grabar con teclado y prioriza la biblioteca, la IA y el dictado) |
| **2 · Qué apps usas más** | Chips de Word, Navegador, VS Code y las plantillas. Línea «⌨ Para Español (LA) · Office en español (detectado)» | Instala las plantillas marcadas y las vincula a su proceso |
| **3 · Cómo quieres el panel** | 3 tarjetas con miniatura: Completa / Compacta / Pestaña, con su descripción | Fija la vista |
| **4 · Elige cómo se ve** | Tamaños Compacto / Normal / Grande con un botón «Copiar» de ejemplo a escala real. Tema: **4 botones en una fila** (Auto, Oscuro, Claro, Alto contraste) | Guarda y muestra el panel con el aviso «¡Todo listo! …» |

- Omitir en cualquier paso cierra el asistente con los valores por defecto.
- Si se detecta `profiles.json` v1, antes del paso 0 se muestra la migración: «Encontramos tu configuración anterior: 4 perfiles, 39 atajos. La importamos (se guarda una copia).», con [Continuar].
