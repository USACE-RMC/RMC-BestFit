using System.Xml.Linq;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Tests for the conditional evaluation order of the time-series models: the likelihood,
/// residuals, transform Jacobian window and prediction seeding all start at the same model step,
/// degenerate training windows are rejected, and property changes keep the evaluation state
/// consistent.
/// </summary>
/// <remarks>
/// ARIMAX conditions on K = max(q, p + b) when it has covariates and on K = max(p, q) when it has
/// none (review decision D6, approved 25 September 2026). Every evaluated step t ≥ K then has its
/// own mean with all b covariate lags, and every autoregressive-lag mean m(t − i), i ≤ p, has all b
/// lags. Residual lags before K fall inside the conditioned window, where the residuals are zero;
/// K ≥ q keeps every lag used at step K at or after step 0.
/// </remarks>
[TestClass]
public class TimeSeriesConditioningOrderTests
{
    private const double Tolerance = 1E-12;
    private const double ConditioningIntercept = 1.0;
    private const double ConditioningSigma = 1.5;
    private static readonly DateTime s_startDate = new(2002, 3, 4);
    private static readonly double[] s_conditioningResponse = { 3.0, 4.5, 2.5, 5.0, 4.0, 6.5, 5.5, 7.0, 6.0, 8.0 };
    private static readonly double[] s_conditioningCovariate = { 1.0, 2.0, 0.5, 1.5, 3.0, 2.5, 1.0, 2.0, 1.5, 0.5 };
    private static readonly double[] s_conditioningBeta = { 0.8, 0.3, -0.2 };
    private static readonly double[] s_conditioningPhi = { 0.4, -0.25 };
    private static readonly double[] s_conditioningTheta = { 0.3, -0.2, 0.15, 0.1 };

    /// <summary>
    /// Verifies the ARIMAX conditioning order for the four hand-derived cases of the rule
    /// K = max(q, p + b) with covariates and K = max(p, q) without: the residuals of the first K
    /// steps are zero, later residuals and the likelihood match an independent recursion that
    /// never truncates a covariate lag, the pointwise decomposition has one term per evaluated
    /// step, and validation requires more than K differenced training steps.
    /// </summary>
    /// <param name="arOrder">The autoregressive order p.</param>
    /// <param name="maOrder">The moving-average order q.</param>
    /// <param name="covariateLagOrder">The covariate lag order b.</param>
    /// <param name="withCovariate">Whether one exact-date covariate is attached.</param>
    /// <param name="expectedConditioningOrder">The hand-derived conditioning order K.</param>
    /// <remarks>
    /// The cases are p = 1, b = 2, q = 0 (p + b decides, K = 3); p = 2, b = 1, q = 4 (q decides,
    /// K = 4); p = 0, b = 2 (K = b = 2); and p = 1, q = 0, b = 2 without covariates (b has no
    /// role, K = 1). The independent recursion throws if a mean it evaluates would need a
    /// covariate value before the first observation, so it also states the rule.
    /// </remarks>
    [TestMethod]
    [DataRow(1, 0, 2, true, 3)]
    [DataRow(2, 4, 1, true, 4)]
    [DataRow(0, 0, 2, true, 2)]
    [DataRow(1, 0, 2, false, 1)]
    public void ArimaxConditioningOrder_IsMaxOfMaOrderAndArPlusCovariateLagOrder(
        int arOrder, int maOrder, int covariateLagOrder, bool withCovariate, int expectedConditioningOrder)
    {
        double[] y = s_conditioningResponse;
        double[]? x = withCovariate ? s_conditioningCovariate : null;
        double[] beta = withCovariate ? s_conditioningBeta.Take(covariateLagOrder + 1).ToArray() : Array.Empty<double>();
        double[] phi = s_conditioningPhi.Take(arOrder).ToArray();
        double[] theta = s_conditioningTheta.Take(maOrder).ToArray();
        double[] parameters = new[] { ConditioningIntercept }.Concat(beta).Concat(phi).Concat(theta).Append(ConditioningSigma).ToArray();
        ARIMAX model = CreateArimax(y, x, differenceOrder: 0, covariateLagOrder, ModelTransform.None, includeIntercept: true,
            arOrder: arOrder, maOrder: maOrder);
        Assert.AreEqual(parameters.Length, model.NumberOfParameters, "parameter layout");

        double[] expectedResiduals = IndependentConditionalResiduals(y, x, ConditioningIntercept, beta, phi, theta, expectedConditioningOrder);
        double expected = expectedResiduals.Skip(expectedConditioningOrder).Sum(residual => NormalLogDensity(residual, ConditioningSigma));
        double[] residuals = model.Residuals(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(y.Length, residuals.Length, "residual length");
        for (int t = 0; t < expectedConditioningOrder; t++)
            Assert.AreEqual(0.0, residuals[t], 0.0, $"conditioning step {t} residual");
        for (int t = expectedConditioningOrder; t < y.Length; t++)
            Assert.AreEqual(expectedResiduals[t], residuals[t], Tolerance, $"residual {t}");
        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "conditional log-likelihood");
        Assert.AreEqual(y.Length - expectedConditioningOrder, pointwise.Length, "pointwise term count");
        Assert.AreEqual(expected, pointwise.Sum(), Tolerance, "pointwise sum");
        Assert.AreEqual(pointwise.Length, components.Count, "component count");
        Assert.AreEqual($"t={expectedConditioningOrder}", components[0].Name, "first evaluated step");

        model.TrainingTimeSteps = expectedConditioningOrder;
        (bool _, List<string> atOrder) = model.Validate();
        Assert.IsTrue(atOrder.Any(message => message.Contains($"conditioning order ({expectedConditioningOrder})", StringComparison.Ordinal)),
            string.Join(" | ", atOrder));

        model.TrainingTimeSteps = expectedConditioningOrder + 1;
        (bool _, List<string> aboveOrder) = model.Validate();
        Assert.IsFalse(aboveOrder.Any(message => message.Contains("differenced model steps", StringComparison.Ordinal)),
            string.Join(" | ", aboveOrder));
    }

