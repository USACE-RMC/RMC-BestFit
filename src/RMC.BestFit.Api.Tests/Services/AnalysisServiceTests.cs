using Microsoft.Extensions.Options;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisService"/>: creation for all three kinds with input
    /// cloning, kind-guarded lookups, run preconditions (validation failure, run-lock conflict),
    /// and results-before-run semantics. No estimators are run.
    /// </summary>
    [TestClass]
    public class AnalysisServiceTests
    {
        /// <summary>
        /// The store backing the service under test.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The service under test.
        /// </summary>
        private AnalysisService _service = null!;

        /// <summary>
        /// Creates a fresh store and service before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _service = new AnalysisService(_store, Options.Create(new ApiOptions()));
        }

        /// <summary>
        /// Adds a valid input-data resource to the store.
        /// </summary>
        /// <returns>The stored resource.</returns>
        private InputDataResource AddInputData()
        {
            return _store.AddInputData(TestAnalyses.CreateInputDataResource());
        }

        /// <summary>
        /// Adds a stage/discharge time-series pair to the store.
        /// </summary>
        /// <returns>The stored stage and discharge resources.</returns>
        private (TimeSeriesResource Stage, TimeSeriesResource Discharge) AddStageDischargePair()
        {
            var (stage, discharge) = TestAnalyses.CreateStageDischargePair();
            var stageResource = _store.AddTimeSeries(new TimeSeriesResource(stage) { Name = "stage", Source = TimeSeriesSource.Manual });
            var dischargeResource = _store.AddTimeSeries(new TimeSeriesResource(discharge) { Name = "discharge", Source = TimeSeriesSource.Manual });
            return (stageResource, dischargeResource);
        }

        /// <summary>
        /// Verifies univariate creation stores the resource with provenance, defaulted name, and
        /// applied ordinates/options.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_StoresConfiguredResource()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.Id,
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue,
                ProbabilityOrdinates = new List<double> { 0.5, 0.1, 0.01 },
                BayesianOptions = new BayesianOptionsDto { PrngSeed = 42 }
            });

            Assert.AreEqual(AnalysisKind.Univariate, resource.Kind);
            Assert.AreEqual(input.Id, resource.InputDataId);
            StringAssert.Contains(resource.Name, "Generalized Extreme Value");
            Assert.AreEqual(3, resource.Univariate!.ProbabilityOrdinates.Count);
            Assert.AreEqual(42, resource.Univariate.BayesianAnalysis.PRNGSeed);
            Assert.AreSame(resource, _store.GetAnalysis(resource.Id));
        }

        /// <summary>
        /// Verifies the analysis owns a CLONE of the input data: mutating the source data frame
        /// afterwards does not change the analysis, and the clone preserves lambda and plotting settings.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_ClonesInputData()
        {
            var input = AddInputData();
            input.DataFrame.SetLambda(0.85);
            input.DataFrame.PlottingParameter = 0.44;

            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var analysisFrame = resource.Univariate!.UnivariateDistribution.DataFrame;

            Assert.AreNotSame(input.DataFrame, analysisFrame);
            Assert.AreEqual(0.85, analysisFrame.Lambda, 1e-12, "Lambda must survive the clone round-trip.");
            Assert.AreEqual(0.44, analysisFrame.PlottingParameter, 1e-12, "PlottingParameter must survive the clone round-trip.");

            int countBefore = analysisFrame.ExactSeries.Count;
            input.DataFrame.ExactSeries.Add(new ExactData(2099, 9999d));
            Assert.AreEqual(countBefore, analysisFrame.ExactSeries.Count, "Mutating the source must not affect the analysis clone.");
        }

        /// <summary>
        /// Verifies invalid probability ordinates are rejected.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_InvalidOrdinates_Throws()
        {
            var input = AddInputData();
            Assert.ThrowsException<ArgumentException>(() => _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.Id,
                ProbabilityOrdinates = new List<double> { 0.5, 1.5 }
            }));
        }

        /// <summary>
        /// Verifies an unknown input-data id fails with not-found semantics.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_UnknownInputData_ThrowsNotFound()
        {
            Assert.ThrowsException<ResourceNotFoundException>(() =>
                _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = Guid.NewGuid() }));
        }

        /// <summary>
        /// Verifies Bulletin 17C creation applies the uncertainty method and rejects unsupported
        /// distributions.
        /// </summary>
        [TestMethod]
        public void CreateBulletin17C_AppliesMethod_AndGuardsDistribution()
        {
            var input = AddInputData();
            var resource = _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest
            {
                InputDataId = input.Id,
                UncertaintyMethod = RMC.BestFit.Analyses.UncertaintyMethod.Bootstrap
            });
            Assert.AreEqual(AnalysisKind.Bulletin17C, resource.Kind);
            Assert.AreEqual(RMC.BestFit.Analyses.UncertaintyMethod.Bootstrap, resource.Bulletin17C!.UncertaintyMethod);

            var ex = Assert.ThrowsException<ArgumentException>(() => _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest
            {
                InputDataId = input.Id,
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue
            }));
            StringAssert.Contains(ex.Message, "not supported");
        }

        /// <summary>
        /// Verifies rating curve creation clones both series and applies the stage grid options.
        /// </summary>
        [TestMethod]
        public void CreateRatingCurve_ClonesSeries_AndAppliesGrid()
        {
            var (stage, discharge) = AddStageDischargePair();
            var resource = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id,
                NumberOfSegments = 1,
                MinStage = 2d,
                MaxStage = 9d,
                StageBins = 50
            });

            Assert.AreEqual(AnalysisKind.RatingCurve, resource.Kind);
            Assert.AreEqual(stage.Id, resource.StageTimeSeriesId);
            Assert.AreEqual(discharge.Id, resource.DischargeTimeSeriesId);
            Assert.IsFalse(resource.RatingCurve!.UseDefaultStageBins);
            Assert.AreEqual(2d, resource.RatingCurve.MinStage);
            Assert.AreEqual(9d, resource.RatingCurve.MaxStage);
            Assert.AreEqual(50, resource.RatingCurve.StageBins);

            // Clone independence: adding to the source series does not change the model's data.
            int alignedBefore = resource.RatingCurve.RatingCurve.GetAlignedObservations().Count;
            stage.TimeSeries.Add(new Numerics.Data.SeriesOrdinate<DateTime, double>(new DateTime(2030, 1, 1), 99d));
            discharge.TimeSeries.Add(new Numerics.Data.SeriesOrdinate<DateTime, double>(new DateTime(2030, 1, 1), 999d));
            Assert.AreEqual(alignedBefore, resource.RatingCurve.RatingCurve.GetAlignedObservations().Count);
        }

        /// <summary>
        /// Verifies inconsistent stage-grid options are rejected.
        /// </summary>
        [TestMethod]
        public void CreateRatingCurve_InconsistentGrid_Throws()
        {
            var (stage, discharge) = AddStageDischargePair();
            Assert.ThrowsException<ArgumentException>(() => _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id,
                MinStage = 2d
            }));
            Assert.ThrowsException<ArgumentException>(() => _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id,
                MinStage = 9d,
                MaxStage = 2d
            }));
        }

        /// <summary>
        /// Verifies kind-guarded lookups: an id of one kind is not found through another kind's guard.
        /// </summary>
        [TestMethod]
        public void Get_KindMismatch_ThrowsNotFound()
        {
            var input = AddInputData();
            var univariate = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            Assert.AreSame(univariate, _service.Get(univariate.Id, AnalysisKind.Univariate));
            Assert.AreSame(univariate, _service.Get(univariate.Id));
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Get(univariate.Id, AnalysisKind.RatingCurve));
        }

        /// <summary>
        /// Verifies list filtering by kind.
        /// </summary>
        [TestMethod]
        public void List_FiltersByKind()
        {
            var input = AddInputData();
            _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest { InputDataId = input.Id });

            Assert.AreEqual(2, _service.List().Count);
            Assert.AreEqual(1, _service.List(AnalysisKind.Univariate).Count);
            Assert.AreEqual(1, _service.List(AnalysisKind.Bulletin17C).Count);
            Assert.AreEqual(0, _service.List(AnalysisKind.RatingCurve).Count);
        }

        /// <summary>
        /// Verifies the validate endpoint surface reports validity without running.
        /// </summary>
        [TestMethod]
        public void Validate_ReportsVerdict()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var response = _service.Validate(resource.Id, AnalysisKind.Univariate);
            Assert.IsTrue(response.IsValid);
            Assert.AreEqual(0, response.Errors.Count);
        }

        /// <summary>
        /// Verifies an invalid configuration fails the run with the validation messages BEFORE any
        /// estimation starts. The configuration is corrupted directly on the model (decreasing
        /// ordinates) because the service's own creation path sorts client ordinates.
        /// </summary>
        [TestMethod]
        public async Task Run_InvalidConfiguration_ThrowsRequestValidation()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            resource.Univariate!.ProbabilityOrdinates.Clear();
            resource.Univariate.ProbabilityOrdinates.AddRange(new[] { 0.5, 0.1 });

            var ex = await Assert.ThrowsExceptionAsync<RequestValidationException>(
                () => _service.RunFrequencyAsync(resource.Id, AnalysisKind.Univariate));
            Assert.IsTrue(ex.Errors.Count > 0);
            Assert.AreEqual(AnalysisRunState.Created, resource.State, "Validation failures must not mark the analysis as run.");
        }

        /// <summary>
        /// Verifies client-supplied ordinates are de-duplicated and sorted ascending (the model
        /// requires strictly increasing exceedance probabilities).
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_SortsAndDeduplicatesOrdinates()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.Id,
                ProbabilityOrdinates = new List<double> { 0.5, 0.01, 0.1, 0.5 }
            });
            CollectionAssert.AreEqual(new List<double> { 0.01, 0.1, 0.5 }, resource.Univariate!.ProbabilityOrdinates.ToList());
            Assert.IsTrue(AnalysisRunHelper.Validate(resource).IsValid);
        }

        /// <summary>
        /// Verifies a second run request while the run lock is held is rejected with conflict
        /// semantics (HTTP 409) without starting an estimation.
        /// </summary>
        [TestMethod]
        public async Task Run_WhileLocked_ThrowsConflict()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            Assert.IsTrue(resource.RunLock.Wait(0), "Test setup: acquire the run lock.");
            try
            {
                await Assert.ThrowsExceptionAsync<ResourceConflictException>(
                    () => _service.RunFrequencyAsync(resource.Id, AnalysisKind.Univariate));
            }
            finally
            {
                resource.RunLock.Release();
            }
        }

        /// <summary>
        /// Verifies a frequency run request against a rating curve id fails with not-found semantics.
        /// </summary>
        [TestMethod]
        public async Task RunFrequency_OnRatingCurve_ThrowsNotFound()
        {
            var (stage, discharge) = AddStageDischargePair();
            var resource = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            });
            await Assert.ThrowsExceptionAsync<ResourceNotFoundException>(
                () => _service.RunFrequencyAsync(resource.Id, expectedKind: null));
        }

        /// <summary>
        /// Verifies results retrieval before any run reports not-found with run guidance.
        /// </summary>
        [TestMethod]
        public void GetResults_BeforeRun_ThrowsNotFound()
        {
            var input = AddInputData();
            var univariate = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var ex = Assert.ThrowsException<ResourceNotFoundException>(
                () => _service.GetFrequencyResults(univariate.Id, AnalysisKind.Univariate));
            StringAssert.Contains(ex.Message, "Run it first");

            var (stage, discharge) = AddStageDischargePair();
            var rating = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            });
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.GetRatingCurveResults(rating.Id));
        }

        /// <summary>
        /// Verifies delete removes the resource, honors the kind guard, and refuses while running.
        /// </summary>
        [TestMethod]
        public void Delete_HonorsKindGuard_AndRunLock()
        {
            var input = AddInputData();
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Delete(resource.Id, AnalysisKind.Bulletin17C));

            Assert.IsTrue(resource.RunLock.Wait(0));
            try
            {
                Assert.ThrowsException<ResourceConflictException>(() => _service.Delete(resource.Id, AnalysisKind.Univariate));
            }
            finally
            {
                resource.RunLock.Release();
            }

            _service.Delete(resource.Id, AnalysisKind.Univariate);
            Assert.IsNull(_store.GetAnalysis(resource.Id));
        }

        /// <summary>
        /// Verifies univariate creation applies informative parameter priors (switching off the
        /// default flat priors) and quantile priors onto the cloned model.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_AppliesPriors()
        {
            var input = AddInputData();
            var probe = new UnivariateDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
            string skewName = probe.Parameters[2].DisplayName;

            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.Id,
                Distribution = UnivariateDistributionType.LogPearsonTypeIII,
                ParameterPriors = new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = skewName,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Normal,
                            Parameters = new List<double> { -0.2, 0.3 }
                        }
                    }
                },
                QuantilePriors = new List<QuantilePriorDto>
                {
                    new()
                    {
                        Alpha = 0.01,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.LogNormal,
                            Parameters = new List<double> { 4.7, 0.1 }
                        }
                    }
                },
                UseSingleQuantile = true
            });

            var model = resource.Univariate!.UnivariateDistribution;
            Assert.IsFalse(model.UseDefaultFlatPriors);
            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[2].PriorDistribution.Type);
            Assert.IsTrue(model.EnableQuantilePriors);
            Assert.IsTrue(model.UseSingleQuantile);
            Assert.AreEqual(1, model.QuantilePriors.Count);
            Assert.AreEqual(0.01, model.QuantilePriors[0].Alpha);
        }

        /// <summary>
        /// Verifies an unknown prior parameter name is rejected with the valid names listed.
        /// </summary>
        [TestMethod]
        public void CreateUnivariate_UnknownPriorParameter_Throws()
        {
            var input = AddInputData();
            var ex = Assert.ThrowsException<ArgumentException>(() => _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.Id,
                ParameterPriors = new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = "No Such Parameter",
                        Distribution = new DistributionSpecDto { Type = UnivariateDistributionType.Normal, Parameters = new List<double> { 0d, 1d } }
                    }
                }
            }));
            StringAssert.Contains(ex.Message, "Valid names");
        }

        /// <summary>
        /// Verifies Bulletin 17C creation fills the named parameter penalty entry in place and
        /// replaces the quantile penalty contents with enabled entries.
        /// </summary>
        [TestMethod]
        public void CreateBulletin17C_AppliesPenalties()
        {
            var input = AddInputData();
            var probe = new Bulletin17CDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
            string skewName = probe.ParameterPenalties[2].Name;

            var resource = _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest
            {
                InputDataId = input.Id,
                ParameterPenalties = new List<ParameterPenaltyDto>
                {
                    new() { ParameterName = skewName, Mean = -0.05, Mse = 0.12 }
                },
                QuantilePenalties = new List<QuantilePenaltyDto>
                {
                    new() { Aep = 0.002, Mean = 4.85, Mse = 0.02 }
                }
            });

            var distribution = resource.Bulletin17C!.Bulletin17CDistribution;
            Assert.IsTrue(distribution.ParameterPenalties[2].Enabled);
            Assert.AreEqual(-0.05, distribution.ParameterPenalties[2].Mean);
            Assert.AreEqual(0.12, distribution.ParameterPenalties[2].MSE);
            Assert.AreEqual(1, distribution.QuantilePenalties.Count);
            Assert.IsTrue(distribution.QuantilePenalties[0].Enabled);
            Assert.AreEqual(0.002, distribution.QuantilePenalties[0].AEP);
            Assert.AreEqual(0, resource.CreationWarnings.Count, "No uncertain data means no creation warnings.");
        }

        /// <summary>
        /// Verifies creating a Bulletin 17C analysis over input data containing uncertain
        /// observations records a warning (the Expected Moments Algorithm ignores them) that the
        /// validate response surfaces.
        /// </summary>
        [TestMethod]
        public void CreateBulletin17C_UncertainData_RecordsWarning()
        {
            var input = _store.AddInputData(new InputDataResource
            {
                Name = "with uncertain",
                DataFrame = TestAnalyses.CreateDataFrameWithUncertain(),
                Method = InputDataMethod.Manual
            });

            var resource = _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest { InputDataId = input.Id });

            Assert.AreEqual(1, resource.CreationWarnings.Count);
            StringAssert.Contains(resource.CreationWarnings[0], "uncertain observation");
            StringAssert.Contains(resource.CreationWarnings[0], "ignored");

            var validation = _service.Validate(resource.Id, AnalysisKind.Bulletin17C);
            Assert.AreEqual(1, validation.Warnings.Count);
            StringAssert.Contains(validation.Warnings[0], "uncertain observation");
        }

        /// <summary>
        /// Verifies rating curve creation applies informative parameter priors onto the model.
        /// </summary>
        [TestMethod]
        public void CreateRatingCurve_AppliesParameterPriors()
        {
            var (stage, discharge) = AddStageDischargePair();
            var (probeStage, probeDischarge) = TestAnalyses.CreateStageDischargePair();
            var probe = new RatingCurve(probeStage, probeDischarge, numberOfSegments: 1);
            string offsetName = probe.Parameters[0].DisplayName;

            var resource = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id,
                ParameterPriors = new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = offsetName,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Normal,
                            Parameters = new List<double> { 1d, 0.5 }
                        }
                    }
                }
            });

            var model = resource.RatingCurve!.RatingCurve;
            Assert.IsFalse(model.UseDefaultFlatPriors);
            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[0].PriorDistribution.Type);
        }

        /// <summary>
        /// Verifies mixture creation configures components, zero inflation, and priors on the
        /// cloned model, and rejects unsupported or over-long component lists.
        /// </summary>
        [TestMethod]
        public void CreateMixture_ConfiguresModel_AndValidatesComponents()
        {
            var input = AddInputData();
            var resource = _service.CreateMixture(new CreateMixtureAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Gumbel,
                    UnivariateDistributionType.LogNormal
                },
                IsZeroInflated = true,
                BayesianOptions = new BayesianOptionsDto { PrngSeed = 7 }
            });

            Assert.AreEqual(AnalysisKind.Mixture, resource.Kind);
            Assert.AreEqual(input.Id, resource.InputDataId);
            var model = resource.Mixture!.MixtureDistribution;
            Assert.IsTrue(model.IsZeroInflated);
            Assert.AreEqual(2, model.Mixture!.Distributions.Length);
            Assert.AreNotSame(input.DataFrame, model.DataFrame);
            Assert.AreEqual(7, resource.Mixture.BayesianAnalysis.PRNGSeed);

            Assert.ThrowsException<ArgumentException>(() => _service.CreateMixture(new CreateMixtureAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = Enumerable.Repeat(UnivariateDistributionType.Gumbel, 4).ToList()
            }));
            Assert.ThrowsException<ArgumentException>(() => _service.CreateMixture(new CreateMixtureAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType>()
            }));
        }

        /// <summary>
        /// Verifies point process creation seeds the threshold from a POT resource, honors
        /// explicit threshold/record-span overrides (switching defaults off and recomputing λ),
        /// and rejects seasonal options without the seasonal flag.
        /// </summary>
        [TestMethod]
        public void CreatePointProcess_SeedsThreshold_AndHonorsOverrides()
        {
            var input = _store.AddInputData(new InputDataResource
            {
                Name = "pot",
                DataFrame = TestAnalyses.CreatePotDataFrame(),
                Method = InputDataMethod.PeaksOverThreshold,
                Threshold = 400d
            });

            var seeded = _service.CreatePointProcess(new CreatePointProcessAnalysisRequest { InputDataId = input.Id });
            var seededModel = seeded.PointProcess!.PointProcess;
            Assert.IsFalse(double.IsNaN(seededModel.Threshold), "The POT resource's threshold must seed the model.");

            var overridden = _service.CreatePointProcess(new CreatePointProcessAnalysisRequest
            {
                InputDataId = input.Id,
                Threshold = 425d,
                TotalYears = 12.5
            });
            var overriddenModel = overridden.PointProcess!.PointProcess;
            Assert.IsFalse(overriddenModel.UseDefaults, "Explicit overrides must switch off the model defaults.");
            Assert.AreEqual(425d, overriddenModel.Threshold);
            Assert.AreEqual(12.5, overriddenModel.TotalYears);
            Assert.AreEqual(overriddenModel.DataFrame.ExactSeries.Count / 12.5, overriddenModel.Lambda, 1e-9,
                "λ must be recomputed from the explicit record span.");

            Assert.ThrowsException<ArgumentException>(() => _service.CreatePointProcess(new CreatePointProcessAnalysisRequest
            {
                InputDataId = input.Id,
                TimeBlock = Numerics.Data.TimeBlockWindow.WaterYear
            }));
        }

        /// <summary>
        /// Verifies competing risks creation configures components and rejects invalid lists.
        /// </summary>
        [TestMethod]
        public void CreateCompetingRisks_ConfiguresModel()
        {
            var input = AddInputData();
            var resource = _service.CreateCompetingRisks(new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Gumbel,
                    UnivariateDistributionType.GeneralizedExtremeValue
                }
            });

            Assert.AreEqual(AnalysisKind.CompetingRisks, resource.Kind);
            var model = resource.CompetingRisks!.CompetingRisksDistribution;
            Assert.AreEqual(2, model.CompetingRisks!.Distributions.Count);
            Assert.AreNotSame(input.DataFrame, model.DataFrame);

            Assert.ThrowsException<ArgumentException>(() => _service.CreateCompetingRisks(new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = Enumerable.Repeat(UnivariateDistributionType.Gumbel, 4).ToList()
            }));
        }

        /// <summary>
        /// Adds two unrun univariate analyses to the store and creates a composite over them.
        /// </summary>
        /// <param name="compositeType">The composition method.</param>
        /// <returns>The composite resource and its two component resources.</returns>
        private (AnalysisResource Composite, AnalysisResource ComponentA, AnalysisResource ComponentB) CreateStoredComposite(
            RMC.BestFit.Analyses.CompositeType compositeType = RMC.BestFit.Analyses.CompositeType.CompetingRisks)
        {
            var input = AddInputData();
            var componentA = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id, Distribution = UnivariateDistributionType.Gumbel });
            var componentB = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id, Distribution = UnivariateDistributionType.LogNormal });
            var composite = _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = componentA.Id, Weight = compositeType == RMC.BestFit.Analyses.CompositeType.Mixture ? 0.6 : null },
                    new() { AnalysisId = componentB.Id, Weight = compositeType == RMC.BestFit.Analyses.CompositeType.Mixture ? 0.4 : null }
                },
                CompositeType = compositeType
            });
            return (composite, componentA, componentB);
        }

        /// <summary>
        /// Verifies composite creation stores live component references, provenance ids, and the
        /// composition settings, and applies the presentation options.
        /// </summary>
        [TestMethod]
        public void CreateComposite_StoresLiveReferences()
        {
            var (composite, componentA, componentB) = CreateStoredComposite();

            Assert.AreEqual(AnalysisKind.Composite, composite.Kind);
            Assert.IsNotNull(composite.ComponentResources);
            Assert.AreSame(componentA, composite.ComponentResources[0], "Components must be LIVE references, not clones.");
            Assert.AreSame(componentB, composite.ComponentResources[1]);
            CollectionAssert.AreEqual(new List<Guid> { componentA.Id, componentB.Id }, composite.ComponentAnalysisIds);
            Assert.AreEqual(2, composite.Composite!.Analyses.Count);
            Assert.AreSame(componentA.Univariate, composite.Composite.Analyses[0].UnivariateAnalysis);
        }

        /// <summary>
        /// Verifies composite creation rejects unknown components, non-univariate component
        /// kinds, nested composites, and invalid mixture weights.
        /// </summary>
        [TestMethod]
        public void CreateComposite_Validation()
        {
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = Guid.NewGuid() } }
            }));

            var (stage, discharge) = AddStageDischargePair();
            var rating = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            });
            var badKind = Assert.ThrowsException<ArgumentException>(() => _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = rating.Id } }
            }));
            StringAssert.Contains(badKind.Message, "cannot be a composite component");

            // A nested composite is rejected by the API-level kind guard (the model's
            // WeightedUnivariateAnalysis setter would also reject it one layer deeper).
            var (composite, _, _) = CreateStoredComposite();
            var nested = Assert.ThrowsException<ArgumentException>(() => _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = composite.Id } }
            }));
            StringAssert.Contains(nested.Message, "cannot be a composite component");

            var input = AddInputData();
            var component = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var badWeights = Assert.ThrowsException<ArgumentException>(() => _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = component.Id, Weight = 0.7 },
                    new() { AnalysisId = component.Id, Weight = 0.7 }
                },
                CompositeType = RMC.BestFit.Analyses.CompositeType.Mixture
            }));
            StringAssert.Contains(badWeights.Message, "sum");

            var missingWeight = Assert.ThrowsException<ArgumentException>(() => _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = component.Id } },
                CompositeType = RMC.BestFit.Analyses.CompositeType.Mixture
            }));
            StringAssert.Contains(missingWeight.Message, "weight");
        }

        /// <summary>
        /// Verifies the component-lock guard: a composite run while a component's run lock is
        /// held reports 409 naming the busy component — deterministically before the
        /// children-unestimated validation error.
        /// </summary>
        [TestMethod]
        public async Task RunComposite_ComponentRunning_Conflicts409()
        {
            var (composite, componentA, _) = CreateStoredComposite();

            Assert.IsTrue(componentA.RunLock.Wait(0));
            try
            {
                var ex = await Assert.ThrowsExceptionAsync<ResourceConflictException>(
                    () => _service.RunFrequencyAsync(composite.Id, AnalysisKind.Composite));
                StringAssert.Contains(ex.Message, componentA.Id.ToString());
                Assert.AreEqual(1, composite.RunLock.CurrentCount, "The composite's own lock must be released after the conflict.");
            }
            finally
            {
                componentA.RunLock.Release();
            }

            // With all locks free, the run proceeds to validation and fails there instead —
            // the components have not been estimated. Both component locks must be released.
            await Assert.ThrowsExceptionAsync<RequestValidationException>(
                () => _service.RunFrequencyAsync(composite.Id, AnalysisKind.Composite));
            Assert.AreEqual(1, componentA.RunLock.CurrentCount, "Component locks must be released after a failed run.");
        }

        /// <summary>
        /// Verifies a composite listing the same component twice does not deadlock on its own
        /// component lock (the lock set is de-duplicated by resource id).
        /// </summary>
        [TestMethod]
        public async Task RunComposite_DuplicateComponent_NoSelfConflict()
        {
            var input = AddInputData();
            var component = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var composite = _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = component.Id, Weight = 0.4 },
                    new() { AnalysisId = component.Id, Weight = 0.4 }
                },
                CompositeType = RMC.BestFit.Analyses.CompositeType.Mixture
            });

            // Must fail on validation (component unestimated), NOT on a self-inflicted 409.
            await Assert.ThrowsExceptionAsync<RequestValidationException>(
                () => _service.RunFrequencyAsync(composite.Id, AnalysisKind.Composite));
            Assert.AreEqual(1, component.RunLock.CurrentCount);
        }

        /// <summary>
        /// Verifies deleting a component from the store leaves the composite readable and its
        /// live reference intact.
        /// </summary>
        [TestMethod]
        public void DeleteComponent_CompositeSurvives()
        {
            var (composite, componentA, _) = CreateStoredComposite();

            _service.Delete(componentA.Id);

            Assert.IsNull(_store.GetAnalysis(componentA.Id));
            var summary = AnalysisMapper.ToSummary(_service.Get(composite.Id, AnalysisKind.Composite));
            Assert.IsNotNull(summary.ComponentAnalysisIds);
            Assert.AreSame(componentA, composite.ComponentResources![0], "The live reference must survive store deletion.");
        }

        /// <summary>
        /// Verifies distribution-fitting creation clones the input, honors a candidate subset,
        /// and rejects unsupported types; results before any run are not found.
        /// </summary>
        [TestMethod]
        public void CreateDistributionFitting_SubsetAndValidation()
        {
            var input = AddInputData();
            var all = _service.CreateDistributionFitting(new CreateDistributionFittingAnalysisRequest { InputDataId = input.Id });
            Assert.AreEqual(15, all.DistributionFitting!.DistributionList.Count, "The default candidate set is all 15 distributions.");

            var subset = _service.CreateDistributionFitting(new CreateDistributionFittingAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.LogPearsonTypeIII,
                    UnivariateDistributionType.GeneralizedExtremeValue
                }
            });
            Assert.AreEqual(2, subset.DistributionFitting!.DistributionList.Count);
            Assert.AreNotSame(input.DataFrame, subset.DistributionFitting.DataFrame);

            Assert.ThrowsException<ArgumentException>(() => _service.CreateDistributionFitting(new CreateDistributionFittingAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Cauchy }
            }));

            Assert.ThrowsException<ResourceNotFoundException>(() => _service.GetDistributionFittingResults(subset.Id));
        }

        /// <summary>
        /// Adds two unrun univariate marginals to the store and creates a bivariate over them.
        /// </summary>
        /// <returns>The bivariate resource and its two marginal resources.</returns>
        private (AnalysisResource Bivariate, AnalysisResource MarginalX, AnalysisResource MarginalY) CreateStoredBivariate()
        {
            var input = AddInputData();
            var marginalX = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id, Distribution = UnivariateDistributionType.Gumbel });
            var marginalY = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id, Distribution = UnivariateDistributionType.LogNormal });
            var bivariate = _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id,
                XyOrdinates = new List<XyOrdinateDto>
                {
                    new() { X = 400d, Y = 430d },
                    new() { X = 200d, Y = 220d }
                }
            });
            return (bivariate, marginalX, marginalY);
        }

        /// <summary>
        /// Verifies bivariate creation stores live marginal references, sorts the XY grid by x,
        /// and the API-level validation reports unestimated marginals without running.
        /// </summary>
        [TestMethod]
        public void CreateBivariate_StoresLiveReferences_AndSortsGrid()
        {
            var (bivariate, marginalX, marginalY) = CreateStoredBivariate();

            Assert.AreEqual(AnalysisKind.Bivariate, bivariate.Kind);
            Assert.AreSame(marginalX, bivariate.MarginalXResource);
            Assert.AreSame(marginalY, bivariate.MarginalYResource);
            Assert.AreSame(marginalX.Univariate!.UnivariateDistribution, bivariate.Bivariate!.BivariateDistribution.MarginalX,
                "The bivariate must reference the LIVE marginal model, not a clone.");
            Assert.AreEqual(2, bivariate.Bivariate.XYOrdinates.Count);
            Assert.AreEqual(200d, bivariate.Bivariate.XYOrdinates[0].X, "The XY grid must be sorted by x.");

            var validation = _service.Validate(bivariate.Id, AnalysisKind.Bivariate);
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(validation.Errors.Any(e => e.Contains("marginal X")), "Validation must name the unestimated marginal.");
        }

        /// <summary>
        /// Verifies bivariate creation rejections: identical marginals, invalid marginal kinds,
        /// the unsupported full-likelihood method, and insufficient data overlap.
        /// </summary>
        [TestMethod]
        public void CreateBivariate_Validation()
        {
            var input = AddInputData();
            var marginal = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            var same = Assert.ThrowsException<ArgumentException>(() => _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginal.Id,
                MarginalYAnalysisId = marginal.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 1d } }
            }));
            StringAssert.Contains(same.Message, "distinct");

            var competing = _service.CreateCompetingRisks(new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel, UnivariateDistributionType.LogNormal }
            });
            var badKind = Assert.ThrowsException<ArgumentException>(() => _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginal.Id,
                MarginalYAnalysisId = competing.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 1d } }
            }));
            StringAssert.Contains(badKind.Message, "cannot be the marginal Y");

            var other = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var badMethod = Assert.ThrowsException<ArgumentException>(() => _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginal.Id,
                MarginalYAnalysisId = other.Id,
                EstimationMethod = Numerics.Distributions.Copulas.CopulaEstimationMethod.FullLikelihood,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 1d } }
            }));
            StringAssert.Contains(badMethod.Message, "fullLikelihood");

            // A marginal over a frame sharing only 5 time indexes with the other fails the
            // overlap requirement.
            var shortFrame = new DataFrame();
            shortFrame.ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < 5; i++)
            {
                shortFrame.ExactSeries.Add(new ExactData(2000 + i, 150d + 10d * i));
            }
            for (int i = 0; i < 10; i++)
            {
                shortFrame.ExactSeries.Add(new ExactData(1900 + i, 150d + 10d * i));
            }
            shortFrame.ExactSeries.SuppressCollectionChanged = false;
            shortFrame.ExactSeries.RaiseCollectionChangedReset();
            var shortInput = _store.AddInputData(new InputDataResource { Name = "short", DataFrame = shortFrame, Method = InputDataMethod.Manual });
            var shortMarginal = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = shortInput.Id });
            var overlap = Assert.ThrowsException<ArgumentException>(() => _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginal.Id,
                MarginalYAnalysisId = shortMarginal.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 1d } }
            }));
            StringAssert.Contains(overlap.Message, "overlapping");
        }

        /// <summary>
        /// Verifies a bivariate run reports 409 while a marginal's run lock is held, releasing
        /// every acquired lock.
        /// </summary>
        [TestMethod]
        public async Task RunBivariate_MarginalRunning_Conflicts409()
        {
            var (bivariate, marginalX, _) = CreateStoredBivariate();

            Assert.IsTrue(marginalX.RunLock.Wait(0));
            try
            {
                var ex = await Assert.ThrowsExceptionAsync<ResourceConflictException>(
                    () => _service.RunBivariateAsync(bivariate.Id));
                StringAssert.Contains(ex.Message, marginalX.Id.ToString());
            }
            finally
            {
                marginalX.RunLock.Release();
            }

            // With locks free the run fails validation instead (marginals unestimated), and
            // every lock is back to available.
            await Assert.ThrowsExceptionAsync<RequestValidationException>(() => _service.RunBivariateAsync(bivariate.Id));
            Assert.AreEqual(1, marginalX.RunLock.CurrentCount);
            Assert.AreEqual(1, bivariate.RunLock.CurrentCount);
        }

        /// <summary>
        /// Verifies coincident frequency creation validates the surface shape and bins, stores
        /// transitive live references, and its runs conflict while a transitive marginal is busy.
        /// </summary>
        [TestMethod]
        public async Task CreateCoincidentFrequency_ShapeValidation_AndTransitiveLocks()
        {
            var (bivariate, marginalX, _) = CreateStoredBivariate();

            var valid = new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariate.Id,
                XValues = new List<double> { 100d, 200d, 300d },
                YValues = new List<double> { 50d, 100d, 150d },
                BivariateResponse = new List<List<double>>
                {
                    new() { 10d, 11d, 12d },
                    new() { 13d, 14d, 15d },
                    new() { 16d, 17d, 18d }
                }
            };
            var cfa = _service.CreateCoincidentFrequency(valid);
            Assert.AreEqual(AnalysisKind.CoincidentFrequency, cfa.Kind);
            Assert.AreSame(bivariate, cfa.BivariateResource);
            Assert.AreEqual(3, cfa.ComponentResources!.Count, "Locks must cover the bivariate plus its transitive marginals.");

            // The pre-run validation reports the unestimated bivariate without running.
            var validation = _service.Validate(cfa.Id, AnalysisKind.CoincidentFrequency);
            Assert.IsFalse(validation.IsValid);

            Assert.IsTrue(marginalX.RunLock.Wait(0));
            try
            {
                await Assert.ThrowsExceptionAsync<ResourceConflictException>(() => _service.RunCoincidentFrequencyAsync(cfa.Id));
            }
            finally
            {
                marginalX.RunLock.Release();
            }

            var ragged = Assert.ThrowsException<ArgumentException>(() => _service.CreateCoincidentFrequency(new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariate.Id,
                XValues = new List<double> { 100d, 200d },
                YValues = new List<double> { 50d, 100d },
                BivariateResponse = new List<List<double>> { new() { 10d, 11d }, new() { 13d } }
            }));
            StringAssert.Contains(ragged.Message, "bivariateResponse[1]");

            var wrongRows = Assert.ThrowsException<ArgumentException>(() => _service.CreateCoincidentFrequency(new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariate.Id,
                XValues = new List<double> { 100d, 200d, 300d },
                YValues = new List<double> { 50d, 100d },
                BivariateResponse = new List<List<double>> { new() { 10d, 11d }, new() { 13d, 14d } }
            }));
            StringAssert.Contains(wrongRows.Message, "one row per xValues");

            Assert.ThrowsException<ArgumentException>(() => _service.CreateCoincidentFrequency(new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariate.Id,
                XValues = valid.XValues,
                YValues = valid.YValues,
                BivariateResponse = valid.BivariateResponse,
                NumberOfBins = 4
            }));

            // A non-bivariate id 404s on the CFA create.
            var input = AddInputData();
            var univariate = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.CreateCoincidentFrequency(new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = univariate.Id,
                XValues = valid.XValues,
                YValues = valid.YValues,
                BivariateResponse = valid.BivariateResponse
            }));
        }

        /// <summary>
        /// Adds the synthetic daily series to the store as a time-series resource.
        /// </summary>
        /// <returns>The stored resource.</returns>
        private TimeSeriesResource AddDailySeries()
        {
            return _store.AddTimeSeries(new TimeSeriesResource(TestSeries.DailyThreeWaterYears())
            {
                Name = "daily",
                Source = TimeSeriesSource.Manual
            });
        }

        /// <summary>
        /// Verifies time-series creation for every model family: defaults, clone independence,
        /// explicit orders, training-window override, and the forecast horizon.
        /// </summary>
        [TestMethod]
        public void CreateTimeSeries_ConfiguresEachFamily()
        {
            var source = AddDailySeries();

            var ar = _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ar,
                ForecastingTimeSteps = 12
            });
            Assert.AreEqual(AnalysisKind.TimeSeries, ar.Kind);
            Assert.AreEqual(TimeSeriesModelType.Ar, ar.TimeSeriesModel);
            Assert.AreEqual(1, ar.Ar!.AutoRegressive.Order, "The AR order defaults to 1.");
            Assert.IsTrue(ar.Ar.AutoRegressive.IncludeIntercept, "The intercept defaults to on.");
            Assert.AreEqual(12, ar.Ar.ForecastingTimeSteps);
            Assert.AreNotSame(source.TimeSeries, ar.Ar.AutoRegressive.TimeSeries, "The series must be cloned.");
            Assert.AreEqual(source.Id, ar.TimeSeriesId);

            var ma = _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ma,
                Order = 2
            });
            Assert.AreEqual(2, ma.Ma!.MovingAverage.Order);

            var arima = _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arima,
                POrder = 2,
                DOrder = 1,
                QOrder = 1,
                TrainingTimeSteps = 900
            });
            Assert.AreEqual(2, arima.Arima!.ARIMA.POrder);
            Assert.AreEqual(1, arima.Arima.ARIMA.DOrder);
            Assert.AreEqual(1, arima.Arima.ARIMA.QOrder);
            Assert.IsFalse(arima.Arima.ARIMA.UseDefaultTrainingSteps, "An explicit training window must switch defaults off.");
            Assert.AreEqual(900, arima.Arima.ARIMA.TrainingTimeSteps);

            var covariate = AddDailySeries();
            var arimax = _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arimax,
                POrder = 1,
                XOrder = 1,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                CovariateTimeSeriesIds = new List<Guid> { covariate.Id },
                CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
            });
            Assert.AreEqual(TimeSeriesModelType.Arimax, arimax.TimeSeriesModel);
            Assert.AreEqual(1, arimax.Arimax!.ARIMAX.XOrderB);
            Assert.AreEqual(ARIMAX.Trend.Linear, arimax.Arimax.ARIMAX.TrendType);
            Assert.IsTrue(arimax.Arimax.ARIMAX.IncludeSeasonality);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, arimax.Arimax.ARIMAX.CovariateExtension);
            CollectionAssert.AreEqual(new List<Guid> { covariate.Id }, arimax.CovariateTimeSeriesIds);
        }

        /// <summary>
        /// Verifies the additive API transform-lambda field maps to the unchanged model setter for
        /// every time-series family and remains fixed through an explicit training-window change.
        /// </summary>
        [TestMethod]
        public void CreateTimeSeries_ManualTransformLambdaMapsToEveryFamily()
        {
            var source = AddDailySeries();
            foreach (TimeSeriesModelType modelType in Enum.GetValues<TimeSeriesModelType>())
            {
                var request = new CreateTimeSeriesAnalysisRequest
                {
                    TimeSeriesId = source.Id,
                    ModelType = modelType,
                    TransformType = RMC.BestFit.Models.Transform.YeoJohnson,
                    TransformLambda = 0.6,
                    TrainingTimeSteps = 900,
                };
                if (modelType is TimeSeriesModelType.Ar or TimeSeriesModelType.Ma)
                    request.Order = 1;
                else
                    request.POrder = 1;

                AnalysisResource resource = _service.CreateTimeSeries(request);
                double actual = modelType switch
                {
                    TimeSeriesModelType.Ar => resource.Ar!.AutoRegressive.TransformLambda,
                    TimeSeriesModelType.Ma => resource.Ma!.MovingAverage.TransformLambda,
                    TimeSeriesModelType.Arima => resource.Arima!.ARIMA.TransformLambda,
                    TimeSeriesModelType.Arimax => resource.Arimax!.ARIMAX.TransformLambda,
                    _ => double.NaN,
                };
                Assert.AreEqual(0.6, actual, 1E-12, modelType.ToString());
            }
        }

        /// <summary>
        /// Verifies fields that do not apply to the chosen model type are rejected with the
        /// offending fields named, and out-of-range forecast horizons and missing covariates fail.
        /// </summary>
        [TestMethod]
        public void CreateTimeSeries_RejectsIrrelevantFields()
        {
            var source = AddDailySeries();

            var arWithArima = Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ar,
                POrder = 1,
                CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
            }));
            StringAssert.Contains(arWithArima.Message, "pOrder");
            StringAssert.Contains(arWithArima.Message, "covariateExtension");
            StringAssert.Contains(arWithArima.Message, "'ar'");

            var arimaWithOrder = Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arima,
                Order = 2
            }));
            StringAssert.Contains(arimaWithOrder.Message, "order");

            var arimaWithSeasonality = Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arima,
                IncludeSeasonality = true
            }));
            StringAssert.Contains(arimaWithSeasonality.Message, "includeSeasonality");

            Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ar,
                ForecastingTimeSteps = 101
            }));

            Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ar,
                TransformLambda = 0.6
            }));
            Assert.ThrowsException<ArgumentException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ar,
                TransformType = RMC.BestFit.Models.Transform.BoxCox,
                TransformLambda = double.NaN
            }));

            Assert.ThrowsException<ResourceNotFoundException>(() => _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arimax,
                CovariateTimeSeriesIds = new List<Guid> { Guid.NewGuid() }
            }));
        }
    }
}
