using Azure;
using Azure.AI.OpenAI;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace audio_powered_gpt
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Task audioBackgroundTask = null;
        private SpeechRecognizer speechRecognizer = null;

        public MainWindow()
        {
            this.InitializeComponent();
            this.SpeechKey.Password = "AZURE_COGNITIVE_SERVICES_API_KEY";
            this.SpeechRegion.Text = "westus2";
            this.GptKey.Password = "AZURE_OPENAI_API_KEY";
            this.GptEndpoint.Text = "https://RESOURCE.openai.azure.com/";
            this.GptModel.Text = "DEPLOYMENT_OR_MODEL_NAME";
            this.ChangeToStartButton();
        }

        public async void StartStop(object sender, RoutedEventArgs e)
        {
            lock (this.StartStopBtn)
            {
                if (!this.StartStopBtn.IsEnabled)
                {
                    return;
                }
                this.StartStopBtn.IsEnabled = false;
            }

            if (this.IsRunning())
            {
                await (this.speechRecognizer?.StopContinuousRecognitionAsync() ?? Task.CompletedTask);
                await (this.audioBackgroundTask ?? Task.CompletedTask);
                this.speechRecognizer?.Dispose();
                this.speechRecognizer = null;
                this.audioBackgroundTask = null;
                this.ChangeToStartButton();
            }
            else if (this.Mode.SelectedIndex == 0)  // Translate to Chinese
            {
                this.ResponseConsole.Text = string.Empty;
                this.audioBackgroundTask = this.AudioBackgroundTask(
                    false,
                    this.SpeechKey.Password,
                    this.SpeechRegion.Text,
                    this.GptKey.Password,
                    this.GptEndpoint.Text,
                    this.GptModel.Text);
                this.ChangeToStopButton();
            }
            else if (this.Mode.SelectedIndex == 1)  // Interactive Speech
            {
                this.ResponseConsole.Text = string.Empty;
                this.audioBackgroundTask = this.AudioBackgroundTask(
                    true,
                    this.SpeechKey.Password,
                    this.SpeechRegion.Text,
                    this.GptKey.Password,
                    this.GptEndpoint.Text,
                    this.GptModel.Text);
                this.ChangeToStopButton();
            }
            else if (this.Mode.SelectedIndex == 2)  // Interactive Text
            {
                this.ResponseConsole.Text = string.Empty;
                await this.GptBackgroundTask(this.PromotConsole.Text, this.GptKey.Password, this.GptEndpoint.Text, this.GptModel.Text);
            }
            this.StartStopBtn.IsEnabled = true;
        }

        private bool IsRunning()
        {
            return this.StartStopBtn.Content.ToString().Contains("Stop");
        }

        private void ChangeToStartButton()
        {
            this.StartStopBtn.Content = "▶️ Start";
            this.StartStopBtn.Background = Brushes.LightGreen;
        }

        private void ChangeToStopButton()
        {
            this.StartStopBtn.Content = "⏹️ Stop";
            this.StartStopBtn.Background = Brushes.LightCoral;
        }

        private void AppendToTextBox(TextBox textbox, string text)
        {
            const int MaxLoggedLineCnt = 100;
            textbox.Text += $"⏱️ {DateTime.Now}\n{text}\n";
            textbox.ScrollToEnd();
            while (textbox.LineCount > MaxLoggedLineCnt)
            {
                textbox.Text = textbox.Text[(textbox.Text.IndexOf("\n", StringComparison.OrdinalIgnoreCase) + 1)..];
            }
        }

        private async Task<int> AudioBackgroundTask(bool interactiveMode, string speechKey, string speechRegion, string gptKey, string gptEndpoint, string gptModel)
        {
            TaskCompletionSource<int> stopRecognition = new TaskCompletionSource<int>();

            try
            {
                SpeechConfig speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
                AutoDetectSourceLanguageConfig autoDetect = AutoDetectSourceLanguageConfig.FromLanguages(new string[] { "en-US", "ja-JP", "ko-KR", "zh-TW" });
                AudioConfig audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                this.speechRecognizer = new SpeechRecognizer(speechConfig, autoDetect, audioConfig);

                this.speechRecognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        Trace.WriteLine(e.Result.Text);
                        this.Dispatcher.Invoke(() =>
                        {
                            this.AppendToTextBox(this.PromotConsole, e.Result.Text);
                        });
                        if (e.Result.Text.Trim(' ', '\r', '\n').Length > 1)
                        {
                            string prompt = interactiveMode ? $"{e.Result.Text}" : $"{e.Result.Text} 翻譯成中文";
                            _ = this.GptBackgroundTask(prompt, gptKey, gptEndpoint, gptModel);
                        }
                    }
                    if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            this.AppendToTextBox(this.PromotConsole, "NOMATCH: Speech could not be recognized.");
                        });
                    }
                };

                this.speechRecognizer.Canceled += (s, e) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.AppendToTextBox(this.PromotConsole, $"CANCELED: Reason = {e.Reason}.");
                    });
                    stopRecognition.TrySetResult(0);
                };

                this.speechRecognizer.SessionStopped += (s, e) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.AppendToTextBox(this.PromotConsole, "STOPPED: Recognition session ended.");
                    });
                    stopRecognition.TrySetResult(0);
                };

                await this.speechRecognizer.StartContinuousRecognitionAsync();
            }
            catch (Exception e)
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.AppendToTextBox(this.PromotConsole, $"EXCEPTION: Message = {e.Message}.");
                });
                stopRecognition.TrySetResult(0);
            }

            return await stopRecognition.Task;
        }

        private async Task GptBackgroundTask(string prompt, string gptKey, string gptEndpoint, string gptModel)
        {
            try
            {
                OpenAIClient client = new OpenAIClient(new Uri(gptEndpoint), new AzureKeyCredential(gptKey));
                Response<ChatCompletions> response = await client.GetChatCompletionsAsync(
                    gptModel,
                    new ChatCompletionsOptions()
                    {
                        Messages = { new ChatMessage(ChatRole.System, prompt), },
                        Temperature = (float)0.7,
                        MaxTokens = 500,
                        NucleusSamplingFactor = (float)0.95,
                        FrequencyPenalty = 0,
                        PresencePenalty = 0,
                    });
                ChatCompletions completions = response.Value;
                this.Dispatcher.Invoke(() =>
                {
                    this.AppendToTextBox(this.ResponseConsole, completions.Choices.FirstOrDefault()?.Message.Content ?? string.Empty);
                });
                new ToastContentBuilder().AddText(completions.Choices.FirstOrDefault()?.Message.Content ?? string.Empty).Show(toast =>
                {
                    toast.ExpirationTime = DateTime.Now.AddSeconds(3);
                });
            }
            catch (Exception e)
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.AppendToTextBox(this.ResponseConsole, $"EXCEPTION: Message = {e.Message}.");
                });
            }
        }
    }
}
