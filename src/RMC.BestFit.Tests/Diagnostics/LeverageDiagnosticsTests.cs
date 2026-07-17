using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Unit tests for the <c>LeverageDiagnostics</c> class and its nested structs
/// <c>LeverageDiagnostics.ObservationLeverage</c> and
/// <c>LeverageDiagnostics.PriorComponentLeverage</c>.
/// </summary>
/// <remarks>
/// These tests cover the empty constructor, pre-computed constructor, XML round-trip,
/// summary statistics, percentage computation, and helper methods.
/// Computational tests (those that invoke numerical Hessian computation on model data)
/// belong in the Verification project.
/// </remarks>
[TestClass]
public class LeverageDiagnosticsTests
{
    #region Empty Constructor Tests

    /// <summary>
    /// Empty constructor initializes all arrays to empty and scalar properties to 0.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_InitializesEmptyState()
    {
        var diag = new LeverageDiagnostics();

        Assert.IsNotNull(diag.Observations);
        Assert.IsNotNull(diag.PriorComponents);
        Assert.AreEqual(0, diag.Observations.Length);
        Assert.AreEqual(0, diag.PriorComponents.Length);
        Assert.AreEqual(0, diag.NumberOfParameters);
        Assert.AreEqual(0, diag.Count);
        Assert.AreEqual(0.0, diag.TotalLeverage);
        Assert.AreEqual(0.0, diag.TotalObservationLeverage);
        Assert.AreEqual(0.0, diag.TotalPriorLeverage);
        Assert.AreEqual(0.0, diag.TotalFitInfluence);
        Assert.AreEqual(0.0, diag.TotalVarianceInfluence);
    }

    #endregion

    #region Pre-Computed Constructor Tests

