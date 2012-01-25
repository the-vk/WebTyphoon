using System.Collections.Generic;
using System.Linq;

namespace WebTyphoon
{
    class HttpMessage
    {
        public string Method { get; set; }
        public string Version { get; set; }
        public string Uri { get; set; }
        public Dictionary<string, string> Headers { get; private set; }

        public HttpMessage()
        {
            Headers = new Dictionary<string, string>();
        }

        public HttpMessage(IEnumerable<string> lines) : this()
        {
            var firstLine = lines.First();
            var firstLineParts = firstLine.Split(' ');
            Method = firstLineParts[0];
            Uri = firstLineParts[1];
            Version = firstLineParts[2];
            foreach (var l in lines.Skip(1))
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
