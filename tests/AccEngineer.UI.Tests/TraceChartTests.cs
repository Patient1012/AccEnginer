using System.Runtime.ExceptionServices;
using AccEngineer.UI.Controls;

namespace AccEngineer.UI.Tests;

public class TraceChartTests
{
    [Fact]
    public void YAxis_IsOptIn_ByDefault()
    {
        RunOnStaThread(() =>
        {
            var chart = new TraceChart();
            Assert.False(chart.ShowYAxis);
        });
    }

    /// <summary>
    /// WPF elements require an STA thread; xUnit runs facts on MTA by default.
    /// </summary>
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
