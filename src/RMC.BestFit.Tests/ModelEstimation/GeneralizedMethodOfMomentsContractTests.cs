using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast validation, clone, default-option, and diagnostic-guard contracts for
/// <see cref="GeneralizedMethodOfMoments"/>.
/// </summary>
[TestClass]
public class GeneralizedMethodOfMomentsContractTests
{
    /// <summary>Valid configuration has no validation errors.</summary>
    [TestMethod]
    public void IsValid_ValidConfiguration_ReturnsTrue()
    {
        var gmm = MakeGmm();

        bool valid = gmm.IsValid(out var errors);

        Assert.IsTrue(valid);
        Assert.AreEqual(0, errors.Count);
    }

    /// <summary>Under-identification is rejected by validation.</summary>
    [TestMethod]
    public void IsValid_UnderIdentified_ReturnsFalse()
    {
        var gmm = MakeGmm(parameters: 2, moments: 1);

        bool valid = gmm.IsValid(out var errors);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(error => error.Contains("under-identified")));
    }

    /// <summary>An absolute tolerance below the supported floor is invalid.</summary>
    [TestMethod]
    public void IsValid_InvalidAbsoluteTolerance_ReturnsFalse()
    {
        var gmm = MakeGmm();
        gmm.AbsoluteTolerance = 1e-20;

        bool valid = gmm.IsValid(out var errors);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(error => error.Contains("tolerance")));
    }

    /// <summary>A relative tolerance below the supported floor is invalid.</summary>
    [TestMethod]
    public void IsValid_InvalidRelativeTolerance_ReturnsFalse()
    {
        var gmm = MakeGmm();
        gmm.RelativeTolerance = 1e-20;

        bool valid = gmm.IsValid(out var errors);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(error => error.Contains("tolerance")));
    }

    /// <summary>Cloning preserves the pointwise-moment delegate and detaches arrays.</summary>
    [TestMethod]
    public void Clone_PreservesPointwiseMomentConditionsAndDetachesArrays()
    {
        PointwiseMomentConditionFunction pointwise = _ => new double[,] { { 0d } };
        var original = MakeGmm(pointwiseMomentConditions: pointwise);

        GeneralizedMethodOfMoments clone = original.Clone();
        original.InitialValues[0] = 0.75;

        Assert.IsNotNull(clone.PointwiseMomentConditions);
        Assert.AreEqual(0.5, clone.InitialValues[0], 0d);
    }

    /// <summary>The model constructor consumes bounds and optional delegates from the model.</summary>
    [TestMethod]
    public void ModelConstructor_ReadsBoundsAndOptionalDelegates()
    {
        var model = new StubGmmModel(includePointwise: true);

        var gmm = new GeneralizedMethodOfMoments(model);

        CollectionAssert.AreEqual(new[] { -10d }, gmm.LowerBounds);
        CollectionAssert.AreEqual(new[] { 10d }, gmm.UpperBounds);
        Assert.IsNotNull(gmm.PointwiseMomentConditions);
        Assert.IsNull(gmm.JacobianFunction);
        Assert.IsNull(gmm.PenaltyFunction);
    }

    /// <summary>Standard errors reject pre-estimation access.</summary>
    [TestMethod]
    public void GetStandardErrors_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().GetStandardErrors());
    }

    /// <summary>Covariance rejects pre-estimation access.</summary>
    [TestMethod]
    public void GetCovarianceMatrix_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().GetCovarianceMatrix());
    }

    /// <summary>Observation influence rejects pre-estimation access.</summary>
    [TestMethod]
    public void GetObservationInfluence_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().GetObservationInfluence());
    }

    /// <summary>Cook-like influence rejects pre-estimation access.</summary>
    [TestMethod]
    public void GetCooksDistance_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().GetCooksDistance());
    }

    /// <summary>Profile-Q rejects pre-estimation access.</summary>
    [TestMethod]
    public void ProfileQ_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().ProfileQ());
    }

    /// <summary>Profile percentiles reject pre-estimation access.</summary>
    [TestMethod]
    public void ProfilePercentiles_BeforeEstimation_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MakeGmm().ProfilePercentiles());
    }

    /// <summary>Observation influence reports the missing pointwise-moment prerequisite.</summary>
    [TestMethod]
    public void GetObservationInfluence_WithoutPointwiseMoments_Throws()
    {
        var gmm = RestoreEstimatedGmmWithoutPointwiseMoments();

        var exception = Assert.ThrowsException<InvalidOperationException>(() => gmm.GetObservationInfluence());

        StringAssert.Contains(exception.Message, "pointwise moment conditions");
    }

    /// <summary>Cook-like influence reports the missing pointwise-moment prerequisite.</summary>
    [TestMethod]
    public void GetCooksDistance_WithoutPointwiseMoments_Throws()
    {
        var gmm = RestoreEstimatedGmmWithoutPointwiseMoments();

        var exception = Assert.ThrowsException<InvalidOperationException>(() => gmm.GetCooksDistance());

        StringAssert.Contains(exception.Message, "pointwise moment conditions");
    }

    /// <summary>Default options restore the documented optimizer and tolerance configuration.</summary>
    [TestMethod]
    public void SetDefaultOptions_RestoresDefaults()
    {
        var gmm = MakeGmm();
        gmm.OptimizerMethod = OptimizationMethod.NelderMead;
        gmm.UseFallbackOptimizer = false;
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.MaxGMMIterations = 5;
        gmm.MaxFunctionEvaluations = 10;
        gmm.AbsoluteTolerance = 0.5;
        gmm.RelativeTolerance = 0.5;

        gmm.SetDefaultOptions();

        Assert.AreEqual(OptimizationMethod.BFGS, gmm.OptimizerMethod);
        Assert.IsTrue(gmm.UseFallbackOptimizer);
        Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative, gmm.EstimationStrategy);
        Assert.AreEqual(100, gmm.MaxGMMIterations);
        Assert.AreEqual(2000, gmm.MaxFunctionEvaluations);
        Assert.AreEqual(1e-8, gmm.AbsoluteTolerance, 0d);
        Assert.AreEqual(1e-8, gmm.RelativeTolerance, 0d);
    }

    /// <summary>
    /// Creates a delegate-backed unestimated GMM fixture.
    /// </summary>
    /// <param name="parameters">The parameter count.</param>
    /// <param name="moments">The moment-condition count.</param>
    /// <param name="pointwiseMomentConditions">Optional pointwise moment function.</param>
    /// <returns>The configured estimator.</returns>
    private static GeneralizedMethodOfMoments MakeGmm(
        int parameters = 1,
        int moments = 1,
        PointwiseMomentConditionFunction? pointwiseMomentConditions = null)
    {
        MomentConditionFunction momentFunction = _ =>
            (new Vector(moments), Matrix.Identity(moments));
        return new GeneralizedMethodOfMoments(
            momentFunction,
            parameters,
            moments,
            sampleSize: 20,
            initialValues: Enumerable.Repeat(0.5, parameters).ToArray(),
            lowerBounds: Enumerable.Repeat(0.0, parameters).ToArray(),
            upperBounds: Enumerable.Repeat(1.0, parameters).ToArray(),
            pointwiseMomentConditions: pointwiseMomentConditions);
    }

    /// <summary>
    /// Restores only the public estimated-state marker so prerequisite guards can be
    /// tested without running an optimizer.
    /// </summary>
    /// <returns>An estimated-state fixture with no pointwise-moment delegate.</returns>
    private static GeneralizedMethodOfMoments RestoreEstimatedGmmWithoutPointwiseMoments()
    {
        var gmm = MakeGmm();
        XElement element = gmm.ToXElement();
        element.SetAttributeValue(nameof(GeneralizedMethodOfMoments.Status), OptimizationStatus.Success);
        gmm.RestoreFromXElement(element);
        Assert.IsTrue(gmm.IsEstimated);
        return gmm;
    }

    /// <summary>
    /// Minimal model-constructor fixture with explicit bounds and optional pointwise moments.
    /// </summary>
    private sealed class StubGmmModel : IGMMModel
    {
        /// <summary>Initializes the model fixture.</summary>
        /// <param name="includePointwise">Whether to expose pointwise moment conditions.</param>
        public StubGmmModel(bool includePointwise)
        {
            Parameters =
            [
                new ModelParameter("Fixture", "Mean", 0d, -10d, 10d, new Uniform(-10d, 10d)),
            ];
            PointwiseMomentConditions = includePointwise ? _ => new double[,] { { 0d } } : null;
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters { get; }

        /// <inheritdoc/>
        public int NumberOfParameters => 1;

        /// <inheritdoc/>
        public int NumberOfMomentConditions => 1;

        /// <inheritdoc/>
        public int SampleSize => 20;

        /// <inheritdoc/>
        public MomentConditionFunction MomentConditionFunction => _ => (new Vector(1), Matrix.Identity(1));

        /// <inheritdoc/>
        public JacobianFunction? JacobianFunction => null;

        /// <inheritdoc/>
        public PenaltyFunction? PenaltyFunction => null;

        /// <inheritdoc/>
        public PointwiseMomentConditionFunction? PointwiseMomentConditions { get; }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters)
        {
            Parameters[0].Value = parameters[0];
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parameters)));
        }

        /// <inheritdoc/>
        public void SetDefaultParameters()
        {
            Parameters[0].Value = 0d;
        }

        /// <inheritdoc/>
        public IGMMModel Clone() => new StubGmmModel(PointwiseMomentConditions != null);

        /// <inheritdoc/>
        public XElement ToXElement() => new("StubGmmModel");

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());
    }
}
