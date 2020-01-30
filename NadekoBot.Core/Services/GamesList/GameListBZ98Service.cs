using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Extensions;
using Steam.Models.SteamCommunity;
using System.Net.Http;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;
using Microsoft.Extensions.Logging;
using NLog;

namespace NadekoBot.Services.GamesList
{
    public class GameListBZ98Service : INService, IGameList
    {
        public string Emoji => @"<:game_icon_battlezone98redux:342134901975547916>";
        public string Title => "Battlezone 98 Redux";
        public string Code => "bzr";

        private readonly IBotCredentials _creds;
        //private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        private readonly SteamService _steam;

        private const string filePath = "C:/Data/BZ98Gamelist.json";

        private readonly Logger _log;

        public GameListBZ98Service(IBotCredentials creds, /*DbService db,*/ DiscordSocketClient client, SteamService steam)
        {
            _log = LogManager.GetCurrentClassLogger();
            _log.Info("GameListBZ98Service");
            _creds = creds;
            //_db = db;
            _client = client;

            _steam = steam;
        }

        public async Task<DataGameList> GetGamesNew()
        {
            Tuple<string, DateTime> gameData = await TryReadText(filePath, TimeSpan.FromSeconds(5));

            if (gameData == null || string.IsNullOrWhiteSpace(gameData.Item1)) return null;

            var LobbyData = JsonConvert.DeserializeObject<Dictionary<string, Lobby>>(gameData.Item1);
            //return new BZ98ServerData()
            //{
            //    Games = LobbyData.Where(dr => !dr.Value.isChat
            //                               && (!dr.Value.isPrivate || (dr.Value.isPrivate && dr.Value.IsPassworded == true))
            //                               && ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && ("0123456789".Contains(dr.Value.clientVersion[0]))) // not mobile which starts with MB or something
            //                               && ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && (dr.Value.clientVersion != "0.0.0")) // not test game
            //                           ).Select(dr => dr.Value).ToList(),
            //    Modified = gameData.Item2
            //};

            DataGameList data = new DataGameList()
            {
                GameTitle = this.Title,
                Header = new DataGameListHeader()
                {
                    Description = "List of games currently on BZ98 Redux matchmaking server",
                    Image = "http://discord.battlezone.report/resources/logos/bz98r.png",
                    Credit = $"Fetched by Nielk1's BZ98Bridge Bot"
                }
            };

            data.Header.ServerStatus = new DataGameListServerStatus[] {
                new DataGameListServerStatus(){ Name = "Rebellion", Status = EDataGameListServerStatus.NotSet, Updated = gameData.Item2 }
            };

            data.Games = (await Task.WhenAll(
                LobbyData
                .Where(dr => !dr.Value.isChat
                          && (!dr.Value.isPrivate || (dr.Value.isPrivate && dr.Value.IsPassworded == true))
                          //&& ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && ("0123456789".Contains(dr.Value.clientVersion[0]))) // not mobile which starts with MB or something
                          && ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && (dr.Value.clientVersion != "0.0.0")) // not test game
                      ).Select(dr => dr.Value)
                .Select(async raw =>
                {
                    DataGameListGame game = new DataGameListGame();

                    game.Name = raw.Name;
                    game.Image = await GetShellMap(raw.MapFile) ?? "http://discord.battlezone.report/resources/logos/nomap.png";

                    game.CurPlayers = raw.userCount;
                    game.MaxPlayers = raw.PlayerLimit;

                    if (raw.isLocked)
                    {
                        game.Status = EDataGameListServerGameStatus.Locked;
                    }
                    else if (raw.IsPassworded == true)
                    {
                        game.Status = EDataGameListServerGameStatus.Passworded;
                    }
                    else
                    {
                        game.Status = EDataGameListServerGameStatus.Open;
                    }

                    game.MapFilename = raw.MapFile;
                    game.Footer = raw.MapFile + @".bzn";
                    if (!string.IsNullOrWhiteSpace(raw.clientVersion)) game.Footer += $" <{raw.clientVersion}>";

                    {
                        await Task.WhenAll(raw.users
                           .Select(async dr =>
                           {
                               string team = dr.Value.metadata.ContainsKey("team") ? dr.Value.metadata["team"] : string.Empty;
                               string vehicle = dr.Value.metadata.ContainsKey("vehicle") ? dr.Value.metadata["vehicle"] : string.Empty;

                               int teamInt = 0;
                               UserData userData = await GetUserData(dr.Value.id, dr.Value.authType);

                               game.Players.Add(new DataGameListPlayer()
                               {
                                   Index = int.TryParse(team, out teamInt) ? (int?)teamInt : null,
                                   Name = dr.Value.name,
                                   PlayerClass = vehicle,
                                   Url = userData?.ProfileUrl
                               });
                           }));

                        game.PlayersHeader = "Players";
                    }

                    {
                        ulong workshopIdNum = 0;
                        if (!string.IsNullOrWhiteSpace(raw.WorkshopID) && ulong.TryParse(raw.WorkshopID, out workshopIdNum) && workshopIdNum > 0)
                        {
                            Task<string> modNameTask = Task.Run(async () =>
                            {
                                string modNameRet = await _steam.GetSteamWorkshopName(raw.WorkshopID);
                                return modNameRet;
                            });
                            var modName = modNameTask?.Result?? raw.WorkshopID;

                            if (!string.IsNullOrWhiteSpace(modName))
                            {
                                game.TopInfo.Add($"Mod: [{Format.Sanitize(modName)}](http://steamcommunity.com/sharedfiles/filedetails/?id={raw.WorkshopID})");
                            }
                            else
                            {
                                game.TopInfo.Add("Mod: " + Format.Sanitize($"http://steamcommunity.com/sharedfiles/filedetails/?id={raw.WorkshopID}"));
                            }
                        }
                    }

                    game.Properties.Add(new Tuple<string, string>("Map", "[" + raw.MapFile + "]"));
                    game.Properties.Add(new Tuple<string, string>("State", raw.IsEnded ? "Ended" : raw.IsLaunched ? "Launched" : "In Shell"));
                    if (raw.TimeLimit.HasValue && raw.TimeLimit.Value > 0) game.Properties.Add(new Tuple<string, string>("TimeLimit", raw.TimeLimit.Value.ToString()));
                    if (raw.KillLimit.HasValue && raw.KillLimit.Value > 0) game.Properties.Add(new Tuple<string, string>("KillLimit", raw.KillLimit.Value.ToString()));
                    if (raw.Lives.HasValue && raw.Lives.Value > 0) game.Properties.Add(new Tuple<string, string>("Lives", raw.Lives.Value.ToString()));
                    if (raw.SyncJoin.HasValue) game.Properties.Add(new Tuple<string, string>("SyncJoin", raw.SyncJoin.Value ? "On" : "Off"));
                    if (raw.SatelliteEnabled.HasValue) game.Properties.Add(new Tuple<string, string>("Satellite", raw.SatelliteEnabled.Value ? "On" : "Off"));
                    if (raw.BarracksEnabled.HasValue) game.Properties.Add(new Tuple<string, string>("Barracks", raw.BarracksEnabled.Value ? "On" : "Off"));
                    if (raw.SniperEnabled.HasValue) game.Properties.Add(new Tuple<string, string>("Sniper", raw.SniperEnabled.Value ? "On" : "Off"));
                    if (raw.SplinterEnabled.HasValue) game.Properties.Add(new Tuple<string, string>("Splinter", raw.SplinterEnabled.Value ? "On" : "Off"));

                    return game;
                })
            )).ToArray();

