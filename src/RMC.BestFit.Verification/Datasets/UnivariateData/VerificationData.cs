using Numerics;
using Numerics.Data.Statistics;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// Provides datasets and test configurations for verification of Maximum Likelihood Estimation (MLE) and Maximum A Posteriori (MAP) estimation methods.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Background:</b>
    ///     The RMC-BestFit verification report (Smith, 2020) validates the software's distribution fitting capabilities
    ///     against well-established textbooks and published datasets. This class provides the datasets used for verifying
    ///     Maximum Likelihood Estimation (MLE) and Maximum A Posteriori (MAP) estimation methods for univariate probability distributions.
    /// </para>
    /// <para>
    ///     <b>Primary References:</b>
    ///     The datasets are primarily sourced from:
    ///     <list type="bullet">
    ///         <item><description>Rao, A.R. &amp; Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.</description></item>
    ///         <item><description>Bobee, B. &amp; Ashkar, F. (1991). The Gamma Family and Derived Distributions Applied in Hydrology. Water Resources Publications.</description></item>
    ///     </list>
    ///     These sources provide both the data and the expected parameter estimates for verification purposes.
    /// </para>
    /// <para>
    ///     <b>Dataset Coverage:</b>
    ///     This class includes annual maximum flow data from multiple USGS stream gauges across the United States and Canada,
    ///     providing diverse hydrologic conditions for testing distribution fitting algorithms. The datasets range from
    ///     48 to 287 observations, allowing verification of estimation methods across different sample sizes.
    /// </para>
    /// <para>
    ///     <b>Additional References:</b>
    ///     <list type="bullet">
    ///         <item><description>Smith, H. (2020). Verification of the Bayesian Estimation and Fitting Software (RMC-BestFit). USACE Risk Management Center Technical Report RMC-TR-2020-02.</description></item>
    ///         <item><description>R Core Team (2024). R: A Language and Environment for Statistical Computing. R Foundation for Statistical Computing, Vienna, Austria.</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    public static class VerificationData
    {
        #region Static Array Properties (Backward Compatibility)

        /// <summary>
        /// Gets the Tippecanoe River near Delphi, IN annual maximum flows (cfs) from 1941 to 1988 as a raw array.
        /// </summary>
        /// <remarks>
        /// This property provides backward compatibility for tests that directly access the data array.
        /// For new code, prefer using the <see cref="GetTippecanoeRiverData"/> method instead.
        /// </remarks>
        public static double[] TippecanoeRiverData => new double[] { 6290d, 2700d, 13100d, 16900d, 14600d, 9600d, 7740d, 8490d, 8130d, 12000d, 17200d, 15000d, 12400d, 6960d, 6500d, 5840d, 10400d, 18800d, 21400d, 22600d, 14200d, 11000d, 12800d, 15700d, 4740d, 6950d, 11800d, 12100d, 20600d, 14600d, 14600d, 8900d, 10600d, 14200d, 14100d, 14100d, 12500d, 7530d, 13400d, 17600d, 13400d, 19200d, 16900d, 15500d, 14500d, 21900d, 10400d, 7460d };

        /// <summary>
        /// Gets the White River near Nora, IN annual maximum flows (cfs) from 1930 to 1991 as a raw array.
        /// </summary>
        /// <remarks>
        /// This property provides backward compatibility for tests that directly access the data array.
        /// For new code, prefer using the <see cref="GetWhiteRiverData"/> method instead.
        /// </remarks>
        public static double[] WhiteRiverData => new double[] { 23200d, 2950d, 10300d, 23200d, 4540d, 9960d, 10800d, 26900d, 23300d, 20400d, 8480d, 3150d, 9380d, 32400d, 20800d, 11100d, 7270d, 9600d, 14600d, 14300d, 22500d, 14700d, 12700d, 9740d, 3050d, 8830d, 12000d, 30400d, 27000d, 15200d, 8040d, 11700d, 20300d, 22700d, 30400d, 9180d, 4870d, 14700d, 12800d, 13700d, 7960d, 9830d, 12500d, 10700d, 13200d, 14700d, 14300d, 4050d, 14600d, 14400d, 19200d, 7160d, 12100d, 8650d, 10600d, 24500d, 14400d, 6300d, 9560d, 15800d, 14300d, 28700d };

        /// <summary>
        /// Gets the White River at Mt. Carmel, IN flows exceeding the threshold of 50,000 cfs as a raw array.
        /// </summary>
        /// <remarks>
        /// This property provides backward compatibility for tests that directly access the data array.
        /// For new code, prefer using the <see cref="GetWhiteRiverAtMtCarmelData"/> method instead.
        /// </remarks>
        public static double[] WhiteRiverAtMtCarmel => new double[] { 126000d, 148000d, 66000d, 156000d, 136000d, 122000d, 183000d, 162000d, 85200d, 56800d, 56600d, 138000d, 81000d, 51800d, 90700d, 139000d, 160000d, 118000d, 50600d, 137000d, 151000d, 172000d, 52600d, 248000d, 152000d, 64500d, 61500d, 143000d, 108000d, 53000d, 134000d, 115000d, 84100d, 105000d, 85400d, 76900d, 99100d, 73700d, 122000d, 62500d, 54300d, 58000d, 144000d, 55800d, 127000d, 55800d, 107000d, 56400d, 128000d, 106000d, 110000d, 232000d, 60400d, 60400d, 50800d, 100000d, 55900d, 167000d, 53700d, 56700d, 126000d, 100000d, 59500d, 164000d, 81800d, 56400d, 124000d, 64600d, 77300d, 65900d, 72500d, 65100d, 80800d, 69800d, 53000d, 195000d, 128000d, 114000d, 110000d, 149000d, 74100d, 75900d, 99300d, 168000d, 70800d, 104000d, 125000d, 77300d, 97300d, 140000d, 54900d, 66000d, 199000d, 99800d, 105000d, 93700d, 277000d, 85700d, 77300d, 122000d, 106000d, 93300d, 79000d, 130000d, 126000d, 57800d, 64700d, 162000d, 71900d, 63500d, 81500d, 51000d, 84400d, 108000d, 185000d, 55800d, 94600d, 82800d, 146000d, 66500d, 57700d, 78700d, 85100d, 129000d, 75700d, 104000d, 139000d, 50600d, 53500d, 178000d, 110000d, 50800d, 76000d, 130000d, 67300d, 149000d, 78400d, 96600d, 83300d, 68400d, 84300d, 56400d, 112000d, 76400d, 116000d, 51400d, 59800d, 63900d, 81900d, 88200d, 62300d, 162000d, 67200d, 85500d, 51000d, 286000d, 73800d, 61300d, 60800d, 91300d, 134000d, 106000d, 70800d, 106000d, 122000d, 149000d, 53700d, 85300d, 144000d, 54800d, 116000d, 67500d, 56500d, 86700d, 91500d, 105000d, 134000d, 97300d, 84000d, 141000d, 52600d, 124000d, 196000d, 84200d, 54500d, 74500d, 104000d, 57200d, 61000d, 155000d, 96500d, 89100d, 77900d, 70500d, 73400d, 180000d, 83700d, 302000d, 133000d, 92100d, 105000d, 235000d, 213000d, 96100d, 77100d, 73900d, 55400d, 55200d, 87800d, 52600d, 106000d, 93000d, 147000d, 61800d, 101000d, 154000d, 52000d, 121000d, 86700d, 57300d, 97500d, 112000d, 88500d, 76200d, 140000d, 87400d, 154000d, 95100d, 131000d, 131000d, 54900d, 78800d, 101000d, 224000d, 54800d, 50900d, 63500d, 63500d, 152000d, 51000d, 285000d, 114000d, 197000d, 106000d, 132000d, 83700d, 67200d, 110000d, 202000d, 127000d, 90600d, 126000d, 73900d, 86500d, 181000d, 141000d, 79700d, 97800d, 57300d, 77200d, 133000d, 82900d, 55000d, 62000d, 51700d, 54500d, 51600d, 103000d, 134000d, 71700d, 57000d, 63900d, 60700d, 81900d, 171000d, 111000d, 50400d, 50500d, 69700d, 88900d, 76600d };

        /// <summary>
        /// Gets the East Fork White River at Seymour, IN annual maximum flows (cfs) from 1924 to 1991 as a raw array.
        /// </summary>
        /// <remarks>
        /// This property provides backward compatibility for tests that directly access the data array.
        /// For new code, prefer using the <see cref="GetEastForkWhiteRiverData"/> method instead.
        /// </remarks>
        public static double[] EastForkWhiteRiverData => new double[] { 24000d, 7920d, 21900d, 47100d, 30400d, 36100d, 67100d, 7030d, 28200d, 40100d, 10300d, 11100d, 17000d, 65600d, 32600d, 36200d, 46400d, 3650d, 16800d, 44800d, 37100d, 42900d, 15000d, 33000d, 28000d, 78500d, 54000d, 28600d, 44000d, 13300d, 6120d, 11100d, 42100d, 33400d, 30100d, 32100d, 28100d, 59400d, 23800d, 52000d, 54900d, 25600d, 10900d, 33700d, 60200d, 39200d, 26300d, 27900d, 27000d, 22700d, 17500d, 46400d, 19300d, 12700d, 36000d, 39900d, 25400d, 30200d, 47000d, 39800d, 23800d, 29600d, 33400d, 15400d, 28400d, 26700d, 46500d, 61200d };

        #endregion

        #region Dataset Properties

        /// <summary>
        /// Gets the Wabash River at Lafayette, IN annual maximum flows (cfs) from 1916 to 2000.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 85-year record from USGS gauge 03335500 represents one of the longer streamflow records in the Indiana region.
        /// The dataset exhibits moderate variability with flows ranging from approximately 13,100 to 99,000 cfs.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 1.8.1
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for the Log-Normal and Log-Pearson Type III distributions.
        /// The large sample size makes it well-suited for testing asymptotic properties of MLEs.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 85 annual maximum flow observations in cfs.</returns>
        public static ExactSeries GetWabashRiverData()
        {
            var data = new double[] { 41500d, 57000d, 44000d, 49000d, 31000d, 45900d, 19000d, 41100d, 37300d, 76000d, 33200d, 61200d, 76000d, 59800d, 44400d, 58400d, 53600d, 59800d, 63300d, 57700d, 64000d, 63500d, 38000d, 74600d, 13100d, 37600d, 67500d, 21700d, 37000d, 93500d, 58500d, 63300d, 74400d, 34200d, 14600d, 44200d, 13100d, 73300d, 46600d, 39400d, 41200d, 41300d, 62000d, 90000d, 50600d, 41900d, 35000d, 16500d, 35300d, 30000d, 52600d, 99000d, 89000d, 39500d, 55400d, 46000d, 63000d, 58300d, 36500d, 14600d, 64900d, 68500d, 69100d, 42600d, 31000d, 39400d, 40700d, 53400d, 36000d, 43900d, 23600d, 50500d, 49700d, 48100d, 44500d, 56400d, 60800d, 40400d, 80400d, 41600d, 14700d, 33300d, 40700d, 53300d, 77400d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the Tippecanoe River near Delphi, IN annual maximum flows (cfs) from 1941 to 1988.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 48-year record from USGS gauge 03331500 represents a moderate-sized watershed in northern Indiana.
        /// The flows range from approximately 2,700 to 22,600 cfs, with the dataset exhibiting slight positive skewness.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 5.1.1
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for the Normal and Weibull distributions.
        /// The moderate sample size and relatively symmetric distribution make it ideal for testing Normal distribution fitting.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 48 annual maximum flow observations in cfs.</returns>
        public static ExactSeries GetTippecanoeRiverData()
        {
            var data = new double[] { 6290d, 2700d, 13100d, 16900d, 14600d, 9600d, 7740d, 8490d, 8130d, 12000d, 17200d, 15000d, 12400d, 6960d, 6500d, 5840d, 10400d, 18800d, 21400d, 22600d, 14200d, 11000d, 12800d, 15700d, 4740d, 6950d, 11800d, 12100d, 20600d, 14600d, 14600d, 8900d, 10600d, 14200d, 14100d, 14100d, 12500d, 7530d, 13400d, 17600d, 13400d, 19200d, 16900d, 15500d, 14500d, 21900d, 10400d, 7460d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the White River near Nora, IN annual maximum flows (cfs) from 1930 to 1991.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 62-year record from USGS gauge 03351000 represents an urban-influenced watershed near Indianapolis.
        /// The flows exhibit high variability, ranging from approximately 3,000 to 32,400 cfs, with significant positive skewness.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 7.1.2
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for distributions that handle skewed data,
        /// including the Gamma and Log-Pearson Type III distributions. The high variability tests the robustness of the estimation procedures.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 62 annual maximum flow observations in cfs.</returns>
        public static ExactSeries GetWhiteRiverData()
        {
            var data = new double[] { 23200d, 2950d, 10300d, 23200d, 4540d, 9960d, 10800d, 26900d, 23300d, 20400d, 8480d, 3150d, 9380d, 32400d, 20800d, 11100d, 7270d, 9600d, 14600d, 14300d, 22500d, 14700d, 12700d, 9740d, 3050d, 8830d, 12000d, 30400d, 27000d, 15200d, 8040d, 11700d, 20300d, 22700d, 30400d, 9180d, 4870d, 14700d, 12800d, 13700d, 7960d, 9830d, 12500d, 10700d, 13200d, 14700d, 14300d, 4050d, 14600d, 14400d, 19200d, 7160d, 12100d, 8650d, 10600d, 24500d, 14400d, 6300d, 9560d, 15800d, 14300d, 28700d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the Sugar Creek at Crawfordsville, IN annual maximum flows (cfs) from 1939 to 1991.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 53-year record from USGS gauge 03339000 represents a smaller watershed in west-central Indiana.
        /// The flows range from approximately 900 to 26,300 cfs, exhibiting moderate to high positive skewness
        /// typical of Midwestern agricultural watersheds.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 7.2.1
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for the Gumbel distribution.
        /// The moderate skewness and sample size make it representative of typical flood frequency analysis applications.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 53 annual maximum flow observations in cfs.</returns>
        public static ExactSeries GetSugarCreekData()
        {
            var data = new double[] { 17600d, 3660d, 903d, 5050d, 24000d, 11400d, 9470d, 8970d, 7710d, 14800d, 13900d, 20800d, 9470d, 7860d, 7860d, 2730d, 6480d, 18200d, 26300d, 15100d, 14600d, 7300d, 8580d, 15100d, 15100d, 21800d, 6200d, 2130d, 11100d, 14300d, 11200d, 6670d, 5440d, 9370d, 6900d, 9680d, 6810d, 7730d, 5290d, 12200d, 9750d, 7390d, 13100d, 7190d, 8850d, 6290d, 18800d, 9740d, 2990d, 6950d, 9390d, 12400d, 21200d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the White River at Mt. Carmel, IN flows exceeding the threshold of 50,000 cfs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This dataset represents a Peaks-Over-Threshold (POT) series containing 287 exceedances over 50,000 cfs
        /// from USGS gauge 03374000. The flows range from approximately 50,000 to 302,000 cfs. This is a partial duration series
        /// rather than an annual maximum series, making it suitable for testing distributions appropriate for POT analysis.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 8.3.1
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This large dataset is used to verify MLE fitting for the Generalized Pareto distribution,
        /// which is the theoretical limiting distribution for exceedances over a high threshold. The large sample size
        /// provides strong verification of asymptotic MLE properties for the Generalized Pareto distribution.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 287 flow observations exceeding 50,000 cfs.</returns>
        public static ExactSeries GetWhiteRiverAtMtCarmelData()
        {
            var data = new double[] { 126000d, 148000d, 66000d, 156000d, 136000d, 122000d, 183000d, 162000d, 85200d, 56800d, 56600d, 138000d, 81000d, 51800d, 90700d, 139000d, 160000d, 118000d, 50600d, 137000d, 151000d, 172000d, 52600d, 248000d, 152000d, 64500d, 61500d, 143000d, 108000d, 53000d, 134000d, 115000d, 84100d, 105000d, 85400d, 76900d, 99100d, 73700d, 122000d, 62500d, 54300d, 58000d, 144000d, 55800d, 127000d, 55800d, 107000d, 56400d, 128000d, 106000d, 110000d, 232000d, 60400d, 60400d, 50800d, 100000d, 55900d, 167000d, 53700d, 56700d, 126000d, 100000d, 59500d, 164000d, 81800d, 56400d, 124000d, 64600d, 77300d, 65900d, 72500d, 65100d, 80800d, 69800d, 53000d, 195000d, 128000d, 114000d, 110000d, 149000d, 74100d, 75900d, 99300d, 168000d, 70800d, 104000d, 125000d, 77300d, 97300d, 140000d, 54900d, 66000d, 199000d, 99800d, 105000d, 93700d, 277000d, 85700d, 77300d, 122000d, 106000d, 93300d, 79000d, 130000d, 126000d, 57800d, 64700d, 162000d, 71900d, 63500d, 81500d, 51000d, 84400d, 108000d, 185000d, 55800d, 94600d, 82800d, 146000d, 66500d, 57700d, 78700d, 85100d, 129000d, 75700d, 104000d, 139000d, 50600d, 53500d, 178000d, 110000d, 50800d, 76000d, 130000d, 67300d, 149000d, 78400d, 96600d, 83300d, 68400d, 84300d, 56400d, 112000d, 76400d, 116000d, 51400d, 59800d, 63900d, 81900d, 88200d, 62300d, 162000d, 67200d, 85500d, 51000d, 286000d, 73800d, 61300d, 60800d, 91300d, 134000d, 106000d, 70800d, 106000d, 122000d, 149000d, 53700d, 85300d, 144000d, 54800d, 116000d, 67500d, 56500d, 86700d, 91500d, 105000d, 134000d, 97300d, 84000d, 141000d, 52600d, 124000d, 196000d, 84200d, 54500d, 74500d, 104000d, 57200d, 61000d, 155000d, 96500d, 89100d, 77900d, 70500d, 73400d, 180000d, 83700d, 302000d, 133000d, 92100d, 105000d, 235000d, 213000d, 96100d, 77100d, 73900d, 55400d, 55200d, 87800d, 52600d, 106000d, 93000d, 147000d, 61800d, 101000d, 154000d, 52000d, 121000d, 86700d, 57300d, 97500d, 112000d, 88500d, 76200d, 140000d, 87400d, 154000d, 95100d, 131000d, 131000d, 54900d, 78800d, 101000d, 224000d, 54800d, 50900d, 63500d, 63500d, 152000d, 51000d, 285000d, 114000d, 197000d, 106000d, 132000d, 83700d, 67200d, 110000d, 202000d, 127000d, 90600d, 126000d, 73900d, 86500d, 181000d, 141000d, 79700d, 97800d, 57300d, 77200d, 133000d, 82900d, 55000d, 62000d, 51700d, 54500d, 51600d, 103000d, 134000d, 71700d, 57000d, 63900d, 60700d, 81900d, 171000d, 111000d, 50400d, 50500d, 69700d, 88900d, 76600d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the East Fork White River at Seymour, IN annual maximum flows (cfs) from 1924 to 1991.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 68-year record from USGS gauge 03302000 represents a significant watershed in southern Indiana.
        /// The flows range from approximately 3,650 to 78,500 cfs, with moderate positive skewness characteristic
        /// of flood frequency distributions in the region.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 9.2.1
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for the Generalized Extreme Value (GEV) distribution.
        /// The long record length and diverse flow conditions make it well-suited for testing three-parameter distribution fitting.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 68 annual maximum flow observations in cfs.</returns>
        public static ExactSeries GetEastForkWhiteRiverData()
        {
            var data = new double[] { 24000d, 7920d, 21900d, 47100d, 30400d, 36100d, 67100d, 7030d, 28200d, 40100d, 10300d, 11100d, 17000d, 65600d, 32600d, 36200d, 46400d, 3650d, 16800d, 44800d, 37100d, 42900d, 15000d, 33000d, 28000d, 78500d, 54000d, 28600d, 44000d, 13300d, 6120d, 11100d, 42100d, 33400d, 30100d, 32100d, 28100d, 59400d, 23800d, 52000d, 54900d, 25600d, 10900d, 33700d, 60200d, 39200d, 26300d, 27900d, 27000d, 22700d, 17500d, 46400d, 19300d, 12700d, 36000d, 39900d, 25400d, 30200d, 47000d, 39800d, 23800d, 29600d, 33400d, 15400d, 28400d, 26700d, 46500d, 61200d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the Harricana River at Amos, Quebec, Canada annual maximum flows (cms) from 1915 to 1983.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 69-year record represents a northern Canadian watershed with snowmelt-dominated hydrology.
        /// The flows range from approximately 99 to 337 cubic meters per second (cms). This dataset exhibits
        /// characteristics typical of snowmelt flood regimes with moderate variability and slight positive skewness.
        /// </para>
        /// <para>
        /// <b>Reference:</b> Bobee &amp; Ashkar (1991), The Gamma Family and Derived Distributions Applied in Hydrology, Table 1.2
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used to verify MLE fitting for the Pearson Type III distribution,
        /// also known as the three-parameter Gamma distribution. Bobee &amp; Ashkar (1991) provide the expected MLE parameter
        /// estimates, making this dataset ideal for verification of the Pearson Type III fitting procedures.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 69 annual maximum flow observations in cubic meters per second.</returns>
        public static ExactSeries GetHarricanaRiverData()
        {
            var data = new double[] { 122d, 244d, 214d, 173d, 229d, 156d, 212d, 263d, 146d, 183d, 161d, 205d, 135d, 331d, 225d, 174d, 98.8d, 149d, 238d, 262d, 132d, 235d, 216d, 240d, 230d, 192d, 195d, 172d, 173d, 172d, 153d, 142d, 317d, 161d, 201d, 204d, 194d, 164d, 183d, 161d, 167d, 179d, 185d, 117d, 192d, 337d, 125d, 166d, 99.1d, 202d, 230d, 158d, 262d, 154d, 164d, 182d, 164d, 183d, 171d, 250d, 184d, 205d, 237d, 177d, 239d, 187d, 180d, 173d, 174d };
            return new ExactSeries(data);
        }

        /// <summary>
        /// Gets the air quality wind speed data from the R datasets package.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This dataset contains 153 daily wind speed observations in miles per hour (mph) from New York, May to September 1973.
        /// The data are from the built-in "airquality" dataset in the R programming language. Unlike the other hydrologic datasets
        /// in this class, this meteorological dataset provides a different distributional shape for testing flexible distribution families.
        /// </para>
        /// <para>
        /// <b>Reference:</b> R Core Team (2024). R: A Language and Environment for Statistical Computing.
        /// </para>
        /// <para>
        /// <b>Use in Verification:</b> This dataset is used for testing flexible distribution families and distributions
        /// that may be applied to non-hydrologic phenomena. The wind speed data exhibit different statistical properties
        /// compared to flood flows, providing additional verification of the software's distribution fitting capabilities
        /// across diverse applications.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 153 wind speed observations in miles per hour.</returns>
        public static ExactSeries GetAirQualityWindData()
        {
            var data = new double[] { 7.4, 8, 12.6, 11.5, 14.3, 14.9, 8.6, 13.8, 20.1, 8.6, 6.9, 9.7, 9.2, 10.9, 13.2, 11.5, 12, 18.4, 11.5, 9.7, 9.7, 16.6, 9.7, 12, 16.6, 14.9, 8, 12, 14.9, 5.7, 7.4, 8.6, 9.7, 16.1, 9.2, 8.6, 14.3, 9.7, 6.9, 13.8, 11.5, 10.9, 9.2, 8, 13.8, 11.5, 14.9, 20.7, 9.2, 11.5, 10.3, 6.3, 1.7, 4.6, 6.3, 8, 8, 10.3, 11.5, 14.9, 8, 4.1, 9.2, 9.2, 10.9, 4.6, 10.9, 5.1, 6.3, 5.7, 7.4, 8.6, 14.3, 14.9, 14.9, 14.3, 6.9, 10.3, 6.3, 5.1, 11.5, 6.9, 9.7, 11.5, 8.6, 8, 8.6, 12, 7.4, 7.4, 7.4, 9.2, 6.9, 13.8, 7.4, 6.9, 7.4, 4.6, 4, 10.3, 8, 8.6, 11.5, 11.5, 11.5, 9.7, 11.5, 10.3, 6.3, 7.4, 10.9, 10.3, 15.5, 14.3, 12.6, 9.7, 3.4, 8, 5.7, 9.7, 2.3, 6.3, 6.3, 6.9, 5.1, 2.8, 4.6, 7.4, 15.5, 10.9, 10.3, 10.9, 9.7, 14.9, 15.5, 6.3, 10.9, 11.5, 6.9, 13.8, 10.3, 10.3, 8, 12.6, 9.2, 10.3, 10.3, 16.6, 6.9, 13.2, 14.3, 8, 11.5 };
            return new ExactSeries(data);
        }

        #endregion

        #region MLE Test Configurations

        /// <summary>
        /// Test configuration for Normal distribution MLE using Tippecanoe River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Normal
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Tippecanoe River near Delphi, IN (48 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 5.1.1
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The MLE for the location parameter (μ) equals the sample mean, while the MLE for the scale parameter (σ)
        /// equals the population standard deviation (dividing by n rather than n-1). This differs from the sample
        /// standard deviation, which is an unbiased estimator. The MLE tends to slightly underestimate the true
        /// standard deviation for small samples but is asymptotically unbiased.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Tippecanoe River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (sample mean).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (population standard deviation).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_Normal_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetTippecanoeRiverData() };
            double trueLocation = Statistics.Mean(df.ExactSeries.ValuesToArray());
            double trueScale = Statistics.PopulationStandardDeviation(df.ExactSeries.ValuesToArray());
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Log-Normal (natural log) distribution MLE using Wabash River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Log-Normal (natural logarithm base)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Wabash River at Lafayette, IN (85 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 1.8.1
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// For the Log-Normal distribution, the MLEs are computed from the log-transformed data.
        /// The location parameter μ is the mean of the natural logarithms, and the scale parameter σ
        /// is the population standard deviation of the natural logarithms.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Wabash River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (mean of ln-transformed data).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (population std dev of ln-transformed data).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_LnNormal_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWabashRiverData() };
            var logData = df.ExactSeries.ValuesToArray().Map(Math.Log);
            double trueLocation = Statistics.Mean(logData);
            double trueScale = Statistics.PopulationStandardDeviation(logData);
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Log-Normal (base-10 log) distribution MLE using Wabash River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Log-Normal (base-10 logarithm)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Wabash River at Lafayette, IN (85 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 1.8.1
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// Similar to the natural log version, but using base-10 logarithms. This is commonly used in older
        /// hydrologic publications and is equivalent to the natural log version after appropriate parameter transformation.
        /// The location parameter μ is the mean of the base-10 logarithms, and the scale parameter σ
        /// is the population standard deviation of the base-10 logarithms.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Wabash River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (mean of log10-transformed data).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (population std dev of log10-transformed data).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_Log10Normal_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWabashRiverData() };
            var logData = df.ExactSeries.ValuesToArray().Map(Math.Log10); ;
            double trueLocation = Statistics.Mean(logData);
            double trueScale = Statistics.PopulationStandardDeviation(logData);
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Gumbel (Type I Extreme Value) distribution MLE using Sugar Creek data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Gumbel (Type I Extreme Value)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Sugar Creek at Crawfordsville, IN (53 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 7.2.1
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The verification report provides the expected MLE parameter estimates:
        /// Location (ξ) = 8,049.6 cfs and Scale (α) = 4,478.6 cfs.
        /// The Gumbel distribution is widely used in flood frequency analysis and has a fixed shape,
        /// making it a two-parameter distribution suitable for smaller datasets.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Sugar Creek data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (8,049.6 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (4,478.6 cfs).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_Gumbel_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetSugarCreekData() };
            double trueLocation = 8049.6;
            double trueScale = 4478.6;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Weibull distribution MLE using Tippecanoe River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Weibull (two-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Tippecanoe River near Delphi, IN (48 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 5.1.1
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// Rao &amp; Hamed (2000) only provide solutions for the 3-parameter Weibull, so this test
        /// validates the 2-parameter form against results obtained from R-Stan Bayesian estimation
        /// with weakly informative priors. The MLE procedure for the two-parameter Weibull requires
        /// numerical optimization.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Scale (λ): 14,196.9496 cfs</description></item>
        ///     <item><description>Shape (κ): 2.9829</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Tippecanoe River data.</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (14,196.9496 cfs).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (2.9829).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueScale, double TrueShape) Test_Weibull_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetTippecanoeRiverData() };
            double trueScale = 14196.9496;
            double trueShape = 2.9829;
            df.CalculatePlottingPositions();
            return (df, trueScale, trueShape);
        }

        /// <summary>
        /// Test configuration for Gamma distribution MLE using White River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Gamma (two-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> White River near Nora, IN (62 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 7.1.2
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The two-parameter Gamma distribution is fitted using MLE. This is a special case of the Pearson Type III
        /// distribution with zero location parameter. The Gamma distribution is frequently used for modeling
        /// positively skewed hydrologic variables.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A <see cref="DataFrame"/> configured with White River data.
        /// </returns>
        public static DataFrame Test_Gamma_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWhiteRiverData() };
            return df;
        }

        /// <summary>
        /// Test configuration for Gamma distribution MLE using Harricana River data with expected parameters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Gamma (two-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Harricana River at Amos, Quebec (69 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Bobee &amp; Ashkar (1991), Table 1.2
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The two-parameter Gamma distribution is fitted using MLE with verified parameter estimates.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Scale (θ): 1/0.08833 = 11.32 cms</description></item>
        ///     <item><description>Shape (κ): 16.89937</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Harricana River data.</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (1/0.08833).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (16.89937).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueScale, double TrueShape) Test_GammaDist_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetHarricanaRiverData() };
            double trueScale = 1.0 / 0.08833;
            double trueShape = 16.89937;
            df.CalculatePlottingPositions();
            return (df, trueScale, trueShape);
        }

        /// <summary>
        /// Test configuration for Pearson Type III distribution MLE using Harricana River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Pearson Type III (three-parameter Gamma)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Harricana River at Amos, Quebec (69 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Bobee &amp; Ashkar (1991), Table 1.2
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// Bobee &amp; Ashkar (1991) provide the expected MLE parameter estimates for the Pearson Type III distribution
        /// fitted to this dataset. The Pearson Type III is equivalent to the three-parameter Gamma distribution
        /// and is widely used in flood frequency analysis, particularly in Canada and parts of Europe.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Mean (μ): 191.31739 cms</description></item>
        ///     <item><description>Standard Deviation (σ): 47.01925 cms</description></item>
        ///     <item><description>Skewness (γ): 0.61897</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Harricana River data.</description></item>
        ///     <item><description><b>TrueMu:</b> Expected MLE mean parameter (191.31739 cms).</description></item>
        ///     <item><description><b>TrueSigma:</b> Expected MLE standard deviation parameter (47.01925 cms).</description></item>
        ///     <item><description><b>TrueGamma:</b> Expected MLE skewness parameter (0.61897).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueMu, double TrueSigma, double TrueGamma) Test_PearsonTypeIII_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetHarricanaRiverData() };
            double trueMu = 191.31739;
            double trueSigma = 47.01925;
            double trueGamma = 0.61897;
            df.CalculatePlottingPositions();
            return (df, trueMu, trueSigma, trueGamma);
        }

        /// <summary>
        /// Test configuration for Log-Pearson Type III distribution MLE using Harricana River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Log-Pearson Type III
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Harricana River at Amos, Quebec (69 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Bobee &amp; Ashkar (1991), Table 1.2
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The Log-Pearson Type III (LP3) distribution is the recommended distribution for flood frequency analysis
        /// in the United States per Bulletin 17C (England et al., 2018). The MLEs are computed from the
        /// log-transformed data using the Pearson Type III distribution. This test verifies the LP3 fitting
        /// implementation against established hydrologic datasets.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Mean (μ): 2.26878</description></item>
        ///     <item><description>Standard Deviation (σ): 0.10621</description></item>
        ///     <item><description>Skewness (γ): -0.02925</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Harricana River data.</description></item>
        ///     <item><description><b>TrueMu:</b> Expected MLE mean parameter (2.26878).</description></item>
        ///     <item><description><b>TrueSigma:</b> Expected MLE standard deviation parameter (0.10621).</description></item>
        ///     <item><description><b>TrueGamma:</b> Expected MLE skewness parameter (-0.02925).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueMu, double TrueSigma, double TrueGamma) Test_LogPearsonTypeIII_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetHarricanaRiverData() };
            double trueMu = 2.26878;
            double trueSigma = 0.10621;
            double trueGamma = -0.02925;
            df.CalculatePlottingPositions();
            return (df, trueMu, trueSigma, trueGamma);
        }

        /// <summary>
        /// Test configuration for Generalized Extreme Value (GEV) distribution MLE using White River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Generalized Extreme Value (GEV)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> White River near Nora, IN (62 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 7.1.2, Example 7.1.1, page 219
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The GEV distribution is a three-parameter family that encompasses the Gumbel, Frechet, and Weibull
        /// distributions as special cases, depending on the shape parameter. It is commonly used in extreme value
        /// analysis and is the theoretical limiting distribution for block maxima. This test verifies the MLE
        /// implementation for the full three-parameter GEV distribution.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 10,849 cfs</description></item>
        ///     <item><description>Scale (α): 5,745.6 cfs</description></item>
        ///     <item><description>Shape (κ): 0.005</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with White River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (10,849 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (5,745.6 cfs).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (0.005).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale, double TrueShape) Test_GEV_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWhiteRiverData() };
            double trueLocation = 10849;
            double trueScale = 5745.6;
            double trueShape = 0.005;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale, trueShape);
        }

        /// <summary>
        /// Test configuration for Generalized Pareto distribution MLE using White River at Mt. Carmel POT data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Generalized Pareto
        /// </para>
        /// <para>
        /// <b>Dataset:</b> White River at Mt. Carmel, IN exceedances over 50,000 cfs (287 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 8.3.1, Example 8.3.1, page 279
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The Generalized Pareto distribution is the theoretical limiting distribution for exceedances over a
        /// high threshold (Peaks-Over-Threshold analysis). With 287 observations, this large dataset provides
        /// strong verification of the asymptotic properties of the MLE estimator. The threshold of 50,000 cfs
        /// is incorporated into the analysis, and the distribution models the excesses above this threshold.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 50,400 cfs (threshold)</description></item>
        ///     <item><description>Scale (α): 55,142.29 cfs</description></item>
        ///     <item><description>Shape (κ): 0.0945</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with White River POT data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (50,400 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (55,142.29 cfs).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (0.0945).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale, double TrueShape) Test_GeneralizedPareto_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWhiteRiverAtMtCarmelData() };
            double trueLocation = 50400;
            double trueScale = 55142.29;
            double trueShape = 0.0945;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale, trueShape);
        }

        /// <summary>
        /// Test configuration for Exponential distribution MLE using Wabash River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Exponential (two-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Wabash River at Lafayette, IN (85 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 1.8.1, Example 6.1.1, page 132
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The Exponential distribution is the simplest model for exceedance data and represents the special
        /// case of the Gamma distribution with shape parameter = 1. The two-parameter form includes a
        /// location parameter ξ (threshold) and scale parameter α.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 13,100 cfs (minimum observed value)</description></item>
        ///     <item><description>Scale (α): 36,122.35 cfs</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Wabash River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (13,100 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (36,122.35 cfs).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_Exponential_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetWabashRiverData() };
            double trueLocation = 13100;
            double trueScale = 36122.35;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Generalized Normal distribution MLE using Air Quality wind data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Generalized Normal (three-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Air Quality wind data from R datasets package (153 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> R lmom package for L-moment estimates
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// MLE results may differ slightly from L-moment estimates but should be similar. The shape parameter
        /// controls the tail behavior, with negative values indicating lighter tails than the normal distribution.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 9.7285364 mph</description></item>
        ///     <item><description>Scale (α): 3.4885029 mph</description></item>
        ///     <item><description>Shape (κ): -0.1307169</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Air Quality wind data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (9.7285364 mph).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (3.4885029 mph).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (-0.1307169).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale, double TrueShape) Test_GeneralizedNormal_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetAirQualityWindData() };
            double trueLocation = 9.7285364;
            double trueScale = 3.4885029;
            double trueShape = -0.1307169;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale, trueShape);
        }

        /// <summary>
        /// Test configuration for Kappa-4 distribution MLE using Air Quality wind data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Kappa-4 (four-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Air Quality wind data from R datasets package (153 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> R lmom package for L-moment estimates
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The Kappa-4 is a four-parameter distribution that encompasses many common distributions as special cases,
        /// including the GEV, Generalized Pareto, Generalized Logistic, and others. It provides great flexibility
        /// for modeling data with varying tail behavior. MLE results may differ from L-moment estimates but should be similar.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 8.68360234 mph</description></item>
        ///     <item><description>Scale (α): 3.10384972 mph</description></item>
        ///     <item><description>Shape 1 (κ): 0.14470737</description></item>
        ///     <item><description>Shape 2 (h): -0.07348014</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Air Quality wind data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (8.68360234 mph).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (3.10384972 mph).</description></item>
        ///     <item><description><b>TrueShape1:</b> Expected MLE first shape parameter (0.14470737).</description></item>
        ///     <item><description><b>TrueShape2:</b> Expected MLE second shape parameter (-0.07348014).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale, double TrueShape1, double TrueShape2) Test_Kappa4_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetAirQualityWindData() };
            double trueLocation = 8.68360234;
            double trueScale = 3.10384972;
            double trueShape1 = 0.14470737;
            double trueShape2 = -0.07348014;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale, trueShape1, trueShape2);
        }

        /// <summary>
        /// Test configuration for Logistic distribution MLE using Tippecanoe River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Logistic (two-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> Tippecanoe River near Delphi, IN (48 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Example 9.1.1, page 295
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// The Logistic distribution has heavier tails than the Normal distribution and is sometimes
        /// used as an alternative for modeling hydrologic extremes.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 12,628.59 cfs</description></item>
        ///     <item><description>Scale (α): 2,708.64 cfs</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Tippecanoe River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (12,628.59 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (2,708.64 cfs).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale) Test_Logistic_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetTippecanoeRiverData() };
            double trueLocation = 12628.59;
            double trueScale = 2708.64;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale);
        }

        /// <summary>
        /// Test configuration for Generalized Logistic distribution MLE using East Fork White River data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Distribution:</b> Generalized Logistic (three-parameter)
        /// </para>
        /// <para>
        /// <b>Dataset:</b> East Fork White River at Seymour, IN (68 observations)
        /// </para>
        /// <para>
        /// <b>Reference:</b> Rao &amp; Hamed (2000), Table 9.2.1, Example 9.1.1, page 295
        /// </para>
        /// <para>
        /// <b>Expected Results:</b>
        /// Note: The textbook uses summary statistics that differ from those calculated directly from
        /// the provided data table. These discrepancies affect the parameter estimates significantly.
        /// When using the textbook's summary statistics, the MLE results match closely. However, when
        /// using the actual dataset, parameter estimates differ. This test validates that RMC-BestFit
        /// results are within 10% of the textbook values, accounting for these data inconsistencies.
        /// </para>
        /// <para>
        /// <b>Expected Parameters:</b>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): 30,911.83 cfs</description></item>
        ///     <item><description>Scale (α): 9,305.0205 cfs</description></item>
        ///     <item><description>Shape (κ): -0.144152</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with East Fork White River data.</description></item>
        ///     <item><description><b>TrueLocation:</b> Expected MLE location parameter (30,911.83 cfs).</description></item>
        ///     <item><description><b>TrueScale:</b> Expected MLE scale parameter (9,305.0205 cfs).</description></item>
        ///     <item><description><b>TrueShape:</b> Expected MLE shape parameter (-0.144152).</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double TrueLocation, double TrueScale, double TrueShape) Test_GeneralizedLogistic_MLE()
        {
            var df = new DataFrame() { ExactSeries = GetEastForkWhiteRiverData() };
            double trueLocation = 30911.83;
            double trueScale = 9305.0205;
            double trueShape = -0.144152;
            df.CalculatePlottingPositions();
            return (df, trueLocation, trueScale, trueShape);
        }

        #endregion
    }
}
