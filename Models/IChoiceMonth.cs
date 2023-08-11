using System.Collections.Generic;

namespace HumbleKeys.Models
{
    public interface IChoiceMonth
    {
        string GameKey { get; }
        string Title { get; }
        List<ContentChoice> ContentChoices { get; }
    }
}