using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System.Collections.Generic;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.InputDataSelectionItem"/>.
    /// </summary>
    [TestClass]
    public class InputDataSelectionItemTests
    {
        /// <summary>
        /// Shared input-data collection used to construct lightweight input data fixtures.
        /// </summary>
        private static InputDataCollection _collection;

        /// <summary>
        /// Creates a shared collection backed by the singleton project.
        /// </summary>
        /// <param name="_">The MSTest context.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            _collection = new InputDataCollection(BestFitProject.GetInstance());
        }

        /// <summary>
        /// Verifies the no-overlay item exposes the null value and expected display text.
        /// </summary>
        [TestMethod]
        public void CreateNone_SetsNullValueAndNoneDisplayName()
        {
            var item = RMC_BestFit.InputDataSelectionItem.CreateNone();

            Assert.IsNull(item.Value);
            Assert.AreEqual(RMC_BestFit.InputDataSelectionItem.NoneDisplayName, item.DisplayName);
            Assert.AreEqual("0", item.SortKey);
        }

        /// <summary>
        /// Verifies a real item exposes the wrapped input data.
        /// </summary>
        [STATestMethod]
        public void Constructor_RealInputData_ExposesWrappedValue()
        {
            var inputData = new InputData("Observed Peaks", _collection);
            var item = new RMC_BestFit.InputDataSelectionItem(inputData);

            try
            {
                Assert.AreSame(inputData, item.Value);
                Assert.AreEqual("Observed Peaks", item.DisplayName);
                Assert.AreEqual("1Observed Peaks", item.SortKey);
            }
            finally
            {
                item.Dispose();
            }
        }

        /// <summary>
        /// Verifies input-data renames notify display and sort bindings.
        /// </summary>
        [STATestMethod]
        public void InputDataRename_RaisesDisplayAndSortNotifications()
        {
            var inputData = new InputData("Before Rename", _collection);
            var item = new RMC_BestFit.InputDataSelectionItem(inputData);
            var notifications = new List<string>();
            item.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

            try
            {
                inputData.Name = "After Rename";

                CollectionAssert.Contains(notifications, nameof(RMC_BestFit.InputDataSelectionItem.DisplayName));
                CollectionAssert.Contains(notifications, nameof(RMC_BestFit.InputDataSelectionItem.SortKey));
                Assert.AreEqual("After Rename", item.DisplayName);
                Assert.AreEqual("1After Rename", item.SortKey);
            }
            finally
            {
                item.Dispose();
            }
        }

        /// <summary>
        /// Verifies disposing a real item unsubscribes from wrapped input-data changes.
        /// </summary>
        [STATestMethod]
        public void Dispose_UnsubscribesFromInputData()
        {
            var inputData = new InputData("Before Dispose", _collection);
            var item = new RMC_BestFit.InputDataSelectionItem(inputData);
            int notifications = 0;
            item.PropertyChanged += (_, _) => notifications++;

            item.Dispose();
            inputData.Name = "After Dispose";

            Assert.AreEqual(0, notifications);
        }
    }
}
