using System.Collections.Generic;

namespace HumbleKeys.Models
{
    public interface IChoiceMonth
    {
        string GameKey { get; }
        string Title { get; }
        Dictionary<string,ContentChoice> ContentChoices { get; }
        
        List<string> ChoicesMade { get; }
        
        bool ChoicesRemaining { get; }
    }
}