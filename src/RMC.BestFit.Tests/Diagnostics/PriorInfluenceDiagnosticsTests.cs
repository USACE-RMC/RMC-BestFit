using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="PriorInfluenceDiagnostics"/> and the nested
/// <see cref="PriorComponentSummary"/> struct.
/// </summary>
/// <remarks>
/// Tests cover: empty constructor, components constructor, XML serialization,
/// helper methods (GetComponentsByType, GetMostConstrainingComponents,
/// GetContributionByType, GetSummary, indexer), and summary statistics.
/// Computational tests using MCMCResults belong in the Verification project.
/// </remarks>
[TestClass]
public class PriorInfluenceDiagnosticsTests
{
    #region Helpers

    /// <summary>
    /// Creates a <see cref="PriorComponentSummary"/> with the given type and mean LL.
    /// </summary>
    private static PriorComponentSummary MakeSummary(
        string name, PriorComponentType type, double mean, double sd = 0.1, double min = -5.0, double max = 0.0)
        => new PriorComponentSummary(name, type, mean, sd, min, max);

    /// <summary>
    /// Constructs a <see cref="PriorInfluenceDiagnostics"/> with specific
    /// <c>TotalPriorLogLikelihood</c>, <c>TotalDataLogLikelihood</c>, and derived
    /// <c>PriorToDataRatio</c> by building an XElement and round-tripping through
    /// the XML constructor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In production, the canonical path is
    /// <c>BayesianAnalysis.ComputePriorInfluenceDiagnostics</c> →
    /// <c>new PriorInfluenceDiagnostics(Model, Results, thinEvery)</c>, which invokes
    /// <c>ComputeFromPosterior</c> to populate the Total* fields from MCMC posterior
    /// samples. The <c>(PriorComponentSummary[])</c> constructor does NOT roll up
    /// component means into Total* fields — those stay at their defaults (0).
    /// </para>
    /// <para>
    /// Unit tests that need to exercise specific ratio / influence scenarios construct
    /// via this helper (XML round-trip reads the Total* attributes directly).
    /// </para>
    /// </remarks>
    /// <param name="totalPriorLL">Total prior log-likelihood to inject.</param>
    /// <param name="totalDataLL">Total data log-likelihood to inject.</param>
    /// <param name="components">Optional component summaries to serialize as child elements.</param>
    /// <returns>A <see cref="PriorInfluenceDiagnostics"/> instance with the specified Total* values and a ratio computed from the same formula the production code uses: |prior| / (|prior| + |data|).</returns>
    private static PriorInfluenceDiagnostics CreateDiagnosticsForTesting(
        double totalPriorLL, double totalDataLL,
        PriorComponentSummary[]? components = null)
    {
        double absPrior = Math.Abs(totalPriorLL);
        double absData = Math.Abs(totalDataLL);
        double total = absPrior + absData;
        double ratio = total > 0 ? absPrior / total : 0;

        var xElement = new XElement("PriorInfluenceDiagnostics",
            new XAttribute("TotalPriorLogLikelihood", totalPriorLL.ToString("G17", CultureInfo.InvariantCulture)),
            new XAttribute("TotalDataLogLikelihood", totalDataLL.ToString("G17", CultureInfo.InvariantCulture)),
            new XAttribute("PriorToDataRatio", ratio.ToString("G17", CultureInfo.InvariantCulture)));

        if (components != null)
        {
            foreach (var comp in components)
                xElement.Add(comp.ToXElement());
        }

        return new PriorInfluenceDiagnostics(xElement);
    }

    #endregion

    #region Empty Constructor Tests

    /// <summary>
    /// Empty constructor creates instance with empty Components array.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_EmptyComponentsArray()
    {
        var diag = new PriorInfluenceDiagnostics();

        Assert.IsNotNull(diag.Components);
        Assert.AreEqual(0, diag.Components.Length);
        Assert.AreEqual(0, diag.Count);
    }

    #endregion

    #region Components Constructor Tests

    /// <summary>
    /// Components constructor with null throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_Components_Null_ThrowsArgumentNullException()
    {
        _ = new PriorInfluenceDiagnostics((PriorComponentSummary[])null!);
    }

