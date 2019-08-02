using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace ConsoleApp1
{
    public class ResponseListener : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>
    {
        private static ConcurrentDictionary<string, HttpContent> _responseContents = new ConcurrentDictionary<string, HttpContent>();

        private ResponseListener()
        {
        }

        public static void Initialize()
        {
            DiagnosticListener.AllListeners.Subscribe(new ResponseListener());
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "HttpHandlerDiagnosticListener")
            {
                value.Subscribe(this);
            }
        }

        public static IDisposable TrackRequest(string clientRequestId)
        {
            _responseContents[clientRequestId] = null;
            return new ContentRemover(() => _responseContents.Remove(clientRequestId, out HttpContent value));
        }

        public static void UnblockResponse(string clientRequestId)
        {
            _responseContents[clientRequestId].Dispose();
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Stop")
            {
                var prop = value.Value.GetType().GetProperty("Response");
                var response = prop.GetValue(value.Value) as HttpResponseMessage;

                if (response.RequestMessage.Headers.TryGetValues("x-ms-client-request-id", out IEnumerable<string> headerValues))
                {
                    string clientRequestId = headerValues.Single();

                    if (_responseContents.Keys.Contains(clientRequestId))
                    {
                        _responseContents[clientRequestId] = response.Content;
                    }
                }
            }
        }

        private class ContentRemover : IDisposable
        {
            private Action _removalAction;

            public ContentRemover(Action removalAction)
            {
                _removalAction = removalAction;
            }

            public void Dispose()
            {
                _removalAction();
            }
        }
    }
}
