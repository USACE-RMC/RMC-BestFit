using System.Text.Json;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>A detached, versioned source snapshot for plotting a completed analysis.</summary>
    public sealed class PlotSourceResponse : ResponseBase
    {
        /// <summary>The plot source schema version.</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>The analysis resource id.</summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>The analysis family.</summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>The state of the run represented by this snapshot.</summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>Completion time of the represented run.</summary>
        [JsonPropertyName("lastRunUtc")]
        public DateTime? LastRunUtc { get; set; }

        /// <summary>The time-series model family, when applicable.</summary>
        [JsonPropertyName("modelType")]
        public string? ModelType { get; set; }

        /// <summary>The originating input-data resource id, when applicable.</summary>
        [JsonPropertyName("inputDataId")]
        public Guid? InputDataId { get; set; }

        /// <summary>The originating time-series resource id, when applicable.</summary>
        [JsonPropertyName("timeSeriesId")]
        public Guid? TimeSeriesId { get; set; }

        /// <summary>Originating stage time-series resource id, when applicable.</summary>
        [JsonPropertyName("stageTimeSeriesId")]
        public Guid? StageTimeSeriesId { get; set; }

        /// <summary>Originating discharge time-series resource id, when applicable.</summary>
        [JsonPropertyName("dischargeTimeSeriesId")]
        public Guid? DischargeTimeSeriesId { get; set; }

        /// <summary>Originating covariate resource ids in model order.</summary>
        [JsonPropertyName("covariateTimeSeriesIds")]
        public List<Guid>? CovariateTimeSeriesIds { get; set; }

        /// <summary>Live bivariate parent id for a coincident analysis.</summary>
        [JsonPropertyName("bivariateAnalysisId")]
        public Guid? BivariateAnalysisId { get; set; }

        /// <summary>Marginal X analysis id for bivariate analyses.</summary>
        [JsonPropertyName("marginalXAnalysisId")]
        public Guid? MarginalXAnalysisId { get; set; }

        /// <summary>Marginal Y analysis id for bivariate analyses.</summary>
        [JsonPropertyName("marginalYAnalysisId")]
        public Guid? MarginalYAnalysisId { get; set; }

        /// <summary>Component analysis ids in configured order.</summary>
        [JsonPropertyName("componentAnalysisIds")]
        public List<Guid>? ComponentAnalysisIds { get; set; }

        /// <summary>Portable analysis configuration XML from the existing serializer.</summary>
        [JsonPropertyName("analysisXml")]
        public string? AnalysisXml { get; set; }

        /// <summary>Portable model configuration XML from the existing serializer.</summary>
        [JsonPropertyName("modelXml")]
        public string? ModelXml { get; set; }

        /// <summary>Independent XML copy of a frequency-kind input data frame.</summary>
        [JsonPropertyName("dataFrameXml")]
        public string? DataFrameXml { get; set; }

        /// <summary>Typed source observations with stored plotting positions and bounds.</summary>
        [JsonPropertyName("observations")]
        public List<PlotSourceObservationDto> Observations { get; set; } = new();

        /// <summary>Point-process observations after the desktop's Langbein AMS conversion.</summary>
        [JsonPropertyName("amsObservations")]
        public List<PlotSourceObservationDto> AmsObservations { get; set; } = new();

        /// <summary>The bivariate X marginal's independent observation frame.</summary>
        [JsonPropertyName("marginalXDataFrameXml")]
        public string? MarginalXDataFrameXml { get; set; }

        /// <summary>The bivariate Y marginal's independent observation frame.</summary>
        [JsonPropertyName("marginalYDataFrameXml")]
        public string? MarginalYDataFrameXml { get; set; }

        /// <summary>The bivariate X marginal model configuration.</summary>
        [JsonPropertyName("marginalXModelXml")]
        public string? MarginalXModelXml { get; set; }

        /// <summary>The bivariate Y marginal model configuration.</summary>
        [JsonPropertyName("marginalYModelXml")]
        public string? MarginalYModelXml { get; set; }

        /// <summary>Dated observed and covariate series copied from the analysis-owned models.</summary>
        [JsonPropertyName("series")]
        public List<PlotSourceSeriesDto> Series { get; set; } = new();

        /// <summary>Detached result DTO, or JSON null when no completed result is available.</summary>
        [JsonPropertyName("results")]
        public JsonElement Results { get; set; } = JsonSerializer.SerializeToElement<object?>(null);

        /// <summary>Saved nonstationary chronology bounds and mean on the model's index grid.</summary>
        [JsonPropertyName("chronology")]
        public PlotSourceChronologyDto? Chronology { get; set; }

        /// <summary>Saved composite component or seasonal point-estimate curves.</summary>
        [JsonPropertyName("componentCurves")]
        public List<PlotSourceCurveDto> ComponentCurves { get; set; } = new();

        /// <summary>Desktop-prepared distribution fitting curves for visible successful fits.</summary>
        [JsonPropertyName("fittingCurves")]
        public List<PlotSourceFittingCurveDto> FittingCurves { get; set; } = new();

        /// <summary>Desktop fitting histogram bins over all observed magnitudes.</summary>
        [JsonPropertyName("fittingHistogram")]
        public List<PlotHistogramBinDto> FittingHistogram { get; set; } = new();

        /// <summary>Detached app bivariate grid and simulated point coordinates.</summary>
        [JsonPropertyName("bivariatePlot")]
        public PlotSourceBivariateDto? BivariatePlot { get; set; }

        /// <summary>Detached rating or time-series residual plot data.</summary>
        [JsonPropertyName("residualPlot")]
        public PlotSourceResidualDto? ResidualPlot { get; set; }

        /// <summary>Exact dated grid of the completed time-series result.</summary>
        [JsonPropertyName("resultDates")]
        public List<DateTime> ResultDates { get; set; } = new();

        /// <summary>Precomputed parameter histogram, density, and autocorrelation points.</summary>
        [JsonPropertyName("parameterDiagnostics")]
        public List<PlotParameterDiagnosticsDto> ParameterDiagnostics { get; set; } = new();

        /// <summary>Saved mean log likelihood by iteration, when available.</summary>
        [JsonPropertyName("meanLogLikelihood")]
        public List<double>? MeanLogLikelihood { get; set; }

        /// <summary>Detached influence values for the configured diagnostic views.</summary>
        [JsonPropertyName("influenceDiagnostics")]
        public PlotSourceInfluenceDto? InfluenceDiagnostics { get; set; }

        /// <summary>Origin of parameter draws: MCMC or Bulletin 17C sampled uncertainty.</summary>
        [JsonPropertyName("sampleOrigin")]
        public string? SampleOrigin { get; set; }

        /// <summary>Bulletin 17C uncertainty method, when applicable.</summary>
        [JsonPropertyName("uncertaintyMethod")]
        public string? UncertaintyMethod { get; set; }

        /// <summary>Full saved draws and chains when requested.</summary>
        [JsonPropertyName("samples")]
        public PlotSourceSamplesDto? Samples { get; set; }
    }

    /// <summary>One independent copy of a time series or aligned rating observation axis.</summary>
    public sealed class PlotSourceSeriesDto
    {
        /// <summary>Role of the series in the analysis.</summary>
        public string? Name { get; set; }
        /// <summary>Originating resource id, when available.</summary>
        public Guid? ResourceId { get; set; }
        /// <summary>Original time interval name.</summary>
        public string? TimeInterval { get; set; }
        /// <summary>Copied dated ordinates in model order.</summary>
        public List<PlotSourcePointDto> Points { get; set; } = new();
    }

    /// <summary>One dated observation.</summary>
    public sealed class PlotSourcePointDto
    {
        /// <summary>The ordinate date.</summary>
        public DateTime Date { get; set; }
        /// <summary>The ordinate value.</summary>
        public double Value { get; set; }
    }

    /// <summary>Precomputed model diagnostics for one sampled parameter.</summary>
    public sealed class PlotParameterDiagnosticsDto
    {
        /// <summary>Zero-based sampled parameter coordinate.</summary>
        public int ParameterIndex { get; set; }
        /// <summary>Desktop display name of this sampled model parameter.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Histogram bins with exact source bounds and frequencies.</summary>
        public List<PlotHistogramBinDto> Histogram { get; set; } = new();
        /// <summary>Stored density coordinates.</summary>
        public List<PlotPairDto> KernelDensity { get; set; } = new();
        /// <summary>Stored autocorrelation lag and value coordinates.</summary>
        public List<PlotPairDto> Autocorrelation { get; set; } = new();
        /// <summary>Configured prior density computed from the parameter's existing distribution.</summary>
        public List<PlotPairDto> PriorDensity { get; set; } = new();
        /// <summary>Desktop label for the selected configured prior.</summary>
        public string PriorName { get; set; } = "Prior Density";
    }

    /// <summary>Two columns from a stored diagnostic array.</summary>
    public sealed class PlotPairDto
    {
        /// <summary>Horizontal coordinate.</summary>
        public double X { get; set; }
        /// <summary>Vertical coordinate.</summary>
        public double Y { get; set; }
    }

    /// <summary>One stored histogram bin with its exact width and raw frequency.</summary>
    public sealed class PlotHistogramBinDto
    {
        /// <summary>Lower bin edge.</summary>
        public double LowerBound { get; set; }
        /// <summary>Upper bin edge.</summary>
        public double UpperBound { get; set; }
        /// <summary>Stored bin frequency.</summary>
        public double Frequency { get; set; }
    }

    /// <summary>A detached chronology array aligned to explicit source indices.</summary>
    public sealed class PlotSourceChronologyDto
    {
        /// <summary>The original model index for each chronology ordinate.</summary>
        public List<double> Indices { get; set; } = new();
        /// <summary>Stored chronology mean values.</summary>
        public List<double> MeanCurve { get; set; } = new();
        /// <summary>Stored point-estimate values, retained for complete source state.</summary>
        public List<double> ModeCurve { get; set; } = new();
        /// <summary>Lower interval bounds.</summary>
        public List<double> CiLower { get; set; } = new();
        /// <summary>Upper interval bounds.</summary>
        public List<double> CiUpper { get; set; } = new();
        /// <summary>Stored credible interval width.</summary>
        public double CredibleIntervalWidth { get; set; }
    }

    /// <summary>One original observation or historical threshold.</summary>
    public sealed class PlotSourceObservationDto
    {
        /// <summary>Exact, uncertain, interval, or threshold.</summary>
        public string Kind { get; set; } = string.Empty;
        /// <summary>Stored observation index.</summary>
        public double? Index { get; set; }
        /// <summary>Stored observation magnitude.</summary>
        public double Value { get; set; }
        /// <summary>Stored annual exceedance plotting position.</summary>
        public double? Aep { get; set; }
        /// <summary>Original low-outlier flag on exact observations.</summary>
        public bool LowOutlier { get; set; }
        /// <summary>Lower measurement bound when present.</summary>
        public double? Lower { get; set; }
        /// <summary>Upper measurement bound when present.</summary>
        public double? Upper { get; set; }
        /// <summary>Threshold window starting index when present.</summary>
        public double? Start { get; set; }
        /// <summary>Threshold window ending index when present.</summary>
        public double? End { get; set; }
    }

    /// <summary>Curves for a visible successful fitted candidate, prepared by existing Numerics methods.</summary>
    public sealed class PlotSourceFittingCurveDto
    {
        /// <summary>Candidate display name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Frequency quantiles against exceedance probabilities.</summary>
        public List<PlotPairDto> Frequency { get; set; } = new();
        /// <summary>Probability density coordinates.</summary>
        public List<PlotPairDto> Pdf { get; set; } = new();
        /// <summary>Cumulative probability coordinates.</summary>
        public List<PlotPairDto> Cdf { get; set; } = new();
        /// <summary>Model CDF versus sorted empirical non-exceedance plotting positions.</summary>
        public List<PlotPairDto> Pp { get; set; } = new();
        /// <summary>Sorted observed magnitudes versus model quantiles.</summary>
        public List<PlotPairDto> Qq { get; set; } = new();
    }

    /// <summary>Prepared geometry for the desktop bivariate plot variants.</summary>
    public sealed class PlotSourceBivariateDto
    {
        /// <summary>Matched exact pairs in model units.</summary>
        public List<PlotPairDto> Observed { get; set; } = new();
        /// <summary>Observed pairs transformed through fitted marginal CDFs.</summary>
        public List<PlotPairDto> ObservedCdf { get; set; } = new();
        /// <summary>Seeded model simulation in model units.</summary>
        public List<PlotPairDto> Simulated { get; set; } = new();
        /// <summary>Simulated pairs transformed through fitted marginal CDFs.</summary>
        public List<PlotPairDto> SimulatedCdf { get; set; } = new();
        /// <summary>Magnitude coordinates of the X contour grid.</summary>
        public List<double> XGrid { get; set; } = new();
        /// <summary>Magnitude coordinates of the Y contour grid.</summary>
        public List<double> YGrid { get; set; } = new();
        /// <summary>Marginal CDF coordinates of the X grid.</summary>
        public List<double> XCdf { get; set; } = new();
        /// <summary>Marginal CDF coordinates of the Y grid.</summary>
        public List<double> YCdf { get; set; } = new();
        /// <summary>Log density by Y row and X column.</summary>
        public List<List<double>> LogPdf { get; set; } = new();
        /// <summary>Joint exceedance by Y row and X column.</summary>
        public List<List<double>> JointExceedance { get; set; } = new();
        /// <summary>Desktop density contour levels.</summary>
        public List<double> DensityLevels { get; set; } = new();
    }

    /// <summary>Stored parameter based residual displays prepared by core Numerics methods.</summary>
    public sealed class PlotSourceResidualDto
    {
        /// <summary>Date-aligned rating observations as discharge and stage.</summary>
        public List<PlotPairDto> AlignedObservations { get; set; } = new();
        /// <summary>Point-estimate fitted values.</summary>
        public List<double> Fitted { get; set; } = new();
        /// <summary>Model residuals in original order.</summary>
        public List<double> Residuals { get; set; } = new();
        /// <summary>Time-series training dates aligned to residuals.</summary>
        public List<DateTime> Dates { get; set; } = new();
        /// <summary>Exact desktop residual histogram bins.</summary>
        public List<PlotHistogramBinDto> Histogram { get; set; } = new();
        /// <summary>Normal reference density coordinates.</summary>
        public List<PlotPairDto> NormalPdf { get; set; } = new();
        /// <summary>Normal theoretical quantile and ordered residual pairs.</summary>
        public List<PlotPairDto> Qq { get; set; } = new();
        /// <summary>Autocorrelation lag and value pairs.</summary>
        public List<PlotPairDto> Acf { get; set; } = new();
        /// <summary>Partial autocorrelation lag and value pairs.</summary>
        public List<PlotPairDto> Pacf { get; set; } = new();
        /// <summary>Point-estimate residual error scale.</summary>
        public double ErrorScale { get; set; }
        /// <summary>Exact common correlation interval from Numerics.</summary>
        public List<double> CorrelationConfidenceInterval { get; set; } = new();
    }

    /// <summary>App influence diagnostic data separated by observation and prior component.</summary>
    public sealed class PlotSourceInfluenceDto
    {
        /// <summary>Bayesian or GMM provenance.</summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>Read-only computed observation measures.</summary>
        public List<PlotSourceInfluenceItemDto> Observations { get; set; } = new();
        /// <summary>Read-only computed prior or penalty measures.</summary>
        public List<PlotSourceInfluenceItemDto> PriorComponents { get; set; } = new();
        /// <summary>Saved-run LOO measures when computed without modifying source state.</summary>
        public List<PlotSourceLooItemDto> LeaveOneOut { get; set; } = new();
    }

    /// <summary>One Hessian leverage component.</summary>
    public sealed class PlotSourceInfluenceItemDto
    {
        /// <summary>Display label including data type, index, and magnitude.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>True for a data observation; false for prior or penalty.</summary>
        public bool IsObservation { get; set; }
        /// <summary>Combined leverage ranking measure.</summary>
        public double Leverage { get; set; }
        /// <summary>Combined leverage as percent of total.</summary>
        public double PercentOfTotal { get; set; }
        /// <summary>Cook fit influence.</summary>
        public double FitInfluence { get; set; }
        /// <summary>Variance influence.</summary>
        public double VarianceInfluence { get; set; }
    }

    /// <summary>One saved-run leave-one-out observation measure.</summary>
    public sealed class PlotSourceLooItemDto
    {
        /// <summary>Display label from the source observation.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>Pointwise expected log predictive density.</summary>
        public double ElpdLoo { get; set; }
        /// <summary>Diagnostic Pareto category.</summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>One completed component curve at its own configured probabilities.</summary>
    public sealed class PlotSourceCurveDto
    {
        /// <summary>The series label or component name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Configured annual exceedance probabilities.</summary>
        public List<double> Probabilities { get; set; } = new();
        /// <summary>Aligned point-estimate quantiles.</summary>
        public List<double> Values { get; set; } = new();
    }

    /// <summary>Full stored draws, present only on explicit opt-in.</summary>
    public sealed class PlotSourceSamplesDto
    {
        /// <summary>Postprocessed output parameter sets.</summary>
        public List<PlotSourceSampleDto> Output { get; set; } = new();
        /// <summary>Saved per-chain parameter sets, including warmup when present.</summary>
        public List<List<PlotSourceSampleDto>>? MarkovChains { get; set; }
    }

    /// <summary>A detached parameter vector with its saved fitness and weight.</summary>
    public sealed class PlotSourceSampleDto
    {
        /// <summary>Parameter vector in stored coordinate order.</summary>
        public List<double> Values { get; set; } = new();
        /// <summary>Stored objective function value.</summary>
        public double Fitness { get; set; }
        /// <summary>Stored optional sample weight.</summary>
        public double Weight { get; set; }
    }
}
