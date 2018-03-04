using Discord;
using Discord.API;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;
using NadekoBot.Extensions;
using NadekoBot.Services;
using Newtonsoft.Json;
using NLog;
using Steam.Models.SteamCommunity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Services.GamesList
{
    public class GameListBZCCService : INService, IGameList
    {
        public string Emoji => null;
        public string Title => "Battlezone Combat Commander";
        public string Code => "bzcc";

        //private readonly IBotCredentials _creds;
        //private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        //public ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>> BZ2GameProperties { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();

        private readonly SteamService _steam;
        private readonly GamesListService _gameList;
        
        /// <summary>
        /// Workshop name cache
        /// </summary>
        static ConcurrentDictionary<string, Tuple<DateTime, string>> steamWorkshopNameCache = new ConcurrentDictionary<string, Tuple<DateTime, string>>();

        private const string queryUrl = "http://raknetsrv2.iondriver.com/testServer?__gameId=BZCC&__excludeCols=__rowId,__city,__cityLon,__cityLat,__timeoutSec,__geoIP,__gameId,__addr&__pluginShowSource=true&__pluginQueryServers=true&__pluginShowStatus=true";

        private Logger _log;

        public GameListBZCCService(/*IBotCredentials creds, DbService db,*/ DiscordSocketClient client, SteamService steam, GamesListService gameList)
        {
            //_creds = creds;
            //_db = db;
            _client = client;

            _log = LogManager.GetCurrentClassLogger();
            /*var sw = Stopwatch.StartNew();
            using (var uow = _db.UnitOfWork)
            {
                var items = uow.BZCCGameProperties.GetAll();
                BZCCGameProperties = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZCCGameProperty>>();
                items.ForEach(BZCCProperty =>
                {
                    lock (BZCCGameProperties)
                    {
                        if (!BZCCGameProperties.ContainsKey(BZCCProperty.TermType))
                            BZCCGameProperties[BZCCProperty.TermType] = new ConcurrentDictionary<string, BZCCGameProperty>();

                        BZCCGameProperties[BZCCProperty.TermType][BZCCProperty.Term] = BZCCProperty;
                    }
                });
            }
            sw.Stop();
            _log.Debug($"Loaded in {sw.Elapsed.TotalSeconds:F2}s");*/

            _steam = steam;

            _gameList = gameList;
            _gameList.AddGameListBZCCService(this);
            _gameList.RegisterGameList(this);
        }

        public async Task<DataGameList> GetGamesNew()
        {
            using (var http = new HttpClient())
            {
                var res = await http.GetStringAsync(queryUrl).ConfigureAwait(false);
                var gamelist = JsonConvert.DeserializeObject<BZCCRaknetData>(res);
                //gamelist.SetBz2Service(this);
                //return gamelist;

                DataGameList data = new DataGameList()
                {
                    GameTitle = this.Title,
                    Header = new DataGameListHeader()
                    {
                        Description = "List of games currently on Battlezone CC matchmaking servers",
                        Image = "http://discord.battlezone.report/resources/logos/bzcc.png",
                        Credit = "Brought to you by Nielk1's Raknet Bot"
                    }
                };

                {
                    bool isOnIonDriver = gamelist.GET.Any(game => game.IsOnIonDriver());
                    DataGameListServerStatus IonDriverStatus = new DataGameListServerStatus() { Name = "IonDriver", Updated = null };
                    //if (isIonDriver)
                    //{
                    //    IonDriverStatus.Status = EDataGameListServerStatus.Online;
                    //}
                    //else if (isIonDriver)
                    //{
                    //    IonDriverStatus.Status = EDataGameListServerStatus.NoMarker;
                    //}
                    //else
                    //{
                    //    IonDriverStatus.Status = EDataGameListServerStatus.Unknown;
                    //}
                    IonDriverStatus.Status = EDataGameListServerStatus.NotSet;

                    bool isOnRebellion = gamelist.GET.Any(game => game.IsOnRebellion());
                    bool haveRebellionStatus = gamelist.proxyStatus.ContainsKey("Rebellion");
                    bool isRebellionUp = haveRebellionStatus && gamelist.proxyStatus["Rebellion"].success == true;
                    string statusRebellion = haveRebellionStatus ? gamelist.proxyStatus["Rebellion"].status : null;
                    DateTime? dateRebellion = haveRebellionStatus ? gamelist.proxyStatus["Rebellion"].updated : null;
                    DataGameListServerStatus RebellionStatus = new DataGameListServerStatus() { Name = "Rebellion (Primary)", Updated = dateRebellion };
                    if (isRebellionUp)
                    {
                        RebellionStatus.Status = EDataGameListServerStatus.Online;
                    }
                    else if (!isRebellionUp)
                    {
                        RebellionStatus.Status = EDataGameListServerStatus.Offline;
                    }
                    else
                    {
                        RebellionStatus.Status = EDataGameListServerStatus.Unknown;
                    }

                    data.Header.ServerStatus = new DataGameListServerStatus[] { IonDriverStatus, RebellionStatus };
                }

                data.Games = (await Task.WhenAll(
                    gamelist.GET
                    .Select(async raw =>
                    {
                        DataGameListGame game = new DataGameListGame();

                        game.Name = raw.Name;
                        game.Image = "http://discord.battlezone.report/resources/logos/nomap.png";

                        game.CurPlayers = raw.CurPlayers;
                        game.MaxPlayers = raw.MaxPlayers;

                        if (raw.Locked)
                        {
                            game.Status = EDataGameListServerGameStatus.Locked;
                        }
                        else if (raw.Passworded)
                        {
                            game.Status = EDataGameListServerGameStatus.Passworded;
                        }
                        else if (!raw.MaxPlayers.HasValue)
                        {
                            game.Status = EDataGameListServerGameStatus.Unknown;
                        }
                        else
                        {
                            game.Status = EDataGameListServerGameStatus.Open;
                        }

                        game.MapFilename = raw.MapFile;
                        game.Footer = raw.MapFile + @".bzn";

                        if (!string.IsNullOrWhiteSpace(raw.MOTD))
                        {
                            game.TopInfo.Add(Format.Sanitize(raw.MOTD));
                        }

                        foreach (string mod in raw.Mods)
                        {
                            ulong workshopIdNum = 0;
                            if (!string.IsNullOrWhiteSpace(mod) && ulong.TryParse(mod, out workshopIdNum) && workshopIdNum > 0)
                            {
                                Task<string> modNameTask = Task.Run(async () =>
                                {
                                    string modNameRet = await _steam.GetSteamWorkshopName(mod);
                                    return modNameRet;
                                });
                                var modName = modNameTask.Result;

                                if (!string.IsNullOrWhiteSpace(modName))
                                {
                                    game.TopInfo.Add($"Mod: [{Format.Sanitize(modName)}](http://steamcommunity.com/sharedfiles/filedetails/?id={mod})");
                                }
                                else
                                {
                                    game.TopInfo.Add("Mod: " + Format.Sanitize($"http://steamcommunity.com/sharedfiles/filedetails/?id={mod}"));
                                }
                            }
                        }

                        {
                            int k = 1;
                            int d = 1;
                            int s = 1;
                            bool scoreNeedsSign = false;

                            raw.pl.ForEach(dr =>
                            {
                                k = Math.Max(k, (dr.Kills.HasValue ? dr.Kills.Value : 0).ToString().Length);
                                d = Math.Max(d, (dr.Deaths.HasValue ? dr.Deaths.Value : 0).ToString().Length);
                                s = Math.Max(s, Math.Abs(dr.Score.HasValue ? dr.Score.Value : 0).ToString().Length);
                                scoreNeedsSign = scoreNeedsSign || (dr.Score < 0);
                            });

                            raw.pl.ForEach(async dr =>
                            {
                                string scoresign = "0";
                                if ((dr.Score.HasValue ? dr.Score.Value : 0) > 0) scoresign = "+";
                                if ((dr.Score.HasValue ? dr.Score.Value : 0) < 0) scoresign = "-";
                                if (!scoreNeedsSign) scoresign = string.Empty;

                                UserData userData = await GetUserData(dr.PlayerID);

                                game.Players.Add(new DataGameListPlayer()
                                {
                                    Index = dr.Team,
                                    Name = dr.Name,
                                    PlayerClass = $"{(dr.Kills.HasValue ? dr.Kills.Value : 0).ToString().PadLeft(k, '0')}/{(dr.Deaths.HasValue ? dr.Deaths.Value : 0).ToString().PadLeft(d, '0')}/{scoresign}{Math.Abs((dr.Score.HasValue ? dr.Score.Value : 0)).ToString().PadLeft(s, '0')}",
                                    Url = userData?.ProfileUrl
                                });
                            });

                            game.PlayersHeader = "[T] (K/D/S) Players";
                        }

                        {
                            string name = await GetBZCCGameProperty("name", raw.MapFile);

                            if (string.IsNullOrWhiteSpace(name))
                            {
                                game.Properties.Add(new Tuple<string, string>("Map", $"[{raw.MapFile}]"));
                            }
                            else
                            {
                                game.Properties.Add(new Tuple<string, string>("Map", $"{name}"));
                            }

                            if (!string.IsNullOrWhiteSpace(raw.v))
                            {
                                game.Properties.Add(new Tuple<string, string>("Version", $"{raw.v}"));
                            }

                            /*if (!string.IsNullOrWhiteSpace(raw.d))
                            {
                                game.Properties.Add(new Tuple<string, string>("Mod", $"[{raw.d}]"));
                            }*/

                            if (!string.IsNullOrWhiteSpace(raw.t))
                                switch (raw.t)
                                {
                                    case "0":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"NONE")); /// Works with anyone
                                        break;
                                    case "1":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"FULL CONE")); /// Accepts any datagrams to a port that has been previously used. Will accept the first datagram from the remote peer.
                                        break;
                                    case "2":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"ADDRESS RESTRICTED")); /// Accepts datagrams to a port as long as the datagram source IP address is a system we have already sent to. Will accept the first datagram if both systems send simultaneously. Otherwise, will accept the first datagram after we have sent one datagram.
                                        break;
                                    case "3":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"PORT RESTRICTED")); /// Same as address-restricted cone NAT, but we had to send to both the correct remote IP address and correct remote port. The same source address and port to a different destination uses the same mapping.
                                        break;
                                    case "4":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"SYMMETRIC")); /// A different port is chosen for every remote destination. The same source address and port to a different destination uses a different mapping. Since the port will be different, the first external punchthrough attempt will fail. For this to work it requires port-prediction (MAX_PREDICTIVE_PORT_RANGE>1) and that the router chooses ports sequentially.
                                        break;
                                    case "5":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"UNKNOWN")); /// Hasn't been determined. NATTypeDetectionClient does not use this, but other plugins might
                                        break;
                                    case "6":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"DETECTION IN PROGRESS")); /// In progress. NATTypeDetectionClient does not use this, but other plugins might
                                        break;
                                    case "7":
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"SUPPORTS UPNP")); /// Didn't bother figuring it out, as we support UPNP, so it is equivalent to NAT_TYPE_NONE. NATTypeDetectionClient does not use this, but other plugins might
                                        break;
                                    default:
                                        game.Properties.Add(new Tuple<string, string>("NAT", $"[" + raw.t + "]"));
                                        break;
                                }

                            switch (raw.proxySource)
                            {
                                case "Rebellion":
                                    game.Properties.Add(new Tuple<string, string>("List", $"Rebellion"));
                                    break;
                                default:
                                    game.Properties.Add(new Tuple<string, string>("List", $"IonDriver"));
                                    break;
                            }

                            {
                                switch (raw.GameType)
                                {
                                    case 0:
                                        game.Properties.Add(new Tuple<string, string>("Type", $"All"));
                                        break;
                                    case 1:
                                        switch (raw.GameSubType)
                                        {
                                            case 0:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"DM"));
                                                break;
                                            case 1:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"KOTH"));
                                                break;
                                            case 2:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"CTF"));
                                                break;
                                            case 3:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"Loot"));
                                                break;
                                            case 4:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"DM [RESERVED]"));
                                                break;
                                            case 5:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"Race"));
                                                break;
                                            case 6:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"Race (Vehicle Only)"));
                                                break;
                                            case 7:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"DM (Vehicle Only)"));
                                                break;
                                            default:
                                                game.Properties.Add(new Tuple<string, string>("Type", $"DM [UNKNOWN]"));
                                                break;
                                        }
                                        break;
                                    case 2:
                                        /*if (raw.pong.TeamsOn && raw.pong.OnlyOneTeam)
                                        {
                                            game.Properties.Add(new Tuple<string, string>("Type", $"MPI"));
                                        }
                                        else
                                        {
                                            game.Properties.Add(new Tuple<string, string>("Type", $"Strat"));
                                        }*/
                                        game.Properties.Add(new Tuple<string, string>("Type", $"Strat/MPI"));
                                        break;
                                    case 3:
                                        game.Properties.Add(new Tuple<string, string>("Type", $"MPI [Invalid]"));
                                        break;
                                }

                                if (raw.GameTimeMinutes.HasValue)
                                {
                                    switch (raw.ServerInfoMode)
                                    {
                                        case 1:
                                            game.Properties.Add(new Tuple<string, string>("Time", $"Not playing or in shell for {raw.GameTimeMinutes.Value} minutes"));
                                            break;
                                        case 3:
                                            game.Properties.Add(new Tuple<string, string>("Time", $"Playing for {raw.GameTimeMinutes.Value} minutes"));
                                            break;
                                    }
                                }

                                if (raw.TPS.HasValue && raw.TPS > 0)
                                    game.Properties.Add(new Tuple<string, string>("TPS", $"{raw.TPS}"));

                                if (raw.MaxPing.HasValue && raw.MaxPing > 0)
                                    game.Properties.Add(new Tuple<string, string>("MaxPing", $"{raw.MaxPing}"));

                                if (raw.TimeLimit.HasValue && raw.TimeLimit > 0)
                                    game.Properties.Add(new Tuple<string, string>("TimeLim", $"{raw.TimeLimit}"));

                                if (raw.KillLimit.HasValue && raw.KillLimit > 0)
                                    game.Properties.Add(new Tuple<string, string>("KillLim", $"{raw.KillLimit}"));
                            }
                        }

                        return game;
                    }))).ToArray();

                return data;
            }
        }

        public async Task<UserData> GetUserData(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            if (id[0] == 'S')
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

        public async Task<string> GetBZCCGameProperty(string termType, string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return null;

            //if (new string[] { "version", "mod" }.Contains(termType))
            //{
            //    using (var http = new HttpClient())
            //    {
            //        var url = $"http://discord.battlezone.report/resources/meta/BZCC/{termType}/{term}";
            //        var reply = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
            //        if (reply.StatusCode == System.Net.HttpStatusCode.OK) return await reply.Content.ReadAsStringAsync();
            //    }
            //    return null;
            //}
            //if (BZCCGameProperties.ContainsKey(termType) && BZCCGameProperties[termType].ContainsKey(term))
            //    return BZCCGameProperties[termType][term].Value;
            return null;
        }
    }

    //public class ProxyStatus
    //{
    //    public DateTime? updated { get; set; }
    //    public string status { get; set; }
    //    public bool? success { get; set; }
    //S}

    public class BZCCRaknetData
    {
        public List<BZCCGame> GET { get; set; }

        public Dictionary<string, ProxyStatus> proxyStatus { get; set; }
    }

    ///// All possible types of NATs (except NAT_TYPE_COUNT, which is an internal value) 
    //enum NATTypeDetectionResult
    //{
    //    /// Works with anyone
    //    NAT_TYPE_NONE,
    //    /// Accepts any datagrams to a port that has been previously used. Will accept the first datagram from the remote peer.
    //    NAT_TYPE_FULL_CONE,
    //    /// Accepts datagrams to a port as long as the datagram source IP address is a system we have already sent to. Will accept the first datagram if both systems send simultaneously. Otherwise, will accept the first datagram after we have sent one datagram.
    //    NAT_TYPE_ADDRESS_RESTRICTED,
    //    /// Same as address-restricted cone NAT, but we had to send to both the correct remote IP address and correct remote port. The same source address and port to a different destination uses the same mapping.
    //    NAT_TYPE_PORT_RESTRICTED,
    //    /// A different port is chosen for every remote destination. The same source address and port to a different destination uses a different mapping. Since the port will be different, the first external punchthrough attempt will fail. For this to work it requires port-prediction (MAX_PREDICTIVE_PORT_RANGE>1) and that the router chooses ports sequentially.
    //    NAT_TYPE_SYMMETRIC,
    //    /// Hasn't been determined. NATTypeDetectionClient does not use this, but other plugins might
    //    NAT_TYPE_UNKNOWN,
    //    /// In progress. NATTypeDetectionClient does not use this, but other plugins might
    //    NAT_TYPE_DETECTION_IN_PROGRESS,
    //    /// Didn't bother figuring it out, as we support UPNP, so it is equivalent to NAT_TYPE_NONE. NATTypeDetectionClient does not use this, but other plugins might
    //    NAT_TYPE_SUPPORTS_UPNP,
    //    /// \internal Must be last
    //    NAT_TYPE_COUNT
    //};

    public class BZCCPlayerData
    {
        public string n { get; set; } // name (base 64)

        [JsonProperty("i")] public string PlayerID { get; set; } // id (player ID)
        public string k { get; set; } // kills
        public string d { get; set; } // deaths
        public string s { get; set; } // score
        public string t { get; set; } // team

        [JsonIgnore] public string Name { get { return string.IsNullOrWhiteSpace(n) ? null : Encoding.UTF8.GetString(Convert.FromBase64String(n)); } }
        [JsonIgnore] public int? Kills { get { int tmp = 0; return int.TryParse(k, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? Deaths { get { int tmp = 0; return int.TryParse(d, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? Score { get { int tmp = 0; return int.TryParse(s, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? Team { get { int tmp = 0; return int.TryParse(t, out tmp) ? (int?)tmp : null; } }
    }

    public class BZCCGame
    {
        //public string __addr { get; set; }
        public string proxySource { get; set; }

        public string g { get; set; } // ex "4M-CB73@GX" (seems to go with NAT type 5???)
        public string n { get; set; } // varchar(256) | Name of client game session, base64 and null terminate.
        [JsonProperty("m")] public string MapFile { get; set; } // varchar(68)  | Name of client map, no bzn extension.
        public string k { get; set; } // tinyint      | Password Flag.
        public string d { get; set; } // varchar(16)  | MODSLISTCRC_KEY
        public string t { get; set; } // tinyint      | NATTYPE_KEY //nat type 5 seems bad, 7 seems to mean direct connect
        public string v { get; set; } // varchar(8)   | GAMEVERSION_KEY (nice string now)
        public string l { get; set; } // locked
        public string h { get; set; } // server message (not base64 yet)

        public string mm { get; set; } // mod list ex: "1300825258;1300820029"
        public string gt { get; set; } // game type
        public string gtd { get; set; } // sub game type
        public string pm { get; set; } // max players

        public string tps { get; set; } // tps
        public string si { get; set; } // gamestate
        public string ti { get; set; } // time limit
        public string ki { get; set; } // kill limit

        public string gtm { get; set; } // game time min
        public string pgm { get; set; } // max ping

        public BZCCPlayerData[] pl { get; set; }

        [JsonIgnore] public int CurPlayers { get { return pl?.Length ?? 0; } }
        [JsonIgnore] public int? MaxPlayers { get { int tmp = 0; return int.TryParse(pm, out tmp) ? (int?)tmp : null; } }

        [JsonIgnore] public bool Locked { get { return l == "1"; } }
        [JsonIgnore] public bool Passworded { get { return k == "1"; } }

        [JsonIgnore] public int? GameType { get { int tmp = 0; return int.TryParse(gt, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? GameSubType { get { int tmp = 0; return int.TryParse(gtd, out tmp) ? (int?)tmp : null; } }

        [JsonIgnore] public int? ServerInfoMode { get { int tmp = 0; return int.TryParse(si, out tmp) ? (int?)tmp : null; } }

        [JsonIgnore] public int? GameTimeMinutes { get { int tmp = 0; return int.TryParse(gtm, out tmp) ? (int?)tmp : null; } }

        [JsonIgnore] public int? TPS { get { int tmp = 0; return int.TryParse(tps, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? MaxPing { get { int tmp = 0; return int.TryParse(pgm, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? TimeLimit { get { int tmp = 0; return int.TryParse(ti, out tmp) ? (int?)tmp : null; } }
        [JsonIgnore] public int? KillLimit { get { int tmp = 0; return int.TryParse(ki, out tmp) ? (int?)tmp : null; } }

        [JsonIgnore] public string Name { get { return string.IsNullOrWhiteSpace(n) ? null : Encoding.UTF8.GetString(Convert.FromBase64String(n).TakeWhile(chr => chr != 0x00).ToArray()); } }
        [JsonIgnore] public string MOTD { get { try { return string.IsNullOrWhiteSpace(h) ? null : Encoding.UTF8.GetString(Convert.FromBase64String(h)); } catch { return null; } } }

        [JsonIgnore] public string[] Mods { get { return mm?.Split(';') ?? new string[] { }; } }
        
        private GameListBZCCService _bzcc;

        public bool IsOnRebellion()
        {
            return proxySource == "Rebellion";
        }

        public bool IsOnIonDriver()
        {
            return proxySource == null;
        }
    }
}
