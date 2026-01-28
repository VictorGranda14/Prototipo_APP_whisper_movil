using APP_whisper_movil.ViewModels;
using Whisper.Maui.VoiceControl;

namespace APP_whisper_movil;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;

    public MainPage()
    {
        InitializeComponent();

        // Crear servicios de la DLL
        var whisperService = new WhisperService();
        var audioRecorderService = new iOSAudioRecorderService();
        var voiceActivityDetector = new VoiceActivityDetector();
        var commandProcessor = new VoiceCommandProcessor();

        // Crear el VoiceControlManager con todos los servicios
        var voiceManager = new VoiceControlManager(
            whisperService,
            audioRecorderService,
            voiceActivityDetector,
            commandProcessor
        );

        // Crear ViewModel y asignar como BindingContext
        _viewModel = new MainPageViewModel(voiceManager);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Inicializar el ViewModel cuando la página aparece
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Liberar recursos cuando la página desaparece
        _viewModel.Dispose();
    }
}
