using Discord;
using Discord.API;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;
using NadekoBot.Extensions;
using NadekoBot.Services;
using Newtonsoft.Json;
using NLog;
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
    public class GameListBZ2Service : INService, IGameList
    {
        public string Emoji => @"<:game_icon_battlezone2:342134902587785219>";
        public string Title => "Battlezone 2: Combat Commander";
        public string Code => "bz2";

        //private readonly IBotCredentials _creds;
        //private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly GamesListService _gameList;

        //public ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>> BZ2GameProperties { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();

        private const string queryUrl = "http://raknetsrv2.iondriver.com/testServer?__gameId=BZ2&__excludeCols=__rowId,__city,__cityLon,__cityLat,__timeoutSec,__geoIP,__gameId&__pluginShowSource=true&__pluginQueryServers=true&__pluginShowStatus=true";
        
        private Logger _log;

        public GameListBZ2Service(/*IBotCredentials creds, DbService db,*/ DiscordSocketClient client, GamesListService gameList)
        {
            //_creds = creds;
            //_db = db;
            _client = client;

            _log = LogManager.GetCurrentClassLogger();
            /*var sw = Stopwatch.StartNew();
            using (var uow = _db.UnitOfWork)
            {
                var items = uow.BZ2GameProperties.GetAll();
                BZ2GameProperties = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();
                items.ForEach(BZ2Property =>
                {
                    lock (BZ2GameProperties)
                    {
                        if (!BZ2GameProperties.ContainsKey(BZ2Property.TermType))
                            BZ2GameProperties[BZ2Property.TermType] = new ConcurrentDictionary<string, BZ2GameProperty>();

                        BZ2GameProperties[BZ2Property.TermType][BZ2Property.Term] = BZ2Property;
                    }
                });
            }
            sw.Stop();
            _log.Debug($"Loaded in {sw.Elapsed.TotalSeconds:F2}s");*/

            _gameList = gameList;
            _gameList.AddGameListBZ2Service(this);
            _gameList.RegisterGameList(this);
        }

        public async Task<DataGameList> GetGamesNew()
        {
            using (var http = new HttpClient())
            {
                var res = await http.GetStringAsync(queryUrl).ConfigureAwait(false);
                var gamelist = JsonConvert.DeserializeObject<RaknetData>(res);
                //gamelist.SetBz2Service(this);
                //return gamelist;

                DataGameList data = new DataGameList() {
                    GameTitle = this.Title,
                    Header = new DataGameListHeader() {
                        Description = "List of games currently on Battlezone II matchmaking servers",
                        Image = "http://discord.battlezone.report/resources/logos/bz2.png",
                        Credit = "Brought to you by Nielk1's Raknet Bot"
                    }
                };

                {
                    bool isIonDriver = gamelist.GET.Any(game => game.IsIonDriverMarker());
                    bool isOnIonDriver = gamelist.GET.Any(game => game.IsOnIonDriver());
                    DataGameListServerStatus IonDriverStatus = new DataGameListServerStatus() { Name = "IonDriver", Updated = null };
                    if (isIonDriver || isOnIonDriver)
                    {
                        IonDriverStatus.Status = EDataGameListServerStatus.Online;
                    }
                    else
                    {
                        IonDriverStatus.Status = EDataGameListServerStatus.NoGames;
                    }

                    bool isMatesFamily = gamelist.GET.Any(game => game.IsMatesFamilyMarker());
                    bool isOnMatesFamily = gamelist.GET.Any(game => game.IsOnMatesFamily());
                    bool haveMatesFamilyStatus = gamelist.proxyStatus.ContainsKey("masterserver.matesfamily.org");
                    bool isMatesFamilyUp = haveMatesFamilyStatus && gamelist.proxyStatus["masterserver.matesfamily.org"].success == true;
                    string statusMatesFamily = haveMatesFamilyStatus ? gamelist.proxyStatus["masterserver.matesfamily.org"].status : null;
                    DateTime? dateMatesFamily = haveMatesFamilyStatus ? gamelist.proxyStatus["masterserver.matesfamily.org"].updated : null;
                    DataGameListServerStatus MatesFamilyStatus = new DataGameListServerStatus() { Name = "MatesFamily (Primary)", Updated = dateMatesFamily };
                    if (isOnMatesFamily || isMatesFamily)
                    {
                        MatesFamilyStatus.Status = EDataGameListServerStatus.Online;
                    }
                    else if (isMatesFamilyUp)
                    {
                        MatesFamilyStatus.Status = EDataGameListServerStatus.NoGames;
                    }
                    else if (!isMatesFamilyUp)
                    {
                        MatesFamilyStatus.Status = EDataGameListServerStatus.Offline;
                    }
                    else
                    {
                        MatesFamilyStatus.Status = EDataGameListServerStatus.Unknown;
                    }

                    data.Header.ServerStatus = new DataGameListServerStatus[] { IonDriverStatus, MatesFamilyStatus };
                }

                data.Games = (await Task.WhenAll(
                    gamelist.GET
                    .Where(dr => !dr.IsMarker())
                    .Select(async raw =>
                    {
                        DataGameListGame game = new DataGameListGame();

                        game.Name = raw.Name;
                        game.Image = await GetBZ2GameProperty("shell", raw.MapFile) ?? "http://discord.battlezone.report/resources/logos/nomap.png";

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
                        else if (!raw.CurPlayers.HasValue || !raw.MaxPlayers.HasValue)
                        {
                            game.Status = EDataGameListServerGameStatus.Unknown;
                        }
                        else
                        {
                            game.Status = EDataGameListServerGameStatus.Open;
                        }

                        game.MapFilename = raw.MapFile;
                        game.Footer = raw.MapFile + @".bzn";

                        if (raw.pong != null && raw.pong.CompressedData != null)
                        {
                            if (raw.pong.CompressedData.MOTD.Length > 0)
                            {
                                game.TopInfo.Add(Format.Sanitize(raw.pong.CompressedData.MOTD));
                            }

                            if (raw.pong.CompressedData.MapURL.Length > 0)
                            {
                                game.TopInfo.Add(Format.Sanitize(raw.pong.CompressedData.MapURL));
                            }
                        }

                        {
                            string name = await GetBZ2GameProperty("name", raw.MapFile);
                            string version = await GetBZ2GameProperty("version", raw.v);
                            string mod = await GetBZ2GameProperty("mod", raw.d);

                            if (string.IsNullOrWhiteSpace(name))
                            {
                                game.Properties.Add(new Tuple<string, string>("Map", $"[{raw.MapFile}]"));
                            }
                            else
                            {
                                game.Properties.Add(new Tuple<string, string>("Map", $"{name}"));
                            }

                            if (string.IsNullOrWhiteSpace(version))
                            {
                                game.Properties.Add(new Tuple<string,string>("Version",$"[{raw.v}]"));
                            }
                            else
                            {
                                game.Properties.Add(new Tuple<string,string>("Version",$"{version}"));
                            }

                            if (string.IsNullOrWhiteSpace(mod))
                            {
                                game.Properties.Add(new Tuple<string,string>("Mod",$"[{raw.d}]"));
                            }
                            else
                            {
                                game.Properties.Add(new Tuple<string,string>("Mod",$"{mod}"));
                            }

                            switch (raw.t)
                            {
                                case"0":
                                    game.Properties.Add(new Tuple<string, string>("NAT",$"NONE")); /// Works with anyone
                                    break;
                                case"1":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"FULL CONE")); /// Accepts any datagrams to a port that has been previously used. Will accept the first datagram from the remote peer.
                                    break;
                                case"2":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"ADDRESS RESTRICTED")); /// Accepts datagrams to a port as long as the datagram source IP address is a system we have already sent to. Will accept the first datagram if both systems send simultaneously. Otherwise, will accept the first datagram after we have sent one datagram.
                                    break;
                                case"3":
                                    game.Properties.Add(new Tuple<string, string>("NAT",$"PORT RESTRICTED")); /// Same as address-restricted cone NAT, but we had to send to both the correct remote IP address and correct remote port. The same source address and port to a different destination uses the same mapping.
                                    break;
                                case"4":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"SYMMETRIC")); /// A different port is chosen for every remote destination. The same source address and port to a different destination uses a different mapping. Since the port will be different, the first external punchthrough attempt will fail. For this to work it requires port-prediction (MAX_PREDICTIVE_PORT_RANGE>1) and that the router chooses ports sequentially.
                                    break;
                                case"5":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"UNKNOWN")); /// Hasn't been determined. NATTypeDetectionClient does not use this, but other plugins might
                                    break;
                                case"6":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"DETECTION IN PROGRESS")); /// In progress. NATTypeDetectionClient does not use this, but other plugins might
                                    break;
                                case"7":
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"SUPPORTS UPNP")); /// Didn't bother figuring it out, as we support UPNP, so it is equivalent to NAT_TYPE_NONE. NATTypeDetectionClient does not use this, but other plugins might
                                    break;
                                default:
                                    game.Properties.Add(new Tuple<string,string>("NAT",$"[" + raw.t +"]"));
                                    break;
                            }

                            switch (raw.proxySource)
                            {
                                case"masterserver.matesfamily.org":
                                    game.Properties.Add(new Tuple<string,string>("List",$"MatesFamily"));
                                    break;
                                case"gamelist.kebbz.com":
                                    game.Properties.Add(new Tuple<string,string>("List",$"KebbzNet"));
                                    break;
                                default:
                                    game.Properties.Add(new Tuple<string,string>("List",$"IonDriver"));
                                    break;
                            }

                            if (raw.pong != null)
                            {
                                if(raw.pong.CompressedData != null)
                                {
                                    int k = 1;
                                    int d = 1;
                                    int s = 1;
                                    bool scoreNeedsSign = false;

                                    raw.pong.CompressedData.Players.ForEach(dr =>
                                    {
                                        k = Math.Max(k, dr.Kills.ToString().Length);
                                        d = Math.Max(d, dr.Deaths.ToString().Length);
                                        s = Math.Max(s, Math.Abs(dr.Score).ToString().Length);
                                        scoreNeedsSign = scoreNeedsSign || (dr.Score < 0);
                                    });

                                    raw.pong.CompressedData.Players.ForEach(dr =>
                                    {
                                        string scoresign = "0";
                                        if (dr.Score > 0) scoresign = "+";
                                        if (dr.Score < 0) scoresign = "-";
                                        if (!scoreNeedsSign) scoresign = string.Empty;

                                        game.Players.Add(new DataGameListPlayer()
                                        {
                                            Index = dr.Team,
                                            Name = dr.UserName,
                                            PlayerClass = $"{dr.Kills.ToString().PadLeft(k, '0')}/{dr.Deaths.ToString().PadLeft(d, '0')}/{scoresign}{Math.Abs(dr.Score).ToString().PadLeft(s, '0')}",
                                            Url = null
                                        });
                                    });

                                    game.PlayersHeader = "[T] (K/D/S) Players";
                                }

                                switch (raw.pong.GameType)
                                {
                                    case 0:
                                        game.Properties.Add(new Tuple<string,string>("Type",$"All"));
                                        break;
                                    case 1:
                                        switch (raw.pong.GameSubType)
                                        {
                                            case 0:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"DM"));
                                                break;
                                            case 1:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"KOTH"));
                                                break;
                                            case 2:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"CTF"));
                                                break;
                                            case 3:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"Loot"));
                                                break;
                                            case 4:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"DM [RESERVED]"));
                                                break;
                                            case 5:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"Race"));
                                                break;
                                            case 6:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"Race (Vehicle Only)"));
                                                break;
                                            case 7:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"DM (Vehicle Only)"));
                                                break;
                                            default:
                                                game.Properties.Add(new Tuple<string,string>("Type",$"DM [UNKNOWN]"));
                                                break;
                                        }
                                        break;
                                    case 2:
                                        if (raw.pong.TeamsOn && raw.pong.OnlyOneTeam)
                                        {
                                            game.Properties.Add(new Tuple<string,string>("Type",$"MPI"));
                                        }
                                        else
                                        {
                                            game.Properties.Add(new Tuple<string,string>("Type",$"Strat"));
                                        }
                                        break;
                                    case 3:
                                        game.Properties.Add(new Tuple<string,string>("Type",$"MPI [Invalid]"));
                                        break;
                                }

                                switch (raw.pong.ServerInfoMode)
                                {
                                    case 1:
                                        game.Properties.Add(new Tuple<string, string>("Time",$"Not playing or in shell for {raw.pong.GameTimeMinutes} minutes"));
                                        break;
                                    case 3:
                                        game.Properties.Add(new Tuple<string, string>("Time",$"Playing for {raw.pong.GameTimeMinutes} minutes"));
                                        break;
                                }

                                game.Properties.Add(new Tuple<string, string>("TPS", $"{raw.pong.TPS}"));

                                if (raw.pong.MaxPing > 0)
                                    game.Properties.Add(new Tuple<string, string>("MaxPing", $"{raw.pong.MaxPing}"));

                                if (raw.pong.TimeLimit > 0)
                                    game.Properties.Add(new Tuple<string, string>("TimeLim", $"{raw.pong.TimeLimit}"));

                                if (raw.pong.KillLimit > 0)
                                    game.Properties.Add(new Tuple<string, string>("KillLim",$"{raw.pong.KillLimit}"));
                            }
                        }

                        return game;
                    }))).ToArray();

                return data;
            }
        }

        public async Task<string> GetBZ2GameProperty(string termType, string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return null;

            if (new string[] { "version", "mod" }.Contains(termType))
            {
                using (var http = new HttpClient())
                {
                    var url = $"http://discord.battlezone.report/resources/meta/bz2/{termType}/{term}";
                    var reply = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                    if (reply.StatusCode == System.Net.HttpStatusCode.OK) return await reply.Content.ReadAsStringAsync();
                }
                return null;
            }
            //if (BZ2GameProperties.ContainsKey(termType) && BZ2GameProperties[termType].ContainsKey(term))
            //    return BZ2GameProperties[termType][term].Value;
            return null;
        }
    }

    public class ProxyStatus
    {
        public DateTime? updated { get; set; }
        public string status { get; set; }
        public bool? success { get; set; }
    }

    public class RaknetData
    {
        public List<BZ2Game> GET { get; set; }

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

    public class BZ2Game
    {
        public string __addr { get; set; }
        public string proxySource { get; set; }

        public string g { get; set; } // ex "4M-CB73@GX" (seems to go with NAT type 5???)
        [JsonProperty("n")] public string Name { get; set; } // varchar(256) | Name of client game session.
        [JsonProperty("m")] public string MapFile { get; set; } // varchar(68)  | Name of client map, no bzn extension.
        public string k { get; set; } // tinyint      | Password Flag.
        public string d { get; set; } // varchar(16)  | MODSLISTCRC_KEY
        public string t { get; set; } // tinyint      | NATTYPE_KEY //nat type 5 seems bad, 7 seems to mean direct connect
        public string r { get; set; } // varchar(16)  | PRIVATEADDRESS_KEY  // ex "@Zg@w"
        public string v { get; set; } // varchar(8)   | GAMEVERSION_KEY
        public string p { get; set; } // varchar(16)  | GAMEPORT_KEY
        public string l { get; set; } // locked

        [JsonIgnore] public int? CurPlayers { get { return pong?.CurPlayers; } }
        [JsonIgnore] public int? MaxPlayers { get { return pong?.MaxPlayers; } }

        [JsonIgnore] public bool Locked { get { return l == "1"; } }
        [JsonIgnore] public bool Passworded { get { return k == "1"; } }

        public RaknetPongResponse pong { get; set; }

        public bool IsMatesFamilyMarker()
        {
            return proxySource == "masterserver.matesfamily.org"
                && l == "1"
                && MapFile == "See http://matesfamily.org/bz2/";
        }

        public bool IsKebbzNetMarker()
        {
            return proxySource == "gamelist.kebbz.com"
                && Name == "http://www.bz2maps.us"
                && MapFile == "bz2maps";
        }

        public bool IsIonDriverMarker()
        {
            return proxySource == null
                && l == "1"
                && MapFile == "bismuth";
        }

        public bool IsOnMatesFamily()
        {
            return proxySource == "masterserver.matesfamily.org";
        }

        public bool IsOnKebbzNet()
        {
            return proxySource == "gamelist.kebbz.com";
        }

        public bool IsOnIonDriver()
        {
            return proxySource == null;
        }

        public bool IsMarker()
        {
            return IsMatesFamilyMarker() || IsKebbzNetMarker() || IsIonDriverMarker();
        }
    }

    public class RaknetPongResponse
    {
        public byte DataVersion { get; set; } // To ignore malformed data
        public byte TimeLimit { get; set; }
        public byte KillLimit { get; set; }
        public byte GameTimeMinutes { get; set; }

        public UInt16 MaxPing { get; set; }
        public UInt16 GameVersion { get; set; } // == NETWORK_GAME_VERSION

        public CompressibleRaknetPongResponse CompressedData { get; set; }

        public bool bDataValid { get; set; }
        public bool bPassworded { get; set; }
        public byte CurPlayers { get; set; }
        public byte MaxPlayers { get; set; }
        public byte TPS { get; set; }
        public bool bLockedDown { get; set; }
        public byte GameType { get; set; } // ivar5
        public byte ServerInfoMode { get; set; } // 3 bits so it has several possibe values clearly
        public bool TeamsOn { get; set; } // ivar3
        public byte GameSubType { get; set; } // ivar7
        public bool OnlyOneTeam { get; set; } // ivar12
    }

    public class CompressibleRaknetPongResponse
    {
        public string SessionName { get; set; }
        public string MapName { get; set; }
        public string Mods { get; set; }
        public string MapURL { get; set; }
        public string MOTD { get; set; }
        public RaknetPongPlayerInfo[] Players { get; set; }
    }

    public class RaknetPongPlayerInfo
    {
        public byte Kills { get; set; }
        public byte Deaths { get; set; }
        public byte Team { get; set; }
        public short Score { get; set; }
        public string UserName { get; set; }
    }
}
