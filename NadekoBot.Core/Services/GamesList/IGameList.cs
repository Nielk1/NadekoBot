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
        public string GameTitle { get; set; }
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

        public string MapFilename { get; set; }

        public List<string> TopInfo = new List<string>();

        public List<Tuple<string, string>> Properties = new List<Tuple<string, string>>();

        public string PlayersHeader { get; set; }
        public List<DataGameListPlayer> Players = new List<DataGameListPlayer>();

        public string Footer { get; set; }
    }

    public class DataGameListPlayer
    {
        public int? Index { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string PlayerClass { get; set; }
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
