using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>Reads effective FFA configuration without assigning model properties or estimating parameters.</summary>
    public static class FrequencyConfigurationMapper
    {
        /// <summary>Maps stationary univariate and B17C configuration; other analysis kinds return null.</summary>
        /// <param name="resource">The configured analysis resource.</param>
        /// <returns>A detached configuration snapshot, or null for other analysis kinds.</returns>
        public static FrequencyConfigurationDto? Map(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind == AnalysisKind.Univariate)
            {
                var analysis = resource.Univariate!;
                var model = analysis.UnivariateDistribution;
                var sampler = analysis.BayesianAnalysis;
                return new FrequencyConfigurationDto
                {
                    Distribution = EnumHelper.ToCamelCase(model.DistributionType.ToString()),
                    ProbabilityOrdinates = analysis.ProbabilityOrdinates.ToList(),
                    UseDefaultFlatPriors = model.UseDefaultFlatPriors,
                    UseJeffreysRuleForScale = model.UseJeffreysRuleForScale,
                    EnableQuantilePriors = model.EnableQuantilePriors,
                    UseSingleQuantile = model.UseSingleQuantile,
                    ParameterPriors = model.Parameters.Select(p => new ParameterPriorDto
                    {
                        ParameterName = p.DisplayName, IsFixed = p.IsFixed,
                        Distribution = DistributionSpecMapper.ToSpec(p.PriorDistribution)
                    }).ToList(),
                    QuantilePriors = model.EnableQuantilePriors ? model.QuantilePriors.Select(q => new QuantilePriorDto
                    {
                        Alpha = q.Alpha, Distribution = DistributionSpecMapper.ToSpec(q.Distribution)
                    }).ToList() : new(),
                    UseSimulationDefaults = sampler.UseSimulationDefaults,
                    BayesianOptions = new BayesianOptionsDto
                    {
                        Sampler = sampler.Type, Iterations = sampler.Iterations,
                        WarmupIterations = sampler.WarmupIterations, ThinningInterval = sampler.ThinningInterval,
                        NumberOfChains = sampler.NumberOfChains, PrngSeed = sampler.PRNGSeed,
                        CredibleIntervalWidth = sampler.CredibleIntervalWidth, OutputLength = sampler.OutputLength,
                        PointEstimator = sampler.PointEstimator
                    }
                };
            }
            if (resource.Kind == AnalysisKind.Bulletin17C)
            {
                var analysis = resource.Bulletin17C!;
                var model = analysis.Bulletin17CDistribution;
                return new FrequencyConfigurationDto
                {
                    Distribution = EnumHelper.ToCamelCase(model.DistributionType.ToString()),
                    ProbabilityOrdinates = analysis.ProbabilityOrdinates.ToList(),
                    UncertaintyMethod = EnumHelper.ToCamelCase(analysis.UncertaintyMethod.ToString()),
                    ParameterPenalties = model.ParameterPenalties.Where(p => p.Enabled).Select(p => new ParameterPenaltyDto
                    {
                        ParameterName = p.Name, Mean = p.Mean, Mse = p.MSE, UseLog = p.UseLog
                    }).ToList(),
                    QuantilePenalties = model.QuantilePenalties.Where(p => p.Enabled).Select(p => new QuantilePenaltyDto
                    {
                        Aep = p.AEP, Mean = p.Mean, Mse = p.MSE, UseLog10 = p.UseLog10
                    }).ToList()
                };
            }
            return null;
        }
    }
}
