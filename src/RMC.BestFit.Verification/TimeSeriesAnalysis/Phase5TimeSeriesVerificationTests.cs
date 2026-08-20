using System.Text.Json;
using System.Xml.Linq;
using RMC.BestFit.Models;

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
    /// Verifies ARIMA and ARIMAX prediction reintegration against hand-evaluated first- and
    /// second-difference recurrences, including inverse transformation and component alignment.
    /// </summary>
    /// <remarks>
    /// For the ARIMA case, the fixed second difference is two and the first two transformed
    /// levels are one and four, giving the exact square-number sequence. For the ARIMAX case,
    /// a unit level covariate supplies every first difference on the logarithmic scale, giving
    /// <c>exp(1), ..., exp(8)</c>. The absolute acceptance tolerance is fixed at 1E-10.
    /// </remarks>
    [TestMethod]
    public void ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle()
    {
        const double tolerance = 1E-10;
        DateTime startDate = new(2002, 3, 4);

        double[] quadratic = { 1, 4, 9, 16, 25, 36 };
        var arima = new ARIMA(CreateDailySeries(quadratic, startDate), 0, 2, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = Transform.None,
        };
        arima.TrainingTimeSteps = quadratic.Length;
        var arimaPrediction = arima.Predict(new[] { 2.0, 1.0 }, 2, -1);
        double[] expectedSquares = { 1, 4, 9, 16, 25, 36, 49, 64 };
        AssertArrayEqual(expectedSquares, arimaPrediction.Y, tolerance, "ARIMA d=2 levels");
        AssertArrayEqual(
            new[] { 0.0, 0.0, 2, 2, 2, 2, 2, 2 },
            arimaPrediction.InterceptPart,
            tolerance,
            "ARIMA d=2 component map");

        double[] logarithmic = Enumerable.Range(1, 6).Select(value => Math.Exp(value)).ToArray();
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
        arimax.TrainingTimeSteps = logarithmic.Length;
        arimax.SetCovariates(new List<Numerics.Data.TimeSeries>
        {
            CreateDailySeries(new[] { 999.0, 1, 1, 1, 1, 1, 1, 1 }, startDate),
        });

        var arimaxPrediction = arimax.Predict(new[] { 1.0, 0.25 }, 2, -1);
        double[] expectedLogarithmic = Enumerable.Range(1, 8)
            .Select(value => Math.Exp(value))
            .ToArray();
        AssertArrayEqual(expectedLogarithmic, arimaxPrediction.Y, tolerance, "ARIMAX log d=1 levels");
        AssertArrayEqual(
            new[] { 0.0, 1, 1, 1, 1, 1, 1, 1 },
            arimaxPrediction.CovariatePart,
            tolerance,
            "ARIMAX d=1 component map");
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
