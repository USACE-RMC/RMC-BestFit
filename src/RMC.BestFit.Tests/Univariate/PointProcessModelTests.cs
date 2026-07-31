using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Unit tests for the <c>PointProcessModel</c> class.
/// Tests peaks-over-threshold (POT) models with Poisson process and GEV distributions.
/// </summary>
/// <remarks>
/// <para>
/// Point process models are used for analyzing peaks-over-threshold data where
/// exceedances of a high threshold are modeled as a Poisson process with GEV
/// marginal distributions. This approach:
/// </para>
/// <list type="bullet">
/// <item><description>Uses all exceedances rather than just annual maxima</description></item>
/// <item><description>Supports seasonal models with multiple GEV components</description></item>
/// <item><description>Incorporates threshold and total years parameters</description></item>
/// </list>
/// </remarks>
[TestClass]
public class PointProcessModelTests
{
    #region Test Data Helper

    /// <summary>
    /// Creates a sample POT data frame with exact observations.
    /// </summary>
    private static BestFitDataFrame CreatePOTDataFrame()
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>
        {
            new ExactData(new DateTime(1990, 3, 15), 1500),
            new ExactData(new DateTime(1990, 5, 20), 1800),
            new ExactData(new DateTime(1991, 4, 10), 2000),
            new ExactData(new DateTime(1992, 3, 25), 1700),
            new ExactData(new DateTime(1992, 6, 5), 2200),
            new ExactData(new DateTime(1993, 4, 1), 2500),
            new ExactData(new DateTime(1994, 5, 15), 1600),
            new ExactData(new DateTime(1995, 3, 30), 3000),
            new ExactData(new DateTime(1996, 4, 20), 2100),
            new ExactData(new DateTime(1997, 5, 10), 2800)
        };
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a seasonal POT data frame with events from different seasons.
    /// </summary>
    private static BestFitDataFrame CreateSeasonalPOTDataFrame()
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();

        // Winter/Spring events (season 1)
        for (int year = 1990; year < 2000; year++)
        {
            data.Add(new ExactData(new DateTime(year, 2, 15), 1500 + (year - 1990) * 100));
        }

