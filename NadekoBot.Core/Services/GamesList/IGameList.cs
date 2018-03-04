using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.GamesList
{
    public interface IGameList
    {
        string Emoji { get; }
        string Title { get; }
        string Code { get; }

        Task<DataGameList> GetGamesNew();
    }

    public class DataGameList
    {
        public string GameTitle;
        public DataGameListHeader Header { get; set; }
        public DataGameListGame[] Games { get; set; }
    }

    public class DataGameListHeader
    {
        public string Description { get; set; }
        public string Image { get; set; }
        public DataGameListServerStatus[] ServerStatus { get; set; }
        public string Credit { get; set; }
    }

    public class DataGameListServerStatus
    {
        public string Name { get; set; }
        public EDataGameListServerStatus Status { get; set; }
        public DateTime? Updated { get; set; }
    }

    public class DataGameListGame
    {
        public string Name { get; set; }
        public string Image { get; set; }

        public int? CurPlayers { get; set; }
        public int? MaxPlayers { get; set; }

        public EDataGameListServerGameStatus Status { get; set; }
    }

    public enum EDataGameListServerStatus
    {
        NotSet,
        Online,
        Offline,
        NoGames,
        Unknown,
    }

    public enum EDataGameListServerGameStatus
    {
        NotSet,
        Open,
        Locked,
        Passworded,
        Unknown,
    }
}
