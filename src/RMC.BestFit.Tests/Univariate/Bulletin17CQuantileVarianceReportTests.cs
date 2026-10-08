using System.Globalization;
using System.Reflection;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Checks quantile-variance reporting from supplied fitted states without running an estimator.
/// </summary>
/// <remarks>
/// Known-point derivatives and inline covariance matrices exercise report arithmetic and units;
/// these tests do not establish covariance accuracy or interval coverage for fitted data.
/// </remarks>
[TestClass]
public class Bulletin17CQuantileVarianceReportTests
{
    private const string VarianceHeading = "ASYMPTOTIC QUANTILE VARIANCE (DELTA METHOD)";
    private const double NormalOneSdAep = 0.15865525393145707;

    /// <summary>
    /// Every supported family reports the fitted quantile and the full covariance quadratic form.
    /// </summary>
    /// <param name="family">The fitted distribution family.</param>
    [DataTestMethod]
    [DataRow(UnivariateDistributionType.Exponential)]
    [DataRow(UnivariateDistributionType.GammaDistribution)]
    [DataRow(UnivariateDistributionType.Normal)]
    [DataRow(UnivariateDistributionType.LogNormal)]
    [DataRow(UnivariateDistributionType.PearsonTypeIII)]
    [DataRow(UnivariateDistributionType.LogPearsonTypeIII)]
    public void GenerateGMMReport_AllFamilies_ReportsKnownQuantileVariance(UnivariateDistributionType family)
    {
        bool logarithmic = family is UnivariateDistributionType.LogNormal or UnivariateDistributionType.LogPearsonTypeIII;
        bool pearson = family is UnivariateDistributionType.PearsonTypeIII or UnivariateDistributionType.LogPearsonTypeIII;
        double[] theta = pearson ? [logarithmic ? 1d : 10d, logarithmic ? 0.2 : 2d, 0d]
            : [logarithmic ? 1d : 10d, logarithmic ? 0.2 : 2d];
        double aep = NormalOneSdAep;
        double quantile = logarithmic ? 1.2 : 12d;
        double variance = 0.15; // [1, 1] * [[.09, .01], [.01, .04]] * [1, 1]'.
        if (family == UnivariateDistributionType.Exponential)
        {
            theta = [3d, 2d];
            aep = Math.Exp(-1d);
            quantile = 5d;
        }
        else if (family == UnivariateDistributionType.GammaDistribution)
        {
            theta = [2d, 1d]; // Scale 2, shape 1, unit-scale quantile 1.
            aep = Math.Exp(-1d);
            quantile = 2d;
            // Implicit Gamma derivative at shape=x=1: dx/da = e*E1(1) + EulerGamma.
            double shapeDerivative = 2d * (Math.E * 0.21938393439552027368 + 0.57721566490153286061);
            variance = 0.09 + 0.02 * shapeDerivative + 0.04 * shapeDerivative * shapeDerivative;
        }

        var analysis = CreateEstimatedAnalysis(family, theta, CreateCovariance(theta.Length), [aep]);
        var storedDistribution = analysis.Bulletin17CDistribution.Distribution;
        double[] originalDistributionParameters = storedDistribution.GetParameters;
        double[] originalEstimate = (double[])analysis.GMM!.BestParameterSet.Values.Clone();
        double[,] originalCovariance = analysis.GMM.Sigma!.ToArray();

        string report = analysis.GenerateGMMReport();
        var rows = ReadVarianceRows(report);

        Assert.AreEqual(1, rows.Count);
        AssertReportedNumber(quantile, rows[0].Quantile);
        AssertReportedNumber(variance, rows[0].Variance);
        Assert.AreEqual(aep, rows[0].Aep, 0.00005);
        Assert.AreEqual(variance, analysis.Bulletin17CDistribution.QuantileVariance(
            1d - aep, theta, originalCovariance), 1e-8);
        StringAssert.Contains(report, logarithmic ? "squared log10 units" : "squared native units");
        StringAssert.Contains(report, "Asymptotic GMM delta-method diagnostic");
        Assert.IsFalse(report.Contains("COHN LP3", StringComparison.Ordinal));
        CollectionAssert.AreEqual(originalDistributionParameters, storedDistribution.GetParameters);
        CollectionAssert.AreEqual(originalEstimate, analysis.GMM.BestParameterSet.Values);
        CollectionAssert.AreEqual(originalCovariance, analysis.GMM.Sigma.ToArray());
        Assert.AreEqual(0, analysis.GMM.Optimizer.FunctionEvaluations);
    }

    /// <summary>
    /// Pearson-family median variance includes skew and cross-coordinate covariance in moment space.
    /// </summary>
    /// <param name="family">The natural or logarithmic Pearson family.</param>
    [DataTestMethod]
    [DataRow(UnivariateDistributionType.PearsonTypeIII)]
    [DataRow(UnivariateDistributionType.LogPearsonTypeIII)]
    public void GenerateGMMReport_PearsonMedian_UsesMomentCoordinateGradient(UnivariateDistributionType family)
    {
        double[] theta = [3d, 0.6, 0d];
        var analysis = CreateEstimatedAnalysis(family, theta, CreateCovariance(3), [0.5]);
        // At zero skew and p=.5, the gradient is [1, 0, -sigma/6] = [1, 0, -.1].
        double expected = 0.09 - 2d * 0.1 * 0.003 + 0.01 * 0.01;
        var row = ReadVarianceRows(analysis.GenerateGMMReport()).Single();
        AssertReportedNumber(3d, row.Quantile);
        AssertReportedNumber(expected, row.Variance);
    }

