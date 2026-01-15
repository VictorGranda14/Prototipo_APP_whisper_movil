using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using AVFoundation;
using Foundation;
using System.Diagnostics;

namespace APP_whisper_movil
{
    public enum AppState
    {
        Navigation,
        Writing,
    }
    public partial class MainPage : ContentPage
    {
        // Variables de Audio y Modelo
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private AVAudioRecorder recorder;
        private NSUrl audioFilePath;

        // Variables de Estado
        private AppState currentState = AppState.Navigation;
        private bool isListening = false; //Indica si se está en modo escucha continua
        private CancellationTokenSource listeningCancellationTokenSource; //Token para cancelar la escucha

        // Variables para VAD
        private DateTime lastSpeechTime = DateTime.MinValue; // Última vez que se detectó voz
        private const int SILENCE_THRESHOLD_MS = 1500; // 1.5 segundos de silencio antes de procesar
        private bool isSpeaking = false; // Indica si el usuario está hablando

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

            recorder = AVAudioRecorder.Create(audioFilePath, settings, out NSError error);
            
            if (error != null)
            {
                StatusLabel.Text = $"Error configurando el audio: {error.LocalizedDescription}";
            }
            else
            {
                recorder.PrepareToRecord();
                recorder.MeteringEnabled = true; // Habilitar medición de niveles para VAD
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

                StatusLabel.TextColor = Colors.White;
                StatusFrame.BackgroundColor = Color.FromArgb("#4aa0ff");
                StatusLabel.Text = "✅ Sistema Listo. Presione el botón.";
            } catch (Exception e)
            {
                StatusLabel.Text = $"Error iniciando el modelo de Whisper: {e.Message}";
            }
        }

        private void OnHablarPressed(object sender, EventArgs e)
        {
            if (processor == null) return;

            if (!isListening)
            {
                StartContinuousListening();
            }
            else
            {
                StopContinuousListening();
            }

            /*
            isRecording = true;
            StatusLabel.Text = "🔴 Escuchando...";
            StatusFrame.BackgroundColor = Colors.Red;
            BtnHablar.BackgroundColor = Colors.DarkRed;
            BtnHablar.Text = "GRABANDO...";

            recorder.Record();*/
        }

        private async void StartContinuousListening()
        {
            isListening = true;
            listeningCancellationTokenSource = new CancellationTokenSource();
            var token = listeningCancellationTokenSource.Token;
            
            recorder.Record();

            BtnHablar.Text = "DETENER ESCUCHA CONTINUA 🛑";
            StatusLabel.Text = "🎧 Modo escucha continua activado.";
            StatusFrame.BackgroundColor = Colors.Green;
            BtnHablar.BackgroundColor = Colors.DarkRed;

            await Task.Run(() => ProcessAudioStream(token));
        }

        private async void StopContinuousListening()
        {
            if (!isListening) return;

            isListening = false;
            listeningCancellationTokenSource?.Cancel();
            recorder.Stop();

            BtnHablar.Text = "PRESIONAR PARA HABLAR 🎙️";
            StatusLabel.Text = "⏸️ Modo escucha continua desactivado.";
            StatusFrame.BackgroundColor = Color.FromArgb("#4aa0ff");
            BtnHablar.BackgroundColor = Color.FromArgb("#4aa0ff");
        }

