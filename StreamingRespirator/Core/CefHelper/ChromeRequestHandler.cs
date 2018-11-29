using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CefSharp;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.CefHelper
{
    internal enum ColumnTypes
    {
        HomeTimeline,
        Notification,
        DirectMessage,
        Other,
    }
    internal struct ColumnInfo
    {
        public ColumnTypes ColumnType;
        public string      Description;
    }

    internal class ChromeRequestHandler : BaseRequestHandler
    {
        public event Action<TwitterApiResponse> TwitterApiRersponse;
        public event Action<bool> TweetdeckAuthorized;
        public event Action<ColumnInfo[]> ColumnsUpdated;

        private struct OwnerInfo
        {
            public int Index;
            public long Id;
            public string Description;
        }
        private struct InnerColumnInfo
        {
            public long         Owner;
            public ColumnTypes  ColumnType;
        }

        private readonly Dictionary<long, OwnerInfo> Owners = new Dictionary<long, OwnerInfo>();
        private readonly List<InnerColumnInfo> Columns = new List<InnerColumnInfo>();

        private long m_mainOwnerId;
        private readonly Dictionary<ulong, ResponseFilter> m_filters = new Dictionary<ulong, ResponseFilter>();

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            if (request.Method == "GET" && request.Url.Contains("api.twitter.com"))
            {
                if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
                {
                    var requestType = ReqeustType.None;

                    switch (uri.AbsolutePath)
                    {
                        case "/1.1/account/verify_credentials.json": requestType = ReqeustType.account__verify_credentials; break;
                        case "/1.1/help/settings.json":              requestType = ReqeustType.help__settings;              break;
                        case "/1.1/activity/about_me.json":          requestType = ReqeustType.activity__about_me;          break;
                        case "/1.1/statuses/home_timeline.json":     requestType = ReqeustType.statuses__home_timeline;     break;
                        case "/1.1/dm/user_updates.json":            requestType = ReqeustType.dm__user_updates;            break;
                        case "/1.1/users/contributees.json":         requestType = ReqeustType.users__contributees;         break;
                        case "/1.1/tweetdeck/clients/blackbird/all": requestType = ReqeustType.tweetdeck__clients__blackbird__all; break;
                    }
                
                    if (requestType != ReqeustType.None)
                    {
                        var dataFilter = new ResponseFilter(requestType);

                        lock (this.m_filters)
                            this.m_filters.Add(request.Identifier, dataFilter);

                        return dataFilter;
                    }
                }
            }

            return null;
        }
        
        protected override void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            if (request.Method == "GET" && request.Url.Contains("api.twitter.com"))
            {
                ResponseFilter filter;

                lock (this.m_filters)
                    if (this.m_filters.TryGetValue(request.Identifier, out filter))
                        this.m_filters.Remove(request.Identifier);
                    else
                        return;

                if (response.StatusCode != 200)
                {
                    if (filter.ReqeustType == ReqeustType.help__settings &&
                        response.StatusCode == 401)
                        this.TweetdeckAuthorized?.Invoke(false);

                    filter.Dispose();
                    return;
                }

                //Debug.WriteLine($"{new Uri(request.Url).AbsolutePath} - {response.ResponseHeaders.Get("x-acted-as-user-id")}");
                
                ThreadPool.QueueUserWorkItem(e =>
                {
                    (var ownerIdStr, var eFilter) = ((string, ResponseFilter))e;

                    long ownerId;
                    if (string.IsNullOrWhiteSpace(ownerIdStr) || !long.TryParse(ownerIdStr, out ownerId))
                        ownerId = this.m_mainOwnerId;

                    if (eFilter.ReqeustType == ReqeustType.account__verify_credentials)
                    {
                        var jt = JToken.Parse(eFilter.ResponseBody);
                        this.m_mainOwnerId = jt["id"].Value<long>();

                        lock (this.Owners)
                        {
                            this.ClearOwner(true);

                            var id   = jt["id"].Value<long>();
                            var desc = $"@{jt["screen_name"].Value<string>()} ({jt["name"].Value<string>()})";

                            this.Owners.Add(id, new OwnerInfo { Index = -1, Id = id, Description = desc });
                        }

                        this.InvokeColumnUpdated();
                    }
                    else if (eFilter.ReqeustType == ReqeustType.users__contributees)
                    {
                        var jta = JArray.Parse(eFilter.ResponseBody);

                        lock (this.Owners)
                        {
                            this.ClearOwner(false);

                            for (int index = 0; index < jta.Count; ++index)
                            {
                                var jt = jta[index]["user"];

                                var id = jt["id"].Value<long>();
                                var desc = $"@{jt["screen_name"].Value<string>()} ({jt["name"].Value<string>()})";

                                this.Owners.Add(id, new OwnerInfo { Index = index, Id = id, Description = desc });
                            }
                        }

                        this.InvokeColumnUpdated();
                    }
                    else if (eFilter.ReqeustType == ReqeustType.tweetdeck__clients__blackbird__all)
                    {
                        var jt = JObject.Parse(eFilter.ResponseBody);
                        var feeds = jt["feeds"].Value<JObject>();

                        lock (this.Columns)
                        {
                            this.Columns.Clear();

                            foreach (var feedPair in feeds)
                            {
                                var feed = feedPair.Value;

                                if (feed["service"].Value<string>() != "twitter")
                                    continue;

                                var ctype = ColumnTypes.Other;
                                switch (feed["type"].Value<string>())
                                {
                                    case "home":            ctype = ColumnTypes.HomeTimeline;   break;
                                    case "interactions":    ctype = ColumnTypes.Notification;   break;
                                    case "direct":          ctype = ColumnTypes.DirectMessage;  break;
                                }

                                if (ctype != ColumnTypes.Other)
                                    this.Columns.Add(new InnerColumnInfo { ColumnType = ctype, Owner = feed["account"]["userid"].Value<long>() });
                            }
                        }

                        this.InvokeColumnUpdated();
                    }
                    else
                    {
                        this.TwitterApiRersponse?.Invoke(new TwitterApiResponse(ownerId, eFilter.ReqeustType, eFilter.ResponseBody));
                    }

                    eFilter.Dispose();
                }, (response.Headers.Get("x-acted-as-user-id"), filter));

                if (filter.ReqeustType == ReqeustType.account__verify_credentials)
                    this.TweetdeckAuthorized?.Invoke(true);
            }
        }

        private void ClearOwner(bool clearMaster)
        {
            // clearMaster | ==     | !=
            // true        | true   | false
            // false       | false  | true
            var keys = this.Owners.Where(e => clearMaster ^ e.Value.Index != -1).Select(e => e.Key).ToArray();

            foreach (var key in keys)
                this.Owners.Remove(key);
        }

        private void InvokeColumnUpdated()
        {
            ColumnInfo[] lst;

            lock (this.Owners)
            {
                lock (this.Columns)
                {
                    if (this.Columns.Count == 0)
                        return;

                    if (this.Columns.Select(e => e.Owner).Distinct().Any(e => !this.Owners.ContainsKey(e)))
                        return;

                    lst = this.Columns.OrderBy(e => Owners[e.Owner].Index)
                                      .ThenBy(e => e.ColumnType)
                                      .Select(e => new ColumnInfo { ColumnType = e.ColumnType, Description = Owners[e.Owner].Description })
                                      .ToArray();
                }
            }

            this.ColumnsUpdated?.Invoke(lst);
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
