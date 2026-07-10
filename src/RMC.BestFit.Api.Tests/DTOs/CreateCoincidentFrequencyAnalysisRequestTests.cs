using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateCoincidentFrequencyAnalysisRequest"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateCoincidentFrequencyAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: 50 bins, empty ordinates and surface, null options.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateCoincidentFrequencyAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.BivariateAnalysisId);
            Assert.AreEqual(0, request.XValues.Count);
            Assert.AreEqual(0, request.YValues.Count);
            Assert.AreEqual(0, request.BivariateResponse.Count);
            Assert.AreEqual(50, request.NumberOfBins);
            Assert.IsNull(request.CredibleIntervalWidth);
            Assert.IsNull(request.PointEstimator);
        }

        /// <summary>
        /// Verifies the ordinates and surface survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = Guid.NewGuid(),
                XValues = new List<double> { 1d, 2d },
                YValues = new List<double> { 10d, 20d },
                BivariateResponse = new List<List<double>>
                {
                    new() { 100d, 110d },
                    new() { 120d, 130d }
                },
                NumberOfBins = 25,
                CredibleIntervalWidth = 0.95,
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.BivariateAnalysisId, copy.BivariateAnalysisId);
            CollectionAssert.AreEqual(request.XValues, copy.XValues);
            CollectionAssert.AreEqual(request.YValues, copy.YValues);
            Assert.AreEqual(2, copy.BivariateResponse.Count);
            Assert.AreEqual(130d, copy.BivariateResponse[1][1]);
            Assert.AreEqual(25, copy.NumberOfBins);
            Assert.AreEqual(0.95, copy.CredibleIntervalWidth);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, copy.PointEstimator);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateCoincidentFrequencyAnalysisRequest
            {
                XValues = new List<double> { 1d },
                YValues = new List<double> { 2d },
                BivariateResponse = new List<List<double>> { new() { 3d } }
            });
            StringAssert.Contains(json, "\"bivariateAnalysisId\"");
            StringAssert.Contains(json, "\"xValues\"");
            StringAssert.Contains(json, "\"yValues\"");
            StringAssert.Contains(json, "\"bivariateResponse\"");
            StringAssert.Contains(json, "\"numberOfBins\"");
        }
    }
}
