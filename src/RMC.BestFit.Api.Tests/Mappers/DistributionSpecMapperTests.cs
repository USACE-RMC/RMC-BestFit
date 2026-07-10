using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="DistributionSpecMapper"/>: spec materialization, parameter-count
    /// and parameter-value validation, and the response echo.
    /// </summary>
    [TestClass]
    public class DistributionSpecMapperTests
    {
        /// <summary>
        /// Verifies a valid spec builds the distribution with the supplied parameters applied.
        /// </summary>
        [TestMethod]
        public void ToDistribution_BuildsConfiguredDistribution()
        {
            var spec = new DistributionSpecDto
            {
                Type = UnivariateDistributionType.Normal,
                Parameters = new List<double> { 100d, 20d }
            };

            var distribution = DistributionSpecMapper.ToDistribution(spec, "test.distribution");

            Assert.AreEqual(UnivariateDistributionType.Normal, distribution.Type);
            CollectionAssert.AreEqual(new[] { 100d, 20d }, distribution.GetParameters);

            var triangular = DistributionSpecMapper.ToDistribution(new DistributionSpecDto
            {
                Type = UnivariateDistributionType.Triangular,
                Parameters = new List<double> { 1d, 2d, 3d }
            }, "test.distribution");
            Assert.AreEqual(UnivariateDistributionType.Triangular, triangular.Type);
        }

        /// <summary>
        /// Verifies a null spec is rejected with the field context in the message.
        /// </summary>
        [TestMethod]
        public void ToDistribution_NullSpec_Throws()
        {
            var ex = Assert.ThrowsException<ArgumentException>(
                () => DistributionSpecMapper.ToDistribution(null, "parameterPriors[0].distribution"));
            StringAssert.Contains(ex.Message, "parameterPriors[0].distribution");
        }

        /// <summary>
        /// Verifies a wrong parameter count is rejected with the canonical names in the message.
        /// </summary>
        [TestMethod]
        public void ToDistribution_WrongParameterCount_Throws()
        {
            var spec = new DistributionSpecDto
            {
                Type = UnivariateDistributionType.Normal,
                Parameters = new List<double> { 100d }
            };

            var ex = Assert.ThrowsException<ArgumentException>(
                () => DistributionSpecMapper.ToDistribution(spec, "uncertainData[3].distribution"));
            StringAssert.Contains(ex.Message, "uncertainData[3].distribution");
            StringAssert.Contains(ex.Message, "2");
        }

        /// <summary>
        /// Verifies invalid parameter values (a non-positive standard deviation) are rejected.
        /// </summary>
        [TestMethod]
        public void ToDistribution_InvalidParameterValues_Throws()
        {
            var spec = new DistributionSpecDto
            {
                Type = UnivariateDistributionType.Normal,
                Parameters = new List<double> { 100d, -5d }
            };

            var ex = Assert.ThrowsException<ArgumentException>(
                () => DistributionSpecMapper.ToDistribution(spec, "test.distribution"));
            StringAssert.Contains(ex.Message, "test.distribution");
        }

        /// <summary>
        /// Verifies the echo carries the type and current parameter values, round-tripping the spec.
        /// </summary>
        [TestMethod]
        public void ToSpec_EchoesTypeAndParameters()
        {
            var spec = new DistributionSpecDto
            {
                Type = UnivariateDistributionType.LogNormal,
                Parameters = new List<double> { 3.5, 0.25 }
            };

            var echo = DistributionSpecMapper.ToSpec(DistributionSpecMapper.ToDistribution(spec, "test"));

            Assert.AreEqual(UnivariateDistributionType.LogNormal, echo.Type);
            CollectionAssert.AreEqual(spec.Parameters, echo.Parameters);

            Assert.ThrowsException<ArgumentNullException>(() => DistributionSpecMapper.ToSpec(null!));
        }
    }
}
