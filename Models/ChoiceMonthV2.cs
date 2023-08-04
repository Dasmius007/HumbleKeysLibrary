using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HumbleKeys.Models
{
    public class ChoiceMonthV2 : IChoiceMonth
    {
        public class ContentChoiceOptions
        {
            public class ContentChoiceDataContainer
            {
                public class ContentChoiceData
                {
                    public Dictionary<string, ContentChoice> content_choices;
                }
                [JsonProperty("initial")]
                public ContentChoiceData initial;
                [JsonProperty("initial-get-all-games")]
                public ContentChoiceData initialGetAllGames;
            }

            public ContentChoiceDataContainer contentChoiceData;
        }

        public ContentChoiceOptions contentChoiceOptions;

        public List<ContentChoice> ContentChoices => contentChoiceOptions.contentChoiceData.initial?.content_choices.Values.ToList()??contentChoiceOptions.contentChoiceData.initialGetAllGames.content_choices.Values.ToList();
    }
}