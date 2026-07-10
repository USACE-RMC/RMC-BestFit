namespace RMC_BestFit
{
    /// <summary>
    /// Represents a single row in the coincident frequency analysis tabular output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Different shape from <see cref="FrequencyCurvePoint"/>: in coincident frequency the
    /// independent variable is the response Z (the first column of the table) and the
    /// dependent variables are the AEPs at that Z level. This mirrors the spreadsheet
    /// convention used in the Waimea CFA workbook.
    /// </para>
    /// </remarks>
    public class CoincidentFrequencyTablePoint
    {
        /// <summary>
        /// Gets or sets the response (Z) value at which AEPs are evaluated.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Gets or sets the lower-bound credible-interval AEP at this Z value.
        /// </summary>
        public double Lower { get; set; }

        /// <summary>
        /// Gets or sets the upper-bound credible-interval AEP at this Z value.
        /// </summary>
        public double Upper { get; set; }

        /// <summary>
        /// Gets or sets the posterior-predictive (mean curve) AEP at this Z value.
        /// </summary>
        public double Predictive { get; set; }

        /// <summary>
        /// Gets or sets the point-estimate (mode) AEP at this Z value.
        /// </summary>
        public double Mode { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyTablePoint"/> class.
        /// </summary>
        /// <param name="z">The Z (response) value.</param>
        /// <param name="lower">The lower-bound CI AEP.</param>
        /// <param name="upper">The upper-bound CI AEP.</param>
        /// <param name="predictive">The posterior-predictive AEP.</param>
        /// <param name="mode">The point-estimate (mode) AEP.</param>
        public CoincidentFrequencyTablePoint(double z, double lower, double upper, double predictive, double mode)
        {
            Z = z;
            Lower = lower;
            Upper = upper;
            Predictive = predictive;
            Mode = mode;
        }
    }
}
