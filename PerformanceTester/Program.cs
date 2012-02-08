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

using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace PerformanceTester
{
    class Program
    {
        static void Main()
        {
            var testData = new byte[]
                                  {
                                      129,
                                      137,
                                      124,
                                      49,
                                      16,
                                      168,
                                      8,
                                      84,
                                      99,
                                      220,
                                      92,
                                      69,
                                      117,
                                      208,
                                      8
                                  };
            const string handshake = "GET /test HTTP/1.1\n" +
                                     "Upgrade: websocket\n" +
                                     "Connection: Upgrade\n" +
                                     "Host: cryoengine.net:9000\n" +
                                     "Origin: http://cryoengine.net\n" +
                                     "Sec-WebSocket-Protocol: test\n" +
                                     "Sec-WebSocket-Key: WxqRv8nGaoArDPGFCWrPFw==\n" +
                                     "Sec-WebSocket-Version: 13\n\n";

            var tcpClient = new TcpClient();
            tcpClient.Connect("localhost", 9000);

            while(!tcpClient.Connected)
            {
            }

            var stream = tcpClient.GetStream();

            var writer = new StreamWriter(stream);
            writer.Write(handshake);
            writer.Flush();
            Thread.Sleep(100);

            for(var i = 0; i < 10000000; ++i)
            {
                stream.Write(testData, 0, testData.Length);
            }
            stream.Flush();
        }
    }
}
