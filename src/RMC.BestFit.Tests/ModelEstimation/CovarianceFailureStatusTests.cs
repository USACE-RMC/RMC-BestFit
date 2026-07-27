using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Reflection;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies the TR-027 covariance status and failure contracts without running an estimator.
/// </summary>
/// <remarks>
/// Reflection is limited to arranging deterministic post-estimation Hessians. Public covariance
/// methods then exercise their normal success, regularization, and failure paths.
/// </remarks>
[TestClass]
public sealed class CovarianceFailureStatusTests
{
    /// <summary>
    /// Confirms that MLE reports singular covariance failure and its legacy getters throw.
    /// </summary>
    [TestMethod]
    public void MaximumLikelihood_SingularHessian_ReportsFailureAndThrows()
    {
        var estimator = new MaximumLikelihood(CreateTwoParameterModel());
        ConfigureEstimatedState(estimator, new Matrix(2, 2));

        bool succeeded = estimator.TryGetCovarianceMatrix(out Matrix covariance);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(CovarianceComputationStatus.Failed, estimator.CovarianceStatus);
        Assert.IsFalse(string.IsNullOrWhiteSpace(estimator.CovarianceDiagnostic));
        AssertZeroMatrix(covariance);
        Assert.ThrowsException<InvalidOperationException>(() => estimator.GetCovarianceMatrix());
        Assert.ThrowsException<InvalidOperationException>(() => estimator.GetStandardErrors());
    }

    /// <summary>
    /// Confirms that MAP reports singular covariance failure and its legacy getters throw.
    /// </summary>
    [TestMethod]
    public void MaximumAPosteriori_SingularHessian_ReportsFailureAndThrows()
    {
        var estimator = new MaximumAPosteriori(CreateTwoParameterModel());
        ConfigureEstimatedState(estimator, new Matrix(2, 2));

        bool succeeded = estimator.TryGetCovarianceMatrix(out Matrix covariance);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(CovarianceComputationStatus.Failed, estimator.CovarianceStatus);
        Assert.IsFalse(string.IsNullOrWhiteSpace(estimator.CovarianceDiagnostic));
        AssertZeroMatrix(covariance);
        Assert.ThrowsException<InvalidOperationException>(() => estimator.GetCovarianceMatrix());
        Assert.ThrowsException<InvalidOperationException>(() => estimator.GetStandardErrors());
    }

    /// <summary>
    /// Confirms that GMM exposes failure through its public Try method and throws from its getter.
    /// </summary>
    [TestMethod]
    public void GeneralizedMethodOfMoments_MomentFailure_ReportsFailureAndThrows()
    {
        var estimator = new GeneralizedMethodOfMoments(
            _ => throw new InvalidOperationException("Forced moment-condition failure."),
            numberOfParameters: 2,
            numberOfMomentConditions: 2,
            sampleSize: 10,
            initialValues: new[] { 0.0, 0.0 },
            lowerBounds: new[] { -1.0, -1.0 },
            upperBounds: new[] { 1.0, 1.0 });

        bool succeeded = estimator.TryGetCovariance(
            new[] { 0.0, 0.0 },
            sandwich: true,
            out Matrix covariance);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(CovarianceComputationStatus.Failed, estimator.CovarianceStatus);
        Assert.IsFalse(string.IsNullOrWhiteSpace(estimator.CovarianceDiagnostic));
        AssertZeroMatrix(covariance);
        Assert.ThrowsException<InvalidOperationException>(
            () => estimator.GetCovariance(new[] { 0.0, 0.0 }));
    }

    /// <summary>
    /// Confirms that a well-conditioned MLE Hessian yields available covariance.
    /// </summary>
    [TestMethod]
    public void MaximumLikelihood_WellConditionedHessian_ReportsAvailable()
    {
        var estimator = new MaximumLikelihood(CreateTwoParameterModel());
        ConfigureEstimatedState(
            estimator,
            new Matrix(new double[,] { { -2.0, 0.0 }, { 0.0, -4.0 } }));

        bool succeeded = estimator.TryGetCovarianceMatrix(out Matrix covariance);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(CovarianceComputationStatus.Available, estimator.CovarianceStatus);
        Assert.IsNull(estimator.CovarianceDiagnostic);
        Assert.AreEqual(0.5, covariance[0, 0], 1e-12);
        Assert.AreEqual(0.25, covariance[1, 1], 1e-12);
    }

