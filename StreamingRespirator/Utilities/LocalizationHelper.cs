using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StreamingRespirator.Properties;

namespace StreamingRespirator.Utilities
{
    internal static class LocalizationHelper
    {
        private static readonly IDictionary<PropertyInfo, string[]> Props;

        private static readonly BindingFlags BindingFlagAll = (BindingFlags)(~0);

        static LocalizationHelper()
        {
            Props = typeof(Lang).GetProperties(BindingFlagAll).Where(e => e.Name.Contains("__")).ToDictionary(e => e, e => e.Name.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static void ApplyLang(object obj)
        {
            FieldInfo finfo;
            PropertyInfo pinfo;
            foreach (var st in Props)
            {
                var langProp = st.Key;
                var langPropSplit = st.Value;

                if (langPropSplit[0] != obj.GetType().Name)
                    continue;

                object localMember = obj;

                for (var i = 1; i < langPropSplit.Length - 1; ++i)
                {
                    if (localMember == null)
                        continue;

                    finfo = localMember.GetType().GetFields(BindingFlagAll)?.FirstOrDefault(e => e.Name == langPropSplit[i]);
                    if (finfo != null)
                    {
                        localMember = finfo.GetValue(localMember);
                        continue;
                    }

                    pinfo = localMember.GetType().GetProperty(langPropSplit[i]);
                    if (pinfo != null)
                    {
                        localMember = pinfo.GetValue(localMember);
                        continue;
                    }

                    localMember = null;
                    break;
                }

                if (localMember == null)
                    continue;

                pinfo = localMember.GetType().GetProperties().FirstOrDefault(e => e.Name == langPropSplit[langPropSplit.Length - 1]);
                if (pinfo != null)
                {
                    pinfo.SetValue(localMember, langProp.GetValue(null));
                }
            }
        }
    }
}
