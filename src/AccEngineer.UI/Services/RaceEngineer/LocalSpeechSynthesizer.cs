using System.Speech.Synthesis;

namespace AccEngineer.UI.Services.RaceEngineer;

public class LocalSpeechSynthesizer : IDisposable
{
    private SpeechSynthesizer? _synth;
    private bool _initialized;

    public bool IsAvailable => _initialized;

    public void Initialize()
    {
        try
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToDefaultAudioDevice();
            _initialized = true;
        }
        catch
        {
            _initialized = false;
        }
    }

    public void Speak(string text, double volume = 1.0)
    {
        if (!_initialized || _synth == null) return;
        try
        {
            _synth.Volume = (int)Math.Clamp(volume * 100, 0, 100);
            _synth.SpeakAsync(text);
        }
        catch { }
    }

    public void Stop()
    {
        try { _synth?.SpeakAsyncCancelAll(); } catch { }
    }

    public void Dispose()
    {
        try { _synth?.Dispose(); } catch { }
        _synth = null;
        _initialized = false;
    }
}
