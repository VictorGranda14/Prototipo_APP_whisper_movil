using System.Diagnostics;

namespace Whisper.Maui.VoiceControl;

public class VoiceCommandProcessor : IVoiceCommandProcessor
{
    // Almacena comandos: key = pattern, value = handler
    private readonly Dictionary<string, Action<string>> _commands = new();

    public bool ProcessCommand(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // Limpiar texto
        string cleanText = text
            .Trim()
            .ToUpper()
            .Replace(".", "")
            .Replace("!", "")
            .Replace("¡", "")
            .Replace("?", "")
            .Replace("¿", "");

        Debug.WriteLine($"[VoiceCommandProcessor] Procesando: '{cleanText}'");

        if (_commands.TryGetValue(cleanText, out var handler))
        {
            Debug.WriteLine($"[VoiceCommandProcessor] Comando encontrado: '{cleanText}'");
            handler.Invoke(cleanText);
            return true;
        }

        Debug.WriteLine($"[VoiceCommandProcessor] Comando no registrado: '{cleanText}'");
        return false;
    }

    public void RegisterCommand(string pattern, Action<string> handler)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentNullException(nameof(pattern));
        
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        // Soportar múltiples patrones separados por |
        var patterns = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in patterns)
        {
            // Normalizar cada patrón a mayúsculas y limpiar espacios
            var normalizedPattern = p.Trim().ToUpper();

            if (string.IsNullOrEmpty(normalizedPattern)) continue;

            if (_commands.ContainsKey(normalizedPattern))
            {
                Debug.WriteLine($"[VoiceCommandProcessor] Reemplazando comando existente: '{normalizedPattern}'");
            }

            // Cada patrón individual se registra como clave independiente
            _commands[normalizedPattern] = handler;
            Debug.WriteLine($"[VoiceCommandProcessor] Comando registrado: '{normalizedPattern}'");
        }
    }

    public void UnregisterCommand(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return;

        // Soportar múltiples patrones separados por |
        var patterns = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in patterns)
        {
            var normalizedPattern = p.Trim().ToUpper();
            
            if (_commands.Remove(normalizedPattern))
            {
                Debug.WriteLine($"[VoiceCommandProcessor] Comando desregistrado: '{normalizedPattern}'");
            }
            else
            {
                Debug.WriteLine($"[VoiceCommandProcessor] Comando no encontrado para desregistrar: '{normalizedPattern}'");
            }
        }
    }

    public void ClearAllCommands()
    {
        _commands.Clear();
        Debug.WriteLine("[VoiceCommandProcessor] Todos los comandos fueron eliminados");
    }

    public int GetCommandCount()
    {
        return _commands.Count;
    }

    public void Dispose()
    {
        // Limpiar comandos
        ClearAllCommands();
    }
}