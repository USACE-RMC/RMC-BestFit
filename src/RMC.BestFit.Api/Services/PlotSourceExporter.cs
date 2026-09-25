using System.Text.Json;
using System.Xml.Linq;
using System.Globalization;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using Numerics.Sampling;
using Numerics.Data.Statistics;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Diagnostics;

namespace RMC.BestFit.Api.Services
{
    /// <summary>Copies only stored plot source state while excluding all runs on the resource and its live components.</summary>
    public static class PlotSourceExporter
    {
        /// <summary>Builds a detached plot source snapshot from one completed stored run.</summary>
        /// <param name="resource">The analysis to export.</param>
        /// <param name="includeSamples">Include full saved draws and chains.</param>
        /// <returns>A versioned plot source response.</returns>
        public static PlotSourceResponse Export(AnalysisResource resource, bool includeSamples = false)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (!resource.RunLock.Wait(0))
                throw new ResourceConflictException($"The analysis '{resource.Id}' is currently running; plot source is unavailable.");

            var held = new List<AnalysisResource>();
            try
            {
                // Match the run path's resource-first, distinct-component ordering.
                foreach (var component in resource.ComponentResources?.GroupBy(r => r.Id).Select(g => g.First())
                    ?? Enumerable.Empty<AnalysisResource>())
                {
                    if (!component.RunLock.Wait(0))
                        throw new ResourceConflictException($"The component analysis '{component.Id}' is currently running; plot source is unavailable.");
                    held.Add(component);
                }

                if (resource.State != AnalysisRunState.Succeeded || !resource.Analysis.IsEstimated)
                    throw new ResourceNotFoundException($"The analysis '{resource.Id}' has no completed current run to export.");

                // Live component objects can be rerun after this parent completed. Their
                // current models must not be combined with the parent's older result curves.
                foreach (var component in held)
                {
                    if (resource.LastRunUtc.HasValue && component.LastRunUtc > resource.LastRunUtc)
                        throw new ResourceConflictException($"The component analysis '{component.Id}' was rerun after analysis '{resource.Id}'; rerun the parent before exporting its plot source.");
                }

                var result = MapResults(resource);
                var analysisXml = MapAnalysisXml(resource);
                // B17C's legacy serializer includes fitted GMM/bootstrap payloads. Keep this
                // field restricted to settings; stored curves and optional draws are separate.
                if (resource.Kind == AnalysisKind.Bulletin17C)
                {
                    analysisXml.Elements().Where(e => e.Name.LocalName is "GeneralizedMethodOfMoments" or "BootstrapDiagnostics").Remove();
                }
                var bayesian = GetBayesian(resource);
                var response = new PlotSourceResponse
                {
                    AnalysisId = resource.Id,
                    Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                    State = "succeeded",
                    LastRunUtc = resource.LastRunUtc,
                    ModelType = resource.TimeSeriesModel?.ToString() is string modelType ? EnumHelper.ToCamelCase(modelType) : null,
                    InputDataId = resource.InputDataId,
                    TimeSeriesId = resource.TimeSeriesId,
                    StageTimeSeriesId = resource.StageTimeSeriesId,
                    DischargeTimeSeriesId = resource.DischargeTimeSeriesId,
                    CovariateTimeSeriesIds = resource.CovariateTimeSeriesIds?.ToList(),
                    BivariateAnalysisId = resource.BivariateAnalysisId,
                    MarginalXAnalysisId = resource.MarginalXAnalysisId,
                    MarginalYAnalysisId = resource.MarginalYAnalysisId,
                    ComponentAnalysisIds = resource.ComponentAnalysisIds?.ToList(),
                    AnalysisXml = analysisXml.ToString(SaveOptions.DisableFormatting),
                    ModelXml = MapModelXml(resource),
                    DataFrameXml = MapDataFrameXml(resource),
                    MarginalXDataFrameXml = MapMarginalDataFrameXml(resource, xAxis: true),
                    MarginalYDataFrameXml = MapMarginalDataFrameXml(resource, xAxis: false),
                    MarginalXModelXml = MapMarginalModelXml(resource, xAxis: true),
                    MarginalYModelXml = MapMarginalModelXml(resource, xAxis: false),
                    Results = JsonSerializer.SerializeToElement(result, result.GetType(), McpJson.Options),
                    Chronology = MapChronology(resource),
                    ComponentCurves = MapComponentCurves(resource),
                    MeanLogLikelihood = bayesian?.Results?.MeanLogLikelihood?.ToList(),
                    SampleOrigin = bayesian?.Results == null ? null
                        : resource.Kind == AnalysisKind.Bulletin17C ? "bulletin17CUncertainty" : "mcmc",
                    UncertaintyMethod = resource.Bulletin17C == null ? null
                        : EnumHelper.ToCamelCase(resource.Bulletin17C.UncertaintyMethod.ToString())
                };
                AddSeries(resource, response.Series);
                var frame = GetFrequencyFrame(resource);
                if (frame != null) response.Observations = MapObservations(frame);
                if (resource.PointProcess is { } pointProcess)
                {
                    var ams = new DataFrame(pointProcess.PointProcess.DataFrame.ToXElement());
                    ams.ApplyLangbeinConversion(pointProcess.PointProcess.Lambda);
                    response.AmsObservations = MapObservations(ams);
                }
                if (resource.DistributionFitting is { } fitting)
                    AddFittingGeometry(fitting, response);
                if (resource.Kind == AnalysisKind.RatingCurve)
                    response.ResidualPlot = MapRatingResiduals(resource);
                if (resource.Kind == AnalysisKind.TimeSeries)
                    MapTimeSeriesGeometry(resource, response);
                if (resource.Kind == AnalysisKind.Bivariate)
                    response.BivariatePlot = MapBivariateGeometry(resource);
                if (bayesian != null)
                    response.InfluenceDiagnostics = MapInfluence(resource, bayesian);
                if (bayesian?.Results is { } posterior)
                {
                    response.ParameterDiagnostics = MapDiagnostics(bayesian!);
                    if (includeSamples)
                    {
                        response.Samples = new PlotSourceSamplesDto
                        {
                            Output = posterior.Output.Select(MapSample).ToList(),
                            MarkovChains = posterior.MarkovChains?.Select(chain => chain.Select(MapSample).ToList()).ToList()
                        };
                    }
                }
                return response;
            }
            finally
            {
                foreach (var component in held) component.RunLock.Release();
                resource.RunLock.Release();
            }
        }

