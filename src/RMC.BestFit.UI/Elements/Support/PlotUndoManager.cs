// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlotUndoManager.cs" company="USACE Risk Management Center">
//   Copyright (c) 2024 USACE Risk Management Center. All rights reserved.
// </copyright>
// <summary>
//   Manages undo bridges for a single OxyPlot Plot and all its child elements.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace RMC.BestFit.UI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using FrameworkInterfaces;
    using FrameworkInterfaces.Undo;
    using FrameworkInterfaces.Undo.Actions;
    using OxyPlot.Wpf;
    using OxyPlot.Wpf.Serialization;

    /// <summary>
    /// Manages undo bridges for a single OxyPlot <see cref="Plot"/> and all its child elements
    /// (axes, series, annotations).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Each element creates one <see cref="PlotUndoManager"/> per plot. The manager automatically
    /// monitors <see cref="Plot.Axes"/> and <see cref="Plot.Annotations"/> collection changes
    /// to rebuild bridges when axes change type or annotations are added/removed via the toolbar.
    /// </para>
    /// <para>
    /// Series bridges are NOT auto-monitored because <see cref="Plot.Series"/> is cleared and
    /// repopulated during data update operations. Instead, call <see cref="RebuildSeriesAndAnnotationBridges"/>
    /// explicitly after each UpdateXxxPlot() method, outside of any <see cref="SuspendRecording"/> scope.
    /// </para>
    /// </remarks>
    public class PlotUndoManager : IDisposable
    {
        /// <summary>
        /// WPF theming properties that change automatically when <see cref="System.Windows.UIElement.IsEnabled"/>
        /// cascades through the visual tree (e.g., disabled state styling). These must NOT be tracked for undo
        /// because they are not user-initiated changes. For example, when <c>DisableOpenWindows()</c> sets
        /// <c>IsEnabled = false</c> on all document controls during MCMC estimation, WPF coerces
        /// Foreground/Background on OxyPlot Series objects, which would create spurious undo entries
        /// on every open element.
        /// </summary>
        private static readonly HashSet<string> WpfThemingProperties = new()
        {
            "Foreground", "Background", "IsEnabled"
        };

        /// <summary>
        /// Plot visual properties for undo tracking, excluding WPF theming properties.
        /// The full <see cref="PlotSerializer.PlotVisualProperties"/> list is still used for serialization.
        /// </summary>
        private static readonly IReadOnlyList<string> PlotUndoProperties =
            PlotSerializer.PlotVisualProperties.Where(p => !WpfThemingProperties.Contains(p)).ToList();

        /// <summary>
        /// Axis visual properties for undo tracking, excluding WPF theming properties.
        /// The full <see cref="AxisSerializer.AxisVisualProperties"/> list is still used for serialization.
        /// </summary>
        private static readonly IReadOnlyList<string> AxisUndoProperties =
            AxisSerializer.AxisVisualProperties.Where(p => !WpfThemingProperties.Contains(p)).ToList();

        /// <summary>
        /// Series visual properties for undo tracking, excluding WPF theming properties.
        /// The full <see cref="SeriesSerializer.SeriesVisualProperties"/> list is still used for serialization.
        /// </summary>
        private static readonly IReadOnlyList<string> SeriesUndoProperties =
            SeriesSerializer.SeriesVisualProperties.Where(p => !WpfThemingProperties.Contains(p)).ToList();

        /// <summary>
        /// Annotation visual properties for undo tracking, excluding WPF theming properties.
        /// The full <see cref="AnnotationSerializer.AnnotationVisualProperties"/> list is still used for serialization.
        /// </summary>
        private static readonly IReadOnlyList<string> AnnotationUndoProperties =
            AnnotationSerializer.AnnotationVisualProperties.Where(p => !WpfThemingProperties.Contains(p)).ToList();

        /// <summary>
        /// The plot being managed.
        /// </summary>
        private readonly Plot _plot;

        /// <summary>
        /// Lambda that returns the <see cref="IUndoManager"/> when undo is enabled, or null otherwise.
        /// </summary>
        private readonly Func<IUndoManager> _getUndoManager;

        /// <summary>
        /// Human-readable description for undo action labels (e.g. "time series plot").
        /// </summary>
        private readonly string _description;

        /// <summary>
        /// The owning element, used as the undo action target.
        /// </summary>
        private readonly object _target;

        /// <summary>
        /// Callback invoked when any bridge records an undo action.
        /// </summary>
        private readonly Action _onActionRecorded;

        /// <summary>
        /// Bridge for plot-level visual properties (Title, LegendPosition, Background, etc.).
        /// </summary>
        private UndoableStateBridge _plotBridge;

        /// <summary>
        /// Per-axis state bridges. Rebuilt when <see cref="Plot.Axes"/> collection changes.
        /// </summary>
        private readonly List<UndoableStateBridge> _axisBridges = new List<UndoableStateBridge>();

        /// <summary>
        /// Per-series state bridges. Rebuilt explicitly via <see cref="RebuildSeriesAndAnnotationBridges"/>.
        /// </summary>
        private readonly List<UndoableStateBridge> _seriesBridges = new List<UndoableStateBridge>();

        /// <summary>
        /// Per-annotation state bridges. Rebuilt when <see cref="Plot.Annotations"/> collection changes
        /// or explicitly via <see cref="RebuildSeriesAndAnnotationBridges"/>.
        /// </summary>
        private readonly List<UndoableStateBridge> _annotationBridges = new List<UndoableStateBridge>();

        /// <summary>
        /// Whether recording is currently suspended.
        /// </summary>
        private bool _isSuspended;

        /// <summary>
        /// Reference count for nested <see cref="SuspendRecording"/> calls.
        /// Only the outermost scope actually suspends/resumes the bridges;
        /// inner scopes are no-ops that just increment/decrement the depth.
        /// </summary>
        private int _suspensionDepth;

        /// <summary>
        /// Whether <see cref="RebuildSeriesAndAnnotationBridges"/> was called while suspended.
        /// When true, the outermost <see cref="SuspensionScope"/> will perform the deferred
        /// rebuild upon dispose.
        /// </summary>
        private bool _needsRebuild;

        /// <summary>
        /// Whether this instance has been disposed.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlotUndoManager"/> class.
        /// </summary>
        /// <param name="plot">The plot to manage undo bridges for.</param>
        /// <param name="getUndoManager">Lambda returning the <see cref="IUndoManager"/> or null.</param>
        /// <param name="description">Human-readable description for undo action labels.</param>
        /// <param name="target">The owning element for undo action association.</param>
        /// <param name="onActionRecorded">Callback when any bridge records an undo action.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="plot"/>, <paramref name="getUndoManager"/>,
        /// or <paramref name="onActionRecorded"/> is null.
        /// </exception>
        public PlotUndoManager(Plot plot, Func<IUndoManager> getUndoManager, string description, object target, Action onActionRecorded)
        {
            _plot = plot ?? throw new ArgumentNullException(nameof(plot));
            _getUndoManager = getUndoManager ?? throw new ArgumentNullException(nameof(getUndoManager));
            _description = description ?? "plot";
            _target = target;
            _onActionRecorded = onActionRecorded ?? throw new ArgumentNullException(nameof(onActionRecorded));

            CreateAllBridges();

            // Auto-monitor axis collection for type changes (axis object replacement).
            _plot.Axes.CollectionChanged += OnAxesCollectionChanged;

            // Auto-monitor annotation collection for toolbar add/remove.
            _plot.Annotations.CollectionChanged += OnAnnotationsCollectionChanged;

            // Listen for axis type changes to record undoable replacement actions.
            _plot.AxisReplaced += OnAxisReplaced;

            // Listen for bulk annotation modification notifications (e.g. after suppressed
            // drag placement completes and Plot.NotifyAnnotationsModified() is called).
            _plot.PropertyChanged += OnPlotPropertyChanged;
        }

        /// <summary>
        /// Gets the plot that this manager is tracking.
        /// </summary>
        public Plot Plot => _plot;

        /// <summary>
        /// Suspends undo recording on all bridges managed by this instance.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        /// <remarks>
        /// <para>
        /// Use this during bulk UpdateXxxPlot() operations to prevent spurious undo entries.
        /// After disposing, call <see cref="RebuildSeriesAndAnnotationBridges"/> to reconnect
        /// bridges to the new series and annotations.
        /// </para>
        /// <para>
        /// Supports nesting: only the outermost call suspends the bridges; inner calls are
        /// lightweight no-ops. The bridges resume only when the outermost scope disposes.
        /// </para>
        /// </remarks>
        public IDisposable SuspendRecording()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PlotUndoManager),
                    "Cannot suspend recording on a disposed PlotUndoManager. Re-create the manager " +
                    "via the owning element's SetupBridges if a fresh suspension is needed after Dispose().");
            _suspensionDepth++;
            _isSuspended = true;

            // Nested call — bridges are already suspended by the outer scope.
            if (_suspensionDepth > 1)
                return new SuspensionScope(this, new List<IDisposable>());

            // Outermost call — actually suspend all bridges.
            var suspensions = new List<IDisposable>();

            if (_plotBridge != null)
                suspensions.Add(_plotBridge.SuspendRecording());

            foreach (var b in _axisBridges)
                suspensions.Add(b.SuspendRecording());

            foreach (var b in _seriesBridges)
                suspensions.Add(b.SuspendRecording());

            foreach (var b in _annotationBridges)
                suspensions.Add(b.SuspendRecording());

            return new SuspensionScope(this, suspensions);
        }

        /// <summary>
        /// Rebuilds per-item state bridges for series and annotations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this after UpdateXxxPlot() methods that clear and repopulate
        /// <see cref="Plot.Series"/> and <see cref="Plot.Annotations"/>.
        /// </para>
        /// <para>
        /// If called while recording is suspended (inside a <see cref="SuspendRecording"/> scope),
        /// the rebuild is deferred until the outermost scope disposes. This prevents creating
        /// live bridges that would fire PropertyChanged events during bulk updates.
        /// </para>
        /// </remarks>
        public void RebuildSeriesAndAnnotationBridges()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PlotUndoManager),
                    "Cannot rebuild bridges on a disposed PlotUndoManager. Calling this on a " +
                    "stale instance after Dispose() typically indicates a wrong-reference bug — " +
                    "the App-side control should use the owning element's current bridge manager.");

            // Defer rebuild until the outermost suspension scope exits.
            // Creating bridges while suspended would leave them live (unsuspended),
            // causing PropertyChanged recording during subsequent plot updates.
            if (_isSuspended)
            {
                _needsRebuild = true;
                return;
            }

            DisposeSeriesBridges();
            DisposeAnnotationBridges();

            foreach (Series series in _plot.Series)
            {
                _seriesBridges.Add(new UndoableStateBridge(
                    series, _getUndoManager, $"{_description} series", _target,
                    includedProperties: SeriesUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }

            foreach (Annotation annotation in _plot.Annotations)
            {
                _annotationBridges.Add(new UndoableStateBridge(
                    annotation, _getUndoManager, $"{_description} annotation", _target,
                    includedProperties: AnnotationUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }
        }

        /// <summary>
        /// Releases all bridges and unsubscribes from collection events.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _plot.Axes.CollectionChanged -= OnAxesCollectionChanged;
            _plot.Annotations.CollectionChanged -= OnAnnotationsCollectionChanged;
            _plot.AxisReplaced -= OnAxisReplaced;
            _plot.PropertyChanged -= OnPlotPropertyChanged;

            _plotBridge?.Dispose();
            _plotBridge = null;

            DisposeAxisBridges();
            DisposeSeriesBridges();
            DisposeAnnotationBridges();
        }

        /// <summary>
        /// Creates all bridges for the plot, axes, series, and annotations.
        /// </summary>
        private void CreateAllBridges()
        {
            _plotBridge = new UndoableStateBridge(
                _plot, _getUndoManager, _description, _target,
                includedProperties: PlotUndoProperties,
                onActionRecorded: _onActionRecorded);

            foreach (Axis axis in _plot.Axes)
            {
                _axisBridges.Add(new UndoableStateBridge(
                    axis, _getUndoManager, $"{_description} axis", _target,
                    includedProperties: AxisUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }

            foreach (Series series in _plot.Series)
            {
                _seriesBridges.Add(new UndoableStateBridge(
                    series, _getUndoManager, $"{_description} series", _target,
                    includedProperties: SeriesUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }

            foreach (Annotation annotation in _plot.Annotations)
            {
                _annotationBridges.Add(new UndoableStateBridge(
                    annotation, _getUndoManager, $"{_description} annotation", _target,
                    includedProperties: AnnotationUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }
        }

        /// <summary>
        /// Handles <see cref="Plot.Axes"/> collection changes to rebuild axis bridges.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// When the user changes an axis type (e.g. Linear to Logarithmic), the
        /// <c>AxesControl</c> removes the old axis and adds a new one.
        /// This handler rebuilds axis bridges so the new axis object is monitored.
        /// Changes during suspended recording are ignored; the caller should
        /// call <see cref="RebuildSeriesAndAnnotationBridges"/> after resuming.
        /// </remarks>
        private void OnAxesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed || _isSuspended) return;
            RebuildAxisBridges();
        }

        /// <summary>
        /// Handles <see cref="Plot.AxisReplaced"/> events to record an undoable axis type change.
        /// </summary>
        /// <param name="oldAxis">The axis that was removed from the collection.</param>
        /// <param name="newAxis">The axis that was inserted in its place.</param>
        /// <remarks>
        /// When the user changes an axis type (e.g. Linear to Logarithmic) via <c>AxisControl</c>,
        /// <see cref="Plot.ReplaceAxis"/> fires this event after atomically swapping the axis objects.
        /// This handler records a <see cref="DelegateAction"/> so the swap can be undone with Ctrl+Z.
        /// The <see cref="IUndoManager.IsExecutingAction"/> guard prevents re-recording during undo/redo replay,
        /// since the DelegateAction's execute/undo lambdas also call <see cref="Plot.ReplaceAxis"/> which
        /// re-fires this event.
        /// </remarks>
        private void OnAxisReplaced(Axis oldAxis, Axis newAxis)
        {
            if (_isDisposed || _isSuspended) return;
            var undoManager = _getUndoManager();
            if (undoManager == null || undoManager.IsExecutingAction) return;

            var action = new DelegateAction(
                $"Change {_description} axis type",
                () => _plot.ReplaceAxis(oldAxis, newAxis),
                () => _plot.ReplaceAxis(newAxis, oldAxis),
                _target);
            undoManager.RecordAction(action);
            _onActionRecorded?.Invoke();
        }

        /// <summary>
        /// Handles <see cref="Plot.Annotations"/> collection changes to rebuild annotation bridges.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// When the user adds or removes an annotation via the toolbar, the
        /// <c>AnnotationSelectorControl</c> modifies <see cref="Plot.Annotations"/>.
        /// This handler rebuilds annotation bridges for the new set of annotations.
        /// Changes during suspended recording are ignored.
        /// </remarks>
        private void OnAnnotationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed || _isSuspended) return;
            RebuildAnnotationBridgesFromCollection();
        }

        /// <summary>
        /// Handles <see cref="Plot.PropertyChanged"/> events to detect bulk annotation modifications.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// When the OxyPlotToolbar finishes placing an annotation, it calls
        /// <see cref="Plot.NotifyAnnotationsModified"/> which fires <c>PropertyChanged("Annotations")</c>.
        /// This handler rebuilds annotation bridges so they pick up the final property values
        /// as their shadow baseline, rather than the initial values from before the drag.
        /// </remarks>
        private void OnPlotPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed || _isSuspended) return;
            if (e.PropertyName == "Annotations")
            {
                RebuildAnnotationBridgesFromCollection();
            }
        }

        /// <summary>
        /// Disposes and rebuilds all axis bridges from the current <see cref="Plot.Axes"/> collection.
        /// </summary>
        private void RebuildAxisBridges()
        {
            DisposeAxisBridges();
            foreach (Axis axis in _plot.Axes)
            {
                _axisBridges.Add(new UndoableStateBridge(
                    axis, _getUndoManager, $"{_description} axis", _target,
                    includedProperties: AxisUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }
        }

        /// <summary>
        /// Disposes and rebuilds annotation bridges from the current <see cref="Plot.Annotations"/> collection.
        /// </summary>
        private void RebuildAnnotationBridgesFromCollection()
        {
            DisposeAnnotationBridges();
            foreach (Annotation annotation in _plot.Annotations)
            {
                _annotationBridges.Add(new UndoableStateBridge(
                    annotation, _getUndoManager, $"{_description} annotation", _target,
                    includedProperties: AnnotationUndoProperties,
                    onActionRecorded: _onActionRecorded));
            }
        }

        /// <summary>
        /// Disposes all axis bridges and clears the list.
        /// </summary>
        private void DisposeAxisBridges()
        {
            foreach (var b in _axisBridges) b.Dispose();
            _axisBridges.Clear();
        }

        /// <summary>
        /// Disposes all series bridges and clears the list.
        /// </summary>
        private void DisposeSeriesBridges()
        {
            foreach (var b in _seriesBridges) b.Dispose();
            _seriesBridges.Clear();
        }

        /// <summary>
        /// Disposes all annotation bridges and clears the list.
        /// </summary>
        private void DisposeAnnotationBridges()
        {
            foreach (var b in _annotationBridges) b.Dispose();
            _annotationBridges.Clear();
        }

        /// <summary>
        /// Aggregates multiple <see cref="IDisposable"/> suspension tokens and
        /// manages the <see cref="_isSuspended"/> flag via reference counting.
        /// </summary>
        /// <remarks>
        /// Only the outermost scope (the one that holds the actual bridge tokens)
        /// resumes recording on dispose. Inner (nested) scopes hold empty lists
        /// and simply decrement the depth counter.
        /// </remarks>
        private class SuspensionScope : IDisposable
        {
            /// <summary>
            /// The owning <see cref="PlotUndoManager"/>.
            /// </summary>
            private readonly PlotUndoManager _owner;

            /// <summary>
            /// The individual bridge suspension tokens. Empty for nested scopes.
            /// </summary>
            private readonly List<IDisposable> _suspensions;

            /// <summary>
            /// Initializes a new instance of the <see cref="SuspensionScope"/> class.
            /// </summary>
            /// <param name="owner">The owning <see cref="PlotUndoManager"/>.</param>
            /// <param name="suspensions">The individual bridge suspension tokens.</param>
            public SuspensionScope(PlotUndoManager owner, List<IDisposable> suspensions)
            {
                _owner = owner;
                _suspensions = suspensions;
            }

            /// <summary>
            /// Decrements the suspension depth. When the outermost scope disposes,
            /// resumes recording on all bridges, clears the suspended flag, and
            /// performs any deferred <see cref="RebuildSeriesAndAnnotationBridges"/> call.
            /// </summary>
            public void Dispose()
            {
                _owner._suspensionDepth--;
                if (_owner._suspensionDepth <= 0)
                {
                    // Resume the original bridge suspension tokens.
                    for (int i = _suspensions.Count - 1; i >= 0; i--)
                        _suspensions[i].Dispose();
                    _owner._isSuspended = false;
                    _owner._suspensionDepth = 0;

                    // Perform deferred rebuild if any UpdateXxxPlot called
                    // RebuildSeriesAndAnnotationBridges while we were suspended.
                    if (_owner._needsRebuild)
                    {
                        _owner._needsRebuild = false;
                        _owner.RebuildSeriesAndAnnotationBridges();
                    }
                }
            }
        }
    }
}