    /// <summary>
    /// The <c>(PriorComponentSummary[])</c> constructor stores the components array
    /// but does not derive <c>TotalPriorLogLikelihood</c> / <c>TotalDataLogLikelihood</c>
    /// from the component means; those fields stay at their defaults (0).
    /// </summary>
    /// <remarks>
    /// The canonical production path is
    /// <see cref="BayesianAnalysis.ComputePriorInfluenceDiagnostics"/> →
    /// <c>new PriorInfluenceDiagnostics(Model, Results, thinEvery)</c>, which invokes
    /// <c>ComputeFromPosterior</c> to populate Total* from MCMC posterior samples.
    /// The <c>(components)</c> constructor is a partial constructor intended for
    /// exercising the component-array APIs (<see cref="PriorInfluenceDiagnostics.GetComponentsByType"/>,
    /// <see cref="PriorInfluenceDiagnostics.GetMostConstrainingComponents"/>, etc.)
    /// in isolation. Tests requiring specific ratio / influence scenarios should use
    /// <see cref="CreateDiagnosticsForTesting"/>.
    /// </remarks>
    [TestMethod]
    public void Constructor_Components_StoresArray_TotalsRemainDefault()
    {
        var comps = new[]
        {
            MakeSummary("param_prior", PriorComponentType.ParameterPrior, -2.0),
            MakeSummary("jeffreys", PriorComponentType.JeffreysScalePrior, -0.5)
        };

        var diag = new PriorInfluenceDiagnostics(comps);

        Assert.AreEqual(2, diag.Components.Length);
        Assert.AreEqual(2, diag.Count);
        Assert.AreEqual("param_prior", diag.Components[0].Name);
        Assert.AreEqual("jeffreys", diag.Components[1].Name);
        // Total* are not derived from component means by this constructor — they stay at 0.
        // With both Total* at 0, the computed ratio is 0 and the prior is not flagged as influential.
        Assert.AreEqual(0.0, diag.TotalPriorLogLikelihood, 1e-10);
        Assert.AreEqual(0.0, diag.TotalDataLogLikelihood, 1e-10);
        Assert.AreEqual(0.0, diag.PriorToDataRatio, 1e-10);
        Assert.IsFalse(diag.IsPriorInfluential);
    }

    /// <summary>
    /// Components constructor with empty array produces Count=0 and ratio=0.
    /// </summary>
    [TestMethod]
    public void Constructor_Components_Empty_RatioIsZero()
    {
        var diag = new PriorInfluenceDiagnostics(Array.Empty<PriorComponentSummary>());

        Assert.AreEqual(0, diag.Count);
        Assert.AreEqual(0.0, diag.PriorToDataRatio, 1e-10);
    }

    #endregion

    #region PriorToDataRatio / IsPriorInfluential Tests

    /// <summary>
    /// <see cref="PriorInfluenceDiagnostics.IsPriorInfluential"/> returns false when
    /// the data dominates the prior (<c>PriorToDataRatio &lt; 0.20</c>).
    /// </summary>
    /// <remarks>
    /// Constructs the diagnostic via XML so specific Total* values can be injected —
    /// the <c>(components)</c> constructor does not populate these fields
    /// (see <see cref="Constructor_Components_StoresArray_TotalsRemainDefault"/>).
    /// With prior LL = −0.1 and data LL = −10.0, the ratio is 0.1 / 10.1 ≈ 0.0099,
    /// well below the 0.20 influence threshold.
    /// </remarks>
    [TestMethod]
    public void IsPriorInfluential_LowRatio_ReturnsFalse()
    {
        var diag = CreateDiagnosticsForTesting(totalPriorLL: -0.1, totalDataLL: -10.0);

        Assert.AreEqual(0.1 / 10.1, diag.PriorToDataRatio, 1e-10);
        Assert.IsFalse(diag.IsPriorInfluential,
            "Data dominates prior (ratio ≈ 0.0099); IsPriorInfluential should be false.");
    }

    /// <summary>
    /// <see cref="PriorInfluenceDiagnostics.IsPriorInfluential"/> returns true when
    /// the prior dominates the data (<c>PriorToDataRatio &gt; 0.20</c>).
    /// </summary>
    /// <remarks>
    /// With prior LL = −10.0 and data LL = −1.0, the ratio is 10 / 11 ≈ 0.909,
    /// which exceeds the 0.20 influence threshold.
    /// </remarks>
    [TestMethod]
    public void IsPriorInfluential_HighRatio_ReturnsTrue()
    {
        var diag = CreateDiagnosticsForTesting(totalPriorLL: -10.0, totalDataLL: -1.0);

        Assert.AreEqual(10.0 / 11.0, diag.PriorToDataRatio, 1e-10);
        Assert.IsTrue(diag.IsPriorInfluential,
            "Prior dominates data (ratio ≈ 0.909); IsPriorInfluential should be true.");
    }

