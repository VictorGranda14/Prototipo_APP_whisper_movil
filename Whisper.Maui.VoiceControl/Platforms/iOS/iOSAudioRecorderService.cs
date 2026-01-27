using AVFoundation;
using Foundation;
using System.Diagnostics;

namespace Whisper.Maui.VoiceControl;

// All the code in this file is only included on iOS.
public class iOSAudioRecorderService : IAudioRecorderService
{
    // Estado
    private AVAudioRecorder? _recorder;
    private NSUrl? _audioFilePath;
    private bool _disposed = false;

    // Propiedades
    public bool IsInitialized { get; private set; } = false;
    public bool IsRecording { get; private set; } = false;
    public string AudioFilePath => _audioFilePath?.Path ?? string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            // Configurar sesión de audio
            var audioSession = AVAudioSession.SharedInstance(); //Configurar sesión de audio
            audioSession.SetCategory(AVAudioSessionCategory.PlayAndRecord); // Permitir grabación y reproducción
            audioSession.SetActive(true); // Activar sesión

            // Definir ruta del archivo temporal
            string audioFileName = "temp_audio.wav";
            string docPath = FileSystem.CacheDirectory;
            string path = Path.Combine(docPath, audioFileName);
            _audioFilePath = NSUrl.FromFilename(path);

            // Configurar ajustes de grabación compatible con Whisper
            var settings = new AudioSettings
            {
                SampleRate = 16000,
                Format = AudioToolbox.AudioFormatType.LinearPCM,
                NumberChannels = 1,
                LinearPcmBitDepth = 16,
                LinearPcmBigEndian = false,
                LinearPcmFloat = false,
            };

            // Crear el grabador
            await Task.Run (() =>
            {
                _recorder = AVAudioRecorder.Create(_audioFilePath, settings, out NSError error);
                if (error != null)
                {
                    Debug.WriteLine($"[iOSAudioRecorder] Error creando el grabador: {error.LocalizedDescription}");
                    IsInitialized = false;
                }
            });

            // Preparar y habilitar medición
            _recorder.PrepareToRecord();
            _recorder.MeteringEnabled = true; // Habilitar medición de niveles para VAD

            IsInitialized = true;
            Debug.WriteLine("[iOSAudioRecorder] Inicializado correctamente");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOSAudioRecorder] Error en inicialización: {ex.Message}");
            IsInitialized = false;
        }
    }

    public void StartRecording()
    {
        if (!IsInitialized || _recorder == null)
        {
            Debug.WriteLine("[iOSAudioRecorder] No se puede iniciar grabación: no inicializado");
            return;
        }

        if (IsRecording)
        {
            Debug.WriteLine("[iOSAudioRecorder] Ya está grabando");
            return;
        }

        try
        {
            _recorder.Record();
            IsRecording = true;
            Debug.WriteLine("[iOSAudioRecorder] Grabación iniciada");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOSAudioRecorder] Error al iniciar grabación: {ex.Message}");
            IsRecording = false;
        }
    }

    public void StopRecording()
    {
        if (!IsRecording || _recorder == null)
        {
            return;
        }

        try
        {
            _recorder.Stop();
            IsRecording = false;
            Debug.WriteLine("[iOSAudioRecorder] Grabación detenida");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOSAudioRecorder] Error al detener grabación: {ex.Message}");
        }
    }

    public float GetAveragePower()
    {
        if (!IsInitialized || _recorder == null)
        {
            Debug.WriteLine("[iOSAudioRecorder] No se puede obtener niveles de audio: no inicializado");
            return -160f; // Valor de silencio absoluto
        }

        try
        {
            // Actualizar medición
            _recorder.UpdateMeters();
            
            // Obtener nivel promedio del canal 0 (mono)
            float averagePower = _recorder.AveragePower(0);
            
            return averagePower;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOSAudioRecorder] Error al obtener niveles de audio: {ex.Message}");
            return -160f;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Detener grabación si está activa
            if (IsRecording)
            {
                StopRecording();
            }

            // Liberar recursos
            _recorder?.Dispose();
            _recorder = null;

            Debug.WriteLine("[iOSAudioRecorder] Recursos liberados");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOSAudioRecorder] Error al liberar recursos: {ex.Message}");
        }
        finally
        {
            _disposed = true;
        }
    }
}
