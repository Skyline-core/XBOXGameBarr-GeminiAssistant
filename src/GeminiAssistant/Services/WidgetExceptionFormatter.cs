using System;
using System.Text;

namespace GeminiAssistant.Services
{
    internal static class WidgetExceptionFormatter
    {
        public static string Format(Exception ex)
        {
            if (ex == null)
            {
                return "(sin excepcion)";
            }

            var sb = new StringBuilder();
            sb.Append(ex.GetType().Name);
            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                sb.Append(": ").Append(ex.Message.Trim());
            }

            sb.Append(" HR=0x").Append(ex.HResult.ToString("X8"));

            if (ex is OperationCanceledException)
            {
                sb.Append(" [cancelado-suele-ser-GameBar]");
            }

            if (ex.HResult == unchecked((int)0x8001010E))
            {
                sb.Append(" [hilo-UI-incorrecto]");
            }

            if (ex.InnerException != null)
            {
                sb.Append(" <- ").Append(Format(ex.InnerException));
            }

            return sb.ToString();
        }
    }
}