        private async Task ProcessAudioStream(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Actualizar medición de audio
                    recorder.UpdateMeters();
                    float averagePower = recorder.AveragePower(0); // Canal 0 (mono)
                    float peakPower = recorder.PeakPower(0);

                    Debug.WriteLine($"Average Power: {averagePower}, Peak Power: {peakPower}");
                    
                    // VAD simple basado en nivel de audio (umbral de -25 dB)
                    if (averagePower > -25f)
                    {
                        if (!isSpeaking)
                        {
                            isSpeaking = true; // Asumimos que el usuario está hablando
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                StatusFrame.BackgroundColor = Colors.Orange;
                                StatusLabel.Text = "🔴 Escuchando sonido...";
                            });
                        }
                        lastSpeechTime = DateTime.Now;
                    }
                    else
                    {
                        if (isSpeaking && (DateTime.Now - lastSpeechTime).TotalMilliseconds > SILENCE_THRESHOLD_MS)
                        {
                            Debug.WriteLine($"{(DateTime.Now - lastSpeechTime).TotalMilliseconds}: Silencio detectado, procesando audio...");
                            isSpeaking = false;
                            await ProcessSpeech();
                        }
                    }

                    await Task.Delay(100, token); // Espera breve para evitar sobrecarga
                }
                catch (TaskCanceledException)
                {
                    break; // Salir del bucle si se cancela la tarea
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error en procesando audio: " + ex.Message);
                }
            }
        }

        private async Task ProcessSpeech()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "⏳ Procesando...";
                StatusFrame.BackgroundColor = Colors.Orange;
            });

            if(!File.Exists(audioFilePath.Path))
            {
                Debug.WriteLine("Archivo de audio no encontrado para procesar.");
                return;
            }

            try
            {
                recorder.Stop(); // Detener la grabación para liberar el archivo

                using var fileStream = File.OpenRead(audioFilePath.Path);

                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ProcesarComando(result.Text); // Procesar el texto reconocido
                        });
                    }
                    else
                    {
                        Debug.WriteLine("No se reconoció texto en este fragmento.");
                    }
                }

                // Reiniciar grabación
                recorder.Record();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = "🎧 Modo escucha continua activado.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error procesando el habla: " + ex.Message);
                recorder.Record(); // Asegurarse de reiniciar la grabación
            }
        }

        private void ProcesarComando(string texto)
        {
            // Limpiar texto
            if (string.IsNullOrEmpty(texto)) return;
            texto = texto
                .Trim()
                .ToUpper()
                .Replace(".", "")
                .Replace("!", "")
                .Replace("¡", "")
                .Replace("?", "")
                .Replace("¿", "");

            LblDebug.Text = $"Último detectado: {texto}"; // Debug visual

            LblStatus.Text = $"Estado actual: {currentState}"; // Debug visual
            switch (currentState)
            {
                case AppState.Navigation:
                    ProcesarComandoNavegacion(texto);
                    break;
                case AppState.Writing:
                    ProcesarComandoEscritura(texto);
                    break;
            }
        }

        private void ProcesarComandoNavegacion(string comando)
        {
            // Comando para entrar en modo escritura
            if (comando.Contains("ESCRIBIR OBSERVACIÓN") || comando.Contains("ESCRIBIR OBSERVACION") || comando.Contains("ESCRIBIR NÚMERO") || comando.Contains("ESCRIBIR NUMERO"))
            {
                currentState = AppState.Writing;
                StatusLabel.Text = "📝 MODO ESCRITURA ACTIVADO (Diga 'Finalizar' para salir)";
                BtnHablar.BackgroundColor = Colors.Orange; // Feedback visual importante
                return;
            }

            if (currentState != AppState.Navigation) return;

            // Lógica Checkbox
            if (comando.Contains("SÍ") || comando.Contains("SI"))
            {
                ChkDaño.IsChecked = true;
            }
            else if (comando.Contains("NO"))
            {
                ChkDaño.IsChecked = false;
            }
        }

        private void ProcesarComandoEscritura(string comando)
        {
            if (comando.Contains("FINALIZAR ESCRITURA") || comando == "FINALIZAR")
            {
                currentState = AppState.Navigation;
                StatusLabel.Text = "🔙 MODO NAVEGACIÓN (Diga comandos)";
                BtnHablar.BackgroundColor = Color.FromArgb("#4aa0ff"); // Volver a azul
                return;
            }

            if (comando.Contains("BORRAR") || comando.Contains("LIMPIAR"))
            {
                TxtObservacion.Text = "";
                return;
            }

            if (!string.IsNullOrEmpty(TxtObservacion.Text)) TxtObservacion.Text += " ";

            TxtObservacion.Text += comando;
        }
    }
}
