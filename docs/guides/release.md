# Publicar una versión

Guía para publicar Clícalo **a mano**, sin GitHub Actions, como decidió el usuario para la 2.0 (D6,
[ADR-0027](../adr/0027-distribucion-sin-firma-y-elevacion-bajo-demanda.md)): el paquete se crea en local con
`cl package` y se sube a las GitHub Releases del repositorio público con `gh release`. La 2.0 **no lleva firma de
código**; la última sección explica cómo añadirla más adelante con SignPath Foundation.

## Qué hay que saber antes

- **Canales.** `stable` y `beta` (Sistema › Actualizaciones › Canal). La app de cada canal lee el archivo
  `releases.<canal>.json` de las GitHub Releases; Beta lee también las versiones marcadas como preliminares.
- **Versiones.** SemVer: `X.Y.Z` en Estable y `X.Y.Z-beta.N` en Beta. Una versión nunca se reutiliza.
- **Beta recibe también las estables.** Una versión estable se empaqueta para los dos canales y se suben los dos
  juegos de archivos a la misma release.
- **Volver a la versión anterior** descarga la versión anterior de las releases: **no borres releases publicadas**.
- **Novedades.** Viajan dentro del paquete y la app las muestra en Sistema › Actualizaciones. Salen de los fragmentos
  de `changes/unreleased/*.yml` que crea `cl note` (una frase en `es:` y otra en `en:`).
- **Datos.** El instalador es por usuario y sin UAC (`%LocalAppData%\Clicalo.App`). Los datos viven en
  `%AppData%\Clicalo` y desinstalar **los conserva**, salvo que la persona pida borrarlos en Sistema › Inicio y
  estabilidad › «Desinstalar Clícalo», con dos toques y tras guardar una copia
  ([ADR-0029](../adr/0029-desinstalar-reinstalar-y-correo-de-opinion.md)).
- **ARM64.** `cl package --runtime win-arm64` crea el paquete de ARM64 en un canal propio (`stable-arm64` o
  `beta-arm64`), que es el que lee un Clícalo de ARM64; así los paquetes de las dos arquitecturas no comparten
  archivos. **No está probado en un equipo ARM64**: mientras no lo esté, solo se publica en Beta (propuesta P5 del
  catálogo).

## Pasos

1. **Parte de `main` al día y en verde.**

   ```text
   git switch main
   git pull
   cl check
   ```

2. **Revisa las novedades** de `changes/unreleased/`: una frase por idioma, en palabras de usuario.

3. **Empaqueta.** Para una beta:

   ```text
   cl package --channel beta --version 2.0.0-beta.1
   ```

   Para una estable, los dos canales con la misma versión:

   ```text
   cl package --version 2.0.0
   cl package --channel beta --version 2.0.0
   ```

   Cada orden publica Clícalo autocontenido con ReadyToRun y Sentinel autocontenido sin Native AOT, escribe las
   novedades en `artifacts/package/notes-<canal>.md` y deja en `artifacts/package/<canal>/`:
   `Clicalo.App-<canal>-Setup.exe`, `Clicalo.App-<versión>-<canal>-full.nupkg`, `releases.<canal>.json`,
   `assets.<canal>.json`, `RELEASES-<canal>` y el ZIP portátil. Cada vez vacía la carpeta del canal: los mismos datos
   dan los mismos archivos. **No publica nada.**

4. **Pruébalo en un equipo de pruebas o una máquina virtual**, nunca sobre la instalación que usas a diario:
   instala `Setup.exe`, abre Sistema y comprueba la versión, «Iniciar con Windows» y «Reabrir como administrador».
   Windows avisará de que el instalador no está firmado (SmartScreen): «Más información» › «Ejecutar de todas formas».
   Comprueba también, porque ninguna prueba automática lo hace con el instalador real:
   - **Desinstalar conservando los datos:** «Desinstalar Clícalo» con dos toques; `%AppData%\Clicalo` sigue ahí.
   - **Reinstalar con datos:** al instalar de nuevo, la bienvenida pregunta «Conservar mis datos» o «Empezar de
     cero»; «Empezar de cero» pide dos toques y deja una copia en Sistema › Copias de seguridad.
   - **Desinstalar borrando los datos:** marca «Borrar también mis atajos y ajustes», guarda la copia fuera de las
     carpetas de datos y comprueba que se borran `%AppData%\Clicalo`, `%LocalAppData%\Clicalo` y la clave de IA.
     Si cancelas la copia, no se desinstala nada.
   - **Copia dentro de los datos:** repite lo anterior eligiendo una carpeta dentro de `%AppData%\Clicalo`: debe
     negarse con su aviso y no desinstalar.
   - **Desde Configuración de Windows:** desinstalar ahí conserva siempre los datos.
   - **Enviar por correo** (cuando exista el correo del proyecto): abre la app de correo con el asunto y el cuerpo;
     mientras no exista, copia el mensaje.

5. **Etiqueta la versión** sobre el commit empaquetado:

   ```text
   git tag v2.0.0-beta.1
   git push origin v2.0.0-beta.1
   ```

6. **Crea la release y sube los archivos.** Una beta es preliminar:

   ```text
   gh release create v2.0.0-beta.1 --prerelease --title "Clícalo 2.0.0-beta.1" --notes-file artifacts/package/notes-beta.md artifacts/package/beta/*
   ```

   Una estable, sin `--prerelease` y con los dos canales:

   ```text
   gh release create v2.0.0 --title "Clícalo 2.0.0" --notes-file artifacts/package/notes-stable.md artifacts/package/stable/* artifacts/package/beta/*
   ```

7. **Comprueba la actualización** desde una instalación anterior del mismo canal: Sistema › Actualizaciones ›
   [Buscar actualizaciones] debe ofrecer la versión nueva con sus novedades.

8. **Archiva las novedades:** mueve los fragmentos de `changes/unreleased/` a `changes/<versión>/` en un commit
   `chore(release): <versión>`.

Si algo sale mal tras publicar, no borres la release: publica una versión nueva que lo corrija. Quien ya actualizó
puede usar «Volver a la versión anterior» durante 7 días.

## Firmar más adelante con SignPath

La 2.0 sale sin firma (D6). Cuando el usuario decida firmar:

1. **Solicitud.** Pide el alta en [SignPath Foundation](https://signpath.org/) (gratuita para proyectos con licencia
   OSI como MIT) y sigue su [política](../../CODE_SIGNING_POLICY.md): equipo, aprobadores con MFA y qué se firma.
2. **Origen verificable.** SignPath Foundation firma artefactos que salen de una compilación verificable en una CI
   conectada a SignPath. Activarla exige un *workflow* de publicación, que hoy no existe por decisión del usuario: es
   una decisión aparte, con su ADR.
3. **Qué se firma y cuándo.** Los ejecutables y ensamblados propios de la carpeta que publica `cl package`
   (`Clicalo.exe`, `Clicalo.Sentinel.exe` y `Clicalo.*.dll`) **antes** de `vpk pack`, y después `Setup.exe` y
   `Update.exe`, que `vpk pack` firma con `--signTemplate` o `--signParams`
   ([firma en Velopack](https://docs.velopack.io/packaging/signing)).
4. **En la app.** «Reabrir como administrador» comprueba hoy que el ejecutable es el instalado; con firma, se añade la
   comprobación del editor (`WinVerifyTrust`) antes del UAC (plano §3.3, regla 2), con un ADR que sustituya a
   ADR-0027 en esa parte.
