# 02 · Modelo de datos

Un único archivo `%APPDATA%\Clicalo\data.json`, **versionado**. Las escrituras son atómicas: se escribe a `.tmp` y se renombra. Los textos de las acciones de tipo Texto se cifran (ver `docs/08`).

## Esquema (v2)

```jsonc
{
  "schema": 2,
  "app": { "version": "2.1.0", "lastRun": "2026-09-25T09:12:03Z", "migratedFrom": 1 },
  "settings": {
    "lang": "es",                       // es | en
    "theme": "auto",                    // auto | dark | light | hc  (auto = sigue Windows)
    "density": "full",                  // full | compact | dock
    "size": "M",                        // S | M | L
    "cols": 3,                          // 2 | 3 | 4
    "rowsPref": 0,                      // 0 = auto | 1 | 2 | 3
    "textScale": 100,                   // 100..150, paso 10
    "opacity": 0.92,                    // 0.30..1.00, paso 0.05
    "autoDim": true, "dimTo": 0.35,     // 0.10..0.80
    "showKeys": true, "voiceNumbers": false, "stickyModsRow": false,
    "showStripRow": true, "showTabsRow": true,
    "reduceMotion": false,
    "feedback": { "sound": true, "flash": true },
    "lockProfile": false,               // false = Auto (azul), true = Fijo (rojo)
    "lastProfile": "word",              // perfil al que vuelve el botón de perfil desde Frecuentes
    "dock": { "side": "right", "handlePosBySide": { "right": 50, "left": 50, "top": 50, "bottom": 50 },
              "pinOpen": false, "gutter": false, "perPage": 5, "coachDone": false },
    "panelPosByMonitor": { "\\\\.\\DISPLAY1": { "x": 1480, "y": 40 } },
    "touch": { "preset": "leve", "debounceMs": 300, "hitSlopPx": 14, "cancelMovePx": 35, "minContactMs": 0 },
    "keySafety": { "maxHoldSec": 60, "releaseOnAppSwitch": true },   // maxHoldSec 0 = nunca
    "autoSuggestProfiles": true,
    "keyboard": { "layout": "es-LA", "appsLang": "es", "detected": true },
    "ai": { "consent": false, "disabled": false, "freeLeftToday": 5, "freeResetAt": "...", "apiKeyRef": null },
    "reliability": { "startWithWindows": true, "autoBackup": true, "crashRecovery": true, "singleInstance": true, "runAsAdmin": false },
    "updates": { "auto": true, "askBefore": true, "backupBefore": true, "channel": "stable" },
    "noKeyboardUser": true,             // de la bienvenida: prioriza biblioteca, IA y dictado
    "dupIgnored": ["ctrl+c"]            // combinaciones marcadas «Está bien así»
  },
  "global": [ /* Button[] — fila «Siempre visible» */ ],
  "profiles": {
    "general": { "name": { "es": "General", "en": "General" }, "icon": "apps", "process": "", "compat": false, "buttons": [] },
    "word":    { "name": { "es": "Word", "en": "Word" }, "icon": "description", "process": "WINWORD.EXE", "compat": false, "buttons": [] }
  },
  "order": ["general", "word", "browser", "code"],
  "frequent": { "pins": ["copy"], "hidden": { "desk": true }, "usage": { "copy": [1727250000, 1727251000] } }
}
```

