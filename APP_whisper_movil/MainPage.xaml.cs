using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using AVFoundation;
using Foundation;

namespace APP_whisper_movil
{
    public partial class MainPage : ContentPage
    {
        // Variables de Audio y Modelo
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private AVAudioRecorder recorder;
        private NSUrl audioFilePath;
        private bool isRecording = false;

        public MainPage()
        {
            InitializeComponent();
            InitializeWhisper();
            ConfigureRecorder();
        }
        private void ConfigureRecorder()
        {
            var audioSession = AVAudioSession.SharedInstance();//Configurar sesión de audio
            audioSession.SetCategory(AVAudioSessionCategory.PlayAndRecord); //Permitir grabación y reproducción
            audioSession.SetActive(true); //Activar sesión

            //Definir ruta del archivo temporal
            string audioFileName = "temp_audio.wav";
            string docPath = FileSystem.CacheDirectory;
            string path = Path.Combine(docPath, audioFileName);
            audioFilePath = NSUrl.FromFilename(path);

            //Configurar ajustes de grabación compatiblke con Whisper
            var settings = new AudioSettings
            {
                SampleRate = 16000,
                Format = AudioToolbox.AudioFormatType.LinearPCM,
                NumberChannels = 1,
                LinearPcmBitDepth = 16,
                LinearPcmBigEndian = false,
                LinearPcmFloat = false,
            };

            var inputActual = audioSession.CurrentRoute.Inputs.FirstOrDefault();

            recorder = AVAudioRecorder.Create(audioFilePath, settings, out NSError error);
            
            if (error != null)
            {
                StatusLabel.Text = $"Error configurando el audio: {error.LocalizedDescription}";
            }
            else
            {
                recorder.PrepareToRecord();
            }
        }
        private async void InitializeWhisper()
        {
            try
            {   
                var model = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Small);
                using var tempMemoryStream = new MemoryStream();

                await model.CopyToAsync(tempMemoryStream);
                
                await Task.Run(() =>
                {
                    whisperFactory = WhisperFactory.FromBuffer(tempMemoryStream.ToArray());
                    processor = whisperFactory.CreateBuilder().WithLanguage("es").Build(); // Creación del procesador (configurado para español)
                });

                StatusLabel.Text = "✅ Sistema Listo. Presione el botón.";
            } catch (Exception e)
            {
                StatusLabel.Text = $"Error iniciando el modelo de Whisper: {e.Message}";
            }
        }

        private void OnHablarPressed(object sender, EventArgs e)
        {
            if (processor == null) return;

            isRecording = true;
            StatusLabel.Text = "🔴 Escuchando...";
            StatusFrame.BackgroundColor = Colors.Red;
            BtnHablar.BackgroundColor = Colors.DarkRed;
            BtnHablar.Text = "GRABANDO...";

            recorder.Record();
        }

        private async void OnHablarReleased(object sender, EventArgs e)
        {
            if (!isRecording) return;

            isRecording = false;
            recorder.Stop();

            StatusLabel.Text = "⏳ Procesando...";
            StatusFrame.BackgroundColor = Colors.Orange;
            BtnHablar.Text = "MANTENER PARA HABLAR 🎙️";
            BtnHablar.BackgroundColor = Color.FromArgb("#4aa0ff");

            if(!File.Exists(audioFilePath.Path))
            {
                StatusLabel.Text = "Error: No se encontró el archivo de audio grabado.";
                return;
            }

            using var fileStream = File.OpenRead(audioFilePath.Path);

            try
            {
                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    string texto = LimpiarComando(result.Text);
                    LblDebug.Text = $"Último detectado: {texto}"; // Debug visual

                    ProcesarComando(texto); // <--- Lógica de Negocio
                }

                StatusLabel.Text = "✅ Listo";
                StatusFrame.BackgroundColor = Color.FromArgb("#333");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Error: " + ex.Message;
            }
        }

        private static string LimpiarComando(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            return input
                .Trim()
                .ToUpper()
                .Replace(".", "")
                .Replace("!", "")
                .Replace("¡", "")
                .Replace("?", "")
                .Replace("¿", "");
        }

        private void ProcesarComando(string texto)
        {
            // Lógica Checkbox
            if (texto.Contains("SÍ") || texto.Contains("SI"))
            {
                ChkDaño.IsChecked = true;
            }
            else if (texto.Contains("NO"))
            {
                ChkDaño.IsChecked = false;
            }
            // Lógica Limpieza
            else if (texto.Contains("BORRAR") || texto.Contains("LIMPIAR"))
            {
                TxtObservacion.Text = "";
            }
            // Si no es comando corto, asumimos que es dictado para el campo de texto
            else
            {
                // Agregamos el texto al editor (con un espacio si ya hay algo)
                if (!string.IsNullOrEmpty(TxtObservacion.Text))
                    TxtObservacion.Text += " ";

                TxtObservacion.Text += texto;
            }
        }
    }
}
