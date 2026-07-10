namespace RMC_BestFit
{
    /// <summary>
    /// Represents a Data Storage System (DSS) pathname item, which contains the six standard parts
    /// (A through F) used to uniquely identify and organize data records in HEC-DSS databases.
    /// DSS pathnames follow the format: /A/B/C/D/E/F/
    /// </summary>
    public class DSSPathnameItem
    {
        /// <summary>
        /// Part A: Typically represents the project or study area name.
        /// </summary>
        public string PartA;

        /// <summary>
        /// Part B: Typically represents the location or basin identifier.
        /// </summary>
        public string PartB;

        /// <summary>
        /// Part C: Typically represents the parameter or data type (e.g., FLOW, STAGE, PRECIP).
        /// </summary>
        public string PartC;

        /// <summary>
        /// Part D: Typically represents the time interval or date range.
        /// </summary>
        public string PartD;

        /// <summary>
        /// Part E: Typically represents the time interval for regular time series data.
        /// </summary>
        public string PartE;

        /// <summary>
        /// Part F: Typically represents the version or scenario identifier.
        /// </summary>
        public string PartF;

        /// <summary>
        /// Initializes a new instance of the <see cref="DSSPathnameItem"/> class with the specified pathname parts.
        /// </summary>
        /// <param name="partA">Part A of the pathname (project/study area).</param>
        /// <param name="partB">Part B of the pathname (location/basin).</param>
        /// <param name="partC">Part C of the pathname (parameter/data type).</param>
        /// <param name="partD">Part D of the pathname (time interval/date range).</param>
        /// <param name="partE">Part E of the pathname (time interval for time series).</param>
        /// <param name="partF">Part F of the pathname (version/scenario).</param>
        public DSSPathnameItem(string partA, string partB, string partC, string partD, string partE, string partF)
        {
            PartA = partA;
            PartB = partB;
            PartC = partC;
            PartD = partD;
            PartE = partE;
            PartF = partF;
        }
    }
}
