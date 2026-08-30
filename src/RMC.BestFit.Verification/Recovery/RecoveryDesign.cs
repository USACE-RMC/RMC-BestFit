namespace RMC.BestFit.Verification.Recovery;

/// <summary>
/// Declares the observational unit for a generated-parent recovery experiment.
/// </summary>
/// <remarks>
/// Recovery designs always contain exactly 1,000 observational units. Coverage replicates,
/// bootstrap realizations, numerical quadrature nodes, and calibration draws are deliberately
/// not represented here because they are not generated-parent recovery sample sizes.
/// </remarks>
public enum RecoverySampleUnit
{
    /// <summary>A single observed scalar value.</summary>
    ScalarObservation,

    /// <summary>A paired response and predictor observation.</summary>
    PairedObservation,

    /// <summary>A complete row or year vector observed as one unit.</summary>
    RowOrYearVector,

    /// <summary>A post-burn-in time-series observation.</summary>
    PostBurnInTimeSeriesObservation,

    /// <summary>A response/covariate row observed as one unit.</summary>
    ResponseCovariateRow
}

/// <summary>
/// Provides predeclared, test-only recovery-design metadata.
/// </summary>
/// <remarks>
/// The shared policy fixes every genuine generated-parent recovery design at <see cref="SampleSize"/>
/// observational units. Consumers must state the appropriate unit type instead of reusing this
/// type for a bootstrap, coverage, quadrature, or simulation-calibration count.
/// </remarks>
public readonly record struct RecoveryDesign(RecoverySampleUnit Unit, string Description)
{
    /// <summary>Gets the approved generated-parent recovery sample size.</summary>
    public const int SampleSize = 1000;

    /// <summary>Creates the scalar-observation recovery design.</summary>
    /// <param name="description">Scientific description of one scalar observation.</param>
    /// <returns>The scalar-observation design.</returns>
    /// <remarks>
    /// Declares a generated-parent design of exactly <see cref="SampleSize"/> scalar observations;
    /// callers must separately predeclare parents, fitted or response coordinates, uncertainty
    /// source, interval width, seed, and secondary-five-percent applicability.
    /// </remarks>
    public static RecoveryDesign ScalarObservations(string description) => new(RecoverySampleUnit.ScalarObservation, description);

    /// <summary>Creates the paired-observation recovery design.</summary>
    /// <param name="description">Scientific description of one response/predictor pair.</param>
    /// <returns>The paired-observation design.</returns>
    /// <remarks>
    /// Declares a generated-parent design of exactly <see cref="SampleSize"/> paired observations.
    /// It records sample-unit semantics only and cannot choose recovery parents, uncertainty bands,
    /// response ordinates, or secondary acceptance criteria.
    /// </remarks>
    public static RecoveryDesign PairedObservations(string description) => new(RecoverySampleUnit.PairedObservation, description);

    /// <summary>Creates the full row/year-vector recovery design.</summary>
    /// <param name="description">Scientific description of one complete row or year vector.</param>
    /// <returns>The row/year-vector design.</returns>
    /// <remarks>
    /// Declares exactly <see cref="SampleSize"/> complete row or year-vector observational units.
    /// The owning cell must document its deterministic seed, parent and response coordinates, and
    /// the source and width of any 95 percent uncertainty band.
    /// </remarks>
    public static RecoveryDesign RowOrYearVectors(string description) => new(RecoverySampleUnit.RowOrYearVector, description);

    /// <summary>Creates the post-burn-in time-series recovery design.</summary>
    /// <param name="description">Scientific description of one retained time-series observation.</param>
    /// <returns>The time-series design.</returns>
    /// <remarks>
    /// Declares exactly <see cref="SampleSize"/> retained post-burn-in observations, not a sampler
    /// iteration or calibration count. The owning recovery cell documents its parent, fitted or
    /// response coordinate, uncertainty source, band width, seed, and secondary rule.
    /// </remarks>
    public static RecoveryDesign PostBurnInTimeSeriesObservations(string description) => new(RecoverySampleUnit.PostBurnInTimeSeriesObservation, description);

    /// <summary>Creates the response/covariate-row recovery design.</summary>
    /// <param name="description">Scientific description of one response/covariate row.</param>
    /// <returns>The response/covariate-row design.</returns>
    /// <remarks>
    /// Declares exactly <see cref="SampleSize"/> response/covariate rows. This metadata does not
    /// alter estimator settings or determine a response grid; callers retain responsibility for
    /// parents, fitted coordinates, uncertainty source, 95 percent band width, seed, and secondary rule.
    /// </remarks>
    public static RecoveryDesign ResponseCovariateRows(string description) => new(RecoverySampleUnit.ResponseCovariateRow, description);
}