        private static object MapResults(AnalysisResource resource) => resource.Kind switch
        {
            AnalysisKind.RatingCurve => ResultsMapper.ToRatingCurveResults(resource),
            AnalysisKind.DistributionFitting => DistributionFittingResultsMapper.ToResults(resource),
            AnalysisKind.Bivariate => BivariateResultsMapper.ToBivariateResults(resource),
            AnalysisKind.CoincidentFrequency => BivariateResultsMapper.ToCoincidentFrequencyResults(resource),
            AnalysisKind.TimeSeries => TimeSeriesResultsMapper.ToResults(resource),
            _ => ResultsMapper.ToFrequencyResults(resource)
        };

        private static PlotSourceChronologyDto? MapChronology(AnalysisResource resource)
        {
            var analysis = resource.Univariate;
            if (analysis == null || !analysis.UnivariateDistribution.IsNonstationary)
                return null;
            var curve = analysis.ChronologyAnalysisResults;
            var series = analysis.UnivariateDistribution.DataFrame.FullTimeSeries;
            if (curve == null || series.Count == 0)
                return null;
            var mean = curve.MeanCurve;
            var mode = curve.ModeCurve;
            var confidence = curve.ConfidenceIntervals;
            if (mean == null || mode == null || confidence == null)
                throw new ResourceConflictException("Stored chronology arrays are incomplete for the current run.");
            var count = mean.Length;
            if (mode.Length != count || confidence.GetLength(0) != count)
                throw new ResourceConflictException("Stored chronology arrays are not aligned to the current run.");
            var start = series.First().Index;
            return new PlotSourceChronologyDto
            {
                Indices = Enumerable.Range(0, count).Select(index => (double)(start + index)).ToList(),
                MeanCurve = mean.ToList(),
                ModeCurve = mode.ToList(),
                CiLower = Enumerable.Range(0, count).Select(index => confidence[index, 0]).ToList(),
                CiUpper = Enumerable.Range(0, count).Select(index => confidence[index, 1]).ToList(),
                CredibleIntervalWidth = analysis.BayesianAnalysis.CredibleIntervalWidth
            };
        }

