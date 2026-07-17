using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// The input data frame containing exact, uncertain, interval, and threshold data series.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class DataFrame : INotifyPropertyChanged
    {

        #region Construction

        /// <summary>
        /// Constructs an empty data frame.
        /// </summary>
        public DataFrame()
        {
            ExactSeries.CollectionChanged += ExactSeriesCollectionChanged;
            UncertainSeries.CollectionChanged += UncertainSeriesCollectionChanged;
            IntervalSeries.CollectionChanged += IntervalSeriesCollectionChanged;
            ThresholdSeries.CollectionChanged += ThresholdSeriesCollectionChanged;
        }

        /// <summary>
        /// Constructs a data frame from XElement.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize.</param>
        public DataFrame(XElement xElement)
        {
            var numberOfLowOutliersAttr = xElement.Attribute(nameof(NumberOfLowOutliers));
            if (numberOfLowOutliersAttr != null) int.TryParse(numberOfLowOutliersAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _numberOfLowOutliers);
            var lowOutlierThresholdAttr = xElement.Attribute(nameof(LowOutlierThreshold));
            if (lowOutlierThresholdAttr != null) double.TryParse(lowOutlierThresholdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _lowOutlierThreshold);
            var plottingParameterAttr = xElement.Attribute(nameof(PlottingParameter));
            if (plottingParameterAttr != null) double.TryParse(plottingParameterAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _plottingParameter);

            foreach (XElement xEl in xElement.Elements())
            {
                if (xEl.Name == nameof(ExactSeries))
                    ExactSeries = new ExactSeries(xEl);
                if (xEl.Name == nameof(UncertainSeries))
                    UncertainSeries = new UncertainSeries(xEl);
                if (xEl.Name == nameof(IntervalSeries))
                    IntervalSeries = new IntervalSeries(xEl);
                if (xEl.Name == nameof(ThresholdSeries))
                    ThresholdSeries = new ThresholdSeries(xEl);
            }

            var lambdaAttr = xElement.Attribute(nameof(Lambda));
            if (lambdaAttr != null)
            {
                double.TryParse(lambdaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _lambda);
            }
            else
            {
                CalculateLambda();
            }

            var usgsRawTextAttr = xElement.Attribute(nameof(USGSRawText));
            if (usgsRawTextAttr != null) _usgsRawText = usgsRawTextAttr.Value;

            // Rebuild effective threshold counts from the source NumberAbove values persisted in XML.
            ProcessThresholdSeries();
        }

        #endregion

        #region Members

        /// <summary>
        /// Occurs when a data-frame property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private ExactSeries _exactSeries = new ExactSeries();
        private UncertainSeries _uncertainSeries = new UncertainSeries();
        private IntervalSeries _intervalSeries = new IntervalSeries();
        private ThresholdSeries _thresholdSeries = new ThresholdSeries();
        private List<Data> _fullTimeSeries = new List<Data>();

        /// <summary>
        /// Synchronization root for thread-safe access to <see cref="ProcessThresholdSeries"/>,
        /// <see cref="CreateFullTimeSeries"/>, and the rebuild branch of the <see cref="FullTimeSeries"/>
        /// getter. A single shared lock is intentional: <see cref="CreateFullTimeSeries"/> reads
        /// <see cref="ThresholdData.NumberAbove"/>/<see cref="ThresholdData.NumberBelow"/> that
        /// <see cref="ProcessThresholdSeries"/> writes, and serializing both with the same monitor
        /// gives the reader a happens-before guarantee on any prior writer.
        /// </summary>
        private readonly object _syncRoot = new object();

        private double _lambda = 1.0;
        private int _numberOfLowOutliers = 0;
        private double _lowOutlierThreshold = 0;
        private double _plottingParameter = 0.0;
        private long _plottingPositionVersion;
        private string _usgsRawText = "";

        /// <summary>
        /// The exact data series collection.
        /// </summary>
        public ExactSeries ExactSeries
        {
            get { return _exactSeries; }
            set
            {
                for (int i = 0; i < _exactSeries.Count; i++)
                    _exactSeries[i].PropertyChanged -= ExactDataChanged;
                _exactSeries.CollectionChanged -= ExactSeriesCollectionChanged;
                _exactSeries = value;
                _exactSeries.CollectionChanged += ExactSeriesCollectionChanged;
                for (int i = 0; i < _exactSeries.Count; i++)
                    _exactSeries[i].PropertyChanged += ExactDataChanged;
                Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(nameof(ExactSeries));
            }
        }

        /// <summary>
        /// The uncertain data series collection.
        /// </summary>
        public UncertainSeries UncertainSeries
        {
            get { return _uncertainSeries; }
            set
            {
                for (int i = 0; i < _uncertainSeries.Count; i++)
                    _uncertainSeries[i].PropertyChanged -= UncertainDataChanged;
                _uncertainSeries.CollectionChanged -= UncertainSeriesCollectionChanged;
                _uncertainSeries = value;
                _uncertainSeries.CollectionChanged += UncertainSeriesCollectionChanged;
                for (int i = 0; i < _uncertainSeries.Count; i++)
                    _uncertainSeries[i].PropertyChanged += UncertainDataChanged;
                Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(nameof(UncertainSeries));
            }
        }

        /// <summary>
        /// The interval data series collection.
        /// </summary>
        public IntervalSeries IntervalSeries
        {
            get { return _intervalSeries; }
            set
            {
                for (int i = 0; i < _intervalSeries.Count; i++)
                    _intervalSeries[i].PropertyChanged -= IntervalDataChanged;
                _intervalSeries.CollectionChanged -= IntervalSeriesCollectionChanged;
                _intervalSeries = value;
                _intervalSeries.CollectionChanged += IntervalSeriesCollectionChanged;
                for (int i = 0; i < _intervalSeries.Count; i++)
                    _intervalSeries[i].PropertyChanged += IntervalDataChanged;
                Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(nameof(IntervalSeries));
            }
        }

        /// <summary>
        /// The threshold data series collection.
        /// </summary>
        public ThresholdSeries ThresholdSeries
        {
            get { return _thresholdSeries; }
            set
            {
                for (int i = 0; i < _thresholdSeries.Count; i++)
                    _thresholdSeries[i].PropertyChanged -= ThresholdDataChanged;
                _thresholdSeries.CollectionChanged -= ThresholdSeriesCollectionChanged;
                _thresholdSeries = value;
                _thresholdSeries.CollectionChanged += ThresholdSeriesCollectionChanged;
                for (int i = 0; i < _thresholdSeries.Count; i++)
                    _thresholdSeries[i].PropertyChanged += ThresholdDataChanged;
                Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(nameof(ThresholdSeries));
            }
        }

        /// <summary>
        /// The full time series in chronological order.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Thread-safe for concurrent readers. The getter uses a double-checked-locking pattern
        /// with <c>Volatile.Read</c> on the fast path so MCMC hot loops that
        /// iterate <see cref="FullTimeSeries"/> do not take a lock. The rebuild branch acquires
        /// <c>_syncRoot</c>, which is also taken by <see cref="CreateFullTimeSeries"/> and
        /// <see cref="ProcessThresholdSeries"/>.
        /// </para>
        /// <para>
        /// The returned <see cref="List{Data}"/> must be treated as read-only; mutation is reserved
        /// for <see cref="DataFrame"/> internals. <see cref="CreateFullTimeSeries"/> publishes a new
        /// list reference atomically, so callers that captured an earlier reference can continue to
        /// iterate it safely while a concurrent rebuild runs.
        /// </para>
        /// </remarks>
        public List<Data> FullTimeSeries
        {
            get
            {
                var snapshot = Volatile.Read(ref _fullTimeSeries);
                int total = TotalRecordLength();
                if (total == 0 || snapshot.Count == total)
                    return snapshot;

                lock (_syncRoot)
                {
                    if (_fullTimeSeries.Count != TotalRecordLength() && TotalRecordLength() > 0)
                        CreateFullTimeSeries();
                    return _fullTimeSeries;
                }
            }
        }

        /// <summary>
        /// Returns the number of low outliers in the exact data series. 
        /// </summary>
        public int NumberOfLowOutliers
        {
            get { return _numberOfLowOutliers; }
        }

        /// <summary>
        /// The low outlier threshold value.
        /// </summary>
        [Category("Low Outlier Tests")]
        [DisplayName("Threshold Value")]
        [Description("Defines the threshold for low outliers; values below this are flagged as low outliers.")]
        [Browsable(true)]
        public double LowOutlierThreshold
        {
            get { return _lowOutlierThreshold; }
            set
            {
                if (_lowOutlierThreshold != value)
                {
                    _lowOutlierThreshold = value;
                    RaisePropertyChange(nameof(LowOutlierThreshold));
                }
            }
        }

        /// <summary>
        /// The plotting position parameter. Default is 0.0 (Weibull).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is not finite or is outside the interval [0, 1).
        /// </exception>
        [Category("Plotting Positions")]
        [DisplayName("Plotting Parameter")]
        [Description("Sets the plotting position parameter. A value of 0.0 (Weibull) is recommended by default. Alternatives include 0.40 (Cunnane), 0.44 (Gringorten), and 0.50 (Hazen).")]
        [Browsable(true)]
        public double PlottingParameter
        {
            get { return _plottingParameter; }
            set
            {
                if (!double.IsFinite(value) || value < 0d || value >= 1d)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "The plotting parameter must be finite, greater than or equal to zero, and less than one.");

                if (_plottingParameter != value)
                {
                    _plottingParameter = value;
                    CalculatePlottingPositions();
                    RaisePropertyChange(nameof(PlottingParameter));       
                }
            }
        }

        /// <summary>
        /// Gets the version stamp for data-frame plotting-position inputs.
        /// </summary>
        /// <remarks>
        /// The stamp changes whenever plotting positions are recalculated or assigned directly.
        /// It permits consumers to cache validation without rescanning unchanged samples.
        /// </remarks>
        internal long PlottingPositionVersion => Volatile.Read(ref _plottingPositionVersion);

        /// <summary>
        /// The average number of events per index.
        /// </summary>
        public double Lambda => _lambda;


        /// <summary>
        /// Returns the USGS raw text file for annual peak data. 
        /// </summary>
        public string USGSRawText => _usgsRawText;

        #endregion

        #region Methods

        #region Property Changed

        /// <summary>
        /// Handles the exact data series collection changed event.
        /// </summary>
        /// <param name="sender">The collection.</param>
        /// <param name="e">Provides data for the collection changed event.</param>
        private void ExactSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Data was added or inserted
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    ((ExactData)e.NewItems[i]!).PropertyChanged += ExactDataChanged;
                }
            }
            // Data was removed
            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    ((ExactData)e.OldItems[i]!).PropertyChanged -= ExactDataChanged;
                }
            }
            // Collection was reset
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < ExactSeries.Count; i++)
                {
                    ExactSeries[i].PropertyChanged -= ExactDataChanged;
                    ExactSeries[i].PropertyChanged += ExactDataChanged;
                }
            }
            //
            RaisePropertyChange(nameof(ExactSeries));
            if (ExactSeries.SuppressCollectionChanged == false)
            {
                CalculateLambda();
                CalculatePlottingPositions();
            }
        }

        /// <summary>
        /// Handles the uncertain data series collection changed event.
        /// </summary>
        /// <param name="sender">The collection.</param>
        /// <param name="e">Provides data for the collection changed event.</param>
        private void UncertainSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Data was added or inserted
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    ((UncertainData)e.NewItems[i]!).PropertyChanged += UncertainDataChanged;
                }
            }
            // Data was removed
            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    ((UncertainData)e.OldItems[i]!).PropertyChanged -= UncertainDataChanged;
                }
            }
            // Collection was reset
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < UncertainSeries.Count; i++)
                {
                    UncertainSeries[i].PropertyChanged -= UncertainDataChanged;
                    UncertainSeries[i].PropertyChanged += UncertainDataChanged;
                }
            }
            //
            RaisePropertyChange(nameof(UncertainSeries));
            if (UncertainSeries.SuppressCollectionChanged == false)
                CalculatePlottingPositions();
        }

        /// <summary>
        /// Handles the interval data series collection changed event.
        /// </summary>
        /// <param name="sender">The collection.</param>
        /// <param name="e">Provides data for the collection changed event.</param>
        private void IntervalSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Data was added or inserted
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    ((IntervalData)e.NewItems[i]!).PropertyChanged += IntervalDataChanged;
                }
            }
            // Data was removed
            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    ((IntervalData)e.OldItems[i]!).PropertyChanged -= IntervalDataChanged;
                }
            }
            // Collection was reset
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < IntervalSeries.Count; i++)
                {
                    IntervalSeries[i].PropertyChanged -= IntervalDataChanged;
                    IntervalSeries[i].PropertyChanged += IntervalDataChanged;
                }
            }
            //
            RaisePropertyChange(nameof(IntervalSeries));
            if (IntervalSeries.SuppressCollectionChanged == false)
                CalculatePlottingPositions();
        }

        /// <summary>
        /// Handles the threshold data series collection changed event.
        /// </summary>
        /// <param name="sender">The collection.</param>
        /// <param name="e">Provides data for the collection changed event.</param>
        private void ThresholdSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Data was added or inserted
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    ((ThresholdData)e.NewItems[i]!).PropertyChanged += ThresholdDataChanged;
                }
            }
            // Data was removed
            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    ((ThresholdData)e.OldItems[i]!).PropertyChanged -= ThresholdDataChanged;
                }
            }
            // Collection was reset
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < ThresholdSeries.Count; i++)
                {
                    ThresholdSeries[i].PropertyChanged -= ThresholdDataChanged;
                    ThresholdSeries[i].PropertyChanged += ThresholdDataChanged;
                }
            }
            //
            RaisePropertyChange(nameof(ThresholdSeries));
            if (ThresholdSeries.SuppressCollectionChanged == false)
                CalculatePlottingPositions();
        }

        /// <summary>
        /// Handles the exact data property changed event.
        /// </summary>
        /// <param name="sender">The data.</param>
        /// <param name="e">Provides data for the property changed event.</param>
        private void ExactDataChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ExactSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName != nameof(Data.PlottingPosition))
                    CalculatePlottingPositions();
                else
                    Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles the uncertain data property changed event.
        /// </summary>
        /// <param name="sender">The data.</param>
        /// <param name="e">Provides data for the property changed event.</param>
        private void UncertainDataChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (UncertainSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName != nameof(Data.PlottingPosition))
                    CalculatePlottingPositions();
                else
                    Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles the interval data property changed event.
        /// </summary>
        /// <param name="sender">The data.</param>
        /// <param name="e">Provides data for the property changed event.</param>
        private void IntervalDataChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IntervalSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName != nameof(Data.PlottingPosition))
                    CalculatePlottingPositions();
                else
                    Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles the threshold data property changed event.
        /// </summary>
        /// <param name="sender">The data.</param>
        /// <param name="e">Provides data for the property changed event.</param>
        private void ThresholdDataChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ThresholdSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName != nameof(Data.PlottingPosition))
                    CalculatePlottingPositions();
                else
                    Interlocked.Increment(ref _plottingPositionVersion);
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        protected virtual void RaisePropertyChange(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        /// <summary>
        /// Validates the current state of the data frame and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the data frame passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the data frame is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            var messages = new List<string>();
            bool isValid = true;

            if (!double.IsFinite(PlottingParameter) || PlottingParameter < 0d || PlottingParameter >= 1d)
            {
                messages.Add("Error: The plotting parameter must be finite, greater than or equal to 0, and less than 1.");
                isValid = false;
            }

            var exactValidation = ExactSeries.Validate();
            if (!exactValidation.IsValid)
            {
                messages.AddRange(exactValidation.ValidationMessages);
                isValid = false;
            }

            var uncertainValidation = UncertainSeries.Validate(this);
            if (!uncertainValidation.IsValid)
            {
                messages.AddRange(uncertainValidation.ValidationMessages);
                isValid = false;
            }

            var intervalValidation = IntervalSeries.Validate(this);
            if (!intervalValidation.IsValid)
            {
                messages.AddRange(intervalValidation.ValidationMessages);
                isValid = false;
            }

            var thresholdValidation = ThresholdSeries.Validate();
            if (!thresholdValidation.IsValid)
            {
                messages.AddRange(thresholdValidation.ValidationMessages);
                isValid = false;
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns the total record length of the data frame.
        /// </summary>
        /// <returns>The total number of data points across all series.</returns>
        public int TotalRecordLength()
        {
            int n = ExactSeries.Count + UncertainSeries.Count + IntervalSeries.Count;
            foreach(ThresholdData threshold in ThresholdSeries)
            {
                n += threshold.NumberBelow + threshold.NumberAbove;
            }
            return n;
        }

        /// <summary>
        /// Computes the relative frequency of data points less than or equal to zero.
        /// </summary>
        /// <returns>The relative frequency of data points less than or equal to zero.</returns>
        public double ZeroValueRelativeFrequency()
        {
            double totalCount = 0;
            double totalZeroCount = 0;
            if (ExactSeries.Count > 0)
            {
                totalCount = ExactSeries.Count;
                totalZeroCount += ExactSeries.Where((x) => x.Value <= 0.0d).Count();
            }
            if (UncertainSeries.Count > 0)
            {
                totalCount += UncertainSeries.Count;
                totalZeroCount += UncertainSeries.Where((x) => x.Value <= 0.0d).Count();
            }
            if (IntervalSeries.Count > 0)
            {
                totalCount += IntervalSeries.Count;
                totalZeroCount += IntervalSeries.Where((x) => x.Value <= 0.0d).Count();
            }
            if (totalCount == 0) return 0d;
            return totalZeroCount / totalCount;
        }

        /// <summary>
        /// Process the threshold data to ensure exclusivity by adjusting counts for overlapping data.
        /// </summary>
        /// <remarks>
        /// Thread-safe. Serialized via the same <c>_syncRoot</c> lock as <see cref="CreateFullTimeSeries"/>
        /// so that a concurrent rebuild observes a fully-processed threshold state rather than a
        /// torn view of <see cref="ThresholdData.NumberAbove"/>/<see cref="ThresholdData.NumberBelow"/>.
        /// Each pass starts from the retained user-supplied exceedance count, so repeated calls are
        /// idempotent and input mutations can restore a previously reduced effective count. Item-level
        /// notifications are suppressed while counts are updated; one aggregate
        /// <see cref="ThresholdSeries"/> notification is raised after the lock when any effective value changes.
        /// </remarks>
        public void ProcessThresholdSeries()
        {
            bool countsChanged = false;
            lock (_syncRoot)
            {
                // Adjust the number above and below for thresholds from the immutable user input.
                for (int i = 0; i < ThresholdSeries.Count; i++)
                {
                    var thresholdData = (ThresholdData)ThresholdSeries[i];
                    int nAbove = thresholdData.SourceNumberAbove;
                    int nBelow = thresholdData.Duration - nAbove;
                    // Check interval data
                    for (int j = 0; j < IntervalSeries.Count; j++)
                    {
                        var intervalData = IntervalSeries[j];
                        if (intervalData.Index >= thresholdData.StartIndex && intervalData.Index <= thresholdData.EndIndex)
                        {
                            nBelow -= 1;
                        }
                    }
                    // Check uncertain data
                    for (int j = 0; j < UncertainSeries.Count; j++)
                    {
                        if (UncertainSeries[j].Index >= thresholdData.StartIndex && UncertainSeries[j].Index <= thresholdData.EndIndex)
                        {
                            nBelow -= 1;
                        }
                    }
                    // Check exact data
                    for (int j = 0; j < ExactSeries.Count; j++)
                    {
                        if (ExactSeries[j].Index >= thresholdData.StartIndex && ExactSeries[j].Index <= thresholdData.EndIndex)
                        {
                            nBelow -= 1;
                        }
                    }
                    // Zero out the effective NumberAbove when explicit data account for every remaining year.
                    countsChanged |= thresholdData.SetProcessedCounts(
                        nBelow == 0 ? 0 : nAbove,
                        Math.Max(0, nBelow));
                }
            }

            if (countsChanged)
                RaisePropertyChange(nameof(ThresholdSeries));
        }

        /// <summary>
        /// Creates a full time series in chronological order by expanding threshold data and combining all series.
        /// </summary>
        /// <remarks>
        /// Thread-safe. Builds the series into a local list and publishes it atomically by assigning
        /// the field inside <c>_syncRoot</c>. Readers that captured the prior list reference (e.g.,
        /// through the <see cref="FullTimeSeries"/> fast-path getter) continue iterating their
        /// immutable-by-convention snapshot safely.
        /// </remarks>
        public void CreateFullTimeSeries()
        {
            lock (_syncRoot)
            {
                // Snapshot each series before iterating. The lock only serializes
                // CreateFullTimeSeries-vs-itself; the series have their own (lockless)
                // mutation path. A concurrent List<T>.RemoveAt nulls the last slot,
                // which could otherwise surface as NullReferenceException downstream.
                // Filter out any null entries the snapshot may capture mid-removal.
                var exactSnapshot = SnapshotNonNull(ExactSeries);
                var uncertainSnapshot = SnapshotNonNull(UncertainSeries);
                var intervalSnapshot = SnapshotNonNull(IntervalSeries);
                var thresholdSnapshot = SnapshotNonNull(ThresholdSeries);

                var newList = new List<Data>();

                // Build a single HashSet of occupied indexes (Exact, Interval, Uncertain)
                // once per call. Replaces the previous triple LINQ.Where().Count() per
                // threshold-day, reducing complexity from O(M·D·(E+I+U)) to
                // O(M·D + E+I+U).
                var occupied = new HashSet<int>();
                for (int k = 0; k < exactSnapshot.Length; k++) occupied.Add(exactSnapshot[k].Index);
                for (int k = 0; k < intervalSnapshot.Length; k++) occupied.Add(intervalSnapshot[k].Index);
                for (int k = 0; k < uncertainSnapshot.Length; k++) occupied.Add(uncertainSnapshot[k].Index);

                // Threshold data
                for (int i = 0; i < thresholdSnapshot.Length; i++)
                {
                    var threshold = (ThresholdData)thresholdSnapshot[i];
                    // Left thresholds
                    for (int j = threshold.StartIndex; j <= threshold.EndIndex - threshold.NumberAbove; j++)
                    {
                        if (!occupied.Contains(j))
                        {
                            var tData = threshold.Clone();
                            tData.StartIndex = j;
                            tData.EndIndex = j;
                            tData.NumberAbove = 0;
                            tData.NumberBelow = 1;
                            newList.Add(tData);
                        }
                    }
                    // Right thresholds
                    for (int j = threshold.EndIndex - threshold.NumberAbove + 1; j <= threshold.EndIndex; j++)
                    {
                        if (!occupied.Contains(j))
                        {
                            var tData = threshold.Clone();
                            tData.StartIndex = j;
                            tData.EndIndex = j;
                            tData.NumberAbove = 1;
                            tData.NumberBelow = 0;
                            newList.Add(tData);
                        }
                    }
                }
                // Exact data
                for (int i = 0; i < exactSnapshot.Length; i++)
                {
                    newList.Add(((ExactData)exactSnapshot[i]).Clone());
                }
                // Uncertain data
                for (int i = 0; i < uncertainSnapshot.Length; i++)
                {
                    newList.Add(((UncertainData)uncertainSnapshot[i]).Clone());
                }
                // Interval data
                for (int i = 0; i < intervalSnapshot.Length; i++)
                {
                    newList.Add(((IntervalData)intervalSnapshot[i]).Clone());
                }
                // sort data
                newList.Sort((x, y) => x.Index.CompareTo(y.Index));

                // Atomic publish. The lock's release fence pairs with Volatile.Read on the getter's fast path.
                _fullTimeSeries = newList;
            }
        }

        /// <summary>
        /// Defensive snapshot of a data series that tolerates a concurrent mutator.
        /// </summary>
        /// <remarks>
        /// Retries up to a few times when the underlying list mutates mid-copy. Depending
        /// on the exact race, LINQ snapshotting can surface either an
        /// <see cref="InvalidOperationException"/> from enumeration or an
        /// <see cref="ArgumentException"/> from <see cref="ICollection{T}.CopyTo(T[], int)"/>
        /// after the source count changes. Null entries left behind by a concurrent
        /// <see cref="List{T}.RemoveAt(int)"/> are filtered from the snapshot.
        /// </remarks>
        private static Data[] SnapshotNonNull(DataSeries series)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var array = series.ToArray();
                    int writeIdx = 0;
                    for (int readIdx = 0; readIdx < array.Length; readIdx++)
                    {
                        if (array[readIdx] is not null)
                            array[writeIdx++] = array[readIdx];
                    }
                    if (writeIdx == array.Length) return array;
                    var trimmed = new Data[writeIdx];
                    Array.Copy(array, trimmed, writeIdx);
                    return trimmed;
                }
                catch (InvalidOperationException) when (attempt < 3)
                {
                    // Concurrent mutation collided with enumeration; retry.
                }
                catch (ArgumentException) when (attempt < 3)
                {
                    // Concurrent mutation changed Count between allocation and CopyTo; retry.
                }
            }
            return Array.Empty<Data>();
        }

        #region Hypothesis Testing

        /// <summary>
        /// Clear the low outlier results. 
        /// </summary>
        public void ClearLowOutliers()
        {
            for (int i = 0; i < ExactSeries.Count; i++)
                ((ExactData)ExactSeries[i]).IsLowOutlier = false;
            _numberOfLowOutliers = 0;
        }

        /// <summary>
        /// Estimates and sets the low outliers using the Multiple Grubbs Beck Test (MGBT). This is only performed on exact data. 
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when exact data series has errors or insufficient data.</exception>
        public void SetLowOutliersFromMGBT()
        {
            if (!ExactSeries.Validate().IsValid) throw new ArgumentException("The exact data series has errors.", nameof(ExactSeries));
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before evaluating low outliers.", nameof(ExactSeries));

            ExactSeries.SuppressCollectionChanged = true;
            ClearLowOutliers();
            LowOutlierThreshold = 0;

            // Add all data point values to an array
            var values = ExactSeries.Select(x => x.Value).ToArray();

            // Compute the number of low outliers using the Multiple Grubbs Beck Test
            _numberOfLowOutliers = MultipleGrubbsBeckTest.Function(values);

            // Set the threshold value as first value larger than N
            Array.Sort(values);
            if (_numberOfLowOutliers > 0)
            {
                LowOutlierThreshold = values[_numberOfLowOutliers];
            }            
            else
            {
                LowOutlierThreshold = 0;
            }

            // Set all exact data points to IsLowOutlier = true if less than threshold
            for (int i = 0; i < ExactSeries.Count; i++)
            {
                if (ExactSeries[i].Value < _lowOutlierThreshold)
                {
                    ((ExactData)ExactSeries[i]).IsLowOutlier = true;
                }
                else
                {
                    ((ExactData)ExactSeries[i]).IsLowOutlier = false;
                }               
            }

            ExactSeries.SuppressCollectionChanged = false;
            RaisePropertyChange("LowOutliers");
        }

        /// <summary>
        /// Estimates and sets the low outliers using low outlier threshold value. This is only performed on exact data.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when exact data series has errors, insufficient data, or threshold would censor more than 50%.</exception>
        public void SetLowOutliersFromThreshold()
        {
            if (!ExactSeries.Validate().IsValid) throw new ArgumentException("The exact data series has errors.", nameof(ExactSeries));
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before evaluating low outliers.", nameof(ExactSeries));
            if (LowOutlierThreshold > ExactSeries.UpperMiddleValue) throw new ArgumentException("The low outlier threshold value cannot be set to a value that would censor more than 50 percent of the values.", nameof(LowOutlierThreshold));

            ExactSeries.SuppressCollectionChanged = true;

            // Set all exact data points to IsLowOutlier = true if less than threshold
            _numberOfLowOutliers = 0;
            for (int i = 0; i < ExactSeries.Count; i++)
            {
                if (ExactSeries[i].Value < LowOutlierThreshold)
                {
                    ((ExactData)ExactSeries[i]).IsLowOutlier = true;
                    _numberOfLowOutliers += 1;
                }
                else
                {
                    ((ExactData)ExactSeries[i]).IsLowOutlier = false;
                }
            }

            ExactSeries.SuppressCollectionChanged = false;
            RaisePropertyChange("LowOutliers");
        }

        /// <summary>
        /// The Jarque-Bera test for normality. This is only performed on exact data. 
        /// </summary>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the Jarque-Bera test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double JarqueBeraTest(bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToList() : ExactSeries.Select(x => x.Value).ToList();
            return HypothesisTests.JarqueBeraTest(sample);
        }

        /// <summary>
        /// The Ljung-Box test whether the autocorrelations of the data are different from zero.
        /// </summary>
        /// <param name="lagMax">The max lag to evaluate.</param>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the Ljung-Box test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double LjungBoxTest(int lagMax = -1, bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToList() : ExactSeries.Select(x => x.Value).ToList();
            return HypothesisTests.LjungBoxTest(sample, lagMax);
        }

        /// <summary>
        /// The t-test determines if there is a significant difference between the means of two samples drawn from populations with equal variances. This is only performed on exact data. 
        /// </summary>
        /// <param name="index">The index location to split the samples.</param>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the T-test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data or invalid index.</exception>
        public double EqualVarianceTtest(int index, bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample1 = useLog10 ? ExactSeries.Where(x => x.Index < index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index < index).Select(x => x.Value).ToList();
            var sample2 = useLog10 ? ExactSeries.Where(x => x.Index >= index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index >= index).Select(x => x.Value).ToList();
            if (sample1.Count < 2 || sample2.Count < 2) throw new ArgumentException("Invalid index.", nameof(index));
            return HypothesisTests.EqualVarianceTtest(sample1, sample2);
        }

        /// <summary>
        /// The t-test determines if there is a significant difference between the means of two samples drawn from populations with unequal variances. This is only performed on exact data. 
        /// </summary>
        /// <param name="index">The index location to split the samples.</param>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the T-test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data or invalid index.</exception>
        public double UnequalVarianceTtest(int index, bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample1 = useLog10 ? ExactSeries.Where(x => x.Index < index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index < index).Select(x => x.Value).ToList();
            var sample2 = useLog10 ? ExactSeries.Where(x => x.Index >= index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index >= index).Select(x => x.Value).ToList();
            if (sample1.Count < 2 || sample2.Count < 2) throw new ArgumentException("Invalid index.", nameof(index));
            return HypothesisTests.UnequalVarianceTtest(sample1, sample2);
        }

        /// <summary>
        /// The F-test for significantly different variances. This is only performed on exact data. 
        /// </summary>
        /// <param name="index">The index location to split the samples.</param>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the F-test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data or invalid index.</exception>
        public double Ftest(int index, bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample1 = useLog10 ? ExactSeries.Where(x => x.Index < index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index < index).Select(x => x.Value).ToList();
            var sample2 = useLog10 ? ExactSeries.Where(x => x.Index >= index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index >= index).Select(x => x.Value).ToList();
            if (sample1.Count < 2 || sample2.Count < 2) throw new ArgumentException("Invalid index.", nameof(index));
            return HypothesisTests.Ftest(sample1, sample2);
        }

        /// <summary>
        /// The Linear Trend test for stationarity (trend). This is only performed on exact data.
        /// </summary>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the linear trend test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double LinearTrendTest(bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var indexes = ExactSeries.Select(x => (double)x.Index).ToArray();
            var values = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToArray() : ExactSeries.Select(x => x.Value).ToArray();
            var xVals = new Matrix(indexes);
            var yVals = new Vector(values);
            var lm = new LinearRegression(xVals, yVals, true);
            var tdist = new StudentT(lm.DegreesOfFreedom);
            double d = Math.Abs(lm.Parameters[1] / lm.ParameterStandardErrors[1]);
            return (1 - tdist.CDF(Math.Abs(lm.Parameters[1] / lm.ParameterStandardErrors[1]))) * 2;
        }

        /// <summary>
        /// The Gaussian Mixture Model Unimodality Test.
        /// </summary>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>Returns the p-value of the test statistic.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double UnimodalityTest(bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToList() : ExactSeries.Select(x => x.Value).ToList();
            return HypothesisTests.UnimodalityTest(sample);
        }


        /// <summary>
        /// The Wald and Wolfowitz test for independence and stationarity (trend). This is only performed on exact data. 
        /// </summary>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the Wald-Wolfowitz test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double WaldWolfowitzTest(bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToList() : ExactSeries.Select(x => x.Value).ToList();
            return HypothesisTests.WaldWolfowitzTest(sample);
        }

        /// <summary>
        /// The Mann-Whitney test for homogeneity and stationarity (jump). This is only performed on exact data. 
        /// </summary>
        /// <param name="index">The index location to split the samples.</param>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the Mann-Whitney test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data or invalid index.</exception>
        public double MannWhitneyTest(int index, bool useLog10 = false)
        {
            if (ExactSeries.Count < 20) throw new ArgumentException("The exact data series must have at least 20 items before performing this hypothesis test.", nameof(ExactSeries));
            var sample1 = useLog10 ? ExactSeries.Where(x => x.Index < index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index < index).Select(x => x.Value).ToList();
            var sample2 = useLog10 ? ExactSeries.Where(x => x.Index >= index).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index >= index).Select(x => x.Value).ToList();
            if (sample1.Count < 3 || sample2.Count < 3) throw new ArgumentException("Invalid index.", nameof(index));
            return sample1.Count <= sample2.Count ?  HypothesisTests.MannWhitneyTest(sample1, sample2) : HypothesisTests.MannWhitneyTest(sample2, sample1);
        }

        /// <summary>
        /// The Mann-Kendall test for homogeneity and stationarity (trend). This is only performed on exact data. 
        /// </summary>
        /// <param name="useLog10">Determines whether to use real-space or log base 10 data. Default = false, use real-space.</param>
        /// <returns>The p-value for the Mann-Kendall test.</returns>
        /// <exception cref="ArgumentException">Thrown when exact data series has insufficient data.</exception>
        public double MannKendallTest(bool useLog10 = false)
        {
            if (ExactSeries.Count < 10) throw new ArgumentException("The exact data series must have at least 10 items before performing hypothesis tests.", nameof(ExactSeries));
            var sample = useLog10 ? ExactSeries.Select(x => x.Log10Value).ToList() : ExactSeries.Select(x => x.Value).ToList();
            return HypothesisTests.MannKendallTest(sample);
        }

        /// <summary>
        /// Returns hypothesis test results. 
        /// </summary>
        /// <param name="index">The index used to split the samples.</param>
        /// <param name="useLog10">Determines whether to use real-space data or log10 data.</param>
        /// <returns>Dictionary containing test names and their p-values.</returns>
        public Dictionary<string, double> SummaryHypothesisTest(int index = -1, bool useLog10 = false)
        {
            var result = new Dictionary<string, double>();
            if (ExactSeries.Count < 10)
            {
                result.Add("Jarque-Bera test for normality", double.NaN);
                result.Add("Ljung-Box test for independence", double.NaN);
                result.Add("Wald-Wolfowitz test for independence and stationarity (trend)", double.NaN);
                result.Add("Mann-Whitney test for homogeneity and stationarity (jump)", double.NaN);
                result.Add("Mann-Kendall test for homogeneity and stationarity (trend)", double.NaN);
                result.Add("Linear trend test for stationarity (trend)", double.NaN);
                result.Add("Equal variance t-test for differences in the means of two samples", double.NaN);
                result.Add("Unequal variance t-test for differences in the means of two samples", double.NaN);
                result.Add("F-test for differences in the variances of two samples", double.NaN);
                result.Add("Mixture model test for unimodality", double.NaN);
            }
            else
            {
                var indexes = useLog10 ? ExactSeries.Where(x => x.Value > 0).Select(x => (double)x.Index).ToArray() : ExactSeries.Select(x => (double)x.Index).ToArray();
                var values = useLog10 ? ExactSeries.Where(x => x.Value > 0).Select(x => x.Log10Value).ToArray() : ExactSeries.Select(x => x.Value).ToArray();

                // Clamp the split index to the data range so that both samples have at least one value.
                // An out-of-range index (e.g., 0 from an uninitialized slider) would leave one sample empty,
                // causing two-sample tests (t-test, F-test) to throw.
                int minIndex = ExactSeries[0].Index;
                int maxIndex = ExactSeries[ExactSeries.Count - 1].Index;
                if (index < 0 || index <= minIndex || index > maxIndex)
                    index = ExactSeries[(int)((double)values.Length / 2)].Index;

                var v1 = useLog10 ? ExactSeries.Where(x => x.Index < index && x.Value > 0).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index < index).Select(x => x.Value).ToList();
                var v2 = useLog10 ? ExactSeries.Where(x => x.Index >= index && x.Value > 0).Select(x => x.Log10Value).ToList() : ExactSeries.Where(x => x.Index >= index).Select(x => x.Value).ToList();

                try
                {
                    result.Add("Jarque-Bera test for normality", HypothesisTests.JarqueBeraTest(values));
                    result.Add("Ljung-Box test for independence", HypothesisTests.LjungBoxTest(values));
                    result.Add("Wald-Wolfowitz test for independence and stationarity (trend)", HypothesisTests.WaldWolfowitzTest(values));
                    result.Add("Mann-Whitney test for homogeneity and stationarity (jump)", HypothesisTests.MannWhitneyTest(v1.Count <= v2.Count ? v1 : v2, v1.Count > v2.Count ? v1 : v2));
                    result.Add("Mann-Kendall test for homogeneity and stationarity (trend)", HypothesisTests.MannKendallTest(values));
                    result.Add("Linear trend test for stationarity (trend)", HypothesisTests.LinearTrendTest(indexes, values));
                    result.Add("Equal variance t-test for differences in the means of two samples", HypothesisTests.EqualVarianceTtest(v1, v2));
                    result.Add("Unequal variance t-test for differences in the means of two samples", HypothesisTests.UnequalVarianceTtest(v1, v2));
                    result.Add("F-test for differences in the variances of two samples", HypothesisTests.Ftest(v1, v2));
                    result.Add("Mixture model test for unimodality", HypothesisTests.UnimodalityTest(values));
                }
                catch(Exception ex)
                {
                    // FIX: Log the error for debugging
                    Debug.WriteLine($"Error in hypothesis testing: {ex.Message}");

                    result.Clear();
                    result.Add("Jarque-Bera test for normality", double.NaN);
                    result.Add("Ljung-Box test for independence", double.NaN);
                    result.Add("Wald-Wolfowitz test for independence and stationarity (trend)", double.NaN);
                    result.Add("Mann-Whitney test for homogeneity and stationarity (jump)", double.NaN);
                    result.Add("Mann-Kendall test for homogeneity and stationarity (trend)", double.NaN);
                    result.Add("Linear trend test for stationarity (trend)", double.NaN);
                    result.Add("Equal variance t-test for differences in the means of two samples", double.NaN);
                    result.Add("Unequal variance t-test for differences in the means of two samples", double.NaN);
                    result.Add("F-test for differences in the variances of two samples", double.NaN);
                    result.Add("Mixture model test for unimodality", double.NaN);
                }
            }

            return result;
        }

        #endregion

        #region Plotting Positions

        /// <summary>
        /// Provides plotting positions for censored data using the Hirsch-Stedinger plotting position formula.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when plotting inputs, threshold windows, processed counts, or a computed probability are invalid.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This routine provides plotting positions for censored data using the Hirsch/Stedinger plotting position 
        /// formula (Hirsch, 1987).
        /// </para>
        /// <para>
        ///     References:
        ///     <list type="bullet">
        ///         <item><description>
        ///         Plotting positions for historical floods and their precision. 
        ///         Hirsch, R.M. and Stedinger, J.R. Water Resources Research. 1987. 
        ///         </description></item>
        ///        <item><description>
        ///         An algorithm for computing moments-based flood quantile estimates when historical flood information is available. 
        ///         Cohn, TA and Lane, WL and Baier, WG. Water Resources Research. 1997. 
        ///         </description></item>
        ///         <item><description>
        ///         Confidence intervals for Expected Moments Algorithm flood quantile estimates. 
        ///         Cohn, T.A. and Lane, W.L. and Stedinger, J.R. Water Resources Research. 2001. 
        ///         </description></item>
        ///         <item><description>
        ///         This subroutine is based on the description provided in Appendix 5 of Bulletin 17C:
        ///         U.S. Geological Survey. (2018). Guidelines for Determining Flood Flow Frequency Bulletin 17C.
        ///         </description></item>
        ///         <item><description>
        ///         FORTRAN source code for this method can be found in PeakfqSA available at:
        ///         <see href="https://sites.google.com/a/alumni.colostate.edu/jengland/resources"/> 
        ///         </description></item>
        ///      </list>
        /// </para>
        /// <para>
        /// The implementation is a documented port of peakFQ's ARRANGE2, PPLOT2, and PLPOS
        /// sequence. Each explicit observation is classified against the perception threshold
        /// covering its own index; this classification changes plotting ranks only and never
        /// changes the observation type or value.
        /// </para>
        /// <para>
        /// After threshold counts are processed, observations and distinct levels are arranged
        /// with one global value sort plus binary threshold lookups to support interactive edits.
        /// </para>
        /// </remarks>
        public void CalculatePlottingPositions()
        {
            double alpha = PlottingParameter;
            if (!double.IsFinite(alpha) || alpha < 0d || alpha >= 1d)
                throw new InvalidOperationException(
                    "Plotting positions require a finite plotting parameter greater than or equal to zero and less than one.");

            // NumberBelow and NumberAbove are inputs to the ARRANGE2 counts.
            ProcessThresholdSeries();
            Interlocked.Increment(ref _plottingPositionVersion);

            var exactSeries = ExactSeries;
            var uncertainSeries = UncertainSeries;
            var intervalSeries = IntervalSeries;
            var thresholdSeries = ThresholdSeries;

            bool exactWasSuppressed = exactSeries.SuppressCollectionChanged;
            bool uncertainWasSuppressed = uncertainSeries.SuppressCollectionChanged;
            bool intervalWasSuppressed = intervalSeries.SuppressCollectionChanged;
            bool thresholdWasSuppressed = thresholdSeries.SuppressCollectionChanged;

            exactSeries.SuppressCollectionChanged = true;
            uncertainSeries.SuppressCollectionChanged = true;
            intervalSeries.SuppressCollectionChanged = true;
            thresholdSeries.SuppressCollectionChanged = true;

            try
            {
                var exactSnapshot = SnapshotNonNull(exactSeries);
                var uncertainSnapshot = SnapshotNonNull(uncertainSeries);
                var intervalSnapshot = SnapshotNonNull(intervalSeries);
                var thresholdDataSnapshot = SnapshotNonNull(thresholdSeries);
                var thresholdsByIndex = new ThresholdData[thresholdDataSnapshot.Length];

                for (int i = 0; i < thresholdDataSnapshot.Length; i++)
                {
                    var threshold = (ThresholdData)thresholdDataSnapshot[i];
                    if (!double.IsFinite(threshold.Value))
                        throw new InvalidOperationException("Perception threshold values must be finite.");
                    if (threshold.StartIndex > threshold.EndIndex)
                        throw new InvalidOperationException("Perception threshold start indexes must not exceed their end indexes.");
                    if (threshold.NumberBelow < 0 || threshold.NumberAbove < 0 ||
                        (long)threshold.NumberBelow + threshold.NumberAbove > threshold.Duration)
                    {
                        throw new InvalidOperationException(
                            "Processed perception-threshold counts must be nonnegative and must not exceed the threshold duration.");
                    }

                    thresholdsByIndex[i] = threshold;
                }

                Array.Sort(thresholdsByIndex, (left, right) =>
                {
                    int comparison = left.StartIndex.CompareTo(right.StartIndex);
                    return comparison != 0 ? comparison : left.EndIndex.CompareTo(right.EndIndex);
                });

                for (int i = 1; i < thresholdsByIndex.Length; i++)
                {
                    if (thresholdsByIndex[i].StartIndex <= thresholdsByIndex[i - 1].EndIndex)
                        throw new InvalidOperationException("Perception threshold windows must not overlap.");
                }

                var explicitData = new List<Data>(
                    exactSnapshot.Length + uncertainSnapshot.Length + intervalSnapshot.Length);
                explicitData.AddRange(intervalSnapshot);
                explicitData.AddRange(uncertainSnapshot);
                explicitData.AddRange(exactSnapshot);

                var occupiedIndexes = new HashSet<int>();
                var thresholdLevels = new SortedSet<double>();
                var observations =
                    new List<(Data Source, double Threshold, int Ordinal, bool IsDetected)>(explicitData.Count);

                for (int i = 0; i < thresholdsByIndex.Length; i++)
                {
                    var threshold = thresholdsByIndex[i];
                    if (threshold.NumberBelow > 0 || threshold.NumberAbove > 0)
                        thresholdLevels.Add(threshold.Value);
                }

                for (int i = 0; i < explicitData.Count; i++)
                {
                    Data source = explicitData[i];
                    if (!double.IsFinite(source.Value))
                        throw new InvalidOperationException("Explicit observation values must be finite.");

                    occupiedIndexes.Add(source.Index);
                    ThresholdData? threshold = FindThresholdForPlotting(thresholdsByIndex, source.Index);
                    double thresholdValue = threshold?.Value ?? double.NegativeInfinity;
                    thresholdLevels.Add(thresholdValue);
                    observations.Add((source, thresholdValue, i, source.Value >= thresholdValue));
                }

                double[] levels = thresholdLevels.ToArray();
                if (levels.Length > 0)
                {
                    var detectedByLevel =
                        new List<(Data Source, double Value, int Index, int Ordinal)>[levels.Length];
                    var censoredByLevel =
                        new List<(Data Source, int Index, int Ordinal)>[levels.Length];
                    var leftPlaceholderIndexes = new List<int>[levels.Length];

                    for (int i = 0; i < levels.Length; i++)
                    {
                        detectedByLevel[i] = new List<(Data Source, double Value, int Index, int Ordinal)>();
                        censoredByLevel[i] = new List<(Data Source, int Index, int Ordinal)>();
                        leftPlaceholderIndexes[i] = new List<int>();
                    }

                    long rightPlaceholderCount = 0L;
                    for (int i = 0; i < thresholdsByIndex.Length; i++)
                    {
                        ThresholdData threshold = thresholdsByIndex[i];
                        if (threshold.NumberBelow == 0 && threshold.NumberAbove == 0)
                            continue;

                        int levelIndex = Array.BinarySearch(levels, threshold.Value);
                        if (levelIndex < 0)
                            throw new InvalidOperationException("A processed perception threshold could not be arranged.");

                        HashSet<int>? selectedLeftIndexes =
                            threshold.NumberAbove > 0 ? new HashSet<int>() : null;
                        int selectedBelow = 0;
                        for (long candidate = threshold.StartIndex;
                             candidate <= threshold.EndIndex && selectedBelow < threshold.NumberBelow;
                             candidate++)
                        {
                            int index = (int)candidate;
                            if (occupiedIndexes.Contains(index))
                                continue;

                            leftPlaceholderIndexes[levelIndex].Add(index);
                            selectedLeftIndexes?.Add(index);
                            selectedBelow++;
                        }

                        if (selectedBelow != threshold.NumberBelow)
                        {
                            throw new InvalidOperationException(
                                "The processed number below a perception threshold exceeds its available unoccupied indexes.");
                        }

                        int selectedAbove = 0;
                        for (long candidate = threshold.EndIndex;
                             candidate >= threshold.StartIndex && selectedAbove < threshold.NumberAbove;
                             candidate--)
                        {
                            int index = (int)candidate;
                            if (occupiedIndexes.Contains(index) ||
                                (selectedLeftIndexes != null && selectedLeftIndexes.Contains(index)))
                            {
                                continue;
                            }

                            selectedAbove++;
                        }

                        if (selectedAbove != threshold.NumberAbove)
                        {
                            throw new InvalidOperationException(
                                "The processed number above a perception threshold exceeds its available unoccupied indexes.");
                        }

                        rightPlaceholderCount += threshold.NumberAbove;
                    }

                    var legacyAboveOrder = new List<Data>();
                    var legacyBelowOrder = new List<Data>();
                    double minimumFiniteThreshold = thresholdsByIndex.Length > 0
                        ? thresholdsByIndex.Min(threshold => threshold.Value)
                        : double.PositiveInfinity;

                    for (int i = 0; i < observations.Count; i++)
                    {
                        var observation = observations[i];
                        if (observation.IsDetected)
                        {
                            if (observation.Source.Value >= minimumFiniteThreshold)
                                legacyAboveOrder.Add(observation.Source);
                            else
                                legacyBelowOrder.Add(observation.Source);
                        }
                        else
                        {
                            int levelIndex = Array.BinarySearch(levels, observation.Threshold);
                            if (levelIndex < 0)
                                throw new InvalidOperationException("A censored observation threshold could not be arranged.");

                            censoredByLevel[levelIndex].Add((
                                observation.Source,
                                observation.Source.Index,
                                observation.Ordinal));
                        }
                    }

                    // Preserve only the legacy tie permutation: the former routine sorted
                    // finite values in separate above/below lists split at the global minimum
                    // threshold. Detection status itself still comes from the observation's
                    // own threshold, as required by ARRANGE2.
                    legacyAboveOrder.Sort((left, right) => -1 * left.Value.CompareTo(right.Value));
                    legacyBelowOrder.Sort((left, right) => -1 * left.Value.CompareTo(right.Value));

                    int detectedOrdinal = 0;
                    for (int group = 0; group < 2; group++)
                    {
                        List<Data> ordered = group == 0 ? legacyAboveOrder : legacyBelowOrder;
                        for (int i = 0; i < ordered.Count; i++)
                        {
                            Data observation = ordered[i];
                            int levelIndex = FindPlottingLevel(levels, observation.Value);
                            detectedByLevel[levelIndex].Add((
                                observation,
                                observation.Value,
                                observation.Index,
                                detectedOrdinal++));
                        }
                    }
                    var detectedCount = new long[levels.Length];
                    var censoredCount = new long[levels.Length];
                    for (int i = 0; i < levels.Length; i++)
                    {
                        detectedCount[i] = detectedByLevel[i].Count;
                        censoredCount[i] = (long)censoredByLevel[i].Count + leftPlaceholderIndexes[i].Count;
                    }

                    // NumberAbove entries are right-censored values. PLPOS orders them above
                    // every finite observation, so they occupy the end of the highest band.
                    detectedCount[^1] += rightPlaceholderCount;

                    // ARRANGE2 cumulative NB recurrence.
                    var notObservableAtLevel = new long[levels.Length];
                    notObservableAtLevel[0] = censoredCount[0];
                    for (int i = 1; i < levels.Length; i++)
                    {
                        notObservableAtLevel[i] =
                            notObservableAtLevel[i - 1] + censoredCount[i] + detectedCount[i - 1];
                    }

                    // PPLOT2 computes nonexceedance interval boundaries from high to low thresholds.
                    var intervalBoundary = new double[levels.Length + 1];
                    intervalBoundary[^1] = 0d;
                    for (int i = levels.Length - 1; i >= 0; i--)
                    {
                        double conditionalDetectionProbability =
                            detectedCount[i] /
                            (Math.Max(1L, detectedCount[i]) + (double)notObservableAtLevel[i]);
                        intervalBoundary[i] =
                            intervalBoundary[i + 1] +
                            (1d - intervalBoundary[i + 1]) * conditionalDetectionProbability;
                    }

                    for (int i = 0; i < levels.Length; i++)
                    {
                        var detected = detectedByLevel[i];
                        double denominator = detectedCount[i] + 1d - 2d * alpha;
                        for (int j = 0; j < detected.Count; j++)
                        {
                            double nonexceedanceProbability =
                                (1d - intervalBoundary[i]) +
                                (intervalBoundary[i] - intervalBoundary[i + 1]) *
                                ((detected.Count - j - alpha) / denominator);
                            SetStrictPlottingPosition(detected[j].Source, 1d - nonexceedanceProbability);
                        }

                        var censored = censoredByLevel[i];
                        censored.Sort((left, right) =>
                        {
                            int comparison = left.Index.CompareTo(right.Index);
                            return comparison != 0 ? comparison : left.Ordinal.CompareTo(right.Ordinal);
                        });

                        var placeholderIndexes = leftPlaceholderIndexes[i];
                        placeholderIndexes.Sort();

                        long rank = 0L;
                        int placeholderCursor = 0;
                        denominator = censoredCount[i] + 1d - 2d * alpha;
                        for (int j = 0; j < censored.Count; j++)
                        {
                            while (placeholderCursor < placeholderIndexes.Count &&
                                   placeholderIndexes[placeholderCursor] < censored[j].Index)
                            {
                                placeholderCursor++;
                                rank++;
                            }

                            rank++;
                            double nonexceedanceProbability =
                                (1d - intervalBoundary[i]) * ((rank - alpha) / denominator);
                            SetStrictPlottingPosition(censored[j].Source, 1d - nonexceedanceProbability);
                        }
                    }
                }
            }
            finally
            {
                exactSeries.SuppressCollectionChanged = exactWasSuppressed;
                uncertainSeries.SuppressCollectionChanged = uncertainWasSuppressed;
                intervalSeries.SuppressCollectionChanged = intervalWasSuppressed;
                thresholdSeries.SuppressCollectionChanged = thresholdWasSuppressed;
            }

            RaisePropertyChange("PlottingPosition");
        }

        /// <summary>
        /// Finds the perception threshold covering an observation index.
        /// </summary>
        /// <param name="thresholdsByIndex">Nonoverlapping thresholds sorted by start index.</param>
        /// <param name="index">Observation index to locate.</param>
        /// <returns>The covering threshold, or <c>null</c> when the observation is outside all threshold windows.</returns>
        /// <remarks>
        /// Binary search keeps per-observation threshold association O(log m), where m is the threshold count.
        /// Observations outside all windows use a synthetic negative-infinity threshold in ARRANGE2.
        /// </remarks>
        private static ThresholdData? FindThresholdForPlotting(ThresholdData[] thresholdsByIndex, int index)
        {
            int low = 0;
            int high = thresholdsByIndex.Length - 1;
            int candidate = -1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (thresholdsByIndex[middle].StartIndex <= index)
                {
                    candidate = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (candidate >= 0 && index <= thresholdsByIndex[candidate].EndIndex)
                return thresholdsByIndex[candidate];

            return null;
        }

        /// <summary>
        /// Finds the highest threshold level that does not exceed a detected value.
        /// </summary>
        /// <param name="levels">Distinct threshold levels sorted from low to high.</param>
        /// <param name="value">Detected observation value.</param>
        /// <returns>The zero-based PPLOT2 detection-band index.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the value falls below every arranged threshold level.
        /// </exception>
        /// <remarks>
        /// Binary search avoids scanning every threshold for each observation.
        /// </remarks>
        private static int FindPlottingLevel(double[] levels, double value)
        {
            int levelIndex = Array.BinarySearch(levels, value);
            if (levelIndex >= 0)
                return levelIndex;

            levelIndex = ~levelIndex - 1;
            if (levelIndex < 0)
                throw new InvalidOperationException("A detected observation falls below every arranged threshold.");

            return levelIndex;
        }

        /// <summary>
        /// Assigns a finite, open-interval exceedance plotting position to an explicit observation.
        /// </summary>
        /// <param name="data">Observation receiving the plotting position.</param>
        /// <param name="plottingPosition">Computed exceedance plotting position.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the computed position is nonfinite or is not strictly between zero and one.
        /// </exception>
        /// <remarks>
        /// Boundary probabilities are rejected rather than clamped because either boundary makes
        /// downstream inverse-CDF calculations infinite and indicates invalid arrangement counts.
        /// </remarks>
        private static void SetStrictPlottingPosition(Data data, double plottingPosition)
        {
            if (!double.IsFinite(plottingPosition) || plottingPosition <= 0d || plottingPosition >= 1d)
            {
                throw new InvalidOperationException(
                    $"The Hirsch-Stedinger routine produced invalid plotting position {plottingPosition:G17}.");
            }

            data.PlottingPosition = plottingPosition;
        }

        /// <summary>
        /// Apply the Langbein conversion to the plotting positions.
        /// </summary>
        /// <param name="lambda">The number of events per block.</param>
        public void ApplyLangbeinConversion(double lambda)
        {
            // Suppress collection changed events
            ExactSeries.SuppressCollectionChanged = true;
            UncertainSeries.SuppressCollectionChanged = true;
            IntervalSeries.SuppressCollectionChanged = true;
            ThresholdSeries.SuppressCollectionChanged = true;

            for (int i = 0; i < ExactSeries.Count; i++)
            {
                ExactSeries[i].PlottingPosition = 1 - Math.Exp(-lambda * ExactSeries[i].PlottingPosition);
            }
            for (int i = 0; i < UncertainSeries.Count; i++)
            {
                UncertainSeries[i].PlottingPosition = 1 - Math.Exp(-lambda * UncertainSeries[i].PlottingPosition);
            }
            for (int i = 0; i < IntervalSeries.Count; i++)
            {
                IntervalSeries[i].PlottingPosition = 1 - Math.Exp(-lambda * IntervalSeries[i].PlottingPosition);
            }

            // All collection changed events to fire
            ExactSeries.SuppressCollectionChanged = false;
            UncertainSeries.SuppressCollectionChanged = false;
            IntervalSeries.SuppressCollectionChanged = false;
            ThresholdSeries.SuppressCollectionChanged = false;

            RaisePropertyChange("PlottingPosition");
        }

        #endregion

        #region Summary Statistics

        /// <summary>
        /// Returns summary statistics for exact data. 
        /// </summary>
        /// <returns>Dictionary containing statistic names and values.</returns>
        public Dictionary<string, double> SummaryStatisticsExactDataOnly()
        {
            var result = new Dictionary<string, double>();
            if (ExactSeries.Count < 10)
            {
                result.Add("Record Length", double.NaN);
                result.Add("Events Per Index (λ)", double.NaN);
                result.Add("Low Outliers", double.NaN);
                result.Add("Minimum", double.NaN);
                result.Add("Maximum", double.NaN);
                result.Add("Mean", double.NaN);
                result.Add("Std Dev", double.NaN);
                result.Add("Skewness", double.NaN);
                result.Add("Kurtosis", double.NaN);
                result.Add("Mean (of log)", double.NaN);
                result.Add("Std Dev (of log)", double.NaN);
                result.Add("Skewness (of log)", double.NaN);
                result.Add("Kurtosis (of log)", double.NaN);
                result.Add("1%", double.NaN);
                result.Add("5%", double.NaN);
                result.Add("25%", double.NaN);
                result.Add("50%", double.NaN);
                result.Add("75%", double.NaN);
                result.Add("95%", double.NaN);
                result.Add("99%", double.NaN);
            }
            else
            {
                var values = ExactSeries.Select(x => x.Value).ToArray();
                var logValues = ExactSeries.Select(x => x.Log10Value).ToArray();

                var moments = ExactSeries.Count <= 2 ? new double[] { double.NaN, double.NaN, double.NaN, double.NaN } : Statistics.ProductMoments(values);
                var logMoments = ExactSeries.Count <= 2 ? new double[] { double.NaN, double.NaN, double.NaN, double.NaN } : Statistics.ProductMoments(logValues);
                var percentiles = ExactSeries.Count <= 2 ? new double[] { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN } : Statistics.Percentile(values, new[] { 0.01, 0.05, 0.25, 0.5, 0.75, 0.95, 0.99 });

                result.Add("Record Length", ExactSeries.Count);
                result.Add("Events Per Index (λ)", Lambda);
                result.Add("Low Outliers", NumberOfLowOutliers);
                result.Add("Minimum", Statistics.Minimum(values));
                result.Add("Maximum", Statistics.Maximum(values));
                result.Add("Mean", moments[0]);
                result.Add("Std Dev", moments[1]);
                result.Add("Skewness", moments[2]);
                result.Add("Kurtosis", moments[3] + 3);
                result.Add("Mean (of log)", logMoments[0]);
                result.Add("Std Dev (of log)", logMoments[1]);
                result.Add("Skewness (of log)", logMoments[2]);
                result.Add("Kurtosis (of log)", logMoments[3] + 3);
                result.Add("1%", percentiles[0]);
                result.Add("5%", percentiles[1]);
                result.Add("25%", percentiles[2]);
                result.Add("50%", percentiles[3]);
                result.Add("75%", percentiles[4]);
                result.Add("95%", percentiles[5]);
                result.Add("99%", percentiles[6]);
            }

            return result;
        }

        /// <summary>
        /// Returns summary statistics for all data (from a nonparametric distribution). 
        /// </summary>
        /// <returns>Dictionary containing statistic names and values.</returns>
        public Dictionary<string, double> SummaryStatisticsAllData()
        {
            var result = new Dictionary<string, double>();
            if (ExactSeries.Count < 10)
            {
                result.Add("Record Length", double.NaN);
                result.Add("Events Per Index (λ)", double.NaN);
                result.Add("Low Outliers", double.NaN);
                result.Add("Minimum", double.NaN);
                result.Add("Maximum", double.NaN);
                result.Add("Mean", double.NaN);
                result.Add("Std Dev", double.NaN);
                result.Add("Skewness", double.NaN);
                result.Add("Kurtosis", double.NaN);
                result.Add("Mean (of log)", double.NaN);
                result.Add("Std Dev (of log)", double.NaN);
                result.Add("Skewness (of log)", double.NaN);
                result.Add("Kurtosis (of log)", double.NaN);
                result.Add("1%", double.NaN);
                result.Add("5%", double.NaN);
                result.Add("25%", double.NaN);
                result.Add("50%", double.NaN);
                result.Add("75%", double.NaN);
                result.Add("95%", double.NaN);
                result.Add("99%", double.NaN);
            }
            else
            {
                var values = ExactSeries.Select(x => x.Value).ToList();
                values.AddRange(UncertainSeries.Select(x => x.Value).ToList());
                values.AddRange(IntervalSeries.Select(x => x.Value).ToList());
                values.Sort();

                var probs = ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                probs.AddRange(UncertainSeries.Select(x => x.PlottingPositionComplement).ToList());
                probs.AddRange(IntervalSeries.Select(x => x.PlottingPositionComplement).ToList());
                probs.Sort();

                var logValues = ExactSeries.Select(x => x.Log10Value).ToList();
                logValues.AddRange(UncertainSeries.Select(x => x.Log10Value).ToList());
                logValues.AddRange(IntervalSeries.Select(x => x.Log10Value).ToList());
                logValues.Sort();

                var dist = new EmpiricalDistribution(values, probs);
                var moments = dist.CentralMoments(1000);
                var logDist = new EmpiricalDistribution(logValues, probs);
                var logMoments = logDist.CentralMoments(1000);

                CreateFullTimeSeries();
                result.Add("Record Length", FullTimeSeries.Count);
                result.Add("Events Per Index (λ)", Lambda);
                result.Add("Low Outliers", NumberOfLowOutliers);
                result.Add("Minimum", Statistics.Minimum(values));
                result.Add("Maximum", Statistics.Maximum(values));
                result.Add("Mean", moments[0]);
                result.Add("Std Dev", moments[1]);
                result.Add("Skewness", moments[2]);
                result.Add("Kurtosis", moments[3]);
                result.Add("Mean (of log)", logMoments[0]);
                result.Add("Std Dev (of log)", logMoments[1]);
                result.Add("Skewness (of log)", logMoments[2]);
                result.Add("Kurtosis (of log)", logMoments[3]);
                result.Add("1%", dist.InverseCDF(0.01));
                result.Add("5%", dist.InverseCDF(0.05));
                result.Add("25%", dist.InverseCDF(0.25));
                result.Add("50%", dist.InverseCDF(0.5));
                result.Add("75%", dist.InverseCDF(0.75));
                result.Add("95%", dist.InverseCDF(0.95));
                result.Add("99%", dist.InverseCDF(0.99));
            }

            return result;
        }

        /// <summary>
        /// Computes nonparametric central moments from all data series using
        /// Hirsch-Stedinger plotting positions and numerical integration.
        /// </summary>
        /// <param name="useLog10Values">
        /// If <c>true</c>, computes moments of the log10-transformed values.
        /// If <c>false</c> (default), computes moments of the raw values.
        /// </param>
        /// <returns>
        /// An array of central moments [mean, stdDev, skewness, kurtosis],
        /// or <c>null</c> if insufficient data (fewer than 4 data points across all series).
        /// </returns>
        /// <remarks>
        /// <para>
        /// Combines exact, uncertain, and interval data series with their
        /// Hirsch-Stedinger plotting position complements to construct an
        /// <see cref="EmpiricalDistribution"/>, then computes central moments
        /// via numerical integration with 1000 points.
        /// </para>
        /// <para>
        /// Reference: Hirsch, R.M. and Stedinger, J.R. (1987).
        /// Plotting positions for historical floods and their precision.
        /// Water Resources Research, 23(4), 715-727.
        /// </para>
        /// </remarks>
        public double[]? GetNonparametricMoments(bool useLog10Values = false)
        {
            if (ExactSeries == null || ExactSeries.Count < 4) return null;

            int totalCount = ExactSeries.Count + UncertainSeries.Count + IntervalSeries.Count;
            if (totalCount < 4) return null;

            // Build sorted values
            List<double> values;
            if (useLog10Values)
            {
                values = ExactSeries.Select(x => x.Log10Value).ToList();
                values.AddRange(UncertainSeries.Select(x => x.Log10Value));
                values.AddRange(IntervalSeries.Select(x => x.Log10Value));
            }
            else
            {
                values = ExactSeries.Select(x => x.Value).ToList();
                values.AddRange(UncertainSeries.Select(x => x.Value));
                values.AddRange(IntervalSeries.Select(x => x.Value));
            }
            values.Sort();

            // Build sorted plotting position complements
            var probs = ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
            probs.AddRange(UncertainSeries.Select(x => x.PlottingPositionComplement));
            probs.AddRange(IntervalSeries.Select(x => x.PlottingPositionComplement));
            probs.Sort();

            var dist = new EmpiricalDistribution(values, probs);
            return dist.CentralMoments(1000);
        }

        /// <summary>
        /// Computes nonparametric central moments using Regression on Order Statistics (ROS)
        /// to impute values for low outliers below the censoring threshold.
        /// </summary>
        /// <param name="useLog10Values">
        /// If <c>true</c>, computes moments of the log10-transformed values.
        /// If <c>false</c> (default), computes moments of the raw values.
        /// </param>
        /// <returns>
        /// An array of central moments [mean, stdDev, skewness, kurtosis],
        /// or <c>null</c> if insufficient data (fewer than 4 data points across all series).
        /// </returns>
        /// <remarks>
        /// <para>
        /// When low outliers are present, their observed values can severely distort moment estimates
        /// (especially for log-transformed data where zeros map to extreme negative values). ROS
        /// addresses this by fitting a linear regression through the uncensored portion of a
        /// normal probability plot, then imputing censored values from the regression line at
        /// their corresponding normal quantiles.
        /// </para>
        /// <para>
        /// Algorithm:
        /// 1. Compute standard normal quantiles z_i = Φ⁻¹(pp_i) for each data point's plotting position.
        /// 2. Fit a simple linear regression of value vs. z using only the uncensored (non-low-outlier) points.
        /// 3. For each low outlier, replace its value with the regression-predicted value at its z_i.
        /// 4. Construct an <see cref="EmpiricalDistribution"/> from the combined imputed and uncensored
        ///    values with their plotting position complements, then compute central moments.
        /// </para>
        /// <para>
        /// Falls back to <see cref="GetNonparametricMoments"/> when there are no low outliers
        /// or fewer than 2 uncensored exact data points (insufficient for regression).
        /// </para>
        /// <para>
        /// References:
        /// Helsel, D.R. and Cohn, T.A. (1988). Estimation of descriptive statistics for multiply
        /// censored water quality data. Water Resources Research, 24(12), 1997–2004.
        /// </para>
        /// <para>
        /// Helsel, D.R. (2005). Nondetects and Data Analysis: Statistics for Censored Environmental
        /// Data. Wiley, New York, 250 p.
        /// </para>
        /// </remarks>
        public double[]? GetNonparametricMomentsROS(bool useLog10Values = false)
        {
            // Fall back to standard method when there are no low outliers to impute
            if (NumberOfLowOutliers == 0)
                return GetNonparametricMoments(useLog10Values);

            if (ExactSeries == null || ExactSeries.Count < 4) return null;

            int totalCount = ExactSeries.Count + UncertainSeries.Count + IntervalSeries.Count;
            if (totalCount < 4) return null;

            // Separate exact data into uncensored and censored (low outlier) sets
            var uncensoredValues = new List<double>();
            var uncensoredQuantiles = new List<double>();
            var stdNormal = new Normal(0, 1);

            // Build paired (value, quantile) lists for exact series
            for (int i = 0; i < ExactSeries.Count; i++)
            {
                double value = useLog10Values ? ExactSeries[i].Log10Value : ExactSeries[i].Value;
                double z = stdNormal.InverseCDF(ExactSeries[i].PlottingPositionComplement);

                if (!((ExactData)ExactSeries[i]).IsLowOutlier)
                {
                    uncensoredValues.Add(value);
                    uncensoredQuantiles.Add(z);
                }
            }

            // Need at least 2 uncensored points to fit a regression line
            if (uncensoredValues.Count < 2)
                return GetNonparametricMoments(useLog10Values);

            // Fit linear regression: value = a + b * z using uncensored points only
            var xMatrix = new Matrix(uncensoredQuantiles.ToArray());
            var yVector = new Vector(uncensoredValues.ToArray());
            var regression = new LinearRegression(xMatrix, yVector, true);
            double intercept = regression.Parameters[0];
            double slope = regression.Parameters[1];

            // Build combined values list with ROS-imputed values for low outliers
            var values = new List<double>();
            for (int i = 0; i < ExactSeries.Count; i++)
            {
                if (((ExactData)ExactSeries[i]).IsLowOutlier)
                {
                    // Impute from regression line at this point's normal quantile
                    double z = stdNormal.InverseCDF(ExactSeries[i].PlottingPositionComplement);
                    double imputed = intercept + slope * z;
                    values.Add(imputed);
                }
                else
                {
                    double value = useLog10Values ? ExactSeries[i].Log10Value : ExactSeries[i].Value;
                    values.Add(value);
                }
            }

            // Add uncertain and interval series values (not subject to low-outlier imputation)
            if (useLog10Values)
            {
                values.AddRange(UncertainSeries.Select(x => x.Log10Value));
                values.AddRange(IntervalSeries.Select(x => x.Log10Value));
            }
            else
            {
                values.AddRange(UncertainSeries.Select(x => x.Value));
                values.AddRange(IntervalSeries.Select(x => x.Value));
            }
            values.Sort();

            // Build sorted plotting position complements
            var probs = ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
            probs.AddRange(UncertainSeries.Select(x => x.PlottingPositionComplement));
            probs.AddRange(IntervalSeries.Select(x => x.PlottingPositionComplement));
            probs.Sort();

            var dist = new EmpiricalDistribution(values, probs);
            return dist.CentralMoments(1000);
        }

        /// <summary>
        /// Set standardized values for creating a Q-Q plot.
        /// </summary>
        public void SetStandardizedValues()
        {
            if (ExactSeries == null || ExactSeries.Count < 4) return;

            var values = ExactSeries.Select(x => x.Value).ToList();
            values.AddRange(UncertainSeries.Select(x => x.Value).ToList());
            values.AddRange(IntervalSeries.Select(x => x.Value).ToList());
            values.Sort();

            var probs = ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
            probs.AddRange(UncertainSeries.Select(x => x.PlottingPositionComplement).ToList());
            probs.AddRange(IntervalSeries.Select(x => x.PlottingPositionComplement).ToList());
            probs.Sort();

            var logValues = ExactSeries.Select(x => x.Log10Value).ToList();
            logValues.AddRange(UncertainSeries.Select(x => x.Log10Value).ToList());
            logValues.AddRange(IntervalSeries.Select(x => x.Log10Value).ToList());
            logValues.Sort();

            var dist = new EmpiricalDistribution(values, probs);
            var moments = dist.CentralMoments(200);
            var logDist = new EmpiricalDistribution(logValues, probs);
            var logMoments = logDist.CentralMoments(200);

            // Check if moments are invalid
            if (double.IsNaN(moments[0]) || double.IsNaN(moments[1]))
            {
                for (int i = 0; i < ExactSeries.Count; i++)
                    ExactSeries[i].StandardizedValue = double.NaN;

                for (int i = 0; i < UncertainSeries.Count; i++)
                    UncertainSeries[i].StandardizedValue = double.NaN;

                for (int i = 0; i < IntervalSeries.Count; i++)
                    IntervalSeries[i].StandardizedValue = double.NaN;
                return;
            }

            var Normal = new Normal(moments[0], moments[1]);

            for (int i = 0; i < ExactSeries.Count; i++)
                ExactSeries[i].StandardizedValue = Normal.InverseCDF(ExactSeries[i].PlottingPositionComplement);
            
            for (int i = 0; i < UncertainSeries.Count; i++)
                UncertainSeries[i].StandardizedValue = Normal.InverseCDF(UncertainSeries[i].PlottingPositionComplement);

            for (int i = 0; i < IntervalSeries.Count; i++)
                IntervalSeries[i].StandardizedValue = Normal.InverseCDF(IntervalSeries[i].PlottingPositionComplement);

            // Check of log moments are invalid
            if (double.IsNaN(logMoments[0]) || double.IsNaN(logMoments[1]))
            {
                for (int i = 0; i < ExactSeries.Count; i++)
                    ExactSeries[i].StandardizedLog10Value = double.NaN;

                for (int i = 0; i < UncertainSeries.Count; i++)
                    UncertainSeries[i].StandardizedLog10Value = double.NaN;

                for (int i = 0; i < IntervalSeries.Count; i++)
                    IntervalSeries[i].StandardizedLog10Value = double.NaN;
                return;
            }

            Normal = new Normal(logMoments[0], logMoments[1]);

            for (int i = 0; i < ExactSeries.Count; i++)
                ExactSeries[i].StandardizedLog10Value = Normal.InverseCDF(ExactSeries[i].PlottingPositionComplement);

            for (int i = 0; i < UncertainSeries.Count; i++)
                UncertainSeries[i].StandardizedLog10Value = Normal.InverseCDF(UncertainSeries[i].PlottingPositionComplement);

            for (int i = 0; i < IntervalSeries.Count; i++)
                IntervalSeries[i].StandardizedLog10Value = Normal.InverseCDF(IntervalSeries[i].PlottingPositionComplement);

        }

        #endregion

        #region IO Methods

        /// <summary>
        /// Create a copy of the data frame.
        /// </summary>
        /// <remarks>
        /// The clone starts with its own empty <c>_fullTimeSeries</c>; first access to
        /// <see cref="FullTimeSeries"/> triggers a lazy rebuild on the clone. This avoids
        /// aliasing the parent's list, which would allow mutations through one instance
        /// (e.g., a <see cref="JackKnife"/> rebuild) to corrupt the other.
        /// </remarks>
        public DataFrame Clone()
        {
            return new DataFrame(ToXElement());
        }

        /// <summary>
        /// Returns the Data Frame as XElement.
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(DataFrame));
            result.SetAttributeValue(nameof(NumberOfLowOutliers), NumberOfLowOutliers.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LowOutlierThreshold), LowOutlierThreshold.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PlottingParameter), PlottingParameter.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Lambda), Lambda.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(USGSRawText), USGSRawText.ToString(CultureInfo.InvariantCulture));
            result.Add(ExactSeries.ToXElement());
            result.Add(UncertainSeries.ToXElement());
            result.Add(IntervalSeries.ToXElement());
            result.Add(ThresholdSeries.ToXElement());
            return result;
        }

        #endregion

        #region Exact Data Methods

        /// <summary>
        /// Sets the lambda value directly without calculation.
        /// </summary>
        /// <param name="lambda">The average number of events per index.</param>
        public void SetLambda(double lambda)
        {
            _lambda = lambda;
            RaisePropertyChange(nameof(Lambda));
        }

        /// <summary>
        /// Calculates the average number of events per index.
        /// </summary>
        public void CalculateLambda()
        {
            double events = ExactSeries.Count;
            double span = ExactSeries.IndexSpan();
            if (events <= 0 || span <= 0)
            {
                _lambda = 0;
            }
            else
            {
                _lambda = events / span;
            }
            RaisePropertyChange(nameof(Lambda));
        }

        /// <summary>
        /// Create a block series for exact data from a time series.
        /// </summary>
        /// <param name="timeSeries">The time series.</param>
        /// <param name="timeBlock">The time block window for creating the block series. Default = Water Year.</param>
        /// <param name="blockFunction">The function to be computed over each time block. Default = Maximum.</param>
        /// <param name="smoothingFunction">The time series smoothing function. Default = None.</param>
        /// <param name="startMonth">The starting month for the custom time block. Default = 10.</param>
        /// <param name="endMonth">The ending month for the custom time block. Default = 9.</param>
        /// <param name="period">The time period to perform smoothing over. Default = 1.</param>
        public void CreateBlockSeries(TimeSeries timeSeries, TimeBlockWindow timeBlock = TimeBlockWindow.WaterYear, 
            BlockFunctionType blockFunction = BlockFunctionType.Maximum, SmoothingFunctionType smoothingFunction = SmoothingFunctionType.None, 
            int startMonth = 10, int endMonth = 9, int period = 1)
        {

            TimeSeries? _timeSeries = null;
            if (timeBlock == TimeBlockWindow.CalendarYear)
            {
                _timeSeries = timeSeries.CalendarYearSeries(blockFunction, smoothingFunction, period);
            }
            else if (timeBlock == TimeBlockWindow.WaterYear)
            {
                _timeSeries = timeSeries.CustomYearSeries(startMonth, blockFunction, smoothingFunction, period);
            }
            else if (timeBlock == TimeBlockWindow.CustomYear)
            {
                _timeSeries = timeSeries.CustomYearSeries(startMonth, endMonth, blockFunction, smoothingFunction, period);
            }
            else if (timeBlock == TimeBlockWindow.Quarter)
            {
                _timeSeries = timeSeries.QuarterlySeries(blockFunction, smoothingFunction, period);
            }
            else if (timeBlock == TimeBlockWindow.Month)
            {
                _timeSeries = timeSeries.MonthlySeries(blockFunction, smoothingFunction, period);
            }

            ExactSeries.Clear();
            ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < _timeSeries!.Count; i++)
                ExactSeries.Add(new ExactData(_timeSeries[i].Index, _timeSeries[i].Value));
            _lambda = 1;
            ExactSeries.SuppressCollectionChanged = false;
            ExactSeries.RaiseCollectionChangedReset();
        }

        /// <summary>
        /// Create a peaks-over-threshold series for exact data from a time series.
        /// </summary>
        /// <param name="timeSeries">The time series.</param>
        /// <param name="threshold">The threshold value. The peaks larger than this threshold will be recorded.</param>
        /// <param name="minStepsBetweenPeaks">The minimum number of time steps between peaks. Default = 1.</param>
        /// <param name="smoothingFunction">The time series smoothing function. Default = None.</param>
        /// <param name="period">The time period to perform smoothing over. Default = 1.</param>
        public void CreatePeaksOverThresholdSeries(TimeSeries timeSeries, double threshold, int minStepsBetweenPeaks = 1, 
            SmoothingFunctionType smoothingFunction = SmoothingFunctionType.None, int period = 1)
        {
            var _timeSeries = timeSeries.PeaksOverThresholdSeries(threshold, minStepsBetweenPeaks, smoothingFunction, period);

            ExactSeries.Clear();
            ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < _timeSeries.Count; i++)
                ExactSeries.Add(new ExactData(_timeSeries[i].Index, _timeSeries[i].Value));
            // Set lambda
            double events = ExactSeries.Count;
            double span = timeSeries.EndDate.Year - timeSeries.StartDate.Year + 1;
            _lambda = events / span;
            ExactSeries.SuppressCollectionChanged = false;
            ExactSeries.RaiseCollectionChangedReset();
        }

        /// <summary>
        /// Create a block series for exact data by downloading peak data from the USGS API.
        /// </summary>
        /// <param name="siteNumber">The USGS site number.</param>
        /// <param name="timeSeriesType">The type of time series to download. Default = PeakDischarge.</param>
        /// <param name="cancellationToken">Token used to cancel the USGS download.</param>
        /// <exception cref="ArgumentException">Thrown when time series type is not peak discharge or peak stage.</exception>
        public async Task CreateFromUSGS(string siteNumber, TimeSeriesDownload.TimeSeriesType timeSeriesType = TimeSeriesDownload.TimeSeriesType.PeakDischarge, CancellationToken cancellationToken = default)
        {
            if (timeSeriesType != TimeSeriesDownload.TimeSeriesType.PeakDischarge && timeSeriesType != TimeSeriesDownload.TimeSeriesType.PeakStage)
                throw new ArgumentException("The time series type must be peak discharge or peak stage", nameof(timeSeriesType));

            var result = await TimeSeriesDownload.FromUSGS(siteNumber, timeSeriesType, cancellationToken);
            var timeSeries = result.TimeSeries;
            _usgsRawText = result.RawText;

            ExactSeries.Clear();
            ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < timeSeries.Count; i++)
                ExactSeries.Add(new ExactData(timeSeries[i].Index, timeSeries[i].Value));
            _lambda = 1;
            ExactSeries.SuppressCollectionChanged = false;
            ExactSeries.RaiseCollectionChangedReset();
        }

        #endregion

        #region Bootstrap Methods

        /// <summary>
        /// Generate a jackknife data frame by leaving out one observation. 
        /// </summary>   
        /// <param name="index">The index to leave out.</param>
        /// <returns>A new DataFrame with the specified index removed.</returns>
        public DataFrame JackKnife(int index)
        {
            var dataframe = Clone();

            // Suppress collection changed events for speed
            dataframe.ExactSeries.SuppressCollectionChanged = true;
            dataframe.UncertainSeries.SuppressCollectionChanged = true;
            dataframe.IntervalSeries.SuppressCollectionChanged = true;
            dataframe.ThresholdSeries.SuppressCollectionChanged = true;

            // Exact data
            foreach (ExactData data in dataframe.ExactSeries)
            {
                if (data.Index == index)
                {
                    dataframe.ExactSeries.Remove(data);
                    dataframe.ExactSeries.SuppressCollectionChanged = false;
                    dataframe.UncertainSeries.SuppressCollectionChanged = false;
                    dataframe.IntervalSeries.SuppressCollectionChanged = false;
                    dataframe.ThresholdSeries.SuppressCollectionChanged = false;
                    return dataframe;
                }
            }

            // Uncertain data
            foreach (UncertainData data in dataframe.UncertainSeries)
            {
                if (data.Index == index)
                {
                    dataframe.UncertainSeries.Remove(data);
                    dataframe.ExactSeries.SuppressCollectionChanged = false;
                    dataframe.UncertainSeries.SuppressCollectionChanged = false;
                    dataframe.IntervalSeries.SuppressCollectionChanged = false;
                    dataframe.ThresholdSeries.SuppressCollectionChanged = false;
                    return dataframe;
                }
            }

            // Interval data
            foreach (IntervalData data in dataframe.IntervalSeries)
            {
                if (data.Index == index)
                {
                    dataframe.IntervalSeries.Remove(data);
                    dataframe.ExactSeries.SuppressCollectionChanged = false;
                    dataframe.UncertainSeries.SuppressCollectionChanged = false;
                    dataframe.IntervalSeries.SuppressCollectionChanged = false;
                    dataframe.ThresholdSeries.SuppressCollectionChanged = false;
                    return dataframe;
                }
            }

            // Threshold data
            foreach (ThresholdData data in dataframe.ThresholdSeries)
            {
                // Number above
                for (int i = data.StartIndex; i < data.StartIndex + data.NumberAbove; i++)
                {
                    if (index == i)
                    {
                        data.NumberAbove--;
                        dataframe.ExactSeries.SuppressCollectionChanged = false;
                        dataframe.UncertainSeries.SuppressCollectionChanged = false;
                        dataframe.IntervalSeries.SuppressCollectionChanged = false;
                        dataframe.ThresholdSeries.SuppressCollectionChanged = false;
                        return dataframe;
                    }
                }
                // Number below
                for (int i = data.StartIndex + data.NumberAbove; i <= data.EndIndex; i++)
                {
                    if (index == i)
                    {
                        data.NumberBelow--;
                        dataframe.ExactSeries.SuppressCollectionChanged = false;
                        dataframe.UncertainSeries.SuppressCollectionChanged = false;
                        dataframe.IntervalSeries.SuppressCollectionChanged = false;
                        dataframe.ThresholdSeries.SuppressCollectionChanged = false;
                        return dataframe;
                    }
                }
            }

            // Un-suppress collection change events so data frame can be used properly
            dataframe.ExactSeries.SuppressCollectionChanged = false;
            dataframe.UncertainSeries.SuppressCollectionChanged = false;
            dataframe.IntervalSeries.SuppressCollectionChanged = false;
            dataframe.ThresholdSeries.SuppressCollectionChanged = false;

            return dataframe;
        }

        /// <summary>
        /// Generate a resampled data frame using nonparametric bootstrap resampling with replacement.
        /// </summary>
        /// <param name="prng">The pseudo-random number generator.</param>
        /// <param name="createFullTimeSeries">If true, creates the full time series. Default = false.</param>
        /// <returns>A new DataFrame with resampled data.</returns>
        public DataFrame Resample(Random prng, bool createFullTimeSeries = false)
        {
            if (FullTimeSeries == null || FullTimeSeries.Count == 0) CreateFullTimeSeries();
            var dataframe = new DataFrame();

            // Suppress collection changed events for speed
            dataframe.ExactSeries.SuppressCollectionChanged = true;
            dataframe.UncertainSeries.SuppressCollectionChanged = true;
            dataframe.IntervalSeries.SuppressCollectionChanged = true;
            dataframe.ThresholdSeries.SuppressCollectionChanged = true;

            int startIndex = FullTimeSeries!.First().Index;
            int endIndex = FullTimeSeries!.Last().Index;
            var indexes = prng.NextIntegers(startIndex, endIndex + 1, FullTimeSeries!.Count, true);
            Array.Sort(indexes);

            for (int i = 0; i < indexes.Length; i++)
            {
                bool found = false;

                // Exact data
                foreach (ExactData data in ExactSeries)
                {
                    if (data.Index == indexes[i])
                    {
                        dataframe.ExactSeries.Add(data.Clone());
                        found = true;
                        break;
                    }
                }
                if (found) continue;

                // Uncertain data
                foreach (UncertainData data in UncertainSeries)
                {
                    if (data.Index == indexes[i])
                    {
                        dataframe.UncertainSeries.Add(data.Clone());
                        found = true;
                        break;
                    }
                }
                if (found) continue;

                // Interval data
                foreach (IntervalData data in IntervalSeries)
                {
                    if (data.Index == indexes[i])
                    {
                        dataframe.IntervalSeries.Add(data.Clone());
                        found = true;
                        break;
                    }
                }
                if (found) continue;

                // Threshold data - check each threshold period
                foreach (ThresholdData threshold in ThresholdSeries)
                {
                    // Check if index falls in above-threshold region
                    if (indexes[i] >= threshold.StartIndex && indexes[i] < threshold.StartIndex + threshold.NumberAbove)
                    {
                        // Add or update threshold in resampled data
                        var existingThreshold = dataframe.ThresholdSeries
                            .FirstOrDefault(t => ((ThresholdData)t).StartIndex == threshold.StartIndex &&
                            ((ThresholdData)t).EndIndex == threshold.EndIndex);

                        if (existingThreshold is not null)
                        {
                            ((ThresholdData)existingThreshold).NumberAbove++;
                        }
                        else
                        {
                            var newThreshold = threshold.Clone();
                            newThreshold.NumberAbove = 1;
                            newThreshold.NumberBelow = 0;
                            dataframe.ThresholdSeries.Add(newThreshold);
                        }
                        found = true;
                        break;
                    }
                    // Check if index falls in below-threshold region
                    else if (indexes[i] >= threshold.StartIndex + threshold.NumberAbove && indexes[i] <= threshold.EndIndex)
                    {
                        // Add or update threshold in resampled data
                        var existingThreshold = dataframe.ThresholdSeries
                            .FirstOrDefault(t => ((ThresholdData)t).StartIndex == threshold.StartIndex &&
                            ((ThresholdData)t).EndIndex == threshold.EndIndex);

                        if (existingThreshold is not null)
                        {
                            ((ThresholdData)existingThreshold).NumberBelow++;
                        }
                        else
                        {
                            var newThreshold = threshold.Clone();
                            newThreshold.NumberAbove = 0;
                            newThreshold.NumberBelow = 1;
                            dataframe.ThresholdSeries.Add(newThreshold);
                        }
                        found = true;
                        break;
                    }
                }

            }

            // Un-suppress collection change events so data frame can be used properly
            dataframe.ExactSeries.SuppressCollectionChanged = false;
            dataframe.UncertainSeries.SuppressCollectionChanged = false;
            dataframe.IntervalSeries.SuppressCollectionChanged = false;
            dataframe.ThresholdSeries.SuppressCollectionChanged = false;

            if (createFullTimeSeries) dataframe.CreateFullTimeSeries();

            return dataframe;
        }

        /// <summary>
        /// Generates a parametric bootstrap data frame by simulating the physical observation process.
        /// </summary>
        /// <param name="distribution">The fitted distribution to sample from.</param>
        /// <param name="prng">The pseudo-random number generator.</param>
        /// <param name="createFullTimeSeries">If true, creates the full time series. Default = false.</param>
        /// <returns>A new <see cref="DataFrame"/> with bootstrapped data that mirrors the censoring
        /// structure of the original.</returns>
        /// <remarks>
        /// <para>
        ///     Each data type is resampled by simulating the physical process that generated the
        ///     original observation:
        /// </para>
        /// <list type="bullet">
        ///     <item><description><b>Exact data:</b> Draw a new value unconditionally from the fitted
        ///         distribution via <c>InverseCDF(U)</c>. This simulates a new annual peak occurring
        ///         under the same flood-generating process.</description></item>
        ///     <item><description><b>Uncertain data:</b> Draw a "true" flood magnitude from the fitted
        ///         distribution, then shift the measurement error distribution to center on that value
        ///         while preserving the original error spread. This simulates observing a new flood
        ///         with the same measurement quality.</description></item>
        ///     <item><description><b>Interval data:</b> Draw a value unconditionally from the fitted
        ///         distribution and re-classify against the original interval bounds. If the simulated
        ///         value falls below the lower bound, the observation becomes left-censored; if above
        ///         the upper bound, right-censored; otherwise the original interval is preserved. This
        ///         correctly propagates uncertainty about whether a paleoflood-era event would have
        ///         been detectable given the original evidence bounds.</description></item>
        ///     <item><description><b>Threshold data (systematic):</b> When <c>NumberBelow == 0</c>
        ///         (every year within the threshold period has a recorded observation in the exact,
        ///         uncertain, or interval series), the threshold is cloned with its original counts.
        ///         This prevents spurious <c>NumberAbove</c> from Binomial resampling that would
        ///         double-count observations already present in the exact series. Typical for
        ///         crest-stage gage perception thresholds.</description></item>
        ///     <item><description><b>Threshold data (historical):</b> When <c>NumberBelow &gt; 0</c>
        ///         (the threshold period contains unobserved years not accounted for by other series),
        ///         the exceedance count is resampled from <c>Binomial(n, 1 − F(threshold))</c> where
        ///         <c>n = Duration − NumberAbove</c>. This propagates uncertainty about how many
        ///         unrecorded floods exceeded the perception threshold during historical or paleoflood
        ///         periods.</description></item>
        ///     <item><description><b>Low outliers:</b> After resampling exact data, any values below
        ///         <see cref="LowOutlierThreshold"/> are re-flagged as low outliers. The marking is
        ///         performed inline (without <see cref="SetLowOutliersFromThreshold"/>) to avoid
        ///         validation guards that can throw for bootstrap samples where the resampled median
        ///         shifts below the threshold.</description></item>
        /// </list>
        /// <para>
        ///     <see cref="ProcessThresholdSeries"/> is called after resampling to adjust threshold
        ///     <c>NumberBelow</c> for overlap with exact, interval, and uncertain data.
        /// </para>
        /// <para>
        ///     Reference: Davison, A.C. and Hinkley, D.V. (1997). Bootstrap Methods and Their Application.
        ///     Cambridge University Press, Sections 3.5 and 7.3.
        /// </para>
        /// </remarks>
        public DataFrame BootstrapDataFrame(IUnivariateDistribution distribution, Random prng, bool createFullTimeSeries = false)
        {
            var dataframe = new DataFrame();
            bool filterLowOutliers = NumberOfLowOutliers > 0;

            // Suppress collection changed events for speed
            dataframe.ExactSeries.SuppressCollectionChanged = true;
            dataframe.UncertainSeries.SuppressCollectionChanged = true;
            dataframe.IntervalSeries.SuppressCollectionChanged = true;
            dataframe.ThresholdSeries.SuppressCollectionChanged = true;

            // ── Exact data ──────────────────────────────────────────────────────
            // Unconditional draw from the fitted distribution simulates a new
            // annual peak under the same flood-generating process.
            foreach (ExactData data in ExactSeries)
            {
                var simulatedValue = distribution.InverseCDF(prng.NextDouble());
                dataframe.ExactSeries.Add(new ExactData(data.Index, simulatedValue));
            }

            // ── Uncertain data ──────────────────────────────────────────────────
            // Draw a "true" flood magnitude, then shift the measurement error
            // distribution to center on that value (preserving error spread).
            foreach (UncertainData data in UncertainSeries)
            {
                var simulatedValue = distribution.InverseCDF(prng.NextDouble());
                var shiftedDist = ShiftDistribution(data.Distribution, simulatedValue);
                dataframe.UncertainSeries.Add(new UncertainData(data.Index, shiftedDist));
            }

            // ── Interval data ───────────────────────────────────────────────────
            // Unconditional draw, then re-classify against original bounds.
            // A paleoflood interval becomes left-censored when the simulated event
            // is smaller than the lower evidence bound — correctly reflecting that
            // most bootstrap replicates would not produce an extreme paleoflood.
            foreach (IntervalData data in IntervalSeries)
            {
                var simulatedValue = distribution.InverseCDF(prng.NextDouble());
                if (simulatedValue < data.LowerValue)
                {
                    double upper = data.LowerValue;
                    double lower = Math.Min(upper - 1E-8, distribution.InverseCDF(Tools.DoubleMachineEpsilon));
                    double mid = 0.5 * (lower + upper);
                    dataframe.IntervalSeries.Add(new IntervalData(data.Index, lower, mid, upper));
                }
                else if (simulatedValue > data.UpperValue)
                {
                    double lower = data.UpperValue;
                    double upper = Math.Max(lower + 1E-8, distribution.InverseCDF(1 - Tools.DoubleMachineEpsilon));
                    double mid = 0.5 * (lower + upper);
                    dataframe.IntervalSeries.Add(new IntervalData(data.Index, lower, mid, upper));
                }
                else
                {
                    dataframe.IntervalSeries.Add(new IntervalData(data.Index, data.LowerValue, 0.5 * (data.LowerValue + data.UpperValue), data.UpperValue));
                }
            }

            // ── Threshold data ──────────────────────────────────────────────────
            // Two cases based on whether the threshold period is fully covered by
            // observations (systematic) or contains unobserved years (historical).
            foreach (ThresholdData data in ThresholdSeries)
            {
                if (data.NumberBelow == 0)
                {
                    // Systematic threshold: every year has an observation in the
                    // exact, uncertain, or interval series. Clone with original
                    // counts to avoid Binomial resampling creating spurious
                    // NumberAbove that would double-count the exact observations.
                    dataframe.ThresholdSeries.Add(data.Clone());
                }
                else
                {
                    // Historical threshold: unobserved years exist within the
                    // period. Resample the exceedance count from Binomial(n, p)
                    // where p = P(X > threshold) under the fitted distribution.
                    double p = 1.0 - distribution.CDF(data.Value);
                    int n = data.Duration - data.NumberAbove;
                    int nAbove;
                    if (n <= 0 || p <= 0.0)
                    {
                        nAbove = 0;
                    }
                    else if (p >= 1.0)
                    {
                        nAbove = n;
                    }
                    else
                    {
                        var binomialDist = new Binomial(p, n);
                        nAbove = Math.Max(0, (int)Math.Floor(binomialDist.InverseCDF(prng.NextDouble())));
                        nAbove = Math.Min(nAbove, n);
                    }
                    int nBelow = data.Duration - nAbove;
                    dataframe.ThresholdSeries.Add(new ThresholdData(data.StartIndex, data.EndIndex, data.Value) { NumberAbove = nAbove, NumberBelow = nBelow });
                }
            }

            // ── Low outliers ────────────────────────────────────────────────────
            // Re-flag resampled exact values below the threshold as low outliers.
            // Uses inline marking instead of SetLowOutliersFromThreshold() to avoid
            // validation guards that can throw when >50% of bootstrap values land
            // below the threshold (the UpperMiddleValue check).
            if (filterLowOutliers)
            {
                dataframe.LowOutlierThreshold = LowOutlierThreshold;
                dataframe._numberOfLowOutliers = 0;
                for (int i = 0; i < dataframe.ExactSeries.Count; i++)
                {
                    if (dataframe.ExactSeries[i].Value < LowOutlierThreshold)
                    {
                        ((ExactData)dataframe.ExactSeries[i]).IsLowOutlier = true;
                        dataframe._numberOfLowOutliers++;
                    }
                }
            }

            // ── Post-processing ─────────────────────────────────────────────────
            dataframe.ProcessThresholdSeries();
            if (createFullTimeSeries) dataframe.CreateFullTimeSeries();

            // Un-suppress collection change events
            dataframe.ExactSeries.SuppressCollectionChanged = false;
            dataframe.UncertainSeries.SuppressCollectionChanged = false;
            dataframe.IntervalSeries.SuppressCollectionChanged = false;
            dataframe.ThresholdSeries.SuppressCollectionChanged = false;

            return dataframe;
        }

        /// <summary>
        /// Creates a new distribution of the same type, shifted so that its center is at the specified value,
        /// while preserving the original measurement error spread.
        /// </summary>
        /// <param name="original">The original measurement error distribution.</param>
        /// <param name="newCenter">The new center value (simulated "true" flood magnitude).</param>
        /// <returns>A new distribution shifted to center on <paramref name="newCenter"/>.</returns>
        /// <remarks>
        /// <para>
        ///     For additive-error families (Normal, Uniform, Triangular, etc.), the distribution
        ///     is shifted by <c>newCenter - original.Mean</c>. For multiplicative-error families
        ///     (LogNormal, Gamma), a ratio-based shift preserves the coefficient of variation.
        /// </para>
        /// <para>
        ///     Supported distributions: Normal, StudentT, TruncatedNormal, LogNormal, LnNormal,
        ///     GammaDistribution, Uniform, Triangular, Pert, GeneralizedBeta. Unrecognized types
        ///     fall back to a clone of the original.
        /// </para>
        /// </remarks>
        private static UnivariateDistributionBase ShiftDistribution(UnivariateDistributionBase original, double newCenter)
        {
            double originalMean = original.Mean;
            double shift = newCenter - originalMean;

            // Guard against degenerate cases
            if (double.IsNaN(shift) || double.IsInfinity(shift))
                return (UnivariateDistributionBase)original.Clone();

            switch (original)
            {
                case Normal n:
                    return new Normal(n.Mu + shift, n.Sigma);

                case TruncatedNormal tn:
                    return new TruncatedNormal(tn.Mu + shift, tn.Sigma, tn.Min + shift, tn.Max + shift);

                case StudentT st:
                    return new StudentT(st.Mu + shift, st.Sigma, st.DegreesOfFreedom);

                case LogNormal ln:
                {
                    // Multiplicative shift in log-space preserves the CV
                    double ratio = (originalMean > 0 && newCenter > 0) ? newCenter / originalMean : 1.0;
                    double newMu = ln.Mu + Math.Log(Math.Max(ratio, 1e-12));
                    return new LogNormal(newMu, ln.Sigma);
                }

                case LnNormal lnn:
                    return new LnNormal(lnn.Mean + shift, lnn.StandardDeviation);

                case GammaDistribution g:
                {
                    // Scale shift preserves shape (and CV)
                    double ratio = (originalMean > 0 && newCenter > 0) ? newCenter / originalMean : 1.0;
                    double newScale = g.Theta * ratio;
                    return new GammaDistribution(Math.Max(newScale, 1e-12), g.Kappa);
                }

                case Uniform u:
                    return new Uniform(u.Min + shift, u.Max + shift);

                case Triangular t:
                    return new Triangular(t.Min + shift, t.MostLikely + shift, t.Max + shift);

                case Pert p:
                    return new Pert(p.Min + shift, p.MostLikely + shift, p.Max + shift);

                case GeneralizedBeta gb:
                    return new GeneralizedBeta(gb.Alpha, gb.Beta, gb.Min + shift, gb.Max + shift);

                default:
                    // Unrecognized distribution type — clone as-is
                    Debug.WriteLine($"ShiftDistribution: unrecognized type {original.GetType().Name}, returning clone.");
                    return (UnivariateDistributionBase)original.Clone();
            }
        }

        #endregion


        #endregion

    }


}
