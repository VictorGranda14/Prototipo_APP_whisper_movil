using System.Diagnostics;

namespace Whisper.Maui.VoiceControl;

public class VoiceControlManager : IVoiceControlManager
{
    // Servicios inyectados
    private readonly IWhisperService _whisperService;
    private readonly IAudioRecorderService _audioRecorderService;
    private readonly IVoiceActivityDetector _voiceActivityDetector;
    private readonly IVoiceCommandProcessor _commandProcessor;

    // Estado interno
    private bool _isListening;
    private CancellationTokenSource? _listeningCancellationTokenSource;
    private bool _disposed = false;

    // Eventos
    public event EventHandler<string>? OnTranscriptionReceived;
    public event EventHandler<VoiceControlStatus>? OnStatusChanged;
    public event EventHandler<Exception>? OnError;

    // Propiedades
    public bool IsListening => _isListening;

    public VoiceControlManager(
        IWhisperService whisperService,
        IAudioRecorderService audioRecorderService,
        IVoiceActivityDetector voiceActivityDetector,
        IVoiceCommandProcessor commandProcessor)
    {
        _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
        _audioRecorderService = audioRecorderService ?? throw new ArgumentNullException(nameof(audioRecorderService));
        _voiceActivityDetector = voiceActivityDetector ?? throw new ArgumentNullException(nameof(voiceActivityDetector));
        _commandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));

        // Suscribirse a eventos
        _voiceActivityDetector.SpeechStarted += OnSpeechStarted;
        _voiceActivityDetector.SpeechEnded += OnSpeechEnded;
        OnTranscriptionReceived += (s, text) =>
        {
            _commandProcessor.ProcessCommand(text);
        };
    }

    public async Task<bool> InitializeAsync(VoiceControlConfig config)
    {
        try
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Configurar VAD
            _voiceActivityDetector.SilenceThresholdMs = config.SilenceThresholdMs;
            _voiceActivityDetector.VoiceThresholdDb = config.VoiceThresholdDb;

            // Inicializar Whisper
            await _whisperService.InitializeAsync(config.ModelPath, config.Language);
            
            if (!_whisperService.IsInitialized)
            {
                UpdateStatus(VoiceControlState.Error, "Error al inicializar Whisper");
                return false;
            }

            // Inicializar grabador de audio
            await _audioRecorderService.InitializeAsync();

            if (!_audioRecorderService.IsInitialized)
            {
                UpdateStatus(VoiceControlState.Error, "Error al inicializar el grabador de audio");
                return false;
            }

            UpdateStatus(VoiceControlState.Idle, "Sistema listo");
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
            UpdateStatus(VoiceControlState.Error, $"Error en inicialización: {ex.Message}");
            return false;
        }
    }

    public async Task StartContinuousListening(CancellationToken token)
    {
        if (_isListening)
        {
            Debug.WriteLine("Ya está en modo escucha continua");
            return;
        }

        if (!_whisperService.IsInitialized)
        {
            UpdateStatus(VoiceControlState.Error, "Sistema no inicializado");
            return;
        }

        try
        {
            _isListening = true;
            _listeningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            // Iniciar grabación
            _audioRecorderService.StartRecording();
            UpdateStatus(VoiceControlState.Listening, "Modo escucha continua activado");

            // Iniciar loop de monitoreo de audio
            await Task.Run(() => ProcessAudioStream(_listeningCancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
            UpdateStatus(VoiceControlState.Error, $"Error al iniciar escucha: {ex.Message}");
            StopListening();
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;

        try
        {
            _isListening = false;
            _listeningCancellationTokenSource?.Cancel();
            
            // Detener grabación
            _audioRecorderService.StopRecording();

            UpdateStatus(VoiceControlState.Idle, "Modo escucha continua desactivado");
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
        }
        finally
        {
            _listeningCancellationTokenSource?.Dispose();
            _listeningCancellationTokenSource = null;
        }
    }

    // Métodos de conveniencia para comandos

    public void RegisterCommand(string pattern, Action<string> handler)
    {
        if (_commandProcessor == null)
            throw new InvalidOperationException("No se configuró un CommandProcessor. Pase uno al constructor para usar esta funcionalidad.");

        _commandProcessor.RegisterCommand(pattern, handler);
    }

    public void UnregisterCommand(string pattern)
    {
        if (_commandProcessor == null)
            throw new InvalidOperationException("No se configuró un CommandProcessor.");

        _commandProcessor.UnregisterCommand(pattern);
    }

    public void SetCommandState(object state)
    {
        if (_commandProcessor == null)
            throw new InvalidOperationException("No se configuró un CommandProcessor.");

        _commandProcessor.SetState(state);
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopListening();

        // Desuscribirse de eventos
        _voiceActivityDetector.SpeechStarted -= OnSpeechStarted;
        _voiceActivityDetector.SpeechEnded -= OnSpeechEnded;
        OnTranscriptionReceived -= (s, text) => _commandProcessor.ProcessCommand(text);

        // Liberar servicios
        _audioRecorderService?.Dispose();
        _whisperService?.Dispose();
        _commandProcessor?.Dispose();

        _disposed = true;
    }

    // Métodos privados

    private async Task ProcessAudioStream(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Obtener niveles de audio del grabador
                float averagePower = _audioRecorderService.GetAveragePower();

                Debug.WriteLine($"Average Power: {averagePower}");

                // Actualizar VAD con los niveles de audio
                _voiceActivityDetector.UpdateAudioLevel(averagePower);

                await Task.Delay(100, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en monitoreo de audio: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }
    }

    private async Task ProcessSpeechAsync()
    {
        UpdateStatus(VoiceControlState.Processing, "Procesando...");

        string audioFilePath = _audioRecorderService.AudioFilePath;
        
        if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
        {
            Debug.WriteLine("Archivo de audio no encontrado para procesar.");
            UpdateStatus(VoiceControlState.Listening, "Modo escucha continua activado");
            return;
        }

        try
        {
            // Detener grabación para liberar el archivo
            _audioRecorderService.StopRecording();

            // Transcribir usando Whisper
            using var fileStream = File.OpenRead(audioFilePath);
            
            await foreach (var text in _whisperService.TranscribeAsync(fileStream))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Intentar procesar como comando. Si no es comando, emitir evento
                    if (_commandProcessor == null || !_commandProcessor.ProcessCommand(text))
                    {
                        OnTranscriptionReceived?.Invoke(this, text);
                    }
                }
                else
                {
                    Debug.WriteLine("No se reconoció texto en este fragmento.");
                }
            }

            // Reiniciar grabación
            _audioRecorderService.StartRecording();
            UpdateStatus(VoiceControlState.Listening, "Modo escucha continua activado");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error procesando el habla: {ex.Message}");
            OnError?.Invoke(this, ex);

            // Asegurar que la grabación se reinicie
            if (_isListening)
            {
                _audioRecorderService.StartRecording();
                UpdateStatus(VoiceControlState.Listening, "Modo escucha continua activado");
            }
        }
    }

    private void OnSpeechStarted(object? sender, EventArgs e)
    {
        UpdateStatus(VoiceControlState.SpeechDetected, "Escuchando sonido...");
    }

    private async void OnSpeechEnded(object? sender, EventArgs e)
    {
        Debug.WriteLine("Silencio detectado, procesando audio...");
        await ProcessSpeechAsync();
    }

    private void UpdateStatus(VoiceControlState state, string message)
    {
        var status = new VoiceControlStatus
        {
            State = state,
            Message = message,
            Timestamp = DateTime.Now
        };

        OnStatusChanged?.Invoke(this, status);
    }
}