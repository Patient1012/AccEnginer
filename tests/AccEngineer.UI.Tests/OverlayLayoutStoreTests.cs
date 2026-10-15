using System.IO;
using AccEngineer.UI.Overlay;

namespace AccEngineer.UI.Tests;

public class OverlayLayoutStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "acc-overlay-tests", Guid.NewGuid().ToString("N"));

    private string PathFor(string name) => Path.Combine(_directory, name);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultProfileAndLayout()
    {
        var store = new OverlayLayoutStore(PathFor("overlay.json"));
        var document = store.Load();

        Assert.Equal(OverlayLayoutDocument.DefaultProfileName, document.ActiveProfile);
        Assert.True(document.Profiles.ContainsKey(OverlayLayoutDocument.DefaultProfileName));
        Assert.Equal(OverlayLayoutDocument.DefaultLayoutName,
            document.Profiles[OverlayLayoutDocument.DefaultProfileName].ActiveLayout);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsProfilesLayoutsAndAppearance()
    {
        var path = PathFor("overlay.json");
        var store = new OverlayLayoutStore(path);

        var document = OverlayLayoutDocument.CreateDefault();
        document.Profiles["acc"].Layouts["Race"] = new OverlayLayout
        {
            Appearance = new OverlayAppearance
            {
                FontScale = 1.25,
                TraceLength = 200,
                RelativeRows = 12,
                RelativeShowGap = false,
                RelativeShowName = true
            },
            Panels = { new OverlayPanelState { Id = "timing", Visible = true, X = 111, Y = 22 } }
        };
        document.Profiles["acc"].ActiveLayout = "Race";
        document.Profiles["iracing"] = new OverlayLayoutProfile
        {
            Simulator = "iracing",
            Layouts = { ["Default"] = new OverlayLayout() }
        };

        store.Save(document);

        var loaded = new OverlayLayoutStore(path).Load();
        Assert.Equal("acc", loaded.ActiveProfile);
        Assert.Contains("iracing", loaded.Profiles.Keys);
        var race = loaded.Profiles["acc"].Layouts["Race"];
        Assert.Equal(1.25, race.Appearance.FontScale);
        Assert.Equal(200, race.Appearance.TraceLength);
        Assert.Equal(12, race.Appearance.RelativeRows);
        Assert.False(race.Appearance.RelativeShowGap);
        Assert.Equal(111, race.Panels.Single(p => p.Id == "timing").X);
    }

    [Fact]
    public void Load_LegacySingleLayout_MigratesIntoDefaultProfile()
    {
        var path = PathFor("overlay.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path,
            """
            {
              "schemaVersion": 1,
              "theme": "ClassicRacelabInspired",
              "panels": [
                { "id": "timing", "visible": true, "x": 10, "y": 20, "width": 260, "height": 158, "opacity": 0.9 }
              ]
            }
            """);

        var document = new OverlayLayoutStore(path).Load();

        var layout = document.Profiles[OverlayLayoutDocument.DefaultProfileName]
            .Layouts[OverlayLayoutDocument.DefaultLayoutName];
        var timing = Assert.Single(layout.Panels);
        Assert.Equal("timing", timing.Id);
        Assert.True(timing.Visible);
        Assert.Equal(10, timing.X);
    }

    [Fact]
    public void Load_PreV3InputsPanel_IsRebasedToLandscape()
    {
        var path = PathFor("overlay.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path,
            """
            {
              "schemaVersion": 2,
              "activeProfile": "acc",
              "profiles": {
                "acc": {
                  "simulator": "acc",
                  "activeLayout": "Default",
                  "layouts": {
                    "Default": {
                      "schemaVersion": 2,
                      "panels": [
                        { "id": "inputs", "visible": true, "x": 300, "y": 40, "width": 240, "height": 210, "opacity": 0.92 },
                        { "id": "timing", "visible": true, "x": 40, "y": 40, "width": 260, "height": 158, "opacity": 0.92 }
                      ]
                    }
                  },
                  "resolutionLayouts": {}
                }
              }
            }
            """);

        var document = new OverlayLayoutStore(path).Load();

        var layout = document.Profiles["acc"].Layouts["Default"];
        var inputs = layout.Panels.Single(p => p.Id == "inputs");
        Assert.Equal(OverlayLayoutSchema.InputsDefaultWidth, inputs.Width);
        Assert.Equal(OverlayLayoutSchema.InputsDefaultHeight, inputs.Height);

        // Other panels keep their saved geometry and the document is stamped as current.
        var timing = layout.Panels.Single(p => p.Id == "timing");
        Assert.Equal(260, timing.Width);
        Assert.Equal(158, timing.Height);
        Assert.Equal(OverlayLayoutSchema.Version, document.SchemaVersion);
    }

    [Fact]
    public void Load_CorruptJson_FallsBackToDefault()
    {
        var path = PathFor("overlay.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ this is not json ");

        var document = new OverlayLayoutStore(path).Load();

        Assert.True(document.Profiles.ContainsKey(OverlayLayoutDocument.DefaultProfileName));
    }

    [Fact]
    public void Load_ProfileWithNoLayouts_GetsADefaultLayout()
    {
        var path = PathFor("overlay.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path,
            """
            {
              "schemaVersion": 2,
              "activeProfile": "acc",
              "profiles": {
                "acc": { "simulator": "acc", "activeLayout": "Missing", "layouts": {}, "resolutionLayouts": {} }
              }
            }
            """);

        var document = new OverlayLayoutStore(path).Load();

        var profile = document.Profiles["acc"];
        Assert.NotEmpty(profile.Layouts);
        Assert.True(profile.Layouts.ContainsKey(profile.ActiveLayout));
    }

    [Fact]
    public void Save_NonFinitePanelValues_AreSanitizedAndRoundTrip()
    {
        var path = PathFor("overlay.json");
        var store = new OverlayLayoutStore(path);

        var document = OverlayLayoutDocument.CreateDefault();
        document.Profiles["acc"].Layouts["Default"].Panels.Add(new OverlayPanelState
        {
            Id = "timing",
            X = double.NaN,
            Y = double.PositiveInfinity,
            Width = double.NaN,
            Height = double.NegativeInfinity,
            Opacity = double.NaN
        });

        // Must not throw: JSON cannot represent NaN/Infinity.
        store.Save(document);

        var timing = new OverlayLayoutStore(path).Load()
            .Profiles["acc"].Layouts["Default"].Panels.Single(p => p.Id == "timing");
        Assert.True(double.IsFinite(timing.X));
        Assert.True(double.IsFinite(timing.Y));
        Assert.True(double.IsFinite(timing.Width));
        Assert.True(double.IsFinite(timing.Height));
        Assert.InRange(timing.Opacity, 0.2, 1.0);
    }

    [Fact]
    public void OverlayPanelState_Sanitize_ClampsRanges()
    {
        var state = new OverlayPanelState
        {
            X = 12, Y = 34, Width = 0, Height = -5, Opacity = 9
        }.Sanitize();

        Assert.Equal(12, state.X);
        Assert.Equal(34, state.Y);
        Assert.True(state.Width >= 1);
        Assert.True(state.Height >= 1);
        Assert.Equal(1.0, state.Opacity);
    }

    [Fact]
    public void OverlayAppearance_Sanitize_ClampsRanges()
    {
        var appearance = new OverlayAppearance
        {
            FontScale = 99, TraceLength = 5, RelativeRows = 100
        }.Sanitize();

        Assert.Equal(2.0, appearance.FontScale);
        Assert.Equal(30, appearance.TraceLength);
        Assert.Equal(20, appearance.RelativeRows);
    }

    [Fact]
    public void OverlayAppearance_CloneIsIndependent()
    {
        var appearance = new OverlayAppearance { FontScale = 1.4, TraceLength = 90 };
        var clone = appearance.Clone();
        clone.TraceLength = 12;

        Assert.Equal(90, appearance.TraceLength);
        Assert.Equal(1.4, clone.FontScale);
    }

    [Fact]
    public void OverlayLayout_CloneDeepCopiesPanelsAndAppearance()
    {
        var layout = new OverlayLayout
        {
            Appearance = new OverlayAppearance { FontScale = 1.1 },
            Panels = { new OverlayPanelState { Id = "timing", X = 5 } }
        };

        var clone = layout.Clone();
        clone.Panels[0].X = 999;
        clone.Appearance.FontScale = 2.0;

        Assert.Equal(5, layout.Panels[0].X);
        Assert.Equal(1.1, layout.Appearance.FontScale);
    }
}
