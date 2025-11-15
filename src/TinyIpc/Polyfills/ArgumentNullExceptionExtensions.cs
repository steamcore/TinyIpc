#if !NET
using System.Runtime.CompilerServices;

namespace System;

internal static class ArgumentNullExceptionExtensions
{
	extension(ArgumentNullException)
	{
		public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
		{
			if (argument is null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}
}
#endif
