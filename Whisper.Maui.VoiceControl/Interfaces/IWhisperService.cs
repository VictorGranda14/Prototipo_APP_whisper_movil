namespace Whisper.Maui.VoiceControl;

public interface IWhisperService
{
    bool IsInitialized { get; }
    Task InitializeAsync(string modelName, string language = "es");
    IAsyncEnumerable<string> TranscribeAsync(Stream audioStream);
    void Dispose();
}