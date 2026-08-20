using Numerics.Data;
using RMC.BestFit.Models;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Verifies the conditional ARIMA likelihood against a hand-calculated recurrence.
/// </summary>
/// <remarks>
/// This fast regression uses an inline deterministic fixture and does not invoke an optimizer or
/// sampler. It fixes the conditioning, moving-average sign, logarithmic Jacobian, and likelihood
/// indexing used by the Phase 5 recovery oracle.
/// </remarks>
[TestClass]
public class ARIMAConditionalLikelihoodTests
{
    /// <summary>
    /// Verifies ARIMA(1,1,1) residuals and the transformed data likelihood term by term.
    /// </summary>
    [TestMethod]
    public void ResidualsAndDataLikelihoodMatchHandArma11Recurrence()
    {
        double[] differences = [0.10, -0.05, 0.20, 0.03, -0.08];
        var transformedLevels = new double[differences.Length + 1];
        transformedLevels[0] = Math.Log(10.0);
        for (int index = 0; index < differences.Length; index++)
            transformedLevels[index + 1] = transformedLevels[index] + differences[index];

        double[] rawLevels = transformedLevels.Select(Math.Exp).ToArray();
        var series = new NumericTimeSeries(
            TimeInterval.OneDay,
            new DateTime(2000, 1, 1),
            rawLevels);
        var model = new ARIMA(series, pOrder: 1, dOrder: 1, qOrder: 1, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic,
        };
        model.TrainingTimeSteps = series.Count;
        model.SetDefaultParameters();

        const double phi = 0.4;
        const double theta = -0.2;
        const double sigma = 0.3;
        double[] parameters = [phi, theta, sigma];
        var expectedResiduals = new double[differences.Length];
        for (int index = 1; index < differences.Length; index++)
        {
            expectedResiduals[index] = differences[index] -
                phi * differences[index - 1] -
                theta * expectedResiduals[index - 1];
        }

        double[] actualResiduals = model.Residuals(parameters);
        Assert.AreEqual(expectedResiduals.Length, actualResiduals.Length);
        for (int index = 0; index < expectedResiduals.Length; index++)
        {
            Assert.AreEqual(
                expectedResiduals[index],
                actualResiduals[index],
                1E-12,
                $"Conditional residual {index}.");
        }

        double expectedLogLikelihood = 0.0;
        for (int index = 1; index < expectedResiduals.Length; index++)
        {
            double standardized = expectedResiduals[index] / sigma;
            expectedLogLikelihood += -Math.Log(sigma) -
                0.5 * Math.Log(2.0 * Math.PI) -
                0.5 * standardized * standardized;
        }

        // For d=1 and max(p,q)=1, the log-transform Jacobian covers raw indices 2...5.
        for (int rawIndex = 2; rawIndex < rawLevels.Length; rawIndex++)
            expectedLogLikelihood -= Math.Log(rawLevels[rawIndex]);

        Assert.AreEqual(
            expectedLogLikelihood,
            model.DataLogLikelihood(parameters),
            1E-12,
            "Conditional Gaussian likelihood plus logarithmic Jacobian.");
    }
}
