namespace Whisper.Maui.VoiceControl;

public class VoiceControlConfig
{
    public string ModelPath { get; set; }
    public string Language { get; set; } = "es";
    public int SilenceThresholdMs { get; set; } = 1500;
    public float VoiceThresholdDb { get; set; } = -25f;
    public int AudioSampleRate { get; set; } = 16000;
}