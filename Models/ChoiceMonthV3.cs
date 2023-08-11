using System.Collections.Generic;
using System.Linq;

namespace HumbleKeys.Models
{
    public class ChoiceMonthV3 : IChoiceMonth
    {
        public class ContentChoiceOptions
        {
            public class ContentChoiceDataContainer
            {
                
                public Dictionary<string,ContentChoice> game_data;
            }

            public string gamekey { get; }

            public string title { get; }
            
            public ContentChoiceDataContainer contentChoiceData;
        }

        public ContentChoiceOptions contentChoiceOptions;
        public string GameKey => contentChoiceOptions.gamekey;
        public string Title => contentChoiceOptions.title;
        public List<ContentChoice> ContentChoices => contentChoiceOptions.contentChoiceData.game_data.Values.ToList();
    }
}