using System.Collections.Generic;

namespace HumbleKeys.Models
{
    public interface IChoiceMonth
    {
        List<ContentChoice> ContentChoices { get; }
    }
}