        private static List<PlotSourceCurveDto> MapComponentCurves(AnalysisResource resource)
        {
            var result = new List<PlotSourceCurveDto>();
            if (resource.Composite is { } composite)
            {
                for (var index = 0; index < composite.Analyses.Count; index++)
                {
                    var item = composite.Analyses[index];
                    var analysis = item.UnivariateAnalysis;
                    var probabilities = analysis.ProbabilityOrdinates.ToList();
                    var values = analysis.AnalysisResults?.ModeCurve;
                    if (values == null || values.Length != probabilities.Count)
                        throw new ResourceConflictException("Composite component curve is unavailable for the current run.");
                    result.Add(new PlotSourceCurveDto
                    {
                        Name = resource.ComponentResources?.ElementAtOrDefault(index)?.Name ?? $"Component {index + 1}",
                        Probabilities = probabilities,
                        Values = values.ToList()
                    });
                }
            }
            if (resource.PointProcess is { PointProcess.IsSeasonal: true, AnalysisResults.ParentDistribution: CompetingRisks seasonal } pointProcess)
            {
                var probabilities = pointProcess.ProbabilityOrdinates.ToList();
                for (var index = 0; index < seasonal.Distributions.Count; index++)
                {
                    var distribution = seasonal.Distributions[index];
                    result.Add(new PlotSourceCurveDto
                    {
                        Name = $"Season {index + 1}",
                        Probabilities = probabilities.ToList(),
                        Values = probabilities.Select(probability => distribution.InverseCDF(1d - probability)).ToList()
                    });
                }
            }
            return result;
        }

        private static XElement MapAnalysisXml(AnalysisResource resource) => resource.Kind switch
        {
            AnalysisKind.Univariate => resource.Univariate!.ToXElement(),
            AnalysisKind.Bulletin17C => resource.Bulletin17C!.ToXElement(),
            AnalysisKind.RatingCurve => resource.RatingCurve!.ToXElement(),
            AnalysisKind.Mixture => resource.Mixture!.ToXElement(),
            AnalysisKind.PointProcess => resource.PointProcess!.ToXElement(),
            AnalysisKind.CompetingRisks => resource.CompetingRisks!.ToXElement(),
            AnalysisKind.Composite => resource.Composite!.ToXElement(),
            AnalysisKind.DistributionFitting => resource.DistributionFitting!.ToXElement(),
            AnalysisKind.Bivariate => resource.Bivariate!.ToXElement(),
            AnalysisKind.CoincidentFrequency => resource.CoincidentFrequency!.ToXElement(),
            AnalysisKind.TimeSeries => resource.TimeSeriesModel switch
            {
                TimeSeriesModelType.Ar => resource.Ar!.ToXElement(),
                TimeSeriesModelType.Ma => resource.Ma!.ToXElement(),
                TimeSeriesModelType.Arima => resource.Arima!.ToXElement(),
                TimeSeriesModelType.Arimax => resource.Arimax!.ToXElement(),
                _ => throw new InvalidOperationException("Missing time series model discriminator.")
            },
            _ => throw new InvalidOperationException("Unsupported analysis kind.")
        };

        private static string? MapModelXml(AnalysisResource resource) => (resource.Kind switch
        {
            AnalysisKind.Univariate => resource.Univariate!.UnivariateDistribution.ToXElement(),
            AnalysisKind.Bulletin17C => resource.Bulletin17C!.Bulletin17CDistribution.ToXElement(),
            AnalysisKind.RatingCurve => resource.RatingCurve!.RatingCurve.ToXElement(),
            AnalysisKind.Mixture => resource.Mixture!.MixtureDistribution.ToXElement(),
            AnalysisKind.PointProcess => resource.PointProcess!.PointProcess.ToXElement(),
            AnalysisKind.CompetingRisks => resource.CompetingRisks!.CompetingRisksDistribution.ToXElement(),
            AnalysisKind.Bivariate => resource.Bivariate!.BivariateDistribution.ToXElement(),
            AnalysisKind.TimeSeries => resource.TimeSeriesModel switch
            {
                TimeSeriesModelType.Ar => resource.Ar!.AutoRegressive.ToXElement(),
                TimeSeriesModelType.Ma => resource.Ma!.MovingAverage.ToXElement(),
                TimeSeriesModelType.Arima => resource.Arima!.ARIMA.ToXElement(),
                TimeSeriesModelType.Arimax => resource.Arimax!.ARIMAX.ToXElement(),
                _ => null
            },
            _ => null
        })?.ToString(SaveOptions.DisableFormatting);

        private static string? MapDataFrameXml(AnalysisResource resource) => (resource.Kind switch
        {
            AnalysisKind.Univariate => resource.Univariate!.UnivariateDistribution.DataFrame,
            AnalysisKind.Bulletin17C => resource.Bulletin17C!.Bulletin17CDistribution.DataFrame,
            AnalysisKind.Mixture => resource.Mixture!.MixtureDistribution.DataFrame,
            AnalysisKind.PointProcess => resource.PointProcess!.PointProcess.DataFrame,
            AnalysisKind.CompetingRisks => resource.CompetingRisks!.CompetingRisksDistribution.DataFrame,
            AnalysisKind.DistributionFitting => resource.DistributionFitting!.DataFrame,
            _ => null
        })?.ToXElement().ToString(SaveOptions.DisableFormatting);

