using Microsoft.VisualStudio.TestTools.UnitTesting;

// Explicit namespace qualification so this file can be linked into test projects
// that disable ImplicitUsings (e.g., RMC.BestFit.App.Tests).
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
