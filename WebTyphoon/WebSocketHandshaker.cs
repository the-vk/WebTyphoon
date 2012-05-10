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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WebTyphoon
{
    class WebSocketHandshaker
    {
        private readonly NetworkStream _stream;
        private readonly WebTyphoon _dispatcher;

        private const string WebSocketKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public WebSocketHandshaker(NetworkStream stream, WebTyphoon dispatcher)
        {
            _stream = stream;

            _dispatcher = dispatcher;
        }

        public void Handshake()
        {
            var handshakeTask = new Task(HandshakeAction);
            handshakeTask.Start();
        }

        private void HandshakeAction()
        {
            var sr = new StreamReader(_stream);
            var sw = new StreamWriter(_stream);
            var strings = new List<string>();
            string str;
            try
            {
                while (!String.IsNullOrEmpty((str = sr.ReadLine())))
                {
                    strings.Add(str);
                }
            }
            catch (IOException ex)
            {
                OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, null, null, null, null));
                return;
            }

            var message = new HttpMessage(strings);

            var hd = _dispatcher.GetBinding(message.Uri);
            if(hd == null)
            {
                OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], null, message.Headers));
                return;
            }

            if(!message.Headers.ContainsKey("Upgrade") || message.Headers["Upgrade"] != "websocket" ||
               !message.Headers.ContainsKey("Sec-WebSocket-Version") || message.Headers["Sec-WebSocket-Version"] != "13" ||
               !message.Headers.ContainsKey("Sec-WebSocket-Key"))
            {
                OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], null, message.Headers));
                return;
            }

            if(hd.AcceptedOrigins != null)
            {
                if(!message.Headers.ContainsKey("Origin") ||
                    !hd.AcceptedOrigins.Contains(message.Headers["Origin"]))
                {
                    OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], null, message.Headers));
                    return;
                }
            }

            string responseProtocolsString = null;
            List<string> responseProtocols = null;

            if(hd.AcceptedProtocols != null)
            {
                if(!message.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                {
                    OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], null, message.Headers));
                    return;
                }
                var protocols = message.Headers["Sec-WebSocket-Protocol"].Split(',').Select(x => x.Trim());
                responseProtocols = hd.AcceptedProtocols.Intersect(protocols).ToList();
                if (!responseProtocols.Any())
                {
                    OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], responseProtocols, message.Headers));
                    return;
                }

                responseProtocolsString = String.Join(", ", responseProtocols);
            }

            if(hd.ConnectionAcceptHandler != null)
            {
                var e = new WebSocketConnectionAcceptEventArgs(message.Uri, message.Headers["Origin"], responseProtocols,
                                                               message.Headers);
                hd.ConnectionAcceptHandler(this, e);
                if(e.Reject)
                {
                    OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], responseProtocols, message.Headers));
                    return;
                }
            }

            var key = message.Headers["Sec-WebSocket-Key"];
            var responseKey = EncodeToBase64SHA1(key + WebSocketKeyGuid);

            sw.WriteLine("HTTP/1.1 101 Switching Protocols");
            sw.WriteLine("Upgrade: websocket");
            sw.WriteLine("Connection: Upgrade");
            sw.WriteLine(string.Format("Sec-WebSocket-Accept: {0}", responseKey));
            sw.WriteLine(string.Format("Sec-WebSocket-Protocol: {0}", responseProtocolsString));

            sw.WriteLine();

            sw.Flush();

            OnHandshakeSuccess(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], responseProtocols, message.Headers));
        }

        public event EventHandler<WebSocketConnectionEventArgs> HandshakeFailed;

        protected void OnHandshakeFailed(object sender, WebSocketConnectionEventArgs e)
        {
            var sw = new StreamWriter(_stream);
            sw.WriteLine("HTTP/1.1 404 Not Found");
            sw.WriteLine();
            sw.Flush();
            sw.Close();

            if(HandshakeFailed != null)
            {
                HandshakeFailed(sender, e);
            }
        }

        public event EventHandler<WebSocketConnectionEventArgs> HandshakeSuccess;

        protected void OnHandshakeSuccess(object sender, WebSocketConnectionEventArgs e)
        {
            if(HandshakeSuccess != null)
            {
                HandshakeSuccess(sender, e);
            }
        }

        private string EncodeToBase64SHA1(string input)
        {
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var sha1 = new SHA1CryptoServiceProvider();
            inputBytes = sha1.ComputeHash(inputBytes);
            return Convert.ToBase64String(inputBytes);
        }
    }
}
