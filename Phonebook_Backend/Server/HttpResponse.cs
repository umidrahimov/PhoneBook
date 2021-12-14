using System;
using System.Collections.Generic;
using System.Text;

namespace Phonebook_Backend.Server
{
    public class HttpResponse
    {
        public int StatusCode
        {
            get;
            set;
        }

        public string StatusDescription
        {
            get;
            set;
        }

        public Dictionary<string, string> Headers
        {
            get;
            private set;
        }

        public string Body
        {
            get;
            set;
        }

        public HttpResponse()
        {
            StatusCode = 200;
            StatusDescription = "OK";
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
