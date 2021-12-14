using System;

namespace Phonebook_Backend.Server
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public HttpRequest Request
        {
            get;
            private set;
        }

        public HttpResponse Response
        {
            get;
            set;
        }

        public MessageReceivedEventArgs(HttpRequest request)
        {
            Request = request;
        }
    }
}
