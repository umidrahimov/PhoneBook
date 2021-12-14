using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Phonebook_Backend.Server
{
    public class HttpServer
    {
        private HttpListener _httpListener;

        private string _prefix;

        private bool _authentication;

        private int _activeSessions;

        private Thread _mainThread;

        public bool IsHealthy => true;

        public bool IsBusy => Thread.VolatileRead(ref _activeSessions) > 0;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public HttpServer(string prefix)
            : this(prefix, authentication: false)
        {
        }

        public HttpServer(string prefix, bool authentication)
        {
            _prefix = prefix;
            _authentication = authentication;
        }

        public void Open()
        {
            if (_httpListener != null)
            {
                return;
            }
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(_prefix);
            if (_authentication)
            {
                _httpListener.AuthenticationSchemes = AuthenticationSchemes.Basic;
            }
            _mainThread = new Thread(dispatcherThreadProc);
            _mainThread.Name = _prefix + " listening thread";
            _httpListener.Start();
            _mainThread.Start();
        }

        public void Close()
        {
            if (_httpListener == null)
            {
                return;
            }
            _mainThread.Abort();
            while (Thread.VolatileRead(ref _activeSessions) > 0)
            {
                Thread.Sleep(100);
            }
            _httpListener.Close();
            _httpListener = null;
            _mainThread = null;
        }

        public HttpRequest getHttpRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpRequest httpRequest = new HttpRequest();
            if (request.HasEntityBody)
            {
                using (StreamReader streamReader = new StreamReader(request.InputStream))
                {
                    httpRequest.Body = streamReader.ReadToEnd();
                }
            }
            httpRequest.Headers["User-agent"] = request.UserAgent;
            httpRequest.Method = request.HttpMethod;
            httpRequest.URI = request.Url.AbsolutePath;
            Encoding encoding = getEncoding(request.ContentType);
            foreach (string key in request.QueryString.Keys)
            {
                if (request.HasEntityBody)
                {
                    httpRequest.Parameters[key] = request.QueryString[key];
                }
                else
                {
                    byte[] bytes = request.ContentEncoding.GetBytes(request.QueryString[key]);
                    httpRequest.Parameters[key] = encoding.GetString(bytes);
                }
            }
            httpRequest.QueryString = request.Url.Query;
            for (int i = 0; i < request.Headers.Count; i++)
            {
                httpRequest.Headers[request.Headers.GetKey(i)] = request.Headers.Get(i);
            }
            if (context.User != null && context.User.Identity is HttpListenerBasicIdentity)
            {
                HttpListenerBasicIdentity httpListenerBasicIdentity = context.User.Identity as HttpListenerBasicIdentity;
            }
            httpRequest.Prefix = _prefix;
            return httpRequest;
        }

        public void setHttpResponse(HttpListenerContext context, HttpResponse clientResponse)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            response.KeepAlive = true;
            if (request.Headers["Connection"] != null && request.Headers["Connection"].ToLowerInvariant().Contains("close"))
            {
                response.KeepAlive = false;
            }
            response.ProtocolVersion = request.ProtocolVersion;
            response.StatusCode = clientResponse.StatusCode;
            response.StatusDescription = clientResponse.StatusDescription;
            foreach (string key in clientResponse.Headers.Keys)
            {
                response.Headers[key] = clientResponse.Headers[key];
            }
            if (!string.IsNullOrEmpty(clientResponse.Body))
            {
                using (StreamWriter streamWriter = new StreamWriter(response.OutputStream))
                {
                    streamWriter.Write(clientResponse.Body);
                }
            }
            response.Close();
        }

        private void dispatcherThreadProc()
        {
            while (true)
            {
                try
                {
                    HttpListenerContext context = _httpListener.GetContext();
                    try
                    {
                        Interlocked.Increment(ref _activeSessions);
                        Thread thread = new Thread(contextProccessThreadProc);
                        thread.Start(context);
                    }
                    catch
                    {
                        Interlocked.Decrement(ref _activeSessions);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void contextProccessThreadProc(object cObj)
        {
            HttpListenerContext httpListenerContext = (HttpListenerContext)cObj;
            HttpRequest httpRequest = null;
            HttpResponse httpResponse = null;
            string originalString = httpListenerContext.Request.Url.OriginalString;
            string remoteAddress = httpListenerContext.Request.RemoteEndPoint.Address.ToString();
            DateTime now = DateTime.Now;
            try
            {
                EventHandler<MessageReceivedEventArgs> messageReceived = MessageReceived;
                if (messageReceived != null)
                {
                    httpRequest = getHttpRequest(httpListenerContext);
                    MessageReceivedEventArgs messageReceivedEventArgs = new MessageReceivedEventArgs(httpRequest);
                    messageReceived(this, messageReceivedEventArgs);
                    httpResponse = (HttpResponse)messageReceivedEventArgs.Response;
                    setHttpResponse(httpListenerContext, httpResponse);
                }
                else
                {
                    httpListenerContext.Response.Abort();
                }
            }
            catch (Exception ex)
            {
                httpListenerContext.Response.StatusCode = 500;
                httpListenerContext.Response.StatusDescription = "Internal Server Error";
                try
                {
                    httpListenerContext.Response.Close();
                }
                catch
                {
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeSessions);
            }
        }

        private Encoding getEncoding(string contentType)
        {
            Encoding result = Encoding.UTF8;
            if (!string.IsNullOrEmpty(contentType) && contentType.ToLower().Contains("charset"))
            {
                string text = Regex.Replace(contentType, ".+charset=(.+?)$", "$1", RegexOptions.IgnoreCase);
                try
                {
                    result = Encoding.GetEncoding(text);
                }
                catch (Exception ex)
                {
                }
            }
            return result;
        }
    }
}
