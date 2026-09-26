---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: evaluación de distribución de WPF; verificación adversarial «Sostenibilidad»; crítica del plano 1.0
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0012 · Velopack con canales, `packId Clicalo.App` y los datos fuera de la carpeta de instalación

## Contexto y planteamiento del problema

Clícalo tiene que ser actualizable y recuperable (NFR-010): canal Estable o Beta, instalación de la
actualización en reposo, versión anterior conservada 7 días, un instalador y un desinstalador que
pregunte si conservar los datos, y la versión visible en la app. El catálogo añade estados y
preferencias de actualización (ACT-001, ACT-002), una actualización segura (ACT-003), novedades para el
usuario (ACT-004) y volver a la versión anterior (ACT-005). La instalación debe ser por usuario y sin
UAC.

La verificación adversarial encontró dos fallos en el plan inicial:

1. La desinstalación de Velopack borra entera la carpeta `%LocalAppData%\{packId}` y, lanzada desde
   Configuración de Windows, no puede mostrar UI. Con los datos en `%LocalAppData%\Clicalo`, se perderían
   en silencio y la pregunta de conservar datos no aparecería en el camino habitual.
2. Las actualizaciones de Velopack solo comprueban el hash que publica el mismo *feed*; eso se resuelve
   en [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md).

¿Con qué se instala y actualiza Clícalo, y dónde viven los datos para que ninguna desinstalación los
pierda?

## Factores de decisión

- Instalación por usuario, sin UAC.
- Canales estable y beta, actualizaciones delta y reversión explícita a N−1 durante 7 días.
- Ningún camino de desinstalación puede perder datos sin preguntar (REG-08).
- Una instancia elevada nunca deja archivos de la instalación con propietario Administradores.
- Compatibilidad con el componente de sistema de [ADR-0009](0009-elevacion-y-componente-de-sistema.md).

## Opciones consideradas

- Velopack 1.2 con `packId Clicalo.App`, canales, fuente propia verificada y datos en `%AppData%\Clicalo`
- MSIX
- Squirrel
- Un MSI por usuario

## Resultado de la decisión

Opción elegida: **«Velopack 1.2 con `packId Clicalo.App` y datos en `%AppData%\Clicalo`»**, porque cubre
canales, deltas, bajada explícita e instalación por usuario sin UAC, y separar el `packId` de la carpeta
de datos hace que ninguna desinstalación pueda borrar los datos.

- **Paquete:** Velopack (NuGet y CLI `vpk` de la misma versión), `Setup.exe` por usuario y sin UAC,
  autocontenido para `win-x64` (y `win-arm64` en beta, según P5), con R2R y sin recorte. El paquete
  incluye `Clicalo.Launcher.exe` y `release-files.json` firmado para el componente de sistema.