        private static DataFrame? GetFrequencyFrame(AnalysisResource resource) => resource.Kind switch
        {
            AnalysisKind.Univariate => resource.Univariate!.UnivariateDistribution.DataFrame,
            AnalysisKind.Bulletin17C => resource.Bulletin17C!.Bulletin17CDistribution.DataFrame,
            AnalysisKind.Mixture => resource.Mixture!.MixtureDistribution.DataFrame,
            AnalysisKind.PointProcess => resource.PointProcess!.PointProcess.DataFrame,
            AnalysisKind.CompetingRisks => resource.CompetingRisks!.CompetingRisksDistribution.DataFrame,
            AnalysisKind.DistributionFitting => resource.DistributionFitting!.DataFrame,
            _ => null
        };

        private static List<PlotSourceObservationDto> MapObservations(DataFrame frame)
        {
            var result = new List<PlotSourceObservationDto>();
            result.AddRange(frame.ExactSeries.Select(row => new PlotSourceObservationDto
            {
                Kind = "exact", Index = row.Index, Value = row.Value, Aep = row.PlottingPosition,
                LowOutlier = ((ExactData)row).IsLowOutlier
            }));
            result.AddRange(frame.UncertainSeries.Select(row => new PlotSourceObservationDto
            {
                Kind = "uncertain", Index = row.Index, Value = row.Value, Aep = row.PlottingPosition,
                Lower = ((UncertainData)row).LowerValue, Upper = ((UncertainData)row).UpperValue
            }));
            result.AddRange(frame.IntervalSeries.Select(row => new PlotSourceObservationDto
            {
                Kind = "interval", Index = row.Index, Value = row.Value, Aep = row.PlottingPosition,
                Lower = ((IntervalData)row).LowerValue, Upper = ((IntervalData)row).UpperValue
            }));
            result.AddRange(frame.ThresholdSeries.Select(row => new PlotSourceObservationDto
            {
                Kind = "threshold", Value = row.Value,
                Start = ((ThresholdData)row).StartIndex, End = ((ThresholdData)row).EndIndex
            }));
            return result;
        }

        private static void AddFittingGeometry(RMC.BestFit.Analyses.FittingAnalysis fitting, PlotSourceResponse response)
        {
            var values = response.Observations.Where(o => o.Kind != "threshold").Select(o => o.Value).ToArray();
            if (values.Length == 0) return;
            var probabilities = response.Observations.Where(o => o.Kind != "threshold" && o.Aep.HasValue)
                .Select(o => 1d - o.Aep!.Value).Order().ToArray();
            var ordered = values.Order().ToArray();
            var bins = new Histogram(values, Math.Max(1, (int)(1 + 3.322 * Math.Log(values.Length))));
            for (var i = 0; i < bins.NumberOfBins; i++)
                response.FittingHistogram.Add(new PlotHistogramBinDto
                { LowerBound = bins[i].LowerBound, UpperBound = bins[i].UpperBound, Frequency = bins[i].Frequency });
            var low = values.Min();
            var high = values.Max();
            if (low <= 0 || high <= 0) return;
            var xValues = Stratify.XValues(new StratificationOptions(
                low - Math.Pow(10, Math.Floor(Math.Log10(low))),
                high + Math.Pow(10, Math.Floor(Math.Log10(high))), 1000));
            foreach (var fit in fitting.FittedDistributions.Where(f => f.FitSucceeded && f.ShowResults && f.Distribution is not null))
            {
                var distribution = fit.Distribution!;
                var curve = new PlotSourceFittingCurveDto { Name = distribution.DisplayName };
                curve.Frequency = fitting.ProbabilityOrdinates.Select(p => new PlotPairDto
                    { X = p, Y = distribution.InverseCDF(1d - p) }).ToList();
                curve.Pdf = CopyPairs(distribution.CreatePDFGraph(xValues));
                curve.Cdf = CopyPairs(distribution.CreateCDFGraph(xValues));
                if (ordered.Length == probabilities.Length)
                {
                    curve.Pp = ordered.Select((value, i) => new PlotPairDto
                        { X = distribution.CDF(value), Y = probabilities[i] }).ToList();
                    curve.Qq = ordered.Select((value, i) => new PlotPairDto
                        { X = value, Y = distribution.InverseCDF(probabilities[i]) }).ToList();
                }
                response.FittingCurves.Add(curve);
            }
        }

        private static PlotSourceResidualDto MapRatingResiduals(AnalysisResource resource)
        {
            var model = resource.RatingCurve!.RatingCurve;
            var parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
            var result = MapResiduals(model.Residuals(parameters), parameters.Last(), defaultBins: false);
            result.Fitted = model.FittedValues(parameters).ToList();
            result.AlignedObservations = model.GetAlignedObservations().Select(pair => new PlotPairDto
                { X = pair.Discharge, Y = pair.Stage }).ToList();
            return result;
        }

