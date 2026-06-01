using System;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Marca de tiempo para importar solo capturas creadas durante el intento actual.
    /// </summary>
    internal static class CaptureSessionClock
    {
        public static DateTimeOffset? NotBeforeUtc { get; private set; }

        public static void Begin()
        {
            NotBeforeUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        }

        public static void End()
        {
            NotBeforeUtc = null;
        }

        public static bool IsRecentEnough(DateTimeOffset modifiedUtc)
        {
            if (!NotBeforeUtc.HasValue)
            {
                return true;
            }

            return modifiedUtc >= NotBeforeUtc.Value;
        }
    }
}