    /// <summary>
    /// Observation types that exclude Cohn intervals do not suppress an available GMM variance.
    /// </summary>
    /// <param name="observationKind">The additional observation information.</param>
    [DataTestMethod]
    [DataRow("low outlier")]
    [DataRow("uncertain")]
    [DataRow("interval")]
    [DataRow("threshold")]
    public void GenerateGMMReport_NonExactLp3_ReportsVarianceWhileCohnRemainsRestricted(string observationKind)
    {
        var analysis = CreateEstimatedAnalysis(UnivariateDistributionType.LogPearsonTypeIII,
            [1d, 0.2, 0d], CreateCovariance(3), [NormalOneSdAep], observationKind);

        var row = ReadVarianceRows(analysis.GenerateGMMReport()).Single();

        AssertReportedNumber(1.2, row.Quantile);
        AssertReportedNumber(0.15, row.Variance);
        Assert.ThrowsException<NotSupportedException>(() => analysis.ComputeCohnStyleConfidenceIntervals());
    }

    /// <summary>
    /// Failed stored covariance leaves point estimates readable and explains missing uncertainty.
    /// </summary>
    [TestMethod]
    public void GenerateGMMReport_UnusableCovariance_ReportsUnavailableWithoutThrowing()
    {
        var analysis = CreateEstimatedAnalysis(UnivariateDistributionType.Normal,
            [10d, 2d], new Matrix(2, 2), [0.5]);

        string report = analysis.GenerateGMMReport();

        StringAssert.Contains(report, "PARAMETER ESTIMATES (GMM)");
        StringAssert.Contains(report, "Standard errors unavailable:");
        StringAssert.Contains(report, VarianceHeading);
        StringAssert.Contains(report, "Not available:");
        StringAssert.Contains(report, "non-positive diagonal variance");
        StringAssert.Contains(report, "N/A");
        Assert.AreEqual(0, ReadVarianceRows(report).Count);
        Assert.IsTrue(analysis.IsEstimated);
    }

    /// <summary>
    /// A failed quantile or gradient affects only its row, without a parent-quantile fallback.
    /// </summary>
    /// <param name="failGradient">Whether to fail the gradient rather than the inverse CDF.</param>
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GenerateGMMReport_RowEvaluationThrows_ReportsNaAndContinues(bool failGradient)
    {
        // Exponential Q(0) is its finite location, but the variance API rejects an endpoint.
        // Normal Q(-1) throws before a quantile is available. Both precede a valid row.
        var analysis = failGradient
            ? CreateEstimatedAnalysis(UnivariateDistributionType.Exponential, [3d, 2d], CreateCovariance(2), [1d, Math.Exp(-1d)])
            : CreateEstimatedAnalysis(UnivariateDistributionType.Normal, [10d, 2d], CreateCovariance(2), [2d, 0.5]);

        var rows = ReadVarianceRows(analysis.GenerateGMMReport());

        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual("N/A", rows[0].Variance);
        if (!failGradient) Assert.AreEqual("N/A", rows[0].Quantile);
        else Assert.IsTrue(double.IsFinite(double.Parse(rows[0].Quantile, CultureInfo.CurrentCulture)));
        AssertReportedNumber(failGradient ? 5d : 10d, rows[1].Quantile);
        AssertReportedNumber(failGradient ? 0.15 : 0.09, rows[1].Variance);
    }

    /// <summary>
    /// A nonfinite or negative quadratic form is unavailable, not a manufactured zero variance.
    /// </summary>
    /// <param name="overflow">Whether the quadratic form overflows instead of being negative.</param>
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GenerateGMMReport_UnusableRowVariance_ReportsNa(bool overflow)
    {
        var covariance = new Matrix(overflow
            ? new double[,] { { 1e308, 0d }, { 0d, 1e308 } }
            : new double[,] { { 1d, -2d }, { -2d, 1d } });
        var analysis = CreateEstimatedAnalysis(UnivariateDistributionType.Normal,
            [10d, 2d], covariance, [NormalOneSdAep, 0.5]);

        var rows = ReadVarianceRows(analysis.GenerateGMMReport());

        AssertReportedNumber(12d, rows[0].Quantile);
        Assert.AreEqual("N/A", rows[0].Variance);
        AssertReportedNumber(overflow ? 1e308 : 1d, rows[1].Variance);
    }