        private static void MapTimeSeriesGeometry(AnalysisResource resource, PlotSourceResponse response)
        {
            var (series, training, residuals, parameters) = resource.TimeSeriesModel switch
            {
                TimeSeriesModelType.Ar => (resource.Ar!.AutoRegressive.TimeSeries,
                    resource.Ar.AutoRegressive.TrainingTimeSeries,
                    resource.Ar.AutoRegressive.Residuals(resource.Ar.AutoRegressive.Parameters.Select(p => p.Value).ToArray()),
                    resource.Ar.AutoRegressive.Parameters.Select(p => p.Value).ToArray()),
                TimeSeriesModelType.Ma => (resource.Ma!.MovingAverage.TimeSeries,
                    resource.Ma.MovingAverage.TrainingTimeSeries,
                    resource.Ma.MovingAverage.Residuals(resource.Ma.MovingAverage.Parameters.Select(p => p.Value).ToArray()),
                    resource.Ma.MovingAverage.Parameters.Select(p => p.Value).ToArray()),
                TimeSeriesModelType.Arima => (resource.Arima!.ARIMA.TimeSeries,
                    resource.Arima.ARIMA.TrainingTimeSeries,
                    resource.Arima.ARIMA.Residuals(resource.Arima.ARIMA.Parameters.Select(p => p.Value).ToArray()),
                    resource.Arima.ARIMA.Parameters.Select(p => p.Value).ToArray()),
                TimeSeriesModelType.Arimax => (resource.Arimax!.ARIMAX.TimeSeries,
                    resource.Arimax.ARIMAX.TrainingTimeSeries,
                    resource.Arimax.ARIMAX.Residuals(resource.Arimax.ARIMAX.Parameters.Select(p => p.Value).ToArray()),
                    resource.Arimax.ARIMAX.Parameters.Select(p => p.Value).ToArray()),
                _ => throw new InvalidOperationException("Missing time series model discriminator.")
            };
            var resultLength = ((TimeSeriesResultsResponse)MapResults(resource)).Curve?.MeanCurve?.Count ?? 0;
            if (series.Count == 0 || series.TimeInterval.ToString() == "Irregular")
                throw new ResourceConflictException("Time series dates are unavailable for the current run.");
            var date = series[0].Index;
            for (var i = 0; i < resultLength; i++)
            {
                response.ResultDates.Add(date);
                date = TimeSeries.AddTimeInterval(date, series.TimeInterval);
            }
            var plot = MapResiduals(residuals, parameters.Last(), defaultBins: true);
            plot.Dates = training.Take(residuals.Length).Select(p => p.Index).ToList();
            if (residuals.Length >= 10 && residuals.All(double.IsFinite))
            {
                plot.Acf = CopyPairs(Autocorrelation.Function(residuals));
                plot.Pacf = CopyPairs(Autocorrelation.Function(residuals, -1, Autocorrelation.Type.Partial));
                plot.CorrelationConfidenceInterval = Autocorrelation.CorrelationConfidenceInterval(residuals.Length).ToList();
            }
            response.ResidualPlot = plot;
        }

        private static PlotSourceResidualDto MapResiduals(double[] residuals, double errorScale, bool defaultBins)
        {
            var result = new PlotSourceResidualDto { Residuals = residuals.ToList(), ErrorScale = errorScale };
            var finite = residuals.Where(double.IsFinite).ToArray();
            if (finite.Length < 2) return result;
            var histogram = defaultBins ? new Histogram(finite)
                : new Histogram(finite, Math.Max(1, (int)(1 + 3.322 * Math.Log(finite.Length))));
            for (var i = 0; i < histogram.NumberOfBins; i++)
                result.Histogram.Add(new PlotHistogramBinDto
                { LowerBound = histogram[i].LowerBound, UpperBound = histogram[i].UpperBound, Frequency = histogram[i].Frequency });
            if (errorScale > 0 && double.IsFinite(errorScale))
                result.NormalPdf = CopyPairs(new Normal(0, errorScale).CreatePDFGraph());
            var ordered = finite.Order().ToArray();
            var moments = Statistics.MeanStandardDeviation(ordered);
            if (moments.Item2 > 0 && double.IsFinite(moments.Item2))
            {
                var normal = new Normal(moments.Item1, moments.Item2);
                var positions = PlottingPositions.Weibull(ordered.Length);
                result.Qq = ordered.Select((value, index) => new PlotPairDto
                    { X = normal.InverseCDF(positions[index]), Y = value }).ToList();
            }
            return result;
        }

