using Discord;
using Discord.API;
using Discord.WebSocket;
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
    public class GameListBZ2Service
    {
        //private readonly IBotCredentials _creds;
        //private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        //public ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>> BZ2GameProperties { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();

        private const string queryUrl = "http://raknetsrv2.iondriver.com/testServer?__gameId=BZ2&__excludeCols=__rowId,__city,__cityLon,__cityLat,__timeoutSec,__geoIP,__gameId&__pluginShowSource=true&__pluginQueryServers=true&__pluginShowStatus=true";
        
        private Logger _log;

        public GameListBZ2Service(/*IBotCredentials creds, DbService db,*/ DiscordSocketClient client)
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
        }

        public async Task<RaknetData> GetGames()
        {
            using (var http = new HttpClient())
            {
                var res = await http.GetStringAsync(queryUrl).ConfigureAwait(false);
                var gamelist = JsonConvert.DeserializeObject<RaknetData>(res);
                gamelist.SetBz2Service(this);
                //if (gamelist?.Title == null)
                //    return null;
                //gamelist.Poster = await NadekoBot.Google.ShortenUrl(gamelist.Poster);
                return gamelist;
            }
        }

        public async Task<string> GetBZ2GameProperty(string termType, string term)
        {
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

        public EmbedBuilder GetTopEmbed()
        {
            bool isMatesFamily = GET.Any(game => game.IsMatesFamilyMarker());
            bool isKebbzNet = GET.Any(game => game.IsKebbzNetMarker());
            bool isIonDriver = GET.Any(game => game.IsIonDriverMarker());

            bool isOnMatesFamily = GET.Any(game => game.IsOnMatesFamily());
            bool isOnKebbzNet = GET.Any(game => game.IsOnKebbzNet());
            bool isOnIonDriver = GET.Any(game => game.IsOnIonDriver());

            bool haveMatesFamilyStatus = proxyStatus.ContainsKey("masterserver.matesfamily.org");
            bool haveKebbzNetStatus = proxyStatus.ContainsKey("gamelist.kebbz.com");

            bool isMatesFamilyUp = haveMatesFamilyStatus && proxyStatus["masterserver.matesfamily.org"].success == true;
            bool isKebbzNetUp = haveKebbzNetStatus && proxyStatus["gamelist.kebbz.com"].success == true;

            string statusMatesFamily = haveMatesFamilyStatus ? proxyStatus["masterserver.matesfamily.org"].status : null;
            string statusKebbzNet = haveKebbzNetStatus ? proxyStatus["gamelist.kebbz.com"].status : null;

            DateTime? dateMatesFamily = haveMatesFamilyStatus ? proxyStatus["masterserver.matesfamily.org"].updated : null;
            DateTime? dateKebbzNet = haveKebbzNetStatus ? proxyStatus["gamelist.kebbz.com"].updated : null;

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(new Color(255, 255, 255))
                .WithTitle("Battlezone II Game List")
                //.WithUrl()
                .WithDescription($"List of games currently on Battlezone II matchmaking servers\n`{GET.Where(game => !game.IsMarker()).Count()} Game(s)`")
                .WithThumbnailUrl("http://discord.battlezone.report/resources/logos/bz2.png")
                .WithFooter(efb => efb.WithText("Brought to you by Nielk1's Raknet Bot"));
            //////////////////////////////////////////////////////////////////////////////////////////////
            if (isIonDriver)
            {
                embed.AddField(efb => efb.WithName("IonDriver").WithValue("✅ Online").WithIsInline(true));
            }
            else if (isIonDriver)
            {
                embed.AddField(efb => efb.WithName("IonDriver").WithValue("⚠ No Marker").WithIsInline(true));
            }
            else
            {
                embed.AddField(efb => efb.WithName("IonDriver").WithValue("❓ Unknown").WithIsInline(true));
            }
            //////////////////////////////////////////////////////////////////////////////////////////////
            embed.AddField(efb => efb.WithName("Raknet").WithValue("⛔ Dead").WithIsInline(true));
            //////////////////////////////////////////////////////////////////////////////////////////////
            if (isMatesFamily)
            {
                if (statusMatesFamily == "new")
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue($"✅ Online\n`Updated {TimeAgoUtc(dateMatesFamily.Value)}`").WithIsInline(true));
                }
                else if (statusMatesFamily == "cached" && dateMatesFamily.HasValue)
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue($"✅ Online\n`Updated {TimeAgoUtc(dateMatesFamily.Value)}`").WithIsInline(true));
                }
                else
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue("✅ Online").WithIsInline(true));
                }
            }
            else if (isOnMatesFamily || isMatesFamilyUp)
            {
                if (statusMatesFamily == "new")
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue($"⚠ No Marker\n`Updated {TimeAgoUtc(dateMatesFamily.Value)}`").WithIsInline(true));
                }
                else if (statusMatesFamily == "cached" && dateMatesFamily.HasValue)
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue($"⚠ No Marker\n`Updated {TimeAgoUtc(dateMatesFamily.Value)}`").WithIsInline(true));
                }
                else
                {
                    embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue("⚠ No Marker").WithIsInline(true));
                }
            }
            else if (!isMatesFamilyUp)
            {
                embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue("❌ Offline").WithIsInline(true));
            }
            else
            {
                embed.AddField(efb => efb.WithName("MatesFamily (Primary)").WithValue("❓ Unknown").WithIsInline(true));
            }
            //////////////////////////////////////////////////////////////////////////////////////////////
            if (isKebbzNet)
            {
                if (statusKebbzNet == "new")
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue($"✅ Online\n`Updated {TimeAgoUtc(dateKebbzNet.Value)}`").WithIsInline(true));
                }
                else if (statusKebbzNet == "cached" && dateKebbzNet.HasValue)
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue($"✅ Online\n`Updated {TimeAgoUtc(dateKebbzNet.Value)}`").WithIsInline(true));
                }
                else
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue("✅ Online").WithIsInline(true));
                }
            }
            else if (isKebbzNet || isKebbzNetUp)
            {
                if (statusKebbzNet == "new")
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue($"⚠ No Marker\n`Updated {TimeAgoUtc(dateKebbzNet.Value)}`").WithIsInline(true));
                }
                else if (statusKebbzNet == "cached" && dateKebbzNet.HasValue)
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue($"⚠ No Marker\n`Updated {TimeAgoUtc(dateKebbzNet.Value)}`").WithIsInline(true));
                }
                else
                {
                    embed.AddField(efb => efb.WithName("Kebbznet").WithValue("⚠ No Marker").WithIsInline(true));
                }
            }
            else if (!isKebbzNetUp)
            {
                embed.AddField(efb => efb.WithName("Kebbznet").WithValue("❌ Offline").WithIsInline(true));
            }
            else
            {
                embed.AddField(efb => efb.WithName("Kebbznet").WithValue("❓ Unknown").WithIsInline(true));
            }
            //////////////////////////////////////////////////////////////////////////////////////////////

            return embed;
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

        internal void SetBz2Service(GameListBZ2Service bZ2Service)
        {
            GET.ForEach(dr => dr.SetBz2Service(bZ2Service));
        }
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
        public string n { get; set; } // varchar(256) | Name of client game session.
        public string m { get; set; } // varchar(68)  | Name of client map, no bzn extension.
        public string k { get; set; } // tinyint      | Password Flag.
        public string d { get; set; } // varchar(16)  | MODSLISTCRC_KEY
        public string t { get; set; } // tinyint      | NATTYPE_KEY //nat type 5 seems bad, 7 seems to mean direct connect
        public string r { get; set; } // varchar(16)  | PRIVATEADDRESS_KEY  // ex "@Zg@w"
        public string v { get; set; } // varchar(8)   | GAMEVERSION_KEY
        public string p { get; set; } // varchar(16)  | GAMEPORT_KEY
        public string l { get; set; } // locked

        private GameListBZ2Service _bz2;

        public RaknetPongResponse pong { get; set; }

        public bool IsMatesFamilyMarker()
        {
            return proxySource == "masterserver.matesfamily.org"
                && l == "1"
                && m == "See http://matesfamily.org/bz2/";
        }

        public bool IsKebbzNetMarker()
        {
            return proxySource == "gamelist.kebbz.com"
                && n == "http://www.bz2maps.us"
                && m == "bz2maps";
        }

        public bool IsIonDriverMarker()
        {
            return proxySource == null
                && l == "1"
                && m == "bismuth";
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

        public async Task<EmbedBuilder> GetEmbed(int idx, int total)
        {
            string footer = $"[{idx}/{total}] ({m}.bzn)";
            if (pong != null && pong.CompressedData != null && pong.CompressedData.Mods.Length > 0)
            {
                footer += " " + Format.Sanitize(pong.CompressedData.Mods);
            }

            EmbedBuilder embed = new EmbedBuilder()
                .WithDescription(await GetGameDataString())
                .WithFooter(efb => efb.WithText(footer));

            string prop = await _bz2.GetBZ2GameProperty("shell", Format.Sanitize(m));
            embed.WithThumbnailUrl(prop ?? "http://discord.battlezone.report/resources/logos/nomap.png");

            string playerCountData = string.Empty;
            bool fullPlayers = false;
            if (pong != null)
            {
                playerCountData = " [" + pong.CurPlayers + "/" + pong.MaxPlayers + "]";
                fullPlayers = (pong.CurPlayers >= pong.MaxPlayers);
            }

            if (l == "1")
            {
                embed.WithColor(new Color(0xbe, 0x19, 0x31))
                     .WithTitle("⛔ " + Format.Sanitize(n) + playerCountData);
            }
            else if (k == "1")
            {
                embed.WithColor(new Color(0xff, 0xac, 0x33))
                     .WithTitle("🔐 " + Format.Sanitize(n) + playerCountData);
            }
            else if (pong == null)
            {
                embed.WithColor(new Color(0xff, 0xff, 0x00))
                     .WithTitle("❓ " + Format.Sanitize(n) + playerCountData);
            }
            else if (pong != null)
            {
                float fullnessRatio = 1.0f * pong.CurPlayers / pong.MaxPlayers;

                if (fullnessRatio >= 1.0f)
                {
                    embed.WithOkColor().WithTitle("🌕 " + Format.Sanitize(n) + playerCountData);
                }
                else if (fullnessRatio >= 0.75f)
                {
                    embed.WithOkColor().WithTitle("🌖 " + Format.Sanitize(n) + playerCountData);
                }
                else if (fullnessRatio >= 0.50f)
                {
                    embed.WithOkColor().WithTitle("🌗 " + Format.Sanitize(n) + playerCountData);
                }
                else if (fullnessRatio >= 0.25f)
                {
                    embed.WithOkColor().WithTitle("🌘 " + Format.Sanitize(n) + playerCountData);
                }
                else if (fullnessRatio >= 0.0f)
                {
                    embed.WithOkColor().WithTitle("🌑 " + Format.Sanitize(n) + playerCountData);
                }
                else
                {
                    embed.WithOkColor().WithTitle("👽 " + Format.Sanitize(n) + playerCountData); // this should never happen
                }
            }
            else // this one should never happen
            {
                embed.WithColor(new Color(0xff, 0xff, 0x00))
                     .WithTitle("⚠ " + Format.Sanitize(n) + playerCountData);
            }

            if (pong != null)
            {
                if (pong.CompressedData != null)
                {
                    if (pong.CompressedData.Players.Length > 0)
                    {
                        embed.AddField(efb => efb.WithName("(K/D/S) Players").WithValue(pong.CompressedData.GetPlayersString()).WithIsInline(false));
                    }
                }
            }

            return embed;
        }

        public async Task<string> GetGameDataString()
        {
            string name = await _bz2.GetBZ2GameProperty("name", m);
            string version = await _bz2.GetBZ2GameProperty("version", v);
            string mod = await _bz2.GetBZ2GameProperty("mod", d);


            StringBuilder builder = new StringBuilder();

            if (string.IsNullOrWhiteSpace(name))
            {
                builder.AppendLine($@"Map     | [{m}]");
            }
            else
            {
                builder.AppendLine($@"Map     | {name}");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                builder.AppendLine($@"Version | [{v}]");
            }
            else
            {
                builder.AppendLine($@"Version | {version}");
            }

            if (string.IsNullOrWhiteSpace(mod))
            {
                builder.AppendLine($@"Mod     | [{d}]");
            }
            else
            {
                builder.AppendLine($@"Mod     | {mod}");
            }

            switch (t)
            {
                case "0":
                    builder.AppendLine(@"NAT     | NONE"); /// Works with anyone
                    break;
                case "1":
                    builder.AppendLine(@"NAT     | FULL CONE"); /// Accepts any datagrams to a port that has been previously used. Will accept the first datagram from the remote peer.
                    break;
                case "2":
                    builder.AppendLine(@"NAT     | ADDRESS RESTRICTED"); /// Accepts datagrams to a port as long as the datagram source IP address is a system we have already sent to. Will accept the first datagram if both systems send simultaneously. Otherwise, will accept the first datagram after we have sent one datagram.
                    break;
                case "3":
                    builder.AppendLine(@"NAT     | PORT RESTRICTED"); /// Same as address-restricted cone NAT, but we had to send to both the correct remote IP address and correct remote port. The same source address and port to a different destination uses the same mapping.
                    break;
                case "4":
                    builder.AppendLine(@"NAT     | SYMMETRIC"); /// A different port is chosen for every remote destination. The same source address and port to a different destination uses a different mapping. Since the port will be different, the first external punchthrough attempt will fail. For this to work it requires port-prediction (MAX_PREDICTIVE_PORT_RANGE>1) and that the router chooses ports sequentially.
                    break;
                case "5":
                    builder.AppendLine(@"NAT     | UNKNOWN"); /// Hasn't been determined. NATTypeDetectionClient does not use this, but other plugins might
                    break;
                case "6":
                    builder.AppendLine(@"NAT     | DETECTION IN PROGRESS"); /// In progress. NATTypeDetectionClient does not use this, but other plugins might
                    break;
                case "7":
                    builder.AppendLine(@"NAT     | SUPPORTS UPNP"); /// Didn't bother figuring it out, as we support UPNP, so it is equivalent to NAT_TYPE_NONE. NATTypeDetectionClient does not use this, but other plugins might
                    break;
                default:
                    builder.AppendLine(@"NAT     | [" + t + "]");
                    break;
            }

            switch (proxySource)
            {
                case "masterserver.matesfamily.org":
                    builder.AppendLine(@"List    | MatesFamily");
                    break;
                case "gamelist.kebbz.com":
                    builder.AppendLine(@"List    | KebbzNet");
                    break;
                default:
                    builder.AppendLine(@"List    | IonDriver");
                    break;
            }

            if (pong != null)
            {
                switch (pong.GameType)
                {
                    case 0:
                        builder.AppendLine(@"Type    | All");
                        break;
                    case 1:
                        switch (pong.GameSubType)
                        {
                            case 0:
                                builder.AppendLine(@"Type    | DM");
                                break;
                            case 1:
                                builder.AppendLine(@"Type    | KOTH");
                                break;
                            case 2:
                                builder.AppendLine(@"Type    | CTF");
                                break;
                            case 3:
                                builder.AppendLine(@"Type    | Loot");
                                break;
                            case 4:
                                builder.AppendLine(@"Type    | DM [RESERVED]");
                                break;
                            case 5:
                                builder.AppendLine(@"Type    | Race");
                                break;
                            case 6:
                                builder.AppendLine(@"Type    | Race (Vehicle Only)");
                                break;
                            case 7:
                                builder.AppendLine(@"Type    | DM (Vehicle Only)");
                                break;
                            default:
                                builder.AppendLine(@"Type    | DM [UNKNOWN]");
                                break;
                        }
                        break;
                    case 2:
                        if (pong.TeamsOn && pong.OnlyOneTeam)
                        {
                            builder.AppendLine(@"Type    | MPI");
                        }
                        else
                        {
                            builder.AppendLine(@"Type    | Strat");
                        }
                        break;
                    case 3:
                        builder.AppendLine(@"Type    | MPI [Invalid]");
                        break;
                }

                switch (pong.ServerInfoMode)
                {
                    case 1:
                        builder.AppendLine($"Time    | Not playing or in shell for {pong.GameTimeMinutes} minutes");
                        break;
                    case 3:
                        builder.AppendLine($"Time    | Playing for {pong.GameTimeMinutes} minutes");
                        break;
                }

                builder.AppendLine($"TPS     | {pong.TPS}");

                if (pong.MaxPing > 0)
                    builder.AppendLine($"MaxPing | {pong.MaxPing}");

                if (pong.TimeLimit > 0)
                    builder.AppendLine($"TimeLim | {pong.TimeLimit}");

                if (pong.KillLimit > 0)
                    builder.AppendLine($"KillLim | {pong.KillLimit}");
            }

            string retVal = Format.Code(builder.ToString(), "css");

            if (pong != null && pong.CompressedData != null)
            {
                if (pong.CompressedData.MapURL.Length > 0)
                {
                    retVal = Format.Sanitize(pong.CompressedData.MapURL) + "\n" + retVal;
                }

                if (pong.CompressedData.MOTD.Length > 0)
                {
                    retVal = Format.Sanitize(pong.CompressedData.MOTD) + "\n" + retVal;
                }
            }

            return retVal;
        }

        internal void SetBz2Service(GameListBZ2Service bZ2Service)
        {
            _bz2 = bZ2Service;
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

        public string GetPlayersString()
        {
            StringBuilder builder = new StringBuilder();

            int k = 1;
            int d = 1;
            int s = 1;
            bool scoreNeedsSign = false;

            Players.ForEach(dr =>
            {
                k = Math.Max(k, dr.Kills.ToString().Length);
                d = Math.Max(d, dr.Deaths.ToString().Length);
                s = Math.Max(s, Math.Abs(dr.Score).ToString().Length);
                scoreNeedsSign = scoreNeedsSign || (dr.Score < 0);
            });

            Players.ForEach(dr =>
            {
                string scoresign = "0";
                if (dr.Score > 0) scoresign = "+";
                if (dr.Score < 0) scoresign = "-";
                if (!scoreNeedsSign) scoresign = string.Empty;
                builder.AppendLine($"`{dr.Kills.ToString().PadLeft(k, '0')}/{dr.Deaths.ToString().PadLeft(d, '0')}/{scoresign}{Math.Abs(dr.Score).ToString().PadLeft(s, '0')}` {Format.Sanitize(dr.UserName)}");
            });

            //return Format.Code(builder.ToString(), "css");
            return builder.ToString();
        }
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
