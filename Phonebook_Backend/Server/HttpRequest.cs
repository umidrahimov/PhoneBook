using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phonebook_Backend.Server
{
    public class HttpRequest
    {
        public string URI
        {
            get;
            set;
        }

        public string Method
        {
            get;
            set;
        }

        public Dictionary<string, string> Headers
        {
            get;
            private set;
        }

        public Dictionary<string, string> Parameters
        {
            get;
            private set;
        }

        public string QueryString
        {
            get;
            set;
        }

        public string Body
        {
            get;
            set;
        }

        public string Prefix
        {
            get;
            protected internal set;
        }

        internal string Content
        {
            get
            {
                if (Method == "POST" && !string.IsNullOrEmpty(Body))
                {
                    return Body;
                }
                if (!string.IsNullOrEmpty(QueryString))
                {
                    return QueryString;
                }
                if (Parameters.Count > 0)
                {
                    return string.Join("&", (from pair in Parameters
                                             select $"{pair.Key}={pair.Value}").ToArray());
                }
                return "<null>";
            }
        }

        public HttpRequest()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
