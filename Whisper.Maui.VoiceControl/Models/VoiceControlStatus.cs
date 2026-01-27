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
}