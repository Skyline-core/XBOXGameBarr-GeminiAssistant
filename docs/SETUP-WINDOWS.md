# Configuraci�n en Windows

## 1. Instalar herramientas

### Visual Studio (2022 o 2026)

Puedes usar **Visual Studio 2022** o **Visual Studio 2026** en **Windows 11** (y Windows 10). Ninguna de las dos es incompatible con Windows 11 para desarrollo UWP.

| Versi�n | Carga de trabajo en el instalador |
|---------|-----------------------------------|
| **VS 2022** | **Desarrollo de la plataforma universal de Windows** |
| **VS 2026** | **Windows application development** + opcional **Universal Windows Platform tools** |

Gu�a detallada, diferencias y soluci�n de problemas: **[VISUAL-STUDIO.md](VISUAL-STUDIO.md)**.

**Recomendaci�n pr�ctica para este repo:**

- Si ya tienes **VS 2022** con UWP ? �salo (formato de proyecto alineado con los samples de Game Bar).
- Si instalas solo **VS 2026** ? instala tambi�n **UWP tools** y el **Windows 11 SDK**; si algo falla al desplegar, consulta VISUAL-STUDIO.md.

### SDK y otros

- **SDK m�nimo del proyecto:** Windows 10 SDK **10.0.19041.0** (declarado en el `.csproj`).
- **SDK recomendado en VS 2026:** Windows 11 SDK **10.0.26100.0** o superior.
- **API key:** [Google AI Studio](https://aistudio.google.com/apikey).

## 2. Abrir y compilar

```text
src\GeminiAssistant.sln
```

- Configuraci�n: **Debug**
- Plataforma: **x64** (recomendado para juegos modernos)
- Men� **Compilar** ? **Compilar soluci�n**

Si falla la restauraci�n de NuGet: clic derecho en la soluci�n ? **Restaurar paquetes NuGet**.

## 3. Modo desarrollador

**Configuraci�n** ? **Privacidad y seguridad** ? **Para desarrolladores** ? activar **Modo de desarrollador** (necesario para instalar el paquete UWP en sideload).

## 4. Depurar el widget

1. Ejecuta el proyecto (F5). Se instalar� la app UWP.
2. Abre un juego o aplicaci�n en primer plano.
3. Pulsa **Win+G** para abrir Xbox Game Bar.
4. En la barra de widgets, busca **Gemini Assistant** y �brelo.
5. Si no aparece, en Game Bar ? **Biblioteca de widgets** ? a�ade la app empaquetada en desarrollo.

Documentaci�n oficial: [Testing and debugging Game Bar widgets](https://learn.microsoft.com/en-us/gaming/game-bar/guide/debug).

### Excepci�n COM al depurar

Es normal ver una excepci�n sobre proxy/stub de `XboxGameBarWidgetActivatedEventArgs`. Puedes desmarcar "break when thrown" para ese tipo o continuar (F5).

## 5. API key

1. En el widget principal, pulsa el bot�n de **Ajustes** (si Game Bar muestra el icono de engranaje en la barra del widget).
2. O abre el widget **Gemini Assistant Settings** desde Game Bar.
3. Pega la API key y pulsa **Guardar**.

La clave se guarda en `ApplicationData.Current.LocalSettings` y no se sube al repositorio.

## 6. Seguimiento del juego (App Target)

Game Bar puede pedir permiso para que el widget "siga" la app en primer plano:

1. Abre el widget ? **Ajustes de Game Bar** para este widget.
2. Habilita el seguimiento del objetivo / target tracking.

Sin esto, el banner mostrar� que el seguimiento est� desactivado.

## 7. Prueba manual

| Paso | Acci�n esperada |
|------|-----------------|
| 1 | Banner muestra el nombre del juego o app activa |
| 2 | Escribe un mensaje ? respuesta de Gemini |
| 3 | **Capturar** ? elige la ventana del juego ? miniatura en el chat ? env�a con texto |
| 4 | **Micr�fono** ? habla ? texto reconocido ? env�a a Gemini |

## 8. Problemas frecuentes

| Problema | Soluci�n |
|----------|----------|
| `401` / API key inv�lida | Revisa la key en Ajustes |
| Sin respuesta de red | Capability `internetClient` en el manifiesto (ya incluida) |
| Captura vac�a | Elige la ventana correcta en el picker; algunos juegos anti-cheat bloquean captura |
| Micr�fono denegado | Configuraci�n de Windows ? Privacidad ? Micr�fono ? permitir para la app |
| VS 2026 no despliega | Ver [VISUAL-STUDIO.md](VISUAL-STUDIO.md) (workloads, x64, SDK) |
| No encuentras workload UWP en VS 2022 | Reinstala/modifica VS y marca "Desarrollo de la plataforma universal de Windows" |
| `Microsoft.Graphics.Win2D` >= 1.26.0 no encontrado | El proyecto usa **`Win2D.uwp`** 1.26.0 (el paquete antiguo solo llega a 1.4.0). Restaura NuGet de nuevo. |
| `PhoneProductId` inv�lido | Debe ser un GUID, no el nombre de la app. |
| `microphone` en `uap:Capability` | Usar `<DeviceCapability Name="microphone" />`. |
| Captura de pantalla no funciona | Declarar `<uap6:Capability Name="graphicsCapture" />` en el manifiesto. |
| Errores CS1525 en `Default.rd.xml` (`<` no v�lido) | El XML estaba en `<Compile>` por error; debe quedar solo como `<Content>`. Recarga el proyecto y recompila. |
