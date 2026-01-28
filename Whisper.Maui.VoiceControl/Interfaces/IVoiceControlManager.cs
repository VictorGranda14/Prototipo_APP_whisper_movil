namespace Whisper.Maui.VoiceControl;

public interface IVoiceControlManager
{
    bool IsListening { get; }
    event EventHandler<string> OnTranscriptionReceived;
    event EventHandler<VoiceControlStatus> OnStatusChanged;
    event EventHandler<Exception> OnError;
    Task<bool> InitializeAsync(VoiceControlConfig config);
    Task StartContinuousListening(CancellationToken token);
    void StopListening();
    void RegisterCommand(string pattern, Action<string> handler);
    void UnregisterCommand(string pattern);
    void Dispose();
}