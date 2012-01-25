using System;
using System.Collections.Generic;

namespace WebTyphoon
{
    class ConnectionHandlerData
    {
        public string Uri { get; set; }
        public IEnumerable<string> AcceptedProtocols { get; set; }
        public IEnumerable<string> AcceptedOrigins { get; set; }
        public EventHandler<WebSocketConnectionAcceptEventArgs> ConnectionAcceptHandler;
        public EventHandler<WebSocketConnectionEventArgs> ConnectionSuccessHandler;
    }
}
