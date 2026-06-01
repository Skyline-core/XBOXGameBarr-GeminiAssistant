# Widget no aparece en Xbox Game Bar

## No confundir con el menu Inicio

**Gemini Assistant** es un widget de **Xbox Game Bar** (Win+G), no una app normal del menu Inicio. Compilar sin errores y activar el modo desarrollador **no** hace que aparezca solo: hay que **instalar el paquete** y **fijarlo** en la biblioteca de widgets.

## Pasos (orden recomendado)

### 1. Desplegar desde Visual Studio

- Plataforma: **x64**, configuracion: **Debug**
- Perfil **GeminiAssistant (Package)** con **Iniciar aplicacion = No** (`doNotLaunchApp` en `Properties/launchSettings.json`). Si VS abre la app en primer plano al depurar, al cerrarla se mata el widget (limitacion de Microsoft).
- Menu **Compilar** -> **Implementar** (o **F5** solo para desplegar)
- Debe terminar sin errores de despliegue

### 2. Verificar que Windows instalo el paquete

En PowerShell, desde la raiz del repositorio:

```powershell
.\scripts\Verify-GameBarWidget.ps1
```

Si dice `NOT INSTALLED`, el widget no puede aparecer: repite el paso 1.

### 3. Abrir Game Bar y fijar el widget

1. Abre un **juego o cualquier aplicacion** en primer plano.
2. Pulsa **Win+G**.
3. En la barra superior, pulsa el icono de **widgets** (cuadricula).
4. Entra en **Biblioteca de widgets** / **Todos los widgets**.
5. Busca **Gemini Assistant**.
6. Pulsa **Fijar** / **Pin** para anclarlo a la barra.

### 4. Si el widget se cierra al Enviar o Capturar

1. Reinstala con **Implementar** (perfil con **Iniciar aplicacion = No**).
2. Reproduce el fallo, vuelve a abrir el widget en Win+G.
3. Debe aparecer **Diagnostico:** con las ultimas lineas del log, o pulsa **Ver log**.
4. Copia ese texto (indica si murio en `HTTP POST`, `Send llamada Gemini`, etc.).

### 5. Si sigue sin salir

- Cierra Game Bar por completo (no solo minimizar).
- En VS: **Implementar** otra vez.
- **Win+G** de nuevo.
- Comprueba que **Xbox Game Bar** este actualizado (Microsoft Store).
- Regenera iconos del widget (opcional, mejora visibilidad):

  ```powershell
  .\scripts\Generate-GameBarIcons.ps1
  ```

  Luego vuelve a implementar.

## Depuracion en Visual Studio

1. Clic derecho en el proyecto **GeminiAssistant** -> **Propiedades**
2. **Depurar** -> **Iniciar aplicacion** = **No**
3. **F5** -> abre el widget manualmente desde Game Bar (Win+G)

Asi el depurador se adjunta cuando Game Bar lanza el proceso del widget.

## API key

Widget **Gemini Assistant Settings** en Game Bar (o el engranaje del widget de chat).

## Captura y microfono

- **Captura:** `PrintWindow` sobre la ventana del juego (sin Win+Alt+Impr Pant ni APIs graficas que cierran Win+G).
- **Enviar:** boton **Enviar** (no Enter); red con `Windows.Web.Http`; trabajo en `XboxGameBarForegroundWorker`; `XboxGameBarWidgetActivity` activa; chat persistido en `ChatSessionStore` si el widget reinicia.
- **Microfono:** graba 6 s de audio y lo envia a Gemini (ya no usa el reconocimiento de voz de Windows, que falla en Game Bar). Habla en cuanto pulses el boton; no hace falta el cuadro de texto.
- Permiso de microfono: Configuracion > Privacidad > Microfono > Gemini Assistant.
- Los botones usan `IsTabStop=False` (UWP no admite `Focusable` en Button).

## Referencia

https://learn.microsoft.com/en-us/gaming/game-bar/guide/testing-debugging
