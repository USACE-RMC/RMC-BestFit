using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.ThresholdDataRowItem"/>.
    /// </summary>
    /// <remarks>
    /// Focus: parameterless construction, parameterized construction, property pass-through to
    /// the wrapped <see cref="ThresholdData"/>, <c>SetOrdinate</c> rebind notifications, the
    /// null-series guard inside <c>ReplaceOrdinate</c>, the display-name table, and the
    /// always-true <see cref="RMC_BestFit.ThresholdDataRowItem.IsGridDisplayable"/> contract.
    /// </remarks>
    [TestClass]
    public class ThresholdDataRowItemTests
    {
        /// <summary>
        /// Verifies the parameterless constructor produces a default row used by the
        /// click-to-add-row factory.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>StartIndex</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesStartIndex()
        {
            var ordinate = new ThresholdData(100, 200, 5.5);
            var row = new RMC_BestFit.ThresholdDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(100, row.StartIndex);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>EndIndex</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesEndIndex()
        {
            var ordinate = new ThresholdData(100, 200, 5.5);
            var row = new RMC_BestFit.ThresholdDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(200, row.EndIndex);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's threshold value.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesValue()
        {
            var ordinate = new ThresholdData(100, 200, 5.5);
            var row = new RMC_BestFit.ThresholdDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(5.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>NumberAbove</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesNumberAbove()
        {
            var ordinate = new ThresholdData(100, 200, 5.5) { NumberAbove = 3 };
            var row = new RMC_BestFit.ThresholdDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(3, row.NumberAbove);
        }

        /// <summary>
        /// Verifies that setting <see cref="RMC_BestFit.ThresholdDataRowItem.StartIndex"/>
        /// when no series is attached does NOT throw (null-series guard).
        /// </summary>
        [TestMethod]
        public void StartIndex_SetWithNullSeries_DoesNotThrow()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(100, 200, 5.0), null);
            row.StartIndex = 150;
        }

        /// <summary>
        /// Verifies that re-assigning <see cref="RMC_BestFit.ThresholdDataRowItem.Value"/> to its
        /// existing value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Value_SetSameValue_DoesNotFirePropertyChanged()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Value)) notifications++; };
            row.Value = 5.0;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ThresholdDataRowItem.SetOrdinate"/> fires
        /// <c>PropertyChanged</c> for every dependent property.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_FiresPropertyChangedForAllProperties()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            row.SetOrdinate(new ThresholdData(20, 30, 7.0) { NumberAbove = 4 });
            CollectionAssert.Contains(changed, nameof(row.StartIndex));
            CollectionAssert.Contains(changed, nameof(row.EndIndex));
            CollectionAssert.Contains(changed, nameof(row.Value));
            CollectionAssert.Contains(changed, nameof(row.NumberAbove));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ThresholdDataRowItem.SetOrdinate"/> updates the wrapped
        /// values exposed by getters.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_UpdatesWrappedValues()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            row.SetOrdinate(new ThresholdData(20, 30, 7.0) { NumberAbove = 4 });
            Assert.AreEqual(20, row.StartIndex);
            Assert.AreEqual(30, row.EndIndex);
            Assert.AreEqual(7.0, row.Value, 1e-12);
            Assert.AreEqual(4, row.NumberAbove);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ThresholdDataRowItem.IsGridDisplayable"/> always returns
        /// <c>true</c> regardless of property name.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_AnyProperty_ReturnsTrue()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.StartIndex)));
            Assert.IsTrue(row.IsGridDisplayable("Anything"));
        }

        /// <summary>
        /// Verifies the display-name table for all known properties.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_AllKnownProperties_ReturnExpectedLabels()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            Assert.AreEqual("Start Index", row.PropertyDisplayName(nameof(row.StartIndex)));
            Assert.AreEqual("End Index", row.PropertyDisplayName(nameof(row.EndIndex)));
            Assert.AreEqual("Value", row.PropertyDisplayName(nameof(row.Value)));
            Assert.AreEqual("No. Above", row.PropertyDisplayName(nameof(row.NumberAbove)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ThresholdDataRowItem.PropertyDisplayName"/> returns
        /// <c>null</c> for an unknown property name.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            Assert.IsNull(row.PropertyDisplayName("Bogus"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ThresholdDataRowItem.AddValidationRules"/> can be invoked
        /// without throwing on a constructed row item.
        /// </summary>
        [TestMethod]
        public void AddValidationRules_OnConstructedRow_DoesNotThrow()
        {
            var row = new RMC_BestFit.ThresholdDataRowItem(
                new ObservableCollection<object>(), new ThresholdData(0, 10, 5.0), null);
            row.AddValidationRules();
        }
    }
}
