using System.Text;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Builds the discovery/metadata responses that let API clients (and MCP agents in particular)
    /// enumerate the accepted enum values and supported distributions before constructing requests.
    /// </summary>
    public static class MetadataMapper
    {
        /// <summary>
        /// Builds the distributions listing: every distribution type supported by the univariate
        /// or Bulletin 17C analysis, with display names and the kinds that support it.
        /// </summary>
        /// <returns>The distributions response.</returns>
        public static DistributionsResponse ToDistributionsResponse()
        {
            var response = new DistributionsResponse();
            foreach (var type in Enum.GetValues<UnivariateDistributionType>())
            {
                bool univariate = UnivariateDistribution.IsSupportedDistributionType(type);
                bool bulletin17C = Bulletin17CDistribution.IsSupportedDistributionType(type);
                bool mixture = MixtureModel.IsSupportedDistributionType(type);
                bool competingRisks = CompetingRisksModel.IsSupportedDistributionType(type);
                bool pointProcess = PointProcessModel.IsSupportedDistributionType(type);
                if (!univariate && !bulletin17C && !mixture && !competingRisks && !pointProcess) continue;

                var info = new DistributionInfoDto
                {
                    Name = EnumHelper.ToCamelCase(type.ToString()),
                    DisplayName = ToDisplayName(type),
                    ParameterNames = UnivariateDistributionFactory.CreateDistribution(type).ParameterNames.ToList()
                };
                if (univariate) info.SupportedBy.Add("univariate");
                if (bulletin17C) info.SupportedBy.Add("bulletin17c");
                if (mixture) info.SupportedBy.Add("mixture");
                if (pointProcess) info.SupportedBy.Add("pointprocess");
                if (competingRisks) info.SupportedBy.Add("competingrisks");
                // The fitting screen accepts exactly the univariate candidate set.
                if (univariate) info.SupportedBy.Add("distributionfitting");
                response.Distributions.Add(info);
            }
            return response;
        }

        /// <summary>
        /// Builds the enum-options listing covering every enum-typed request field in the API.
        /// </summary>
        /// <returns>The enum options response.</returns>
        public static EnumOptionsResponse ToEnumOptionsResponse()
        {
            return new EnumOptionsResponse
            {
                Samplers = EnumHelper.CamelCaseNames<BayesianAnalysis.SamplerType>(),
                PointEstimators = EnumHelper.CamelCaseNames<BayesianAnalysis.PointEstimateType>(),
                UncertaintyMethods = EnumHelper.CamelCaseNames<UncertaintyMethod>(),
                TimeBlockWindows = EnumHelper.CamelCaseNames<TimeBlockWindow>(),
                BlockFunctions = EnumHelper.CamelCaseNames<BlockFunctionType>(),
                SmoothingFunctions = EnumHelper.CamelCaseNames<SmoothingFunctionType>(),
                UsgsSeriesTypes = EnumHelper.CamelCaseNames<TimeSeriesDownload.TimeSeriesType>(),
                TimeIntervals = EnumHelper.CamelCaseNames<TimeInterval>(),
                AnalysisKinds = EnumHelper.CamelCaseNames<AnalysisKind>(),
                InputDataMethods = EnumHelper.CamelCaseNames<InputDataMethod>(),
                PriorDistributions = _priorDistributionNames.Value,
                CompositeTypes = EnumHelper.CamelCaseNames<CompositeType>(),
                AverageMethods = EnumHelper.CamelCaseNames<AverageMethod>(),
                DependencyTypes = EnumHelper.CamelCaseNames<Numerics.Data.Statistics.Probability.DependencyType>(),
                CopulaTypes = EnumHelper.CamelCaseNames<Numerics.Distributions.Copulas.CopulaType>(),
                CopulaEstimationMethods = EnumHelper.CamelCaseNames<Numerics.Distributions.Copulas.CopulaEstimationMethod>(),
                TimeSeriesModelTypes = EnumHelper.CamelCaseNames<TimeSeriesModelType>(),
                // Fully qualified: Numerics.Data.Transform also exists (documented ambiguity).
                TransformTypes = EnumHelper.CamelCaseNames<RMC.BestFit.Models.Transform>(),
                TrendTypes = EnumHelper.CamelCaseNames<ARIMAX.Trend>(),
                CovariateExtensions = EnumHelper.CamelCaseNames<ARIMAX.CovariateExtensionMethod>()
            };
        }

        /// <summary>
        /// The camelCase names of every distribution type the factory can construct, computed
        /// once. Factory support is the operational definition of what a distribution spec
        /// (uncertain observation, parameter prior, quantile prior) may name.
        /// </summary>
        private static readonly Lazy<List<string>> _priorDistributionNames = new(() =>
        {
            var names = new List<string>();
            foreach (var type in Enum.GetValues<UnivariateDistributionType>())
            {
                try
                {
                    UnivariateDistributionFactory.CreateDistribution(type);
                    names.Add(EnumHelper.ToCamelCase(type.ToString()));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Not constructible from a bare type (e.g., composite types that require
                    // component distributions) — excluded from the spec list.
                }
            }
            return names;
        });

        /// <summary>
        /// Returns the human-readable display name for a distribution type, matching the names
        /// used in the RMC-BestFit desktop application and documentation.
        /// </summary>
        /// <param name="type">The distribution type.</param>
        /// <returns>The display name.</returns>
        public static string ToDisplayName(UnivariateDistributionType type)
        {
            return type switch
            {
                UnivariateDistributionType.Exponential => "Exponential",
                UnivariateDistributionType.GammaDistribution => "Gamma",
                UnivariateDistributionType.GeneralizedExtremeValue => "Generalized Extreme Value",
                UnivariateDistributionType.GeneralizedLogistic => "Generalized Logistic",
                UnivariateDistributionType.GeneralizedNormal => "Generalized Normal",
                UnivariateDistributionType.GeneralizedPareto => "Generalized Pareto",
                UnivariateDistributionType.Gumbel => "Gumbel",
                UnivariateDistributionType.KappaFour => "Kappa Four",
                UnivariateDistributionType.LnNormal => "Ln-Normal",
                UnivariateDistributionType.Logistic => "Logistic",
                UnivariateDistributionType.LogNormal => "Log-Normal",
                UnivariateDistributionType.LogPearsonTypeIII => "Log-Pearson Type III",
                UnivariateDistributionType.Normal => "Normal",
                UnivariateDistributionType.PearsonTypeIII => "Pearson Type III",
                UnivariateDistributionType.Weibull => "Weibull",
                _ => SplitPascalCase(type.ToString())
            };
        }

        /// <summary>
        /// Fallback display-name formatter: inserts spaces at the case boundaries of a PascalCase
        /// enum member name (e.g., "GeneralizedExtremeValue" → "Generalized Extreme Value").
        /// </summary>
        /// <param name="name">The PascalCase enum member name.</param>
        /// <returns>The spaced display name.</returns>
        private static string SplitPascalCase(string name)
        {
            var builder = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    builder.Append(' ');
                }
                builder.Append(name[i]);
            }
            return builder.ToString();
        }
    }
}
