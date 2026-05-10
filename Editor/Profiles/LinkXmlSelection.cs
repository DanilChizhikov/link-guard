using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
	[Serializable]
	internal sealed class LinkXmlSelection
	{
		public string Assembly { get; set; }
		public bool PreserveAll { get; set; }
		public bool IgnoreIfMissing { get; set; }

		public List<string> Namespaces { get; set; } = new();
		public List<string> GlobalTypes { get; set; } = new();
	}
}