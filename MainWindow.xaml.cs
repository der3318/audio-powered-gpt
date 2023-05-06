using Azure;
using Azure.AI.OpenAI;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.Reflection;

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
            this.ChangeReponseToTextView();
        }

        public void ConfigsLoad(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    Dictionary<string, string> configs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(dlg.FileName));
                    this.SpeechKey.Password = configs?.GetValueOrDefault("SpeechKey") ?? string.Empty;
                    this.SpeechRegion.Text = configs?.GetValueOrDefault("SpeechRegion") ?? string.Empty;
                    this.GptKey.Password = configs?.GetValueOrDefault("GptKey") ?? string.Empty;
                    this.GptEndpoint.Text = configs?.GetValueOrDefault("GptEndpoint") ?? string.Empty;
                    this.GptModel.Text = configs?.GetValueOrDefault("GptModel") ?? string.Empty;
                    this.ShowToast($"Loaded: {dlg.FileName}");
                }
                catch (Exception exception)
                {
                    this.ShowToast($"Error: {exception.Message}");
                }
            }
        }

        public void ConfigsSave(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                Dictionary<string, string> configs = new Dictionary<string, string>
                {
                    { "SpeechKey", this.SpeechKey.Password },
                    { "SpeechRegion", this.SpeechRegion.Text },
                    { "GptKey", this.GptKey.Password },
                    { "GptEndpoint", this.GptEndpoint.Text },
                    { "GptModel", this.GptModel.Text }
                };
                string json = JsonSerializer.Serialize(configs);
                File.WriteAllText(dlg.FileName, json);
                this.ShowToast($"Saved: {dlg.FileName}");
            }
        }

        public void AboutVersion(object sender, RoutedEventArgs e)
        {
            this.ShowToast($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        }

        public void AboutSource(object sender, RoutedEventArgs e)
        {
            string destinationurl = "https://github.com/der3318/audio-powered-gpt";
            Process.Start(new ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            });
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
                this.ResponseMdConsole.Markdown = string.Empty;
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
                this.ResponseMdConsole.Markdown = string.Empty;
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
                this.ResponseMdConsole.Markdown = string.Empty;
                await this.GptBackgroundTask(this.PromotConsole.Text, this.GptKey.Password, this.GptEndpoint.Text, this.GptModel.Text);
            }
            this.StartStopBtn.IsEnabled = true;
        }

        public void TextMarkdown(object sender, RoutedEventArgs e)
        {
            lock (this.TextMarkdownBtn)
            {
                if (this.IsUsingMarkdown())
                {
                    this.ChangeReponseToTextView();
                }
                else
                {
                    this.ChangeReponseToMarkdownView();
                }
            }
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

        private bool IsUsingMarkdown()
        {
            return this.TextMarkdownBtn.Content.ToString().Contains("Text");
        }

        private void ChangeReponseToTextView()
        {
            this.ResponseConsole.Visibility = Visibility.Visible;
            this.ResponseMdConsole.Visibility = Visibility.Collapsed;
            this.TextMarkdownBtn.Content = "📰 Switch to Markdown View";
            this.UpdateLayout();
        }

        private void ChangeReponseToMarkdownView()
        {
            this.ResponseConsole.Visibility = Visibility.Collapsed;
            this.ResponseMdConsole.Visibility = Visibility.Visible;
            this.TextMarkdownBtn.Content = "🗒 Switch to Text View";
            this.UpdateLayout();
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

        private void RenderMarkdown(string text)
        {
            this.ResponseMdConsole.Markdown = $"{text}";
        }

        private void ShowToast(string text)
        {
            new ToastContentBuilder().AddText(text).Show(toast =>
            {
                toast.ExpirationTime = DateTime.Now.AddSeconds(3);
            });
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
                    this.AppendToTextBox(this.PromotConsole, $"EXCEPTION: Message = {e.Message}.\n{e.StackTrace}");
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
                string message = completions.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
                this.Dispatcher.Invoke(() =>
                {
                    this.AppendToTextBox(this.ResponseConsole, message);
                    this.RenderMarkdown(message);
                });
                this.ShowToast(message);
            }
            catch (Exception e)
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.AppendToTextBox(this.ResponseConsole, $"EXCEPTION: Message = {e.Message}.\n{e.StackTrace}");
                    this.RenderMarkdown($"EXCEPTION: Message = {e.Message}.\n{e.StackTrace}");
                });
            }
        }
    }
}
