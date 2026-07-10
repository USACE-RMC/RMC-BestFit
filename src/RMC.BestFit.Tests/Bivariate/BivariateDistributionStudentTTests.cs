using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Structural unit tests for the 2-parameter Student's t copula path through
/// <c>BivariateDistribution</c>. Validates that the parameter-count
/// generalization (factory case, SetDefaultParameters loop, likelihood guards,
/// SetParameterValues fan-out, XElement round-trip, and Validate) behaves
/// correctly for a copula with <c>NumberOfCopulaParameters == 2</c>.
/// </summary>
/// <remarks>
/// Numerical recovery (MCMC fits truth) lives in
/// <c>RMC.BestFit.Verification/Bivariate/BivariateAnalysisParameterRecoveryTests.cs</c>.
/// </remarks>
[TestClass]
public class BivariateDistributionStudentTTests
{
    #region Test Helpers

    /// <summary>
    /// Builds a pair of Normal-marginal UnivariateDistributions seeded with fixed
    /// parameter values (no MLE call) so <c>UnivariateDistribution.Validate</c>
    /// passes and <c>BivariateDistribution.SetDefaultParameters</c> can run.
    /// </summary>
    private static (UnivariateDistribution X, UnivariateDistribution Y) CreateFittedMarginals()
    {
        double[] xValues = { 98.1, 102.7, 115.3, 88.4, 104.9, 92.0, 110.5, 99.2, 107.6, 101.3,
                             95.8, 108.1, 103.4, 97.6, 112.9, 89.5, 106.2, 100.0, 93.7, 109.4 };
        double[] yValues = { 75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81.0,
                             74.1, 87.6, 82.8, 76.3, 91.5, 69.2, 86.0, 80.4, 73.5, 89.1 };

        var dfX = new BestFitDataFrame();
        dfX.ExactSeries = new ExactSeries(xValues);
        dfX.CalculatePlottingPositions();
        var distX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);
        distX.SetParameterValues(new[] { 101.8, 7.5 });

        var dfY = new BestFitDataFrame();
        dfY.ExactSeries = new ExactSeries(yValues);
        dfY.CalculatePlottingPositions();
        var distY = new UnivariateDistribution(dfY, UnivariateDistributionType.Normal);
        distY.SetParameterValues(new[] { 80.8, 7.5 });

        // ExactData carries an Index. To allow BivariateDistribution.SetSampleData to
        // match pairs, make indices match between the two series.
        for (int i = 0; i < xValues.Length; i++)
        {
            ((ExactData)dfX.ExactSeries[i]).Index = i;
            ((ExactData)dfY.ExactSeries[i]).Index = i;
        }

