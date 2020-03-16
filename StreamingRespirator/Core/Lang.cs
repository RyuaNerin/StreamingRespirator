using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core
{
    internal static class Lang
    {
        public static string Name                           { get; private set; }
        public static string NewUpdate                      { get; private set; }
        public static string StartError                     { get; private set; }

        public static string CertificateError               { get; private set; }
        public static string CertificateInstall             { get; private set; }
        public static string CertificateRemoveOld           { get; private set; }

        public static string LoginWindowWeb__AddSuccess     { get; private set; }
        public static string LoginWindowWeb__AddError       { get; private set; }

        public static string MainContext__NoAccount         { get; private set; }
        public static string MainContext__Client__Remove    { get; private set; }
        public static string MainContext__Client__Refresh   { get; private set; }
        public static string MainContext__m_scripPort__Text { get; private set; }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        
        private const BindingFlags BindingFlagAll = (BindingFlags)(~0);

        private static readonly LangDic LangMap = new LangDic();

        static Lang()
        {
            using (var mem = new MemoryStream(Properties.Resources.Lang))
            using (var reader = new StreamReader(mem))
            using (var jreader = new JsonTextReader(reader))
            {
                var jo = Program.JsonSerializer.Deserialize<JObject>(jreader);

                var currentISOLangCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var langCode = (((JObject)jo["Lang"]).Properties().FirstOrDefault(ep => ep.Values().Any(ev => ev.Value<string>() == currentISOLangCode)) as JProperty)?.Name;
                if (langCode == default)
                {
                    langCode = (((JObject)jo["Lang"]).Properties().FirstOrDefault(ep => ep.Values().Any(ev => ev.Value<string>() == "default")) as JProperty).Name;
                }

                foreach (JProperty jp in jo.Properties())
                {
                    if (jp.Name == "Lang")
                        continue;

                    GenMap(jp, LangMap, langCode);
                }
            }

            foreach (var p in typeof(Lang).GetProperties(BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.Public))
            {
                var ss = p.Name.Split(new string[] { "__" }, StringSplitOptions.RemoveEmptyEntries);

                var d = LangMap;
                for (var i = 0; i < ss.Length - 1; i++)
                {
                    d = (LangDic)d[ss[i]];
                }

                p.SetValue(null, ((LangValue)d[ss[ss.Length - 1]]).Value);
            }
        }

        private static void GenMap(JProperty jp, LangDic dic, string langCode)
        {
            // 마지막 항목인지 확인한다
            var langLevel = jp.Values().All(e => e.Values().All(ev => ev.Type == JTokenType.String));

            if (langLevel)
            {
                dic[jp.Name] = new LangValue(jp.Value[langCode].Value<string>());
            }
            else
            {
                var dicSub = new LangDic();
                dic[jp.Name] = dicSub;

                foreach (JProperty jpp in jp.Values<JObject>().Properties())
                {
                    GenMap(jpp, dicSub, langCode);
                }
            }
        }

        public static void ApplyLang(object obj)
        {
            if (LangMap.TryGetValue(obj.GetType().Name, out var v))
                ApplyLang(obj, (LangDic)v);
        }
        private static void ApplyLang(object obj, LangDic dic)
        {
            var type = obj.GetType();

            foreach (var st in dic)
            {
                foreach (var fi in type.GetFields(BindingFlagAll))
                {
                    if (!dic.TryGetValue(fi.Name, out var dv))
                        continue;

                    if (dv is LangValue lv)
                        fi.SetValue(obj, lv.Value);
                    else
                    {
                        ApplyLang(fi.GetValue(obj), (LangDic)dv);
                        return;
                    }
                }

                foreach (var pi in type.GetProperties(BindingFlagAll))
                {
                    if (!dic.TryGetValue(pi.Name, out var dv))
                        continue;

                    if (dv is LangValue lv)
                        pi.SetValue(obj, lv.Value);
                    else
                    {
                        ApplyLang(pi.GetValue(obj), (LangDic)dv);
                        return;
                    }
                }

            }
        }

        private interface ILangData
        {
        }

        private class LangDic : Dictionary<string, ILangData>, ILangData
        {

        }

        private class LangValue : ILangData
        {
            public LangValue(string v)
                => this.Value = v;
            public string Value { get; }
        }
    }
}
