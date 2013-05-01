using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ServiceDiscovery
{
    public sealed class SimpleHttpServer: IDisposable
    {
        private readonly HttpListener _listener;
        private const int ChunkSize = 1024;

        private readonly Dictionary<string, Func<byte[], string>> _requestHandlers;

        private class HttpResponseState
        {
            public Stream Stream { get; set; }
            public byte[] Buffer { get; set; }            
            public readonly List<byte[]> Result = new List<byte[]>();
            public HttpListenerRequest Request;
            public HttpListenerResponse Response;
        }

        public SimpleHttpServer(int port, Dictionary<string, Func<byte[], string>> requestHandlers)
        {
            _requestHandlers = requestHandlers;
            _listener = new HttpListener();
            _listener.Prefixes.Add(String.Format("http://*:{0}/", port));
            _listener.Start();
            _listener.BeginGetContext(HandleRequest, _listener);

            Debug.WriteLine("ProtoPad HTTP Server started");
        }

        private void Callback(IAsyncResult ar)
        {            
            var state = (HttpResponseState)ar.AsyncState;

            var bytesRead = state.Stream.EndRead(ar);
            if (bytesRead > 0)
            {
                var buffer = new byte[bytesRead];
                Buffer.BlockCopy(state.Buffer, 0, buffer, 0, bytesRead);
                state.Result.Add(buffer);
                state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, Callback, state);
            }
            else
            {
                state.Stream.Dispose();
                var responseData = state.Result.SelectMany(byteArr => byteArr).ToArray();

                foreach (var requestHandler in _requestHandlers)
                {
                    if (!state.Request.Url.PathAndQuery.Contains(requestHandler.Key)) continue;
                    var responseValue = requestHandler.Value(responseData);
                    var responseBytes = Encoding.UTF8.GetBytes(responseValue);
                    state.Response.ContentType = "text/plain";
                    state.Response.StatusCode = (int)HttpStatusCode.OK;
                    state.Response.ContentLength64 = responseBytes.Length;
                    state.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    state.Response.OutputStream.Close();
                    return;
                }
            }
        }

        private void HandleRequest(IAsyncResult result)
        {
            var context = _listener.EndGetContext(result);
            _listener.BeginGetContext(HandleRequest, _listener);            
            var state = new HttpResponseState { Stream = context.Request.InputStream, Buffer = new byte[ChunkSize], Response = context.Response, Request = context.Request };
            context.Request.InputStream.BeginRead(state.Buffer, 0, state.Buffer.Length, Callback, state);        
        }

        public static string SendCustomCommand(string ipAddress, string command)
        {
            var wc = new WebClient();
            try
            {
                return wc.DownloadString(ipAddress + "/" + command);
            }
            catch
            {
                return null;
            }
        }

        public static string SendPostRequest(string ipAddress, byte[] byteArray, string command)
        {
            var request = WebRequest.Create(ipAddress + "/" + command);
            request.Method = "POST";
            request.ContentLength = byteArray.Length;
            request.ContentType = "application/x-www-form-urlencoded";
            var dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            try
            {
                var response = request.GetResponse();
                var stream = response.GetResponseStream();
                if (stream == null) return "";
                var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_listener == null) return;
            _listener.Close();
        }
    }
}