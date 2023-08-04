using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HumbleKeys.Models
{
    public class ContentChoice
    {
        [JsonProperty("title")]
        public string Title;
        public Order.TpkdDict.Tpk[] tpkds;
        public Dictionary<string, Order.TpkdDict.Tpk[]> nested_choice_tpkds;
    }
}