    /// <summary>
    /// <see cref="PriorInfluenceDiagnostics.IsPriorInfluential"/> sits right at the
    /// 0.20 boundary — only strictly greater than 0.20 is considered influential.
    /// </summary>
    /// <remarks>
    /// With prior LL = −1 and data LL = −4, the ratio is 1 / 5 = 0.20 exactly,
    /// which must NOT trigger <c>IsPriorInfluential</c> (the threshold is strictly greater).
    /// </remarks>
    [TestMethod]
    public void IsPriorInfluential_RatioEqualsThreshold_ReturnsFalse()
    {
        var diag = CreateDiagnosticsForTesting(totalPriorLL: -1.0, totalDataLL: -4.0);

        Assert.AreEqual(0.20, diag.PriorToDataRatio, 1e-10);
        Assert.IsFalse(diag.IsPriorInfluential,
            "Ratio at exactly 0.20 is not influential (threshold is strictly > 0.20).");
    }

    #endregion

    #region GetComponentsByType Tests

    /// <summary>
    /// GetComponentsByType returns only components of the specified type.
    /// </summary>
    [TestMethod]
    public void GetComponentsByType_FiltersCorrectly()
    {
        var comps = new[]
        {
            MakeSummary("param1", PriorComponentType.ParameterPrior, -1.0),
            MakeSummary("jeffreys", PriorComponentType.JeffreysScalePrior, -0.5),
            MakeSummary("param2", PriorComponentType.ParameterPrior, -0.8)
        };
        var diag = new PriorInfluenceDiagnostics(comps);

        var paramComponents = diag.GetComponentsByType(PriorComponentType.ParameterPrior);
        var jeffreysComponents = diag.GetComponentsByType(PriorComponentType.JeffreysScalePrior);
        var quantileComponents = diag.GetComponentsByType(PriorComponentType.QuantilePrior);

        Assert.AreEqual(2, paramComponents.Length);
        Assert.AreEqual(1, jeffreysComponents.Length);
        Assert.AreEqual(0, quantileComponents.Length);
    }

    /// <summary>
    /// GetComponentsByType on empty diagnostics returns empty array (does not throw).
    /// </summary>
    [TestMethod]
    public void GetComponentsByType_EmptyDiagnostics_ReturnsEmpty()
    {
        var diag = new PriorInfluenceDiagnostics();
        var result = diag.GetComponentsByType(PriorComponentType.ParameterPrior);

        Assert.AreEqual(0, result.Length);
    }

    #endregion

    #region GetMostConstrainingComponents Tests

    /// <summary>
    /// GetMostConstrainingComponents returns components sorted by mean LL ascending.
    /// </summary>
    [TestMethod]
    public void GetMostConstrainingComponents_SortedAscending()
    {
        var comps = new[]
        {
            MakeSummary("mild", PriorComponentType.ParameterPrior, -0.5),
            MakeSummary("strong", PriorComponentType.ParameterPrior, -5.0),
            MakeSummary("medium", PriorComponentType.ParameterPrior, -2.0)
        };
        var diag = new PriorInfluenceDiagnostics(comps);

        var sorted = diag.GetMostConstrainingComponents();

        Assert.AreEqual("strong", sorted[0].Name);
        Assert.AreEqual("medium", sorted[1].Name);
        Assert.AreEqual("mild", sorted[2].Name);
    }

    /// <summary>
    /// GetMostConstrainingComponents with topN limits the result.
    /// </summary>
    [TestMethod]
    public void GetMostConstrainingComponents_TopN_LimitsResult()
    {
        var comps = new[]
        {
            MakeSummary("a", PriorComponentType.ParameterPrior, -1.0),
            MakeSummary("b", PriorComponentType.ParameterPrior, -2.0),
            MakeSummary("c", PriorComponentType.ParameterPrior, -3.0)
        };
        var diag = new PriorInfluenceDiagnostics(comps);

        var top2 = diag.GetMostConstrainingComponents(2);

        Assert.AreEqual(2, top2.Length);
    }

    /// <summary>
    /// GetMostConstrainingComponents on empty returns empty.
    /// </summary>
    [TestMethod]
    public void GetMostConstrainingComponents_EmptyDiagnostics_ReturnsEmpty()
    {
        var diag = new PriorInfluenceDiagnostics();
        var result = diag.GetMostConstrainingComponents();

        Assert.AreEqual(0, result.Length);
    }

    #endregion

    #region GetContributionByType Tests