        return (distX, distY);
    }

    #endregion

    #region Factory Tests

    /// <summary>
    /// Verifies the factory produces a <c>StudentTCopula</c> for
    /// <c>CopulaType.StudentT</c>, not a placeholder or exception.
    /// </summary>
    [TestMethod]
    public void CreateCopula_StudentT_ReturnsStudentTCopula()
    {
        var copula = BivariateDistribution.CreateCopula(CopulaType.StudentT);

        Assert.IsNotNull(copula);
        Assert.IsInstanceOfType(copula, typeof(StudentTCopula));
        Assert.AreEqual(CopulaType.StudentT, copula.Type);
        Assert.AreEqual(2, copula.NumberOfCopulaParameters);
    }

    #endregion

    #region CopulaType Switching

    /// <summary>
    /// Switching the CopulaType property to StudentT replaces the Copula with
    /// a StudentTCopula instance, validating the factory wire-up through the setter.
    /// </summary>
    [TestMethod]
    public void CopulaType_SwitchToStudentT_ReplacesCopula()
    {
        var dist = new BivariateDistribution();  // defaults to Normal
        Assert.IsInstanceOfType(dist.Copula, typeof(NormalCopula));

        dist.CopulaType = CopulaType.StudentT;

        Assert.IsInstanceOfType(dist.Copula, typeof(StudentTCopula));
        Assert.AreEqual(CopulaType.StudentT, dist.CopulaType);
    }

    /// <summary>
    /// Switching from Normal to StudentT (with valid marginals) rebuilds Parameters
    /// from 1 entry (Theta) to 2 entries (Theta, DegreesOfFreedom).
    /// </summary>
    [TestMethod]
    public void CopulaType_SwitchNormalToStudentT_RebuildsParametersTwoEntries()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.Normal);

        Assert.AreEqual(1, dist.Parameters.Count, "Normal copula should have 1 parameter.");
        Assert.AreEqual("Dependency (θ)", dist.Parameters[0].Name);

        dist.CopulaType = CopulaType.StudentT;

        Assert.AreEqual(2, dist.Parameters.Count, "Student's t copula should have 2 parameters.");
        Assert.AreEqual("Dependency (θ)", dist.Parameters[0].Name);
        Assert.AreEqual("DegreesOfFreedom", dist.Parameters[1].Name);
    }

    /// <summary>
    /// Switching back from StudentT to Normal rebuilds Parameters from 2 entries to 1 entry.
    /// </summary>
    [TestMethod]
    public void CopulaType_SwitchStudentTToNormal_RebuildsParametersOneEntry()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        Assert.AreEqual(2, dist.Parameters.Count);

        dist.CopulaType = CopulaType.Normal;

        Assert.AreEqual(1, dist.Parameters.Count);
        Assert.AreEqual("Dependency (θ)", dist.Parameters[0].Name);
    }

    #endregion

    #region Default Parameter Layout

    /// <summary>
    /// SetDefaultParameters on a StudentT model produces two ModelParameters whose
    /// bounds match <c>BivariateCopula.ParameterConstraints</c> row by row,
    /// with a default degrees-of-freedom value (5) strictly inside the [3, 30] range.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_StudentT_ProducesTwoParametersWithCorrectBoundsAndInitialValues()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        Assert.AreEqual(2, dist.Parameters.Count);

        var theta = dist.Parameters[0];
        var df    = dist.Parameters[1];

        Assert.AreEqual("Dependency (θ)",   theta.Name);
        Assert.AreEqual("DegreesOfFreedom", df.Name);

        // StudentTCopula: theta ∈ [-1+ε, 1-ε], df strictly above 2 (so variance exists),
        // upper bound = 30 (typical practical limit).
        Assert.IsTrue(theta.LowerBound > -1.0 && theta.LowerBound < 0.0);
        Assert.IsTrue(theta.UpperBound <  1.0 && theta.UpperBound > 0.0);
        Assert.IsTrue(df.LowerBound > 2.0 && df.LowerBound < 3.0,
            $"df.LowerBound should be just above 2 (current: {df.LowerBound}).");
        Assert.AreEqual(30.0, df.UpperBound, 1E-9);

        // Initial values are inside bounds and sensible defaults.
        Assert.IsTrue(theta.Value >= theta.LowerBound && theta.Value <= theta.UpperBound);
        Assert.AreEqual(5.0, df.Value, 1E-9, "DegreesOfFreedom should default to 5.");

        // Each parameter carries a uniform prior matching its bounds.
        Assert.IsNotNull(theta.PriorDistribution);
        Assert.IsNotNull(df.PriorDistribution);
        Assert.IsTrue(theta.PriorDistribution.ParametersValid);
        Assert.IsTrue(df.PriorDistribution.ParametersValid);
    }

    #endregion

    #region SetParameterValues Fan-out

    /// <summary>
    /// SetParameterValues on a StudentT model must update both the internal
    /// ModelParameter.Value array AND the underlying Copula's rho (Theta) and
    /// degrees-of-freedom — not just Theta. This guards the fix for the old
    /// hardcoded <c>Copula.Theta = parameters[0];</c> which silently ignored df.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_StudentT_UpdatesBothThetaAndDegreesOfFreedom()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        dist.SetParameterValues(new[] { 0.6, 8.0 });

        Assert.AreEqual(0.6, dist.Parameters[0].Value, 1E-9);
        Assert.AreEqual(8.0, dist.Parameters[1].Value, 1E-9);
        Assert.AreEqual(0.6, dist.Copula.Theta, 1E-9);
        Assert.AreEqual(8.0, ((StudentTCopula)dist.Copula).DegreesOfFreedom, 1E-9);
    }

    /// <summary>
    /// ν is stored as a continuous <c>double</c> — non-integer values are preserved
    /// exactly rather than rounded. This guards against the legacy behavior that rounded ν
    /// to an integer inside <c>SetCopulaParameters</c>, which produced step-function
    /// likelihood surfaces during MCMC and unnecessary posterior plateaus.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_StudentT_NonIntegerDf_IsPreserved()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        dist.SetParameterValues(new[] { 0.5, 7.25 });

        Assert.AreEqual(7.25, dist.Parameters[1].Value, 1E-12);
        Assert.AreEqual(7.25, ((StudentTCopula)dist.Copula).DegreesOfFreedom, 1E-12);
    }

    #endregion

    #region Likelihood Guards

    /// <summary>
    /// DataLogLikelihood rejects a parameter array whose length does not match
    /// <c>Copula.NumberOfCopulaParameters</c> (2 for StudentT). Passing a 1-element
    /// array mimics the old hardcoded shape and must return <c>double.MinValue</c>
    /// without crashing. Returns <c>double.NegativeInfinity</c> per the
    /// CLAUDE.md numerical pattern for impossible log-likelihood.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_StudentT_WrongParameterCount_ReturnsNegativeInfinity()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        double ll = dist.DataLogLikelihood(new[] { 0.5 });   // too few — StudentT needs 2

        Assert.AreEqual(double.NegativeInfinity, ll);
    }

    /// <summary>
    /// DataLogLikelihood with the correct parameter count (2) returns a finite value
    /// for a StudentT model with valid sample data.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_StudentT_CorrectParameterCount_ReturnsFinite()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        double ll = dist.DataLogLikelihood(new[] { 0.5, 5.0 });

        Assert.IsFalse(double.IsNaN(ll));
        Assert.IsFalse(double.IsInfinity(ll));
        Assert.AreNotEqual(double.MinValue, ll);
    }

    #endregion

    #region Validation

    /// <summary>
    /// A well-configured StudentT model validates successfully with 2 parameters.
    /// </summary>
    [TestMethod]
    public void Validate_StudentT_WellConfigured_IsValid()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        var (isValid, messages) = dist.Validate();

        Assert.IsTrue(isValid, "Validate failed: " + string.Join("; ", messages));
    }

    /// <summary>
    /// If the Parameters list count does not match the copula's expected parameter count,
    /// Validate reports invalid. Guards against stale Parameters after manual mutation
    /// or a partial deserialization.
    /// </summary>
    [TestMethod]
    public void Validate_StudentT_WrongParameterCount_IsInvalid()
    {
        var (x, y) = CreateFittedMarginals();
        var dist = new BivariateDistribution(x, y, CopulaType.StudentT);

        // Remove the second parameter to simulate a stale 1-parameter configuration
        // while the copula is still a 2-parameter StudentT.
        dist.Parameters.RemoveAt(1);

        var (isValid, messages) = dist.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Exists(m => m.Contains("unexpected size")),
            "Expected a parameter-size validation error; got: " + string.Join("; ", messages));
    }

    #endregion

    #region XElement Round-trip

    /// <summary>
    /// ToXElement / XElement-constructor round-trip preserves both parameters
    /// of a StudentT model — both the ModelParameter.Value array AND the underlying
    /// copula's rho/df are restored.
    /// </summary>
    [TestMethod]
    public void XElementRoundTrip_StudentT_PreservesBothParameters()
    {
        var (x, y) = CreateFittedMarginals();
        var original = new BivariateDistribution(x, y, CopulaType.StudentT);
        original.SetParameterValues(new[] { 0.65, 7.0 });

        XElement xml = original.ToXElement();
        var restored = new BivariateDistribution(x, y, xml);

        Assert.AreEqual(CopulaType.StudentT, restored.CopulaType);
        Assert.AreEqual(2, restored.Parameters.Count);
        Assert.AreEqual(0.65, restored.Parameters[0].Value, 1E-9);
        Assert.AreEqual(7.0,  restored.Parameters[1].Value, 1E-9);
        Assert.AreEqual(0.65, restored.Copula.Theta, 1E-9);
        Assert.AreEqual(7.0,  ((StudentTCopula)restored.Copula).DegreesOfFreedom, 1E-9);
    }

    #endregion
}