    /// <summary>
    /// Verifies the hand-computed residuals and deterministic predictions of an ARIMAX(1,0,0)
    /// with one covariate lagged twice (K = p + b = 3): the first three steps are observed
    /// values, and each later step adds its full-lag mean to the AR term of the previous step's
    /// full-lag mean.
    /// </summary>
    /// <remarks>
    /// With μ = 1, β = (0.8, 0.3, −0.2) and φ = 0.4 the means are m₂ = 1.8, m₃ = 1.95,
    /// m₄ = 3.75, m₅ = 3.6 and m₆ = 1.95, so the one-step predictions are 1.95 + 0.4(2.5 − 1.8) =
    /// 2.23, 3.75 + 0.4(5.0 − 1.95) = 4.97 and 3.6 + 0.4(4.0 − 3.75) = 3.7, and the forecast from
    /// the final observed training value is 1.95 + 0.4(6.5 − 3.6) = 3.11. The former order
    /// max(p, q, b) = 2 predicted step 2 from the truncated mean m₁ = 2.9 (1.8 + 0.4(4.5 − 2.9) =
    /// 2.44) instead of conditioning on the observed 2.5.
    /// </remarks>
    [TestMethod]
    public void ArimaxArDistributedLag_HandDerivedResidualsAndPredictionsStartAtArPlusLagOrder()
    {
        double[] parameters = { 1.0, 0.8, 0.3, -0.2, 0.4, 1.5 };
        ARIMAX model = CreateArimax(s_conditioningResponse, s_conditioningCovariate, differenceOrder: 0, covariateLagOrder: 2,
            ModelTransform.None, includeIntercept: true, trainingSteps: 6, arOrder: 1);

        double[] residuals = model.Residuals(parameters);
        AssertArrayEqual(new[] { 0.0, 0.0, 0.0, 2.77, -0.97, 2.8 }, residuals, "hand-derived residuals");

        var result = model.Predict(parameters, 1, -1);
        AssertArrayEqual(new[] { 3.0, 4.5, 2.5, 2.23, 4.97, 3.7, 3.11 }, result.Y, "hand-derived conditional predictions");
        Assert.AreEqual(0.95, result.CovariatePart[3], Tolerance, "covariate part at raw slot 3");
        Assert.AreEqual(2.75, result.CovariatePart[4], Tolerance, "covariate part at raw slot 4");
        Assert.AreEqual(2.6, result.CovariatePart[5], Tolerance, "covariate part at raw slot 5");
        Assert.AreEqual(0.95, result.CovariatePart[6], Tolerance, "covariate part at the forecast slot");
    }

