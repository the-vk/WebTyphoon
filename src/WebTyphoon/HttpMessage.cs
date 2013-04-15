/*
Copyright (C) 2012 Andrew 'the vk' Maraev

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections.Generic;
using System.Linq;

namespace WebTyphoon
{
	class HttpMessage
	{
		public string Method { get; set; }
		public string Version { get; set; }
		public string Uri { get; set; }
		public IDictionary<string, string> Headers { get; private set; }

		public string this[string header]
		{
			get { return Headers.ContainsKey(header) ? Headers[header] : null; }
			set { Headers[header] = value; }
		}

		public HttpMessage()
		{
			Headers = new Dictionary<string, string>();
		}

		public HttpMessage(IEnumerable<string> lines)
			: this()
		{
			var lns = lines.ToList();
			var firstLine = lns.First();
			var firstLineParts = firstLine.Split(' ');
			Method = firstLineParts[0];
			Uri = firstLineParts[1];
			Version = firstLineParts[2];
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
