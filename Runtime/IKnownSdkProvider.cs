using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DTech.LinkGuard
{
	public interface IKnownSdkProvider
	{
		IEnumerable<Regex> GetSdkPatterns();
	}
}