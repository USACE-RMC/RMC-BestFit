using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.OrdinateRowItem"/> — the base class wrapping a single
    /// double value in an ordinate <c>ValidationDataGrid</c>.
    /// </summary>
    [TestClass]
    public class OrdinateRowItemTests
    {
        /// <summary>
        /// Verifies the parameterless constructor produces a default row used by the
        /// click-to-add-row factory.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.OrdinateRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor stores the value through the <c>Value</c> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValue_ExposesValue()
        {
            var row = new RMC_BestFit.OrdinateRowItem(new ObservableCollection<object>(), 42.5);
            Assert.AreEqual(42.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies setting <see cref="RMC_BestFit.OrdinateRowItem.Value"/> stores the new value.
        /// </summary>
        [TestMethod]
        public void Value_SetNewValue_UpdatesValue()
        {
            var row = new RMC_BestFit.OrdinateRowItem(new ObservableCollection<object>(), 1.0);
            row.Value = 2.5;
            Assert.AreEqual(2.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies setting <see cref="RMC_BestFit.OrdinateRowItem.Value"/> to a new value fires
        /// <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Value_SetNewValue_FiresPropertyChanged()
        {
            var row = new RMC_BestFit.OrdinateRowItem(new ObservableCollection<object>(), 1.0);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Value)) notifications++; };
            row.Value = 2.5;
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Verifies setting <see cref="RMC_BestFit.OrdinateRowItem.Value"/> to its existing value
        /// does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [TestMethod]
        public void Value_SetSameValue_DoesNotFirePropertyChanged()
        {
            var row = new RMC_BestFit.OrdinateRowItem(new ObservableCollection<object>(), 1.0);
            int notifications = 0;
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Value)) notifications++; };
            row.Value = 1.0;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.OrdinateRowItem.IsGridDisplayable"/> shows only the
        /// <c>Value</c> property column.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_Value_ReturnsTrue()
        {
            var row = new RMC_BestFit.OrdinateRowItem();
            Assert.IsTrue(row.IsGridDisplayable(nameof(row.Value)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.OrdinateRowItem.IsGridDisplayable"/> hides any other
        /// inherited / framework property.
        /// </summary>
        [TestMethod]
        public void IsGridDisplayable_OtherProperty_ReturnsFalse()
        {
            var row = new RMC_BestFit.OrdinateRowItem();
            Assert.IsFalse(row.IsGridDisplayable("DoesNotExist"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.OrdinateRowItem.PropertyDisplayName"/> returns "Value"
        /// for the <c>Value</c> property.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_Value_ReturnsValue()
        {
            var row = new RMC_BestFit.OrdinateRowItem();
            Assert.AreEqual("Value", row.PropertyDisplayName(nameof(row.Value)));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.OrdinateRowItem.PropertyDisplayName"/> returns
        /// <c>null</c> for any other property.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_OtherProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.OrdinateRowItem();
            Assert.IsNull(row.PropertyDisplayName("Anything"));
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.OrdinateRowItem.AddValidationRules"/> can be invoked
        /// without throwing.
        /// </summary>
        [TestMethod]
        public void AddValidationRules_OnConstructedRow_DoesNotThrow()
        {
            var row = new RMC_BestFit.OrdinateRowItem(new ObservableCollection<object>(), 1.0);
            row.AddValidationRules();
        }
    }
}
