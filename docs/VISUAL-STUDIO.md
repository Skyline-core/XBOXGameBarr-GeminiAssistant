# Visual Studio 2022 y 2026

Este proyecto es **UWP clásico** (formato de proyecto heredado, como los [samples oficiales de Game Bar](https://github.com/microsoft/XboxGameBarSamples)). Funciona en **Windows 10 y Windows 11** con Visual Studio 2022 o 2026.

## ¿VS 2022 es incompatible con Windows 11?

**No.** Microsoft documenta explícitamente que el desarrollo UWP está disponible en **Windows 11** con Visual Studio 2022 y 2026:

- [Compatibilidad VS 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/compatibility)
- [Compatibilidad VS 2026](https://learn.microsoft.com/en-us/visualstudio/releases/2026/compatibility)

Si no puedes instalar o abrir VS 2022, suele deberse a la **edición o carga de trabajo** (falta “Universal Windows Platform”), no a Windows 11.

## ¿Qué versión usar?

| Escenario | Recomendación |
|-----------|----------------|
| Ya tienes **VS 2022** con UWP instalado | Úsalo: es la opción más directa para este `.csproj` actual |
| Instalas **VS 2026** nuevo | También válido; Microsoft prioriza UWP moderno (.NET actual) en VS 2026 |
| VS 2026 abre el proyecto pero no compila/despliega | Ver [Problemas con VS 2026](#problemas-con-vs-2026) |

**Resumen:** no necesitas VS 2026 por compatibilidad con Windows 11; puedes quedarte en VS 2022. VS 2026 es la línea más nueva y conviene si empiezas instalación desde cero en 2026.

## Instalación: Visual Studio 2022

1. [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community, Professional o Enterprise).
2. En el instalador ? **Modificar** ? carga de trabajo:
   - **Desarrollo de la plataforma universal de Windows** (UWP)
3. Componentes recomendados:
   - **Windows 10 SDK (10.0.19041.0)** o superior (cubre el mínimo del proyecto)
   - **Windows 11 SDK** (opcional, recomendado en equipos solo con Win11)

## Instalación: Visual Studio 2026

1. [Visual Studio 2026](https://visualstudio.microsoft.com/) (misma familia de ediciones).
2. En el instalador ? carga de trabajo:
   - **Windows application development** (Desarrollo de aplicaciones para Windows)
3. En **Detalles de instalación** ? opcionales, marca:
   - **Universal Windows Platform tools**
   - **Windows 11 SDK (10.0.26100.0)** o la versión más reciente ofrecida

Documentación de UWP moderno en VS 2026: [Modernize your UWP app with .NET](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet).

### Diferencias relevantes para este repo

| Aspecto | VS 2022 (proyecto actual) | VS 2026 (plantillas nuevas) |
|---------|---------------------------|----------------------------|
| Formato `.csproj` | Heredado (`ToolsVersion`, `TargetPlatformVersion`) | SDK-style (`UseUwp`, `net10.0-windows10.0.26100.0`, etc.) |
| Runtime | .NET Native / UWP clásico del sample Game Bar | .NET moderno + Native AOT (plantillas por defecto) |
| Game Bar SDK | NuGet `Microsoft.Gaming.XboxGameBar` — probado en samples UWP clásicos | Mismo NuGet; puede requerir migración del `.csproj` si VS 2026 no restaura bien el formato antiguo |

El repositorio **no está migrado** al formato SDK de VS 2026. En la mayoría de casos VS 2026 **abre y compila** proyectos UWP antiguos si instalas **Universal Windows Platform tools** y el SDK adecuado.

## Problemas con VS 2026

Si el proyecto carga pero **no compila o no despliega** (F5):

1. **Instalador** ? revisa UWP tools + Windows 11 SDK (10.0.26100.0).
2. **Plataforma** ? selecciona **x64** (no “Any CPU” para UWP).
3. **Modo desarrollador** en Windows activado.
4. **Reparar** la solución: clic derecho en solución ? Restaurar paquetes NuGet.
5. Si persiste: abre el mismo repo en **VS 2022** en paralelo (instalación side-by-side permitida) o valora migrar el `.csproj` al formato moderno (issue futuro del proyecto).

Referencias de la comunidad sobre despliegue UWP en VS 2026: [Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5703435/vs2022-community-has-been-removed-and-vs-2026-will).

## Coexistencia VS 2022 + VS 2026

Puedes tener **ambas versiones** en el mismo PC. Útil si VS 2026 falla en un paso y quieres validar en VS 2022 sin cambiar el código.

## Enlaces útiles

- [Getting started – Xbox Game Bar SDK](https://learn.microsoft.com/en-us/gaming/game-bar/quickstart/introduction)
- [Xbox Game Bar samples](https://github.com/microsoft/XboxGameBarSamples)
- [UWP en VS 2026 (.NET moderno)](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet)