    /// <summary>
    /// GetContributionByType aggregates mean LL by prior type.
    /// </summary>
    [TestMethod]
    public void GetContributionByType_AggregatesCorrectly()
    {
        var comps = new[]
        {
            MakeSummary("p1", PriorComponentType.ParameterPrior, -1.0),
            MakeSummary("p2", PriorComponentType.ParameterPrior, -2.0),
            MakeSummary("j", PriorComponentType.JeffreysScalePrior, -0.5)
        };
        var diag = new PriorInfluenceDiagnostics(comps);

        var contributions = diag.GetContributionByType();

        Assert.IsTrue(contributions.ContainsKey(PriorComponentType.ParameterPrior));
        Assert.IsTrue(contributions.ContainsKey(PriorComponentType.JeffreysScalePrior));
        Assert.AreEqual(-3.0, contributions[PriorComponentType.ParameterPrior], 1e-10);
        Assert.AreEqual(-0.5, contributions[PriorComponentType.JeffreysScalePrior], 1e-10);
    }

    /// <summary>
    /// GetContributionByType on empty returns empty dictionary.
    /// </summary>
    [TestMethod]
    public void GetContributionByType_EmptyDiagnostics_ReturnsEmptyDictionary()
    {
        var diag = new PriorInfluenceDiagnostics();
        var result = diag.GetContributionByType();

        Assert.AreEqual(0, result.Count);
    }

    #endregion

    #region GetSummary Tests

    /// <summary>
    /// GetSummary with no components returns appropriate message.
    /// </summary>
    [TestMethod]
    public void GetSummary_NoComponents_ReturnsNoDataMessage()
    {
        var diag = new PriorInfluenceDiagnostics();
        var summary = diag.GetSummary();

        Assert.IsTrue(summary.Contains("No prior components"),
            $"Expected no-data message, got: {summary}");
    }

    /// <summary>
    /// GetSummary with components contains "Prior Influence Summary" header.
    /// </summary>
    [TestMethod]
    public void GetSummary_WithComponents_ContainsSummaryHeader()
    {
        var comps = new[] { MakeSummary("p", PriorComponentType.ParameterPrior, -2.0) };
        var diag = new PriorInfluenceDiagnostics(comps);

        var summary = diag.GetSummary();

        Assert.IsTrue(summary.Contains("Prior Influence Summary"),
            $"Expected header in summary: {summary}");
    }

    #endregion

    #region Indexer Tests

    /// <summary>
    /// Indexer returns the correct component at each position.
    /// </summary>
    [TestMethod]
    public void Indexer_ReturnsCorrectComponent()
    {
        var comps = new[]
        {
            MakeSummary("first", PriorComponentType.ParameterPrior, -1.0),
            MakeSummary("second", PriorComponentType.QuantilePrior, -3.0)
        };
        var diag = new PriorInfluenceDiagnostics(comps);

        Assert.AreEqual("first", diag[0].Name);
        Assert.AreEqual("second", diag[1].Name);
    }

    #endregion

    #region XML Round-Trip Tests

    /// <summary>
    /// ToXElement/FromXElement round-trip preserves component count and the computed
    /// summary statistics (non-zero Total* and derived ratio).
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CreateDiagnosticsForTesting"/> so the "original" has meaningful
    /// (non-zero) Total* values; otherwise the round-trip assertions would trivially
    /// compare 0 == 0 and miss serializer defects.
    /// </remarks>
    [TestMethod]
    public void XmlRoundTrip_PreservesComponentsAndStatistics()
    {
        var comps = new[]
        {
            MakeSummary("param_prior", PriorComponentType.ParameterPrior, -2.5, 0.3, -4.0, -0.1),
            MakeSummary("jeffreys", PriorComponentType.JeffreysScalePrior, -0.7, 0.1, -1.5, -0.2)
        };
        var original = CreateDiagnosticsForTesting(
            totalPriorLL: -3.2, totalDataLL: -42.5, components: comps);

        var xElement = original.ToXElement();
        var restored = new PriorInfluenceDiagnostics(xElement);

        Assert.AreEqual(original.Count, restored.Count);
        Assert.AreEqual(-3.2, restored.TotalPriorLogLikelihood, 1e-10);
        Assert.AreEqual(-42.5, restored.TotalDataLogLikelihood, 1e-10);
        Assert.AreEqual(3.2 / 45.7, restored.PriorToDataRatio, 1e-10);
    }