    /// <summary>
    /// Pre-computed constructor with one observation sets totals correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_PreComputed_OneObservation_SetsTotalsCorrectly()
    {
        var obs = new LeverageDiagnostics.ObservationLeverage(
            index: 0, leverage: 0.8, percentOfTotal: 0,
            fitInfluence: 0.5, varianceInfluence: 0.3,
            percentFitOfTotal: 0, percentVarianceOfTotal: 0,
            value: 100.0, dataType: DataComponentType.Exact, count: 1);

        var diag = new LeverageDiagnostics(
            new[] { obs },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        Assert.AreEqual(1, diag.Observations.Length);
        Assert.AreEqual(0, diag.PriorComponents.Length);
        Assert.AreEqual(2, diag.NumberOfParameters);
        Assert.AreEqual(0.8, diag.TotalObservationLeverage, 1e-10);
        Assert.AreEqual(0.0, diag.TotalPriorLeverage, 1e-10);
        Assert.AreEqual(0.8, diag.TotalLeverage, 1e-10);
        Assert.AreEqual(0.5, diag.TotalFitInfluence, 1e-10);
        Assert.AreEqual(0.3, diag.TotalVarianceInfluence, 1e-10);
    }

    /// <summary>
    /// Pre-computed constructor with null observations array uses empty array.
    /// </summary>
    [TestMethod]
    public void Constructor_PreComputed_NullObservations_UsesEmptyArray()
    {
        var diag = new LeverageDiagnostics(null!, null!, numberOfParameters: 3);

        Assert.IsNotNull(diag.Observations);
        Assert.IsNotNull(diag.PriorComponents);
        Assert.AreEqual(0, diag.Observations.Length);
        Assert.AreEqual(0, diag.PriorComponents.Length);
    }

    /// <summary>
    /// Pre-computed constructor with multiple observations sums fit and variance correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_PreComputed_MultipleObservations_SumsCorrectly()
    {
        var obs1 = new LeverageDiagnostics.ObservationLeverage(
            0, 1.0, 0, 0.6, 0.4, 0, 0, 50.0, DataComponentType.Exact);
        var obs2 = new LeverageDiagnostics.ObservationLeverage(
            1, 0.5, 0, 0.2, 0.3, 0, 0, 75.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs1, obs2 },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        Assert.AreEqual(1.5, diag.TotalObservationLeverage, 1e-10);
        Assert.AreEqual(1.5, diag.TotalLeverage, 1e-10);
        Assert.AreEqual(0.8, diag.TotalFitInfluence, 1e-10);
        Assert.AreEqual(0.7, diag.TotalVarianceInfluence, 1e-10);
    }

    /// <summary>
    /// Pre-computed constructor updates percentage of total on each observation.
    /// </summary>
    [TestMethod]
    public void Constructor_PreComputed_UpdatesPercentages()
    {
        var obs1 = new LeverageDiagnostics.ObservationLeverage(
            0, 1.0, 0, 0.6, 0.4, 0, 0, 50.0, DataComponentType.Exact);
        var obs2 = new LeverageDiagnostics.ObservationLeverage(
            1, 3.0, 0, 1.5, 1.5, 0, 0, 75.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs1, obs2 },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        // obs1 has leverage 1.0 out of 4.0 total = 25%
        Assert.AreEqual(25.0, diag.Observations[0].PercentOfTotal, 1e-8);
        // obs2 has leverage 3.0 out of 4.0 total = 75%
        Assert.AreEqual(75.0, diag.Observations[1].PercentOfTotal, 1e-8);
    }

    /// <summary>
    /// Pre-computed constructor with prior component sets TotalPriorLeverage.
    /// </summary>
    [TestMethod]
    public void Constructor_PreComputed_WithPriorComponent_SetsPriorTotal()
    {
        var prior = new LeverageDiagnostics.PriorComponentLeverage(
            "mu_prior", PriorComponentType.ParameterPrior,
            leverage: 0.4, percentOfTotal: 0,
            fitInfluence: 0.2, varianceInfluence: 0.2,
            percentFitOfTotal: 0, percentVarianceOfTotal: 0);

        var diag = new LeverageDiagnostics(
            Array.Empty<LeverageDiagnostics.ObservationLeverage>(),
            new[] { prior },
            numberOfParameters: 2);

        Assert.AreEqual(0.4, diag.TotalPriorLeverage, 1e-10);
        Assert.AreEqual(0.4, diag.TotalLeverage, 1e-10);
    }

    #endregion

    #region XML Serialization Round-Trip Tests

    /// <summary>
    /// ToXElement/FromXElement round-trip preserves scalar properties.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_ScalarProperties_Preserved()
    {
        var obs = new LeverageDiagnostics.ObservationLeverage(
            0, 1.2, 60.0, 0.7, 0.5, 35.0, 25.0, 200.0, DataComponentType.Exact, 1, "site1");

        var diag = new LeverageDiagnostics(
            new[] { obs },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 3);

        var xElement = diag.ToXElement();
        var restored = new LeverageDiagnostics(xElement);

        Assert.AreEqual(diag.NumberOfParameters, restored.NumberOfParameters);
        Assert.AreEqual(diag.TotalLeverage, restored.TotalLeverage, 1e-10);
        Assert.AreEqual(diag.TotalObservationLeverage, restored.TotalObservationLeverage, 1e-10);
        Assert.AreEqual(diag.TotalPriorLeverage, restored.TotalPriorLeverage, 1e-10);
        Assert.AreEqual(diag.TotalFitInfluence, restored.TotalFitInfluence, 1e-10);
        Assert.AreEqual(diag.TotalVarianceInfluence, restored.TotalVarianceInfluence, 1e-10);
    }

    /// <summary>
    /// ToXElement/FromXElement round-trip preserves observation count.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_ObservationCount_Preserved()
    {
        var obs1 = new LeverageDiagnostics.ObservationLeverage(
            0, 0.5, 50.0, 0.3, 0.2, 25.0, 25.0, 10.0, DataComponentType.Exact);
        var obs2 = new LeverageDiagnostics.ObservationLeverage(
            1, 0.5, 50.0, 0.3, 0.2, 25.0, 25.0, 20.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs1, obs2 },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        var restored = new LeverageDiagnostics(diag.ToXElement());

        Assert.AreEqual(2, restored.Observations.Length);
        Assert.AreEqual(0, restored.PriorComponents.Length);
    }

    /// <summary>
    /// ToXElement/FromXElement round-trip preserves prior component count.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PriorComponentCount_Preserved()
    {
        var prior = new LeverageDiagnostics.PriorComponentLeverage(
            "quantile_prior", PriorComponentType.QuantilePrior,
            0.6, 100.0, 0.3, 0.3, 50.0, 50.0);

        var diag = new LeverageDiagnostics(
            Array.Empty<LeverageDiagnostics.ObservationLeverage>(),
            new[] { prior },
            numberOfParameters: 2);

        var restored = new LeverageDiagnostics(diag.ToXElement());

        Assert.AreEqual(1, restored.PriorComponents.Length);
        Assert.AreEqual("quantile_prior", restored.PriorComponents[0].Name);
        Assert.AreEqual(PriorComponentType.QuantilePrior, restored.PriorComponents[0].Type);
    }

    /// <summary>
    /// FromXElement with null XElement uses default empty state (does not throw).
    /// </summary>
    [TestMethod]
    public void Constructor_XmlNull_UsesEmptyState()
    {
        var diag = new LeverageDiagnostics((XElement)null!);

        Assert.IsNotNull(diag.Observations);
        Assert.IsNotNull(diag.PriorComponents);
        Assert.AreEqual(0, diag.NumberOfParameters);
    }

    #endregion

    #region GetMostInfluentialObservations Tests

    /// <summary>
    /// GetMostInfluentialObservations returns top-N observations sorted by leverage descending.
    /// </summary>
    [TestMethod]
    public void GetMostInfluentialObservations_ReturnsTopNDescending()
    {
        var obs1 = new LeverageDiagnostics.ObservationLeverage(
            0, 0.3, 0, 0.2, 0.1, 0, 0, 10.0, DataComponentType.Exact);
        var obs2 = new LeverageDiagnostics.ObservationLeverage(
            1, 1.5, 0, 0.8, 0.7, 0, 0, 20.0, DataComponentType.Exact);
        var obs3 = new LeverageDiagnostics.ObservationLeverage(
            2, 0.7, 0, 0.4, 0.3, 0, 0, 30.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs1, obs2, obs3 },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        var top2 = diag.GetMostInfluentialObservations(2);

        Assert.AreEqual(2, top2.Length);
        Assert.AreEqual(1.5, top2[0].Leverage, 1e-10, "First should be highest leverage.");
        Assert.AreEqual(0.7, top2[1].Leverage, 1e-10, "Second should be next highest.");
    }

    /// <summary>
    /// GetMostInfluentialObservations with topN larger than count returns all.
    /// </summary>
    [TestMethod]
    public void GetMostInfluentialObservations_TopNLargerThanCount_ReturnsAll()
    {
        var obs = new LeverageDiagnostics.ObservationLeverage(
            0, 1.0, 0, 0.5, 0.5, 0, 0, 100.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 2);

        var result = diag.GetMostInfluentialObservations(100);

        Assert.AreEqual(1, result.Length);
    }

    /// <summary>
    /// GetMostInfluentialObservations on empty diagnostics returns empty array.
    /// </summary>
    [TestMethod]
    public void GetMostInfluentialObservations_EmptyDiagnostics_ReturnsEmpty()
    {
        var diag = new LeverageDiagnostics();
        var result = diag.GetMostInfluentialObservations(5);

        Assert.AreEqual(0, result.Length);
    }

    #endregion

    #region GetSummary Tests

    /// <summary>
    /// GetSummary on empty diagnostics returns "No leverage diagnostics available." message.
    /// </summary>
    [TestMethod]
    public void GetSummary_EmptyDiagnostics_ReturnsNoDataMessage()
    {
        var diag = new LeverageDiagnostics();
        var summary = diag.GetSummary();

        Assert.IsTrue(summary.Contains("No leverage diagnostics"),
            $"Expected no-data message, got: {summary}");
    }

    /// <summary>
    /// GetSummary with data contains parameter count.
    /// </summary>
    [TestMethod]
    public void GetSummary_WithData_ContainsParameterCount()
    {
        var obs = new LeverageDiagnostics.ObservationLeverage(
            0, 1.0, 100.0, 0.6, 0.4, 60.0, 40.0, 50.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 3);

        var summary = diag.GetSummary();
        Assert.IsTrue(summary.Contains("p = 3"), $"Expected 'p = 3' in summary: {summary}");
    }

    #endregion

    #region ObservationLeverage Struct Tests

    /// <summary>
    /// ObservationLeverage constructor sets all properties.
    /// </summary>
    [TestMethod]
    public void ObservationLeverage_Constructor_SetsAllProperties()
    {
        var obs = new LeverageDiagnostics.ObservationLeverage(
            index: 5, leverage: 2.1, percentOfTotal: 42.0,
            fitInfluence: 1.2, varianceInfluence: 0.9,
            percentFitOfTotal: 57.1, percentVarianceOfTotal: 42.9,
            value: 3500.0, dataType: DataComponentType.Interval,
            count: 3, name: "historic");

        Assert.AreEqual(5, obs.Index);
        Assert.AreEqual(2.1, obs.Leverage, 1e-10);
        Assert.AreEqual(42.0, obs.PercentOfTotal, 1e-10);
        Assert.AreEqual(1.2, obs.FitInfluence, 1e-10);
        Assert.AreEqual(0.9, obs.VarianceInfluence, 1e-10);
        Assert.AreEqual(57.1, obs.PercentFitOfTotal, 1e-10);
        Assert.AreEqual(42.9, obs.PercentVarianceOfTotal, 1e-10);
        Assert.AreEqual(3500.0, obs.Value, 1e-10);
        Assert.AreEqual(DataComponentType.Interval, obs.DataType);
        Assert.AreEqual(3, obs.Count);
        Assert.AreEqual("historic", obs.Name);
    }

    /// <summary>
    /// ObservationLeverage XML round-trip preserves all fields.
    /// </summary>
    [TestMethod]
    public void ObservationLeverage_XmlRoundTrip_PreservesAllFields()
    {
        var original = new LeverageDiagnostics.ObservationLeverage(
            2, 1.5, 30.0, 0.8, 0.7, 16.0, 14.0, 999.9, DataComponentType.LeftCensored, 5, "paleoflood");

        var xElement = original.ToXElement();
        var restored = new LeverageDiagnostics.ObservationLeverage(xElement);

        Assert.AreEqual(original.Index, restored.Index);
        Assert.AreEqual(original.Leverage, restored.Leverage, 1e-10);
        Assert.AreEqual(original.PercentOfTotal, restored.PercentOfTotal, 1e-10);
        Assert.AreEqual(original.FitInfluence, restored.FitInfluence, 1e-10);
        Assert.AreEqual(original.VarianceInfluence, restored.VarianceInfluence, 1e-10);
        Assert.AreEqual(original.PercentFitOfTotal, restored.PercentFitOfTotal, 1e-10);
        Assert.AreEqual(original.PercentVarianceOfTotal, restored.PercentVarianceOfTotal, 1e-10);
        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.DataType, restored.DataType);
        Assert.AreEqual(original.Count, restored.Count);
        Assert.AreEqual(original.Name, restored.Name);
    }

