using System;

namespace WebTyphoon
{
    public class WebSocketFragmentRecievedEventArgs : EventArgs
    {
        public WebSocketFragment Fragment { get; set; }

        public WebSocketFragmentRecievedEventArgs()
        {
            
        }

        public WebSocketFragmentRecievedEventArgs(WebSocketFragment fragment)
        {
            Fragment = fragment;
        }
    }
}
