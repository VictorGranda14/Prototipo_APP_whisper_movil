namespace Whisper.Maui.VoiceControl;

public class WhisperService : IWhisperService
{
    private WhisperProcessor processor;
    private WhisperFactory factory;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    // Inicializa el servicio Whisper con el modelo y el idioma especificados
    public async Task InitializeAsync(string modelName, string language = "es")
    {
        if (_isInitialized)
            return;

        var modelPath = Path.Combine(FileSystem.CacheDirectory, modelName);

        if (!File.Exists(modelPath))
        {
            // Copiar el modelo desde los recursos de la aplicación al sistema de archivos local
            using var stream = await FileSystem.OpenAppPackageFileAsync(modelName);
            using var fileStream = File.Create(modelPath);
            await stream.CopyToAsync(fileStream);
        }else{
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        }

        await Task.Run(() =>
        {
            factory = WhisperFactory.FromPath(modelPath);
            processor = factory.CreateBuilder().WithLanguage(language).Build(); // Creación del procesador (configurado para el idioma especificado)
        });

        _isInitialized = true;
    }
    // Transcribe el audio del stream y devuelve los resultados como un flujo asincrónico
    public async IAsyncEnumerable<string> TranscribeAsync(Stream audioStream)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("WhisperService is not initialized.");

        await foreach (var result in processor.ProcessAsync(audioStream))
        {
            yield return result.Text;
        }
    }

    // Libera los recursos utilizados por el servicio Whisper
    public void Dispose()
    {
        processor?.Dispose();
        factory?.Dispose();
        _isInitialized = false;
    }
}