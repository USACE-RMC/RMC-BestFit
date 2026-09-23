using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests distribution-aware defaults assigned when a univariate parameter is made nonstationary.
/// </summary>
[TestClass]
public class UnivariateTrendDefaultTests
{
    private static readonly double[] InlineNormalData = new Normal(100d, 15d)
        .GenerateRandomValues(1000, 12345);

    /// <summary>
    /// Verifies that reciprocal coefficients are initialized in reciprocal space while preserving
    /// the stationary response at the first time index.
    /// </summary>
    [TestMethod]
    public void SetTrendModel_Reciprocal_PreservesStationaryStartResponse()
    {
        var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineNormalData) };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        double stationaryInitial = model.Parameters[0].Value;
        model.IsNonstationary = true;

        model.SetTrendModel(0, TrendModelType.Reciprocal);

        var trend = (ReciprocalTrend)model.TrendModels[0];
        Assert.AreEqual(stationaryInitial, trend.Predict(trend.StartIndex), 1e-10d);
        Assert.AreEqual(1d / stationaryInitial, trend.Parameters[0].Value, 1e-12d);
        Assert.IsTrue(double.IsFinite(trend.Parameters[0].LowerBound));
        Assert.IsTrue(double.IsFinite(trend.Parameters[0].UpperBound));
        Assert.IsTrue(trend.Parameters[0].LowerBound < trend.Parameters[0].Value);
        Assert.IsTrue(trend.Parameters[0].Value < trend.Parameters[0].UpperBound);
        Assert.IsTrue(trend.Parameters[1].LowerBound < 0.00001d);
        Assert.IsTrue(trend.Parameters[1].UpperBound > 0.00001d);
    }

    /// <summary>
    /// Verifies that the default sinusoidal amplitude keeps a positive scale parameter valid over
    /// the complete fitted time range.
    /// </summary>
    [TestMethod]
    public void SetTrendModel_SinusoidalScale_ProducesValidDefaultTrajectory()
    {
        var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineNormalData) };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal)
        {
            IsNonstationary = true
        };

        model.SetTrendModel(1, TrendModelType.Sinusoidal);

        for (int index = 0; index < InlineNormalData.Length; index++)
        {
            model.SetDistributionParameterValues(index);
            var validation = model.Distribution.ValidateParameters(model.Distribution.GetParameters, false);
            Assert.IsNull(validation, $"Default Normal scale trajectory is invalid at index {index}: {validation}");
        }
    }

    /// <summary>
    /// Verifies finite priors and valid default response trajectories for every supported parent
    /// distribution parameter crossed with every temporal trend model.
    /// </summary>
    /// <remarks>
    /// This deterministic configuration test covers 38 parent-distribution parameters and ten
    /// temporal trend types, for 380 assignments. GeneralLinear is excluded because it is a
    /// covariate/spatial model rather than a temporal trend.
    /// </remarks>
    [TestMethod]
    public void AllSupportedParentAndTemporalTrendAssignments_HaveFiniteValidDefaults()
    {
        UnivariateDistributionType[] distributionTypes =
        [
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.GammaDistribution,
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedLogistic,
            UnivariateDistributionType.GeneralizedNormal,
            UnivariateDistributionType.GeneralizedPareto,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.KappaFour,
            UnivariateDistributionType.LnNormal,
            UnivariateDistributionType.Logistic,
            UnivariateDistributionType.LogNormal,
            UnivariateDistributionType.LogPearsonTypeIII,
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.PearsonTypeIII,
            UnivariateDistributionType.Weibull
        ];
        TrendModelType[] trendTypes =
        [
            TrendModelType.Constant,
            TrendModelType.Cubic,
            TrendModelType.Exponential,
            TrendModelType.Linear,
            TrendModelType.Logistic,
            TrendModelType.Power,
            TrendModelType.Quadratic,
            TrendModelType.Reciprocal,
            TrendModelType.Sinusoidal,
            TrendModelType.StepFunction
        ];
        int assignmentCount = 0;

        foreach (UnivariateDistributionType distributionType in distributionTypes)
        {
            var stationary = new UnivariateDistribution(
                new BestFitDataFrame { ExactSeries = new ExactSeries(InlineNormalData) },
                distributionType);
            int parameterCount = stationary.Distribution.NumberOfParameters;

            for (int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
            {
                foreach (TrendModelType trendType in trendTypes)
                {
                    var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineNormalData) };
                    var model = new UnivariateDistribution(dataFrame, distributionType)
                    {
                        IsNonstationary = true
                    };
                    string cell = $"{distributionType}.parameter[{parameterIndex}] x {trendType}";

                    model.SetTrendModel(parameterIndex, trendType);
                    assignmentCount++;

                    foreach (var parameter in model.Parameters)
                    {
                        Assert.IsTrue(double.IsFinite(parameter.Value), $"{cell}: initial value must be finite.");
                        Assert.IsTrue(double.IsFinite(parameter.LowerBound), $"{cell}: lower bound must be finite.");
                        Assert.IsTrue(double.IsFinite(parameter.UpperBound), $"{cell}: upper bound must be finite.");
                        Assert.IsTrue(parameter.LowerBound < parameter.UpperBound, $"{cell}: bounds must be ordered.");
                        Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
                            $"{cell}: initial value must lie within its bounds.");
                        Assert.IsNotNull(parameter.PriorDistribution, $"{cell}: each parameter must have a prior.");
                        Assert.IsTrue(parameter.PriorDistribution.ParametersValid, $"{cell}: prior must be valid.");
                    }

                    var (isValid, validationMessages) = model.Validate();
                    Assert.IsTrue(isValid, $"{cell}: model validation failed: {string.Join(" | ", validationMessages)}");

                    for (int timeIndex = 0; timeIndex < InlineNormalData.Length; timeIndex++)
                    {
                        model.SetDistributionParameterValues(timeIndex);
                        var validation = model.Distribution.ValidateParameters(model.Distribution.GetParameters, false);
                        Assert.IsNull(validation, $"{cell}: invalid default response at index {timeIndex}: {validation}");
                    }
                }
            }
        }

        Assert.AreEqual(380, assignmentCount, "The matrix must cover 38 parent parameters by ten temporal trends.");
    }
}
