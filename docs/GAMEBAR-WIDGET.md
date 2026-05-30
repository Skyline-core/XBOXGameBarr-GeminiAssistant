# Widget no aparece en Xbox Game Bar

## No confundir con el menu Inicio

**Gemini Assistant** es un widget de **Xbox Game Bar** (Win+G), no una app normal del menu Inicio. Compilar sin errores y activar el modo desarrollador **no** hace que aparezca solo: hay que **instalar el paquete** y **fijarlo** en la biblioteca de widgets.

## Pasos (orden recomendado)

### 1. Desplegar desde Visual Studio

- Plataforma: **x64**, configuracion: **Debug**
- Menu **Compilar** -> **Implementar** (o **F5**)
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

### 4. Si sigue sin salir

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

- **Captura:** selector de ventana de Windows, luego mensaje con miniatura + respuesta de Gemini. Errores aparecen en el chat (rol Sistema).
- **Microfono:** graba 6 s de audio y lo envia a Gemini (ya no usa el reconocimiento de voz de Windows, que falla en Game Bar). Habla en cuanto pulses el boton; no hace falta el cuadro de texto.
- Permiso de microfono: Configuracion > Privacidad > Microfono > Gemini Assistant.
- Los botones usan `IsTabStop=False` (UWP no admite `Focusable` en Button).

## Referencia

https://learn.microsoft.com/en-us/gaming/game-bar/guide/testing-debugging
