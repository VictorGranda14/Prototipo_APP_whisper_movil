namespace Whisper.Maui.VoiceControl;

public interface IAudioRecorderService
{
    bool IsInitialized { get; }
    bool IsRecording { get; }
    string AudioFilePath { get; }
    float GetAveragePower();
    Task InitializeAsync();
    void StartRecording();
    void StopRecording();

    void Dispose();
}