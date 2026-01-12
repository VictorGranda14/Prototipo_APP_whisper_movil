using NAudio.Wave;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
//using static Android.Renderscripts.ScriptGroup;

namespace APP_whisper_movil
{
    public partial class MainPage : ContentPage
    {
        // Variables de Audio y Modelo
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private WaveInEvent waveIn;
        private MemoryStream audioBuffer;
        private WaveFileWriter waveWriter;
        private bool isRecording = false;

        public MainPage()
        {
            InitializeComponent();
            InitializeWhisper();
        }
        private async void InitializeWhisper()
        {
            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormat(16000, 1);
            try
            {
                //Revisar si existe el modelo
                var modelName = "ggml-small.bin";
                var modelPath = Path.Combine(FileSystem.Current.AppDataDirectory, modelName);

                if (!File.Exists(modelPath)) modelPath = modelName;

                if (!File.Exists(modelPath))
                {
                    StatusLabel.Text = "Error: Modelo no encontrado";
                    return;
                }

                await Task.Run(() =>
                {
                    whisperFactory = WhisperFactory.FromPath(modelPath);
                    processor = whisperFactory.CreateBuilder().WithLanguage("es").Build(); // Creación del procesador (configurado para español)
                });

                StatusLabel.Text = "✅ Sistema Listo. Presione el botón.";

                // Configuración de NAudio
                waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(16000, 1); // 16000 Hz, Mono (compatible con Whisper)

                // Buffer para guardar el audio en memoria RAM
                audioBuffer = new MemoryStream();

                // Definición de evento: Cada vez que el micro llena un buffer, este se escribirá en RAM
                waveIn.DataAvailable += (sender, e) =>
                {
                    if (isRecording && waveWriter != null) waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                };
            } catch (Exception e)
            {
                StatusLabel.Text = $"Error Init: {e.Message}";
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

            // Crear nuevo buffer y writer para cada grabación
            audioBuffer = new MemoryStream();
            waveWriter = new WaveFileWriter(audioBuffer, waveIn.WaveFormat);

            waveIn.StartRecording();
        }

        private async void OnHablarReleased(object sender, EventArgs e)
        {
            if (!isRecording) return;

            isRecording = false;
            waveIn.StopRecording();
            waveWriter.Flush();

            StatusLabel.Text = "⏳ Procesando...";
            StatusFrame.BackgroundColor = Colors.Orange;
            BtnHablar.Text = "MANTENER PARA HABLAR 🎙️";
            BtnHablar.BackgroundColor = Color.FromArgb("#4aa0ff");

            audioBuffer.Position= 0;

            try
            {
                await foreach (var result in processor.ProcessAsync(audioBuffer))
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
            finally
            {
                // Limpiar recursos de esta grabación
                waveWriter?.Dispose();
                waveWriter = null;
                audioBuffer?.Dispose();
                audioBuffer = null;
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
