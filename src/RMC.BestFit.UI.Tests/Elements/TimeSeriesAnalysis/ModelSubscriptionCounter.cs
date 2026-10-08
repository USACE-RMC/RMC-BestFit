using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesAnalysis;

/// <summary>
/// Counts the ARIMAX models subscribed to a time series, so tests can detect replaced models that
/// stay bound to live data.
/// </summary>
internal static class ModelSubscriptionCounter
{
    /// <summary>
    /// Counts the ARIMAX models subscribed to a series' collection-change event.
    /// </summary>
    /// <param name="series">The series whose subscribers are counted.</param>
    /// <returns>The number of handlers whose target is an <see cref="ARIMAX"/> model.</returns>
    internal static int Count(TimeSeries series)
    {
        System.Reflection.FieldInfo? field = null;
        for (Type? type = series.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(nameof(TimeSeries.CollectionChanged),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        Assert.IsNotNull(field, "The series' CollectionChanged backing field was not found.");
        var handler = (Delegate?)field.GetValue(series);
        return handler?.GetInvocationList().Count(subscriber => subscriber.Target is ARIMAX) ?? 0;
    }
}
