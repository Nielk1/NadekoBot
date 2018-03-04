using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Extensions;
using Steam.Models.SteamCommunity;
using SteamWebAPI2.Utilities;
using System.Collections.Concurrent;
using SteamWebAPI2.Interfaces;
using System.Net.Http;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;

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
        private readonly GamesListService _gameList;

        private const string filePath = "C:/Data/BZ98Gamelist.json";

        public GameListBZ98Service(IBotCredentials creds, /*DbService db,*/ DiscordSocketClient client, SteamService steam, GamesListService gameList)
        {
            _creds = creds;
            //_db = db;
            _client = client;

            _steam = steam;

            _gameList = gameList;
            _gameList.AddGameListBZ98Service(this);
            _gameList.RegisterGameList(this);
        }

        public async Task<DataGameList> GetGamesNew()
        {
            Tuple<string, DateTime> gameData = await TryReadText(filePath, TimeSpan.FromSeconds(5));

            if (string.IsNullOrWhiteSpace(gameData.Item1)) return null;

            var LobbyData = JsonConvert.DeserializeObject<Dictionary<string, Lobby>>(gameData.Item1);
            LobbyData.ForEach(dr => dr.Value.SetSteamService(_steam));
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

            return data;
        }

        public async Task<BZ98ServerData> GetGames()
        {
            Tuple<string, DateTime> gameData = await TryReadText(filePath, TimeSpan.FromSeconds(5));

            if (string.IsNullOrWhiteSpace(gameData.Item1)) return null;

            var LobbyData = JsonConvert.DeserializeObject<Dictionary<string, Lobby>>(gameData.Item1);
            LobbyData.ForEach(dr => dr.Value.SetSteamService(_steam));
            return new BZ98ServerData() {
                Games = LobbyData.Where(dr => !dr.Value.isChat
                                           && (!dr.Value.isPrivate || (dr.Value.isPrivate && dr.Value.IsPassworded == true))
                                           && ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && ("0123456789".Contains(dr.Value.clientVersion[0]))) // not mobile which starts with MB or something
                                           && ((!string.IsNullOrWhiteSpace(dr.Value.clientVersion)) && (dr.Value.clientVersion != "0.0.0")) // not test game
                                       ).Select(dr => dr.Value).ToList(),
                Modified = gameData.Item2
            };
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

        public EmbedBuilder GetTopEmbed()
        {
            return new EmbedBuilder()
                .WithColor(new Color(255, 255, 255))
                .WithTitle("Battlezone 98 Redux Game List")
                //.WithUrl()
                .WithDescription($"List of games currently on BZ98 Redux matchmaking server\n`{Games.Count} Game(s)`")
                .WithThumbnailUrl("http://discord.battlezone.report/resources/logos/bz98r.png")
                .WithFooter(efb => efb.WithText($"Last fetched by Nielk1's BZ98Bridge Bot {TimeAgoUtc(Modified)}"));
        }

        private static string TimeAgoUtc(DateTime dt)
        {
            TimeSpan span = DateTime.UtcNow - dt;
            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                if (span.Days % 365 != 0)
                    years += 1;
                return String.Format("about {0} {1} ago",
                years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                if (span.Days % 31 != 0)
                    months += 1;
                return String.Format("about {0} {1} ago",
                months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("about {0} {1} ago",
                span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("about {0} {1} ago",
                span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("about {0} {1} ago",
                span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            if (span.Seconds > 5)
                return String.Format("about {0} seconds ago", span.Seconds);
            if (span.Seconds <= 5)
                return "just now";
            return string.Empty;
        }
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

        public EmbedBuilder GetEmbed(int idx, int total)
        {
            string footer = $"[{idx}/{total}] ({MapFile}) <{clientVersion}>";

            EmbedBuilder embed = new EmbedBuilder()
                .WithDescription(ToString())
                .WithFooter(efb => efb.WithText(footer));


            string prop = null;
            Task<string> propTask = Task.Run(async () =>
            {
                string propRet = await GameListBZ98Service.GetShellMap(MapFile);
                return propRet;
            });
            prop = propTask.Result;

            embed.WithThumbnailUrl(prop ?? "http://discord.battlezone.report/resources/logos/nomap.png");

            string playerCountData = string.Empty;
            bool fullPlayers = false;
            {
                playerCountData = " [" + userCount + "/" + (PlayerLimit ?? memberLimit) + "]";
                fullPlayers = (userCount >= (PlayerLimit ?? memberLimit));
            }

            if (isLocked)
            {
                embed.WithColor(new Color(0xbe, 0x19, 0x31))
                     .WithTitle("⛔ " + Format.Sanitize(Name) + playerCountData);
            }
            else if (IsPassworded == true)
            {
                embed.WithColor(new Color(0xff, 0xac, 0x33))
                     .WithTitle("🔐 " + Format.Sanitize(Name) + playerCountData);
            }
            else
            {
                float fullnessRatio = 1.0f * userCount / (PlayerLimit ?? memberLimit);

                if (fullnessRatio >= 1.0f)
                {
                    embed.WithOkColor().WithTitle("🌕 " + Format.Sanitize(Name) + playerCountData);
                }
                else if (fullnessRatio >= 0.75f)
                {
                    embed.WithOkColor().WithTitle("🌖 " + Format.Sanitize(Name) + playerCountData);
                }
                else if (fullnessRatio >= 0.50f)
                {
                    embed.WithOkColor().WithTitle("🌗 " + Format.Sanitize(Name) + playerCountData);
                }
                else if (fullnessRatio >= 0.25f)
                {
                    embed.WithOkColor().WithTitle("🌘 " + Format.Sanitize(Name) + playerCountData);
                }
                else if (fullnessRatio >= 0.0f)
                {
                    embed.WithOkColor().WithTitle("🌑 " + Format.Sanitize(Name) + playerCountData);
                }
                else
                {
                    embed.WithOkColor().WithTitle("👽 " + Format.Sanitize(Name) + playerCountData); // this should never happen
                }
            }

            if (users.Count > 0)
            {
                embed.AddField(efb => efb.WithName("Players").WithValue(GetPlayersString()).WithIsInline(false));
            }

            return embed;
        }

        public string GetPlayersString()
        {
            StringBuilder builder = new StringBuilder();

            int t = 0;
            int v = 0;

            users.ForEach(dr =>
            {
                t = Math.Max(t, dr.Value.metadata.ContainsKey("team") ? dr.Value.metadata["team"].Length : 0);
                v = Math.Max(v, dr.Value.metadata.ContainsKey("vehicle") ? dr.Value.metadata["vehicle"].Length : 0);
            });

            users.OrderBy(dr => dr.Value.metadata.ContainsKey("team") ? int.Parse(dr.Value.metadata["team"]) : 0)
                .ForEach(dr =>
                {
                    string team = dr.Value.metadata.ContainsKey("team") ? dr.Value.metadata["team"] : string.Empty;
                    string vehicle = dr.Value.metadata.ContainsKey("vehicle") ? dr.Value.metadata["vehicle"] : string.Empty;

                    string codeBlock = $"{team.PadLeft(t, '0')}{(t > 0 ? " " : string.Empty)}{vehicle.PadRight(v, ' ')}";
                    codeBlock = Format.Sanitize(codeBlock);
                    if (codeBlock.Length == 0) codeBlock = " ";
                    builder.AppendLine($"`{codeBlock}` {dr.Value.GetFormattedName()}");
                });

            //return Format.Code(builder.ToString(), "css");
            return builder.ToString();
        }

        public override string ToString()
        {
            //string name = Battlezone.GetBZ2GameProperty("name", m);
            //string version = Battlezone.GetBZ2GameProperty("version", v);
            //string mod = Battlezone.GetBZ2GameProperty("mod", d);

            List<Tuple<string, string>> lines = new List<Tuple<string, string>>();
            lines.Add(new Tuple<string, string>("Map", "[" + MapFile + "]"));
            lines.Add(new Tuple<string, string>("State", IsEnded ? "Ended" : IsLaunched ? "Launched" : "In Shell"));
            if (TimeLimit.HasValue && TimeLimit.Value > 0) lines.Add(new Tuple<string, string>("TimeLimit", TimeLimit.Value.ToString()));
            if (KillLimit.HasValue && KillLimit.Value > 0) lines.Add(new Tuple<string, string>("KillLimit", KillLimit.Value.ToString()));
            if (Lives.HasValue && Lives.Value > 0) lines.Add(new Tuple<string, string>("Lives", Lives.Value.ToString()));
            if (SyncJoin.HasValue) lines.Add(new Tuple<string, string>("SyncJoin", SyncJoin.Value ? "On" : "Off"));
            if (SatelliteEnabled.HasValue) lines.Add(new Tuple<string, string>("Satellite", SatelliteEnabled.Value ? "On" : "Off"));
            if (BarracksEnabled.HasValue) lines.Add(new Tuple<string, string>("Barracks", BarracksEnabled.Value ? "On" : "Off"));
            if (SniperEnabled.HasValue) lines.Add(new Tuple<string, string>("Sniper", SniperEnabled.Value ? "On" : "Off"));
            if (SplinterEnabled.HasValue) lines.Add(new Tuple<string, string>("Splinter", SplinterEnabled.Value ? "On" : "Off"));

            StringBuilder builder = new StringBuilder();

            int lenKey = lines.Max(dr => dr.Item1.Length);

            lines.ForEach(dr =>
            {
                builder.AppendLine($"{dr.Item1.PadRight(lenKey)} | {dr.Item2}");
            });

            string retVal = Format.Code(builder.ToString(), "css");

            ulong workshopIdNum = 0;
            if (!string.IsNullOrWhiteSpace(WorkshopID) && ulong.TryParse(WorkshopID,out workshopIdNum) && workshopIdNum > 0)
            {
                Task<string> modNameTask = Task.Run(async () =>
                {
                    string modNameRet = await _steam.GetSteamWorkshopName(WorkshopID);
                    return modNameRet;
                });
                var modName = modNameTask.Result;

                if (!string.IsNullOrWhiteSpace(modName))
                {
                    retVal = $"Mod: [{Format.Sanitize(modName)}](http://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopID})" + "\n" + retVal;
                }
                else
                {
                    retVal = "Mod: " + Format.Sanitize($"http://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopID}") + "\n" + retVal;
                }
            }

            return retVal;
        }

        private SteamService _steam;
        internal void SetSteamService(SteamService steam)
        {
            _steam = steam;
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


        public string GetFormattedName()
        {
            UserData userData = null;// GameListBZ98Service.GetUserData(id, authType);

            if (userData != null && !string.IsNullOrWhiteSpace(userData.ProfileUrl))
            {
                return $"[{Format.Sanitize(name)}]({userData.ProfileUrl})";
            }
            else
            {
                return Format.Sanitize(name);
            }
        }
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