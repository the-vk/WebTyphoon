using System.Collections.Generic;
using System.Linq;

namespace WebTyphoon
{
	class HttpResponse
	{
		public string Version { get; set; }
		public string ResponseCode { get; set; }
		public string Reason { get; set; }
		public IDictionary<string, string> Headers { get; set; }

		public HttpResponse()
		{
			Headers = new Dictionary<string, string>();
		}

		public HttpResponse(IEnumerable<string> lines)
			: this()
		{
			var lns = lines.ToList();
			var firstLine = lns.First();
			var firstLineParts = firstLine.Split(' ');

			Version = firstLineParts[0];
			ResponseCode = firstLineParts[1];
			if (firstLineParts.Length >= 3) Reason = firstLineParts[2];

			foreach (var l in lns.Skip(1))
			{
				var splitterIndex = l.IndexOf(':');
				var name = l.Substring(0, splitterIndex).Trim();
				var value = l.Substring(splitterIndex + 1).Trim();
				if (!Headers.ContainsKey(name))
				{
					Headers.Add(name, value);
				}
				else
				{
					Headers[name] = Headers[name] + " " + value;
				}
			}
		}
	}
}
