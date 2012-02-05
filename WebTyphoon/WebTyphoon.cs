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
using System.Net.Sockets;
using System.Threading;

namespace WebTyphoon
{
    public class WebTyphoon
    {
        private readonly List<WebSocketConnection> _connections;

        private const int WorkersCount = 10;
        private readonly List<WorkerThread> _workers;
        private readonly Thread _dispatch;

        private readonly Dictionary<string, ConnectionHandlerData> _uriBindings;

        public WebTyphoon()
        {
            _connections = new List<WebSocketConnection>();
            _uriBindings = new Dictionary<string, ConnectionHandlerData>();

            _workers = new List<WorkerThread>();

            for (var i = 0; i < WorkersCount; ++i)
            {
                _workers.Add(CreateWorkerThread());
                _workers[i].Start();
            }

           _dispatch = new Thread(Dispatch);
           _dispatch.Start();
        }

        public void AcceptConnection(NetworkStream stream)
        {
            var handshaker = new WebSocketHandshaker(stream, this);
            handshaker.HandshakeSuccess += HandshakeSuccessHandler;
            handshaker.HandshakeFailed += HandshakeFailedHandler;

            handshaker.Handshake();
        }

        public void AddUriBinding(IEnumerable<string> uris,
            IEnumerable<string> protocols,
            IEnumerable<string> origins,
            EventHandler<WebSocketConnectionAcceptEventArgs> connectionAcceptHandler,
            EventHandler<WebSocketConnectionEventArgs> connectionSuccessHandler)
        {
            foreach (var u in uris)
            {
                var hd = new ConnectionHandlerData
                             {
                                 Uri = u,
                                 AcceptedOrigins = origins.ToList(),
                                 AcceptedProtocols = protocols.ToList(),
                                 ConnectionAcceptHandler = connectionAcceptHandler,
                                 ConnectionSuccessHandler = connectionSuccessHandler
                             };
                _uriBindings.Add(u, hd);
            }
        }

        internal ConnectionHandlerData GetBinding(string uri)
        {
            return _uriBindings[uri];
        }

        protected event EventHandler<WebSocketConnectionEventArgs> ConnectionAccepted;
        protected void OnConnectionAccepted(object sender, WebSocketConnectionEventArgs e)
        {
            if (ConnectionAccepted != null)
            {
                ConnectionAccepted(sender, e);
            }
        }

        protected event EventHandler<WebSocketConnectionEventArgs> ConnectionFailed;
        protected void OnConnectionFailed(object sender, WebSocketConnectionEventArgs e)
        {
            if (ConnectionFailed != null)
            {
                ConnectionFailed(sender, e);
            }
        }

        private void HandshakeSuccessHandler(object sender, WebSocketConnectionEventArgs e)
        {
            var connection = CreateNewConnection(e.Stream);
            e.Connection = connection;
            var hd = GetBinding(e.Uri);
            if(hd.ConnectionSuccessHandler != null)
            {
                hd.ConnectionSuccessHandler(this, e);
            }
            OnConnectionAccepted(this, e);
        }

        private void HandshakeFailedHandler(object sender, WebSocketConnectionEventArgs e)
        {
            OnConnectionFailed(this, e);
        }

        private WebSocketConnection CreateNewConnection(NetworkStream stream)
        {
            var connection = new WebSocketConnection(stream);
            lock (_connections)
            {
                _connections.Add(connection);
            }

            connection.Closed += ConnectionClosedHandler;

            return connection;
        }

        private void ConnectionClosedHandler(object sender, WebSocketConnectionStateChangeEventArgs e)
        {
            var cn = e.Connection;

            cn.Closed -= ConnectionClosedHandler;
            lock (_connections)
            {
                _connections.Remove(cn);
            }
        }

        private static void Worker(object workerData)
        {
            var data = (WorkerStartData) workerData;
            var workerQueue = data.Queue;
            var workSignal = data.WorkSignal;

            while (true)
            {
                while (workerQueue.Count != 0)
                {
                    WebSocketConnection cn;
                    lock (workerQueue)
                    {
                        cn = workerQueue.Dequeue();
                    }
                    if (cn.HasWork)
                    {
                        cn.Process();
                        cn.Processing = false;
                    }
                }
                workSignal.WaitOne();
            }
        }

        private void Dispatch()
        {
            while (true)
            {
                IEnumerable<WebSocketConnection> cons;
                lock (_connections)
                {
                    cons = _connections.ToArray();
                }
                foreach (var c in cons)
                {
                    if (c.Status != WebSocketConnectionStatus.Closed && !c.Processing && c.HasWork)
                    {
                        lock (_workers)
                        {
                            var freeQueueWorker = (from w in _workers orderby w.Queue.Count ascending select w).First();
                            c.Processing = true;
                            lock (freeQueueWorker.Queue)
                            {
                                freeQueueWorker.Queue.Enqueue(c);
                                freeQueueWorker.WorkSignal.Set();
                            }
                        }
                    }
                }
                Thread.Sleep(100);
            }
        }

        private WorkerThread CreateWorkerThread()
        {
            var wt = new WorkerThread
                         {
                             Queue = new Queue<WebSocketConnection>(), 
                             Worker = new Thread(Worker),
                             WorkSignal = new EventWaitHandle(false, EventResetMode.AutoReset)
                         };

            return wt;
        }
    }

    struct WorkerStartData
    {
        public Queue<WebSocketConnection> Queue;
        public EventWaitHandle WorkSignal;
    }
}
