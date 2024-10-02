using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Data;

namespace HumbleKeys.Models
{
    public class ChoiceMonthV2 : IChoiceMonth
    {
        public class ContentChoiceOptions
        {
            public class ContentChoicesMadeDataContainer
            {
                public class ContentChociesMadeData
                {
                    [SerializationPropertyName("choices_made")]
                    public List<string> ChoicesMade;
                }
                [SerializationPropertyName("initial")]
                public ContentChociesMadeData ChociesMadeData;

                [SerializationPropertyName("initial-get-all-games")]
                public ContentChociesMadeData ChociesMadeDataGetAllGames;
            }

            public class ContentChoiceDataContainer
            {
                public class ContentChoiceData
                {
                    public Dictionary<string, ContentChoice> content_choices;
                    [SerializationPropertyName("total_choices")]
                    public int TotalChoices;
                    [SerializationPropertyName("title")]
                    public string Title;
                }
                [SerializationPropertyName("initial")]
                public ContentChoiceData initial;
                [SerializationPropertyName("initial-get-all-games")]
                public ContentChoiceData initialGetAllGames;

                public string Title => Data.Title;
                public ContentChoiceData Data => initial ?? initialGetAllGames;
            }
            [SerializationPropertyName("gamekey")]
            public string gamekey;

            [SerializationPropertyName("title")]
            public string title;

            public ContentChoiceDataContainer contentChoiceData;
            public ContentChoicesMadeDataContainer contentChoicesMade;
        }

        public ContentChoiceOptions contentChoiceOptions;

        public string GameKey => contentChoiceOptions.gamekey;

        public string Title => contentChoiceOptions.title;
        public Dictionary<string,ContentChoice> ContentChoices => contentChoiceOptions.contentChoiceData.initial?.content_choices??contentChoiceOptions.contentChoiceData.initialGetAllGames.content_choices;

        public int TotalChoices => contentChoiceOptions.contentChoiceData.initial?.TotalChoices ??
                                   contentChoiceOptions.contentChoiceData.initialGetAllGames.TotalChoices;
        public List<string> ChoicesMade
        {
            get {
                if (contentChoiceOptions.contentChoicesMade == null) return new List<string>();
                if (contentChoiceOptions.contentChoicesMade.ChociesMadeDataGetAllGames != null)
                {
                    return contentChoiceOptions.contentChoicesMade.ChociesMadeDataGetAllGames.ChoicesMade;
                }

                return contentChoiceOptions.contentChoicesMade.ChociesMadeData != null ? contentChoiceOptions.contentChoicesMade.ChociesMadeData.ChoicesMade : new List<string>();
            }
        }

        public bool ChoicesRemaining => ChoicesMade.Count < TotalChoices;

    }
}