using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Data;

namespace HumbleKeys.Models
{
    public class ChoiceMonthV3 : IChoiceMonth
    {
        public class ContentChoiceOptions
        {
            public class ContentChoicesMadeContainer
            {
                public class ContentChoicesContainer
                {
                    [SerializationPropertyName("choices_made")]
                    public List<string> ChoicesMade;
                }

                [SerializationPropertyName("initial")]
                public ContentChoicesContainer contentChoicesContainer;

                public int TotalChoices;
            }

            public class ContentChoiceDataContainer
            {
                
                public Dictionary<string,ContentChoice> game_data;
            }

            public string gamekey { get; }

            public string title { get; }
            
            public ContentChoiceDataContainer contentChoiceData;
            public ContentChoicesMadeContainer contentChoicesMade;
        }

        public ContentChoiceOptions contentChoiceOptions;
        public string GameKey => contentChoiceOptions.gamekey;
        public string Title => contentChoiceOptions.title;
        public Dictionary<string,ContentChoice> ContentChoices => contentChoiceOptions.contentChoiceData.game_data;

        public List<string> ChoicesMade => contentChoiceOptions.contentChoicesMade?.contentChoicesContainer?.ChoicesMade ?? new List<string>();

        public bool ChoicesRemaining => ChoicesMade.Count == ContentChoices.Count;
    }
}