    /// <summary>
    /// XML round-trip preserves component names and types.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesComponentNamesAndTypes()
    {
        var comps = new[]
        {
            MakeSummary("quantile_100yr", PriorComponentType.QuantilePrior, -1.5)
        };
        var original = new PriorInfluenceDiagnostics(comps);

        var restored = new PriorInfluenceDiagnostics(original.ToXElement());

        Assert.AreEqual("quantile_100yr", restored.Components[0].Name);
        Assert.AreEqual(PriorComponentType.QuantilePrior, restored.Components[0].Type);
    }

    /// <summary>
    /// XML constructor with null throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_Xml_Null_ThrowsArgumentNullException()
    {
        _ = new PriorInfluenceDiagnostics((XElement)null!);
    }

    #endregion

    #region PriorComponentSummary Struct Tests

    /// <summary>
    /// PriorComponentSummary constructor sets all properties correctly.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_Constructor_SetsAllProperties()
    {
        var summary = new PriorComponentSummary(
            "mu_prior", PriorComponentType.ParameterPrior, -3.5, 0.4, -6.0, -1.0);

        Assert.AreEqual("mu_prior", summary.Name);
        Assert.AreEqual(PriorComponentType.ParameterPrior, summary.Type);
        Assert.AreEqual(-3.5, summary.MeanLogLikelihood, 1e-10);
        Assert.AreEqual(0.4, summary.StandardDeviation, 1e-10);
        Assert.AreEqual(-6.0, summary.MinLogLikelihood, 1e-10);
        Assert.AreEqual(-1.0, summary.MaxLogLikelihood, 1e-10);
    }

    /// <summary>
    /// PriorComponentSummary CoefficientOfVariation computes |sd/mean|.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_CoefficientOfVariation_ComputedCorrectly()
    {
        var summary = new PriorComponentSummary("p", PriorComponentType.ParameterPrior, -4.0, 2.0, -8.0, 0.0);

        // CV = |2.0 / -4.0| = 0.5
        Assert.AreEqual(0.5, summary.CoefficientOfVariation, 1e-10);
    }

    /// <summary>
    /// PriorComponentSummary CoefficientOfVariation returns 0 when mean is near zero.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_CoefficientOfVariation_ZeroMean_ReturnsZero()
    {
        var summary = new PriorComponentSummary("p", PriorComponentType.ParameterPrior, 0.0, 1.0, -1.0, 1.0);

        Assert.AreEqual(0.0, summary.CoefficientOfVariation, 1e-10);
    }

    /// <summary>
    /// PriorComponentSummary XML round-trip preserves all fields.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_XmlRoundTrip_PreservesAllFields()
    {
        var original = new PriorComponentSummary(
            "sigma_jeffreys", PriorComponentType.JeffreysScalePrior, -1.2, 0.3, -3.0, -0.2);

        var xElement = original.ToXElement();
        var restored = new PriorComponentSummary(xElement);

        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Type, restored.Type);
        Assert.AreEqual(original.MeanLogLikelihood, restored.MeanLogLikelihood, 1e-10);
        Assert.AreEqual(original.StandardDeviation, restored.StandardDeviation, 1e-10);
        Assert.AreEqual(original.MinLogLikelihood, restored.MinLogLikelihood, 1e-10);
        Assert.AreEqual(original.MaxLogLikelihood, restored.MaxLogLikelihood, 1e-10);
    }

    /// <summary>
    /// PriorComponentSummary ToString returns non-empty string.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_ToString_ReturnsNonEmptyString()
    {
        var summary = new PriorComponentSummary(
            "p", PriorComponentType.ParameterPrior, -2.0, 0.5, -4.0, 0.0);

        var str = summary.ToString();

        Assert.IsFalse(string.IsNullOrEmpty(str));
        Assert.IsTrue(str.Contains("p"), "ToString should include the name.");
    }

    /// <summary>
    /// PriorComponentSummary XML round-trip with missing Type attribute defaults to ParameterPrior.
    /// </summary>
    [TestMethod]
    public void PriorComponentSummary_XmlRoundTrip_MissingType_DefaultsToParameterPrior()
    {
        // Manually create XElement without Type attribute
        var xElement = new XElement("PriorComponentSummary",
            new XAttribute("Name", "test"),
            new XAttribute("MeanLogLikelihood", "-1.0"),
            new XAttribute("StandardDeviation", "0.1"),
            new XAttribute("MinLogLikelihood", "-2.0"),
            new XAttribute("MaxLogLikelihood", "0.0"));

        var restored = new PriorComponentSummary(xElement);

        Assert.AreEqual(PriorComponentType.ParameterPrior, restored.Type);
    }

    #endregion
}
