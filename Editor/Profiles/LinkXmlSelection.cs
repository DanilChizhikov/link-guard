using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
	[Serializable]
	internal sealed class LinkXmlSelection
	{
		public string Assembly;
		public bool PreserveAll;
		public bool IgnoreIfMissing;

		public List<string> Namespaces = new();
		public List<string> GlobalTypes = new();
		public List<string> Types = new();
		public List<LinkXmlMethodSelection> Methods = new();
	}
}
