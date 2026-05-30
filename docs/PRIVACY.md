# Privacidad y datos

## Qué se almacena localmente

| Dato | Ubicación | Propósito |
|------|-----------|-----------|
| API key de Gemini | `ApplicationData.LocalSettings` | Autenticación con Google AI Studio |
| Historial de chat (sesión) | Memoria de la app | Contexto de la conversación actual |

No se envían datos a servidores propios del proyecto; solo a la **API de Google** según sus [términos](https://ai.google.dev/gemini-api/terms).

## Qué se envía a Google

- Mensajes de texto del chat
- Imágenes de captura que adjuntes explícitamente
- Audio convertido a texto mediante el reconocimiento de voz de Windows (el audio en bruto no se envía en el MVP de voz por STT)
- Contexto del juego activo (nombre, si es juego, fullscreen) en la instrucción del sistema

## Capturas de pantalla

Las capturas usan el **selector del sistema** (`GraphicsCapturePicker`). Solo se captura lo que el usuario elige. La imagen se procesa en memoria y se envía a Gemini solo cuando envías un mensaje con captura adjunta.

## Micrófono

La app declara la capability `microphone`. El MVP usa **Windows.Media.SpeechRecognition** en el dispositivo; el texto resultante se envía a Gemini. Revisa los permisos de micrófono en Windows.

## Recomendaciones de seguridad

- No compartas builds con tu API key embebida.
- No subas `secrets.json` ni `.env` al repositorio.
- Para distribución pública, usa [tokens efímeros](https://ai.google.dev/gemini-api/docs/ephemeral-tokens) en lugar de API keys en el cliente.

## Eliminación

Desinstala la app UWP o borra la API key en el widget de Ajustes. Los datos en `LocalSettings` se eliminan con la desinstalación.
