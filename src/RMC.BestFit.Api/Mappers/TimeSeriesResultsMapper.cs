using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps time-series analysis results to the response DTO. All four model families share the
    /// results shape: <c>ModeCurve</c>/<c>MeanCurve</c> span the observed series plus the
    /// forecast horizon, and <c>ConfidenceIntervals</c> is <c>[n, 3]</c> (time index, lower,
    /// upper).
    /// </summary>
    public static class TimeSeriesResultsMapper
    {
        /// <summary>
        /// Builds the results response for a time-series analysis of any model family.
        /// </summary>
        /// <param name="resource">The time-series analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is not a time-series analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has never been run.</exception>
        public static TimeSeriesResultsResponse ToResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind != AnalysisKind.TimeSeries)
            {
                throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce time-series results.", nameof(resource));
            }

            // One extraction switch: results object, estimator, parameters, and the per-family
            // settings echo. The curve/summary assembly below is family-independent.
            UncertaintyAnalysisResults? results;
            BayesianAnalysis bayesian;
            IReadOnlyList<ModelParameter> parameters;
            string transform;
            int dataLength;
            int trainingSteps;
            int forecastSteps;
            int? order = null, pOrder = null, dOrder = null, qOrder = null, xOrder = null;

            switch (resource.TimeSeriesModel)
            {
                case TimeSeriesModelType.Ar:
                {
                    var analysis = resource.Ar!;
                    var model = analysis.AutoRegressive;
                    results = analysis.AnalysisResults;
                    bayesian = analysis.BayesianAnalysis;
                    parameters = model.Parameters;
                    transform = model.TransformType.ToString();
                    dataLength = model.TimeSeries?.Count ?? 0;
                    trainingSteps = model.TrainingTimeSteps;
                    forecastSteps = analysis.ForecastingTimeSteps;
                    order = model.Order;
                    break;
                }
                case TimeSeriesModelType.Ma:
                {
                    var analysis = resource.Ma!;
                    var model = analysis.MovingAverage;
                    results = analysis.AnalysisResults;
                    bayesian = analysis.BayesianAnalysis;
                    parameters = model.Parameters;
                    transform = model.TransformType.ToString();
                    dataLength = model.TimeSeries?.Count ?? 0;
                    trainingSteps = model.TrainingTimeSteps;
                    forecastSteps = analysis.ForecastingTimeSteps;
                    order = model.Order;
                    break;
                }
                case TimeSeriesModelType.Arima:
                {
                    var analysis = resource.Arima!;
                    var model = analysis.ARIMA;
                    results = analysis.AnalysisResults;
                    bayesian = analysis.BayesianAnalysis;
                    parameters = model.Parameters;
                    transform = model.TransformType.ToString();
                    dataLength = model.TimeSeries?.Count ?? 0;
                    trainingSteps = model.TrainingTimeSteps;
                    forecastSteps = analysis.ForecastingTimeSteps;
                    pOrder = model.POrder;
                    dOrder = model.DOrder;
                    qOrder = model.QOrder;
                    break;
                }
                case TimeSeriesModelType.Arimax:
                {
                    var analysis = resource.Arimax!;
                    var model = analysis.ARIMAX;
                    results = analysis.AnalysisResults;
                    bayesian = analysis.BayesianAnalysis;
                    parameters = model.Parameters;
                    transform = model.TransformType.ToString();
                    dataLength = model.TimeSeries?.Count ?? 0;
                    trainingSteps = model.TrainingTimeSteps;
                    forecastSteps = analysis.ForecastingTimeSteps;
                    pOrder = model.AROrderP;
                    dOrder = model.DiffOrderD;
                    qOrder = model.MAOrderQ;
                    xOrder = model.XOrderB;
                    break;
                }
                default:
                    throw new ArgumentException("The time-series resource has no model discriminator.", nameof(resource));
            }

            if (results == null)
            {
                throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            }
            var parameterNames = parameters.Select(p => p.DisplayName).ToList();

            var curve = new TimeSeriesCurveDto
            {
                ModeCurve = results.ModeCurve?.ToList(),
                MeanCurve = results.MeanCurve?.ToList(),
                CredibleIntervalWidth = bayesian.CredibleIntervalWidth
            };
            if (results.ConfidenceIntervals is { } intervals && intervals.GetLength(1) >= 3)
            {
                // [n, 3]: column 0 = time index, column 1 = lower, column 2 = upper.
                int rows = intervals.GetLength(0);
                var indices = new List<double>(rows);
                var lower = new List<double>(rows);
                var upper = new List<double>(rows);
                for (int i = 0; i < rows; i++)
                {
                    indices.Add(intervals[i, 0]);
                    lower.Add(intervals[i, 1]);
                    upper.Add(intervals[i, 2]);
                }
                curve.TimeIndices = indices;
                curve.CiLower = lower;
                curve.CiUpper = upper;
            }

            return new TimeSeriesResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                ModelType = EnumHelper.ToCamelCase(resource.TimeSeriesModel.ToString()!),
                TransformType = EnumHelper.ToCamelCase(transform),
                DataLength = dataLength,
                TrainingTimeSteps = trainingSteps,
                ForecastingTimeSteps = forecastSteps,
                Order = order,
                POrder = pOrder,
                DOrder = dOrder,
                QOrder = qOrder,
                XOrder = xOrder,
                Curve = curve,
                Parameters = parameters
                    .Select(p => new ParameterValueDto { Name = p.DisplayName, Value = p.Value })
                    .ToList(),
                ParameterSummaries = ResultsMapper.BuildParameterSummaries(parameterNames, bayesian.Results, includeChainDiagnostics: true),
                InformationCriteria = ResultsMapper.BuildInformationCriteria(results, bayesian),
                Diagnostics = ResultsMapper.BuildMcmcDiagnostics(bayesian, parameterNames)
            };
        }
    }
}
