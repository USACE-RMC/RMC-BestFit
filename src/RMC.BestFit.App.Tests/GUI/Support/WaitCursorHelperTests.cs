using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Source-level regression tests for the lazy plot wait-cursor helper.
    /// </summary>
    /// <remarks>
    /// The App test project is often built with dependency builds skipped to avoid unrelated
    /// core-library rebuild failures. These tests pin the helper implementation without forcing
    /// a direct reference to the WinExe assembly metadata.
    /// </remarks>
    [TestClass]
    public class WaitCursorHelperTests
    {
        /// <summary>
        /// Verifies the helper sets a visible wait cursor before synchronous work begins.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursor_SetsCursorBeforeAction()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursor");

            StringAssert.Contains(method, "Mouse.OverrideCursor = Cursors.Wait;");
            StringAssert.Contains(method, "Mouse.UpdateCursor();");
            StringAssert.Contains(method, "action();");
        }

        /// <summary>
        /// Verifies the helper restores the previous cursor after protected work completes.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursor_RestoresPreviousCursorAtBackgroundPriority()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursor");

            StringAssert.Contains(method, "Cursor previousCursor = Mouse.OverrideCursor;");
            StringAssert.Contains(method, "dispatcher.BeginInvoke(DispatcherPriority.Background");
            StringAssert.Contains(method, "Mouse.OverrideCursor = previousCursor;");
        }

        /// <summary>
        /// Verifies the helper does not push a nested dispatcher frame.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursor_DoesNotSynchronouslyInvokeDispatcher()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursor");

            Assert.IsFalse(method.Contains("dispatcher.Invoke(", StringComparison.Ordinal),
                "Synchronous dispatcher invocation can fail while WPF dispatcher processing is suspended.");
        }

        /// <summary>
        /// Verifies the helper validates its required arguments.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursor_ValidatesArguments()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursor");

            StringAssert.Contains(method, "throw new ArgumentNullException(nameof(dispatcher))");
            StringAssert.Contains(method, "throw new ArgumentNullException(nameof(action))");
        }

        /// <summary>
        /// Verifies the asynchronous helper sets the wait cursor before yielding and running work.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursorAsync_SetsCursorBeforeAwaitingAction()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursorAsync");

            StringAssert.Contains(method, "Mouse.OverrideCursor = Cursors.Wait;");
            StringAssert.Contains(method, "Mouse.UpdateCursor();");
            StringAssert.Contains(method, "await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);");
            StringAssert.Contains(method, "await action();");
        }

        /// <summary>
        /// Verifies the asynchronous helper restores the previous cursor at background priority.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursorAsync_RestoresPreviousCursorAtBackgroundPriority()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursorAsync");

            StringAssert.Contains(method, "Cursor previousCursor = Mouse.OverrideCursor;");
            StringAssert.Contains(method, "dispatcher.BeginInvoke(DispatcherPriority.Background");
            StringAssert.Contains(method, "Mouse.OverrideCursor = previousCursor;");
        }

        /// <summary>
        /// Verifies the asynchronous helper avoids synchronous dispatcher invocation.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursorAsync_DoesNotSynchronouslyInvokeDispatcher()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursorAsync");

            Assert.IsFalse(method.Contains("dispatcher.Invoke(", StringComparison.Ordinal),
                "Synchronous dispatcher invocation can block render updates before the batch starts.");
        }

        /// <summary>
        /// Verifies the asynchronous helper validates its required arguments.
        /// </summary>
        [TestMethod]
        public void RunWithVisibleWaitCursorAsync_ValidatesArguments()
        {
            string source = ReadWaitCursorHelperSource();
            string method = ExtractMethodSource(source, "RunWithVisibleWaitCursorAsync");

            StringAssert.Contains(method, "throw new ArgumentNullException(nameof(dispatcher))");
            StringAssert.Contains(method, "throw new ArgumentNullException(nameof(action))");
        }

        /// <summary>
        /// Reads the wait-cursor helper source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>WaitCursorHelper.cs</c>.</returns>
        private static string ReadWaitCursorHelperSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(
                FindRepoRoot(sourceFilePath),
                "src",
                "RMC.BestFit.App",
                "GUI",
                "Support",
                "WaitCursorHelper.cs"));
        }

        /// <summary>
        /// Extracts a method body from a C# source file using brace counting.
        /// </summary>
        /// <param name="source">The C# source text.</param>
        /// <param name="methodName">The method name to extract.</param>
        /// <returns>The method source, including its signature and braces.</returns>
        private static string ExtractMethodSource(string source, string methodName)
        {
            int start = source.IndexOf(methodName + "(", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, methodName + " should exist.");

            int openingBrace = source.IndexOf('{', start);
            Assert.IsTrue(openingBrace > start, methodName + " should have an opening brace.");

            int depth = 0;
            for (int i = openingBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail(methodName + " should have a closing brace.");
            return string.Empty;
        }

        /// <summary>
        /// Finds the repository root from the test output directory.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The absolute repository root.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source tree cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            string[] searchRoots = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(sourceFilePath)
            };

            foreach (string searchRoot in searchRoots)
            {
                if (string.IsNullOrEmpty(searchRoot)) continue;
                DirectoryInfo directory = new DirectoryInfo(searchRoot);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "src", "RMC.BestFit.App");
                    if (Directory.Exists(candidate))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
        }
    }
}
