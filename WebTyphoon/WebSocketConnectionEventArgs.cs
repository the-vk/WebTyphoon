using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace WebTyphoon
{
    public class WebSocketConnectionEventArgs : EventArgs
    {
        public WebSocketConnectionEventArgs(WebSocketConnection connection, NetworkStream stream,
            string uri,
            string origin,
            IEnumerable<string> protocols,
            IDictionary<string, string> headers)
        {
            Connection = connection;
            Stream = stream;

            Uri = uri;
            Origin = origin;
            Protocols = protocols;
            Headers = headers;
        }

        public WebSocketConnection Connection { get; set; }
        public NetworkStream Stream { get; set; }

        public string Uri { get; set; }
        public string Origin { get; set; }
        public IEnumerable<string> Protocols { get; set; }
        public IDictionary<string, string> Headers { get; set; }
    }

    public class WebSocketConnectionAcceptEventArgs : EventArgs
    {
        public WebSocketConnectionAcceptEventArgs(string uri,
            string origin,
            IEnumerable<string> protocols,
            IDictionary<string, string> headers)
        {
            Uri = uri;
            Origin = origin;
            Protocols = protocols;
            Headers = headers;

            Reject = false;
        }

        public string Uri { get; set; }
        public string Origin { get; set; }
        public IEnumerable<string> Protocols { get; set; }
        public IDictionary<string, string> Headers { get; set; }

        public bool Reject { get; set; }
    }
}
