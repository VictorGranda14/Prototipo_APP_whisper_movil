namespace Whisper.Maui.VoiceControl;

public interface IAudioRecorderService
{
    bool IsRecording { get; }
    string AudioFilePath { get; }
    float GetAveragePower();
    float GetPeakPower();
    Task<bool> InitializeAsync();
    void StartRecording();
    void StopRecording();

    void Dispose();
}