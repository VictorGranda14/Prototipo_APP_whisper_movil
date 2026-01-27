namespace Whisper.Maui.VoiceControl;

public interface IVoiceActivityDetector
{
    bool IsSpeaking { get; }
    int SilenceThresholdMs { get; set; }
    float VoiceThresholdDb { get; set; }
    event EventHandler SpeechStarted;
    event EventHandler SpeechEnded;
    void UpdateAudioLevel(float averagePower, float peakPower);
}