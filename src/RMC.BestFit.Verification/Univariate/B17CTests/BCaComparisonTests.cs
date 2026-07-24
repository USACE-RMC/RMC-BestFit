using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Verifies Bulletin 17C BCa interval calculations against linked multivariate-normal uncertainty results.
/// </summary>
[TestClass]
public class BCaComparisonTests
{
    /// <summary>
    /// Verifies <c>LogNormal_LinkedMVM_BCa_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogNormal_LinkedMVM_BCa_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    /// <summary>
    /// Verifies <c>LogNormal_LinkedMVM_BCa_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogNormal_LinkedMVM_BCa_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    /// <summary>
    /// Verifies <c>LogNormal_LinkedMVM_BCa_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogNormal_LinkedMVM_BCa_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    /// <summary>
    /// Verifies <c>LogNormal_LinkedMVM_BCa_N200</c>.
    /// </summary>
    [TestMethod]
    public async Task LogNormal_LinkedMVM_BCa_N200()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 200);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    #region LogPearsonTypeIII - N=25


    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -1.0, n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist)
        {
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
        };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -0.5, n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist)
        {
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
        };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_00_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_00_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.0, n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist)
        {
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
        };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}
    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.5, n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist)
        {
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
        };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);


        //}
    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N25</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 1.0, n: 25);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist)
        {
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
        };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}

    }

    #endregion

    #region LogPearsonTypeIII - N=50


    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -1.0, n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs!.UpperCI![i];
            var lower = emaCIs!.LowerCI![i];
            Debug.WriteLine(upper + ", " + lower);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -0.5, n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_00_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_00_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.0, n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs!.UpperCI![i];
            var lower = emaCIs!.LowerCI![i];
            Debug.WriteLine(upper + ", " + lower);


        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.5, n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N50</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N50()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 1.0, n: 50);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

    }

    #endregion

    #region LogPearsonTypeIII - N=100

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus1_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -1.0, n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs!.UpperCI![i];
            var lower = emaCIs!.LowerCI![i];
            Debug.WriteLine(upper + ", " + lower);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Minus05_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: -0.5, n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs!.UpperCI![i];
            var lower = emaCIs!.LowerCI![i];
            Debug.WriteLine(upper + ", " + lower);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_0_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_0_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.0, n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs!.UpperCI![i];
            var lower = emaCIs!.LowerCI![i];
            Debug.WriteLine(upper + ", " + lower);

        }

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus05_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 0.5, n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}

    }

    /// <summary>
    /// Verifies <c>LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N100</c>.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinkedMVM_BCa_Skew_Plus1_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(gamma: 1.0, n: 100);

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            var lower = b17CAnalysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            var mean = b17CAnalysis.AnalysisResults!.MeanCurve![i];
            var mode = b17CAnalysis.AnalysisResults!.ModeCurve![i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        //var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        //for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        //{
        //    var upper = emaCIs!.UpperCI![i];
        //    var lower = emaCIs!.LowerCI![i];
        //    Debug.WriteLine(upper + ", " + lower);

        //}

    }

    #endregion

    #region LogPearsonTypeIII - N=200
    #endregion






}
