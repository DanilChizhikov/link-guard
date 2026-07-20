using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DTech.LinkGuard
{
	/// <summary>
	/// Supplies regular expressions that group assemblies into a named SDK when the
	/// generator builds the assembly tree. Implement to teach Link Guard about custom
	/// or third-party SDKs.
	/// </summary>
	public interface IKnownSdkProvider
	{
		/// <summary>
		/// Returns the assembly-name patterns that identify this SDK's assemblies.
		/// </summary>
		/// <returns>The regular expressions matched against assembly names.</returns>
		IEnumerable<Regex> GetSdkPatterns();
	}
}
