using Microsoft.VisualStudio.TestTools.UnitTesting;

// Shared by public test projects so unit tests run in parallel without depending
// on the private verification project.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]