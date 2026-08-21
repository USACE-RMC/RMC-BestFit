using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RatingCurveModel = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Independent numerical verification methods for the Phase 5 time-series findings.
/// </summary>
[TestClass]
public class Phase5TimeSeriesVerificationTests
{
    /// <summary>
    /// Verifies the four time-series Jeffreys scale components against the analytical
    /// log-density oracle <c>log(1 / sigma) = -log(sigma)</c> at fixed positive scales.
    /// </summary>
    /// <remarks>
    /// The oracle is evaluated directly from the mathematical Jeffreys density and does not
    /// call either production scalar-prior implementation. The absolute acceptance tolerance
    /// is fixed at 1E-12.
    /// </remarks>
    [TestMethod]
    public void JeffreysScaleMetadataMatchesIndependentPriorOracle()
    {
        JsonElement oracle = LoadOracle("phase5-jeffreys-prior-oracle.json");
        double tolerance = oracle.GetProperty("absolute_tolerance").GetDouble();

        foreach (JsonElement testCase in oracle.GetProperty("cases").EnumerateArray())
        {
            string modelName = testCase.GetProperty("model").GetString()!;
            double sigma = testCase.GetProperty("sigma").GetDouble();
            double expected = testCase.GetProperty("expected_log_density").GetDouble();
            VerifyJeffreysComponent(CreateModel(modelName), sigma, expected, tolerance);
        }
    }

