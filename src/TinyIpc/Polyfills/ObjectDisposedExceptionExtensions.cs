#if !NET
using System.Diagnostics.CodeAnalysis;

namespace System;

internal static class ObjectDisposedExceptionExtensions
{
	extension(ObjectDisposedException)
	{
		public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
		{
			if (condition)
			{
				throw new ObjectDisposedException(instance?.GetType().FullName);
			}
		}
	}
}
#endif
