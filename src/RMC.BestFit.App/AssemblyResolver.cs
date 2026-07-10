using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RMC_BestFit
{
    /// <summary>
    /// Provides assembly resolution from a "Libraries" subfolder beside the application executable.
    /// This allows the application to keep the exe directory clean by placing all supporting DLLs
    /// in a dedicated Libraries folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolver must be registered before any WPF types or external assemblies are loaded.
    /// Call <see cref="Register"/> at the very beginning of application startup.
    /// </para>
    /// <para>
    /// The Libraries folder is expected at: {exe directory}\Libraries\
    /// </para>
    /// </remarks>
    internal static class AssemblyResolver
    {
        /// <summary>
        /// The path to the Libraries folder beside the application executable.
        /// </summary>
        private static readonly string LibrariesPath =
            Path.Combine(AppContext.BaseDirectory, "Libraries");

        /// <summary>
        /// Registers the assembly resolver with the default <see cref="AssemblyLoadContext"/>.
        /// When the runtime cannot find an assembly in the default locations, this resolver
        /// checks the Libraries subfolder.
        /// </summary>
        /// <remarks>
        /// This method must be called before any external assemblies are referenced,
        /// typically as the first line in the application constructor or entry point.
        /// </remarks>
        public static void Register()
        {
            AssemblyLoadContext.Default.Resolving += ResolveFromLibraries;
        }

        /// <summary>
        /// Attempts to resolve an assembly from the Libraries subfolder.
        /// </summary>
        /// <param name="context">The assembly load context requesting resolution.</param>
        /// <param name="assemblyName">The name of the assembly to resolve.</param>
        /// <returns>The loaded assembly if found in the Libraries folder; otherwise, <c>null</c>.</returns>
        private static Assembly ResolveFromLibraries(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            string dllPath = Path.Combine(LibrariesPath, $"{assemblyName.Name}.dll");
            if (File.Exists(dllPath))
            {
                return context.LoadFromAssemblyPath(dllPath);
            }
            return null;
        }
    }
}
