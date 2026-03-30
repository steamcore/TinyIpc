#pragma warning disable IDE0130 // Namespace does not match folder structure
#if !NET
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(
	validOn: AttributeTargets.Method,
	Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute;
#endif