        private static PlotSourceBivariateDto MapBivariateGeometry(AnalysisResource resource)
        {
            var analysis = resource.Bivariate!;
            var model = analysis.BivariateDistribution;
            var xDistribution = model.MarginalX.Distribution!.Clone();
            var yDistribution = model.MarginalY.Distribution!.Clone();
            var copula = model.Copula.Clone();
            var xRows = model.MarginalX.DataFrame.ExactSeries.Cast<ExactData>()
                .Where(row => !row.IsLowOutlier).OrderBy(row => row.Index).ToArray();
            var yRows = model.MarginalY.DataFrame.ExactSeries.Cast<ExactData>()
                .Where(row => !row.IsLowOutlier).OrderBy(row => row.Index).ToArray();
            var result = new PlotSourceBivariateDto();
            var xi = 0;
            var yi = 0;
            while (xi < xRows.Length && yi < yRows.Length)
            {
                if (xRows[xi].Index == yRows[yi].Index)
                {
                    result.Observed.Add(new PlotPairDto { X = xRows[xi].Value, Y = yRows[yi].Value });
                    xi++; yi++;
                }
                else if (xRows[xi].Index < yRows[yi].Index) xi++;
                else yi++;
            }
            if (result.Observed.Count == 0)
                throw new ResourceConflictException("Bivariate observations are unavailable for the current run.");
            result.ObservedCdf = result.Observed.Select(pair => new PlotPairDto
                { X = xDistribution.CDF(pair.X), Y = yDistribution.CDF(pair.Y) }).ToList();
            var simulated = model.GenerateRandomValues(Math.Min(10000, analysis.BayesianAnalysis.OutputLength),
                analysis.BayesianAnalysis.PRNGSeed);
            for (var i = 0; i < simulated.GetLength(0); i++)
                result.Simulated.Add(new PlotPairDto { X = simulated[i, 0], Y = simulated[i, 1] });
            result.SimulatedCdf = result.Simulated.Select(pair => new PlotPairDto
                { X = xDistribution.CDF(pair.X), Y = yDistribution.CDF(pair.Y) }).ToList();
            var p = 1d / Math.Pow(10, Math.Ceiling(Math.Log10(result.Observed.Count) + 2));
            static List<double> Grid(Numerics.Distributions.UnivariateDistributionBase distribution, double probability)
            {
                var bins = Stratify.XValues(new StratificationOptions(
                    distribution.InverseCDF(probability), distribution.InverseCDF(1d - probability), 99), false);
                var grid = bins.Select(bin => bin.LowerBound).ToList();
                grid.Add(bins.Last().UpperBound);
                return grid;
            }
            result.XGrid = Grid(xDistribution, p);
            result.YGrid = Grid(yDistribution, p);
            result.XCdf = result.XGrid.Select(xDistribution.CDF).ToList();
            result.YCdf = result.YGrid.Select(yDistribution.CDF).ToList();
            var maxLogPdf = double.NegativeInfinity;
            for (var j = 0; j < result.YGrid.Count; j++)
            {
                var densityRow = new List<double>();
                var exceedanceRow = new List<double>();
                for (var i = 0; i < result.XGrid.Count; i++)
                {
                    var value = copula.LogPDF(result.XCdf[i], result.YCdf[j])
                        + xDistribution.LogPDF(result.XGrid[i]) + yDistribution.LogPDF(result.YGrid[j]);
                    densityRow.Add(value);
                    exceedanceRow.Add(copula.ANDJointExceedanceProbability(result.XCdf[i], result.YCdf[j]));
                    maxLogPdf = Math.Max(maxLogPdf, value);
                }
                result.LogPdf.Add(densityRow);
                result.JointExceedance.Add(exceedanceRow);
            }
            var minLevel = Math.Log(1d / Math.Pow(10, Math.Ceiling(Math.Log10(1d / Math.Exp(maxLogPdf)) + 2.5)));
            var levels = Stratify.XValues(new StratificationOptions(minLevel, maxLogPdf, 9), false);
            result.DensityLevels = levels.Select(bin => bin.LowerBound).ToList();
            result.DensityLevels.Add(levels.Last().UpperBound);
            return result;
        }

        private static string? MapMarginalDataFrameXml(AnalysisResource resource, bool xAxis)
        {
            var bivariate = resource.Kind switch
            {
                AnalysisKind.Bivariate => resource.Bivariate,
                AnalysisKind.CoincidentFrequency => resource.BivariateResource?.Bivariate,
                _ => null
            };
            var frame = xAxis
                ? bivariate?.BivariateDistribution.MarginalX?.DataFrame
                : bivariate?.BivariateDistribution.MarginalY?.DataFrame;
            return frame?.ToXElement().ToString(SaveOptions.DisableFormatting);
        }

