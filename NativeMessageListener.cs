using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class NativeMessageListener
{
    private readonly Stream _inputStream;
    private bool _isListening;
    private CancellationTokenSource _cts;

    public event Action<string> OnFinishProcess;

    public NativeMessageListener()
    {
        _inputStream = Console.OpenStandardInput();
    }

    public void Start()
    {
        if (_isListening) return;

        _isListening = true;
        _cts = new CancellationTokenSource();

        Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Stop()
    {
        _isListening = false;
        _cts?.Cancel();
    }

    private async Task ListenLoop(CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[4];

        while (_isListening && !cancellationToken.IsCancellationRequested) {
            try {
                int bytesRead = await ReadExactlyAsync(_inputStream, lengthBuffer, 0, 4, cancellationToken);
                if (bytesRead == 0) break;

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0) continue;

                byte[] messageBuffer = new byte[messageLength];
                bytesRead = await ReadExactlyAsync(_inputStream, messageBuffer, 0, messageLength, cancellationToken);
                if (bytesRead == 0) break;

                string jsonString = Encoding.UTF8.GetString(messageBuffer);
                ProcessMessage(jsonString);
            }
            catch {
                break;
            }
        }
    }

    private async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < count) {
            int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken);
            if (bytesRead == 0) return 0;
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }

    private void ProcessMessage(string jsonString)
    {
        try {
            using (JsonDocument doc = JsonDocument.Parse(jsonString)) {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("action", out JsonElement actionElement)) {
                    string action = actionElement.GetString();
                    if (action == "finish_process") {
                        string attachedText = string.Empty;
                        if (root.TryGetProperty("text", out JsonElement textElement)) {
                            attachedText = textElement.GetString();
                        }

                        OnFinishProcess?.Invoke(attachedText);
                    }
                }
            }
        }
        catch { }
    }
}