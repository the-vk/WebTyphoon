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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WebTyphoon;

namespace ConsoleHost
{
    class Program
    {
        private static WebSocketConnection _connection;
        private static Stopwatch _stopwatch = new Stopwatch();
        private static int _i;
        private const int MessageCount = 100000;

        static void Main()
        {
            var webTyphoon = new WebTyphoon.WebTyphoon();

            webTyphoon.AddUriBinding(new[] { "/test" }, new[] { "test" }, new[] { "http://cryoengine.net" }, null, WebTyphoonConnectionAccepted);

            var ipEndpoint = new IPEndPoint(IPAddress.Any, 9000);
            var tcpListener = new TcpListener(ipEndpoint);
            tcpListener.Start();

            while(true)
            {
                var client = tcpListener.AcceptTcpClient();
                webTyphoon.AcceptConnection(client.GetStream());
            }
        }

        static void WebTyphoonConnectionAccepted(object sender, WebSocketConnectionEventArgs e)
        {
            _connection = e.Connection;
            _connection.WebSocketFragmentRecieved += ConnectionWebSocketFragmentRecieved;
        }

        static void ConnectionWebSocketFragmentRecieved(object sender, WebSocketFragmentRecievedEventArgs e)
        {
            lock (_stopwatch)
            {
                if (_i == 0) _stopwatch.Start();
                //Console.WriteLine(e.Fragment.PayloadString);
                //var fragment = new WebTyphoon.WebSocketFragment(true, OpCode.TextFrame, e.Fragment.PayloadString);
                //_connection.SendFragment(fragment);
                ++_i;
                if (_i >= MessageCount)
                {
                    _stopwatch.Stop();
                    var time = (double) _stopwatch.ElapsedTicks/Stopwatch.Frequency;
                    _stopwatch.Reset();
                    var messagesPerSec = MessageCount/(time);
                    Console.WriteLine(messagesPerSec);
                    _i = 0;
                    _stopwatch.Start();
                }
            }
        }
    }
}
