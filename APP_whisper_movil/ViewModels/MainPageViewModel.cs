using System.Diagnostics;
using System.Windows.Input;
using Whisper.Maui.VoiceControl;

namespace APP_whisper_movil.ViewModels;

public enum AppState
{
    Navigation,
    Writing,
}

public class MainPageViewModel : BaseViewModel
{
    // Servicios
    private readonly IVoiceControlManager _voiceManager;

    // Estado de la aplicación
    private AppState _currentState = AppState.Navigation;
    
    // Propiedades enlazables (UI)
    private string _statusText = "Inicializando...";
    private Color _statusBackgroundColor = Color.FromArgb("#4aa0ff");
    private Color _buttonBackgroundColor = Color.FromArgb("#4aa0ff");
    private string _buttonText = "PRESIONAR PARA HABLAR 🎙️";
    private string _debugText = "";
    private string _stateText = "";
    private string _observacionText = "";
    private bool _checkboxDano = false;
    private bool _isInitialized = false;

    // Propiedades públicas con notificación de cambios

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Color StatusBackgroundColor
    {
        get => _statusBackgroundColor;
        set => SetProperty(ref _statusBackgroundColor, value);
    }

    public Color ButtonBackgroundColor
    {
        get => _buttonBackgroundColor;
        set => SetProperty(ref _buttonBackgroundColor, value);
    }

    public string ButtonText
    {
        get => _buttonText;
        set => SetProperty(ref _buttonText, value);
    }

    public string DebugText
    {
        get => _debugText;
        set => SetProperty(ref _debugText, value);
    }

    public string StateText
    {
        get => _stateText;
        set => SetProperty(ref _stateText, value);
    }

    public string ObservacionText
    {
        get => _observacionText;
        set => SetProperty(ref _observacionText, value);
    }

    public bool CheckboxDano
    {
        get => _checkboxDano;
        set => SetProperty(ref _checkboxDano, value);
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    // Comandos
    public ICommand ToggleListeningCommand { get; }

    // Constructor
    public MainPageViewModel(IVoiceControlManager voiceManager)
    {
        _voiceManager = voiceManager;

        // Inicializar comandos
        ToggleListeningCommand = new Command(async () => await ToggleListeningAsync());

        // Suscribirse a eventos del VoiceControlManager
        _voiceManager.OnStatusChanged += OnVoiceStatusChanged;
        _voiceManager.OnTranscriptionReceived += OnTranscriptionReceived;
        _voiceManager.OnError += OnVoiceError;

        // Registrar comandos de voz
        RegisterVoiceCommands();
    }

    // Inicialización asíncrona
    public async Task InitializeAsync()
    {
        try
        {
            StatusText = "⏳ Inicializando modelo de voz...";

            var config = new VoiceControlConfig
            {
                ModelPath = "ggml-small.bin",
                Language = "es",
                SilenceThresholdMs = 1500,
                VoiceThresholdDb = -25f,
                AudioSampleRate = 16000
            };

            bool success = await _voiceManager.InitializeAsync(config);

            if (success)
            {
                IsInitialized = true;
                StatusText = "✅ Sistema Listo. Presione el botón.";
                StatusBackgroundColor = Color.FromArgb("#4aa0ff");
            }
            else
            {
                StatusText = "❌ Error al inicializar el sistema";
                StatusBackgroundColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Error: {ex.Message}";
            StatusBackgroundColor = Colors.Red;
            Debug.WriteLine($"[MainPageViewModel] Error en inicialización: {ex.Message}");
        }
    }

    // Toggle escucha continua
    private async Task ToggleListeningAsync()
    {
        if (!IsInitialized) return;

        if (_voiceManager.IsListening)
        {
            StopListening();
        }
        else
        {
            await StartListeningAsync();
        }
    }

    private async Task StartListeningAsync()
    {
        try
        {
            ButtonText = "DETENER ESCUCHA CONTINUA 🛑";
            ButtonBackgroundColor = Colors.DarkRed;

            var canellationTokenSource = new CancellationTokenSource();
            await _voiceManager.StartContinuousListening(canellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPageViewModel] Error al iniciar escucha: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void StopListening()
    {
        _voiceManager.StopListening();

        ButtonText = "PRESIONAR PARA HABLAR 🎙️";
        ButtonBackgroundColor = Color.FromArgb("#4aa0ff");
    }

    // Registrar comandos de voz
    private void RegisterVoiceCommands()
    {
        // Comandos de navegación - Modo escritura
        _voiceManager.RegisterCommand("ESCRIBIR OBSERVACIÓN|ESCRIBIR OBSERVACION|ESCRIBIR NÚMERO|ESCRIBIR NUMERO", (cmd) =>
        {
            if (_currentState != AppState.Navigation) return;

            _currentState = AppState.Writing;
            StateText = $"Estado actual: {_currentState}";
            StatusText = "📝 MODO ESCRITURA ACTIVADO (Diga 'Finalizar' para salir)";
            ButtonBackgroundColor = Colors.Orange;
        });

        // Comandos de checkbox
        _voiceManager.RegisterCommand("SÍ|SI", (cmd) =>
        {
            if (_currentState != AppState.Navigation) return;
            CheckboxDano = true;
        });

        _voiceManager.RegisterCommand("NO", (cmd) =>
        {
            if (_currentState != AppState.Navigation) return;
            CheckboxDano = false;
        });

        // Comandos de escritura
        _voiceManager.RegisterCommand("FINALIZAR ESCRITURA|FINALIZAR", (cmd) =>
        {
            if (_currentState != AppState.Writing) return;

            _currentState = AppState.Navigation;
            StateText = $"Estado actual: {_currentState}";
            StatusText = "🔙 MODO NAVEGACIÓN (Diga comandos)";
            ButtonBackgroundColor = Color.FromArgb("#4aa0ff");
        });

        _voiceManager.RegisterCommand("BORRAR|LIMPIAR", (cmd) =>
        {
            if (_currentState != AppState.Writing) return;
            ObservacionText = "";
        });
    }

    // Handlers de eventos
    private void OnVoiceStatusChanged(object? sender, VoiceControlStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (status.State == VoiceControlState.Error)
            {
                StatusText = $"❌ {status.Message}";
            }

            StatusBackgroundColor = status.State switch
            {
                VoiceControlState.Idle => Color.FromArgb("#4aa0ff"),
                VoiceControlState.Listening => Colors.Green,
                VoiceControlState.SpeechDetected => Colors.Purple,
                VoiceControlState.Processing => Colors.Orange,
                VoiceControlState.Error => Colors.Red
            };
        });
    }

    private void OnTranscriptionReceived(object? sender, string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DebugText = $"Último detectado: {text}";
            StateText = $"Estado actual: {_currentState}";

            // Si estamos en modo escritura, agregar al texto
            if (_currentState == AppState.Writing)
            {
                if (!string.IsNullOrEmpty(ObservacionText))
                    ObservacionText += " ";
                
                ObservacionText += text.Trim();
            }
        });
    }

    private void OnVoiceError(object? sender, Exception ex)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = $"❌ Error: {ex.Message}";
            StatusBackgroundColor = Colors.Red;
            Debug.WriteLine($"[MainPageViewModel] Error: {ex.Message}");
        });
    }

    public void Dispose()
    {
        _voiceManager.OnStatusChanged -= OnVoiceStatusChanged;
        _voiceManager.OnTranscriptionReceived -= OnTranscriptionReceived;
        _voiceManager.OnError -= OnVoiceError;
        _voiceManager.Dispose();
    }
}
