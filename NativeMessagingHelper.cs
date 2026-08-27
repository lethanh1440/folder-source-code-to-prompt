using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ProjectToPromptScanner
{
    public static class NativeMessagingHelper
    {
        public static void SendMessageToExtension(object payload)
        {
            try {
                string json = JsonSerializer.Serialize(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                using (Stream stdout = Console.OpenStandardOutput()) {
                    stdout.WriteByte((byte)((bytes.Length >> 0) & 0xFF));
                    stdout.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
                    stdout.WriteByte((byte)((bytes.Length >> 16) & 0xFF));
                    stdout.WriteByte((byte)((bytes.Length >> 24) & 0xFF));

                    stdout.Write(bytes, 0, bytes.Length);
                    stdout.Flush();
                }
            }
            catch { }
        }
    }
}