        private static string? MapMarginalModelXml(AnalysisResource resource, bool xAxis)
        {
            var bivariateResource = resource.Kind switch
            {
                AnalysisKind.Bivariate => resource,
                AnalysisKind.CoincidentFrequency => resource.BivariateResource,
                _ => null
            };
            var marginal = xAxis ? bivariateResource?.MarginalXResource : bivariateResource?.MarginalYResource;
            return marginal == null ? null : MapModelXml(marginal);
        }

        private static BayesianAnalysis? GetBayesian(AnalysisResource resource) => resource.Kind switch
        {
            AnalysisKind.Univariate => resource.Univariate!.BayesianAnalysis,
            AnalysisKind.Bulletin17C => resource.Bulletin17C!.BayesianAnalysis,
            AnalysisKind.RatingCurve => resource.RatingCurve!.BayesianAnalysis,
            AnalysisKind.Mixture => resource.Mixture!.BayesianAnalysis,
            AnalysisKind.PointProcess => resource.PointProcess!.BayesianAnalysis,
            AnalysisKind.CompetingRisks => resource.CompetingRisks!.BayesianAnalysis,
            AnalysisKind.Bivariate => resource.Bivariate!.BayesianAnalysis,
            AnalysisKind.CoincidentFrequency => resource.CoincidentFrequency!.BayesianAnalysis,
            AnalysisKind.TimeSeries => resource.TimeSeriesModel switch
            {
                TimeSeriesModelType.Ar => resource.Ar!.BayesianAnalysis,
                TimeSeriesModelType.Ma => resource.Ma!.BayesianAnalysis,
                TimeSeriesModelType.Arima => resource.Arima!.BayesianAnalysis,
                TimeSeriesModelType.Arimax => resource.Arimax!.BayesianAnalysis,
                _ => null
            },
            _ => null
        };

        private static void AddSeries(AnalysisResource resource, List<PlotSourceSeriesDto> series)
        {
            if (resource.Kind == AnalysisKind.RatingCurve)
            {
                var model = resource.RatingCurve!.RatingCurve;
                series.Add(CopySeries("stage", resource.StageTimeSeriesId, model.StageData));
                series.Add(CopySeries("discharge", resource.DischargeTimeSeriesId, model.DischargeData));
            }
            if (resource.Kind == AnalysisKind.TimeSeries)
            {
                var model = resource.TimeSeriesModel switch
                {
                    TimeSeriesModelType.Ar => resource.Ar!.AutoRegressive.TimeSeries,
                    TimeSeriesModelType.Ma => resource.Ma!.MovingAverage.TimeSeries,
                    TimeSeriesModelType.Arima => resource.Arima!.ARIMA.TimeSeries,
                    TimeSeriesModelType.Arimax => resource.Arimax!.ARIMAX.TimeSeries,
                    _ => throw new InvalidOperationException("Missing time series model discriminator.")
                };
                series.Add(CopySeries("observed", resource.TimeSeriesId, model));
                if (resource.TimeSeriesModel == TimeSeriesModelType.Arimax)
                {
                    var covariates = resource.Arimax!.ARIMAX.Covariates;
                    for (int index = 0; index < covariates.Count; index++)
                        series.Add(CopySeries($"covariate{index + 1}", resource.CovariateTimeSeriesIds?.ElementAtOrDefault(index), covariates[index]));
                }
            }
        }

        private static PlotSourceSeriesDto CopySeries(string name, Guid? resourceId, TimeSeries source) => new()
        {
            Name = name,
            ResourceId = resourceId,
            TimeInterval = source.TimeInterval.ToString(),
            Points = source.Select(point => new PlotSourcePointDto { Date = point.Index, Value = point.Value }).ToList()
        };