    /// <summary>
    /// Verifies valid Gaussian data/prior decomposition and invalid scale rejection against an
    /// independently tabulated finite-positive-scale oracle.
    /// </summary>
    /// <remarks>
    /// The Gaussian log density is calculated directly as
    /// <c>-0.5*log(2*pi)-log(sigma)-e^2/(2*sigma^2)</c>. Uniform marginal-prior normalization and
    /// the Jeffreys contribution are calculated independently. Valid values use 1E-12 absolute
    /// tolerance; invalid values must equal negative infinity exactly.
    /// </remarks>
    [TestMethod]
    public void InvalidScaleBehaviorMatchesScalarAndPointwiseOracle()
    {
        JsonElement oracle = LoadOracle("phase5-invalid-scale-oracle.json");
        double[] response = ReadDoubleArray(oracle.GetProperty("response"));
        double sigma = oracle.GetProperty("sigma").GetDouble();
        double tolerance = oracle.GetProperty("absolute_tolerance").GetDouble();
        double expectedPriorFromFormula = ReadDoubleArray(oracle.GetProperty("parameter_prior_widths"))
            .Sum(width => -Math.Log(width)) - Math.Log(sigma);

        foreach (JsonElement testCase in oracle.GetProperty("cases").EnumerateArray())
        {
            string modelName = testCase.GetProperty("model").GetString()!;
            ModelBase model = CreateModel(modelName, response);
            SetJeffreysRule(model);
            double[] parameters = new double[model.Parameters.Count];
            parameters[^1] = sigma;
            double[] residuals = ReadDoubleArray(testCase.GetProperty("residuals"));
            double[] expectedPointwise = residuals
                .Select(residual => IndependentGaussianLogDensity(residual, sigma))
                .ToArray();
            double expectedData = testCase.GetProperty("expected_data_log_likelihood").GetDouble();
            double expectedPrior = testCase.GetProperty("expected_prior_log_likelihood").GetDouble();

            AssertArrayEqual(expectedPointwise, model.PointwiseDataLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(expectedData, expectedPointwise.Sum(), tolerance, $"{modelName} oracle total");
            Assert.AreEqual(expectedData, model.DataLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(
                expectedData,
                model.PointwiseDataLogLikelihoodComponents(parameters).Sum(component => component.LogLikelihood),
                tolerance,
                modelName);
            Assert.AreEqual(expectedPrior, model.PriorLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(expectedPriorFromFormula, expectedPrior, tolerance, $"{modelName} prior oracle");
            Assert.AreEqual(
                expectedPrior,
                model.PointwisePriorLogLikelihood(parameters).Sum(component => component.LogLikelihood),
                tolerance,
                modelName);

            foreach (double invalidScale in ReadInvalidScales(oracle.GetProperty("invalid_scales")))
            {
                double[] invalid = (double[])parameters.Clone();
                invalid[^1] = invalidScale;
                Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(invalid), modelName);
                Assert.AreEqual(double.NegativeInfinity, model.PriorLogLikelihood(invalid), modelName);
                Assert.IsTrue(model.PointwiseDataLogLikelihood(invalid)
                    .All(value => value == double.NegativeInfinity), modelName);
                Assert.IsTrue(model.PointwiseDataLogLikelihoodComponents(invalid)
                    .All(component => component.LogLikelihood == double.NegativeInfinity), modelName);
                Assert.AreEqual(
                    double.NegativeInfinity,
                    model.PointwisePriorLogLikelihood(invalid).Sum(component => component.LogLikelihood),
                    modelName);
            }
        }
    }

    /// <summary>
    /// Verifies automatic Box-Cox and Yeo-Johnson exponents use only the raw training prefix for
    /// all four time-series models and match the independently implemented R profile oracle.
    /// </summary>
    /// <remarks>
    /// The committed artifact fixes the raw values, six-observation training boundary, R/package
    /// versions, and acceptance rule before C# evaluation. Acceptance is 1E-8 absolute or 1E-7
    /// relative. Altering the three-observation holdout tail must not change the exponent,
    /// transformed training values, or conditional likelihood.
    /// </remarks>
    [TestMethod]
    public void TransformLambdaMatchesIndependentTrainingOnlyOracle()
    {
        JsonElement oracle = LoadOracle("phase5-transform-lambda-oracle.json");
        JsonElement tolerances = oracle.GetProperty("metadata").GetProperty("tolerances");
        double absoluteTolerance = tolerances.GetProperty("cross_language_absolute").GetDouble();
        double relativeTolerance = tolerances.GetProperty("cross_language_relative").GetDouble();

        foreach (string transformName in new[] { "box_cox", "yeo_johnson" })
        {
            JsonElement transformCase = oracle.GetProperty("fitted").GetProperty(transformName);
            double[] raw = ReadDoubleArray(transformCase.GetProperty("raw"));
            double[] alternateHoldout = ReadDoubleArray(transformCase.GetProperty("alternate_holdout"));
            double[] mutated = raw.Take(6).Concat(alternateHoldout).ToArray();
            double expectedLambda = transformCase.GetProperty("expected_lambda").GetDouble();
            double[] expectedTraining = ReadDoubleArray(transformCase.GetProperty("expected_training_transformed"));
            Transform transform = transformName == "box_cox" ? Transform.BoxCox : Transform.YeoJohnson;

            foreach (string modelName in new[] { nameof(AutoRegressive), nameof(MovingAverage), nameof(ARIMA), nameof(ARIMAX) })
            {
                ModelBase model = CreateAutomaticallyTransformedModel(modelName, raw, transform, 6);
                ModelBase holdoutMutation = CreateAutomaticallyTransformedModel(modelName, mutated, transform, 6);
                double actualLambda = GetTransformLambda(model);

                AssertClose(expectedLambda, actualLambda, absoluteTolerance, relativeTolerance, $"{modelName} {transform}");
                AssertClose(expectedLambda, GetTransformLambda(holdoutMutation), absoluteTolerance, relativeTolerance, $"{modelName} {transform} holdout");
                AssertArrayClose(expectedTraining, GetTrainingValues(model), absoluteTolerance, relativeTolerance, $"{modelName} {transform} training");
                AssertArrayClose(GetTrainingValues(model), GetTrainingValues(holdoutMutation), 1E-12, 0.0, $"{modelName} {transform} holdout state");
                Assert.AreEqual(
                    model.DataLogLikelihood(CreateFixedTimeSeriesParameters(model)),
                    holdoutMutation.DataLogLikelihood(CreateFixedTimeSeriesParameters(holdoutMutation)),
                    1E-12,
                    $"{modelName} {transform} holdout likelihood");

                XElement fittedXml = model.ToXElement();
                Assert.AreEqual("False", fittedXml.Attribute("TransformLambdaIsManual")?.Value, modelName);
                ModelBase restored = RestoreTimeSeriesModel(modelName, CreateSeries(raw), fittedXml);
                Assert.AreEqual(actualLambda, GetTransformLambda(restored), 1E-12, $"{modelName} restored fitted value");

                SetTrainingSteps(restored, 7);
                Assert.AreNotEqual(actualLambda, GetTransformLambda(restored), $"{modelName} automatic training-window refit");
            }
        }
    }

    /// <summary>
    /// Verifies a manual Yeo-Johnson assignment atomically rebuilds transformed observations,
    /// residual recurrences, Jacobians, and conditional likelihoods against the independent R
    /// oracle for AR, MA, ARIMA, and ARIMAX.
    /// </summary>
    /// <remarks>
    /// The fixed fixture uses ten raw values, eight training observations, lambda 0.6, AR
    /// coefficient 0.35, MA coefficient -0.25, and innovation scale 0.8. Cross-language
    /// acceptance is 1E-8 absolute or 1E-7 relative; serialization state identities use 1E-12.
    /// </remarks>
    [TestMethod]
    public void ManualTransformLambdaRebuildMatchesIndependentLikelihoodOracle()
    {
        JsonElement oracle = LoadOracle("phase5-transform-lambda-oracle.json");
        JsonElement tolerances = oracle.GetProperty("metadata").GetProperty("tolerances");
        double absoluteTolerance = tolerances.GetProperty("cross_language_absolute").GetDouble();
        double relativeTolerance = tolerances.GetProperty("cross_language_relative").GetDouble();
        JsonElement manual = oracle.GetProperty("manual");
        double[] raw = ReadDoubleArray(manual.GetProperty("raw"));
        int trainingSteps = manual.GetProperty("training_steps").GetInt32();
        double lambda = manual.GetProperty("lambda").GetDouble();
        double phi = manual.GetProperty("phi").GetDouble();
        double theta = manual.GetProperty("theta").GetDouble();
        double sigma = manual.GetProperty("sigma").GetDouble();
        double[] expectedTraining = ReadDoubleArray(manual.GetProperty("expected_full_transformed"))
            .Take(trainingSteps)
            .ToArray();
        double[] expectedArResiduals = ReadDoubleArray(manual.GetProperty("expected_ar_residuals"));
        double[] expectedMaResiduals = ReadDoubleArray(manual.GetProperty("expected_ma_residuals"));
        double expectedArLikelihood = manual.GetProperty("expected_ar_log_likelihood").GetDouble();
        double expectedMaLikelihood = manual.GetProperty("expected_ma_log_likelihood").GetDouble();

        foreach (string modelName in new[] { nameof(AutoRegressive), nameof(MovingAverage), nameof(ARIMA), nameof(ARIMAX) })
        {
            ModelBase model = CreateManuallyTransformedModel(modelName, raw, lambda, trainingSteps);
            AssertArrayClose(expectedTraining, GetTrainingValues(model), absoluteTolerance, relativeTolerance, $"{modelName} transformed");

            double[] parameters = modelName == nameof(MovingAverage)
                ? new[] { theta, sigma }
                : new[] { phi, sigma };
            double[] residuals = GetResiduals(model, parameters);
            double[] expectedResiduals = modelName == nameof(MovingAverage) ? expectedMaResiduals : expectedArResiduals;
            if (modelName != nameof(MovingAverage))
                residuals = residuals.Skip(1).ToArray();
            AssertArrayClose(expectedResiduals, residuals, absoluteTolerance, relativeTolerance, $"{modelName} residuals");

            double expectedLikelihood = modelName == nameof(MovingAverage) ? expectedMaLikelihood : expectedArLikelihood;
            AssertClose(expectedLikelihood, model.DataLogLikelihood(parameters), absoluteTolerance, relativeTolerance, $"{modelName} likelihood");

            XElement xml = model.ToXElement();
            ModelBase restored = RestoreTimeSeriesModel(modelName, CreateSeries(raw), xml);
            Assert.AreEqual(lambda, GetTransformLambda(restored), 1E-12, $"{modelName} restored lambda");
            AssertClose(expectedLikelihood, restored.DataLogLikelihood(parameters), absoluteTolerance, relativeTolerance, $"{modelName} restored likelihood");
        }
    }

    /// <summary>
    /// Verifies ARIMAX differencing, conditional likelihood, transformation Jacobian, and
    /// level-covariate alignment against the independently generated R oracle for
    /// differencing orders zero, one, and two.
    /// </summary>
    /// <remarks>
    /// Model step <c>k</c> must map to raw response index <c>k+d</c>. The level covariate is
    /// selected by the exact later-response timestamp and is never differenced. The committed
    /// oracle fixes the Box-Cox exponent, training boundary, parameters, and 1E-10 absolute
    /// tolerance before C# evaluation.
    /// </remarks>
    [TestMethod]
    public void ArimaxDifferencedLikelihoodMatchesDateIndexedIndependentOracle()
    {
        JsonElement oracle = LoadOracle("phase5-arimax-alignment-oracle.json");
        JsonElement metadata = oracle.GetProperty("metadata");
        JsonElement fixture = oracle.GetProperty("fixture");
        double tolerance = metadata.GetProperty("tolerance_absolute").GetDouble();
        DateTime startDate = DateTime.Parse(fixture.GetProperty("dates")[0].GetString()!);
        double[] raw = ReadDoubleArray(fixture.GetProperty("raw"));
        double[] alternateHoldout = ReadDoubleArray(fixture.GetProperty("alternate_holdout"));
        double[] covariate = ReadDoubleArray(fixture.GetProperty("covariate_values"));
        int trainingSteps = fixture.GetProperty("training_steps").GetInt32();
        double lambda = fixture.GetProperty("lambda").GetDouble();
        double[] parameters =
        {
            fixture.GetProperty("intercept").GetDouble(),
            fixture.GetProperty("beta").GetDouble(),
            fixture.GetProperty("phi").GetDouble(),
            fixture.GetProperty("theta").GetDouble(),
            fixture.GetProperty("sigma").GetDouble(),
        };

        foreach (JsonElement testCase in oracle.GetProperty("cases").EnumerateArray())
        {
            int differencingOrder = testCase.GetProperty("differencing_order").GetInt32();
            string context = $"d={differencingOrder}";
            ARIMAX model = CreateAlignedArimax(raw, covariate, startDate, trainingSteps, differencingOrder, lambda);

            Assert.AreEqual(
                testCase.GetProperty("training_difference_count").GetInt32(),
                model.TrainingTimeSeries.Count,
                $"{context} training count");
            AssertArrayEqual(
                ReadDoubleArray(testCase.GetProperty("training_difference_values")),
                model.TrainingTimeSeries.ValuesToArray(),
                tolerance,
                $"{context} training differences");

            string[] expectedDates = testCase.GetProperty("difference_dates")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();
            Assert.AreEqual(expectedDates.Length, model.DifferencedSeries.Count, $"{context} full date count");
            for (int i = 0; i < expectedDates.Length; i++)
                Assert.AreEqual(DateTime.Parse(expectedDates[i]), model.DifferencedSeries[i].Index, $"{context} date {i}");

            double[] expectedResiduals = ReadDoubleArray(testCase.GetProperty("residuals"));
            double[] expectedPointwise = ReadDoubleArray(testCase.GetProperty("pointwise_log_likelihood"));
            double[] actualResiduals = model.Residuals(parameters);
            double[] actualPointwise = model.PointwiseDataLogLikelihood(parameters);
            List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);
            AssertArrayEqual(expectedResiduals, actualResiduals, tolerance, $"{context} residuals");
            AssertArrayEqual(expectedPointwise, actualPointwise, tolerance, $"{context} pointwise");
            Assert.AreEqual(expectedPointwise.Length, components.Count, $"{context} component count");
            AssertArrayEqual(
                expectedPointwise,
                components.Select(component => component.LogLikelihood).ToArray(),
                tolerance,
                $"{context} components");
            Assert.AreEqual(
                testCase.GetProperty("log_likelihood").GetDouble(),
                model.DataLogLikelihood(parameters),
                tolerance,
                $"{context} scalar likelihood");

            double[] mutatedRaw = raw.Take(trainingSteps).Concat(alternateHoldout).ToArray();
            ARIMAX holdoutMutation = CreateAlignedArimax(
                mutatedRaw,
                covariate,
                startDate,
                trainingSteps,
                differencingOrder,
                lambda);
            AssertArrayEqual(
                model.TrainingTimeSeries.ValuesToArray(),
                holdoutMutation.TrainingTimeSeries.ValuesToArray(),
                1E-12,
                $"{context} holdout training isolation");
            Assert.AreEqual(
                model.DataLogLikelihood(parameters),
                holdoutMutation.DataLogLikelihood(parameters),
                1E-12,
                $"{context} holdout likelihood isolation");
        }
    }

    /// <summary>
    /// Verifies ARIMA and ARIMAX prediction reconstruction against hand-evaluated first- and
    /// second-difference recurrences, including the training boundary, inverse transformation,
    /// and component alignment.
    /// </summary>
    /// <remarks>
    /// The fixtures intentionally depart from their fitted recurrences inside training. Each
    /// fitted level therefore has to use the preceding observed state, the first forecast has to
    /// use the final observed training state, and only later forecasts may recurse from predicted
    /// states. A holdout sentinel proves the reconstruction cannot start from or consume the
    /// holdout response. The absolute acceptance tolerance is fixed at 1E-10.
    /// </remarks>
    [TestMethod]
    public void ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle()
    {
        const double tolerance = 1E-10;
        DateTime startDate = new(2002, 3, 4);

        double[] irregular = { 1, 4, 10, 999 };
        var arima = new ARIMA(CreateDailySeries(irregular, startDate), 0, 2, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = Transform.None,
        };
        arima.TrainingTimeSteps = 3;
        var arimaPrediction = arima.Predict(new[] { 2.0, 1.0 }, 2, -1);
        double[] expectedArima = { 1, 4, 9, 18, 28 };
        AssertArrayEqual(expectedArima, arimaPrediction.Y, tolerance, "ARIMA d=2 conditional levels");
        AssertArrayEqual(
            new[] { 0.0, 0.0, 2, 2, 2 },
            arimaPrediction.InterceptPart,
            tolerance,
            "ARIMA d=2 component map");

        double[] transformed = { 1.0, 1.5, 1.6, 9.0 };
        double[] logarithmic = transformed.Select(Math.Exp).ToArray();
        var arimax = new ARIMAX
        {
            IncludeIntercept = false,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
            TransformType = Transform.Logarithmic,
        };
        arimax.TimeSeries = CreateDailySeries(logarithmic, startDate);
        arimax.TrainingTimeSteps = 3;
        arimax.SetCovariates(new List<Numerics.Data.TimeSeries>
        {
            CreateDailySeries(new[] { 999.0, 0.1, 0.1, 0.1, 0.1 }, startDate),
        });

        var arimaxPrediction = arimax.Predict(new[] { 1.0, 0.25 }, 2, -1);
        double[] expectedLogarithmic = new[] { 1.0, 1.1, 1.6, 1.7, 1.8 }
            .Select(Math.Exp)
            .ToArray();
        AssertArrayEqual(expectedLogarithmic, arimaxPrediction.Y, tolerance, "ARIMAX log d=1 conditional levels");
        AssertArrayEqual(
            new[] { 0.0, 0.1, 0.1, 0.1, 0.1 },
            arimaxPrediction.CovariatePart,
            tolerance,
            "ARIMAX d=1 component map");
    }

    /// <summary>
    /// Verifies differenced prediction uncertainty is conditional inside training and begins
    /// recursive accumulation only after the training/forecast boundary.
    /// </summary>
    /// <remarks>
    /// For an ARIMA(0,1,0) process with unit innovation scale, every conditional training value
    /// and the first forecast have variance one. Forecast horizons two and three have variances
    /// two and three because only forecast innovations accumulate. The same analytical oracle is
    /// applied to ARIMA and ARIMAX with exactly 1,000 fixed seeds. Mean and variance acceptance
    /// bounds are four Monte Carlo standard errors, with the existing three-percent variance
    /// floor.
    /// </remarks>
    [TestMethod]
    public void ArimaAndArimaxPredictionUncertaintyBeginsAtForecastBoundary()
    {
        const int realizationCount = 1000;
        const int trainingSteps = 4;
        const int forecastSteps = 3;
        DateTime startDate = new(2002, 3, 4);
        double[] raw = { 10, 14, 15, 20, 999 };

        var arima = new ARIMA(CreateDailySeries(raw, startDate), 0, 1, 0, false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = Transform.None,
        };
        arima.TrainingTimeSteps = trainingSteps;

        var arimax = new ARIMAX
        {
            IncludeIntercept = false,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
            TransformType = Transform.None,
        };
        arimax.TimeSeries = CreateDailySeries(raw, startDate);
        arimax.TrainingTimeSteps = trainingSteps;

        double[][] arimaSamples = Enumerable.Range(0, realizationCount)
            .Select(seed => arima.Predict(new[] { 1.0 }, forecastSteps, seed).Y)
            .ToArray();
        double[][] arimaxSamples = Enumerable.Range(0, realizationCount)
            .Select(seed => arimax.Predict(new[] { 1.0 }, forecastSteps, seed).Y)
            .ToArray();

        AssertConditionalPredictionMoments(arimaSamples, raw, trainingSteps, "ARIMA");
        AssertConditionalPredictionMoments(arimaxSamples, raw, trainingSteps, "ARIMAX");
    }

    /// <summary>
    /// Verifies transformed ARIMA and ARIMAX forecasts recurse exclusively on transformed
    /// differences, integrate transformed levels, and inverse-transform the completed path once.
    /// </summary>
    /// <remarks>
    /// A hand-coded Yeo-Johnson and ARMA(1,1) oracle supplies the deterministic conditional path.
    /// The raw fixture contains values whose scale is deliberately far from the transformed scale,
    /// so a raw lag entering either recurrence cannot satisfy the 1E-10 absolute tolerance. The
    /// stochastic check uses exactly 1,000 fixed seeds. At the final horizon, transformed forecast
    /// mean and variance must match the analytical accumulated ARMA impulse-response moments within
    /// four Monte Carlo standard errors, with the established three-percent variance floor.
    /// </remarks>
    [TestMethod]
    public void TransformedArimaAndArimaxForecastsMatchModelScaleOracle()
    {
        const int realizationCount = 1000;
        const int trainingSteps = 5;
        const int forecastSteps = 5;
        const double lambda = 0.04;
        const double mu = 0.02;
        const double phi = -0.2;
        const double theta = 0.35;
        const double sigma = 0.12;
        const double tolerance = 1E-10;
        DateTime startDate = new(2002, 3, 4);

        double[] transformed = { 6.0, 6.15, 6.11, 6.20, 6.18, 8.0 };
        double[] raw = transformed
            .Select(value => IndependentYeoJohnsonInverse(value, lambda))
            .ToArray();
        double[] parameters = { mu, phi, theta, sigma };
        double[] expectedTransformed = IndependentConditionalArmaD1Prediction(
            raw,
            trainingSteps,
            forecastSteps,
            lambda,
            mu,
            phi,
            theta);

        var arima = new ARIMA
        {
            POrder = 1,
            DOrder = 1,
            QOrder = 1,
            IncludeIntercept = true,
            TransformType = Transform.YeoJohnson,
        };
        arima.SetTransformParameters(lambda, double.NaN);
        arima.TimeSeries = CreateDailySeries(raw, startDate);
        arima.UseDefaultTrainingSteps = false;
        arima.TrainingTimeSteps = trainingSteps;

        var arimax = new ARIMAX
        {
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            IncludeIntercept = true,
            TransformType = Transform.YeoJohnson,
        };
        arimax.SetTransformParameters(lambda, double.NaN);
        arimax.TimeSeries = CreateDailySeries(raw, startDate);
        arimax.UseDefaultTrainingSteps = false;
        arimax.TrainingTimeSteps = trainingSteps;

        double[] arimaDeterministic = arima.Predict(parameters, forecastSteps, -1).Y
            .Select(value => IndependentYeoJohnsonTransform(value, lambda))
            .ToArray();
        double[] arimaxDeterministic = arimax.Predict(parameters, forecastSteps, -1).Y
            .Select(value => IndependentYeoJohnsonTransform(value, lambda))
            .ToArray();
        AssertArrayEqual(expectedTransformed, arimaDeterministic, tolerance, "ARIMA transformed recurrence");
        AssertArrayEqual(expectedTransformed, arimaxDeterministic, tolerance, "ARIMAX transformed recurrence");

        double[] arimaFinal = Enumerable.Range(0, realizationCount)
            .Select(seed => IndependentYeoJohnsonTransform(
                arima.Predict(parameters, forecastSteps, seed).Y[^1],
                lambda))
            .ToArray();
        double[] arimaxFinal = Enumerable.Range(0, realizationCount)
            .Select(seed => IndependentYeoJohnsonTransform(
                arimax.Predict(parameters, forecastSteps, seed).Y[^1],
                lambda))
            .ToArray();
        double expectedStandardDeviation = Math.Sqrt(
            IndependentIntegratedArmaForecastVariance(forecastSteps, phi, theta, sigma));
        double expectedMean = expectedTransformed[^1];
        AssertIndependentGaussianMoments(arimaFinal, expectedMean, expectedStandardDeviation, "ARIMA transformed horizon five");
        AssertIndependentGaussianMoments(arimaxFinal, expectedMean, expectedStandardDeviation, "ARIMAX transformed horizon five");
    }

    /// <summary>
    /// Verifies transformed AR and MA generators against fixed model-scale recurrences and
    /// independently evaluated inverse-transform and Monte Carlo moment oracles.
    /// </summary>
    /// <remarks>
    /// The algebraic acceptance tolerance is 1E-10. Each Monte Carlo check uses 1,000 seeded,
    /// independent model-scale values. The sample mean must be within four standard errors and
    /// the sample variance within the larger of four standard errors or three percent relative.
    /// </remarks>
    [TestMethod]
    public void ArAndMaTransformedGeneratorsMatchIndependentOracle()
    {
        const double algebraicTolerance = 1E-10;
        double[] arModelScale =
        {
            0.5275903734293788, 0.19367432545682162, 0.598464731025482,
            0.8573412905998781, 0.3188682234957716, 0.9177848551790461,
            2.6350006057479343, 2.635215407976874,
        };
        var ar = new AutoRegressive
        {
            Order = 2,
            IncludeIntercept = true,
            TransformType = Transform.Logarithmic,
        };
        ar.SetParameterValues(new[] { 1.25, 0.35, -0.15, 0.75 });
        AssertArrayEqual(
            arModelScale.Select(value => Math.Exp(value)).ToArray(),
            ar.GenerateRandomValues(8, 13579),
            algebraicTolerance,
            "AR logarithmic algebra");

        const double boxCoxLambda = 0.5;
        double[] maModelScale =
        {
            1.0227687685036484, 0.4974530014652585, 1.1363812293685684,
            0.8964366966232473, 1.3833833036998602, 0.6973654912089674,
            0.15873803749631565, 0.2074704533451301,
        };
        var ma = new MovingAverage
        {
            Order = 2,
            IncludeIntercept = true,
            TransformType = Transform.BoxCox,
        };
        ma.SetTransformParameters(boxCoxLambda, double.NaN);
        ma.SetParameterValues(new[] { 0.75, -0.2, 0.3, 0.6 });
        AssertArrayEqual(
            maModelScale.Select(value => IndependentBoxCoxInverse(value, boxCoxLambda)).ToArray(),
            ma.GenerateRandomValues(8, 13580),
            algebraicTolerance,
            "MA Box-Cox algebra");

        const int realizationCount = 1000;
        const double mean = 0.2;
        const double sigma = 0.6;
        var iidAr = new AutoRegressive
        {
            Order = 1,
            IncludeIntercept = true,
            TransformType = Transform.Logarithmic,
        };
        iidAr.SetParameterValues(new[] { mean, 0.0, sigma });
        double[] arModelValues = iidAr.GenerateRandomValues(realizationCount, 52037)
            .Select(value => Math.Log(value))
            .ToArray();
        AssertIndependentGaussianMoments(arModelValues, mean, sigma, "AR model-scale moments");

        const double yeoJohnsonLambda = 0.6;
        var iidMa = new MovingAverage
        {
            Order = 1,
            IncludeIntercept = true,
            TransformType = Transform.YeoJohnson,
        };
        iidMa.SetTransformParameters(yeoJohnsonLambda, double.NaN);
        iidMa.SetParameterValues(new[] { mean, 0.0, sigma });
        double[] maModelValues = iidMa.GenerateRandomValues(realizationCount, 52038)
            .Select(value => IndependentYeoJohnsonTransform(value, yeoJohnsonLambda))
            .ToArray();
        AssertIndependentGaussianMoments(maModelValues, mean, sigma, "MA model-scale moments");
    }

    /// <summary>
    /// Verifies differenced/transformed ARIMA generation against fixed recurrence, anchor,
    /// inverse-transform, and model-scale Monte Carlo moment oracles.
    /// </summary>
    /// <remarks>
    /// The fixed algebraic tolerance is 1E-10. The stochastic check generates exactly 1,000
    /// raw steps and evaluates their 999 independently generated first differences using the
    /// same four-standard-error/three-percent acceptance rule as the AR/MA method.
    /// </remarks>
    [TestMethod]
    public void ArimaDifferencedTransformedGeneratorMatchesIndependentOracle()
    {
        const double tolerance = 1E-10;
        const double lambda = 0.6;
        DateTime startDate = new(2004, 5, 6);
        double[] modelDifferences =
        {
            1.5504888464633453, 2.245464762138672, 0.2062780780166804,
            0.49593324179260784, 0.9224018499700285, 0.8145819937498172,
            2.459490777492791,
        };
        double[] transformedData = { 2, 3, 4, 5, 6 };
        double[] rawData = transformedData
            .Select(value => IndependentYeoJohnsonInverse(value, lambda))
            .ToArray();
        var arima = new ARIMA(CreateDailySeries(rawData, startDate), 1, 1, 1, true)
        {
            TransformType = Transform.YeoJohnson,
        };
        arima.SetTransformParameters(lambda, double.NaN);
        arima.SetParameterValues(new[] { 1.1, 0.4, -0.25, 0.7 });

        var expectedTransformed = new double[8];
        expectedTransformed[0] = transformedData[0];
        for (int i = 1; i < expectedTransformed.Length; i++)
            expectedTransformed[i] = expectedTransformed[i - 1] + modelDifferences[i - 1];
        AssertArrayEqual(
            expectedTransformed.Select(value => IndependentYeoJohnsonInverse(value, lambda)).ToArray(),
            arima.GenerateRandomValues(8, 13581),
            tolerance,
            "ARIMA transformed recurrence");

        const int generatedStepCount = 1000;
        const double mean = 0.2;
        const double sigma = 0.5;
        var iidArima = new ARIMA
        {
            POrder = 0,
            DOrder = 1,
            QOrder = 0,
            IncludeIntercept = true,
            TransformType = Transform.Logarithmic,
        };
        iidArima.SetParameterValues(new[] { mean, sigma });
        double[] generated = iidArima.GenerateRandomValues(generatedStepCount, 52039);
        double[] transformed = generated.Select(value => Math.Log(value)).ToArray();
        var differences = new double[generatedStepCount - 1];
        for (int i = 0; i < differences.Length; i++)
            differences[i] = transformed[i + 1] - transformed[i];
        AssertIndependentGaussianMoments(differences, mean, sigma, "ARIMA difference moments");
    }

    /// <summary>
    /// Verifies transformed/differenced ARIMAX generation against independent recurrence,
    /// exact-date level-covariate, integration, inverse-transform, and Gaussian-moment oracles.
    /// </summary>
    /// <remarks>
    /// The fixed algebraic tolerance is 1E-10. The stochastic check requests exactly 1,000 raw
    /// generated steps and evaluates the 999 independent innovations implied by first differencing,
    /// using the predeclared four-standard-error/three-percent moment rule.
    /// </remarks>
    [TestMethod]
    public void ArimaxTransformedDifferencedGeneratorMatchesIndependentOracle()
    {
        const double tolerance = 1E-10;
        const double lambda = 0.6;
        DateTime startDate = new(2005, 6, 7);
        double[] modelDifferences =
        {
            2.2620632327954455, -0.6690412732355178, 2.2364682873083486,
            2.718481409956337, -3.097232432613497, 1.0828631290683435,
            3.8879909176213205,
        };
        double[] transformedData = { 2, 3, 4, 5, 6 };
        double[] rawData = transformedData
            .Select(value => IndependentYeoJohnsonInverse(value, lambda))
            .ToArray();
        var arimax = new ARIMAX(CreateDailySeries(rawData, startDate))
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            TransformType = Transform.YeoJohnson,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
        };
        arimax.SetCovariates(new List<Numerics.Data.TimeSeries>
        {
            CreateDailySeries(new[] { 99.0, 2, -1, 0.5, 3, -2, 1.5, 4.0 }, startDate),
        });
        arimax.SetDefaultParameters();
        arimax.SetTransformParameters(lambda, double.NaN);
        arimax.SetParameterValues(new[] { 0.25, 1.1, 0.3, -0.2, 0.75 });

        var expectedTransformed = new double[8];
        expectedTransformed[0] = transformedData[0];
        for (int i = 1; i < expectedTransformed.Length; i++)
            expectedTransformed[i] = expectedTransformed[i - 1] + modelDifferences[i - 1];
        AssertArrayEqual(
            expectedTransformed.Select(value => IndependentYeoJohnsonInverse(value, lambda)).ToArray(),
            arimax.GenerateRandomValues(8, 24682),
            tolerance,
            "ARIMAX transformed/differenced recurrence");

        const int generatedStepCount = 1000;
        const double intercept = 0.02;
        const double beta = 0.05;
        const double sigma = 0.4;
        double[] covariate = Enumerable.Range(0, generatedStepCount)
            .Select(index => index % 2 == 0 ? -1.0 : 1.0)
            .ToArray();
        var iidArimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            TransformType = Transform.Logarithmic,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
        };
        iidArimax.SetCovariates(new List<Numerics.Data.TimeSeries>
        {
            CreateDailySeries(covariate, startDate),
        });
        iidArimax.SetDefaultParameters();
        iidArimax.SetParameterValues(new[] { intercept, beta, sigma });

