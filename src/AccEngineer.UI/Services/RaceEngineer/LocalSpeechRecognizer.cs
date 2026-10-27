using System.Speech.Recognition;

namespace AccEngineer.UI.Services.RaceEngineer;

public class LocalSpeechRecognizer : IDisposable
{
    private SpeechRecognitionEngine? _recognizer;
    private bool _initialized;
    private bool _isListening;

    public bool IsAvailable => _initialized;
    public event EventHandler<string>? SpeechRecognized;

    public void Initialize()
    {
        try
        {
            _recognizer = new SpeechRecognitionEngine();
            _recognizer.SetInputToDefaultAudioDevice();
            var dictationGrammar = new DictationGrammar();
            _recognizer.LoadGrammar(dictationGrammar);
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _initialized = true;
        }
        catch
        {
            _initialized = false;
        }
    }

    public void StartPushToTalk()
    {
        if (!_initialized || _recognizer == null || _isListening) return;
        try
        {
            _recognizer.RecognizeAsync(RecognizeMode.Single);
            _isListening = true;
        }
        catch { }
    }

    public void StopPushToTalk()
    {
        if (!_initialized || _recognizer == null) return;
        try
        {
            _recognizer.RecognizeAsyncCancel();
            _isListening = false;
        }
        catch { }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result?.Text != null)
        {
            SpeechRecognized?.Invoke(this, e.Result.Text);
        }
        _isListening = false;
    }

    public void Dispose()
    {
        try
        {
            if (_recognizer != null)
                _recognizer.SpeechRecognized -= OnSpeechRecognized;
            _recognizer?.Dispose();
        }
        catch { }
        _recognizer = null;
        _initialized = false;
    }
}
