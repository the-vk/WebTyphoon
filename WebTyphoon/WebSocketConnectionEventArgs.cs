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
