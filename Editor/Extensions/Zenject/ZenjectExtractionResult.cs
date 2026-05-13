#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
	internal sealed class ZenjectExtractionResult
	{
		public IReadOnlyCollection<TypeIdentifier> BoundTypes { get; }
		public IReadOnlyCollection<Type> InstallEdges { get; }

		public ZenjectExtractionResult(
			IReadOnlyCollection<TypeIdentifier> boundTypes,
			IReadOnlyCollection<Type> installEdges)
		{
			BoundTypes = boundTypes ?? Array.Empty<TypeIdentifier>();
			InstallEdges = installEdges ?? Array.Empty<Type>();
		}
	}
}
#endif