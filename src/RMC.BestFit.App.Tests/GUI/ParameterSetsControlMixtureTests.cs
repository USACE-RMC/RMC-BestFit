using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Verifies mixture parameter-set display reconstructs physical weights without rewriting MCMC results.
    /// </summary>
    [TestClass]
    public sealed class ParameterSetsControlMixtureTests
    {
        /// <summary>
        /// Creates a small deterministic exact-data frame.
        /// </summary>
        /// <returns>The inline test data.</returns>
        private static BestFitDataFrame CreateDataFrame()
        {
            return new BestFitDataFrame
            {
                ExactSeries = new ExactSeries(new[] { 0.0, 1.0, 2.0, 3.0, 4.0 })
            };
        }

        /// <summary>
        /// Verifies a two-component K-1 result displays both physical weights and preserves component order.
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_TwoComponentResult_DerivesFinalWeightWithoutMutation()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value)
                .Where((_, index) => index != 1)
                .ToArray();
            stored[0] = 0.4;
            double[] snapshot = stored.ToArray();

            double[] displayed = RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored);

            CollectionAssert.AreEqual(snapshot, stored);
            Assert.AreEqual(model.NumberOfParameters, displayed.Length);
            Assert.AreEqual(0.4, displayed[0], 0.0);
            Assert.AreEqual(0.6, displayed[1], 0.0);
            CollectionAssert.AreEqual(stored.Skip(1).ToArray(), displayed.Skip(2).ToArray());
        }

        /// <summary>
        /// Verifies zero-inflated K-1 results use the continuous mass rather than one.
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_ZeroInflatedThreeComponentResult_UsesContinuousMass()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                },
                isZeroInflated: true);
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value)
                .Where((_, index) => index != 2)
                .ToArray();
            stored[0] = 0.2;
            stored[1] = 0.3;

            double[] displayed = RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored);

            Assert.AreEqual(0.3, displayed[2], 1E-15);
            Assert.AreEqual(1.0 - model.Mixture!.ZeroWeight, displayed.Take(3).Sum(), 1E-15);
            CollectionAssert.AreEqual(stored.Skip(2).ToArray(), displayed.Skip(3).ToArray());
        }

        /// <summary>
        /// Verifies legacy full-K result rows pass through unchanged in a new array.
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_LegacyFullKResult_PassesThroughUnchanged()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value).ToArray();

            double[] displayed = RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored);

            CollectionAssert.AreEqual(stored, displayed);
            Assert.AreNotSame(stored, displayed);
        }

        /// <summary>
        /// Verifies an infeasible residual weight is reported as an explicit data error.
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_InfeasibleResult_Throws()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value)
                .Where((_, index) => index != 1)
                .ToArray();
            stored[0] = 1.1;

            Assert.ThrowsException<InvalidDataException>(() =>
                RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored));
        }

        /// <summary>
        /// Verifies an invalid legacy full-K row is reported instead of passed to the table.
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_InvalidLegacyFullKResult_Throws()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value).ToArray();
            stored[0] = -0.1;

            Assert.ThrowsException<InvalidDataException>(() =>
                RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored));
        }

        /// <summary>
        /// Verifies a derived weight that is negative only at rounding level displays as zero
        /// instead of throwing (finding M6: the control's duplicated expansion lacked the core's
        /// 1E-12 simplex-boundary clamp, so this exact draw used to crash the tab).
        /// </summary>
        [TestMethod]
        public void GetPhysicalDisplayValues_DerivedWeightRoundingLevelNegative_ClampsToZeroInsteadOfThrowing()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] stored = model.Parameters.Select(parameter => parameter.Value)
                .Where((_, index) => index != 1)
                .ToArray();
            // Derived weight = componentMass(1.0) - stored[0] = -5E-13: within the core's 1E-12
            // clamp tolerance, so it must display as zero rather than throw.
            stored[0] = 1.0 + 5E-13;

            double[] displayed = RMC_BestFit.ParameterSetsControl.GetPhysicalDisplayValues(analysis, stored);

            Assert.AreEqual(0.0, displayed[1], 0.0,
                "A rounding-level negative derived weight is clamped to zero, not rejected.");
        }

        /// <summary>
        /// Verifies the WPF-independent decision seam behind the data-grid binding never throws:
        /// when any one retained draw cannot be expanded, it reports failure and no rows, rather
        /// than a table missing just that row or an unhandled exception reaching the UI thread.
        /// </summary>
        [TestMethod]
        public void TryGetAllPhysicalDisplayValues_OneInfeasibleDrawAmongValidOnes_ReturnsFalseWithNoRows()
        {
            var model = new MixtureModel(
                CreateDataFrame(),
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Normal,
                    UnivariateDistributionType.Normal
                });
            var analysis = new BayesianAnalysis(model);
            double[] baseline = model.Parameters.Select(parameter => parameter.Value)
                .Where((_, index) => index != 1)
                .ToArray();

            var output = new List<ParameterSet>();
            for (int drawIndex = 0; drawIndex < 3; drawIndex++)
            {
                double[] values = baseline.ToArray();
                values[0] = 0.3 + drawIndex * 0.05;
                output.Add(new ParameterSet(values, -drawIndex));
            }
            // A genuinely infeasible draw: the free weight alone exceeds the component mass by
            // far more than the clamp tolerance, so no valid physical mixture can be derived.
            double[] infeasibleValues = baseline.ToArray();
            infeasibleValues[0] = 1.5;
            output.Add(new ParameterSet(infeasibleValues, -100));

            var results = new MCMCResults(output[0], output, alpha: 0.10);
            analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);

            bool succeeded = RMC_BestFit.ParameterSetsControl.TryGetAllPhysicalDisplayValues(
                analysis, out List<double[]> displayRows);

            Assert.IsFalse(succeeded,
                "An infeasible draw must fail the whole-table decision instead of dropping just that row.");
            Assert.AreEqual(0, displayRows.Count);
        }
    }
}