        double[] generated = iidArimax.GenerateRandomValues(generatedStepCount, 52040);
        double[] transformed = generated.Select(value => Math.Log(value)).ToArray();
        var innovations = new double[generatedStepCount - 1];
        for (int i = 0; i < innovations.Length; i++)
        {
            int rawIndex = i + 1;
            innovations[i] = transformed[rawIndex] - transformed[rawIndex - 1] -
                intercept - beta * covariate[rawIndex];
        }
        AssertIndependentGaussianMoments(innovations, 0.0, sigma, "ARIMAX innovation moments");
    }

    /// <summary>
    /// Verifies AR, MA, ARIMA, ARIMAX, and rating-curve AIC/BIC use data likelihood at the stored
    /// MAP, exclude prior density, and that the production MLE and flat-prior MAP estimators reach
    /// the analytical Gaussian optimum.
    /// </summary>
    /// <remarks>
    /// Criteria use 1E-10 absolute acceptance. The four time-series criterion cells compare the
    /// production data log-likelihood with an independent iid Gaussian evaluation of the training
    /// window before forming the criteria; the rating-curve cell is a routing check on the
    /// production likelihood. The flat-prior Gaussian cell runs the production MLE and MAP
    /// estimators from the model defaults and accepts the analytical optimum and MAP/MLE parity at
    /// 1E-3. No sampler, simulation, or time-series fixture above 1,000 steps is invoked;
    /// time-series analyses use 40 observations and one injected posterior row.
    /// </remarks>
    [TestMethod]
    public async Task InformationCriteriaUseDataLikelihoodAtMapAndExcludePrior()
    {
        const double tolerance = 1E-10;
        DateTime startDate = new(2002, 3, 4);
        double[] responsePattern = { 9, 11, 10, 12, 8, 10 };
        double[] responseValues = Enumerable.Range(0, 40)
            .Select(index => responsePattern[index % responsePattern.Length])
            .ToArray();
        double[] timeSeriesMap = { 10.0, 1.5 };

        var ar = new AutoRegressive(CreateDailySeries(responseValues, startDate), 0, true)
        {
            UseJeffreysRuleForScale = true,
        };
        await VerifyAnalysisCriteriaAsync(
            "AR",
            new ARAnalysis(ar),
            ar,
            timeSeriesMap,
            responseValues.Length,
            tolerance,
            IndependentIidGaussianLogLikelihood(responseValues, ar.TrainingTimeSteps, timeSeriesMap));

        var ma = new MovingAverage(CreateDailySeries(responseValues, startDate), 0, true)
        {
            UseJeffreysRuleForScale = true,
        };
        await VerifyAnalysisCriteriaAsync(
            "MA",
            new MAAnalysis(ma),
            ma,
            timeSeriesMap,
            responseValues.Length,
            tolerance,
            IndependentIidGaussianLogLikelihood(responseValues, ma.TrainingTimeSteps, timeSeriesMap));

        var arima = new ARIMA(CreateDailySeries(responseValues, startDate), 0, 0, 0, true)
        {
            UseJeffreysRuleForScale = true,
        };
        await VerifyAnalysisCriteriaAsync(
            "ARIMA",
            new ARIMAAnalysis(arima),
            arima,
            timeSeriesMap,
            responseValues.Length,
            tolerance,
            IndependentIidGaussianLogLikelihood(responseValues, arima.TrainingTimeSteps, timeSeriesMap));

        var arimax = new ARIMAX(CreateDailySeries(responseValues, startDate))
        {
            IncludeIntercept = true,
            AROrderP = 0,
            DiffOrderD = 0,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = true,
        };
        arimax.SetDefaultParameters();
        await VerifyAnalysisCriteriaAsync(
            "ARIMAX",
            new ARIMAXAnalysis(arimax),
            arimax,
            timeSeriesMap,
            arimax.TrainingTimeSteps,
            tolerance,
            IndependentIidGaussianLogLikelihood(responseValues, arimax.TrainingTimeSteps, timeSeriesMap));

        double[] stages = Enumerable.Range(0, 20)
            .Select(index => 1.0 + 0.25 * index)
            .ToArray();
        double[] ratingMap = { 0.5, 1.0, 1.5, 0.05 };
        double[] discharges = stages
            .Select(stage => 10.0 * Math.Pow(stage - ratingMap[0], ratingMap[2]))
            .ToArray();
        var ratingCurve = new RatingCurveModel(
            CreateDailySeries(stages, startDate),
            CreateDailySeries(discharges, startDate),
            numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = true,
        };
        await VerifyAnalysisCriteriaAsync(
            "RatingCurve",
            new RatingCurveAnalysis(ratingCurve),
            ratingCurve,
            ratingMap,
            stages.Length,
            tolerance);

        var flatModel = new AutoRegressive(CreateDailySeries(responseValues, startDate), 0, true)
        {
            UseDefaultFlatPriors = true,
            UseJeffreysRuleForScale = false,
        };
        flatModel.SetDefaultParameters();
        double[] flatTrainingValues = responseValues
            .Take(flatModel.TrainingTimeSteps)
            .ToArray();
        double analyticalMean = flatTrainingValues.Average();
        double analyticalSigma = Math.Sqrt(flatTrainingValues
            .Select(value => Math.Pow(value - analyticalMean, 2.0))
            .Average());
        double[] analyticalOptimum = { analyticalMean, analyticalSigma };
        AssertLocalGaussianOptimum(flatModel, analyticalOptimum, usePosterior: false);
        AssertLocalGaussianOptimum(flatModel, analyticalOptimum, usePosterior: true);

        // The production estimators must reach the analytical optimum from the model defaults,
        // and under flat priors the MAP must coincide with the MLE. The 1E-3 acceptance reflects
        // the global optimizer's convergence tolerance on a quadratic objective.
        var mle = new MaximumLikelihood(flatModel);
        mle.Estimate();
        var map = new MaximumAPosteriori(flatModel);
        map.Estimate();
        Assert.IsTrue(mle.IsEstimated, "Flat-prior MLE estimation.");
        Assert.IsTrue(map.IsEstimated, "Flat-prior MAP estimation.");
        AssertArrayEqual(analyticalOptimum, mle.BestParameterSet.Values, 1E-3, "Production MLE versus analytical optimum");
        AssertArrayEqual(analyticalOptimum, map.BestParameterSet.Values, 1E-3, "Production MAP versus analytical optimum");
        AssertArrayEqual(mle.BestParameterSet.Values, map.BestParameterSet.Values, 1E-3, "Flat-prior MAP/MLE parity");
    }

    /// <summary>
    /// Verifies one concrete analysis result builder against hand AIC/BIC formulas.
    /// </summary>
    /// <param name="label">The model label.</param>
    /// <param name="analysis">The concrete analysis instance.</param>
    /// <param name="model">The concrete model instance.</param>
    /// <param name="mapValues">The injected stored MAP parameters.</param>
    /// <param name="sampleSize">The analysis-specific BIC sample size.</param>
    /// <param name="tolerance">The fixed criterion tolerance.</param>
    /// <param name="independentDataLogLikelihood">
    /// The independently computed data log-likelihood at the stored MAP, or null when the
    /// model's likelihood is not evaluated independently and the check is a routing check only.
    /// </param>
    /// <returns>A task representing deterministic result construction.</returns>
    private static async Task VerifyAnalysisCriteriaAsync(
        string label,
        object analysis,
        ModelBase model,
        double[] mapValues,
        int sampleSize,
        double tolerance,
        double? independentDataLogLikelihood = null)
    {
        double dataLogLikelihood = model.DataLogLikelihood(mapValues);
        double priorLogLikelihood = model.PriorLogLikelihood(mapValues);
        Assert.IsTrue(double.IsFinite(dataLogLikelihood), $"{label} data likelihood must be finite.");
        if (independentDataLogLikelihood.HasValue)
        {
            Assert.AreEqual(
                independentDataLogLikelihood.Value,
                dataLogLikelihood,
                tolerance,
                $"{label} data likelihood versus the independent Gaussian evaluation.");
            dataLogLikelihood = independentDataLogLikelihood.Value;
        }
        Assert.IsTrue(Math.Abs(priorLogLikelihood) > 1E-6, $"{label} prior fixture must distinguish the posterior kernel.");

        BayesianAnalysis bayesian = analysis switch
        {
            ARAnalysis value => value.BayesianAnalysis,
            MAAnalysis value => value.BayesianAnalysis,
            ARIMAAnalysis value => value.BayesianAnalysis,
            ARIMAXAnalysis value => value.BayesianAnalysis,
            RatingCurveAnalysis value => value.BayesianAnalysis,
            _ => throw new ArgumentOutOfRangeException(nameof(analysis), analysis.GetType().Name, "Unsupported criterion analysis."),
        };
        Assert.AreEqual(
            BayesianAnalysis.PointEstimateType.PosteriorMean,
            bayesian.PointEstimator,
            $"{label} point-estimator default.");
        bayesian.SetCustomMCMCResults(
            new MCMCResults(
                new ParameterSet((double[])mapValues.Clone(), dataLogLikelihood + priorLogLikelihood),
                new List<ParameterSet>
                {
                    new((double[])mapValues.Clone(), dataLogLikelihood + priorLogLikelihood),
                },
                alpha: 0.1),
            skipInformationCriteria: true);
        SetAnalysisResults(analysis, new UncertaintyAnalysisResults());

        switch (analysis)
        {
            case ARAnalysis value: await value.UpdatePointEstimateResultsAsync(); break;
            case MAAnalysis value: await value.UpdatePointEstimateResultsAsync(); break;
            case ARIMAAnalysis value: await value.UpdatePointEstimateResultsAsync(); break;
            case ARIMAXAnalysis value: await value.UpdatePointEstimateResultsAsync(); break;
            case RatingCurveAnalysis value: await value.UpdatePointEstimateResultsAsync(); break;
        }

        UncertaintyAnalysisResults results = GetAnalysisResults(analysis);
        double expectedAic = -2.0 * dataLogLikelihood + 2.0 * model.NumberOfParameters;
        double expectedBic = -2.0 * dataLogLikelihood + model.NumberOfParameters * Math.Log(sampleSize);
        double posteriorAic = -2.0 * (dataLogLikelihood + priorLogLikelihood) + 2.0 * model.NumberOfParameters;
        double posteriorBic = -2.0 * (dataLogLikelihood + priorLogLikelihood) + model.NumberOfParameters * Math.Log(sampleSize);
        Assert.AreEqual(expectedAic, results.AIC, tolerance, $"{label} AIC");
        Assert.AreEqual(expectedBic, results.BIC, tolerance, $"{label} BIC");
        Assert.IsTrue(Math.Abs(results.AIC - posteriorAic) > 1E-6, $"{label} AIC included prior density.");
        Assert.IsTrue(Math.Abs(results.BIC - posteriorBic) > 1E-6, $"{label} BIC included prior density.");
    }

    /// <summary>
    /// Assigns a deterministic result container through an analysis's private setter.
    /// </summary>
    /// <param name="analysis">The concrete analysis.</param>
    /// <param name="results">The empty result container.</param>
    private static void SetAnalysisResults(object analysis, UncertaintyAnalysisResults results)
    {
        PropertyInfo property = analysis.GetType().GetProperty("AnalysisResults")!;
        property.GetSetMethod(nonPublic: true)!.Invoke(analysis, new object[] { results });
    }

    /// <summary>
    /// Gets the populated result container from a concrete analysis.
    /// </summary>
    /// <param name="analysis">The concrete analysis.</param>
    /// <returns>The populated results.</returns>
    private static UncertaintyAnalysisResults GetAnalysisResults(object analysis)
    {
        PropertyInfo property = analysis.GetType().GetProperty("AnalysisResults")!;
        return (UncertaintyAnalysisResults)property.GetValue(analysis)!;
    }

    /// <summary>
    /// Evaluates the independent iid Gaussian log-likelihood of an intercept-only training window
    /// at a stored mean and scale.
    /// </summary>
    /// <param name="values">The response values.</param>
    /// <param name="trainingSteps">The number of leading values in the training window.</param>
    /// <param name="map">The stored mean and scale.</param>
    /// <returns>The sum of the Gaussian log densities of the training residuals.</returns>
    private static double IndependentIidGaussianLogLikelihood(double[] values, int trainingSteps, double[] map) =>
        values.Take(trainingSteps).Sum(value => IndependentGaussianLogDensity(value - map[0], map[1]));

    /// <summary>
    /// Confirms the analytical Gaussian optimum dominates small perturbations in both coordinates.
    /// </summary>
    /// <param name="model">The flat-prior order-zero Gaussian model.</param>
    /// <param name="parameters">The analytical mean and maximum-likelihood scale.</param>
    /// <param name="usePosterior">Whether to evaluate the flat-prior posterior objective.</param>
    private static void AssertLocalGaussianOptimum(AutoRegressive model, double[] parameters, bool usePosterior)
    {
        const double perturbation = 1E-4;
        Func<double[], double> objective = usePosterior ? model.LogLikelihood : model.DataLogLikelihood;
        double optimum = objective(parameters);
        for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
        {
            foreach (double direction in new[] { -1.0, 1.0 })
            {
                double[] candidate = (double[])parameters.Clone();
                candidate[parameterIndex] += direction * perturbation;
                Assert.IsTrue(
                    optimum >= objective(candidate),
                    $"Analytical {(usePosterior ? "MAP" : "MLE")} optimum failed coordinate {parameterIndex} direction {direction}.");
            }
        }
    }

    /// <summary>
    /// Creates the fixed ARIMAX alignment fixture from the committed oracle.
    /// </summary>
    /// <param name="raw">The raw response values.</param>
    /// <param name="covariate">The level-covariate values.</param>
    /// <param name="startDate">The first exact response and covariate timestamp.</param>
    /// <param name="trainingSteps">The raw training boundary.</param>
    /// <param name="differencingOrder">The response differencing order.</param>
    /// <param name="lambda">The fixed Box-Cox exponent.</param>
    /// <returns>The configured ARIMAX model.</returns>
    private static ARIMAX CreateAlignedArimax(
        double[] raw,
        double[] covariate,
        DateTime startDate,
        int trainingSteps,
        int differencingOrder,
        double lambda)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = differencingOrder,
            MAOrderQ = 1,
            XOrderB = 0,
            TransformType = Transform.BoxCox,
        };
        model.SetTransformParameters(lambda, double.NaN);
        model.TimeSeries = CreateDailySeries(raw, startDate);
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = trainingSteps;
        model.SetCovariates(new List<Numerics.Data.TimeSeries> { CreateDailySeries(covariate, startDate) });
        return model;
    }

    /// <summary>
    /// Creates a daily time series beginning at an exact date.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <param name="startDate">The first timestamp.</param>
    /// <returns>The daily time series.</returns>
    private static Numerics.Data.TimeSeries CreateDailySeries(double[] values, DateTime startDate)
    {
        return new Numerics.Data.TimeSeries(Numerics.Data.TimeInterval.OneDay, startDate, values);
    }

    /// <summary>
    /// Compares one model's pointwise Jeffreys component with the analytical oracle.
    /// </summary>
    /// <param name="model">The time-series model under verification.</param>
    /// <param name="sigma">The fixed positive innovation scale.</param>
    /// <param name="expected">The independently calculated log density.</param>
    /// <param name="tolerance">The fixed absolute acceptance tolerance.</param>
    private static void VerifyJeffreysComponent(
        ModelBase model,
        double sigma,
        double expected,
        double tolerance)
    {
        SetJeffreysRule(model);
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        parameters[^1] = sigma;
        PriorComponent component = model.PointwisePriorLogLikelihood(parameters)
            .Single(item => item.Type == PriorComponentType.JeffreysScalePrior);

        Assert.AreEqual(expected, component.LogLikelihood, tolerance, model.GetType().Name);
        Assert.AreEqual(-Math.Log(sigma), expected, tolerance, $"Oracle drift for {model.GetType().Name}.");
    }

    /// <summary>
    /// Creates the model named by one committed oracle case.
    /// </summary>
    /// <param name="modelName">The exact model type name stored in the oracle.</param>
    /// <returns>A default time-series model instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown oracle model name.</exception>
    private static ModelBase CreateModel(string modelName)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(),
            nameof(MovingAverage) => new MovingAverage(),
            nameof(ARIMA) => new ARIMA(),
            nameof(ARIMAX) => new ARIMAX(),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown oracle model."),
        };
    }

    /// <summary>
    /// Creates a model that fits its exponent after the raw training boundary is fixed.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="values">The raw response.</param>
    /// <param name="transform">The transform to fit.</param>
    /// <param name="trainingSteps">The raw training boundary.</param>
    /// <returns>The configured model.</returns>
    private static ModelBase CreateAutomaticallyTransformedModel(string modelName, double[] values, Transform transform, int trainingSteps)
    {
        ModelBase model = CreateUnattachedTimeSeriesModel(modelName);
        SetTimeSeries(model, CreateSeries(values));
        SetUseDefaultTrainingSteps(model, false);
        SetTrainingSteps(model, trainingSteps);
        SetTransformType(model, transform);
        return model;
    }

    /// <summary>
    /// Creates a model whose manual exponent is installed before response attachment.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="values">The raw response.</param>
    /// <param name="lambda">The manual Yeo-Johnson exponent.</param>
    /// <param name="trainingSteps">The raw training boundary.</param>
    /// <returns>The configured model.</returns>
    private static ModelBase CreateManuallyTransformedModel(string modelName, double[] values, double lambda, int trainingSteps)
    {
        ModelBase model = CreateUnattachedTimeSeriesModel(modelName);
        SetTransformType(model, Transform.YeoJohnson);
        SetTransformParameters(model, lambda);
        SetTimeSeries(model, CreateSeries(values));
        SetUseDefaultTrainingSteps(model, false);
        SetTrainingSteps(model, trainingSteps);
        return model;
    }

    /// <summary>
    /// Creates an unattached one-lag, no-intercept model.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown model name.</exception>
    private static ModelBase CreateUnattachedTimeSeriesModel(string modelName)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive { Order = 1, IncludeIntercept = false },
            nameof(MovingAverage) => new MovingAverage { Order = 1, IncludeIntercept = false },
            nameof(ARIMA) => new ARIMA { POrder = 1, DOrder = 0, QOrder = 0, IncludeIntercept = false },
            nameof(ARIMAX) => new ARIMAX
            {
                AROrderP = 1,
                DiffOrderD = 0,
                MAOrderQ = 0,
                XOrderB = 0,
                IncludeIntercept = false,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown model."),
        };
    }

    /// <summary>
    /// Loads a committed Phase 5 analytical oracle.
    /// </summary>
    /// <param name="fileName">The oracle file name under the verification-data output folder.</param>
    /// <returns>The root JSON element.</returns>
    private static JsonElement LoadOracle(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Creates a configured common-data model named by the invalid-scale oracle.
    /// </summary>
    /// <param name="modelName">The exact model type name.</param>
    /// <param name="values">The common raw response values.</param>
    /// <returns>The configured model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown oracle model name.</exception>
    private static ModelBase CreateModel(string modelName, double[] values)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(CreateSeries(values), 1, false),
            nameof(MovingAverage) => new MovingAverage(CreateSeries(values), 1, false),
            nameof(ARIMA) => new ARIMA(CreateSeries(values), 1, 0, 0, false),
            nameof(ARIMAX) => CreateArimax(values),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown oracle model."),
        };
    }

    /// <summary>
    /// Creates the configured ARIMAX oracle model.
    /// </summary>
    /// <param name="values">The common response values.</param>
    /// <returns>An ARIMAX(1,0,0,0) model without an intercept.</returns>
    private static ARIMAX CreateArimax(double[] values)
    {
        return new ARIMAX(CreateSeries(values))
        {
            IncludeIntercept = false,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            TransformType = Transform.None,
        };
    }

    /// <summary>
    /// Creates an annual time series containing the supplied values.
    /// </summary>
    /// <param name="values">The response values.</param>
    /// <returns>The constructed time series.</returns>
    private static Numerics.Data.TimeSeries CreateSeries(double[] values)
    {
        var series = new Numerics.Data.TimeSeries(
            Numerics.Data.TimeInterval.OneYear,
            new DateTime(2000, 1, 1),
            new DateTime(2000 + values.Length - 1, 1, 1));
        for (int i = 0; i < values.Length; i++)
            series[i].Value = values[i];
        return series;
    }

    /// <summary>
    /// Evaluates the Gaussian log-density formula independently of Numerics distributions.
    /// </summary>
    /// <param name="residual">The model residual.</param>
    /// <param name="sigma">The positive innovation scale.</param>
    /// <returns>The analytical Gaussian log density.</returns>
    private static double IndependentGaussianLogDensity(double residual, double sigma)
    {
        return -0.5 * Math.Log(2 * Math.PI)
            - Math.Log(sigma)
            - residual * residual / (2 * sigma * sigma);
    }

    /// <summary>
    /// Evaluates a conditional ARMA(1,1) forecast on first-differenced Yeo-Johnson values.
    /// </summary>
    /// <param name="raw">The raw observations, including any unused holdout sentinel.</param>
    /// <param name="trainingSteps">The number of raw observations in the training window.</param>
    /// <param name="forecastSteps">The forecast horizon.</param>
    /// <param name="lambda">The fixed Yeo-Johnson exponent.</param>
    /// <param name="mu">The differenced-scale intercept.</param>
    /// <param name="phi">The AR(1) coefficient.</param>
    /// <param name="theta">The MA(1) coefficient.</param>
    /// <returns>The conditional fitted and forecast levels on the transformed scale.</returns>
    private static double[] IndependentConditionalArmaD1Prediction(
        double[] raw,
        int trainingSteps,
        int forecastSteps,
        double lambda,
        double mu,
        double phi,
        double theta)
    {
        double[] observedLevels = raw
            .Take(trainingSteps)
            .Select(value => IndependentYeoJohnsonTransform(value, lambda))
            .ToArray();
        double[] observedDifferences = Enumerable.Range(1, observedLevels.Length - 1)
            .Select(index => observedLevels[index] - observedLevels[index - 1])
            .ToArray();
        int trainingModelSteps = trainingSteps - 1;
        int modelSteps = trainingModelSteps + forecastSteps;
        var predictedDifferences = new double[modelSteps];
        var residuals = new double[modelSteps];

        predictedDifferences[0] = observedDifferences[0];
        for (int modelIndex = 1; modelIndex < modelSteps; modelIndex++)
        {
            double lag = modelIndex - 1 < trainingModelSteps
                ? observedDifferences[modelIndex - 1]
                : predictedDifferences[modelIndex - 1];
            predictedDifferences[modelIndex] = mu
                + phi * (lag - mu)
                + theta * residuals[modelIndex - 1];
            if (modelIndex < trainingModelSteps)
            {
                residuals[modelIndex] = observedDifferences[modelIndex]
                    - predictedDifferences[modelIndex];
            }
        }

        var levels = new double[trainingSteps + forecastSteps];
        levels[0] = observedLevels[0];
        for (int rawIndex = 1; rawIndex < levels.Length; rawIndex++)
        {
            double previousLevel = rawIndex <= trainingSteps
                ? observedLevels[rawIndex - 1]
                : levels[rawIndex - 1];
            levels[rawIndex] = previousLevel + predictedDifferences[rawIndex - 1];
        }

        return levels;
    }

    /// <summary>
    /// Calculates the transformed-level forecast variance for an integrated ARMA(1,1) process.
    /// </summary>
    /// <param name="horizon">The positive forecast horizon.</param>
    /// <param name="phi">The AR(1) coefficient.</param>
    /// <param name="theta">The MA(1) coefficient.</param>
    /// <param name="sigma">The innovation standard deviation.</param>
    /// <returns>The analytical variance of the transformed level at the requested horizon.</returns>
    private static double IndependentIntegratedArmaForecastVariance(
        int horizon,
        double phi,
        double theta,
        double sigma)
    {
        double cumulativeImpulse = 0.0;
        double sumSquares = 0.0;
        for (int lag = 0; lag < horizon; lag++)
        {
            double impulse = lag == 0
                ? 1.0
                : (phi + theta) * Math.Pow(phi, lag - 1);
            cumulativeImpulse += impulse;
            sumSquares += cumulativeImpulse * cumulativeImpulse;
        }

        return sigma * sigma * sumSquares;
    }

    /// <summary>
    /// Evaluates the positive-branch Box-Cox inverse independently of production transform code.
    /// </summary>
    /// <param name="value">The transformed value.</param>
    /// <param name="lambda">The transform exponent.</param>
    /// <returns>The raw positive value.</returns>
    private static double IndependentBoxCoxInverse(double value, double lambda)
    {
        return lambda == 0.0
            ? Math.Exp(value)
            : Math.Pow(1.0 + lambda * value, 1.0 / lambda);
    }

    /// <summary>
    /// Evaluates the Yeo-Johnson transform independently of production transform code.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="lambda">The transform exponent.</param>
    /// <returns>The transformed value.</returns>
    private static double IndependentYeoJohnsonTransform(double value, double lambda)
    {
        if (value >= 0.0)
        {
            return lambda == 0.0
                ? Math.Log(value + 1.0)
                : (Math.Pow(value + 1.0, lambda) - 1.0) / lambda;
        }

        return lambda == 2.0
            ? -Math.Log(1.0 - value)
            : -(Math.Pow(1.0 - value, 2.0 - lambda) - 1.0) / (2.0 - lambda);
    }

    /// <summary>
    /// Evaluates the Yeo-Johnson inverse independently of production transform code.
    /// </summary>
    /// <param name="value">The transformed value.</param>
    /// <param name="lambda">The transform exponent.</param>
    /// <returns>The raw value.</returns>
    private static double IndependentYeoJohnsonInverse(double value, double lambda)
    {
        if (value >= 0.0)
        {
            return lambda == 0.0
                ? Math.Exp(value) - 1.0
                : Math.Pow(1.0 + lambda * value, 1.0 / lambda) - 1.0;
        }

        return lambda == 2.0
            ? 1.0 - Math.Exp(-value)
            : 1.0 - Math.Pow(1.0 - (2.0 - lambda) * value, 1.0 / (2.0 - lambda));
    }

    /// <summary>
    /// Compares seeded independent Gaussian sample moments with their analytical sampling errors.
    /// </summary>
    /// <param name="values">The model-scale sample.</param>
    /// <param name="expectedMean">The generating mean.</param>
    /// <param name="sigma">The generating standard deviation.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertIndependentGaussianMoments(
        double[] values,
        double expectedMean,
        double sigma,
        string context)
    {
        double sampleMean = values.Average();
        double sumSquares = values.Sum(value => (value - sampleMean) * (value - sampleMean));
        double sampleVariance = sumSquares / (values.Length - 1);
        double expectedVariance = sigma * sigma;
        double meanBound = 4.0 * sigma / Math.Sqrt(values.Length);
        double varianceStandardError = expectedVariance * Math.Sqrt(2.0 / (values.Length - 1));
        double varianceBound = Math.Max(4.0 * varianceStandardError, 0.03 * expectedVariance);

        Assert.AreEqual(expectedMean, sampleMean, meanBound, $"{context} mean");
        Assert.AreEqual(expectedVariance, sampleVariance, varianceBound, $"{context} variance");
    }

    /// <summary>
    /// Compares conditional fitted and forecast samples with the analytical random-walk moments.
    /// </summary>
    /// <param name="samples">The prediction realizations indexed by realization and raw step.</param>
    /// <param name="raw">The observed raw response, including an unused holdout sentinel.</param>
    /// <param name="trainingSteps">The raw training boundary.</param>
    /// <param name="context">The model label.</param>
    private static void AssertConditionalPredictionMoments(
        double[][] samples,
        double[] raw,
        int trainingSteps,
        string context)
    {
        foreach (double[] sample in samples)
            Assert.AreEqual(raw[0], sample[0], 0.0, $"{context} conditioning anchor");

        for (int rawIndex = 1; rawIndex < trainingSteps; rawIndex++)
        {
            AssertIndependentGaussianMoments(
                samples.Select(sample => sample[rawIndex]).ToArray(),
                raw[rawIndex - 1],
                1.0,
                $"{context} training index {rawIndex}");
        }

        for (int horizon = 1; horizon <= 3; horizon++)
        {
            int rawIndex = trainingSteps + horizon - 1;
            AssertIndependentGaussianMoments(
                samples.Select(sample => sample[rawIndex]).ToArray(),
                raw[trainingSteps - 1],
                Math.Sqrt(horizon),
                $"{context} forecast horizon {horizon}");
        }
    }

    /// <summary>
    /// Reads a JSON array of doubles.
    /// </summary>
    /// <param name="element">The JSON array.</param>
    /// <returns>The numeric values.</returns>
    private static double[] ReadDoubleArray(JsonElement element)
    {
        return element.EnumerateArray().Select(item => item.GetDouble()).ToArray();
    }

    /// <summary>
    /// Reads numeric and named non-finite scale cases from the committed oracle.
    /// </summary>
    /// <param name="element">The invalid-scale JSON array.</param>
    /// <returns>The invalid scale values.</returns>
    /// <exception cref="InvalidDataException">Thrown for an unknown named scale.</exception>
    private static double[] ReadInvalidScales(JsonElement element)
    {
        return element.EnumerateArray().Select(item => item.ValueKind switch
        {
            JsonValueKind.Number => item.GetDouble(),
            JsonValueKind.String when item.GetString() == "NaN" => double.NaN,
            JsonValueKind.String when item.GetString() == "PositiveInfinity" => double.PositiveInfinity,
            JsonValueKind.String when item.GetString() == "NegativeInfinity" => double.NegativeInfinity,
            _ => throw new InvalidDataException($"Unknown invalid-scale case: {item}."),
        }).ToArray();
    }

    /// <summary>
    /// Compares two numeric arrays using one absolute tolerance.
    /// </summary>
    /// <param name="expected">The independent expected values.</param>
    /// <param name="actual">The production values.</param>
    /// <param name="tolerance">The absolute tolerance.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, double tolerance, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], tolerance, $"{context}, index {i}");
    }

    /// <summary>
    /// Compares two values using the predeclared combined absolute/relative rule.
    /// </summary>
    /// <param name="expected">The oracle value.</param>
    /// <param name="actual">The production value.</param>
    /// <param name="absoluteTolerance">The absolute tolerance.</param>
    /// <param name="relativeTolerance">The relative tolerance.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertClose(double expected, double actual, double absoluteTolerance, double relativeTolerance, string context)
    {
        double bound = Math.Max(absoluteTolerance, relativeTolerance * Math.Abs(expected));
        Assert.AreEqual(expected, actual, bound, context);
    }

    /// <summary>
    /// Compares two vectors using the predeclared combined absolute/relative rule.
    /// </summary>
    /// <param name="expected">The oracle values.</param>
    /// <param name="actual">The production values.</param>
    /// <param name="absoluteTolerance">The absolute tolerance.</param>
    /// <param name="relativeTolerance">The relative tolerance.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayClose(double[] expected, double[] actual, double absoluteTolerance, double relativeTolerance, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            AssertClose(expected[i], actual[i], absoluteTolerance, relativeTolerance, $"{context}, index {i}");
    }

    /// <summary>
    /// Creates fixed valid parameters for the common one-lag, no-intercept fixture.
    /// </summary>
    /// <param name="model">The configured model.</param>
    /// <returns>The parameter vector.</returns>
    private static double[] CreateFixedTimeSeriesParameters(ModelBase model)
    {
        var parameters = new double[model.Parameters.Count];
        parameters[0] = 0.35;
        parameters[^1] = 0.8;
        return parameters;
    }

    /// <summary>
    /// Gets the effective transform exponent.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The exponent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static double GetTransformLambda(ModelBase model)
    {
        return model switch
        {
            AutoRegressive ar => ar.TransformLambda,
            MovingAverage ma => ma.TransformLambda,
            ARIMA arima => arima.TransformLambda,
            ARIMAX arimax => arimax.TransformLambda,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Gets transformed training values.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The transformed training values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static double[] GetTrainingValues(ModelBase model)
    {
        return model switch
        {
            AutoRegressive ar => ar.TrainingTimeSeries.ValuesToArray(),
            MovingAverage ma => ma.TrainingTimeSeries.ValuesToArray(),
            ARIMA arima => arima.TrainingTimeSeries.ValuesToArray(),
            ARIMAX arimax => arimax.TrainingTimeSeries.ValuesToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Gets conditional residuals from a supported time-series model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="parameters">The fixed parameter vector.</param>
    /// <returns>The residual vector.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static double[] GetResiduals(ModelBase model, double[] parameters)
    {
        return model switch
        {
            AutoRegressive ar => ar.Residuals(parameters),
            MovingAverage ma => ma.Residuals(parameters),
            ARIMA arima => arima.Residuals(parameters),
            ARIMAX arimax => arimax.Residuals(parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Restores one model from serialized state.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="series">The response series.</param>
    /// <param name="xml">The serialized model.</param>
    /// <returns>The restored model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model name is unknown.</exception>
    private static ModelBase RestoreTimeSeriesModel(string modelName, Numerics.Data.TimeSeries series, XElement xml)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(series, xml),
            nameof(MovingAverage) => new MovingAverage(series, xml),
            nameof(ARIMA) => new ARIMA(series, xml),
            nameof(ARIMAX) => new ARIMAX(series, xml),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown model."),
        };
    }

    /// <summary>
    /// Assigns the transform type.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="transform">The transform.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTransformType(ModelBase model, Transform transform)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TransformType = transform; break;
            case MovingAverage ma: ma.TransformType = transform; break;
            case ARIMA arima: arima.TransformType = transform; break;
            case ARIMAX arimax: arimax.TransformType = transform; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns a manual transform exponent.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="lambda">The exponent.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTransformParameters(ModelBase model, double lambda)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.SetTransformParameters(lambda, double.NaN); break;
            case MovingAverage ma: ma.SetTransformParameters(lambda, double.NaN); break;
            case ARIMA arima: arima.SetTransformParameters(lambda, double.NaN); break;
            case ARIMAX arimax: arimax.SetTransformParameters(lambda, double.NaN); break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns a response series.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="series">The response series.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTimeSeries(ModelBase model, Numerics.Data.TimeSeries series)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TimeSeries = series; break;
            case MovingAverage ma: ma.TimeSeries = series; break;
            case ARIMA arima: arima.TimeSeries = series; break;
            case ARIMAX arimax: arimax.TimeSeries = series; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns default-training-window state.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The requested state.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetUseDefaultTrainingSteps(ModelBase model, bool value)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.UseDefaultTrainingSteps = value; break;
            case MovingAverage ma: ma.UseDefaultTrainingSteps = value; break;
            case ARIMA arima: arima.UseDefaultTrainingSteps = value; break;
            case ARIMAX arimax: arimax.UseDefaultTrainingSteps = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns the raw training boundary.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The training boundary.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTrainingSteps(ModelBase model, int value)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TrainingTimeSteps = value; break;
            case MovingAverage ma: ma.TrainingTimeSteps = value; break;
            case ARIMA arima: arima.TrainingTimeSteps = value; break;
            case ARIMAX arimax: arimax.TrainingTimeSteps = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Enables the Jeffreys scale-prior contribution for a supported time-series model.
    /// </summary>
    /// <param name="model">The time-series model under verification.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unsupported model type.</exception>
    private static void SetJeffreysRule(ModelBase model)
    {
        switch (model)
        {
            case AutoRegressive autoRegressive:
                autoRegressive.UseJeffreysRuleForScale = true;
                break;
            case MovingAverage movingAverage:
                movingAverage.UseJeffreysRuleForScale = true;
                break;
            case ARIMA arima:
                arima.UseJeffreysRuleForScale = true;
                break;
            case ARIMAX arimax:
                arimax.UseJeffreysRuleForScale = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported time-series model.");
        }
    }
}
