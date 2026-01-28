namespace Whisper.Maui.VoiceControl;

public interface IVoiceCommandProcessor
{
    bool ProcessCommand(string text);
    void RegisterCommand(string pattern, Action<string> handler);
    void UnregisterCommand(string pattern);
    void Dispose();
}