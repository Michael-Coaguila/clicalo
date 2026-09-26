# Clicalo.TestKit y Clicalo.TestKit.Windows

Ayudas compartidas por los proyectos de prueba (blueprint §10.1). `Clicalo.TestKit` es portable (`net10.0`) y no depende de ningún marco de pruebas. `Clicalo.TestKit.Windows` añade lo que necesita Windows: WPF, InputProbe y un inyector de pruebas. Ninguno de los dos es un ejecutable de pruebas. Las pruebas propias de ambos viven de momento en `tests/Clicalo.Platform.IntegrationTests/TestKit/`.

## Convención de trazabilidad: `[Trait("Req", …)]`

- Una prueba que verifica un requisito del catálogo (`docs/requirements/catalog.md`) lleva `[Trait("Req", "<ID>")]`, una vez por requisito. Se pone en el método, o en la clase si vale para todas sus pruebas. Los ID válidos son los requisitos (`EJE-003`) y los casos límite (`EC-EJE-10`). Las divergencias (`DIS-…`) y las preguntas (`PQ-…`) no son requisitos.
- Se escribe con el literal `"Req"`, no con una constante, para que las herramientas de trazabilidad (`cl trace`) lo encuentren tal cual.
- Cada proyecto de pruebas puede comprobar que sus referencias existen:

  ```csharp
  RequirementTraits.FindUnknown(typeof(MiPrueba).Assembly, RequirementCatalog.Default).ShouldBeEmpty();
  ```

- Las pruebas que necesitan un escritorio interactivo llevan además `[Trait("Requires", "Desktop")]` (ver más abajo).

## Instantáneas de texto (`TextSnapshot`)

```csharp
TextSnapshot.Match(textoGenerado, "menu");
```

- Compara con `Snapshots/<ArchivoDePrueba>.<Método>.<nombre>.verified.txt`, junto al archivo de la prueba. Cada archivo de prueba contiene una sola clase, así que su nombre identifica la clase.
- Normaliza antes de comparar y de escribir: saltos de línea `\n` y exactamente un `\n` final. Usa UTF-8 sin BOM. Así los archivos son idénticos en cualquier equipo (ver `.gitattributes`).
- Si no coincide, escribe `<…>.received.txt`, que git ignora por el patrón `*.received.*`, y falla con la primera línea distinta. Los caracteres invisibles (NBSP, controles, formato) se muestran como `\uXXXX`.
- **Aceptar:** se revisa el `.received.txt` y se vuelve a ejecutar con `CLICALO_ACCEPT_SNAPSHOTS=1`, o se renombra a `.verified.txt`. En CI (`CI` o `GITHUB_ACTIONS` a `true`) la variable se rechaza: un *pipeline* nunca aprueba su propia salida.
- Se descartó Verify por su licencia de pago renovable (`docs/architecture/deviations.md`).

## Instantáneas de renderizado (`RenderSnapshot`, en TestKit.Windows)

```csharp
RenderSnapshot.Match(() => new MiControl(), "normal", RenderSnapshotOptions.Default with { Dpi = 144 });
```

- El visual se crea, se maqueta y se renderiza en un hilo STA compartido (`WpfThread`) con `RenderTargetBitmap`, a un **DPI fijo** (96 = 100 %, 144 = 150 %) y nunca con la escala del equipo.
- Se compara con `…verified.png` píxel a píxel. Un píxel cuenta como distinto si algún canal (B, G, R o A) difiere más que `ChannelTolerance` (2 por defecto). La prueba falla si los píxeles distintos superan `MaxDifferentPixelsPercent` (0,5 % por defecto). Dos píxeles totalmente transparentes son iguales.
- Si falla, escribe `….received.png` y `….received.diff.png`, con las diferencias en magenta sobre una copia gris atenuada. Se acepta igual que las de texto. Un resultado dentro de la tolerancia nunca reescribe la referencia, así que los PNG no cambian sin motivo.
- El visual no se aloja en una ventana: `Loaded` no se dispara. Las referencias con texto dependen de las fuentes instaladas. Para las del producto (M3) se usarán las fuentes incrustadas.

## Reloj simulado (`TestTime`)

```csharp
var time = TestTime.CreateProvider();           // lunes 5-1-2026 09:00 UTC, zona local UTC
var limite = time.After(umbral);             // un umbral generado desde timings.json
time.AdvanceToJustBefore(limite);               // un tick antes: todavía no
time.AdvanceTo(limite);                         // justo a tiempo: se dispara
time.AdvanceInSteps(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(40));
```

`FakeTimeProvider` solo avanza cuando se le pide. Los temporizadores vencidos se disparan en orden y en el hilo que avanza. Todo umbral del producto sale de `timings.json`, y las pruebas lo cruzan con estas dos llamadas.

## Pruebas de escritorio (TestKit.Windows)

- **Activación:** se omiten salvo con `CLICALO_DESKTOP_TESTS=1`, mediante el *skip* dinámico de xUnit v3 (`SkipUnless` sobre `DesktopTestEnvironment.IsEnabled`). Llevan `[Trait("Requires", "Desktop")]` y comparten una colección sin paralelismo, porque el primer plano y la cola de entrada son de toda la sesión.
- **`InputProbeSession`:** lanza `InputProbe` (el proyecto de pruebas recibe una copia en `InputProbe\` al compilar; `CLICALO_INPUTPROBE_PATH` lo sustituye), lee sus eventos tipados y la lleva a primer plano **solo por medios legítimos**: `SetForegroundWindow` desde la prueba y desde la sonda después de `AllowSetForegroundWindow`. Nunca inyecta teclas ni usa `AttachThreadInput`. Si Windows no lo permite, falla con un diagnóstico: quién tiene el primer plano (proceso, nunca el título), el *foreground lock timeout* y la sesión. Eso es justo lo que mide el spike S0.
- **`TestKeyboardInjector` (solo para pruebas; el real llega en M2 en Platform.Core):**
  - antes de cada lote comprueba que la ventana de la sonda existe y tiene el primer plano, y que no hay ningún modificador pulsado; si algo falla, no envía nada;
  - solo acepta lotes pequeños y equilibrados (cada pulsación se suelta dentro del mismo lote) y los envía con un único `SendInput`;
  - marca cada evento con `dwExtraInfo = 0x434C4B31` para distinguirlo de la entrada física.
- **Esperar eventos:** `SendInput` es asíncrono, así que no hay una barrera exacta entre inyectar y observar. Las pruebas esperan los eventos previstos (`CollectAsync`) y un periodo de calma de 150 ms para detectar extras.
