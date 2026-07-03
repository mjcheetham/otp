using System.Text.Json;

namespace Mjcheetham.Otp;

public static class Utf8JsonWriterExtensions
{
    extension(Utf8JsonWriter writer)
    {
        public void WriteStringEscaped(string propertyName, string? value)
        {
            if (value is null)
            {
                writer.WriteNull(propertyName);
                return;
            }

            string escapedValue = Uri.EscapeDataString(value);
            writer.WriteString(propertyName, escapedValue);
        }
    }
}