    /// <summary>
    /// Verifies the logarithmic transform Jacobian of an ARIMAX(1,0,0) with one covariate lagged
    /// twice starts at the conditioning order K = p + b = 3, matching the conditional likelihood.
    /// </summary>
    /// <remarks>
    /// The covariate is attached after the response and transform, so the model reaches K = 3
    /// only when the covariate is added; the expected value sums the Gaussian log density of each
    /// log-scale residual and −log(y) for raw indices 3 through 9.
    /// </remarks>
    [TestMethod]
    public void ArimaxArDistributedLag_LogJacobianStartsAtConditioningOrder()
    {
        double[] logResponse = s_conditioningResponse.Select(value => Math.Log(value)).ToArray();
        double[] beta = s_conditioningBeta;
        double[] phi = { s_conditioningPhi[0] };
        double[] parameters = { ConditioningIntercept, beta[0], beta[1], beta[2], phi[0], ConditioningSigma };
        ARIMAX model = CreateArimax(s_conditioningResponse, s_conditioningCovariate, differenceOrder: 0, covariateLagOrder: 2,
            ModelTransform.Logarithmic, includeIntercept: true, arOrder: 1);

        double[] expectedResiduals = IndependentConditionalResiduals(
            logResponse, s_conditioningCovariate, ConditioningIntercept, beta, phi, Array.Empty<double>(), conditioningOrder: 3);
        double expected = 0.0;
        for (int t = 3; t < logResponse.Length; t++)
            expected += NormalLogDensity(expectedResiduals[t], ConditioningSigma) - logResponse[t];

        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "log-scale conditional log-likelihood");
        Assert.AreEqual(expected, model.PointwiseDataLogLikelihood(parameters).Sum(), 1E-10, "pointwise sum");
    }

    /// <summary>
    /// Verifies changing the covariate lag order of a transformed ARIMAX refreshes the Jacobian
    /// window, so the likelihood equals that of a model built with the new lag order.
    /// </summary>
    /// <remarks>
    /// With p = 1 and one covariate, b = 0 gives K = 1 and b = 2 gives K = 3. The reference models
    /// set their transform after every structural input, so their training state is built once for
    /// the final structure and does not depend on the lag-order setter.
    /// </remarks>
    [TestMethod]
    public void ArimaxCovariateLagOrderChange_RefreshesTransformJacobianWindow()
    {
        double[] lagTwoParameters = { 1.0, 0.8, 0.3, -0.2, 0.4, 1.5 };
        double[] lagZeroParameters = { 1.0, 0.8, 0.4, 1.5 };

        ARIMAX changed = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 0, ModelTransform.Logarithmic);
        changed.XOrderB = 2;
        ARIMAX lagTwo = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 2, ModelTransform.Logarithmic);
        Assert.AreEqual(lagTwo.DataLogLikelihood(lagTwoParameters), changed.DataLogLikelihood(lagTwoParameters), Tolerance, "b = 0 to b = 2");
        AssertArrayEqual(lagTwo.PointwiseDataLogLikelihood(lagTwoParameters), changed.PointwiseDataLogLikelihood(lagTwoParameters), "b = 0 to b = 2 pointwise");

        changed.XOrderB = 0;
        ARIMAX lagZero = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 0, ModelTransform.Logarithmic);
        Assert.AreEqual(lagZero.DataLogLikelihood(lagZeroParameters), changed.DataLogLikelihood(lagZeroParameters), Tolerance, "b = 2 back to b = 0");
        AssertArrayEqual(lagZero.PointwiseDataLogLikelihood(lagZeroParameters), changed.PointwiseDataLogLikelihood(lagZeroParameters), "b = 2 back to b = 0 pointwise");
    }

    /// <summary>
    /// Verifies a covariate lag-order change keeps a restored automatic Box-Cox exponent, raises
    /// no exponent notification, and moves the Jacobian window to the new conditioning order.
    /// </summary>
    /// <remarks>
    /// The automatic exponent is fitted on the whole raw training prefix, which does not depend on
    /// the conditioning order, so a lag-order change has no reason to refit it. The saved XML is
    /// written by hand, so no exponent fit runs. After b changes from 0 to 2, K = p + b = 3 and the
    /// expected value sums the Gaussian log density of each Box-Cox residual and
    /// (λ − 1)·log(y) for raw indices 3 through 9.
    /// </remarks>
    [TestMethod]
    public void ArimaxCovariateLagOrderChange_KeepsRestoredExponent()
    {
        const double lambda = 0.5;
        ARIMAX template = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 0, ModelTransform.None);
        XElement saved = template.ToXElement();
        saved.SetAttributeValue(nameof(ARIMAX.TransformType), nameof(ModelTransform.BoxCox));
        saved.SetAttributeValue(nameof(ARIMAX.TransformLambda), "0.5");
        saved.SetAttributeValue("TransformLambdaIsManual", "False");
        var restored = new ARIMAX(template.TimeSeries, saved);
        restored.SetCovariates(new List<NumericTimeSeries> { template.Covariates[0] }, resetParameters: false);
        Assert.AreEqual(lambda, restored.TransformLambda, 0.0, "Precondition: the restored exponent.");

        var names = new List<string>();
        restored.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
        restored.XOrderB = 2;

        Assert.AreEqual(lambda, restored.TransformLambda, 0.0, "The lag-order change must keep the restored exponent.");
        CollectionAssert.DoesNotContain(names, nameof(ARIMAX.TransformLambda), "No exponent change may be reported.");

        double[] boxCoxResponse = s_conditioningResponse.Select(value => (Math.Pow(value, lambda) - 1.0) / lambda).ToArray();
        double[] beta = s_conditioningBeta;
        double[] phi = { s_conditioningPhi[0] };
        double[] parameters = { ConditioningIntercept, beta[0], beta[1], beta[2], phi[0], ConditioningSigma };
        double[] expectedResiduals = IndependentConditionalResiduals(
            boxCoxResponse, s_conditioningCovariate, ConditioningIntercept, beta, phi, Array.Empty<double>(), conditioningOrder: 3);
        double expected = 0.0;
        for (int t = 3; t < boxCoxResponse.Length; t++)
            expected += NormalLogDensity(expectedResiduals[t], ConditioningSigma) + (lambda - 1.0) * Math.Log(s_conditioningResponse[t]);

        Assert.AreEqual(expected, restored.DataLogLikelihood(parameters), 1E-10, "Box-Cox conditional log-likelihood after the lag-order change");
    }

    /// <summary>
    /// Verifies adding the only covariate to, or removing it from, a transformed ARIMAX refreshes
    /// the Jacobian window, because the covariates decide whether the lag order enters K.
    /// </summary>
    /// <remarks>
    /// With p = 1 and b = 2, K is max(p, q) = 1 without covariates and max(q, p + b) = 3 with one.
    /// The reference models set their transform after the covariates, so their Jacobian window is
    /// built for the final structure.
    /// </remarks>
    [TestMethod]
    public void ArimaxCovariatePresenceChange_RefreshesTransformJacobianWindow()
    {
        double[] withCovariateParameters = { 1.0, 0.8, 0.3, -0.2, 0.4, 1.5 };
        double[] withoutCovariateParameters = { 1.0, 0.4, 1.5 };

        ARIMAX added = CreateRebuiltArimax(s_conditioningResponse, covariate: null, arOrder: 1, covariateLagOrder: 2, ModelTransform.Logarithmic);
        added.SetCovariates(new List<NumericTimeSeries> { CreateSeries(s_conditioningCovariate) });
        ARIMAX withCovariate = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 2, ModelTransform.Logarithmic);
        Assert.AreEqual(withCovariate.DataLogLikelihood(withCovariateParameters), added.DataLogLikelihood(withCovariateParameters), Tolerance, "first covariate added");
        AssertArrayEqual(withCovariate.PointwiseDataLogLikelihood(withCovariateParameters), added.PointwiseDataLogLikelihood(withCovariateParameters), "first covariate added pointwise");

        ARIMAX removed = CreateRebuiltArimax(s_conditioningResponse, s_conditioningCovariate, arOrder: 1, covariateLagOrder: 2, ModelTransform.Logarithmic);
        removed.SetCovariates(new List<NumericTimeSeries>());
        ARIMAX withoutCovariate = CreateRebuiltArimax(s_conditioningResponse, covariate: null, arOrder: 1, covariateLagOrder: 2, ModelTransform.Logarithmic);
        Assert.AreEqual(withoutCovariate.DataLogLikelihood(withoutCovariateParameters), removed.DataLogLikelihood(withoutCovariateParameters), Tolerance, "last covariate removed");
        AssertArrayEqual(withoutCovariate.PointwiseDataLogLikelihood(withoutCovariateParameters), removed.PointwiseDataLogLikelihood(withoutCovariateParameters), "last covariate removed pointwise");
    }

    /// <summary>
    /// Verifies a Box-Cox ARIMAX with a lagged covariate that is restored from XML and reattached
    /// to its covariate, or cloned, keeps its exponent and the source's Jacobian window.
    /// </summary>
    /// <remarks>
    /// Restoring and cloning build the training state before the covariates are attached, when
    /// K is max(p, q) = 1, so the covariate attachment must move the window to K = p + b = 3.
    /// The source sets its manual exponent again after the covariate, which rebuilds its training
    /// state for the final structure.
    /// </remarks>
    [TestMethod]
    public void ArimaxRestoredOrClonedWithLaggedCovariate_KeepsTransformJacobianWindow()
    {
        const double lambda = 0.4;
        double[] parameters = { 1.0, 0.8, 0.3, -0.2, 0.4, 1.5 };
        var source = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 2,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            TransformType = ModelTransform.BoxCox,
        };
        source.SetTransformParameters(lambda);
        source.TimeSeries = CreateSeries(s_conditioningResponse);
        source.UseDefaultTrainingSteps = false;
        source.TrainingTimeSteps = s_conditioningResponse.Length;
        source.SetCovariates(new List<NumericTimeSeries> { CreateSeries(s_conditioningCovariate) });
        source.SetTransformParameters(lambda);
        double expected = source.DataLogLikelihood(parameters);

        var restored = new ARIMAX(source.TimeSeries, source.ToXElement());
        restored.SetCovariates(new List<NumericTimeSeries> { source.Covariates[0] }, resetParameters: false);
        Assert.AreEqual(lambda, restored.TransformLambda, 0.0, "restored exponent");
        Assert.AreEqual(expected, restored.DataLogLikelihood(parameters), Tolerance, "restored likelihood");
        AssertArrayEqual(source.PointwiseDataLogLikelihood(parameters), restored.PointwiseDataLogLikelihood(parameters), "restored pointwise");

        var clone = (ARIMAX)source.Clone();
        Assert.AreEqual(lambda, clone.TransformLambda, 0.0, "cloned exponent");
        Assert.AreEqual(expected, clone.DataLogLikelihood(parameters), Tolerance, "cloned likelihood");
        AssertArrayEqual(source.PointwiseDataLogLikelihood(parameters), clone.PointwiseDataLogLikelihood(parameters), "cloned pointwise");
    }

    /// <summary>
    /// Verifies an ARIMAX distributed-lag regression (p = q = 0, b = 2) conditions on
    /// K = max(q, p + b) = 2 model steps, so every evaluated step includes all lagged covariate
    /// terms.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_ConditionsOnCovariateLagOrder()
    {
        double[] y = { 3.0, 4.5, 2.5, 5.0, 4.0, 6.5, 5.5, 7.0 };
        double[] x = { 1.0, 2.0, 0.5, 1.5, 3.0, 2.5, 1.0, 2.0 };
        const double mu = 1.0, beta0 = 0.8, beta1 = 0.3, beta2 = -0.2, sigma = 1.5;
        double[] parameters = { mu, beta0, beta1, beta2, sigma };
        ARIMAX model = CreateArimax(y, x, differenceOrder: 0, covariateLagOrder: 2, ModelTransform.None, includeIntercept: true);

        double expected = 0.0;
        var expectedPointwise = new List<double>();
        for (int t = 2; t < y.Length; t++)
        {
            double residual = y[t] - (mu + beta0 * x[t] + beta1 * x[t - 1] + beta2 * x[t - 2]);
            double term = NormalLogDensity(residual, sigma);
            expected += term;
            expectedPointwise.Add(term);
        }

        double[] residuals = model.Residuals(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "conditional log-likelihood");
        Assert.AreEqual(y.Length, residuals.Length, "residual length");
        Assert.AreEqual(0.0, residuals[0], 0.0, "conditioning step 0 residual");
        Assert.AreEqual(0.0, residuals[1], 0.0, "conditioning step 1 residual");
        CollectionAssert.AreEqual(
            expectedPointwise.Select(value => Math.Round(value, 10)).ToArray(),
            pointwise.Select(value => Math.Round(value, 10)).ToArray(),
            "pointwise log-likelihood");
        Assert.AreEqual(expected, pointwise.Sum(), Tolerance, "pointwise sum");
        Assert.AreEqual(expectedPointwise.Count, components.Count, "component count");
        Assert.AreEqual(expected, components.Sum(component => component.LogLikelihood), Tolerance, "component sum");
    }

    /// <summary>
    /// Verifies the transform Jacobian window of an ARIMAX distributed-lag regression
    /// (p = q = 0, b = 1) starts at K = max(q, p + b) = 1, matching the conditional likelihood.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_LogJacobianStartsAtCovariateLagOrder()
    {
        double[] y = { 2.0, 4.0, 8.0, 4.0, 2.0, 6.0 };
        double[] x = { 1.0, 3.0, 2.0, 5.0, 4.0, 2.0 };
        const double mu = 0.5, beta0 = 0.1, beta1 = 0.2, sigma = 0.7;
        double[] parameters = { mu, beta0, beta1, sigma };
        ARIMAX model = CreateArimax(y, x, differenceOrder: 0, covariateLagOrder: 1, ModelTransform.Logarithmic, includeIntercept: true);

        double expected = 0.0;
        for (int t = 1; t < y.Length; t++)
        {
            double residual = Math.Log(y[t]) - (mu + beta0 * x[t] + beta1 * x[t - 1]);
            expected += NormalLogDensity(residual, sigma) - Math.Log(y[t]);
        }

        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "log-scale conditional log-likelihood");
        Assert.AreEqual(expected, model.PointwiseDataLogLikelihood(parameters).Sum(), 1E-10, "pointwise sum");
    }

    /// <summary>
    /// Verifies first-difference ARIMAX predictions (p = q = 0, b = 1) seed the first
    /// K = max(q, p + b) = 1 model step from the observed difference and apply all lagged
    /// covariate terms to every predicted step.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_PredictSeedsCovariateLagStepsForDifferencedModels()
    {
        double[] raw = { 10.0, 14.0, 15.0, 19.0, 999.0 };
        double[] covariate = { 1.0, 2.0, 3.0, 4.0, 5.0 };
        ARIMAX model = CreateArimax(raw, covariate, differenceOrder: 1, covariateLagOrder: 1, ModelTransform.None, includeIntercept: true, trainingSteps: 4);

        var result = model.Predict(new[] { 0.5, 0.2, 0.1, 1.0 }, 1, -1);

        // Model step 0 is an observed difference (4); steps 1-2 add c + 0.2 x_t + 0.1 x_{t-1} to
        // the observed level; the forecast step adds the same mean to the final training level.
        AssertArrayEqual(new[] { 10.0, 14.0, 15.3, 16.6, 20.9 }, result.Y, "d=1, b=1 conditional levels");
        Assert.AreEqual(0.8, result.CovariatePart[2], Tolerance, "covariate part at raw slot 2");
        Assert.AreEqual(1.1, result.CovariatePart[3], Tolerance, "covariate part at raw slot 3");
        Assert.AreEqual(1.4, result.CovariatePart[4], Tolerance, "covariate part at raw slot 4");
    }

    /// <summary>
    /// Verifies undifferenced ARIMAX predictions (p = q = 0, b = 1) seed the first
    /// K = max(q, p + b) = 1 model step from the observation.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_PredictSeedsCovariateLagStepsForUndifferencedModels()
    {
        double[] raw = { 10.0, 14.0, 15.0, 19.0 };
        double[] covariate = { 1.0, 2.0, 3.0, 4.0 };
        ARIMAX model = CreateArimax(raw, covariate, differenceOrder: 0, covariateLagOrder: 1, ModelTransform.None, includeIntercept: true);

        var result = model.Predict(new[] { 0.5, 0.2, 0.1, 1.0 }, 0, -1);

        AssertArrayEqual(new[] { 10.0, 1.0, 1.3, 1.6 }, result.Y, "d=0, b=1 conditional values");
    }

    /// <summary>
    /// Verifies changing the AR or MA order of an ARIMA model refreshes the transform Jacobian
    /// window, so the data log-likelihood equals that of a freshly constructed model.
    /// </summary>
    [TestMethod]
    public void ArimaOrderChange_RefreshesTransformJacobianWindow()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };
        double[] parameters = { 0.1, 0.4, -0.2, 0.3 };

        var changed = new ARIMA(CreateSeries(raw), 1, 0, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changed.TrainingTimeSteps = raw.Length;
        changed.POrder = 2;

        var fresh = new ARIMA(CreateSeries(raw), 2, 0, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        fresh.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(fresh.DataLogLikelihood(parameters), changed.DataLogLikelihood(parameters), Tolerance, "AR order change");

        var changedMa = new ARIMA(CreateSeries(raw), 0, 0, 1, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changedMa.TrainingTimeSteps = raw.Length;
        changedMa.QOrder = 2;

        var freshMa = new ARIMA(CreateSeries(raw), 0, 0, 2, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        freshMa.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(freshMa.DataLogLikelihood(parameters), changedMa.DataLogLikelihood(parameters), Tolerance, "MA order change");
    }

    /// <summary>
    /// Verifies changing the order of an autoregressive model refreshes the transform Jacobian
    /// window, so the data log-likelihood equals that of a freshly constructed model.
    /// </summary>
    [TestMethod]
    public void AutoRegressiveOrderChange_RefreshesTransformJacobianWindow()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };
        double[] parameters = { 0.1, 0.4, -0.2, 0.3 };

        var changed = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changed.TrainingTimeSteps = raw.Length;
        changed.Order = 2;

        var fresh = new AutoRegressive(CreateSeries(raw), order: 2, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        fresh.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(fresh.DataLogLikelihood(parameters), changed.DataLogLikelihood(parameters), Tolerance);
    }

    /// <summary>
    /// Verifies a training window that leaves no evaluated model step yields negative infinity
    /// and a validation error instead of a perfect-fit log-likelihood.
    /// </summary>
    [TestMethod]
    public void EmptyConditionalSum_IsNegativeInfinityAndInvalid()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };

        var arima = new ARIMA(CreateSeries(raw), 1, 1, 0, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = 2;
        Assert.AreEqual(double.NegativeInfinity, arima.DataLogLikelihood(new[] { 0.1, 0.4, 0.3 }), "ARIMA");
        (bool isValid, List<string> messages) = arima.Validate();
        Assert.IsFalse(isValid, "ARIMA validity");
        Assert.IsTrue(messages.Any(message => message.Contains("differenced model steps")), string.Join(" | ", messages));

        var arimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
        };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = 2;
        Assert.AreEqual(double.NegativeInfinity, arimax.DataLogLikelihood(new[] { 0.1, 0.4, 0.3 }), "ARIMAX");

        // With one covariate lagged twice, an ARIMAX(1,0,0) conditions on K = p + b = 3, so a
        // three-step training window leaves no evaluated step.
        ARIMAX distributedLag = CreateArimax(s_conditioningResponse, s_conditioningCovariate, differenceOrder: 0, covariateLagOrder: 2,
            ModelTransform.None, includeIntercept: true, trainingSteps: 3, arOrder: 1);
        double[] distributedLagParameters = { 1.0, 0.8, 0.3, -0.2, 0.4, 1.5 };
        Assert.AreEqual(double.NegativeInfinity, distributedLag.DataLogLikelihood(distributedLagParameters), "ARIMAX with a lagged covariate");
        Assert.AreEqual(0, distributedLag.PointwiseDataLogLikelihood(distributedLagParameters).Length, "ARIMAX with a lagged covariate pointwise");
        (bool distributedLagValid, List<string> distributedLagMessages) = distributedLag.Validate();
        Assert.IsFalse(distributedLagValid, "ARIMAX with a lagged covariate validity");
        Assert.IsTrue(distributedLagMessages.Any(message => message.Contains("conditioning order (3)", StringComparison.Ordinal)),
            string.Join(" | ", distributedLagMessages));
    }

    /// <summary>
    /// Verifies residuals requested before any data are attached return an empty array.
    /// </summary>
    [TestMethod]
    public void ArimaResiduals_WithoutData_ReturnsEmpty()
    {
        var model = new ARIMA { POrder = 1, IncludeIntercept = true };

        double[] residuals = model.Residuals(new[] { 0.1, 0.4, 0.3 });

        Assert.AreEqual(0, residuals.Length);
    }

    /// <summary>
    /// Verifies a transform change rebuilds default priors only when default flat priors are in
    /// use, matching the transform-parameter setter.
    /// </summary>
    [TestMethod]
    public void TransformTypeChange_PreservesCustomPriors()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };

        foreach (ModelBase model in CreateModels(raw))
        {
            model.UseDefaultFlatPriors = false;
            var customPrior = new Uniform(0.001, 5.0);
            ModelParameter scale = model.Parameters[^1];
            scale.PriorDistribution = customPrior;

            SetTransformType(model, ModelTransform.Logarithmic);

            Assert.AreSame(scale, model.Parameters[^1], $"{model.GetType().Name} keeps its parameter objects");
            Assert.AreSame(customPrior, model.Parameters[^1].PriorDistribution, $"{model.GetType().Name} keeps its custom prior");

            model.UseDefaultFlatPriors = true;
            SetTransformType(model, ModelTransform.None);

            Assert.AreNotSame(customPrior, model.Parameters[^1].PriorDistribution, $"{model.GetType().Name} restores default priors");
        }
    }

    /// <summary>
    /// Verifies differenced ARIMA and ARIMAX stochastic predictions keep a roughly constant
    /// credible-band width through the training period instead of a random-walk cone.
    /// </summary>
    [TestMethod]
    public void DifferencedPredictions_TrainingBandWidthIsBoundedBySigma()
    {
        double[] raw = CreateRandomWalk(60, 1234);
        double[] parameters = { 0.5, 0.3, -0.2, 30.0 };
        double sigma = parameters[^1];
        const int realizations = 200;

        var arima = new ARIMA(CreateSeries(raw), 1, 1, 1, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = raw.Length;
        AssertBoundedTrainingWidth(seed => arima.Predict(parameters, 0, seed).Y, raw.Length, realizations, sigma, "ARIMA");

        var arimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
        };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = raw.Length;
        AssertBoundedTrainingWidth(seed => arimax.Predict(parameters, 0, seed).Y, raw.Length, realizations, sigma, "ARIMAX");
    }

    /// <summary>
    /// Asserts the empirical 95 percent band of stochastic in-sample predictions stays bounded
    /// late in the training period and is non-degenerate early in the training period.
    /// </summary>
    /// <param name="predict">Produces one stochastic prediction path for a seed.</param>
    /// <param name="length">The prediction path length.</param>
    /// <param name="realizations">The number of seeds.</param>
    /// <param name="sigma">The innovation standard deviation.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertBoundedTrainingWidth(Func<int, double[]> predict, int length, int realizations, double sigma, string context)
    {
        var samples = new double[length, realizations];
        for (int realization = 0; realization < realizations; realization++)
        {
            double[] path = predict(realization + 1);
            for (int t = 0; t < length; t++)
                samples[t, realization] = path[t];
        }

        double WidthAt(int t)
        {
            var row = new double[realizations];
            for (int realization = 0; realization < realizations; realization++)
                row[realization] = samples[t, realization];
            Array.Sort(row);
            return row[(int)(0.975 * realizations)] - row[(int)(0.025 * realizations)];
        }

        double earlyWidth = WidthAt(5);
        double lateWidth = WidthAt(length - 5);

        // A random-walk cone over ~50 steps would be roughly 2 * 1.96 * sqrt(50) * sigma; the
        // conditional band is roughly 2 * 1.96 * sigma.
        Assert.IsTrue(lateWidth < 6.0 * sigma * Math.Sqrt(2), $"{context}: late training width {lateWidth:F1} exceeds the bound {6.0 * sigma * Math.Sqrt(2):F1}.");
        Assert.IsTrue(earlyWidth > 0.1 * sigma, $"{context}: early training width {earlyWidth:F1} is degenerate.");
    }

    /// <summary>
    /// Creates the four time-series models on the same raw data with a default transform.
    /// </summary>
    /// <param name="raw">The raw observations.</param>
    /// <returns>AR, MA, ARIMA and ARIMAX models.</returns>
    private static IEnumerable<ModelBase> CreateModels(double[] raw)
    {
        var ar = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: true) { UseDefaultTrainingSteps = false };
        ar.TrainingTimeSteps = raw.Length;
        yield return ar;

        var ma = new MovingAverage(CreateSeries(raw), order: 1, includeIntercept: true) { UseDefaultTrainingSteps = false };
        ma.TrainingTimeSteps = raw.Length;
        yield return ma;

        var arima = new ARIMA(CreateSeries(raw), 1, 0, 1, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = raw.Length;
        yield return arima;

        var arimax = new ARIMAX { IncludeIntercept = true, AROrderP = 1, DiffOrderD = 0, MAOrderQ = 1, XOrderB = 0, UseDefaultTrainingSteps = false };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = raw.Length;
        yield return arimax;
    }

    /// <summary>
    /// Sets the transform type of a time-series model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="transform">The transform.</param>
    private static void SetTransformType(ModelBase model, ModelTransform transform)
    {
        switch (model)
        {
            case AutoRegressive ar:
                ar.TransformType = transform;
                break;
            case MovingAverage ma:
                ma.TransformType = transform;
                break;
            case ARIMA arima:
                arima.TransformType = transform;
                break;
            case ARIMAX arimax:
                arimax.TransformType = transform;
                break;
            default:
                throw new ArgumentException("Unexpected model type.", nameof(model));
        }
    }

    /// <summary>
    /// Creates an ARIMAX model with at most one exact-date covariate, attached last.
    /// </summary>
    /// <param name="raw">The raw response values.</param>
    /// <param name="covariate">The covariate values on the response dates; null attaches none.</param>
    /// <param name="differenceOrder">The differencing order.</param>
    /// <param name="covariateLagOrder">The covariate lag order.</param>
    /// <param name="transform">The transform.</param>
    /// <param name="includeIntercept">Whether an intercept is included.</param>
    /// <param name="trainingSteps">The raw training steps; null uses the full series.</param>
    /// <param name="arOrder">The autoregressive order.</param>
    /// <param name="maOrder">The moving-average order.</param>
    /// <returns>The configured model.</returns>
    /// <remarks>
    /// The transform is set before the response is attached and the covariate is attached last,
    /// so a model with a covariate reaches its conditioning order only when the covariate is added.
    /// </remarks>
    private static ARIMAX CreateArimax(
        double[] raw,
        double[]? covariate,
        int differenceOrder,
        int covariateLagOrder,
        ModelTransform transform,
        bool includeIntercept,
        int? trainingSteps = null,
        int arOrder = 0,
        int maOrder = 0)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = includeIntercept,
            AROrderP = arOrder,
            DiffOrderD = differenceOrder,
            MAOrderQ = maOrder,
            XOrderB = covariateLagOrder,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TimeSeries = CreateSeries(raw);
        model.TrainingTimeSteps = trainingSteps ?? raw.Length;
        if (covariate != null)
            model.SetCovariates(new List<NumericTimeSeries> { CreateSeries(covariate) });
        return model;
    }

    /// <summary>
    /// Creates an ARIMAX(p,0,0) model whose transform is set after its covariate and orders, so
    /// the transform setter builds the training state, including the Jacobian window, for the
    /// final structure.
    /// </summary>
    /// <param name="raw">The raw response values.</param>
    /// <param name="covariate">The covariate values on the response dates; null attaches none.</param>
    /// <param name="arOrder">The autoregressive order.</param>
    /// <param name="covariateLagOrder">The covariate lag order.</param>
    /// <param name="transform">The transform, set last; it must not require an exponent fit.</param>
    /// <returns>The configured model.</returns>
    /// <remarks>
    /// This reference does not depend on the covariate lag-order setter or on
    /// <see cref="ARIMAX.SetCovariates(List{NumericTimeSeries})"/> to place the Jacobian window.
    /// </remarks>
    private static ARIMAX CreateRebuiltArimax(double[] raw, double[]? covariate, int arOrder, int covariateLagOrder, ModelTransform transform)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = arOrder,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = covariateLagOrder,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
        };
        model.TimeSeries = CreateSeries(raw);
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = raw.Length;
        if (covariate != null)
            model.SetCovariates(new List<NumericTimeSeries> { CreateSeries(covariate) });
        model.TransformType = transform;
        return model;
    }

    /// <summary>
    /// Independently evaluates the conditional residuals of a regression with ARMA errors and one
    /// lagged covariate, conditioning on the first <paramref name="conditioningOrder"/> steps.
    /// </summary>
    /// <param name="y">The model-scale response.</param>
    /// <param name="x">The covariate on the response dates; null when there is none.</param>
    /// <param name="mu">The intercept.</param>
    /// <param name="beta">The covariate coefficients for lags 0 through b; empty without a covariate.</param>
    /// <param name="phi">The autoregressive coefficients.</param>
    /// <param name="theta">The moving-average coefficients.</param>
    /// <param name="conditioningOrder">The number of leading conditioned steps, whose residuals are zero.</param>
    /// <returns>The residuals; zero for the conditioned steps.</returns>
    /// <exception cref="InvalidOperationException">
    /// A mean that the recursion evaluates would need a covariate value before the first
    /// observation, so the conditioning order is too small for the lag structure.
    /// </exception>
    /// <remarks>
    /// Each evaluated step t uses e(t) = y(t) − m(t) − Σ φ_i (y(t − i) − m(t − i)) − Σ θ_j e(t − j)
    /// with m(t) = μ + Σ β_j x(t − j). No covariate lag is ever truncated.
    /// </remarks>
    private static double[] IndependentConditionalResiduals(
        double[] y, double[]? x, double mu, double[] beta, double[] phi, double[] theta, int conditioningOrder)
    {
        double Mean(int t)
        {
            double mean = mu;
            for (int lag = 0; lag < beta.Length; lag++)
            {
                if (t - lag < 0)
                    throw new InvalidOperationException($"The mean at step {t} needs covariate lag {lag}, which precedes the first observation.");
                mean += beta[lag] * x![t - lag];
            }

            return mean;
        }

        var residuals = new double[y.Length];
        for (int t = conditioningOrder; t < y.Length; t++)
        {
            double prediction = Mean(t);
            for (int i = 1; i <= phi.Length; i++)
                prediction += phi[i - 1] * (y[t - i] - Mean(t - i));
            for (int j = 1; j <= theta.Length; j++)
                prediction += theta[j - 1] * residuals[t - j];
            residuals[t] = y[t] - prediction;
        }

        return residuals;
    }

    /// <summary>
    /// Creates a daily series.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <returns>The series.</returns>
    private static NumericTimeSeries CreateSeries(double[] values) =>
        new(TimeInterval.OneDay, s_startDate, values);

    /// <summary>
    /// Creates a deterministic positive random-walk fixture.
    /// </summary>
    /// <param name="length">The series length.</param>
    /// <param name="seed">The generator seed.</param>
    /// <returns>The fixture values.</returns>
    private static double[] CreateRandomWalk(int length, int seed)
    {
        var random = new Random(seed);
        var values = new double[length];
        double level = 500.0;
        for (int i = 0; i < length; i++)
        {
            level += 40.0 * (random.NextDouble() - 0.5);
            values[i] = level;
        }

        return values;
    }

    /// <summary>
    /// Evaluates the Gaussian log density of a residual.
    /// </summary>
    /// <param name="residual">The residual.</param>
    /// <param name="sigma">The standard deviation.</param>
    /// <returns>The log density.</returns>
    private static double NormalLogDensity(double residual, double sigma) =>
        -0.5 * Math.Log(2.0 * Math.PI * sigma * sigma) - residual * residual / (2.0 * sigma * sigma);

    /// <summary>
    /// Asserts two arrays agree element by element within the test tolerance.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, $"{context}: length");
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], Tolerance, $"{context}: index {i}");
    }
}
