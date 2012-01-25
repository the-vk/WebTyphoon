using System.Collections.Generic;
using System.Threading;

namespace WebTyphoon
{
    class WorkerThread
    {
        public Thread Worker { get; set; }
        public Queue<WebSocketConnection> Queue { get; set; }

        public void Start()
        {
            Worker.Start(Queue);
        }
    }
}