    /// <summary>
    /// Confirms that positive-definite adjustment is visible through MLE covariance status.
    /// </summary>
    [TestMethod]
    public void MaximumLikelihood_NonsymmetricCandidate_ReportsRegularized()
    {
        var estimator = new MaximumLikelihood(CreateTwoParameterModel());
        ConfigureEstimatedState(
            estimator,
            new Matrix(new double[,] { { -2.0, -0.5 }, { 0.0, -1.0 } }));

        bool succeeded = estimator.TryGetCovarianceMatrix(out Matrix covariance);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(CovarianceComputationStatus.Regularized, estimator.CovarianceStatus);
        Assert.IsFalse(string.IsNullOrWhiteSpace(estimator.CovarianceDiagnostic));
        Assert.IsTrue(covariance[0, 0] > 0.0);
        Assert.IsTrue(covariance[1, 1] > 0.0);
        Assert.AreEqual(covariance[0, 1], covariance[1, 0], 1e-12);
    }

    /// <summary>
    /// Locks the serialized numeric values of the public covariance status enum.
    /// </summary>
    [TestMethod]
    public void CovarianceComputationStatus_ValuesAreStable()
    {
        Assert.AreEqual(0, (int)CovarianceComputationStatus.NotComputed);
        Assert.AreEqual(1, (int)CovarianceComputationStatus.Available);
        Assert.AreEqual(2, (int)CovarianceComputationStatus.Regularized);
        Assert.AreEqual(3, (int)CovarianceComputationStatus.Failed);
    }

    /// <summary>
    /// Creates a normal-distribution model with two free parameters.
    /// </summary>
    /// <returns>A two-parameter model used only to construct MLE and MAP estimators.</returns>
    private static UnivariateDistribution CreateTwoParameterModel()
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new[] { -1.0, -0.2, 0.4, 1.1 })
        };
        return new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Places an MLE estimator in a deterministic estimated state.
    /// </summary>
    /// <param name="estimator">The estimator to configure.</param>
    /// <param name="hessian">The Hessian exposed to covariance computation.</param>
    private static void ConfigureEstimatedState(MaximumLikelihood estimator, Matrix hessian)
    {
        SetPrivateProperty(estimator, nameof(MaximumLikelihood.IsEstimated), true);
        SetPrivateField(estimator, "_hessian", hessian);
    }

    /// <summary>
    /// Places a MAP estimator in a deterministic estimated state.
    /// </summary>
    /// <param name="estimator">The estimator to configure.</param>
    /// <param name="hessian">The Hessian exposed to covariance computation.</param>
    private static void ConfigureEstimatedState(MaximumAPosteriori estimator, Matrix hessian)
    {
        SetPrivateProperty(estimator, nameof(MaximumAPosteriori.IsEstimated), true);
        SetPrivateField(estimator, "_hessian", hessian);
    }

    /// <summary>
    /// Sets a property with a non-public setter for deterministic failure-path setup.
    /// </summary>
    /// <param name="target">The object containing the property.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        property.SetValue(target, value);
    }

    /// <summary>
    /// Sets a private field for deterministic failure-path setup.
    /// </summary>
    /// <param name="target">The object containing the field.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }

    /// <summary>
    /// Asserts that every matrix entry is exactly zero.
    /// </summary>
    /// <param name="matrix">The matrix returned by a failed Try operation.</param>
    private static void AssertZeroMatrix(Matrix matrix)
    {
        for (int row = 0; row < matrix.NumberOfRows; row++)
        {
            for (int column = 0; column < matrix.NumberOfColumns; column++)
                Assert.AreEqual(0.0, matrix[row, column]);
        }
    }
}