- **Ubicaciones:** instalación en `%LocalAppData%\Clicalo.App\`; datos en `%AppData%\Clicalo\`, que no
  toca ninguna desinstalación de Velopack.
- **Canales:** `stable` y `beta`, cada uno con su manifiesto (`ExplicitChannel`). Beta recibe también las
  estables posteriores. Nunca se baja de versión de forma implícita: pasar de beta a estable espera a que
  estable alcance o supere la versión instalada.
- **Reversión solo por acción del usuario**, con dos toques, si el paquete N−1 conservado está en
  `rollbackAllowed` del manifiesto vigente, no está revocado y es ≥ `minSafeVersion`. Si N cambió el
  major del esquema, se restaura la copia `pre-update` y se informa de qué se pierde.
- **Cuándo se instala:** sin nada pulsado, tras 5 min sin contacto (o a petición), sin edición abierta en
  el Centro de control, sin Modo prueba y sin concesión de primer plano activa; con copia `pre-update`
  antes. Un arranque nuevo que no confirma su salud dos veces entra en modo seguro y ofrece volver.
- **Instancias elevadas:** nunca descargan ni aplican. Delegan en `Clicalo.exe --apply-update` en
  integridad media, lanzado mediante el escritorio del shell.
- **Desinstalación:** desde Sistema › Desinstalar, Clícalo pregunta si conservar los datos (dos toques si
  se borran), desinstala el componente de sistema si existe y lanza `Update.exe --uninstall`. Desde
  Configuración de Windows los *hooks* no pueden mostrar UI, así que ese camino **conserva siempre los
  datos**. Al reinstalar, la bienvenida detecta los datos y ofrece conservarlos (por defecto) o empezar de
  cero con una copia `pre-reset` (propuesta P6).

### Consecuencias

- Buena, porque instalar y actualizar no piden UAC y los canales, las deltas y la reversión vienen de
  serie.
- Buena, porque ninguna desinstalación puede borrar los datos sin preguntar.
- Mala, porque el modo autocontenido no recibe los parches de .NET por su cuenta: hay que publicar una
  versión cuando un CVE afecte al runtime (beta en 72 h o menos y estable en 7 días o menos, con
  `patch-tuesday.yml`).
- Mala, porque la desinstalación desde Configuración de Windows no puede preguntar; P6 necesita dos textos
  nuevos en ES y EN para la pregunta en Sistema y en la bienvenida.
- Mala, porque la verificación de firma no es de Velopack: hay que escribir una fuente propia
  (`SignedManifestSource : IUpdateSource`, ADR-0013).

### Confirmación

- Spike S8: `vpk pack` con canales, delta, reversión con `rollbackAllowed`, desinstalación propia y desde
  Configuración (los datos se conservan), `SignedManifestSource` y actualización desde una instancia
  elevada con el propietario de los archivos correcto.
- Pruebas de Infrastructure: una bajada legítima se aplica; se rechazan un manifiesto antiguo con `seq`
  menor, un N−1 fuera de `rollbackAllowed` y un N−1 revocado; una actualización automática nunca baja.
- Criterio de M5: actualización delta, reversión legítima a N−1 y actualización desde una instancia
  elevada sin archivos con propietario Administradores.

## Pros y contras de las opciones

### Velopack 1.2

- Buena, porque está activo y cubre canales, deltas, `AllowVersionDowngrade` e instalación por usuario.
- Buena, porque puede generar un MSI por máquina si algún día hiciera falta.
- Mala, porque sus *hooks* de desinstalación no admiten UI y solo verifica el hash del propio *feed*.

### MSIX

- Buena, porque se integra con la Tienda y con la gestión de paquetes de Windows.
- Mala, porque no admite uiAccess y complica el inicio elevado.

### Squirrel

- Buena, porque tiene el mismo modelo de instalación por usuario.
- Mala, porque no aporta nada que Velopack no cubra, y las capacidades necesarias (canales, deltas,
  bajada explícita) solo se verificaron en Velopack.

### MSI por usuario

- Buena, porque su diálogo de desinstalación podría preguntar por los datos.
- Mala, porque no trae canales, deltas ni actualización automática: habría que escribirlos.

## Criterios de reapertura

Si S8 demuestra que Velopack no admite una fuente de actualizaciones propia con verificación de firma o
la reversión con `rollbackAllowed`, se escribe un ADR nuevo.

## Más información

- Plano: [§6.5 (ubicaciones)](../architecture/blueprint.md#65-persistencia),
  [§9.3](../architecture/blueprint.md#93-actualizaciones),
  [§11](../architecture/blueprint.md#11-distribución-versionado-y-publicación),
  [§1.4 (P5 y P6)](../architecture/blueprint.md#14-propuestas-de-producto-pendientes-de-ratificar-por-el-usuario).
- Catálogo: ACT-001 a ACT-005 en [§2.25](../requirements/catalog.md#225-act--actualizaciones); NFR-010 en
  [§3](../requirements/catalog.md#3-requisitos-no-funcionales).
- Registro: [techVerification.json](../architecture/decision-record/techVerification.json) (enfoque
  «Sostenibilidad»), [techEvaluations.json](../architecture/decision-record/techEvaluations.json)
  (criterio (h) de WPF).
- Evidencia:
  [Velopack en NuGet](https://www.nuget.org/packages/Velopack),
  [UpdateOptions](https://docs.velopack.io/reference/cs/Velopack/UpdateOptions),
  [versión concreta](https://docs.velopack.io/integrating/specific-version),
  [hooks](https://docs.velopack.io/integrating/hooks),
  [desinstalación](https://docs.velopack.io/integrating/uninstalling),
  [archivos conservados](https://docs.velopack.io/integrating/preserved-files),
  [Velopack en Windows](https://docs.velopack.io/packaging/operating-systems/windows),
  [canales](https://docs.velopack.io/packaging/channels),
  [cambio de canal](https://docs.velopack.io/integrating/switching-channels),
  [instalador](https://docs.velopack.io/packaging/installer),
  [msix-packaging #486](https://github.com/microsoft/msix-packaging/issues/486).
- ADR relacionados: [ADR-0007](0007-documento-json-versionado.md),
  [ADR-0009](0009-elevacion-y-componente-de-sistema.md),
  [ADR-0013](0013-firma-de-codigo-y-manifiesto-firmado.md).
