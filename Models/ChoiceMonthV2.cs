using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Data;

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
                [SerializationPropertyName("initial")]
                public ContentChoiceData initial;
                [SerializationPropertyName("initial-get-all-games")]
                public ContentChoiceData initialGetAllGames;
            }

            public string gamekey { get; }

            public string title { get; }

            public ContentChoiceDataContainer contentChoiceData;
        }

        public ContentChoiceOptions contentChoiceOptions;

        public string GameKey => contentChoiceOptions.gamekey;

        public string Title => contentChoiceOptions.title;
        public List<ContentChoice> ContentChoices => contentChoiceOptions.contentChoiceData.initial?.content_choices.Values.ToList()??contentChoiceOptions.contentChoiceData.initialGetAllGames.content_choices.Values.ToList();
    }
}