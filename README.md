# XBOXGameBarr-GeminiAssistant

Widget de **Xbox Game Bar** (Win+G) con chat multimodal de **Google Gemini**: recomendaciones segn el juego activo, anlisis de capturas de pantalla y conversacin por voz.

## Requisitos

- Windows 10 (build 19041+) o **Windows 11**
- **Visual Studio 2022 o 2026** con herramientas UWP (ver [docs/VISUAL-STUDIO.md](docs/VISUAL-STUDIO.md))
- **.NET SDK 10** (el repo fija la versión en `global.json`)
- Windows 10/11 SDK (mínimo 10.0.19041.0; recomendado 10.0.26100.0+)
- [API key de Google AI Studio](https://aistudio.google.com/apikey)
- Xbox Game Bar actualizado (incluido en Windows)

## Inicio rpido

1. Clona el repositorio en tu PC Windows.
2. Abre `src/GeminiAssistant.sln` en Visual Studio 2022 o 2026.
3. Restaura paquetes NuGet y compila (x64 recomendado).
4. Habilita **Modo de desarrollador** en Windows si es necesario para instalar el paquete UWP.
5. Pulsa F5, abre Game Bar (Win+G) y aade el widget **Gemini Assistant**.
6. Abre el widget de **Ajustes** (icono de engranaje) y guarda tu API key.

Ver [docs/SETUP-WINDOWS.md](docs/SETUP-WINDOWS.md) para depuracin del widget y [docs/PRIVACY.md](docs/PRIVACY.md) para datos locales.

## Funciones (MVP)

| Funcin | Descripcin |
|--------|-------------|
| Chat flotante | Panel redimensionable dentro de Game Bar |
| Juego activo | `XboxGameBarAppTargetTracker`  nombre, si es juego, fullscreen |
| Captura | Selector de ventana (`GraphicsCapturePicker`) ? anlisis en Gemini |
| Voz | Reconocimiento de voz de Windows ? texto ? Gemini (MVP); Live API documentado para fase 2 |

## Estructura

```
src/GeminiAssistant/     # Proyecto UWP + widgets Game Bar
docs/                    # Guas de instalacin y privacidad
```

## Desarrollo desde Mac

No es posible compilar UWP en macOS. Usa este repo para documentacin y control de versiones; compila y prueba solo en Windows.

## Licencia

MIT  ver [LICENSE](LICENSE).