    /// <summary>
    /// Missing probability ordinates produce an explicit diagnostic rather than hiding the section.
    /// </summary>
    [TestMethod]
    public void GenerateGMMReport_NoOrdinates_ReportsReason()
    {
        var analysis = CreateEstimatedAnalysis(UnivariateDistributionType.Normal,
            [10d, 2d], CreateCovariance(2), []);
        string report = analysis.GenerateGMMReport();
        StringAssert.Contains(report, VarianceHeading);
        StringAssert.Contains(report, "No probability ordinates");
    }

    /// <summary>
    /// Creates a small correlated covariance in the fitted parameter coordinates.
    /// </summary>
    /// <param name="dimension">The two- or three-parameter family dimension.</param>
    /// <returns>The inline covariance matrix.</returns>
    private static Matrix CreateCovariance(int dimension) => new(dimension == 2
        ? new double[,] { { 0.09, 0.01 }, { 0.01, 0.04 } }
        : new double[,] { { 0.09, 0.01, 0.003 }, { 0.01, 0.04, 0.002 }, { 0.003, 0.002, 0.01 } });

    /// <summary>
    /// Injects completed estimator state and an unused optimizer to exercise the public live report.
    /// </summary>
    /// <param name="family">The supported parent family.</param>
    /// <param name="theta">The fitted parameter vector, distinct from data-derived model defaults.</param>
    /// <param name="covariance">The supplied parameter covariance.</param>
    /// <param name="aeps">The requested annual exceedance probabilities.</param>
    /// <param name="observationKind">Optional additional observation information.</param>
    /// <returns>An estimated analysis that has never run an optimizer or sampler.</returns>
    private static Bulletin17CAnalysis CreateEstimatedAnalysis(UnivariateDistributionType family,
        double[] theta, Matrix covariance, double[] aeps, string? observationKind = null)
    {
        var frame = new BestFitDataFrame();
        for (int i = 0; i < 8; i++) frame.ExactSeries.Add(new ExactData(2000 + i, 10d + i * 3d));
        switch (observationKind)
        {
            case "low outlier": ((ExactData)frame.ExactSeries[0]).IsLowOutlier = true; break;
            case "uncertain": frame.UncertainSeries.Add(new UncertainData(1999, new Normal(20d, 2d))); break;
            case "interval": frame.IntervalSeries.Add(new IntervalData(1998, 15d, 20d, 25d)); break;
            case "threshold": frame.ThresholdSeries.Add(new ThresholdData(1980, 1989, 30d)); break;
        }
        var model = new Bulletin17CDistribution(frame, family);
        int p = theta.Length;
        double[] lower = theta.Select(t => t - 100d).ToArray();
        double[] upper = theta.Select(t => t + 100d).ToArray();
        var gmm = new GeneralizedMethodOfMoments(_ => (new Vector(new double[p]), Matrix.Identity(p)),
            p, p, frame.ExactSeries.Count, theta, lower, upper, Matrix.Identity(p));
        SetProperty(gmm, nameof(GeneralizedMethodOfMoments.IsEstimated), true);
        SetProperty(gmm, nameof(GeneralizedMethodOfMoments.BestParameterSet), new ParameterSet(theta, 0d));
        SetProperty(gmm, nameof(GeneralizedMethodOfMoments.Sigma), covariance);
        SetProperty(gmm, nameof(GeneralizedMethodOfMoments.Optimizer), new NelderMead(_ => 0d, p, theta, lower, upper));
        var analysis = new Bulletin17CAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        foreach (double aep in aeps) analysis.ProbabilityOrdinates.Add(aep);
        typeof(Bulletin17CAnalysis).GetField("_gmm", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(analysis, gmm);
        SetProperty(analysis, nameof(Bulletin17CAnalysis.IsEstimated), true);
        return analysis;
    }

    /// <summary>
    /// Sets an existing non-public setter solely to arrange an already-estimated state.
    /// </summary>
    /// <param name="target">The test fixture object.</param>
    /// <param name="name">The public property name.</param>
    /// <param name="value">The injected value.</param>
    private static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);

    /// <summary>
    /// Reads only numeric-AEP rows from the quantile-variance section of the complete report.
    /// </summary>
    /// <param name="report">The generated report.</param>
    /// <returns>The reported AEP, quantile text, and variance text for each row.</returns>
    private static List<(double Aep, string Quantile, string Variance)> ReadVarianceRows(string report)
    {
        StringAssert.Contains(report, VarianceHeading);
        var rows = new List<(double, string, string)>();
        string section = report[(report.IndexOf(VarianceHeading, StringComparison.Ordinal) + VarianceHeading.Length)..];
        foreach (string line in section.Split('\n'))
        {
            string[] cells = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length == 3 && double.TryParse(cells[0], NumberStyles.Float, CultureInfo.CurrentCulture, out double aep))
                rows.Add((aep, cells[1], cells[2]));
        }
        return rows;
    }

    /// <summary>
    /// Compares a reported G6 value with its independently calculated value at display precision.
    /// </summary>
    /// <param name="expected">The independent expected value.</param>
    /// <param name="reported">The report cell.</param>
    private static void AssertReportedNumber(double expected, string reported) =>
        Assert.AreEqual(expected, double.Parse(reported, CultureInfo.CurrentCulture), Math.Abs(expected) * 5e-6 + 1e-12);

}