### Button
```jsonc
{
  "id": "bold",                              // único en todo el archivo
  "name": { "es": "Negrita", "en": "Bold" },  // al crear se rellenan los dos idiomas con el mismo texto
  "icon": "format_bold",                     // nombre de Material Symbols
  "autoIcon": true,                          // true = el icono se recalcula al cambiar el nombre
  "cat": "fmt",                              // categoría de color: edit|hist|file|sel|win|voice|nav|fmt|web|text
  "type": "tap",                             // tap|hold|toggle|text|mouse|macro|url|app
  "keys": ["Ctrl", "N"],                     // ORDEN DE PULSACIÓN tal como lo eligió el usuario
  "mouse": null,                             // rclick|dbl|mid|drag|sup|sdn|sleft|sright
  "speed": "normal",                         // slow|normal|fast (desplazamiento)
  "text": null, "textMethod": "type",        // type (Unicode) | paste
  "url": null, "app": null,                  // app = ruta o nombre de ejecutable
  "steps": [                                 // solo en macros
    { "kind": "keys", "keys": ["F12"] }, { "kind": "wait", "ms": 500 },
    { "kind": "text", "text": "PDF" }, { "kind": "mouse", "mouse": "rclick" }
  ],
  "maxHold": null,                           // segundos; null = usa settings.keySafety.maxHoldSec
  "confirm": false,                          // pide 2º toque antes de ejecutar (p. ej. Alt+F4)
  "vk": { "en": ["Ctrl", "B"] }              // variantes por idioma de la app (plantillas)
}
```

Nombres de teclas válidos: ver `KEYG` y `MODS` en `data/seed-and-catalogs.json`. Incluyen `Ctrl izq.`, `Ctrl der.`, `Shift izq.`, `Shift der.`, `Alt izq.`, `AltGr`, las del teclado numérico (`Num 0`…`Num Enter`) y las multimedia (`Vol +`, `Play/Pausa`, `F13`–`F24`…). Internamente, mapear cada nombre a VK o scancode (ver `docs/03`).

## Reglas de integridad
- Un botón vive en **una sola lista**: `global` o un perfil. «Fijar en Siempre visible» lo **mueve**, no lo copia.
- `general` y `global` no se pueden borrar. `general.process` siempre es `""`.
- Dos perfiles no pueden tener el mismo `process`. Si se vincula uno ya usado, se pide confirmar y se desvincula el anterior.
- Un borrador (sin nombre, sin teclas, de tipo tap y sin mouse) se descarta al salir del editor.
- **Deshacer**: pila en memoria de 20 estados. Las ediciones seguidas de un mismo botón cuentan como un solo paso; el paso se cierra al cambiar de botón.

## Migración desde v1 (`profiles.json` actual)
1. Al primer arranque de v2, si existe `profiles.json` y no existe `data.json`, **copiarlo tal cual** a `backups\v1-original-<fecha>.json`.
2. Convertir:
   - `active_profile` → `settings.lastProfile`.
   - `window_pos` → `panelPosByMonitor` del monitor principal.
   - Cada perfil v1 → `profiles[id]`, con `process` si v1 lo tenía.
   - Cada botón v1 → `Button`: normalizar las teclas (`"ctrl+shift+s"` → `["Ctrl","Shift","S"]`), asignar el icono con `suggestIcons()` y poner `autoIcon:true`.
3. Mostrar en Sistema → Copias la tarjeta «Importado desde tu versión anterior: N perfiles y M atajos».
4. Si la conversión falla, no escribir `data.json`: arrancar con los datos de ejemplo y mostrar un aviso con «Reintentar migración».

## Copias de seguridad
- Carpeta `%APPDATA%\Clicalo\backups\`, formato `backup-<ISO>.json` (el mismo esquema, con `schema`).
- Con la copia automática activa, se guarda una a los 30 s de cada cambio, con un máximo de 12, rotando las más antiguas. Siempre se guarda también antes de actualizar y antes de migrar.
- **Importar** pregunta **Combinar** (añade perfiles y botones que faltan; si un id coincide, gana el existente) o **Reemplazar** (sustituye todo, con deshacer).
- **Restaurar** exige doble toque («Restaurar» → «¿Seguro?») y admite deshacer.
- **Exportar un perfil** genera `clicalo-perfil-<id>.json`, que contiene un solo perfil más `"type":"profile-share"`. Se importa desde Plantillas, mostrando antes una vista previa.
