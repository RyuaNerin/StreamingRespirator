using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using CefSharp;
using Newtonsoft.Json.Linq;
using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core
{
    internal class ChromeRequestHandler : BaseRequestHandler
    {
        public event Action<TwitterApiResponse> TwitterApiRersponse;

        private long m_mainOwnerId;
        private readonly Dictionary<ulong, ResponseFilter> m_filters = new Dictionary<ulong, ResponseFilter>();

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            if (request.Method == "GET" &&
                Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) &&
                uri.Host == "api.twitter.com")
            {
                var requestType = ReqeustType.None;

                switch (uri.AbsolutePath)
                {
                    case "/1.1/account/verify_credentials.json": requestType = ReqeustType.Account;       break;
                    case "/1.1/activity/about_me.json":          requestType = ReqeustType.Activity;      break;
                    case "/1.1/statuses/home_timeline.json":     requestType = ReqeustType.Statuses;      break;
                    case "/1.1/dm/user_updates.json":            requestType = ReqeustType.DirectMessage; break;
                }
                
                if (requestType != ReqeustType.None)
                {
                    var dataFilter = new ResponseFilter(requestType);

                    lock (this.m_filters)
                        this.m_filters.Add(request.Identifier, dataFilter);

                    return dataFilter;
                }
            }

            return null;
        }
        
        protected override void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            if (request.Method == "GET")
            {
                ResponseFilter filter;

                lock (this.m_filters)
                    if (this.m_filters.TryGetValue(request.Identifier, out filter))
                        this.m_filters.Remove(request.Identifier);
                    else
                        return;

                if (response.StatusCode != 200)
                {
                    filter.Dispose();
                    return;
                }
                
                ThreadPool.QueueUserWorkItem(e =>
                {
                    (var ownerIdStr, var eFilter) = ((string, ResponseFilter))e;

                    long ownerId;
                    if (string.IsNullOrWhiteSpace(ownerIdStr) || !long.TryParse(ownerIdStr, out ownerId))
                        ownerId = this.m_mainOwnerId;

                    if (eFilter.ReqeustType == ReqeustType.Account)
                    {
                        this.m_mainOwnerId = JToken.Parse(eFilter.ResponseBody)["id"].Value<long>();
                    }
                    else
                    {
                        this.TwitterApiRersponse?.Invoke(new TwitterApiResponse(ownerId, eFilter.ReqeustType, eFilter.ResponseBody));
                    }

                    eFilter.Dispose();
                }, (response.ResponseHeaders.Get("x-acted-as-user-id"), filter));
            }
        }
    }

    internal class ResponseFilter : IResponseFilter
    {
        public ResponseFilter(ReqeustType requestType)
        {
            this.ReqeustType = requestType;
        }
        
        private readonly MemoryStream m_buffer = new MemoryStream(32768);

        public ReqeustType ReqeustType { get; }

        public string ResponseBody
            => Encoding.UTF8.GetString(this.m_buffer.ToArray());

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IResponseFilter.InitFilter()
            => true;

        [EditorBrowsable(EditorBrowsableState.Never)]
        FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
        {
            if (dataIn == null)
            {
                dataInRead = dataOutWritten = 0;

                return FilterStatus.Done;
            }

            var len = Math.Min(dataIn.Length, dataOut.Length);
            var buffer = new byte[len];

            var read = dataIn.Read(buffer, 0, (int)len);

            this.m_buffer.Write(buffer, 0, read);
            dataOut      .Write(buffer, 0, read);

            dataInRead = dataOutWritten = read;

            return FilterStatus.Done;
        }

        public void Dispose()
        {
            this.m_buffer.Dispose();
        }
    }
}
