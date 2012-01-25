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
