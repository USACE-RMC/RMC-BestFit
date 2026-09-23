using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Pins how <c>DataFrame.BootstrapDataFrame</c> re-centres uncertain-data measurement-error
/// distributions (TR-086): log-space fitted families and strictly positive error supports keep the
/// relative error (ratio shift, support stays positive); real-space fits with unbounded error
/// distributions keep the absolute error spread (additive shift).
/// </summary>
/// <remarks>
/// Until 22 August 2026 every additive family was shifted additively, so a wide MOVE.3-style
/// Triangular error shifted onto a small simulated flood crossed zero under a Log-Pearson Type III
/// fit and the log-space moment conditions became NaN. Seeded and deterministic; no estimator runs.
/// </remarks>
[TestClass]
public class DataFrameBootstrapShiftTests
{
    /// <summary>Twelve positive systematic flows.</summary>
    private static readonly double[] Flows = { 820, 950, 1010, 1100, 1240, 760, 990, 1320, 880, 1050, 930, 1180 };

    /// <summary>
    /// Builds a frame with the systematic flows and one uncertain observation.
    /// </summary>
    /// <param name="error">The measurement-error distribution of the uncertain observation.</param>
    /// <returns>The data frame.</returns>
    private static BestFitDataFrame CreateFrame(UnivariateDistributionBase error)
    {
        var frame = new BestFitDataFrame { ExactSeries = new ExactSeries(Flows) };
        frame.UncertainSeries.Add(new UncertainData(Flows.Length, error));
        return frame;
    }

    /// <summary>
    /// Under a Log-Pearson Type III sampling distribution a wide Triangular error is rescaled: the
    /// support stays positive and the relative spread (max/min, mode/min) is preserved in every replicate.
    /// </summary>
    [TestMethod]
    public void BootstrapDataFrame_LogSpaceFit_RescalesAdditiveErrorAndKeepsSupportPositive()
    {
        var frame = CreateFrame(new Triangular(500.0, 1000.0, 1750.0));
        var sampling = new LogPearsonTypeIII(2.9, 0.3, -0.2);
        var prng = new MersenneTwister(24681357);

        for (int replicate = 0; replicate < 200; replicate++)
        {
            var boot = frame.BootstrapDataFrame(sampling, prng);
            var error = (Triangular)((UncertainData)boot.UncertainSeries[0]).Distribution;

            Assert.IsTrue(error.Min > 0.0, $"Replicate {replicate}: the shifted support must stay positive (min {error.Min:G6}).");
            Assert.AreEqual(3.5, error.Max / error.Min, 1e-9, $"Replicate {replicate}: the relative spread must be preserved.");
            Assert.AreEqual(2.0, error.MostLikely / error.Min, 1e-9, $"Replicate {replicate}: the relative mode must be preserved.");
        }
    }

    /// <summary>
    /// Under a real-space Normal sampling distribution an unbounded Normal error keeps its absolute
    /// spread: the shifted error has the original sigma bitwise and is centred on the simulated value.
    /// </summary>
    [TestMethod]
    public void BootstrapDataFrame_RealSpaceFit_UnboundedError_KeepsAdditiveShift()
    {
        var frame = CreateFrame(new Normal(1000.0, 50.0));
        var sampling = new Normal(1000.0, 150.0);
        var prng = new MersenneTwister(24681357);

        for (int replicate = 0; replicate < 50; replicate++)
        {
            var boot = frame.BootstrapDataFrame(sampling, prng);
            var error = (Normal)((UncertainData)boot.UncertainSeries[0]).Distribution;

            Assert.AreEqual(50.0, error.Sigma, 0.0, $"Replicate {replicate}: the absolute spread must be preserved bitwise.");
            Assert.IsTrue(double.IsFinite(error.Mu), $"Replicate {replicate}: the shifted centre must be finite.");
        }
    }

    /// <summary>
    /// Under a real-space sampling distribution an error distribution with strictly positive support
    /// is rescaled (relative error preserved) rather than shifted.
    /// </summary>
    [TestMethod]
    public void BootstrapDataFrame_RealSpaceFit_PositiveSupportError_UsesRelativeShift()
    {
        var frame = CreateFrame(new Triangular(900.0, 1000.0, 1100.0));
        var sampling = new Normal(1000.0, 150.0);
        var prng = new MersenneTwister(24681357);

        for (int replicate = 0; replicate < 50; replicate++)
        {
            var boot = frame.BootstrapDataFrame(sampling, prng);
            var error = (Triangular)((UncertainData)boot.UncertainSeries[0]).Distribution;

            Assert.IsTrue(error.Min > 0.0, $"Replicate {replicate}: the support must stay positive.");
            Assert.AreEqual(1100.0 / 900.0, error.Max / error.Min, 1e-9, $"Replicate {replicate}: the relative spread must be preserved.");
        }
    }
}