    /// <summary>
    /// ObservationLeverage XML round-trip without name preserves null.
    /// </summary>
    [TestMethod]
    public void ObservationLeverage_XmlRoundTrip_NullName_PreservesNull()
    {
        var original = new LeverageDiagnostics.ObservationLeverage(
            0, 0.5, 100.0, 0.3, 0.2, 60.0, 40.0, 100.0, DataComponentType.Exact, 1, null);

        var restored = new LeverageDiagnostics.ObservationLeverage(original.ToXElement());

        Assert.IsNull(restored.Name);
    }

    #endregion

    #region PriorComponentLeverage Struct Tests

    /// <summary>
    /// PriorComponentLeverage constructor sets all properties.
    /// </summary>
    [TestMethod]
    public void PriorComponentLeverage_Constructor_SetsAllProperties()
    {
        var prior = new LeverageDiagnostics.PriorComponentLeverage(
            "jeffreys_scale", PriorComponentType.JeffreysScalePrior,
            leverage: 0.7, percentOfTotal: 35.0,
            fitInfluence: 0.4, varianceInfluence: 0.3,
            percentFitOfTotal: 20.0, percentVarianceOfTotal: 15.0);

        Assert.AreEqual("jeffreys_scale", prior.Name);
        Assert.AreEqual(PriorComponentType.JeffreysScalePrior, prior.Type);
        Assert.AreEqual(0.7, prior.Leverage, 1e-10);
        Assert.AreEqual(35.0, prior.PercentOfTotal, 1e-10);
        Assert.AreEqual(0.4, prior.FitInfluence, 1e-10);
        Assert.AreEqual(0.3, prior.VarianceInfluence, 1e-10);
        Assert.AreEqual(20.0, prior.PercentFitOfTotal, 1e-10);
        Assert.AreEqual(15.0, prior.PercentVarianceOfTotal, 1e-10);
    }

