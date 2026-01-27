namespace Whisper.Maui.VoiceControl;

public enum VoiceControlState
{
    Idle,
    Listening,
    Processing,
    SpeechDetected,
    Error
}

public class VoiceControlStatus
{
    public VoiceControlState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}