        private static List<PlotParameterDiagnosticsDto> MapDiagnostics(BayesianAnalysis analysis)
        {
            var results = analysis.Results!;
            var diagnostics = new List<PlotParameterDiagnosticsDto>();
            if (results.ParameterResults == null) return diagnostics;
            for (int index = 0; index < results.ParameterResults.Length; index++)
            {
                var source = results.ParameterResults[index];
                var item = new PlotParameterDiagnosticsDto
                {
                    ParameterIndex = index,
                    KernelDensity = CopyPairs(source.KernelDensity),
                    Autocorrelation = CopyPairs(source.Autocorrelation)
                };
                if (source.Histogram != null)
                {
                    for (int bin = 0; bin < source.Histogram.NumberOfBins; bin++)
                    {
                        var value = source.Histogram[bin];
                        item.Histogram.Add(new PlotHistogramBinDto
                        {
                            LowerBound = value.LowerBound,
                            UpperBound = value.UpperBound,
                            Frequency = value.Frequency
                        });
                    }
                }
                if (analysis.Model is { } model)
                {
                    var modelIndex = index;
                    if (model is MixtureModel mixture && mixture.Mixture is { } distribution &&
                        distribution.Distributions.Length > 1 &&
                        results.ParameterResults.Length == model.Parameters.Count - 1)
                    {
                        var derivedWeightIndex = distribution.Distributions.Length - 1;
                        if (index >= derivedWeightIndex) modelIndex++;
                        if (modelIndex < derivedWeightIndex) item.PriorName = "Configured Prior Factor";
                    }
                    if (modelIndex < model.Parameters.Count)
                    {
                        item.DisplayName = model.Parameters[modelIndex].DisplayName;
                        item.PriorDensity = CopyPairs(model.Parameters[modelIndex].PriorDistribution.CreatePDFGraph());
                    }
                }
                diagnostics.Add(item);
            }
            return diagnostics;
        }

        private static List<PlotPairDto> CopyPairs(double[,]? values)
        {
            var result = new List<PlotPairDto>();
            if (values == null || values.GetLength(1) < 2) return result;
            for (int row = 0; row < values.GetLength(0); row++)
                result.Add(new PlotPairDto { X = values[row, 0], Y = values[row, 1] });
            return result;
        }

        private static PlotSourceSampleDto MapSample(ParameterSet sample) => new()
        {
            Values = sample.Values?.ToList() ?? new List<double>(),
            Fitness = sample.Fitness,
            Weight = sample.Weight
        };

        private static PlotSourceInfluenceDto? MapInfluence(AnalysisResource resource, BayesianAnalysis bayesian)
        {
            LeverageDiagnostics leverage;
            var gmm = resource.Kind == AnalysisKind.Bulletin17C ? resource.Bulletin17C?.GMM : null;
            if (gmm?.IsEstimated == true)
            {
                leverage = gmm.GetLeverageDiagnostics();
            }
            else if (resource.Kind != AnalysisKind.Bulletin17C && bayesian.Model != null && bayesian.Results != null)
            {
                // The app computes the same Hessian diagnostic on demand. Clone the model
                // so numerical likelihood evaluation cannot alter the live source object.
                leverage = new LeverageDiagnostics(bayesian.Model.Clone(),
                    (double[])bayesian.Results.MAP.Values.Clone());
            }
            else return null;

            var response = new PlotSourceInfluenceDto
            {
                Method = gmm?.IsEstimated == true ? "gmm" : "bayesian",
                Observations = leverage.Observations.Select(item => new PlotSourceInfluenceItemDto
                {
                    Label = FormatInfluenceLabel(item.DataType.ToString(), item.Name, item.Index, item.Value),
                    IsObservation = true,
                    Leverage = item.Leverage,
                    PercentOfTotal = item.PercentOfTotal,
                    FitInfluence = item.FitInfluence,
                    VarianceInfluence = item.VarianceInfluence
                }).ToList(),
                PriorComponents = leverage.PriorComponents.Select(item => new PlotSourceInfluenceItemDto
                {
                    Label = item.Name,
                    IsObservation = false,
                    Leverage = item.Leverage,
                    PercentOfTotal = item.PercentOfTotal,
                    FitInfluence = item.FitInfluence,
                    VarianceInfluence = item.VarianceInfluence
                }).ToList()
            };
            if (resource.Kind != AnalysisKind.Bulletin17C && bayesian.Model != null &&
                bayesian.Results is { Output.Count: > 0 })
            {
                // The desktop's PSIS-LOO diagnostic can populate transient caches. Run it
                // against detached configuration and a cloned model, never the live analysis.
                var detached = new BayesianAnalysis(bayesian.Model.Clone(), bayesian.ToXElement(), bayesian.Results);
                var loo = detached.ComputeInfluenceDiagnostics();
                response.LeaveOneOut = loo.Observations.Select(item => new PlotSourceLooItemDto
                {
                    Label = FormatInfluenceLabel(item.DataType.ToString(), item.Name, item.Index, item.Value),
                    ElpdLoo = item.ElpdLoo,
                    Category = item.Category.ToString()
                }).ToList();
            }
            return response;
        }

        private static string FormatInfluenceLabel(string dataType, string? name, int index, double value)
        {
            var prefix = dataType is "LeftCensored" or "RightCensored" ? "Threshold" : dataType;
            return $"{prefix} - {(string.IsNullOrEmpty(name) ? index.ToString(CultureInfo.InvariantCulture) : name)} - {value.ToString("G4", CultureInfo.InvariantCulture)}";
        }
    }
}
