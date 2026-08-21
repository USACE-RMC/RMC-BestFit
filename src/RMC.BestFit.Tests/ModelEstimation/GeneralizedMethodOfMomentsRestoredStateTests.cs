using System.Globalization;
using System.Xml.Linq;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies the GMM estimator's J-statistic scope rule on restore, its post-processing on a restored
/// estimate, and the independence of the estimator state from covariance queries. Estimated states
/// are injected through XML so no optimizer runs.
/// </summary>
[TestClass]
public class GeneralizedMethodOfMomentsRestoredStateTests
{
    /// <summary>
    /// Two moment conditions in one parameter with a parameter-dependent moment covariance.
    /// </summary>
    /// <param name="parameters">Parameter vector.</param>
    /// <returns>The moment vector and its covariance.</returns>
    private static (Vector G, Matrix S) OverIdentifiedMomentConditions(double[] parameters)
    {
        double theta = parameters[0];
        var g = new Vector(new[] { theta - 0.3, theta * theta - 0.2 });
        var s = new Matrix(2, 2);
        s[0, 0] = 1.0 + theta * theta;
        s[1, 1] = 2.0 + theta * theta;
        return (g, s);
    }

    /// <summary>
    /// One moment condition in one parameter.
    /// </summary>
    /// <param name="parameters">Parameter vector.</param>
    /// <returns>The moment vector and its covariance.</returns>
    private static (Vector G, Matrix S) JustIdentifiedMomentConditions(double[] parameters)
    {
        double theta = parameters[0];
        var g = new Vector(new[] { theta - 0.3 });
        var s = new Matrix(1, 1);
        s[0, 0] = 1.0 + theta * theta;
        return (g, s);
    }

    /// <summary>
    /// Creates an estimator for the requested strategy and moment count.
    /// </summary>
    /// <param name="strategy">Estimation strategy.</param>
    /// <param name="overIdentified">True for two moment conditions; false for one.</param>
    /// <returns>The estimator.</returns>
    private static GeneralizedMethodOfMoments MakeGmm(
        GeneralizedMethodOfMoments.GMMEstimationStrategy strategy,
        bool overIdentified = true)
    {
        return new GeneralizedMethodOfMoments(
            momentConditionFunction: overIdentified ? OverIdentifiedMomentConditions : JustIdentifiedMomentConditions,
            numberOfParameters: 1,
            numberOfMomentConditions: overIdentified ? 2 : 1,
            sampleSize: 50,
            initialValues: [0.4],
            lowerBounds: [-5.0],
            upperBounds: [5.0])
        {
            EstimationStrategy = strategy
        };
    }

