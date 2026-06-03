using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Estimation;

/// <summary>
/// Fast structural unit tests for the <see cref="GeneralizedMethodOfMoments"/> class.
/// </summary>
/// <remarks>
/// Real estimation runs (Bulletin 17C published-table parity, Cohn-style confidence
/// intervals, sandwich-variance verification) live in <c>RMC.BestFit.Verification</c>.
/// These tests cover the configuration / state surface only.
/// </remarks>
[TestClass]
public class GeneralizedMethodOfMomentsTests
{
    /// <summary>
    /// Trivial moment-condition function used by the delegate-based constructor tests.
    /// Returns a zero G vector and zero S matrix sized to the requested counts; sufficient
    /// for exercising configuration paths without actually solving.
    /// </summary>
    private static (Vector G, Matrix S) StubMomentConditions(double[] parameters)
        => (new Vector(parameters.Length), new Matrix(parameters.Length, parameters.Length));

    #region Constructor — IGMMModel overload

    /// <summary>
    /// Verifies that the IGMMModel constructor rejects a null model with
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        _ = new GeneralizedMethodOfMoments((IGMMModel)null!);
    }

    #endregion

    #region Constructor — delegate overload

    /// <summary>
    /// Verifies that the delegate constructor populates all configuration arrays from
    /// its arguments and leaves the estimator in an unestimated state.
    /// </summary>
    [TestMethod]
    public void Test_DelegateConstructor_PopulatesShape_AndUnestimatedState()
    {
        double[] init   = [0.5, 1.0];
        double[] lower  = [0.0, 0.0];
        double[] upper  = [1.0, 2.0];

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: 2,
            numberOfMomentConditions: 3,
            sampleSize: 100,
            initialValues: init,
            lowerBounds: lower,
            upperBounds: upper);

        Assert.AreEqual(2, gmm.NumberOfParameters);
        Assert.AreEqual(3, gmm.NumberOfMomentConditions);
        Assert.AreEqual(100, gmm.SampleSize);
        CollectionAssert.AreEqual(init, gmm.InitialValues);
        CollectionAssert.AreEqual(lower, gmm.LowerBounds);
        CollectionAssert.AreEqual(upper, gmm.UpperBounds);
        Assert.IsFalse(gmm.IsEstimated);
        Assert.AreEqual(OptimizationStatus.None, gmm.Status);
    }

    /// <summary>
    /// Verifies that the delegate constructor rejects a null moment-condition function
    /// with <see cref="ArgumentNullException"/>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_DelegateConstructor_NullMomentConditionFunction_Throws()
    {
        _ = new GeneralizedMethodOfMoments(
            momentConditionFunction: null!,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 10,
            initialValues: [0.0],
            lowerBounds: [0.0],
            upperBounds: [1.0]);
    }

    /// <summary>
    /// Verifies that the delegate constructor rejects mismatched-length arrays.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_DelegateConstructor_MismatchedArrayLength_Throws()
    {
        _ = new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: 2,
            numberOfMomentConditions: 2,
            sampleSize: 10,
            initialValues: [0.0],         // length 1, but numberOfParameters = 2
            lowerBounds: [0.0, 0.0],
            upperBounds: [1.0, 1.0]);
    }

    /// <summary>
    /// Verifies that the delegate constructor rejects an upper bound less than the
    /// corresponding lower bound.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_DelegateConstructor_UpperLessThanLower_Throws()
    {
        _ = new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 10,
            initialValues: [0.5],
            lowerBounds: [1.0],
            upperBounds: [0.0]);   // upper < lower
    }

    /// <summary>
    /// Verifies that the delegate constructor rejects an initial value outside the bounds.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_DelegateConstructor_InitialOutsideBounds_Throws()
    {
        _ = new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 10,
            initialValues: [5.0],         // outside [0, 1]
            lowerBounds: [0.0],
            upperBounds: [1.0]);
    }

    #endregion

    #region Identification status

    /// <summary>
    /// p == q is exact identification: <see cref="GeneralizedMethodOfMoments.IdentificationStatus"/>
    /// must report <c>JustIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_ParametersEqualMoments_IsJustIdentified()
    {
        var gmm = MakeStubGmm(parameters: 2, moments: 2);

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.JustIdentified,
            gmm.IdentificationStatus);
    }

    /// <summary>
    /// q &gt; p is over-identification.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_MomentsGreaterThanParameters_IsOverIdentified()
    {
        var gmm = MakeStubGmm(parameters: 2, moments: 4);

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.OverIdentified,
            gmm.IdentificationStatus);
    }

    /// <summary>
    /// q &lt; p is under-identification — the model has more parameters than moment
    /// conditions and cannot be uniquely solved.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_MomentsLessThanParameters_IsUnderIdentified()
    {
        var gmm = MakeStubGmm(parameters: 3, moments: 2);

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.UnderIdentified,
            gmm.IdentificationStatus);
    }

    #endregion

    #region Configuration round-trip

    /// <summary>
    /// Verifies that <see cref="GeneralizedMethodOfMoments.OptimizerMethod"/> stores and
    /// returns the value passed to the constructor.
    /// </summary>
    [TestMethod]
    public void Test_OptimizerMethod_ReflectsConstructorChoice()
    {
        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 10,
            initialValues: [0.5],
            lowerBounds: [0.0],
            upperBounds: [1.0])
        {
            OptimizerMethod = OptimizationMethod.NelderMead
        };

        Assert.AreEqual(OptimizationMethod.NelderMead, gmm.OptimizerMethod);
    }

    /// <summary>
    /// Verifies that <see cref="GeneralizedMethodOfMoments.MaxGMMIterations"/> round-trips
    /// through the property setter.
    /// </summary>
    [TestMethod]
    public void Test_MaxGMMIterations_RoundTrips()
    {
        var gmm = MakeStubGmm(parameters: 1, moments: 1);

        gmm.MaxGMMIterations = 25;

        Assert.AreEqual(25, gmm.MaxGMMIterations);
    }

    /// <summary>
    /// Verifies <see cref="GeneralizedMethodOfMoments.ObjectiveFunctionValue"/> returns
    /// <see cref="double.NaN"/> before estimation runs (matches the MLE/MAP
    /// "NaN before estimation" sign-convention contract).
    /// </summary>
    [TestMethod]
    public void Test_ObjectiveFunctionValue_BeforeEstimation_IsNaN()
    {
        var gmm = MakeStubGmm(parameters: 1, moments: 1);

        Assert.IsTrue(double.IsNaN(gmm.ObjectiveFunctionValue));
    }

    #endregion

    /// <summary>
    /// Helper that builds a stub GMM instance with the requested
    /// (parameters, moments) shape — initial values, bounds, and sample size are
    /// fixed at sensible defaults so each test only specifies what's relevant.
    /// </summary>
    private static GeneralizedMethodOfMoments MakeStubGmm(int parameters, int moments)
    {
        var init  = Enumerable.Repeat(0.5, parameters).ToArray();
        var lower = Enumerable.Repeat(0.0, parameters).ToArray();
        var upper = Enumerable.Repeat(1.0, parameters).ToArray();

        return new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: parameters,
            numberOfMomentConditions: moments,
            sampleSize: 100,
            initialValues: init,
            lowerBounds: lower,
            upperBounds: upper);
    }
}
