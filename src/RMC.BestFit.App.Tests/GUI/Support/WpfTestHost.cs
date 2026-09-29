using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Supplies application resources and dispatcher draining for real WPF control regressions.
    /// </summary>
    /// <remarks>Call from an STA test marked nonparallel because application resources are shared.</remarks>
    internal static class WpfTestHost
    {
        /// <summary>Resource dictionaries installed for the current STA test.</summary>
        private static readonly List<ResourceDictionary> _dictionaries = new List<ResourceDictionary>();
        /// <summary>The STA thread that created the current resource instances.</summary>
        private static int _resourceThreadId;
        /// <summary>Whether this host supplied the shell-only separator brush.</summary>
        private static bool _addedSeparatorBrush;
        /// <summary>
        /// Loads the production resource dictionaries required by analysis controls.
        /// </summary>
        internal static void EnsureResources()
        {
            if (Application.Current == null) _ = new Application();
            if (_dictionaries.Count > 0 && _resourceThreadId == Environment.CurrentManagedThreadId) return;
            ReleaseResources();
            var resources = Application.Current.Resources;
            _resourceThreadId = Environment.CurrentManagedThreadId;

            // Load the same theme graph directly. A test's Application may have been created on
            // an earlier MSTest STA thread, so invoking the desktop theme service could deadlock.
            foreach (var resource in new[]
            {
                ("Themes", "Resources/Controls/Merged.xaml"),
                ("Themes", "Resources/Colors/LightColors.xaml"),
                ("FrameworkUI", "Themes/VS2013/LightTheme.xaml"),
                ("GenericControls", "Themes/GenericControlsTheme.xaml"),
                ("OxyPlotControls", "Themes/OxyPlotControlsTheme.xaml"),
                ("NumericControls", "Themes/NumericControlsTheme.xaml"),
                ("DatabaseControls", "Themes/DatabaseControlsTheme.xaml"),
                ("RMC.BestFit.UI", "Resources/IconDictionary.xaml"),
                ("RMC-BestFit", "Resources/IconDictionary.xaml"),
                ("GenericControls", "Resources/ResourceDictionary.xaml")
            })
            {
                var source = new Uri($"pack://application:,,,/{resource.Item1};component/{resource.Item2}");
                var dictionary = new ResourceDictionary { Source = source };
                resources.MergedDictionaries.Add(dictionary);
                _dictionaries.Add(dictionary);
            }

            // The desktop shell supplies this brush; control-only hosts do not create that shell.
            _addedSeparatorBrush = !resources.Contains("SeparatorBackground");
            if (_addedSeparatorBrush) resources["SeparatorBackground"] = Brushes.Gray;
        }

        /// <summary>
        /// Removes test-owned dictionaries so vector icons are not reused by a different STA thread.
        /// </summary>
        internal static void ReleaseResources()
        {
            if (Application.Current != null)
            {
                foreach (var dictionary in _dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                if (_addedSeparatorBrush) Application.Current.Resources.Remove("SeparatorBackground");
            }
            _dictionaries.Clear();
            _addedSeparatorBrush = false;
        }

        /// <summary>
        /// Processes queued bindings, result notifications, and background cursor resets on the owning thread.
        /// </summary>
        internal static void DrainDispatcher()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }
    }
}