    /// <summary>
    /// PriorComponentLeverage XML round-trip preserves all fields.
    /// </summary>
    [TestMethod]
    public void PriorComponentLeverage_XmlRoundTrip_PreservesAllFields()
    {
        var original = new LeverageDiagnostics.PriorComponentLeverage(
            "quantile_prior", PriorComponentType.QuantilePrior,
            0.9, 45.0, 0.5, 0.4, 25.0, 20.0);

        var xElement = original.ToXElement();
        var restored = new LeverageDiagnostics.PriorComponentLeverage(xElement);

        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Type, restored.Type);
        Assert.AreEqual(original.Leverage, restored.Leverage, 1e-10);
        Assert.AreEqual(original.PercentOfTotal, restored.PercentOfTotal, 1e-10);
        Assert.AreEqual(original.FitInfluence, restored.FitInfluence, 1e-10);
        Assert.AreEqual(original.VarianceInfluence, restored.VarianceInfluence, 1e-10);
        Assert.AreEqual(original.PercentFitOfTotal, restored.PercentFitOfTotal, 1e-10);
        Assert.AreEqual(original.PercentVarianceOfTotal, restored.PercentVarianceOfTotal, 1e-10);
    }

    /// <summary>
    /// PriorComponentLeverage ToXElement produces element named PriorComponent.
    /// </summary>
    [TestMethod]
    public void PriorComponentLeverage_ToXElement_ElementNameIsCorrect()
    {
        var prior = new LeverageDiagnostics.PriorComponentLeverage(
            "test", PriorComponentType.ParameterPrior, 0.5, 100.0, 0.3, 0.2, 60.0, 40.0);

        var el = prior.ToXElement();

        Assert.AreEqual("PriorComponent", el.Name.LocalName);
    }

    #endregion

    #region Count Property Tests

    /// <summary>
    /// Count property reflects the number of observations.
    /// </summary>
    [TestMethod]
    public void Count_ReflectsObservationCount()
    {
        var obs1 = new LeverageDiagnostics.ObservationLeverage(
            0, 0.5, 0, 0.3, 0.2, 0, 0, 1.0, DataComponentType.Exact);
        var obs2 = new LeverageDiagnostics.ObservationLeverage(
            1, 0.5, 0, 0.3, 0.2, 0, 0, 2.0, DataComponentType.Exact);

        var diag = new LeverageDiagnostics(
            new[] { obs1, obs2 },
            Array.Empty<LeverageDiagnostics.PriorComponentLeverage>(),
            numberOfParameters: 1);

        Assert.AreEqual(2, diag.Count);
    }

    #endregion
}
