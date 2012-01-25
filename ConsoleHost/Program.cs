using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsoleHost
{
    class Program
    {
        private static WebTyphoon.WebSocketConnection _connection;
        private static DateTime Start;
        private static DateTime End;
        private static int i = 0;

        static void Main(string[] args)
        {
            var webTyphoon = new WebTyphoon.WebTyphoon();

            webTyphoon.AddUriBinding(new string[] { "/test" }, new string[] { "test" }, new string[] { "http://cryoengine.net" }, null, webTyphoon_ConnectionAccepted);

            var ipEndpoint = new IPEndPoint(IPAddress.Any, 9000);
            var tcpListener = new TcpListener(ipEndpoint);
            tcpListener.Start();

            while(true)
            {
                var client = tcpListener.AcceptTcpClient();
                webTyphoon.AcceptConnection(client.GetStream());
            }
        }

        static void webTyphoon_ConnectionAccepted(object sender, WebTyphoon.WebSocketConnectionEventArgs e)
        {
            _connection = e.Connection;
            _connection.WebSocketFragmentRecieved += new EventHandler<WebTyphoon.WebSocketFragmentRecievedEventArgs>(_connection_WebSocketFragmentRecieved);
        }

        static void _connection_WebSocketFragmentRecieved(object sender, WebTyphoon.WebSocketFragmentRecievedEventArgs e)
        {
            if (i == 0) Start = DateTime.Now;
            Console.WriteLine(e.Fragment.PayloadString);
            ++i;
            if(i == 9999)
            {
                End = DateTime.Now;
                var time = End - Start;
                var messagesPerSec = 10000/time.TotalSeconds;
                i = 0;
            }
        }
    }
}