        // Summer events (season 2)
        for (int year = 1990; year < 2000; year++)
        {
            data.Add(new ExactData(new DateTime(year, 7, 15), 2000 + (year - 1990) * 150));
        }

        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a simple annual maximum data frame.
    /// </summary>
    private static BestFitDataFrame CreateAMSDataFrame()
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 20; i++)
        {
            data.Add(new ExactData(1980 + i, 2000 + i * 100));
        }
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new PointProcessModel();

        Assert.IsNotNull(model);
        Assert.IsNotNull(model.Distribution);
        Assert.IsFalse(model.IsSeasonal);
    }

    /// <summary>Verifies that constructor empty constructor has single GEV.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_HasSingleGEV()
    {
        var model = new PointProcessModel();

        Assert.AreEqual(1, model.Distribution!.Distributions.Count);
        Assert.IsInstanceOfType(model.Distribution!.Distributions[0], typeof(GeneralizedExtremeValue));
    }

    /// <summary>Verifies that constructor with data and distribution sets properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithDataAndDistribution_SetsProperties()
    {
        var df = CreatePOTDataFrame();
        var dist = new CompetingRisks(new IUnivariateDistribution[]
        {
            new GeneralizedExtremeValue(2000, 500, 0.1)
        });

        var model = new PointProcessModel(df, dist);

        Assert.AreSame(df, model.DataFrame);
        Assert.IsNotNull(model.Distribution);
    }

    /// <summary>Verifies that constructor with data and distribution clones distribution.</summary>
    [TestMethod]
    public void Test_Constructor_WithDataAndDistribution_ClonesDistribution()
    {
        var df = CreatePOTDataFrame();
        var dist = new CompetingRisks(new IUnivariateDistribution[]
        {
            new GeneralizedExtremeValue(2000, 500, 0.1)
        });

        var model = new PointProcessModel(df, dist);

        Assert.AreNotSame(dist, model.Distribution);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that distribution set and get.</summary>
    [TestMethod]
    public void Test_Distribution_SetAndGet()
    {
        var model = new PointProcessModel();
        var newDist = new CompetingRisks(new IUnivariateDistribution[]
        {
            new GeneralizedExtremeValue(1000, 200, 0.0)
        });

        model.Distribution = newDist;

        Assert.IsNotNull(model.Distribution);
    }

    /// <summary>Verifies that threshold set and get.</summary>
    [TestMethod]
    public void Test_Threshold_SetAndGet()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        model.Threshold = 1000;

        Assert.AreEqual(1000, model.Threshold);
    }

    /// <summary>Verifies that total years set and get.</summary>
    [TestMethod]
    public void Test_TotalYears_SetAndGet()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        model.TotalYears = 25;

        Assert.AreEqual(25, model.TotalYears);
    }

    /// <summary>Verifies that total years updates lambda.</summary>
    [TestMethod]
    public void Test_TotalYears_UpdatesLambda()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        model.TotalYears = 10;

        // Lambda = events / years = 10 events / 10 years = 1.0
        Assert.AreEqual(1.0, model.Lambda, 1e-10);
    }

    /// <summary>Verifies that non-exact observations do not become empirical Poisson events.</summary>
    [TestMethod]
    public void Test_EmpiricalEventRate_CountsExactEventsOnly()
    {
        var df = CreatePOTDataFrame();
        df.UncertainSeries.Add(new UncertainData(1998, new Normal(1800.0, 100.0)));
        df.IntervalSeries.Add(new IntervalData(1999, 1600.0, 1700.0, 1800.0));
        df.ThresholdSeries.Add(new BestFitThresholdData(2000, 2005, 1500.0) { NumberAbove = 20 });
        var model = new PointProcessModel { DataFrame = df, TotalYears = 5.0 };

        Assert.AreEqual(df.ExactSeries.Count, model.EmpiricalEventCount);
        Assert.AreEqual(2.0, model.EmpiricalEventRate, 1E-12);
        Assert.AreEqual(model.EmpiricalEventRate, model.Lambda, 0.0);
    }

    /// <summary>Verifies that a retained source exposure takes precedence over event-span inference.</summary>
    [TestMethod]
    public void Test_DefaultExposure_UsesStoredSourceYears()
    {
        var df = CreatePOTDataFrame();
        df.PointProcessObservationYears = 25.0;

        var model = new PointProcessModel { DataFrame = df };
        var validation = model.Validate();

        Assert.AreEqual(25.0, model.TotalYears, 0.0);
        Assert.IsFalse(model.IsTotalYearsInferred);
        Assert.IsFalse(validation.ValidationMessages.Any(message => message.StartsWith("Warning: TotalYears", StringComparison.Ordinal)));
    }

    /// <summary>Verifies that an explicit exposure is not replaced by a stored source exposure.</summary>
    [TestMethod]
    public void Test_DefaultExposure_ExplicitTotalYearsHasPrecedence()
    {
        var df = CreatePOTDataFrame();
        df.PointProcessObservationYears = 25.0;
        var model = new PointProcessModel { UseDefaults = false, DataFrame = df, TotalYears = 40.0 };

        model.SetDefaultThresholdAndTotalYears();

        Assert.AreEqual(40.0, model.TotalYears, 0.0);
        Assert.IsFalse(model.IsTotalYearsInferred);
    }

    /// <summary>Verifies that event-span fallback is identified by a validation warning.</summary>
    [TestMethod]
    public void Test_DefaultExposure_EventSpanFallbackWarns()
    {
        var model = new PointProcessModel { DataFrame = CreatePOTDataFrame() };

        var validation = model.Validate();

        Assert.IsTrue(model.IsTotalYearsInferred);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.StartsWith("Warning: TotalYears", StringComparison.Ordinal)));
    }

    /// <summary>Verifies that explicitly accepting an inferred value records an explicit override.</summary>
    [TestMethod]
    public void Test_TotalYears_SettingSameValueClearsInferenceFlag()
    {
        var model = new PointProcessModel { DataFrame = CreatePOTDataFrame() };
        model.UseDefaults = false;

        model.TotalYears = model.TotalYears;

        Assert.IsFalse(model.IsTotalYearsInferred);
    }

    /// <summary>Verifies explicit exposure precedence and rate refresh regardless of initializer order.</summary>
    [TestMethod]
    public void Test_DataFrameReplacement_WithDefaultsDisabledPreservesExposureAndRefreshesRate()
    {
        var frame = CreatePOTDataFrame();
        var model = new PointProcessModel
        {
            UseDefaults = false,
            TotalYears = 5.0,
            DataFrame = frame
        };

        Assert.AreEqual(5.0, model.TotalYears, 0.0);
        Assert.AreEqual(2.0, model.Lambda, 1E-12);

        frame.ExactSeries.Add(new ExactData(2011, 2100.0));

        Assert.AreEqual(11, model.EmpiricalEventCount);
        Assert.AreEqual(2.2, model.Lambda, 1E-12);
    }

    /// <summary>Verifies that use defaults sets threshold and years.</summary>
    [TestMethod]
    public void Test_UseDefaults_SetsThresholdAndYears()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;

        model.UseDefaults = true;

        Assert.IsFalse(double.IsNaN(model.Threshold));
        Assert.IsFalse(double.IsNaN(model.TotalYears));
    }

    /// <summary>Verifies that is seasonal set and get.</summary>
    [TestMethod]
    public void Test_IsSeasonal_SetAndGet()
    {
        var model = new PointProcessModel();

        model.IsSeasonal = true;

        Assert.IsTrue(model.IsSeasonal);
    }

    /// <summary>Verifies that is seasonal true creates two GE vs.</summary>
    [TestMethod]
    public void Test_IsSeasonal_True_CreatesTwoGEVs()
    {
        var model = new PointProcessModel();

        model.IsSeasonal = true;

        Assert.AreEqual(2, model.Distribution!.Distributions.Count);
    }

    /// <summary>Verifies that is seasonal false creates single GEV.</summary>
    [TestMethod]
    public void Test_IsSeasonal_False_CreatesSingleGEV()
    {
        var model = new PointProcessModel();
        model.IsSeasonal = true;  // First set to true
        model.IsSeasonal = false; // Then back to false

        Assert.AreEqual(1, model.Distribution!.Distributions.Count);
    }

    /// <summary>Verifies that time block set and get.</summary>
    [TestMethod]
    public void Test_TimeBlock_SetAndGet()
    {
        var model = new PointProcessModel();

        model.TimeBlock = TimeBlockWindow.CalendarYear;

        Assert.AreEqual(TimeBlockWindow.CalendarYear, model.TimeBlock);
    }

    /// <summary>Verifies that start month set and get.</summary>
    [TestMethod]
    public void Test_StartMonth_SetAndGet()
    {
        var model = new PointProcessModel();

        model.StartMonth = 1; // January

        Assert.AreEqual(1, model.StartMonth);
    }

    /// <summary>Verifies that lambda calculated from events and years.</summary>
    [TestMethod]
    public void Test_Lambda_CalculatedFromEventsAndYears()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.TotalYears = 8; // 8 years for 10 events

        double expected = 10.0 / 8.0;
        Assert.AreEqual(expected, model.Lambda, 1e-10);
    }

    #endregion

    #region SetDistribution Tests

    /// <summary>Verifies that set distribution non seasonal single GEV.</summary>
    [TestMethod]
    public void Test_SetDistribution_NonSeasonal_SingleGEV()
    {
        var model = new PointProcessModel();
        model.IsSeasonal = false;

        Assert.AreEqual(1, model.Distribution!.Distributions.Count);
        Assert.IsInstanceOfType(model.Distribution!.Distributions[0], typeof(GeneralizedExtremeValue));
    }

    /// <summary>Verifies that set distribution seasonal two GE vs.</summary>
    [TestMethod]
    public void Test_SetDistribution_Seasonal_TwoGEVs()
    {
        var model = new PointProcessModel();
        model.IsSeasonal = true;

        Assert.AreEqual(2, model.Distribution!.Distributions.Count);
        Assert.IsInstanceOfType(model.Distribution!.Distributions[0], typeof(GeneralizedExtremeValue));
        Assert.IsInstanceOfType(model.Distribution!.Distributions[1], typeof(GeneralizedExtremeValue));
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters non seasonal three GEV parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_NonSeasonal_ThreeGEVParameters()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        // Non-seasonal: 3 GEV parameters (xi, alpha, kappa)
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>Verifies that set default parameters seasonal eight parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_Seasonal_EightParameters()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        // Seasonal: 2 change points + 3 GEV params × 2 seasons = 8
        Assert.AreEqual(8, model.NumberOfParameters);
    }

    /// <summary>Verifies that set default parameters seasonal has change points.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_Seasonal_HasChangePoints()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        Assert.IsTrue(model.Parameters[0].Name.Contains("Change Point"));
        Assert.IsTrue(model.Parameters[1].Name.Contains("Change Point"));
    }

    /// <summary>Verifies that set default parameters has uniform priors.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_HasUniformPriors()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        foreach (var param in model.Parameters)
        {
            Assert.IsInstanceOfType(param.PriorDistribution, typeof(Uniform));
        }
    }

    #endregion

    #region SetDefaultThresholdAndTotalYears Tests

    /// <summary>Verifies that set default threshold and total years sets threshold.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_SetsThreshold()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;
        // Baseline threshold before the method call — should differ from the value set afterward.
        double baselineThreshold = model.Threshold;

        model.SetDefaultThresholdAndTotalYears();

        // The method sets Threshold to (min - machineEpsilon). At the scale of typical flood
        // peaks (1500-3000), machineEpsilon (~2.22e-16) is far smaller than the double spacing
        // near 1500 (~3.3e-13), so the subtraction may round back to the minimum value exactly.
        // The meaningful invariant is that Threshold is now set near the minimum observed value
        // (and has changed from its uninitialized baseline).
        double minValue = df.ExactSeries.Min(x => x.Value);
        Assert.AreNotEqual(baselineThreshold, model.Threshold,
            "SetDefaultThresholdAndTotalYears must update Threshold from its baseline.");
        Assert.AreEqual(minValue, model.Threshold, 1e-6,
            "Threshold should be set approximately equal to the minimum observed value.");
    }

    /// <summary>Verifies that set default threshold and total years sets total years.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_SetsTotalYears()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;

        model.SetDefaultThresholdAndTotalYears();

        Assert.IsTrue(model.TotalYears > 0);
    }

    /// <summary>Verifies that re-enabling defaults replaces a manual total-years value.</summary>
    [TestMethod]
    public void Test_UseDefaults_RecomputesTotalYearsAfterManualOverride()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.UseDefaults = false;
        model.TotalYears = 123.0;

        model.UseDefaults = true;

        Assert.AreEqual(df.ExactSeries.IndexSpan(), model.TotalYears, 1e-10);
    }

    /// <summary>Verifies that Lambda is current when default TotalYears notification is raised.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_TotalYearsEventSeesUpdatedLambda()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.UseDefaults = false;
        model.TotalYears = 123.0;
        double lambdaAtTotalYearsEvent = double.NaN;

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PointProcessModel.TotalYears))
                lambdaAtTotalYearsEvent = model.Lambda;
        };

        model.UseDefaults = true;

        Assert.AreEqual(df.ExactSeries.Count / (double)df.ExactSeries.IndexSpan(), lambdaAtTotalYearsEvent, 1e-10);
    }

    /// <summary>Verifies that POT threshold input is used when it is below all exact events.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_UsesPeaksOverThresholdInput()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel { DataFrame = df };
        double potThreshold = df.ExactSeries.MinimumValue() - 100.0;

        model.SetDefaultThresholdAndTotalYears(potThreshold, forceTotalYears: true);

        Assert.AreEqual(potThreshold, model.Threshold, 1e-10);
    }

    /// <summary>Verifies that POT threshold input is clamped below the exact-event minimum.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_ClampsPeaksOverThresholdInput()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel { DataFrame = df };
        double exactMinimum = df.ExactSeries.MinimumValue();

        model.SetDefaultThresholdAndTotalYears(exactMinimum + 100.0, forceTotalYears: true);

        Assert.IsTrue(model.Threshold < exactMinimum);
    }

    /// <summary>Verifies that non-exact data do not extend point-process exposure duration.</summary>
    [TestMethod]
    public void Test_SetDefaultThresholdAndTotalYears_TotalYearsUsesExactSpanOnly()
    {
        var df = CreatePOTDataFrame();
        double expectedSpan = df.ExactSeries.IndexSpan();
        df.ThresholdSeries.Add(new BestFitThresholdData(1000, 2000, 500.0) { NumberAbove = 3 });
        df.UncertainSeries.Add(new UncertainData(500, new Normal(1000.0, 100.0)));
        df.IntervalSeries.Add(new IntervalData(2500, 900.0, 1000.0, 1100.0));
        var model = new PointProcessModel { DataFrame = df };

        model.SetDefaultThresholdAndTotalYears(forceTotalYears: true);

        Assert.AreEqual(expectedSpan, model.TotalYears, 1e-10);
    }

    #endregion

    #region CalculateLambda Tests

    /// <summary>Verifies that calculate lambda returns events per year.</summary>
    [TestMethod]
    public void Test_CalculateLambda_ReturnsEventsPerYear()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.TotalYears = 10;

        model.CalculateLambda();

        // 10 events / 10 years = 1.0
        Assert.AreEqual(1.0, model.Lambda, 1e-10);
    }

    /// <summary>Verifies that calculate lambda returns na n when null data frame.</summary>
    [TestMethod]
    public void Test_CalculateLambda_NullDataFrame_ReturnsNaN()
    {
        var model = new PointProcessModel();
        model.CalculateLambda();

        Assert.IsTrue(double.IsNaN(model.Lambda));
    }

    /// <summary>Verifies that calculate lambda returns na n when zero years.</summary>
    [TestMethod]
    public void Test_CalculateLambda_ZeroYears_ReturnsNaN()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;
        model.TotalYears = 0;

        model.CalculateLambda();

        Assert.IsTrue(double.IsNaN(model.Lambda));
    }

    #endregion

    #region SetAMSData Tests

    /// <summary>Verifies that set AMS data non seasonal creates block maxima.</summary>
    [TestMethod]
    public void Test_SetAMSData_NonSeasonal_CreatesBlockMaxima()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = false;
        model.DataFrame = df;

        Assert.IsTrue(model.AMSDataFrame.ExactSeries.Count > 0);
    }

    /// <summary>Verifies that set AMS data seasonal creates POT days.</summary>
    [TestMethod]
    public void Test_SetAMSData_Seasonal_CreatesPOTDays()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        Assert.IsTrue(model.POTDays.Count > 0);
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>Verifies that data log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ReturnsFiniteValue()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>Verifies that prior log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>Verifies that data log likelihood returns negative infinity when null data frame.</summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NullDataFrame_ReturnsNegativeInfinity()
    {
        var model = new PointProcessModel();

        double result = model.DataLogLikelihood(new double[] { 1, 2, 3 });

        Assert.AreEqual(double.NegativeInfinity, result);
    }

    /// <summary>Verifies that data log likelihood returns negative infinity when na n threshold.</summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NaNThreshold_ReturnsNegativeInfinity()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;
        model.Threshold = double.NaN;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double result = model.DataLogLikelihood(parameters);

        Assert.AreEqual(double.NegativeInfinity, result);
    }

    /// <summary>Verifies that pointwise data log likelihood returns correct count.</summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        Assert.AreEqual(df.ExactSeries.Count, pointwise.Length);
    }

    #endregion

    #region Seasonal Model Tests

    /// <summary>Verifies that seasonal change points have correct bounds.</summary>
    [TestMethod]
    public void Test_Seasonal_ChangePointsHaveCorrectBounds()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        Assert.AreEqual(1.0, model.Parameters[0].LowerBound);
        Assert.AreEqual(Math.BitDecrement(251.0), model.Parameters[0].UpperBound);
        Assert.AreEqual(200.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(Math.BitDecrement(367.0), model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that seasonal change point default values.</summary>
    [TestMethod]
    public void Test_Seasonal_ChangePointDefaultValues()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        // Default K1 = 90 (around April 1)
        Assert.AreEqual(90, model.Parameters[0].Value);

        // Default K2 = 250 (around September 7)
        Assert.AreEqual(250, model.Parameters[1].Value);
    }

    /// <summary>Verifies that seasonal returns finite when data log likelihood.</summary>
    [TestMethod]
    public void Test_Seasonal_DataLogLikelihood_ReturnsFinite()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>Verifies that block-indexed non-exact records use the annual maximum distribution.</summary>
    [TestMethod]
    public void Test_Seasonal_NonExactLikelihoodUsesAnnualizedCompetingRisk()
    {
        var frame = CreateSeasonalPOTDataFrame();
        var measurementError = new Normal(2250.0, 75.0);
        frame.UncertainSeries.Add(new UncertainData(2001, measurementError));
        frame.IntervalSeries.Add(new IntervalData(2002, 2000.0, 2200.0, 2400.0));
        frame.ThresholdSeries.Add(new BestFitThresholdData(2003, 2004, 2100.0)
        {
            NumberBelow = 1,
            NumberAbove = 1
        });
        var model = new PointProcessModel { IsSeasonal = true, DataFrame = frame };
        double[] parameters = { 90.0, 250.0, 2100.0, 450.0, 0.10, 2500.0, 550.0, -0.05 };
        CompetingRisks annualMaximum = model.GetDistribution(parameters);

        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        int nonExactIndex = frame.ExactSeries.Count;
        const double lowerProbability = 1E-8;
        const double upperProbability = 1.0 - 1E-8;
        double lower = measurementError.InverseCDF(lowerProbability);
        double upper = measurementError.InverseCDF(upperProbability);
        double expectedUncertain = Math.Log(Integration.GaussLegendre20(
            value => measurementError.PDF(value) * annualMaximum.PDF(value), lower, upper) /
            (upperProbability - lowerProbability));
        double expectedInterval = annualMaximum.LogLikelihood_Intervals(2000.0, 2400.0);
        double expectedThreshold = annualMaximum.LogLikelihood_LeftCensored(2100.0, 1) +
                                   annualMaximum.LogLikelihood_RightCensored(2100.0, 1);

        Assert.AreEqual(expectedUncertain, pointwise[nonExactIndex], 1E-12);
        Assert.AreEqual(expectedInterval, pointwise[nonExactIndex + 1], 1E-12);
        Assert.AreEqual(expectedThreshold, pointwise[nonExactIndex + 2], 1E-12);
        Assert.AreEqual(model.DataLogLikelihood(parameters), pointwise.Sum(), 1E-10);
    }

    /// <summary>Verifies that seasonal weights use the floored integer changepoint days.</summary>
    [TestMethod]
    public void Test_Seasonal_ExposureWeightsUseFlooredChangePoints()
    {
        var model = new PointProcessModel { IsSeasonal = true, DataFrame = CreateSeasonalPOTDataFrame() };
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        parameters[0] = 10.9;
        parameters[1] = 200.9;

        model.SetParameterValues(parameters);

        Assert.AreEqual(176.0 / 366.0, model.SeasonOneExposureWeight, 1E-15);
        Assert.AreEqual(190.0 / 366.0, model.SeasonTwoExposureWeight, 1E-15);
    }

    /// <summary>Verifies that proposals within the same changepoint day cells have identical likelihoods.</summary>
    [TestMethod]
    public void Test_Seasonal_LikelihoodIsConstantWithinFlooredDayCells()
    {
        var model = new PointProcessModel { IsSeasonal = true, DataFrame = CreateSeasonalPOTDataFrame() };
        double[] first = model.Parameters.Select(parameter => parameter.Value).ToArray();
        first[0] = 10.1;
        first[1] = 200.8;
        double[] second = (double[])first.Clone();
        second[0] = 10.9;
        second[1] = 200.1;

        Assert.AreEqual(model.DataLogLikelihood(first), model.DataLogLikelihood(second), 0.0);
    }

    /// <summary>Verifies elapsed-day water-year indexing across a leap day.</summary>
    [TestMethod]
    public void Test_Seasonal_WaterYearBlockDaysUseElapsedDates()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new ExactData(new DateTime(2019, 10, 1), 1000.0),
                new ExactData(new DateTime(2020, 9, 30), 1100.0)
            })
        };
        var model = new PointProcessModel
        {
            IsSeasonal = true,
            TimeBlock = TimeBlockWindow.WaterYear,
            StartMonth = 10,
            DataFrame = df
        };

        CollectionAssert.AreEqual(new List<int> { 1, 366 }, model.POTDays);
    }

    /// <summary>Verifies the analytical fitted Gumbel threshold intensity.</summary>
    [TestMethod]
    public void Test_FittedThresholdIntensity_GumbelMatchesAnalyticalMeasure()
    {
        var model = new PointProcessModel
        {
            UseDefaults = false,
            DataFrame = CreatePOTDataFrame(),
            Threshold = 900.0,
            TotalYears = 10.0
        };
        model.SetParameterValues(new double[] { 1000.0, 100.0, 0.0 });

        Assert.AreEqual(Math.Exp(1.0), model.FittedThresholdIntensity, 1E-12);
        Assert.IsTrue(double.IsNaN(model.FittedSeasonOneThresholdIntensity));
    }

    /// <summary>Verifies that duration-based simulation uses the empirical Poisson arrival rate.</summary>
    [TestMethod]
    public void Test_GeneratePOTTimeSeries_DurationUsesEmpiricalLambda()
    {
        const double threshold = 0.4;
        const double lambda = 8.0;
        const double gpaScale = 0.7;
        const double kappa = -0.2;
        const double durationYears = 3.0;
        const int seed = 12345;
        var model = new PointProcessModel
        {
            UseDefaults = false,
            DataFrame = CreatePOTDataFrame()
        };
        model.Threshold = threshold;
        model.TotalYears = model.DataFrame.ExactSeries.Count / lambda;
        model.SetDefaultParameters();

        double gevLocation = threshold + gpaScale / kappa * (1.0 - Math.Pow(lambda, -kappa));
        double gevScale = gpaScale * Math.Pow(lambda, -kappa);
        model.SetParameterValues(new[] { gevLocation, gevScale, kappa });

        var rng = new Numerics.Sampling.MersenneTwister(seed);
        int expectedCount = checked((int)new Poisson(durationYears * lambda).InverseCDF(rng.NextDouble()));
        Numerics.Data.TimeSeries actual = model.GeneratePOTTimeSeries(
            new DateTime(2000, 1, 1),
            durationYears,
            seed);

        Assert.AreEqual(expectedCount, actual.Count);
        Assert.IsTrue(actual.All(point => point.Value >= threshold));
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var clone = (PointProcessModel)model.Clone();

        Assert.AreNotSame(model, clone);
        Assert.AreNotSame(model.Distribution, clone.Distribution);
    }

    /// <summary>Verifies that clone preserves threshold for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesThreshold()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.Threshold = 1234.5;

        var clone = (PointProcessModel)model.Clone();

        Assert.AreEqual(1234.5, clone.Threshold);
    }

    /// <summary>Verifies that clone preserves total years for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesTotalYears()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.TotalYears = 50;

        var clone = (PointProcessModel)model.Clone();

        Assert.AreEqual(50, clone.TotalYears);
    }

    /// <summary>Verifies that clone preserves is seasonal for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesIsSeasonal()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        var clone = (PointProcessModel)model.Clone();

        Assert.IsTrue(clone.IsSeasonal);
    }

    /// <summary>Verifies that clone preserves time block settings for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesTimeBlockSettings()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.TimeBlock = TimeBlockWindow.CalendarYear;
        model.StartMonth = 1;

        var clone = (PointProcessModel)model.Clone();

        Assert.AreEqual(TimeBlockWindow.CalendarYear, clone.TimeBlock);
        Assert.AreEqual(1, clone.StartMonth);
    }

    /// <summary>Verifies that clone parameters are independent.</summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        double originalValue = model.Parameters[0].Value;

        var clone = (PointProcessModel)model.Clone();
        model.Parameters[0].Value = 99999;

        Assert.AreEqual(originalValue, clone.Parameters[0].Value);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains point process model element.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsPointProcessModelElement()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var xElement = model.ToXElement();

        Assert.AreEqual("PointProcessModel", xElement.Name.LocalName);
    }

    /// <summary>Verifies that to X element contains threshold attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsThresholdAttribute()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("Threshold"));
    }

    /// <summary>Verifies that to X element contains total years attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTotalYearsAttribute()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("TotalYears"));
        Assert.IsNotNull(xElement.Attribute("IsTotalYearsInferred"));
    }

    /// <summary>Verifies that to X element contains is seasonal attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsIsSeasonalAttribute()
    {
        var model = new PointProcessModel();

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("IsSeasonal"));
    }

    /// <summary>Verifies that to X element contains distribution element.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsDistributionElement()
    {
        var model = new PointProcessModel();

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Element("Distribution"));
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var df = CreateAMSDataFrame();
        var original = new PointProcessModel();
        original.DataFrame = df;
        original.Threshold = 1500;
        original.TotalYears = 25;

        var xElement = original.ToXElement();
        var restored = new PointProcessModel(df, xElement);

        Assert.AreEqual(original.Threshold, restored.Threshold, 1e-10);
        Assert.AreEqual(original.TotalYears, restored.TotalYears, 1e-10);
        Assert.AreEqual(original.IsSeasonal, restored.IsSeasonal);
        Assert.AreEqual(original.IsTotalYearsInferred, restored.IsTotalYearsInferred);
    }

    /// <summary>Verifies that inferred exposure origin survives point-process XML serialization.</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesInferredExposureOrigin()
    {
        var df = CreatePOTDataFrame();
        var original = new PointProcessModel { DataFrame = df };

        var restored = new PointProcessModel(df, original.ToXElement());

        Assert.IsTrue(original.IsTotalYearsInferred);
        Assert.IsTrue(restored.IsTotalYearsInferred);
    }

    /// <summary>Verifies that round trip preserves seasonal settings for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesSeasonalSettings()
    {
        var df = CreateSeasonalPOTDataFrame();
        var original = new PointProcessModel();
        original.IsSeasonal = true;
        original.TimeBlock = TimeBlockWindow.CalendarYear;
        original.StartMonth = 1;
        original.DataFrame = df;

        var xElement = original.ToXElement();
        var restored = new PointProcessModel(df, xElement);

        Assert.IsTrue(restored.IsSeasonal);
        Assert.AreEqual(TimeBlockWindow.CalendarYear, restored.TimeBlock);
        Assert.AreEqual(1, restored.StartMonth);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid model.</summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate returns false when null data frame.</summary>
    [TestMethod]
    public void Test_Validate_NullDataFrame_ReturnsFalse()
    {
        var model = new PointProcessModel();

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Data frame")));
    }

    /// <summary>Verifies that validate returns false when na n threshold.</summary>
    [TestMethod]
    public void Test_Validate_NaNThreshold_ReturnsFalse()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;
        model.Threshold = double.NaN;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Threshold")));
    }

    /// <summary>Verifies that validate returns false when invalid total years.</summary>
    [TestMethod]
    public void Test_Validate_InvalidTotalYears_ReturnsFalse()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.UseDefaults = false;
        model.DataFrame = df;
        model.Threshold = 1000;
        model.TotalYears = 0;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("TotalYears")));
    }

    /// <summary>Verifies that validate returns false when invalid start month.</summary>
    [TestMethod]
    public void Test_Validate_InvalidStartMonth_ReturnsFalse()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;
        model.StartMonth = 13; // Invalid month

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("StartMonth")));
    }

    /// <summary>Verifies that seasonal fitting rejects an exact observation without a date.</summary>
    [TestMethod]
    public void Test_Validate_SeasonalUndatedExactObservation_ReturnsFalse()
    {
        var frame = CreateSeasonalPOTDataFrame();
        frame.ExactSeries.Add(new ExactData(2001, 1750.0));
        var model = new PointProcessModel { IsSeasonal = true, DataFrame = frame };

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(message => message.Contains("requires a valid date", StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>Verifies that set parameter values updates parameters.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesParameters()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var newValues = new double[] { 2500.0, 750.0, 0.1 };
        model.SetParameterValues(newValues);

        Assert.AreEqual(2500.0, model.Parameters[0].Value);
        Assert.AreEqual(750.0, model.Parameters[1].Value);
        Assert.AreEqual(0.1, model.Parameters[2].Value);
    }

    /// <summary>Verifies that set parameter values throws when null parameters.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullParameters_ThrowsException()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        model.SetParameterValues(null!);
    }

    /// <summary>Verifies that set parameter values throws when wrong count.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongCount_ThrowsException()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var wrongCount = new double[] { 1.0 };
        model.SetParameterValues(wrongCount);
    }

    #endregion

    #region GetDistribution Tests

    /// <summary>Verifies that get distribution returns cloned distribution.</summary>
    [TestMethod]
    public void Test_GetDistribution_ReturnsClonedDistribution()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var dist = model.GetDistribution(parameters);

        Assert.IsNotNull(dist);
        Assert.AreNotSame(model.Distribution, dist);
    }

    /// <summary>Verifies that get distribution applies parameters.</summary>
    [TestMethod]
    public void Test_GetDistribution_AppliesParameters()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = new double[] { 2000.0, 500.0, 0.1 };
        var dist = model.GetDistribution(parameters);

        var gev = (GeneralizedExtremeValue)dist.Distributions[0];
        Assert.AreEqual(2000.0, gev.Xi, 1e-10);
        Assert.AreEqual(500.0, gev.Alpha, 1e-10);
        Assert.AreEqual(0.1, gev.Kappa, 1e-10);
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>Verifies that point process flood frequency analysis.</summary>
    [TestMethod]
    public void Test_PointProcess_FloodFrequencyAnalysis()
    {
        // Typical POT flood frequency analysis scenario
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(dataLogLH));
    }

    /// <summary>Verifies that point process seasonal flood analysis.</summary>
    [TestMethod]
    public void Test_PointProcess_SeasonalFloodAnalysis()
    {
        // Seasonal POT analysis with winter/summer populations
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.DataFrame = df;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        Assert.AreEqual(2, model.Distribution!.Distributions.Count);
    }

    /// <summary>Verifies that point process water year convention.</summary>
    [TestMethod]
    public void Test_PointProcess_WaterYearConvention()
    {
        // Water year starting in October
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.TimeBlock = TimeBlockWindow.WaterYear;
        model.StartMonth = 10;
        model.DataFrame = df;

        Assert.AreEqual(TimeBlockWindow.WaterYear, model.TimeBlock);
        Assert.AreEqual(10, model.StartMonth);
    }

    #endregion

    #region Jeffreys Prior Tests

    /// <summary>Verifies that jeffreys prior affects prior log likelihood.</summary>
    [TestMethod]
    public void Test_JeffreysPrior_AffectsPriorLogLikelihood()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        model.UseJeffreysRuleForScale = false;
        double priorNoJeffreys = model.PriorLogLikelihood(parameters);

        model.UseJeffreysRuleForScale = true;
        double priorWithJeffreys = model.PriorLogLikelihood(parameters);

        Assert.AreNotEqual(priorNoJeffreys, priorWithJeffreys);
    }

    /// <summary>Verifies that jeffreys prior seasonal applied to both GE vs.</summary>
    [TestMethod]
    public void Test_JeffreysPrior_Seasonal_AppliedToBothGEVs()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel();
        model.IsSeasonal = true;
        model.UseJeffreysRuleForScale = true;
        model.DataFrame = df;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var priors = model.PointwisePriorLogLikelihood(parameters);

        // Should have 2 Jeffreys scale priors (one per GEV)
        int jeffreysCount = priors.Count(p => p.Type == PriorComponentType.JeffreysScalePrior);
        Assert.AreEqual(2, jeffreysCount);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that point process single data point.</summary>
    [TestMethod]
    public void Test_PointProcess_SingleDataPoint()
    {
        var df = new BestFitDataFrame();
        df.ExactSeries = new ExactSeries(new List<ExactData>
        {
            new ExactData(2000, 1000)
        });

        var model = new PointProcessModel();
        model.DataFrame = df;

        Assert.IsNotNull(model.Distribution);
    }

    /// <summary>Verifies that point process large values.</summary>
    [TestMethod]
    public void Test_PointProcess_LargeValues()
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new ExactData(2000 + i, 1e8 + i * 1e6));
        }
        df.ExactSeries = new ExactSeries(data);

        var model = new PointProcessModel();
        model.DataFrame = df;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that point process small values.</summary>
    [TestMethod]
    public void Test_PointProcess_SmallValues()
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new ExactData(2000 + i, 0.01 + i * 0.001));
        }
        df.ExactSeries = new ExactSeries(data);

        var model = new PointProcessModel();
        model.DataFrame = df;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that point process many events per year.</summary>
    [TestMethod]
    public void Test_PointProcess_ManyEventsPerYear()
    {
        // High-frequency POT data
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 100; i++)
        {
            data.Add(new ExactData(2000 + i / 10, 1000 + i * 50));
        }
        df.ExactSeries = new ExactSeries(data);

        var model = new PointProcessModel();
        model.DataFrame = df;
        model.TotalYears = 10;

        // Lambda should be ~10 events per year
        Assert.AreEqual(10.0, model.Lambda, 1e-10);
    }

    #endregion

    #region Random Generation Tests

    /// <summary>
    /// Verifies that nonseasonal random generation uses the empirical Poisson rate and the
    /// Hosking-parameterized Madsen GEV-to-GPA conversion.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_NonSeasonal_UsesPoissonGpaMadsenProcess()
    {
        const double threshold = 0.4;
        const double lambda = 8.0;
        const double gpaScale = 0.7;
        const double kappa = -0.2;
        const int sampleSize = 50;
        const int seed = 12345;

        var model = new PointProcessModel
        {
            UseDefaults = false,
            DataFrame = CreatePOTDataFrame()
        };
        model.Threshold = threshold;
        model.TotalYears = model.DataFrame.ExactSeries.Count / lambda;
        model.SetDefaultParameters();

        double gevLocation = threshold + gpaScale / kappa * (1.0 - Math.Pow(lambda, -kappa));
        double gevScale = gpaScale * Math.Pow(lambda, -kappa);
        model.SetParameterValues(new[] { gevLocation, gevScale, kappa });

        double[] actual = model.GenerateRandomValues(sampleSize, seed);

        var poisson = new Poisson(lambda);
        var gpa = new GeneralizedPareto(threshold, gpaScale, kappa);
        var rng = new Numerics.Sampling.MersenneTwister(seed);
        var expected = new double[sampleSize];
        int generated = 0;
        while (generated < sampleSize)
        {
            int yearlyEventCount = checked((int)poisson.InverseCDF(rng.NextDouble()));
            int eventsToRetain = Math.Min(yearlyEventCount, sampleSize - generated);
            for (int i = 0; i < eventsToRetain; i++)
            {
                expected[generated] = gpa.InverseCDF(rng.NextDouble());
                generated++;
            }
        }

        Assert.AreEqual(lambda, model.Lambda, 1E-12);
        Assert.AreEqual(sampleSize, actual.Length);
        for (int i = 0; i < sampleSize; i++)
        {
            Assert.AreEqual(expected[i], actual[i], 1E-12, $"Generated magnitude {i} did not match the Numerics Poisson-GPA construction.");
        }
        Assert.IsTrue(actual.All(value => value >= threshold));
    }

    /// <summary>
    /// Verifies seasonal Poisson thinning, Madsen GPA marks, and calendar/water-year dummy dates.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_Seasonal_UsesPoissonGpaExposureMixture()
    {
        const double threshold = 0.4;
        const double lambda = 8.0;
        const double gpaScaleOne = 0.5;
        const double gpaKappaOne = 0.1;
        const double gpaScaleTwo = 0.7;
        const double gpaKappaTwo = -0.2;
        const int changePointOne = 60;
        const int changePointTwo = 210;
        const int sampleSize = 80;
        const int seed = 24680;
        var model = new PointProcessModel
        {
            UseDefaults = false,
            IsSeasonal = true,
            TimeBlock = TimeBlockWindow.CalendarYear,
            StartMonth = 1,
            DataFrame = CreateSeasonalPOTDataFrame()
        };
        model.Threshold = threshold;
        model.TotalYears = model.DataFrame.ExactSeries.Count / lambda;
        model.SetDefaultParameters();

        double gevLocationOne = threshold + gpaScaleOne / gpaKappaOne *
            (1.0 - Math.Pow(lambda, -gpaKappaOne));
        double gevScaleOne = gpaScaleOne * Math.Pow(lambda, -gpaKappaOne);
        double gevLocationTwo = threshold + gpaScaleTwo / gpaKappaTwo *
            (1.0 - Math.Pow(lambda, -gpaKappaTwo));
        double gevScaleTwo = gpaScaleTwo * Math.Pow(lambda, -gpaKappaTwo);
        model.SetParameterValues(new[]
        {
            changePointOne + 0.75,
            changePointTwo + 0.25,
            gevLocationOne,
            gevScaleOne,
            gpaKappaOne,
            gevLocationTwo,
            gevScaleTwo,
            gpaKappaTwo
        });

        double weightOne = (changePointOne + 366.0 - changePointTwo) / 366.0;
        int firstSegmentDays = changePointOne - 1;
        int seasonOneDays = changePointOne + 366 - changePointTwo;
        int seasonTwoDays = changePointTwo - changePointOne;
        var poisson = new Poisson(lambda);
        var gpaOne = new GeneralizedPareto(threshold, gpaScaleOne, gpaKappaOne);
        var gpaTwo = new GeneralizedPareto(threshold, gpaScaleTwo, gpaKappaTwo);
        var rng = new Numerics.Sampling.MersenneTwister(seed);
        var expected = new List<(int component, int day, int block, double magnitude)>(sampleSize);
        int blockIndex = 0;
        while (expected.Count < sampleSize)
        {
            int yearlyEventCount = checked((int)poisson.InverseCDF(rng.NextDouble()));
            int eventsToRetain = Math.Min(yearlyEventCount, sampleSize - expected.Count);
            for (int i = 0; i < eventsToRetain; i++)
            {
                int component = rng.NextDouble() >= weightOne ? 1 : 0;
                int day;
                if (component == 0)
                {
                    int seasonIndex = Math.Min((int)(rng.NextDouble() * seasonOneDays), seasonOneDays - 1);
                    day = seasonIndex < firstSegmentDays
                        ? seasonIndex + 1
                        : changePointTwo + seasonIndex - firstSegmentDays;
                }
                else
                {
                    day = changePointOne +
                        Math.Min((int)(rng.NextDouble() * seasonTwoDays), seasonTwoDays - 1);
                }

                double magnitude = (component == 0 ? gpaOne : gpaTwo).InverseCDF(rng.NextDouble());
                expected.Add((component, day, blockIndex, magnitude));
            }
            blockIndex++;
        }

        double[] actualMagnitudes = model.GenerateRandomValues(sampleSize, seed);
        Assert.AreEqual(sampleSize, actualMagnitudes.Length);
        for (int i = 0; i < sampleSize; i++)
        {
            Assert.AreEqual(expected[i].magnitude, actualMagnitudes[i], 1E-12);
            Assert.AreEqual(expected[i].component == 0, expected[i].day < changePointOne || expected[i].day >= changePointTwo);
        }

        var expectedOrdered = expected.OrderBy(point => point.block).ThenBy(point => point.day).ThenBy(point => point.magnitude).ToArray();
        Numerics.Data.TimeSeries calendar = model.GeneratePOTTimeSeries(sampleSize, seed);
        var calendarOrdered = calendar.OrderBy(point => point.Index).ThenBy(point => point.Value).ToArray();
        for (int i = 0; i < sampleSize; i++)
        {
            DateTime calendarStart = new DateTime(2000 + 4 * expectedOrdered[i].block, 1, 1);
            Assert.AreEqual(calendarStart.AddDays(expectedOrdered[i].day - 1), calendarOrdered[i].Index);
            Assert.AreEqual(expectedOrdered[i].magnitude, calendarOrdered[i].Value, 1E-12);
        }

        model.TimeBlock = TimeBlockWindow.WaterYear;
        model.StartMonth = 10;
        model.SetParameterValues(new[]
        {
            changePointOne + 0.75,
            changePointTwo + 0.25,
            gevLocationOne,
            gevScaleOne,
            gpaKappaOne,
            gevLocationTwo,
            gevScaleTwo,
            gpaKappaTwo
        });
        Numerics.Data.TimeSeries waterYear = model.GeneratePOTTimeSeries(sampleSize, seed);
        var waterYearOrdered = waterYear.OrderBy(point => point.Index).ThenBy(point => point.Value).ToArray();
        for (int i = 0; i < sampleSize; i++)
        {
            DateTime waterYearStart = new DateTime(1999 + 4 * expectedOrdered[i].block, 10, 1);
            Assert.AreEqual(waterYearStart.AddDays(expectedOrdered[i].day - 1), waterYearOrdered[i].Index);
            Assert.AreEqual(expectedOrdered[i].magnitude, waterYearOrdered[i].Value, 1E-12);
        }
    }

    #endregion

    #region Quantile Prior Tests

    /// <summary>Verifies that set default quantile priors single quantile.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_SingleQuantile()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.EnableQuantilePriors = true;
        model.UseSingleQuantile = true;

        model.SetDefaultQuantilePriors();

        Assert.AreEqual(1, model.QuantilePriors.Count);
    }

    /// <summary>Verifies that set default quantile priors three quantiles single GEV.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_ThreeQuantiles_SingleGEV()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.EnableQuantilePriors = true;
        model.UseSingleQuantile = false;

        model.SetDefaultQuantilePriors();

        // Single GEV allows 3 quantile priors (one per parameter)
        Assert.AreEqual(3, model.QuantilePriors.Count);
    }

    /// <summary>Verifies that set default quantile priors disabled empty list.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_Disabled_EmptyList()
    {
        var df = CreateAMSDataFrame();
        var model = new PointProcessModel();
        model.DataFrame = df;
        model.EnableQuantilePriors = false;

        model.SetDefaultQuantilePriors();

        Assert.AreEqual(0, model.QuantilePriors.Count);
    }

    #endregion

    #region Pointwise vs Scalar Prior Sum-Equality (CON-4)

    /// <summary>
    /// Verifies the canonical model-base contract that
    /// <c>PointProcessModel.PointwisePriorLogLikelihood</c>.Sum() equals
    /// <c>PointProcessModel.PriorLogLikelihood</c>. Non-seasonal configuration.
    /// </summary>
    /// <remarks>
    /// Per the project coding standards "LogLikelihood vs DataLogLikelihood (CRITICAL)" the canonical contract is
    /// pointwise.Sum(c =&gt; c.LogLikelihood) == scalar prior. Disable Jeffreys-rule scaling so
    /// the two methods evaluate identical prior contributions (PointwisePriorLogLikelihood
    /// excludes the Jeffreys term to match ModelBase.PriorLogLikelihood semantics).
    /// </remarks>
    [TestMethod]
    public void Test_PointwisePriorLogLikelihood_Sum_Equals_PriorLogLikelihood_NonSeasonal()
    {
        var df = CreatePOTDataFrame();
        var model = new PointProcessModel { DataFrame = df, UseJeffreysRuleForScale = false };
        model.SetDefaultParameters();

        var p = model.Parameters.Select(x => x.Value).ToArray();
        double scalar = model.PriorLogLikelihood(p);
        double pointwiseSum = model.PointwisePriorLogLikelihood(p).Sum(c => c.LogLikelihood);

        Assert.AreEqual(scalar, pointwiseSum, 1e-9,
            $"Pointwise sum {pointwiseSum} disagreed with scalar prior {scalar}.");
    }

    /// <summary>
    /// Same canonical contract as <c>Test_PointwisePriorLogLikelihood_Sum_Equals_PriorLogLikelihood_NonSeasonal</c>,
    /// but with a 2-season seasonal configuration to cover the seasonal parameter slicing path.
    /// </summary>
    [TestMethod]
    public void Test_PointwisePriorLogLikelihood_Sum_Equals_PriorLogLikelihood_Seasonal()
    {
        var df = CreateSeasonalPOTDataFrame();
        var model = new PointProcessModel
        { DataFrame = df,
            UseJeffreysRuleForScale = false,
            IsSeasonal = true
        };
        model.SetDefaultParameters();

        var p = model.Parameters.Select(x => x.Value).ToArray();
        double scalar = model.PriorLogLikelihood(p);
        double pointwiseSum = model.PointwisePriorLogLikelihood(p).Sum(c => c.LogLikelihood);

        Assert.AreEqual(scalar, pointwiseSum, 1e-9,
            $"Seasonal pointwise sum {pointwiseSum} disagreed with scalar prior {scalar}.");
    }

    #endregion
}
