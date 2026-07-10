using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.IntervalDataRowItem"/>.
    /// </summary>
    /// <remarks>
    /// Focus: parameterless construction, parameterized construction, property pass-through to
    /// the wrapped <see cref="IntervalData"/>, <c>SetOrdinate</c> rebind notifications, the
    /// null-series guard inside <c>ReplaceOrdinate</c>, the display-name table, and the
    /// constant <see cref="RMC_BestFit.IntervalDataRowItem.IsGridDisplayable"/> contract.
    /// </remarks>
    [TestClass]
    public class IntervalDataRowItemTests
    {
        /// <summary>
        /// Verifies the parameterless constructor produces a default row used by the
        /// click-to-add-row factory.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.IntervalDataRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>Index</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesIndex()
        {
            var ordinate = new IntervalData(8, 1.0, 2.0, 3.0);
            var row = new RMC_BestFit.IntervalDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreEqual(8, row.Index);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's lower bound.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesLowerValue()
        {
            var ordinate = new IntervalData(8, 1.0, 2.0, 3.0);
            var row = new RMC_BestFit.IntervalDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreEqual(1.0, row.LowerValue, 1e-12);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's most-likely value.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesValue()
        {
            var ordinate = new IntervalData(8, 1.0, 2.0, 3.0);
            var row = new RMC_BestFit.IntervalDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreEqual(2.0, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's upper bound.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesUpperValue()
        {
            var ordinate = new IntervalData(8, 1.0, 2.0, 3.0);
            var row = new RMC_BestFit.IntervalDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            Assert.AreEqual(3.0, row.UpperValue, 1e-12);
        }

        /// <summary>
        /// Verifies that setting <see cref="RMC_BestFit.IntervalDataRowItem.LowerValue"/> when
        /// no series is attached does NOT throw (null-series guard).
        /// </summary>
        [TestMethod]
        public void LowerValue_SetWithNullSeries_DoesNotThrow()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            row.LowerValue = 0.5;
        }

        /// <summary>
        /// Verifies that re-assigning <see cref="RMC_BestFit.IntervalDataRowItem.Value"/> to its
        /// existing value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Value_SetSameValue_DoesNotFirePropertyChanged()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Value)) notifications++; };
            row.Value = 2.0;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.IntervalDataRowItem.SetOrdinate"/> fires
        /// <c>PropertyChanged</c> for every dependent property.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_FiresPropertyChangedForAllProperties()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            row.SetOrdinate(new IntervalData(5, 10.0, 11.0, 12.0));
            CollectionAssert.Contains(changed, nameof(row.Index));
            CollectionAssert.Contains(changed, nameof(row.LowerValue));
            CollectionAssert.Contains(changed, nameof(row.Value));
            CollectionAssert.Contains(changed, nameof(row.UpperValue));
            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.IntervalDataRowItem.SetOrdinate"/> updates the wrapped
        /// values exposed by getters.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_UpdatesWrappedValues()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            row.SetOrdinate(new IntervalData(5, 10.0, 11.0, 12.0));
            Assert.AreEqual(5, row.Index);
            Assert.AreEqual(10.0, row.LowerValue, 1e-12);
            Assert.AreEqual(11.0, row.Value, 1e-12);
            Assert.AreEqual(12.0, row.UpperValue, 1e-12);
        }

        /// <summary>
        /// Verifies the row can notify WPF when only the wrapped ordinate's computed plotting position changes.
        /// </summary>
        [TestMethod]
        public void RefreshPlottingPosition_FiresPropertyChangedAndExposesUpdatedValue()
        {
            var ordinate = new IntervalData(1, 1.0, 2.0, 3.0, 0.10);
            var row = new RMC_BestFit.IntervalDataRowItem(new ObservableCollection<object>(), ordinate, new DataFrame(), null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            ordinate.PlottingPosition = 0.75;
            row.RefreshPlottingPosition();

            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
            Assert.AreEqual(0.75, row.PlottingPosition, 1e-12);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.IntervalDataRowItem.IsGridDisplayable"/> always
        /// returns <c>true</c> regardless of property name.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_AnyProperty_ReturnsTrue()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.Index)));
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.LowerValue)));
            Assert.IsTrue(row.IsGridDisplayable("Anything"));
        }

        /// <summary>
        /// Verifies the display-name table for all known properties.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_AllKnownProperties_ReturnExpectedLabels()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            Assert.AreEqual("Index", row.PropertyDisplayName(nameof(row.Index)));
            Assert.AreEqual("Lower", row.PropertyDisplayName(nameof(row.LowerValue)));
            Assert.AreEqual("Most Likely", row.PropertyDisplayName(nameof(row.Value)));
            Assert.AreEqual("Upper", row.PropertyDisplayName(nameof(row.UpperValue)));
            Assert.AreEqual("Plotting Position", row.PropertyDisplayName(nameof(row.PlottingPosition)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.IntervalDataRowItem.PropertyDisplayName"/> returns
        /// <c>null</c> for an unknown property name.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            Assert.IsNull(row.PropertyDisplayName("Bogus"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.IntervalDataRowItem.AddValidationRules"/> can be invoked
        /// without throwing on a constructed row item.
        /// </summary>
        [TestMethod]
        public void AddValidationRules_OnConstructedRow_DoesNotThrow()
        {
            var row = new RMC_BestFit.IntervalDataRowItem(
                new ObservableCollection<object>(), new IntervalData(0, 1.0, 2.0, 3.0), new DataFrame(), null);
            row.AddValidationRules();
        }
    }
}
