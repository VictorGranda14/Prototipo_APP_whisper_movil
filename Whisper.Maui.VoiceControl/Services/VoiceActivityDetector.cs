using System.Diagnostics;

namespace Whisper.Maui.VoiceControl;

public class VoiceActivityDetector : IVoiceActivityDetector
{
    // Estado interno
    private bool _isSpeaking;
    private DateTime _lastSpeechTime = DateTime.MinValue;

    // Configuración inicial
    private int _silenceThresholdMs;
    private float _voiceThresholdDb;

    // Eventos
    public event EventHandler? SpeechStarted;
    public event EventHandler? SpeechEnded;

    // Propiedades
    public bool IsSpeaking => _isSpeaking;

    public int SilenceThresholdMs
    {
        get => _silenceThresholdMs;
        set
        {
            if (value <= 0)
                throw new ArgumentException("SilenceThresholdMs debe ser mayor a 0", nameof(value));
            
            _silenceThresholdMs = value;
            Debug.WriteLine($"[VAD] Umbral de silencio actualizado: {value}ms");
        }
    }

    public float VoiceThresholdDb
    {
        get => _voiceThresholdDb;
        set
        {
            _voiceThresholdDb = value;
            Debug.WriteLine($"[VAD] Umbral de voz actualizado: {value}dB");
        }
    }

    public void UpdateAudioLevel(float averagePower)
    {
        // VAD basado en nivel de audio
        if (averagePower > _voiceThresholdDb)
        {
            // Detectamos voz
            if (!_isSpeaking)
            {
                // Primera vez que detectamos voz
                _isSpeaking = true;
                Debug.WriteLine($"[VAD] Voz iniciada (power: {averagePower}dB > {_voiceThresholdDb}dB)");
                
                // Emitir evento de inicio de voz
                SpeechStarted?.Invoke(this, EventArgs.Empty);
            }

            // Actualizar el timestamp de última voz detectada
            _lastSpeechTime = DateTime.Now;
        }
        else
        {
            // No detectamos voz y se percibiendo sonido
            if (_isSpeaking)
            {
                // Verificar si ya pasó el umbral de silencio
                var silenceDuration = (DateTime.Now - _lastSpeechTime).TotalMilliseconds;
                
                if (silenceDuration > _silenceThresholdMs)
                {
                    // Silencio detectado después de hablar
                    _isSpeaking = false;
                    Debug.WriteLine($"[VAD] Silencio detectado después de {silenceDuration}ms (umbral: {_silenceThresholdMs}ms)");
                    
                    // Emitir evento de fin de voz
                    SpeechEnded?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}