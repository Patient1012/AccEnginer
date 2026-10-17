using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AccEngineer.UI.Controls;

namespace AccEngineer.UI.Tests;

public sealed class TrackMapTests
{
    [Fact]
    public void Render_WithNonFreezablePathBrush_DoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            // Overlay theme brushes carry a DynamicResource expression on their Color,
            // which makes them non-freezable. Rendering must not assume the pen freezes.
            var brush = new SolidColorBrush();
            BindingOperations.SetBinding(
                brush,
                SolidColorBrush.ColorProperty,
                new Binding { Source = Brushes.Red, Path = new PropertyPath(nameof(SolidColorBrush.Color)) });
            Assert.False(brush.CanFreeze);

            var map = new TrackMap
            {
                PathBrush = brush,
                Path = new[]
                {
                    new MapPoint(0, 0, false),
                    new MapPoint(10, 0, false),
                    new MapPoint(10, 10, false)
                },
                Markers = new[] { new MapPoint(5, 5, true) }
            };

            map.Measure(new Size(120, 120));
            map.Arrange(new Rect(0, 0, 120, 120));
        });
    }

    /// <summary>WPF elements require an STA thread; xUnit runs facts on MTA by default.</summary>
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
