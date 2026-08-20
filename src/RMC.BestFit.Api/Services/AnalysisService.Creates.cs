using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Creation methods for the Phase-4 analysis kinds. Kept in a separate partial file so the
    /// core run/lookup orchestration in AnalysisService.cs stays readable as the kind count grows.
    /// </summary>
    public partial class AnalysisService
    {
        /// <inheritdoc/>
        public AnalysisResource CreateMixture(CreateMixtureAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);
            ValidateComponentDistributions(request.Distributions, MixtureModel.IsSupportedDistributionType, "mixture");

            var model = new MixtureModel(input.DataFrame.Clone(), request.Distributions, request.IsZeroInflated);
            var analysis = new MixtureAnalysis(model);
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            // Priors are applied last: the model is fully configured, so nothing after this point
            // triggers the default-parameter rebuild that would wipe them.
            PriorMapper.ApplyParameterPriors(model, request.ParameterPriors);
            PriorMapper.ApplyQuantilePriors(model, request.QuantilePriors, request.UseSingleQuantile);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Mixture analysis of {input.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.Mixture,
                Mixture = analysis,
                InputDataId = input.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreatePointProcess(CreatePointProcessAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);

            if (!request.IsSeasonal && (request.TimeBlock.HasValue || request.StartMonth.HasValue))
            {
                throw new ArgumentException("timeBlock and startMonth apply only when isSeasonal is true. Remove them or set isSeasonal.");
            }
            if (request.TotalYears is <= 0d)
            {
                throw new ArgumentException("totalYears must be greater than 0.");
            }

            // Construction order matters and mirrors the desktop application: the DataFrame setter
            // processes the threshold series and seeds data-derived defaults; seasonality swaps the
            // internal GEV distribution wholesale, so it must follow the data assignment and
            // precede any threshold seeding.
            var model = new PointProcessModel();
            model.DataFrame = input.DataFrame.Clone();
            if (request.IsSeasonal)
            {
                model.IsSeasonal = true;
                if (request.TimeBlock.HasValue) model.TimeBlock = request.TimeBlock.Value;
                if (request.StartMonth.HasValue) model.StartMonth = request.StartMonth.Value;
            }

            // A POT-created input-data resource records the extraction threshold; reuse it so the
            // model's threshold matches the events unless the client overrides explicitly.
            if (!request.Threshold.HasValue && input.Method == InputDataMethod.PeaksOverThreshold && input.Threshold.HasValue)
            {
                model.SetDefaultThresholdAndTotalYears(input.Threshold, forceTotalYears: true);
            }
            if (request.Threshold.HasValue || request.TotalYears.HasValue)
            {
                model.UseDefaults = false;
                if (request.Threshold.HasValue) model.Threshold = request.Threshold.Value;
                if (request.TotalYears.HasValue) model.TotalYears = request.TotalYears.Value;
            }

            var analysis = new PointProcessAnalysis(model);
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            PriorMapper.ApplyParameterPriors(model, request.ParameterPriors);
            PriorMapper.ApplyQuantilePriors(model, request.QuantilePriors, request.UseSingleQuantile);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Point process analysis of {input.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.PointProcess,
                PointProcess = analysis,
                InputDataId = input.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateCompetingRisks(CreateCompetingRisksAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);
            ValidateComponentDistributions(request.Distributions, CompetingRisksModel.IsSupportedDistributionType, "competing risks");

            var model = new CompetingRisksModel(input.DataFrame.Clone(), request.Distributions);
            var analysis = new CompetingRiskAnalysis(model);
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            PriorMapper.ApplyParameterPriors(model, request.ParameterPriors);
            PriorMapper.ApplyQuantilePriors(model, request.QuantilePriors, request.UseSingleQuantile);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Competing risks analysis of {input.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.CompetingRisks,
                CompetingRisks = analysis,
                InputDataId = input.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateComposite(CreateCompositeAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Components == null || request.Components.Count == 0)
            {
                throw new ArgumentException("At least one component analysis is required.");
            }

            // Mixture weights are user inputs; the other composite types ignore or compute them.
            if (request.CompositeType == CompositeType.Mixture)
            {
                double sum = 0d;
                for (int i = 0; i < request.Components.Count; i++)
                {
                    double? weight = request.Components[i].Weight;
                    if (!weight.HasValue || double.IsNaN(weight.Value) || weight.Value <= 0d || weight.Value >= 1d)
                    {
                        throw new ArgumentException(
                            $"components[{i}]: mixture composites require a weight strictly between 0 and 1 for every component.");
                    }
                    sum += weight.Value;
                }
                if (sum > 1d + 1e-12)
                {
                    throw new ArgumentException(
                        $"The mixture weights sum to {sum:G6}; they must sum to at most 1 (any remainder becomes a point mass at zero).");
                }
            }

            var componentResources = new List<AnalysisResource>(request.Components.Count);
            var weighted = new List<WeightedUnivariateAnalysis>(request.Components.Count);
            foreach (var component in request.Components)
            {
                var componentResource = _store.GetAnalysis(component.AnalysisId)
                    ?? throw new ResourceNotFoundException("component analysis", component.AnalysisId);
                componentResources.Add(componentResource);
                // The WeightedUnivariateAnalysis setter rejects composite children itself; this
                // guard turns the remaining non-univariate kinds into a clear client error.
                weighted.Add(new WeightedUnivariateAnalysis(
                    GetUnivariateAnalysis(componentResource), component.Weight ?? 0d));
            }

            var analysis = new CompositeAnalysis(weighted)
            {
                CompositeDistributionType = request.CompositeType,
                IsMaximum = request.IsMaximum
            };
            if (request.AverageMethod.HasValue) analysis.ModelAverageMethod = request.AverageMethod.Value;
            if (request.Dependency.HasValue) analysis.Dependency = request.Dependency.Value;
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            if (request.CredibleIntervalWidth.HasValue) analysis.BayesianAnalysis.CredibleIntervalWidth = request.CredibleIntervalWidth.Value;
            if (request.PointEstimator.HasValue) analysis.BayesianAnalysis.PointEstimator = request.PointEstimator.Value;

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? $"Composite of {string.Join(", ", componentResources.Select(r => r.Name))}"
                    : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.Composite,
                Composite = analysis,
                ComponentResources = componentResources,
                ComponentAnalysisIds = componentResources.Select(r => r.Id).ToList()
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateDistributionFitting(CreateDistributionFittingAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);

            var analysis = new FittingAnalysis(input.DataFrame.Clone());
            if (request.Distributions is { Count: > 0 })
            {
                foreach (var type in request.Distributions)
                {
                    if (!UnivariateDistribution.IsSupportedDistributionType(type))
                    {
                        throw new ArgumentException(
                            $"Distribution '{type}' is not supported by the distribution fitting analysis. See GET api/metadata/distributions.");
                    }
                }
                // The candidate list defaults to all 15 supported distributions; a client subset
                // replaces its contents (the property setter is private).
                analysis.DistributionList.Clear();
                foreach (var type in request.Distributions.Distinct())
                {
                    analysis.DistributionList.Add(Numerics.Distributions.UnivariateDistributionFactory.CreateDistribution(type));
                }
            }

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Distribution fitting of {input.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.DistributionFitting,
                DistributionFitting = analysis,
                InputDataId = input.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateBivariate(CreateBivariateAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.MarginalXAnalysisId == request.MarginalYAnalysisId)
            {
                throw new ArgumentException("The two marginal analyses must be distinct resources.");
            }
            if (request.EstimationMethod == Numerics.Distributions.Copulas.CopulaEstimationMethod.FullLikelihood)
            {
                throw new ArgumentException(
                    "estimationMethod 'fullLikelihood' is not supported by the bivariate analysis. Use 'inferenceFromMargins' or 'pseudoLikelihood'.");
            }
            if (request.XyOrdinates == null || request.XyOrdinates.Count == 0)
            {
                throw new ArgumentException("At least one xy evaluation point is required (the joint-exceedance grid).");
            }

            var marginalX = _store.GetAnalysis(request.MarginalXAnalysisId)
                ?? throw new ResourceNotFoundException("marginal X analysis", request.MarginalXAnalysisId);
            var marginalY = _store.GetAnalysis(request.MarginalYAnalysisId)
                ?? throw new ResourceNotFoundException("marginal Y analysis", request.MarginalYAnalysisId);
            var modelX = GetMarginalModel(marginalX, "marginal X");
            var modelY = GetMarginalModel(marginalY, "marginal Y");

            if (modelX.IsNonstationary || modelY.IsNonstationary)
            {
                throw new ArgumentException("Nonstationary marginals are not supported by the bivariate analysis.");
            }
            int overlap = CountMatchedExactPairs(modelX.DataFrame, modelY.DataFrame);
            if (overlap < 10)
            {
                throw new ArgumentException(
                    $"The two marginals share only {overlap} overlapping non-outlier exact observations (matched by time index); at least 10 are required to fit a copula.");
            }

            var distribution = new BivariateDistribution(modelX, modelY, request.CopulaType);
            if (request.EstimationMethod.HasValue)
            {
                distribution.CopulaEstimationMethod = request.EstimationMethod.Value;
            }
            var analysis = new BivariateAnalysis(distribution)
            {
                // Sorted by x so the model's ascending-ordered pair container accepts the grid
                // regardless of client ordering. Y ordinates are deterministic point values,
                // matching the model's own placeholder construction.
                XYOrdinates = new UncertainOrderedPairedData(
                    request.XyOrdinates
                        .OrderBy(p => p.X)
                        .Select(p => new UncertainOrdinate(p.X, new Deterministic(p.Y)))
                        .ToList(),
                    false, SortOrder.Ascending, false, SortOrder.Ascending,
                    Numerics.Distributions.UnivariateDistributionType.Deterministic)
            };
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            PriorMapper.ApplyParameterPriors(distribution, request.ParameterPriors);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Bivariate: {marginalX.Name} / {marginalY.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.Bivariate,
                Bivariate = analysis,
                MarginalXResource = marginalX,
                MarginalYResource = marginalY,
                ComponentResources = new List<AnalysisResource> { marginalX, marginalY },
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateCoincidentFrequency(CreateCoincidentFrequencyAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var bivariate = _store.GetAnalysis(request.BivariateAnalysisId);
            if (bivariate == null || bivariate.Kind != AnalysisKind.Bivariate)
            {
                throw new ResourceNotFoundException("bivariate analysis", request.BivariateAnalysisId);
            }
            if (request.NumberOfBins is < 5 or > 1000)
            {
                throw new ArgumentException("numberOfBins must be between 5 and 1000.");
            }

            var surface = ToResponseSurface(request);
            var analysis = new CoincidentFrequencyAnalysis(
                bivariate.Bivariate!,
                request.XValues.ToArray(),
                request.YValues.ToArray(),
                surface)
            {
                NumberOfBins = request.NumberOfBins
            };
            if (request.CredibleIntervalWidth.HasValue) analysis.BayesianAnalysis.CredibleIntervalWidth = request.CredibleIntervalWidth.Value;
            if (request.PointEstimator.HasValue) analysis.BayesianAnalysis.PointEstimator = request.PointEstimator.Value;

            // Locks cover the bivariate plus its transitive marginals: a marginal re-running
            // mid-CFA would mutate the shared marginal model this analysis reads.
            var components = new List<AnalysisResource> { bivariate };
            if (bivariate.MarginalXResource != null) components.Add(bivariate.MarginalXResource);
            if (bivariate.MarginalYResource != null) components.Add(bivariate.MarginalYResource);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Coincident frequency over {bivariate.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.CoincidentFrequency,
                CoincidentFrequency = analysis,
                BivariateResource = bivariate,
                ComponentResources = components,
                BivariateAnalysisId = bivariate.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateTimeSeries(CreateTimeSeriesAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var source = _store.GetTimeSeries(request.TimeSeriesId)
                ?? throw new ResourceNotFoundException("time series", request.TimeSeriesId);

            RejectFieldsForModelType(request);
            // The model layer silently clamps the forecast horizon to 0-100; range-check here so
            // an out-of-range request is an error rather than a silent adjustment.
            if (request.ForecastingTimeSteps is < 0 or > 100)
            {
                throw new ArgumentException("forecastingTimeSteps must be between 0 and 100.");
            }
            if (request.TransformLambda.HasValue)
            {
                if (!double.IsFinite(request.TransformLambda.Value))
                    throw new ArgumentException("transformLambda must be finite.");
                if (request.TransformType is not RMC.BestFit.Models.Transform.BoxCox and not RMC.BestFit.Models.Transform.YeoJohnson)
                    throw new ArgumentException("transformLambda requires transformType 'boxCox' or 'yeoJohnson'.");
            }

            var series = CloneSeries(source.TimeSeries);
            bool includeIntercept = request.IncludeIntercept ?? true;

            AnalysisResource resource;
            switch (request.ModelType)
            {
                case TimeSeriesModelType.Ar:
                {
                    var model = new AutoRegressive(series, request.Order ?? 1, includeIntercept);
                    var analysis = new ARAnalysis(model);
                    ConfigureTimeSeriesAnalysis(request, () =>
                    {
                        if (request.TransformType.HasValue) model.TransformType = request.TransformType.Value;
                        if (request.TransformLambda.HasValue) model.SetTransformParameters(request.TransformLambda.Value);
                    },
                        setTraining: v => { model.UseDefaultTrainingSteps = false; model.TrainingTimeSteps = v; },
                        setForecast: v => analysis.ForecastingTimeSteps = v,
                        analysis.BayesianAnalysis, model, request.ParameterPriors);
                    resource = new AnalysisResource
                    {
                        Name = DefaultTimeSeriesName(request, "AR", source.Name),
                        Description = request.Description,
                        Kind = AnalysisKind.TimeSeries,
                        TimeSeriesModel = TimeSeriesModelType.Ar,
                        Ar = analysis,
                        TimeSeriesId = source.Id
                    };
                    break;
                }
                case TimeSeriesModelType.Ma:
                {
                    var model = new MovingAverage(series, request.Order ?? 1, includeIntercept);
                    var analysis = new MAAnalysis(model);
                    ConfigureTimeSeriesAnalysis(request, () =>
                    {
                        if (request.TransformType.HasValue) model.TransformType = request.TransformType.Value;
                        if (request.TransformLambda.HasValue) model.SetTransformParameters(request.TransformLambda.Value);
                    },
                        setTraining: v => { model.UseDefaultTrainingSteps = false; model.TrainingTimeSteps = v; },
                        setForecast: v => analysis.ForecastingTimeSteps = v,
                        analysis.BayesianAnalysis, model, request.ParameterPriors);
                    resource = new AnalysisResource
                    {
                        Name = DefaultTimeSeriesName(request, "MA", source.Name),
                        Description = request.Description,
                        Kind = AnalysisKind.TimeSeries,
                        TimeSeriesModel = TimeSeriesModelType.Ma,
                        Ma = analysis,
                        TimeSeriesId = source.Id
                    };
                    break;
                }
                case TimeSeriesModelType.Arima:
                {
                    var model = new ARIMA(series, request.POrder ?? 1, request.DOrder ?? 0, request.QOrder ?? 0, includeIntercept);
                    var analysis = new ARIMAAnalysis(model);
                    ConfigureTimeSeriesAnalysis(request, () =>
                    {
                        if (request.TransformType.HasValue) model.TransformType = request.TransformType.Value;
                        if (request.TransformLambda.HasValue) model.SetTransformParameters(request.TransformLambda.Value);
                    },
                        setTraining: v => { model.UseDefaultTrainingSteps = false; model.TrainingTimeSteps = v; },
                        setForecast: v => analysis.ForecastingTimeSteps = v,
                        analysis.BayesianAnalysis, model, request.ParameterPriors);
                    resource = new AnalysisResource
                    {
                        Name = DefaultTimeSeriesName(request, "ARIMA", source.Name),
                        Description = request.Description,
                        Kind = AnalysisKind.TimeSeries,
                        TimeSeriesModel = TimeSeriesModelType.Arima,
                        Arima = analysis,
                        TimeSeriesId = source.Id
                    };
                    break;
                }
                case TimeSeriesModelType.Arimax:
                {
                    var model = new ARIMAX(series)
                    {
                        AROrderP = request.POrder ?? 1,
                        DiffOrderD = request.DOrder ?? 0,
                        MAOrderQ = request.QOrder ?? 0,
                        XOrderB = request.XOrder ?? 0,
                        IncludeIntercept = includeIntercept
                    };
                    if (request.TrendType.HasValue) model.TrendType = request.TrendType.Value;
                    if (request.IncludeSeasonality.HasValue) model.IncludeSeasonality = request.IncludeSeasonality.Value;
                    if (request.CovariateExtension.HasValue) model.CovariateExtension = request.CovariateExtension.Value;

                    var covariateIds = new List<Guid>();
                    if (request.CovariateTimeSeriesIds is { Count: > 0 })
                    {
                        var covariates = new List<TimeSeries>(request.CovariateTimeSeriesIds.Count);
                        foreach (var covariateId in request.CovariateTimeSeriesIds)
                        {
                            var covariateSource = _store.GetTimeSeries(covariateId)
                                ?? throw new ResourceNotFoundException("covariate time series", covariateId);
                            covariates.Add(CloneSeries(covariateSource.TimeSeries));
                            covariateIds.Add(covariateSource.Id);
                        }
                        model.SetCovariates(covariates);
                    }

                    var analysis = new ARIMAXAnalysis(model);
                    ConfigureTimeSeriesAnalysis(request, () =>
                    {
                        if (request.TransformType.HasValue) model.TransformType = request.TransformType.Value;
                        if (request.TransformLambda.HasValue) model.SetTransformParameters(request.TransformLambda.Value);
                    },
                        setTraining: v => { model.UseDefaultTrainingSteps = false; model.TrainingTimeSteps = v; },
                        setForecast: v => analysis.ForecastingTimeSteps = v,
                        analysis.BayesianAnalysis, model, request.ParameterPriors);
                    resource = new AnalysisResource
                    {
                        Name = DefaultTimeSeriesName(request, "ARIMAX", source.Name),
                        Description = request.Description,
                        Kind = AnalysisKind.TimeSeries,
                        TimeSeriesModel = TimeSeriesModelType.Arimax,
                        Arimax = analysis,
                        TimeSeriesId = source.Id,
                        CovariateTimeSeriesIds = covariateIds.Count > 0 ? covariateIds : null
                    };
                    break;
                }
                default:
                    throw new ArgumentException($"Unknown time-series model type '{request.ModelType}'.");
            }
            return _store.AddAnalysis(resource);
        }

        /// <summary>
        /// Rejects request fields that do not apply to the chosen time-series model type, naming
        /// every offending field — never silently ignored.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <exception cref="ArgumentException">Thrown when any inapplicable field is set.</exception>
        private static void RejectFieldsForModelType(CreateTimeSeriesAnalysisRequest request)
        {
            var offending = new List<string>();
            bool isArOrMa = request.ModelType is TimeSeriesModelType.Ar or TimeSeriesModelType.Ma;
            bool isArimax = request.ModelType == TimeSeriesModelType.Arimax;

            if (isArOrMa)
            {
                if (request.POrder.HasValue) offending.Add(nameof(request.POrder));
                if (request.DOrder.HasValue) offending.Add(nameof(request.DOrder));
                if (request.QOrder.HasValue) offending.Add(nameof(request.QOrder));
            }
            else if (request.Order.HasValue)
            {
                offending.Add(nameof(request.Order));
            }
            if (!isArimax)
            {
                if (request.XOrder.HasValue) offending.Add(nameof(request.XOrder));
                if (request.TrendType.HasValue) offending.Add(nameof(request.TrendType));
                if (request.IncludeSeasonality.HasValue) offending.Add(nameof(request.IncludeSeasonality));
                if (request.CovariateTimeSeriesIds is { Count: > 0 }) offending.Add(nameof(request.CovariateTimeSeriesIds));
                if (request.CovariateExtension.HasValue) offending.Add(nameof(request.CovariateExtension));
            }

            if (offending.Count > 0)
            {
                string fields = string.Join(", ", offending.Select(EnumHelperCamel));
                throw new ArgumentException(
                    $"The following fields do not apply to modelType '{EnumHelperCamel(request.ModelType.ToString())}' and must be omitted: {fields}.");
            }
        }

        /// <summary>
        /// Applies the shared time-series configuration in the safe order: transform and training
        /// window first, then the forecast horizon, MCMC options, and priors LAST (order setters
        /// rebuild the parameter list).
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <param name="applyTransform">Applies the transform onto the concrete model.</param>
        /// <param name="setTraining">Applies an explicit training window onto the concrete model.</param>
        /// <param name="setForecast">Applies the forecast horizon onto the concrete analysis.</param>
        /// <param name="bayesianAnalysis">The analysis's Bayesian estimator.</param>
        /// <param name="model">The concrete model, for prior application.</param>
        /// <param name="priors">The client-supplied parameter priors.</param>
        private void ConfigureTimeSeriesAnalysis(
            CreateTimeSeriesAnalysisRequest request,
            Action applyTransform,
            Action<int> setTraining,
            Action<int> setForecast,
            Estimation.BayesianAnalysis bayesianAnalysis,
            ModelBase model,
            List<ParameterPriorDto>? priors)
        {
            applyTransform();
            if (request.TrainingTimeSteps.HasValue)
            {
                setTraining(request.TrainingTimeSteps.Value);
            }
            if (request.ForecastingTimeSteps.HasValue)
            {
                setForecast(request.ForecastingTimeSteps.Value);
            }
            BayesianOptionsMapper.Apply(bayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            PriorMapper.ApplyParameterPriors(model, priors);
        }

        /// <summary>
        /// Builds the default display name for a time-series analysis.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <param name="modelLabel">The model family label (e.g., "ARIMA").</param>
        /// <param name="sourceName">The source series display name.</param>
        /// <returns>The display name.</returns>
        private static string DefaultTimeSeriesName(CreateTimeSeriesAnalysisRequest request, string modelLabel, string? sourceName)
        {
            return string.IsNullOrWhiteSpace(request.Name) ? $"{modelLabel} analysis of {sourceName}" : request.Name;
        }

        /// <summary>
        /// Converts a PascalCase field or enum name to the camelCase wire form for error messages.
        /// </summary>
        /// <param name="name">The PascalCase name.</param>
        /// <returns>The camelCase form.</returns>
        private static string EnumHelperCamel(string name)
        {
            return Helpers.EnumHelper.ToCamelCase(name);
        }

        /// <summary>
        /// Converts the jagged response surface of a coincident frequency request into the
        /// model's rectangular array, validating the shape.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The rectangular surface, indexed [x row, y column].</returns>
        /// <exception cref="ArgumentException">Thrown when the row count, a row length, or a value is inconsistent with the ordinates.</exception>
        private static double[,] ToResponseSurface(CreateCoincidentFrequencyAnalysisRequest request)
        {
            if (request.BivariateResponse == null || request.BivariateResponse.Count != request.XValues.Count)
            {
                throw new ArgumentException(
                    $"bivariateResponse must have exactly one row per xValues entry ({request.XValues.Count}), but has {request.BivariateResponse?.Count ?? 0}.");
            }
            var surface = new double[request.XValues.Count, request.YValues.Count];
            for (int i = 0; i < request.BivariateResponse.Count; i++)
            {
                var row = request.BivariateResponse[i];
                if (row == null || row.Count != request.YValues.Count)
                {
                    throw new ArgumentException(
                        $"bivariateResponse[{i}] must have exactly one entry per yValues entry ({request.YValues.Count}), but has {row?.Count ?? 0}.");
                }
                for (int j = 0; j < row.Count; j++)
                {
                    surface[i, j] = row[j];
                }
            }
            return surface;
        }

        /// <summary>
        /// Extracts the model-layer marginal model from an analysis resource for use in a
        /// bivariate distribution. Competing risks and composite analyses expose no marginal
        /// model and are rejected.
        /// </summary>
        /// <param name="resource">The marginal analysis resource.</param>
        /// <param name="role">The role name used in error messages ("marginal X" or "marginal Y").</param>
        /// <returns>The marginal model.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource's kind cannot serve as a bivariate marginal.</exception>
        private static IUnivariateModel GetMarginalModel(AnalysisResource resource, string role)
        {
            return resource.Kind switch
            {
                AnalysisKind.Univariate => resource.Univariate!.UnivariateDistribution,
                AnalysisKind.Bulletin17C => resource.Bulletin17C!.Bulletin17CDistribution,
                AnalysisKind.Mixture => resource.Mixture!.MixtureDistribution,
                AnalysisKind.PointProcess => resource.PointProcess!.PointProcess,
                _ => throw new ArgumentException(
                    $"An analysis of kind '{resource.Kind}' cannot be the {role} of a bivariate analysis. " +
                    "Valid marginal kinds: univariate, bulletin17c, mixture, pointprocess.")
            };
        }

        /// <summary>
        /// Counts the non-low-outlier exact observations the two marginals share by time index —
        /// the pairs the copula sample is built from (mirrors the model's two-pointer merge).
        /// </summary>
        /// <param name="frameX">The marginal-X data frame.</param>
        /// <param name="frameY">The marginal-Y data frame.</param>
        /// <returns>The matched pair count.</returns>
        private static int CountMatchedExactPairs(DataFrame frameX, DataFrame frameY)
        {
            var indexesX = new HashSet<int>();
            foreach (ExactData data in frameX.ExactSeries)
            {
                if (!data.IsLowOutlier) indexesX.Add(data.Index);
            }
            int count = 0;
            foreach (ExactData data in frameY.ExactSeries)
            {
                if (!data.IsLowOutlier && indexesX.Contains(data.Index)) count++;
            }
            return count;
        }

        /// <summary>
        /// Extracts the model-layer univariate-analysis interface from a component resource for
        /// use as a composite component.
        /// </summary>
        /// <param name="resource">The component analysis resource.</param>
        /// <returns>The component viewed as <see cref="IUnivariateAnalysis"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource's kind cannot serve as a composite component.</exception>
        private static IUnivariateAnalysis GetUnivariateAnalysis(AnalysisResource resource)
        {
            return resource.Kind switch
            {
                AnalysisKind.Univariate => resource.Univariate!,
                AnalysisKind.Bulletin17C => resource.Bulletin17C!,
                AnalysisKind.Mixture => resource.Mixture!,
                AnalysisKind.PointProcess => resource.PointProcess!,
                AnalysisKind.CompetingRisks => resource.CompetingRisks!,
                _ => throw new ArgumentException(
                    $"An analysis of kind '{resource.Kind}' cannot be a composite component. " +
                    "Valid component kinds: univariate, bulletin17c, mixture, pointprocess, competingrisks.")
            };
        }

        /// <summary>
        /// Validates a component-distribution list: 1-3 entries, each supported by the target
        /// model family.
        /// </summary>
        /// <param name="distributions">The client-supplied component types.</param>
        /// <param name="isSupported">The model family's support predicate.</param>
        /// <param name="familyName">The family name used in error messages (e.g., "mixture").</param>
        /// <exception cref="ArgumentException">Thrown when the list is empty, too long, or names an unsupported type.</exception>
        private static void ValidateComponentDistributions(
            List<Numerics.Distributions.UnivariateDistributionType> distributions,
            Func<Numerics.Distributions.UnivariateDistributionType, bool> isSupported,
            string familyName)
        {
            if (distributions == null || distributions.Count == 0)
            {
                throw new ArgumentException($"At least one component distribution is required for a {familyName} analysis.");
            }
            if (distributions.Count > 3)
            {
                throw new ArgumentException($"A {familyName} analysis supports at most 3 component distributions.");
            }
            foreach (var type in distributions)
            {
                if (!isSupported(type))
                {
                    throw new ArgumentException(
                        $"Distribution '{type}' is not supported by the {familyName} analysis. See GET api/metadata/distributions.");
                }
            }
        }
    }
}
