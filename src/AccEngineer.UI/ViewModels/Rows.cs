using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AccEngineer.Analysis;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>One reconstructed lap as shown in the Analysis table.</summary>
public sealed class LapRowViewModel
{
    public LapRowViewModel(LapTimeline lap)
    {
        LapNumber = lap.LapNumber;
        LapTime = Display.LapTime(lap.LapTimeSeconds);
        IsValid = lap.IsValid;
        IsComplete = lap.IsComplete;
        Source = lap.LapTimeSource switch
        {
            LapTimeSource.AccReported => "ACC",
            LapTimeSource.ComputedBoundary => "computed",
            LapTimeSource.PartialSpan => "partial",
            _ => "unavailable"
        };
        Samples = lap.Samples.Count.ToString(CultureInfo.InvariantCulture);
        Sectors = lap.Sectors.Count == 0
            ? Display.Missing
            : string.Join(" / ", lap.Sectors.Select(s => Display.LapTime(s.TimeSeconds)));
        StatusText = !IsComplete ? "Incomplete" : IsValid ? "Valid" : "Invalid";
    }

    public int LapNumber { get; }

    public string LapTime { get; }

    public bool IsValid { get; }

    public bool IsComplete { get; }

    public string Source { get; }

    public string Samples { get; }

    public string Sectors { get; }

    public string StatusText { get; }
}

/// <summary>A gain/loss region within a lap comparison.</summary>
public sealed class RegionRowViewModel
{
    public RegionRowViewModel(GainLossRegion region)
    {
        IsGain = region.IsGain;
        Type = region.IsGain ? "Gain" : "Loss";
        Range = $"{region.StartProgressMeters.ToString("0", CultureInfo.InvariantCulture)}–" +
                $"{region.EndProgressMeters.ToString("0", CultureInfo.InvariantCulture)} m";
        Delta = Display.Delta(region.DeltaSeconds);
    }

    public bool IsGain { get; }

    public string Type { get; }

    public string Range { get; }

    public string Delta { get; }
}

/// <summary>One lap's delta against the reference lap, with its regions.</summary>
public sealed class LapDeltaRowViewModel
{
    public LapDeltaRowViewModel(LapDelta delta, bool isReference)
    {
        LapNumber = delta.LapNumber;
        IsReference = isReference;
        LapTime = Display.LapTime(delta.LapTimeSeconds);
        TotalDelta = isReference ? "reference" : Display.Delta(delta.TotalDeltaSeconds);
        EndDelta = isReference ? "reference" : Display.Delta(delta.DeltaAtEndSeconds);
        Regions = new ObservableCollection<RegionRowViewModel>(delta.Regions.Select(r => new RegionRowViewModel(r)));
    }

    public int LapNumber { get; }

    public bool IsReference { get; }

    public string LapTime { get; }

    public string TotalDelta { get; }

    public string EndDelta { get; }

    public ObservableCollection<RegionRowViewModel> Regions { get; }

    public bool HasRegions => Regions.Count > 0;
}

/// <summary>A generated report file pair on disk.</summary>
public sealed class ReportEntry
{
    public ReportEntry(string markdownPath, string jsonPath, DateTimeOffset lastWriteUtc)
    {
        MarkdownPath = markdownPath;
        JsonPath = jsonPath;
        LastWriteUtc = lastWriteUtc;
        FileName = Path.GetFileName(markdownPath);
    }

    public string MarkdownPath { get; }

    public string JsonPath { get; }

    public DateTimeOffset LastWriteUtc { get; }

    public string FileName { get; }

    public string CreatedText => LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
