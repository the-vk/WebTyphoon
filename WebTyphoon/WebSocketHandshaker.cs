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
            while(!String.IsNullOrEmpty((str = sr.ReadLine())))
            {
                strings.Add(str);
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
               !message.Headers.ContainsKey("Sec-WebSocket-Key") ||
               !message.Headers.ContainsKey("Origin") ||
               !hd.AcceptedOrigins.Contains(message.Headers["Origin"]))
            {
                OnHandshakeFailed(this, new WebSocketConnectionEventArgs(null, _stream, message.Uri, message.Headers["Origin"], null, message.Headers));
                return;
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