            return data;
        }

        public static async Task<Tuple<string,DateTime>> TryReadText(string filepath, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(filepath)) return null;
            if (!File.Exists(filepath)) return null;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var info = new FileInfo(filepath);
                    DateTime FetchTime = info.LastWriteTimeUtc;
                    using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        return new Tuple<string, DateTime>(await sr.ReadToEndAsync(), FetchTime);
                    }
                }
                catch (IOException) { }
                Thread.Sleep(100);
            }
            return null;
        }

        /// <summary>
        /// Get extended user data from outside APIs
        /// </summary>
        /// <param name="id">ID of user in BZ98</param>
        /// <param name="type">Auth type of user in BZ98</param>
        /// <returns></returns>
        public async Task<UserData> GetUserData(string id, string type)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (string.IsNullOrWhiteSpace(type)) return null;

            if (type == "steam" && id[0] == 'S')
            {
                ulong playerID = 0;
                if (ulong.TryParse(id.Substring(1), out playerID))
                {
                    PlayerSummaryModel newPlayerData = await _steam.GetSteamUserData(playerID);

                    if (newPlayerData != null)
                    {
                        return new UserData()
                        {
                            AvatarUrl = newPlayerData.AvatarFullUrl,
                            ProfileUrl = newPlayerData.ProfileUrl
                        };
                    }
                }
            }

            return null;
        }


        public static async Task<string> GetShellMap(string mapFile)
        {
            if (string.IsNullOrWhiteSpace(mapFile)) return null;
            mapFile = Path.GetFileNameWithoutExtension(mapFile);
            if (string.IsNullOrWhiteSpace(mapFile)) return null;
            mapFile = mapFile.ToLowerInvariant();

            try
            {
                using (var http = new HttpClient())
                {
                    {
                        var url = $"http://discord.battlezone.report/resources/meta/bz98/shellmaps/{mapFile}.jpg";
                        var reply = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        if (reply.StatusCode == System.Net.HttpStatusCode.OK) return url;
                    }
                    {
                        var url = $"http://discord.battlezone.report/resources/meta/bz98/shellmaps/{mapFile}.jpeg";
                        var reply = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        if (reply.StatusCode == System.Net.HttpStatusCode.OK) return url;
                    }
                    {
                        var url = $"http://discord.battlezone.report/resources/meta/bz98/shellmaps/{mapFile}.png";
                        var reply = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        if (reply.StatusCode == System.Net.HttpStatusCode.OK) return url;
                    }
                }
            }
            catch (Exception ex)
            {
                //_log.Warn(ex, "Steam Workshop scan failed");
            }
            return null;
        }
    }

    public class BZ98ServerData
    {
        public List<Lobby> Games { get; set; }
        public DateTime Modified { get; set; }
    }

    public class Lobby
    {
        public int id { get; set; }
        public string owner { get; set; }
        public bool isLocked { get; set; }
        public bool isChat { get; set; }
        public bool isPrivate { get; set; }
        public string password { get; set; } // is this a real property?
        public int memberLimit { get; set; }
        public Dictionary<string, User> users { get; set; }
        public int userCount { get; set; }
        public Dictionary<string, string> metadata { get; set; }
        /////////////////////////////////////////////
        public string clientVersion { get; set; }


        [JsonIgnore]
        public bool IsLaunched
        {
            get
            {
                return metadata != null && metadata.ContainsKey("launched") && metadata["launched"] == "1";
            }
        }

        [JsonIgnore]
        public bool IsEnded
        {
            get
            {
                return metadata != null && metadata.ContainsKey("gameended") && metadata["gameended"] == "1";
            }
        }

        public enum ELobbyType
        {
            Chat,
            Game,
            Unknown
        }

        public enum ELobbyVisibility
        {
            Public,
            Private,
            Unknown
        }

        [JsonIgnore]
        public ELobbyType LobbyType
        {
            get
            {
                string name = ExtractName(1);
                if (name == null) return ELobbyType.Unknown;
                if (name == "chat") return ELobbyType.Chat;
                if (name == "game") return ELobbyType.Game;
                return ELobbyType.Unknown;
            }
        }

        [JsonIgnore]
        public ELobbyVisibility LobbyVisibility
        {
            get
            {
                string name = ExtractName(2);
                if (name == null) return ELobbyVisibility.Unknown;
                if (name == "pub") return ELobbyVisibility.Public;
                if (name == "priv") return ELobbyVisibility.Private;
                return ELobbyVisibility.Unknown;
            }
        }

        [JsonIgnore]
        public bool? IsPassworded
        {
            get
            {
                string passworded = ExtractName(3);
                if (passworded == null) return null;
                return passworded.Length > 0; // contains something, valid values are "" and "*"
            }
        }

        [JsonIgnore]
        public string Name { get { return ExtractName(4); } }

        [JsonIgnore]
        public int? MetaDataVersion
        {
            get
            {
                string GameSettingVersion = ExtractGameSettings(0);
                if (string.IsNullOrWhiteSpace(GameSettingVersion)) return null;

                int versionNum = 0;
                if (int.TryParse(GameSettingVersion, out versionNum))
                    return versionNum;

                return null;
            }
        }

        [JsonIgnore]
        public string MapFile { get { return ExtractGameSettings(1); } }

        [JsonIgnore]
        public string InternalID { get { return ExtractGameSettings(2); } }

        [JsonIgnore]
        public string WorkshopID { get { return ExtractGameSettings(3); } }

        [JsonIgnore]
        public bool? SyncJoin
        {
            get
            {
                string val = ExtractGameSettings(4);
                if (string.IsNullOrWhiteSpace(val)) return null;
                if (val == "0") return false;
                if (val == "1") return true;
                return null;
            }
        }

        [JsonIgnore]
        public bool? SatelliteEnabled
        {
            get
            {
                string val = ExtractGameSettings(5);
                if (string.IsNullOrWhiteSpace(val)) return null;
                if (val == "0") return false;
                if (val == "1") return true;
                return null;
            }
        }

        [JsonIgnore]
        public bool? BarracksEnabled
        {
            get
            {
                string val = ExtractGameSettings(6);
                if (string.IsNullOrWhiteSpace(val)) return null;
                if (val == "0") return false;
                if (val == "1") return true;
                return null;
            }
        }

        [JsonIgnore]
        public int? TimeLimit
        {
            get
            {
                string val = ExtractGameSettings(7);
                if (string.IsNullOrWhiteSpace(val)) return null;
                int tmpVal = 0;
                if (int.TryParse(val, out tmpVal)) return tmpVal;
                return null;
            }
        }

        [JsonIgnore]
        public int? Lives
        {
            get
            {
                string val = ExtractGameSettings(8);
                if (string.IsNullOrWhiteSpace(val)) return null;
                int tmpVal = 0;
                if (int.TryParse(val, out tmpVal)) return tmpVal;
                return null;
            }
        }

        [JsonIgnore]
        public int? PlayerLimit
        {
            get
            {
                string val = ExtractGameSettings(9);
                if (string.IsNullOrWhiteSpace(val)) return null;
                int tmpVal = 0;
                if (int.TryParse(val, out tmpVal)) return tmpVal;
                return null;
            }
        }

        [JsonIgnore]
        public bool? SniperEnabled
        {
            get
            {
                string val = ExtractGameSettings(10);
                if (string.IsNullOrWhiteSpace(val)) return null;
                if (val == "0") return false;
                if (val == "1") return true;
                return null;
            }
        }

        [JsonIgnore]
        public int? KillLimit
        {
            get
            {
                string val = ExtractGameSettings(11);
                if (string.IsNullOrWhiteSpace(val)) return null;
                int tmpVal = 0;
                if (int.TryParse(val, out tmpVal)) return tmpVal;
                return null;
            }
        }

        [JsonIgnore]
        public bool? SplinterEnabled
        {
            get
            {
                string val = ExtractGameSettings(12);
                if (string.IsNullOrWhiteSpace(val)) return null;
                if (val == "0") return false;
                if (val == "1") return true;
                return null;
            }
        }

        private string ExtractName(int index)
        {
            if (!metadata.ContainsKey("name"))
                return null;

            string metaValue = metadata["name"];
            string[] metaValues = metaValue.Split(new[] { "~" }, 5, StringSplitOptions.None);

            if (metaValues.Length > index)
                return metaValues[index];

            return null;
        }

        private string ExtractGameSettings(int index)
        {
            if (!metadata.ContainsKey("gameSettings"))
                return null;

            string metaValue = metadata["gameSettings"];
            string[] metaValues = metaValue.Split(new[] { "*" }, StringSplitOptions.None);

            if (metaValues.Length > index)
                return metaValues[index];

            return null;
        }
    }

    public class User
    {
        public string id { get; set; }
        public string name { get; set; }
        public string authType { get; set; }
        public string clientVersion { get; set; }
        public string ipAddress { get; set; }
        public int lobby { get; set; }
        public bool isAdmin { get; set; }
        public bool isInLounge { get; set; }
        public Dictionary<string, string> metadata { get; set; }
        public string wanAddress;
        public string[] lanAddresses { get; set; }
        /////////////////////////////////////////////
        public bool isAuth { get; set; }
    }

    /// <summary>
    /// Collection of extended user data from other APIs such as Steam
    /// </summary>
    public class UserData
    {
        public string AvatarUrl { get; set; }
        public string ProfileUrl { get; set; }
    }
}