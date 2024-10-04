using System.Collections.Generic;
using Playnite.SDK.Data;

namespace HumbleKeys.Models
{
    public class ContentChoice
    {
        [SerializationPropertyName("title")]
        public string Title;
        public Order.TpkdDict.Tpk[] tpkds;
        public Dictionary<string, Order.TpkdDict.Tpk[]> nested_choice_tpkds;
    }
}