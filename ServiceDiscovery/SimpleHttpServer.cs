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

        public delegate string ResponseBytesWithResultHandler(byte[] responseBytes);
        private readonly ResponseBytesWithResultHandler _responseBytesWithResultHandler;

        private class HttpResponseState
        {
            public Stream Stream { get; set; }
            public byte[] Buffer { get; set; }
            public readonly List<byte[]> Result = new List<byte[]>();
            public HttpListenerResponse Response;
        }

        public SimpleHttpServer(ResponseBytesWithResultHandler responseBytesWithResultHandler)
        {
            _responseBytesWithResultHandler = responseBytesWithResultHandler;
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:8080/");
            _listener.Start();
            _listener.BeginGetContext(HandleRequest, _listener);

            Debug.WriteLine("Server started");
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
                var totalBytes = state.Result.SelectMany(byteArr => byteArr).ToArray();
                var codeResult = _responseBytesWithResultHandler(totalBytes);
                var response = codeResult ?? "ok";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                state.Response.ContentType = "text/html";
                state.Response.StatusCode = (int)HttpStatusCode.OK;
                state.Response.ContentLength64 = responseBytes.Length;
                state.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                state.Response.OutputStream.Close();    
            }
        }

        private void HandleRequest(IAsyncResult result)
        {
            var context = _listener.EndGetContext(result);
            _listener.BeginGetContext(HandleRequest, _listener);
            var state = new HttpResponseState { Stream = context.Request.InputStream, Buffer = new byte[ChunkSize], Response = context.Response };
            context.Request.InputStream.BeginRead(state.Buffer, 0, state.Buffer.Length, Callback, state);        
        }

        public static string SendPostRequest(string ipAddress, byte[] byteArray)
        {
            var request = WebRequest.Create(ipAddress);
            request.Method = "POST";
            request.ContentLength = byteArray.Length;
            request.ContentType = "application/x-www-form-urlencoded";
            var dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            var response = request.GetResponse();
            var stream = response.GetResponseStream();
            if (stream == null) return "";
            var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public void Dispose()
        {
            if (_listener == null) return;
            _listener.Close();
        }
    }
}