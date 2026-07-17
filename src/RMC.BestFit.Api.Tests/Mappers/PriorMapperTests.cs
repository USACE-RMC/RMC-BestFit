using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="PriorMapper"/>: name-matched parameter priors (including the
    /// default-flat-prior reset guard), quantile priors, and Bulletin 17C penalties. Models are
    /// only configured — never estimated.
    /// </summary>
    [TestClass]
    public class PriorMapperTests
    {
        /// <summary>
        /// Builds a Log-Pearson Type III univariate model over the standard test record.
        /// </summary>
        /// <returns>The configured model.</returns>
        private static UnivariateDistribution CreateModel()
        {
            return new UnivariateDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
        }

        /// <summary>
        /// Verifies a prior is applied to the named parameter (leaving the others untouched),
        /// the fixed flag is honored, and the default-flat-priors flag is switched off.
        /// </summary>
        [TestMethod]
        public void ApplyParameterPriors_AppliesNamedPrior()
        {
            var model = CreateModel();
            var untouchedPrior = model.Parameters[0].PriorDistribution;
            string skewName = model.Parameters[2].DisplayName;

            PriorMapper.ApplyParameterPriors(model, new List<ParameterPriorDto>
            {
                new()
                {
                    ParameterName = skewName,
                    Distribution = new DistributionSpecDto
                    {
                        Type = UnivariateDistributionType.Normal,
                        Parameters = new List<double> { -0.2, 0.3 }
                    },
                    IsFixed = true
                }
            });

            Assert.IsFalse(model.UseDefaultFlatPriors, "Supplying a prior must switch off the default flat priors.");
            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[2].PriorDistribution.Type);
            CollectionAssert.AreEqual(new[] { -0.2, 0.3 }, model.Parameters[2].PriorDistribution.GetParameters);
            Assert.IsTrue(model.Parameters[2].IsFixed);
            Assert.AreSame(untouchedPrior, model.Parameters[0].PriorDistribution, "Unnamed parameters must keep their existing priors.");
        }

        /// <summary>
        /// Verifies applied priors survive a later data-frame replacement — the model rebuilds
        /// default parameters on data changes only while UseDefaultFlatPriors is true, and the
        /// mapper switches that flag off before applying.
        /// </summary>
        [TestMethod]
        public void ApplyParameterPriors_SurviveDataFrameChange()
        {
            var model = CreateModel();
            string skewName = model.Parameters[2].DisplayName;
            PriorMapper.ApplyParameterPriors(model, new List<ParameterPriorDto>
            {
                new()
                {
                    ParameterName = skewName,
                    Distribution = new DistributionSpecDto
                    {
                        Type = UnivariateDistributionType.Normal,
                        Parameters = new List<double> { -0.1, 0.25 }
                    }
                }
            });

            model.DataFrame = TestAnalyses.CreateDataFrame(25);

            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[2].PriorDistribution.Type,
                "The informative prior must survive data-frame changes once default flat priors are off.");
        }

        /// <summary>
        /// Verifies a client name without the trailing short-form marker (e.g., "Skew (of log)"
        /// for "Skew (of log) (γ)") matches, so clients never need to type Greek letters.
        /// </summary>
        [TestMethod]
        public void ApplyParameterPriors_MatchesNameWithoutShortForm()
        {
            var model = CreateModel();
            string fullName = model.Parameters[2].DisplayName;
            string clientName = fullName[..fullName.LastIndexOf(" (", StringComparison.Ordinal)];

            PriorMapper.ApplyParameterPriors(model, new List<ParameterPriorDto>
            {
                new()
                {
                    ParameterName = clientName,
                    Distribution = new DistributionSpecDto
                    {
                        Type = UnivariateDistributionType.Normal,
                        Parameters = new List<double> { -0.2, 0.3 }
                    }
                }
            });

            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[2].PriorDistribution.Type);
        }

        /// <summary>
        /// Verifies unknown, duplicated, and blank parameter names are rejected, with the valid
        /// names listed for the unknown case.
        /// </summary>
        [TestMethod]
        public void ApplyParameterPriors_BadNames_Throw()
        {
            var spec = new DistributionSpecDto { Type = UnivariateDistributionType.Normal, Parameters = new List<double> { 0d, 1d } };

            var unknown = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyParameterPriors(CreateModel(),
                new List<ParameterPriorDto> { new() { ParameterName = "No Such Parameter", Distribution = spec } }));
            StringAssert.Contains(unknown.Message, "No Such Parameter");
            StringAssert.Contains(unknown.Message, "Valid names");

            var model = CreateModel();
            string name = model.Parameters[0].DisplayName;
            var duplicate = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyParameterPriors(model,
                new List<ParameterPriorDto>
                {
                    new() { ParameterName = name, Distribution = spec },
                    new() { ParameterName = name, Distribution = spec }
                }));
            StringAssert.Contains(duplicate.Message, "more than once");

            var blank = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyParameterPriors(CreateModel(),
                new List<ParameterPriorDto> { new() { ParameterName = " ", Distribution = spec } }));
            StringAssert.Contains(blank.Message, "parameterName is required");
        }

        /// <summary>
        /// Verifies a null or empty prior list is a no-op that keeps the default flat priors on.
        /// </summary>
        [TestMethod]
        public void ApplyParameterPriors_NullOrEmpty_IsNoOp()
        {
            var model = CreateModel();
            PriorMapper.ApplyParameterPriors(model, null);
            Assert.IsTrue(model.UseDefaultFlatPriors);
            PriorMapper.ApplyParameterPriors(model, new List<ParameterPriorDto>());
            Assert.IsTrue(model.UseDefaultFlatPriors);
            Assert.ThrowsException<ArgumentNullException>(() => PriorMapper.ApplyParameterPriors(null!, null));
        }

        /// <summary>
        /// Verifies quantile priors are enabled and assigned with the client's ordinates and
        /// distributions, honoring the single-quantile flag.
        /// </summary>
        [TestMethod]
        public void ApplyQuantilePriors_EnablesAndAssigns()
        {
            var model = CreateModel();
            PriorMapper.ApplyQuantilePriors(model, new List<QuantilePriorDto>
            {
                new() { Alpha = 0.01, Distribution = new DistributionSpecDto { Type = UnivariateDistributionType.LogNormal, Parameters = new List<double> { 4.7, 0.1 } } }
            }, useSingleQuantile: true);

            Assert.IsTrue(model.EnableQuantilePriors);
            Assert.IsTrue(model.UseSingleQuantile);
            Assert.AreEqual(1, model.QuantilePriors.Count);
            Assert.AreEqual(0.01, model.QuantilePriors[0].Alpha);
            Assert.AreEqual(UnivariateDistributionType.LogNormal, model.QuantilePriors[0].Distribution.Type);
        }

        /// <summary>
        /// Verifies out-of-range exceedance probabilities are rejected and a null or empty list
        /// leaves quantile priors disabled.
        /// </summary>
        [TestMethod]
        public void ApplyQuantilePriors_Validation()
        {
            var spec = new DistributionSpecDto { Type = UnivariateDistributionType.Normal, Parameters = new List<double> { 0d, 1d } };
            var ex = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyQuantilePriors(CreateModel(),
                new List<QuantilePriorDto> { new() { Alpha = 1.0, Distribution = spec } }, null));
            StringAssert.Contains(ex.Message, "strictly between 0 and 1");

            var model = CreateModel();
            PriorMapper.ApplyQuantilePriors(model, null, useSingleQuantile: true);
            Assert.IsFalse(model.EnableQuantilePriors, "A null prior list must not enable quantile priors.");
        }

        /// <summary>
        /// Verifies a parameter penalty fills the pre-created, index-aligned entry by name and
        /// leaves the other entries disabled — the collection is never replaced.
        /// </summary>
        [TestMethod]
        public void ApplyPenalties_FillsParameterPenaltyByName()
        {
            var distribution = new Bulletin17CDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
            int entryCount = distribution.ParameterPenalties.Count;
            string skewName = distribution.ParameterPenalties[2].Name;

            PriorMapper.ApplyPenalties(distribution,
                new List<ParameterPenaltyDto> { new() { ParameterName = skewName, Mean = -0.05, Mse = 0.12 } },
                quantilePenalties: null);

            Assert.AreEqual(entryCount, distribution.ParameterPenalties.Count, "The penalty collection must stay index-aligned with the parameters.");
            Assert.IsTrue(distribution.ParameterPenalties[2].Enabled);
            Assert.AreEqual(-0.05, distribution.ParameterPenalties[2].Mean);
            Assert.AreEqual(0.12, distribution.ParameterPenalties[2].MSE);
            Assert.IsFalse(distribution.ParameterPenalties[0].Enabled, "Unnamed penalties must stay disabled.");
        }

        /// <summary>
        /// Verifies quantile penalties replace the pre-created placeholder contents with enabled
        /// entries carrying the client's values.
        /// </summary>
        [TestMethod]
        public void ApplyPenalties_ReplacesQuantilePenaltyContents()
        {
            var distribution = new Bulletin17CDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);

            PriorMapper.ApplyPenalties(distribution, parameterPenalties: null, quantilePenalties: new List<QuantilePenaltyDto>
            {
                new() { Aep = 0.002, Mean = 4.85, Mse = 0.02 },
                new() { Aep = 0.01, Mean = 4.5, Mse = 0.05, UseLog10 = false }
            });

            Assert.AreEqual(2, distribution.QuantilePenalties.Count);
            Assert.IsTrue(distribution.QuantilePenalties[0].Enabled);
            Assert.AreEqual(0.002, distribution.QuantilePenalties[0].AEP);
            Assert.IsTrue(distribution.QuantilePenalties[0].UseLog10);
            Assert.IsFalse(distribution.QuantilePenalties[1].UseLog10);
        }

        /// <summary>
        /// Verifies penalty validation: unknown parameter names, non-positive MSE, log-space
        /// penalties with non-positive means, and out-of-range exceedance probabilities.
        /// </summary>
        [TestMethod]
        public void ApplyPenalties_Validation()
        {
            var distribution = new Bulletin17CDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);

            var unknown = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyPenalties(distribution,
                new List<ParameterPenaltyDto> { new() { ParameterName = "No Such Parameter", Mean = 0d, Mse = 1d } }, null));
            StringAssert.Contains(unknown.Message, "Valid names");

            var badMse = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyPenalties(distribution,
                new List<ParameterPenaltyDto> { new() { ParameterName = distribution.ParameterPenalties[0].Name, Mean = 0d, Mse = 0d } }, null));
            StringAssert.Contains(badMse.Message, "greater than 0");

            var badLog = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyPenalties(distribution,
                new List<ParameterPenaltyDto> { new() { ParameterName = distribution.ParameterPenalties[0].Name, Mean = -1d, Mse = 1d, UseLog = true } }, null));
            StringAssert.Contains(badLog.Message, "useLog");

            var badAep = Assert.ThrowsException<ArgumentException>(() => PriorMapper.ApplyPenalties(distribution, null,
                new List<QuantilePenaltyDto> { new() { Aep = 0d, Mean = 1d, Mse = 1d } }));
            StringAssert.Contains(badAep.Message, "strictly between 0 and 1");

            Assert.ThrowsException<ArgumentNullException>(() => PriorMapper.ApplyPenalties(null!, null, null));
        }
    }
}
