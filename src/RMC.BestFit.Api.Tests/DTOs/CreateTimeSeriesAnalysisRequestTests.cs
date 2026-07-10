using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateTimeSeriesAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateTimeSeriesAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: every optional field null so the model defaults apply.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateTimeSeriesAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.TimeSeriesId);
            Assert.AreEqual(TimeSeriesModelType.Ar, request.ModelType);
            Assert.IsNull(request.Order);
            Assert.IsNull(request.POrder);
            Assert.IsNull(request.DOrder);
            Assert.IsNull(request.QOrder);
            Assert.IsNull(request.XOrder);
            Assert.IsNull(request.IncludeIntercept);
            Assert.IsNull(request.TransformType);
            Assert.IsNull(request.TrendType);
            Assert.IsNull(request.IncludeSeasonality);
            Assert.IsNull(request.CovariateTimeSeriesIds);
            Assert.IsNull(request.CovariateExtension);
            Assert.IsNull(request.TrainingTimeSteps);
            Assert.IsNull(request.ForecastingTimeSteps);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.ParameterPriors);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = Guid.NewGuid(),
                ModelType = TimeSeriesModelType.Arimax,
                POrder = 2,
                DOrder = 1,
                QOrder = 1,
                XOrder = 1,
                IncludeIntercept = false,
                TransformType = Transform.Logarithmic,
                TrendType = ARIMAX.Trend.Quadratic,
                IncludeSeasonality = true,
                CovariateTimeSeriesIds = new List<Guid> { Guid.NewGuid() },
                CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN,
                TrainingTimeSteps = 800,
                ForecastingTimeSteps = 24,
                Name = "arimax"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(TimeSeriesModelType.Arimax, copy.ModelType);
            Assert.AreEqual(2, copy.POrder);
            Assert.AreEqual(1, copy.DOrder);
            Assert.AreEqual(1, copy.XOrder);
            Assert.IsFalse(copy.IncludeIntercept!.Value);
            Assert.AreEqual(Transform.Logarithmic, copy.TransformType);
            Assert.AreEqual(ARIMAX.Trend.Quadratic, copy.TrendType);
            Assert.IsTrue(copy.IncludeSeasonality!.Value);
            Assert.AreEqual(1, copy.CovariateTimeSeriesIds!.Count);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, copy.CovariateExtension);
            Assert.AreEqual(800, copy.TrainingTimeSteps);
            Assert.AreEqual(24, copy.ForecastingTimeSteps);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings, omitting nulls.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateTimeSeriesAnalysisRequest
            {
                ModelType = TimeSeriesModelType.Arima,
                TransformType = Transform.BoxCox
            });
            StringAssert.Contains(json, "\"timeSeriesId\"");
            StringAssert.Contains(json, "\"modelType\"");
            StringAssert.Contains(json, "\"arima\"");
            StringAssert.Contains(json, "\"transformType\"");
            StringAssert.Contains(json, "\"boxCox\"");
            Assert.IsFalse(json.Contains("\"order\""), "Null order should be omitted.");
        }
    }
}
