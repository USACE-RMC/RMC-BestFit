using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="InputDataService.CreateManual"/>'s application of
    /// <see cref="CreateManualInputDataRequest.LowOutlierThreshold"/> (Task 2.10 / decision D5,
    /// approved 25 September 2026): the threshold is applied with
    /// <see cref="RMC.BestFit.Models.DataFrame.SetLowOutliersFromThreshold"/> after the exact
    /// series is populated, mirroring the desktop's <c>InputData.Open</c> behavior, instead of
    /// only being stored. No estimator or optimizer runs; these are data-frame construction
    /// contracts only.
    /// </summary>
    [TestClass]
    public class LowOutlierThresholdInputDataTests
    {
        /// <summary>The isolated resource store.</summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>The input service under test.</summary>
        private InputDataService _inputs = null!;

        /// <summary>Initializes an independent store and service for each test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _inputs = new InputDataService(_store, new FakeUsgsTimeSeriesService());
        }

        /// <summary>
        /// Builds a 15-observation request. Two values (0, 0) sit well below any interesting
        /// threshold; the remaining thirteen climb from 100 to 320 so the sorted upper-middle
        /// value (index Count/2 = 7) is 180 - a deliberate boundary for the 50%-censoring guard.
        /// </summary>
        /// <param name="threshold">The manual low-outlier threshold, or null to omit it.</param>
        /// <param name="preflagFirstAs">Optional <see cref="ExactObservationDto.IsLowOutlier"/> value for the first observation (value 0).</param>
        /// <returns>The request, ready to pass to <see cref="InputDataService.CreateManual"/>.</returns>
        private static CreateManualInputDataRequest Request(double? threshold, bool? preflagFirstAs = null)
        {
            double[] values = { 0d, 0d, 100d, 100d, 120d, 140d, 160d, 180d, 200d, 220d, 240d, 260d, 280d, 300d, 320d };
            var request = new CreateManualInputDataRequest
            {
                ExactData = values.Select((value, i) => new ExactObservationDto { Index = 2000 + i, Value = value }).ToList(),
                LowOutlierThreshold = threshold
            };
            if (preflagFirstAs.HasValue) request.ExactData[0].IsLowOutlier = preflagFirstAs.Value;
            return request;
        }

        /// <summary>Values strictly below the threshold are flagged and counted; values at/above are not.</summary>
        [TestMethod]
        public void BelowThreshold_AreFlaggedAndCountedInResponse()
        {
            // Preflag the first observation (value 0) in agreement with the threshold: the ruling
            // only rejects a preflag that CONTRADICTS the threshold, not one that agrees with it.
            var resource = _inputs.CreateManual(Request(110d, preflagFirstAs: true));
            var frame = resource.DataFrame;

            Assert.AreEqual(4, frame.NumberOfLowOutliers, "Values 0, 0, 100, 100 are below the threshold of 110.");
            var flags = frame.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray();
            CollectionAssert.AreEqual(
                new[] { true, true, true, true, false, false, false, false, false, false, false, false, false, false, false },
                flags);

            var summary = InputDataMapper.ToSummary(resource);
            Assert.AreEqual(4, summary.LowOutlierCount);
            Assert.AreEqual(110d, summary.LowOutlierThreshold);
        }

        /// <summary>A threshold above the sorted upper-middle value censors more than half the record and is rejected.</summary>
        [TestMethod]
        public void AboveFiftyPercentLimit_ThrowsWithDataFrameMessage_AndStoresNothing()
        {
            // UpperMiddleValue of the fixture is 180; 181 is the smallest threshold that trips the guard.
            var ex = Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(Request(181d)));
            StringAssert.Contains(ex.Message, "cannot censor more than 50%");
            StringAssert.Contains(ex.Message, "180");
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>Fewer than ten exact observations cannot support threshold evaluation.</summary>
        [TestMethod]
        public void FewerThanTenExactValues_WithThreshold_Throws_AndStoresNothing()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 10 },
                    new() { Index = 2001, Value = 20 },
                    new() { Index = 2002, Value = 30 },
                    new() { Index = 2003, Value = 40 },
                    new() { Index = 2004, Value = 50 }
                },
                LowOutlierThreshold = 25
            };
            var ex = Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(request));
            StringAssert.Contains(ex.Message, "at least 10 items");
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>
        /// Omitting the threshold while an exact observation is preflagged is rejected: Haden
        /// Smith's 26 September 2026 decision (Task 3.20 / finding L29) requires
        /// <see cref="CreateManualInputDataRequest.LowOutlierThreshold"/> whenever any observation
        /// is preflagged, so the low-outlier state can never be stored with the flags set, the
        /// count at zero, and no censoring threshold to define them. Renamed from
        /// <c>OmittingThreshold_LeavesPreflagsUnchanged</c>, which encoded the old (now rejected)
        /// contract this decision replaces.
        /// </summary>
        [TestMethod]
        public void OmittingThreshold_WithPreflag_Throws_AndStoresNothing()
        {
            var request = Request(null, preflagFirstAs: true);
            var ex = Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(request));
            // ArgumentException.Message appends "(Parameter 'request')" to the constructor's
            // message argument, so Contains (not AreEqual) checks the verbatim text supplied.
            StringAssert.Contains(ex.Message,
                "Exact observations flagged isLowOutlier=true require lowOutlierThreshold, which defines their censoring threshold; supply lowOutlierThreshold or remove the flags (useMultipleGrubbsBeckTest derives the flags itself).");
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>Omitting the threshold when no observation is preflagged is unaffected by the new rejection rule.</summary>
        [TestMethod]
        public void OmittingThreshold_NoPreflags_Succeeds()
        {
            var request = Request(null);
            var frame = _inputs.CreateManual(request).DataFrame;

            Assert.AreEqual(0d, frame.LowOutlierThreshold, "No threshold was supplied; the frame keeps its unset default.");
            Assert.IsFalse(((ExactData)frame.ExactSeries[0]).IsLowOutlier);
            Assert.IsFalse(((ExactData)frame.ExactSeries[1]).IsLowOutlier);
        }

        /// <summary>A preflagged observation whose value the threshold would NOT flag (it is above the threshold) is contradictory.</summary>
        [TestMethod]
        public void PreflaggedValueAboveThreshold_ThrowsBeforeAnythingIsBuilt_AndStoresNothing()
        {
            var request = Request(50d);
            request.ExactData[2].IsLowOutlier = true; // value 100, threshold 50: contradictory (100 >= 50).
            var ex = Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(request));
            StringAssert.Contains(ex.Message, "100");
            StringAssert.Contains(ex.Message, "50");
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>
        /// A preflagged observation whose value equals the threshold is also contradictory: the
        /// threshold only flags values strictly below it, so an equal value would be unflagged.
        /// </summary>
        [TestMethod]
        public void PreflaggedValueEqualToThreshold_Throws_AndStoresNothing()
        {
            var request = Request(100d);
            request.ExactData[2].IsLowOutlier = true; // value 100 == threshold 100: not strictly below.
            Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(request));
            Assert.AreEqual(0, _store.TotalCount);
        }
    }
}
