using System.Diagnostics;
using Numerics.Distributions;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verification tests for the fit/variance influence decomposition in <see cref="LeverageDiagnostics"/>.
/// Uses LnNormal(mu=3, sigma=0.5) with N=30 samples. Parameters set directly (no optimization)
/// so tests run in milliseconds.
/// </summary>
[TestClass]
public class FitVarianceInfluenceTests
{
    private const double Mu = 3.0;
    private const double Sigma = 0.5;
    private const int N = 30;

    /// <summary>
    /// Generates N samples from LnNormal(mu, sigma) with a fixed seed.
    /// </summary>
    private static double[] GenerateSamples()
    {
        var rng = new Random(12345);
        var dist = new LogNormal(Mu, Sigma);
        var samples = new double[N];
        for (int i = 0; i < N; i++)
            samples[i] = dist.InverseCDF(rng.NextDouble());
        return samples;
    }

    /// <summary>
    /// Creates a LnNormal model with the given DataFrame and sets parameters to known values.
    /// </summary>
    private static UnivariateDistribution CreateModel(DataFrame df, double mu = Mu, double sigma = Sigma)
    {
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogNormal);
        model.SetParameterValues(new[] { mu, sigma });
        return model;
    }

    /// <summary>
    /// Prints diagnostic table for inspection.
    /// </summary>
    private static void Print(LeverageDiagnostics d)
    {
        Debug.WriteLine($"\n{"Component",-35} | {"Type",-14} | {"FitInfl",8} | {"VarInfl",8} | {"Lever",8} | {"%Tot",6}");
        Debug.WriteLine(new string('-', 100));
        foreach (var o in d.Observations.OrderByDescending(x => x.Leverage).Take(10))
        {
            string n = (o.Name ?? $"[{o.Index}]") + $"={o.Value:G4}";
            Debug.WriteLine($"{n,-35} | {o.DataType + (o.Count > 1 ? $"(n={o.Count})" : ""),-14} | {o.FitInfluence,8:G3} | {o.VarianceInfluence,8:G3} | {o.Leverage,8:G3} | {o.PercentOfTotal,5:F1}%");
        }
        foreach (var p in d.PriorComponents.OrderByDescending(x => x.Leverage))
            Debug.WriteLine($"{p.Name,-35} | {p.Type,-14} | {p.FitInfluence,8:G3} | {p.VarianceInfluence,8:G3} | {p.Leverage,8:G3} | {p.PercentOfTotal,5:F1}%");
        Debug.WriteLine($"\n{d.GetSummary()}");
        Debug.WriteLine($"TotalLev={d.TotalLeverage:G4} TotalFit={d.TotalFitInfluence:G4} TotalVar={d.TotalVarianceInfluence:G4}");
    }

    /// <summary>
    /// Verifies <c>Test_BaselineExact_NonNegativeInfluences</c>.
    /// </summary>
    [TestMethod]
    public void Test_BaselineExact_NonNegativeInfluences()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(GenerateSamples().Select((v, i) => new ExactData(i, v)).ToList());
        var model = CreateModel(df);
        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);
        Print(diag);

        foreach (var o in diag.Observations)
        {
            Assert.IsTrue(o.FitInfluence >= -1e-6, $"Obs[{o.Index}] FitInfluence={o.FitInfluence}");
            Assert.IsTrue(o.VarianceInfluence >= -1e-6, $"Obs[{o.Index}] VarianceInfluence={o.VarianceInfluence}");
            Assert.IsTrue(o.Leverage > 0, $"Obs[{o.Index}] Leverage={o.Leverage}");
        }

        Assert.IsTrue(diag.TotalLeverage > 0);
        Assert.IsTrue(diag.TotalFitInfluence >= 0);
        Assert.IsTrue(diag.TotalVarianceInfluence > 0);
    }

    /// <summary>
    /// Verifies <c>Test_Outlier_HighestFitInfluence</c>.
    /// </summary>
    [TestMethod]
    public void Test_Outlier_HighestFitInfluence()
    {
        var samples = GenerateSamples();
        var all = samples.Concat(new[] { 1e5 }).ToArray(); // extreme outlier
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(all.Select((v, i) => new ExactData(i, v)).ToList());
        var model = CreateModel(df);
        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);
        Print(diag);

        var outlier = diag.Observations[N]; // last obs
        double maxOtherFit = diag.Observations.Take(N).Max(o => o.FitInfluence);

        Assert.IsTrue(outlier.FitInfluence > maxOtherFit,
            $"Outlier FitInfluence ({outlier.FitInfluence:G4}) must exceed max other ({maxOtherFit:G4}).");
    }

    /// <summary>
    /// Verifies <c>Test_Threshold_HighLeverage</c>.
    /// </summary>
    [TestMethod]
    public void Test_Threshold_HighLeverage()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(GenerateSamples().Select((v, i) => new ExactData(i, v)).ToList());
        // Threshold: 50 years of data below 500
        df.ThresholdSeries.Add(new ThresholdData(1800, 1850, 500.0));
        df.ProcessThresholdSeries();
        var model = CreateModel(df);
        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);
        Print(diag);

        var thresh = diag.Observations.Last();
        double maxExactLeverage = diag.Observations.Take(N).Max(o => o.Leverage);

        Assert.IsTrue(thresh.Leverage > maxExactLeverage,
            $"Threshold Leverage ({thresh.Leverage:G4}) must exceed max exact ({maxExactLeverage:G4}).");
    }

    /// <summary>
    /// Verifies <c>Test_PriorNearMode_LowFitHighVariance</c>.
    /// </summary>
    [TestMethod]
    public void Test_PriorNearMode_LowFitHighVariance()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(GenerateSamples().Select((v, i) => new ExactData(i, v)).ToList());
        var model = CreateModel(df);

        // Strong prior on mu centered at the true value (no fit shift expected)
        model.Parameters[0].PriorDistribution = new Normal(Mu, 0.05);

        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);
        Print(diag);

        var muPrior = diag.PriorComponents.FirstOrDefault(p =>
            p.Name.Contains("μ") || p.Name.Contains("Mu") || p.Name.Contains("Location") || p.Type == PriorComponentType.ParameterPrior);

        if (muPrior.Name != null)
        {
            Assert.IsTrue(muPrior.VarianceInfluence > 0, "Prior near mode should have positive VarianceInfluence.");
            Assert.IsTrue(muPrior.FitInfluence < muPrior.VarianceInfluence * 0.5,
                $"Prior near mode: FitInfluence ({muPrior.FitInfluence:G4}) should be < 50% of VarianceInfluence ({muPrior.VarianceInfluence:G4}).");
        }
    }

    /// <summary>
    /// Verifies <c>Test_PriorFarFromMode_HighFit</c>.
    /// </summary>
    [TestMethod]
    public void Test_PriorFarFromMode_HighFit()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(GenerateSamples().Select((v, i) => new ExactData(i, v)).ToList());
        var model = CreateModel(df);
        // Strong prior on mu centered 2 sigma away
        model.Parameters[0].PriorDistribution = new Normal(Mu + 2 * Sigma, 0.05);

        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);
        Print(diag);

        var muPrior = diag.PriorComponents.FirstOrDefault(p =>
            p.Name.Contains("μ") || p.Name.Contains("Mu") || p.Name.Contains("Location") || p.Type == PriorComponentType.ParameterPrior);

        if (muPrior.Name != null)
        {
            Assert.IsTrue(muPrior.FitInfluence > 0.01,
                $"Far prior FitInfluence ({muPrior.FitInfluence:G4}) should be substantial.");
            Assert.IsTrue(muPrior.VarianceInfluence > 0,
                $"Far prior VarianceInfluence ({muPrior.VarianceInfluence:G4}) should be positive.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_LeverageSumsPositive</c>.
    /// </summary>
    [TestMethod]
    public void Test_LeverageSumsPositive()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(GenerateSamples().Select((v, i) => new ExactData(i, v)).ToList());
        var model = CreateModel(df);
        var map = new MaximumAPosteriori(model);
        map.Estimate(); // ensure MAP values are computed for diagnostics

        var diag = new LeverageDiagnostics(model, map.BestParameterSet.Values);

        Assert.IsTrue(diag.TotalLeverage > 0, "Total leverage should be positive.");
        Assert.IsTrue(diag.TotalVarianceInfluence > 0, "Total variance influence should be positive.");
    }

}
