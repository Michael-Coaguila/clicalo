# 08 · Sistema, IA y distribución

## Arranque y permisos
- **Iniciar con Windows**:
  - Si no es como administrador, entrada `HKCU\...\Run`.
  - Si es como administrador, tarea programada «Ejecutar con los privilegios más altos» al iniciar sesión (evita el aviso de UAC en cada arranque).
- **Iniciar como administrador**: necesario para enviar teclas a apps elevadas (UIPI). Al activarlo se relanza elevado. Alternativa avanzada: manifiesto `uiAccess=true`, que requiere firma de código e instalación en Program Files.
- **Recuperación automática**:
  - Un proceso guardián relanza la app si se cierra de forma inesperada y restaura la última copia válida si `data.json` está dañado.
  - Al relanzar, siempre suelta las teclas.

## Actualizaciones
- Comprobar al arrancar y cada 24 h en un manifiesto firmado (versión, canal, hash, notas ES/EN).
- Descargar en segundo plano. Instalar solo cuando el panel lleva ≥ 5 min sin usarse o cuando el usuario lo pide. Con «Avisar antes», primero se muestra un aviso.
- Copia de `data.json` antes de instalar. Conservar la versión anterior 7 días para **Volver**.
- Binarios **firmados con código** (evita SmartScreen). Canal Beta opcional.

## Registros
- Archivo `%APPDATA%\Clicalo\logs\clicalo.log`, rotación de 5 × 1 MB.
- **Nunca** registrar el contenido de los textos (solo `[oculto · N caracteres]`), los títulos de ventana (`[título oculto]`) ni la clave de la IA.
- «Adjuntar registro» en Opinión adjunta el archivo ya depurado y muestra antes su vista previa.

## Cifrado
Los campos `text` de los botones de tipo Texto se guardan con **DPAPI** (`CryptProtectData`, ámbito del usuario), en base64 con el prefijo `dpapi:`. La clave de la IA va en el **Administrador de credenciales** de Windows, y `data.json` solo guarda la referencia.

## IA para generar plantillas
- **Opcional**. Las plantillas locales funcionan siempre sin conexión.
- **Consentimiento** explícito la primera vez. Si el usuario elige «No usar IA», se desactiva y se puede reactivar.
- **Datos enviados**: solo `{ appName, keyboardLayout, appsLang, uiLang }`. Nunca documentos, títulos ni atajos del usuario.
- **Respuesta esperada**, JSON validado contra el esquema; si falla la validación, se trata como error:
  ```json
  { "known": true, "app": "WhatsApp", "process": "WhatsApp.exe", "icon": "chat",
    "buttons": [ { "name": {"es":"Nuevo chat","en":"New chat"}, "icon":"add", "keys":["Ctrl","N"], "cat":"file", "confidence":0.9 } ] }
  ```
  Si `known:false`, mostrar el aviso de programa desconocido, con los atajos comunes y [Mejor, crear vacío].
- **Cuota**: 5 generaciones gratis al día por instalación (el contador se reinicia a medianoche local), o sin límite con la clave propia del usuario.
- **Errores**: sin conexión (timeout de 15 s), límite alcanzado, respuesta no válida e IA desactivada. Cada uno con 3 salidas (ver `docs/05`).
- El proveedor y el coste los decide el autor; el cliente debe poder cambiar de proveedor sin tocar la UI.

## Plantillas locales
Formato en `seed-and-catalogs.json` → `TPL`. Cada plantilla tiene `id`, nombre, icono, proceso, categoría y botones.
- `variants:true` indica que tiene variantes por idioma (`vk.en` en los botones).
- Sin variante para el idioma de las apps, se muestra «Plantilla revisada solo en español».
- Al instalar, se aplican el nombre editado en la vista previa y la variante de teclas correspondiente.

## Distribución
- Instalador con opciones «Iniciar con Windows» y «Crear acceso directo». La desinstalación pregunta si conservar los datos.
- Licencia MIT; repositorio público con plantillas de issues (fallo, sugerencia, accesibilidad).
