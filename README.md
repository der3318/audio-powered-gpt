
## 💬 Audio Powered GPT

![version](https://img.shields.io/badge/version-2.0.1-blue.svg)
![dotnetf](https://img.shields.io/badge/.net-6.0-green.svg)
[![openai](https://img.shields.io/badge/Azure.AI.OpenAI%20%28nuget%29-1.0.0%20beta.5-yellow.svg)](https://www.nuget.org/packages/Azure.AI.OpenAI)
[![speech](https://img.shields.io/badge/Microsoft.CognitiveServices.Speech%20%28nuget%29-1.27.0-pink.svg)](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech)
![portable](https://img.shields.io/badge/portable-win%20x64%2019041+-blueviolet.svg)

A tiny WPF interface that integrates [Azure cognitive service](https://portal.azure.com/#create/Microsoft.CognitiveServicesSpeechServices) with [GPT endpoint](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/how-to/create-resource). This requires Azure subscription resources of both speech service and OpenAI.

![Demo.png](https://github.com/der3318/audio-powered-gpt/blob/main/Images/Demo.png)


### Interactive Mode

Simply type or speak (via microphone) to ask GTP questions in this mode. Press the "start button" to trigger a speech QA session, and click the "start/stop button" again to pause.

![InteractiveMode.png](https://github.com/der3318/audio-powered-gpt/blob/main/Images/InteractiveMode.png)


### Translation Mode

This is the real time translation (into Chinese) functionality. Result texts will also be displayed as a 3-second toast in the bottom corner, so the app can be run completely in the background.

![TranslationMode.png](https://github.com/der3318/audio-powered-gpt/blob/main/Images/TranslationMode.png)

An audio redirection (from speacker to input) interface is a prerequisite to use the feature. Windows stereo mix or [VoiceMeeter](https://vb-audio.com/Voicemeeter/) is probably a good choice.


### References

* Icon: https://arstechnica.com/information-technology/2023/01/openai-and-microsoft-reaffirm-shared-quest-for-powerful-ai-with-new-investment/
* Azure Speech to Text: https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/how-to-recognize-speech
* Azure OpenAI Studio: https://learn.microsoft.com/en-us/azure/cognitive-services/openai/quickstart
* Toast Notification: https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/send-local-toast
* Embedded WPF Markdown Viewer: https://github.com/whistyun/MdXaml
