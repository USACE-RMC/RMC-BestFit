using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using RMC.BestFit.Models;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.UncertainDataRowItem"/>.
    /// </summary>
    /// <remarks>
    /// Focus: parameterless construction, parameterized construction, property pass-through to
    /// the wrapped <see cref="UncertainData"/>, <c>SetOrdinate</c> rebind notifications, the
    /// null-series guard inside <c>ReplaceOrdinate</c>, the display-name table, and the
    /// always-true <see cref="RMC_BestFit.UncertainDataRowItem.IsGridDisplayable"/> contract.
    /// </remarks>
    [TestClass]
    public class UncertainDataRowItemTests
    {
        /// <summary>
        /// Builds a fresh <see cref="Normal"/> distribution with mean=10, stddev=1 for each test —
        /// avoids any shared state between tests.
        /// </summary>
        /// <returns>A new <see cref="Normal"/> distribution.</returns>
        private static Normal MakeNormalDistribution() => new Normal(10.0, 1.0);

        /// <summary>
        /// Verifies the parameterless constructor produces a default row used by the
        /// click-to-add-row factory.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.UncertainDataRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>Index</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesIndex()
        {
            var ordinate = new UncertainData(7, MakeNormalDistribution());
            var row = new RMC_BestFit.UncertainDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreEqual(7, row.Index);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's distribution reference.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesDistribution()
        {
            var dist = MakeNormalDistribution();
            var ordinate = new UncertainData(7, dist);
            var row = new RMC_BestFit.UncertainDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreSame(dist, row.Distribution);
        }

        /// <summary>
        /// Verifies that setting <see cref="RMC_BestFit.UncertainDataRowItem.Index"/> with a
        /// null series does NOT throw (null-series guard).
        /// </summary>
        [TestMethod]
        public void Index_SetWithNullSeries_DoesNotThrow()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(7, MakeNormalDistribution()), new DataFrame(), null);
            row.Index = 99;
        }

        /// <summary>
        /// Verifies that re-assigning <see cref="RMC_BestFit.UncertainDataRowItem.Index"/> to its
        /// existing value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Index_SetSameValue_DoesNotFirePropertyChanged()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(7, MakeNormalDistribution()), new DataFrame(), null);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Index)) notifications++; };
            row.Index = 7;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.UncertainDataRowItem.SetOrdinate"/> fires
        /// <c>PropertyChanged</c> for Index, Distribution, and PlottingPosition.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_FiresPropertyChangedForAllProperties()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            row.SetOrdinate(new UncertainData(99, new Normal(20.0, 2.0)));
            CollectionAssert.Contains(changed, nameof(row.Index));
            CollectionAssert.Contains(changed, nameof(row.Distribution));
            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.UncertainDataRowItem.SetOrdinate"/> updates the wrapped
        /// values exposed by getters.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_UpdatesWrappedValues()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            var newDist = new Normal(20.0, 2.0);
            row.SetOrdinate(new UncertainData(99, newDist));
            Assert.AreEqual(99, row.Index);
            Assert.AreSame(newDist, row.Distribution);
        }

        /// <summary>
        /// Verifies the row can notify WPF when only the wrapped ordinate's computed plotting position changes.
        /// </summary>
        [TestMethod]
        public void RefreshPlottingPosition_FiresPropertyChangedAndExposesUpdatedValue()
        {
            var ordinate = new UncertainData(1, MakeNormalDistribution(), 0.10);
            var row = new RMC_BestFit.UncertainDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            ordinate.PlottingPosition = 0.75;
            row.RefreshPlottingPosition();

            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
            Assert.AreEqual(0.75, row.PlottingPosition, 1e-12);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.UncertainDataRowItem.IsGridDisplayable"/> always returns
        /// <c>true</c> regardless of property name.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_AnyProperty_ReturnsTrue()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.Index)));
            Assert.IsTrue(row.IsGridDisplayable("Anything"));
        }

        /// <summary>
        /// Verifies the display-name table for all known properties.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_AllKnownProperties_ReturnExpectedLabels()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            Assert.AreEqual("Index", row.PropertyDisplayName(nameof(row.Index)));
            Assert.AreEqual("Distribution", row.PropertyDisplayName(nameof(row.Distribution)));
            Assert.AreEqual("Plotting Position", row.PropertyDisplayName(nameof(row.PlottingPosition)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.UncertainDataRowItem.PropertyDisplayName"/> returns
        /// <c>null</c> for an unknown property name.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            Assert.IsNull(row.PropertyDisplayName("Bogus"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.UncertainDataRowItem.AddValidationRules"/> can be invoked
        /// without throwing on a constructed row item.
        /// </summary>
        [TestMethod]
        public void AddValidationRules_OnConstructedRow_DoesNotThrow()
        {
            var row = new RMC_BestFit.UncertainDataRowItem(
                new ObservableCollection<object>(), new UncertainData(0, MakeNormalDistribution()), new DataFrame(), null);
            row.AddValidationRules();
        }
    }
}
