using System.Text.Json;
using AccEngineer.Telemetry;

Console.WriteLine("Track Map Drawer - ACC Track Outline Recorder");
Console.WriteLine("===========================================");
Console.WriteLine();

var outputDir = Path.Combine(Environment.CurrentDirectory, "track-maps");
Directory.CreateDirectory(outputDir);
Console.WriteLine($"Output directory: {outputDir}");
Console.WriteLine();

Console.WriteLine("Attempting to connect to ACC shared memory...");
Console.WriteLine("Make sure ACC is running and you're in a session.");
Console.WriteLine("Drive around to record the track outline.");
Console.WriteLine("Press Ctrl+C to stop and save.");
Console.WriteLine();

var reader = new AccSharedMemoryReader();
var points = new List<(double X, double Z, DateTime Time)>();
var seenTracks = new Dictionary<string, List<(double X, double Z)>>();
var lastTrack = "";
var sampleCount = 0;
var pointCount = 0;

Console.CancelKeyPress += (s, e) => {
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopping...");
    SaveAll(seenTracks, outputDir);
    Environment.Exit(0);
};

while (true)
{
    try
    {
        var sample = reader.ReadLatest();
        if (sample != null)
        {
            sampleCount++;
            if (sampleCount % 50 == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Samples: {sampleCount} | Points collected: {pointCount} | Track: {sample.TrackName ?? "Unknown"}");
            }
            
            if (!string.IsNullOrEmpty(sample.TrackName) && 
                sample.PositionXMeters.HasValue && sample.PositionZMeters.HasValue &&
                sample.SpeedKph > 10) // Only collect when moving
            {
                var track = sample.TrackName;
                if (!seenTracks.ContainsKey(track))
                {
                    seenTracks[track] = new List<(double X, double Z)>();
                    Console.WriteLine($"[NEW TRACK] Started recording: {track}");
                    lastTrack = track;
                }
                
                var list = seenTracks[track];
                // Avoid duplicate points very close together
                if (list.Count == 0 || 
                    Math.Sqrt(Math.Pow(list[list.Count-1].X - sample.PositionXMeters.Value, 2) + 
                              Math.Pow(list[list.Count-1].Z - sample.PositionZMeters.Value, 2)) > 2.0)
                {
                    list.Add((sample.PositionXMeters.Value, sample.PositionZMeters.Value));
                    pointCount++;
                }
            }
        }
        Thread.Sleep(50);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
        Thread.Sleep(1000);
    }
}

static void SaveAll(Dictionary<string, List<(double X, double Z)>> tracks, string outputDir)
{
    foreach (var kvp in tracks)
    {
        if (kvp.Value.Count < 50)
        {
            Console.WriteLine($"[SKIP] {kvp.Key}: only {kvp.Value.Count} points (need at least 50)");
            continue;
        }
        
        // Downsample to reasonable number of points
        var sampled = Downsample(kvp.Value, 1000);
        var data = sampled.Select(p => new { X = p.X, Z = p.Z }).ToList();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var safeName = string.Join("_", kvp.Key.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(outputDir, safeName + ".json");
        File.WriteAllText(path, json);
        Console.WriteLine($"[SAVED] {kvp.Key}: {sampled.Count} points -> {path}");
    }
    
    if (tracks.Count == 0)
        Console.WriteLine("[INFO] No tracks recorded");
}

static List<(double X, double Z)> Downsample(List<(double X, double Z)> points, int maxPoints)
{
    if (points.Count <= maxPoints) return points;
    var result = new List<(double X, double Z)>(maxPoints);
    var step = (double)points.Count / maxPoints;
    for (int i = 0; i < maxPoints; i++)
    {
        result.Add(points[(int)(i * step)]);
    }
    return result;
}
