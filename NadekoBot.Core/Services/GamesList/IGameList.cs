using System;
using System.Collections.Generic;
using System.Text;

namespace NadekoBot.Core.Services.GamesList
{
    public interface IGameList
    {
        string Emoji { get; }
        string Name { get; }
        string Code { get; }
    }
}
