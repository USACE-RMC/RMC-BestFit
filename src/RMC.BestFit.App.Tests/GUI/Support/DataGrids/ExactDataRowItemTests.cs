using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using System;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.ExactDataRowItem"/>.
    /// </summary>
    /// <remarks>
    /// Focus: parameterless construction, parameterized construction, property pass-through to
    /// the wrapped <see cref="ExactData"/>, <c>SetOrdinate</c> rebind notifications, the
    /// null-series guard inside <c>ReplaceOrdinate</c>, the display-name table, and the
    /// <c>showDateTime</c> branching of <see cref="RMC_BestFit.ExactDataRowItem.IsGridDisplayable"/>.
    /// </remarks>
    [TestClass]
    public class ExactDataRowItemTests
    {
        /// <summary>
        /// Verifies that the parameterless constructor produces a default row with no wrapped data
        /// (used by the click-to-add-row factory in <c>ValidationDataGrid</c>).
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.ExactDataRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>Index</c> through the
        /// row's <see cref="RMC_BestFit.ExactDataRowItem.Index"/> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesIndex()
        {
            var ordinate = new ExactData(7, 12.5);
            var collection = new ObservableCollection<object>();
            var row = new RMC_BestFit.ExactDataRowItem(collection, ordinate, null);
            Assert.AreEqual(7, row.Index);
        }

        /// <summary>
        /// Verifies the parameterized constructor exposes the ordinate's <c>Value</c> through the
        /// row's <see cref="RMC_BestFit.ExactDataRowItem.Value"/> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesValue()
        {
            var ordinate = new ExactData(7, 12.5);
            var collection = new ObservableCollection<object>();
            var row = new RMC_BestFit.ExactDataRowItem(collection, ordinate, null);
            Assert.AreEqual(12.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the row exposes the ordinate's <c>PlottingPosition</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesPlottingPosition()
        {
            var ordinate = new ExactData(0, 1.0, 0.42, false);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(0.42, row.PlottingPosition, 1e-12);
        }

        /// <summary>
        /// Verifies the row exposes the ordinate's <c>IsLowOutlier</c> flag.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesIsLowOutlier()
        {
            var ordinate = new ExactData(0, 1.0, 0.0, isLowOutlier: true);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.IsTrue(row.IsLowOutlier);
        }

        /// <summary>
        /// Verifies the row exposes the ordinate's <c>DateTime</c>. Constructed via the int-index
        /// constructor so DateTime is the default <c>DateTime.MinValue</c>.
        /// </summary>
        [TestMethod]
        public void Constructor_WithOrdinate_ExposesDateTime()
        {
            var dt = new DateTime(2010, 5, 1);
            var ordinate = new ExactData(dt, 100.0);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            Assert.AreEqual(dt, row.DateTime);
        }

        /// <summary>
        /// Verifies that setting <see cref="RMC_BestFit.ExactDataRowItem.Index"/> when no series
        /// is attached does NOT throw (null-series guard inside <c>ReplaceOrdinate</c>).
        /// </summary>
        [TestMethod]
        public void Index_SetWithNullSeries_DoesNotThrow()
        {
            var ordinate = new ExactData(0, 1.0);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            row.Index = 99; // null-series path returns silently
        }

        /// <summary>
        /// Verifies that assigning <see cref="RMC_BestFit.ExactDataRowItem.Index"/> to its
        /// current value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Index_SetSameValue_DoesNotFirePropertyChanged()
        {
            var ordinate = new ExactData(5, 1.0);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Index)) notifications++; };
            row.Index = 5;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies that assigning <see cref="RMC_BestFit.ExactDataRowItem.Value"/> to its
        /// current value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Value_SetSameValue_DoesNotFirePropertyChanged()
        {
            var ordinate = new ExactData(0, 7.5);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Value)) notifications++; };
            row.Value = 7.5;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies that <see cref="RMC_BestFit.ExactDataRowItem.SetOrdinate"/> rebinds the row
        /// to a new ordinate and fires <c>PropertyChanged</c> for every dependent property.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_FiresPropertyChangedForAllProperties()
        {
            var ordinate = new ExactData(1, 1.0);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            row.SetOrdinate(new ExactData(2, 99.9));
            CollectionAssert.Contains(changed, nameof(row.Index));
            CollectionAssert.Contains(changed, nameof(row.DateTime));
            CollectionAssert.Contains(changed, nameof(row.Value));
            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
            CollectionAssert.Contains(changed, nameof(row.IsLowOutlier));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.SetOrdinate"/> updates the wrapped
        /// ordinate so subsequent reads return the new values.
        /// </summary>
        [TestMethod]
        public void SetOrdinate_UpdatesWrappedValues()
        {
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), new ExactData(1, 1.0), null);
            row.SetOrdinate(new ExactData(2, 99.9));
            Assert.AreEqual(2, row.Index);
            Assert.AreEqual(99.9, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the row can notify WPF when only the wrapped ordinate's computed plotting position changes.
        /// </summary>
        [TestMethod]
        public void RefreshPlottingPosition_FiresPropertyChangedAndExposesUpdatedValue()
        {
            var ordinate = new ExactData(1, 1.0, 0.10);
            var row = new RMC_BestFit.ExactDataRowItem(new ObservableCollection<object>(), ordinate, null);
            var changed = new System.Collections.Generic.List<string>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            ordinate.PlottingPosition = 0.75;
            row.RefreshPlottingPosition();

            CollectionAssert.Contains(changed, nameof(row.PlottingPosition));
            Assert.AreEqual(0.75, row.PlottingPosition, 1e-12);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.IsGridDisplayable"/> hides the
        /// integer Index column when constructed with <c>showDateTime: true</c>.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_ShowDateTimeTrue_HidesIndexColumn()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null, showDateTime: true);
            Assert.IsFalse(row.IsGridDisplayable(nameof(row.Index)));
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.DateTime)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.IsGridDisplayable"/> shows the integer
        /// Index column and hides DateTime in the default index-display mode.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_ShowDateTimeFalse_ShowsIndexColumn()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null, showDateTime: false);
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.Index)));
            Assert.IsFalse(row.IsGridDisplayable(nameof(row.DateTime)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.IsGridDisplayable"/> returns true for
        /// any property other than Index / DateTime (e.g. Value, PlottingPosition, IsLowOutlier).
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_OtherProperties_ReturnsTrue()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null);
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.Value)));
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.PlottingPosition)));
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.IsLowOutlier)));
        }

        /// <summary>
        /// Verifies the display-name table maps each known property name to its UI label.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_AllKnownProperties_ReturnExpectedLabels()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null);
            Assert.AreEqual("Index", row.PropertyDisplayName(nameof(row.Index)));
            Assert.AreEqual("Date Time", row.PropertyDisplayName(nameof(row.DateTime)));
            Assert.AreEqual("Value", row.PropertyDisplayName(nameof(row.Value)));
            Assert.AreEqual("Low Outlier", row.PropertyDisplayName(nameof(row.IsLowOutlier)));
            Assert.AreEqual("Plotting Position", row.PropertyDisplayName(nameof(row.PlottingPosition)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.PropertyDisplayName"/> returns
        /// <c>null</c> for an unknown property name (defensive fallback).
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null);
            Assert.IsNull(row.PropertyDisplayName("DoesNotExist"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.ExactDataRowItem.AddValidationRules"/> can be invoked
        /// without throwing on a constructed row item.
        /// </summary>
        [TestMethod]
        public void AddValidationRules_OnConstructedRow_DoesNotThrow()
        {
            var row = new RMC_BestFit.ExactDataRowItem(
                new ObservableCollection<object>(), new ExactData(1, 1.0), null);
            row.AddValidationRules();
        }
    }
}
