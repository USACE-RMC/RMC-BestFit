using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Converts a <see cref="BatchRunStatus"/> value to its corresponding
    /// <c>DrawingImage</c> icon from the application's <c>IconDictionary.xaml</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This converter is used in the <see cref="BatchRunWindow"/> DataGrid to display
    /// the appropriate status icon (pending clock, running spinner, green check, red X)
    /// for each analysis row. The icons are defined as <c>DrawingImage</c> resources
    /// in <c>Resources/IconDictionary.xaml</c> and are theme-neutral (they look correct
    /// on both light and dark backgrounds).
    /// </para>
    /// <para>
    /// Resource keys used:
    /// <list type="bullet">
    /// <item><description><c>StatusPendingIcon</c> — gray clock for <see cref="BatchRunStatus.Pending"/></description></item>
    /// <item><description><c>StatusRunningIcon</c> — blue circular arrows for <see cref="BatchRunStatus.Running"/></description></item>
    /// <item><description><c>StatusSucceededIcon</c> — green circle with white check for <see cref="BatchRunStatus.Succeeded"/></description></item>
    /// <item><description><c>StatusFailedIcon</c> — red circle with white X for <see cref="BatchRunStatus.Failed"/></description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class BatchRunStatusToIconConverter : IValueConverter
    {
        /// <summary>
        /// Converts a <see cref="BatchRunStatus"/> value to its corresponding icon resource.
        /// </summary>
        /// <param name="value">The <see cref="BatchRunStatus"/> value to convert.</param>
        /// <param name="targetType">The target type (not used).</param>
        /// <param name="parameter">An optional parameter (not used).</param>
        /// <param name="culture">The culture information (not used).</param>
        /// <returns>
        /// A <c>DrawingImage</c> resource from the application resources, or <c>null</c>
        /// if the value is not a valid <see cref="BatchRunStatus"/>.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BatchRunStatus status)
            {
                string resourceKey;
                try
                {
                    resourceKey = BatchRunStatusIconKeys.ForStatus(status);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }

                return Application.Current.FindResource(resourceKey);
            }

            return null;
        }

        /// <summary>
        /// Not supported. This is a one-way converter.
        /// </summary>
        /// <param name="value">The value to convert back (not used).</param>
        /// <param name="targetType">The target type (not used).</param>
        /// <param name="parameter">An optional parameter (not used).</param>
        /// <param name="culture">The culture information (not used).</param>
        /// <returns>Never returns; always throws <see cref="NotSupportedException"/>.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("BatchRunStatusToIconConverter is a one-way converter.");
        }
    }
}