    /// <summary>
    /// Builds the XML of an estimated state: the estimate, the weighting matrix and its inverse
    /// moment covariance, and the stored J-statistic values.
    /// </summary>
    /// <param name="gmm">Estimator whose configuration is serialized.</param>
    /// <param name="theta">Stored estimate.</param>
    /// <param name="weighting">Stored weighting matrix.</param>
    /// <param name="jStat">Stored J-statistic.</param>
    /// <param name="jStatPval">Stored J-statistic p-value.</param>
    /// <returns>The XML element.</returns>
    private static XElement EstimatedStateXml(
        GeneralizedMethodOfMoments gmm,
        double theta,
        Matrix weighting,
        double jStat,
        double jStatPval)
    {
        XElement xml = gmm.ToXElement();
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.JStat), jStat.ToString("G17", CultureInfo.InvariantCulture));
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.JStatPval), jStatPval.ToString("G17", CultureInfo.InvariantCulture));
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.GMMIterations), 2);
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.Status), nameof(OptimizationStatus.Success));

        xml.Element(nameof(GeneralizedMethodOfMoments.BestParameterSet))?.Remove();
        xml.Add(new XElement(nameof(GeneralizedMethodOfMoments.BestParameterSet), new ParameterSet([theta], 0d).ToXElement()));
        xml.Element(nameof(GeneralizedMethodOfMoments.W))?.Remove();
        xml.Add(new XElement(nameof(GeneralizedMethodOfMoments.W), weighting.ToXElement()));
        xml.Element(nameof(GeneralizedMethodOfMoments.S))?.Remove();
        xml.Add(new XElement(nameof(GeneralizedMethodOfMoments.S), weighting.Inverse().ToXElement()));
        return xml;
    }

    /// <summary>
    /// Files written before the J-statistic scope rule stored 0 for fits without a Hansen chi-square
    /// interpretation; such values restore as NaN, while an in-scope statistic restores unchanged.
    /// </summary>
    [TestMethod]
    public void RestoreFromXElement_AppliesJStatisticScopeRule()
    {
        var oneStep = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep);
        oneStep.RestoreFromXElement(EstimatedStateXml(oneStep, 0.4, Matrix.Identity(2), jStat: 0.0, jStatPval: 1.0));
        Assert.IsTrue(double.IsNaN(oneStep.JStat), "A one-step fit has no Hansen J interpretation.");
        Assert.IsTrue(double.IsNaN(oneStep.JStatPval));

        var justIdentified = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative, overIdentified: false);
        justIdentified.RestoreFromXElement(EstimatedStateXml(justIdentified, 0.4, Matrix.Identity(1), jStat: 0.0, jStatPval: 1.0));
        Assert.IsTrue(double.IsNaN(justIdentified.JStat), "A just-identified fit has no overidentifying restrictions.");
        Assert.IsTrue(double.IsNaN(justIdentified.JStatPval));

        var twoStep = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep);
        twoStep.RestoreFromXElement(EstimatedStateXml(twoStep, 0.4, Matrix.Identity(2), jStat: 2.5, jStatPval: 0.11));
        Assert.AreEqual(2.5, twoStep.JStat, 0.0);
        Assert.AreEqual(0.11, twoStep.JStatPval, 0.0);
    }

    /// <summary>
    /// Post-processing a restored estimator computes the covariance and keeps the restored J-statistic,
    /// because the selected-weight objective of the original run is not available to recompute it.
    /// </summary>
    [TestMethod]
    public void PostProcess_OnRestoredEstimator_ComputesCovariance_AndKeepsRestoredJStatistic()
    {
        var gmm = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep);
        gmm.RestoreFromXElement(EstimatedStateXml(gmm, 0.4, Matrix.Identity(2), jStat: 2.5, jStatPval: 0.11));

        gmm.PostProcess(useSandwich: true, computeJstat: true);

        Assert.IsNotNull(gmm.Sigma);
        Assert.IsTrue(gmm.Sigma![0, 0] > 0.0 && double.IsFinite(gmm.Sigma[0, 0]));
        Assert.AreEqual(2.5, gmm.JStat, 0.0);
        Assert.AreEqual(0.11, gmm.JStatPval, 0.0);
    }

    /// <summary>
    /// Post-processing refreshes the moment covariance and weighting matrix at the estimate before the
    /// covariance is computed.
    /// </summary>
    [TestMethod]
    public void PostProcess_RefreshesWeightingAtTheEstimate()
    {
        var gmm = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative);
        gmm.RestoreFromXElement(EstimatedStateXml(gmm, 0.4, Matrix.Identity(2), double.NaN, double.NaN));

        gmm.PostProcess();

        Assert.AreEqual(1.0 + 0.16, gmm.S![0, 0], 1e-9);
        Assert.AreEqual(2.0 + 0.16, gmm.S[1, 1], 1e-9);
        Assert.AreEqual(1.0 / 1.16, gmm.W![0, 0], 1e-9);
        Assert.AreEqual(1.0 / 2.16, gmm.W[1, 1], 1e-9);
    }

    /// <summary>
    /// Querying the covariance at another parameter vector leaves the selected weighting matrix, and
    /// therefore the moment objective, unchanged.
    /// </summary>
    [TestMethod]
    public void TryGetCovariance_AtAnotherVector_LeavesObjectiveAndWeightingUnchanged()
    {
        var gmm = MakeGmm(GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep);
        gmm.RestoreFromXElement(EstimatedStateXml(gmm, 0.4, Matrix.Identity(2), double.NaN, double.NaN));
        double[] theta = [0.5];
        double objectiveBefore = gmm.Q(theta);
        Matrix weightingBefore = gmm.W!.Clone();

        Assert.IsTrue(gmm.TryGetCovariance([0.9], sandwich: true, out Matrix covariance));

        Assert.IsTrue(double.IsFinite(covariance[0, 0]) && covariance[0, 0] > 0.0);
        Assert.AreEqual(objectiveBefore, gmm.Q(theta), 0.0);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
                Assert.AreEqual(weightingBefore[row, column], gmm.W[row, column], 0.0);
        }
    }
}
