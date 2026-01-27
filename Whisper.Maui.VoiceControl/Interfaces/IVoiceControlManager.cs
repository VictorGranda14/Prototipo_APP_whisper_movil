namespace Whisper.Maui.VoiceControl;

public interface IVoiceControlManager
{
    bool IsListening { get; }
    event EventHandler<string> OnTranscriptionReceived;
    event EventHandler<VoiceControlStatus> OnStatusChanged;
    event EventHandler<Exception> OnError;
    Task<bool> InitializeAsync(VoiceControlConfig config);
    Task StartContinuousListeningAsync(CancellationToken token);
    void StopListening();
    void Dispose();
}