using Discord;
using Discord.API;
using NadekoBot.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Battlezone.Commands.BZ2
{
    public static class BZ2Provider
    {
        private const string queryUrl = "http://raknetsrv2.iondriver.com/testServer?__gameId=BZ2&__excludeCols=__rowId,__city,__cityLon,__cityLat,__timeoutSec,__geoIP,__gameId&__pluginShowSource=true&__pluginQueryServers=true";

        public static async Task<RaknetData> GetGames()
        {
            using (var http = new HttpClient())
            {
                var res = await http.GetStringAsync(queryUrl).ConfigureAwait(false);
                var gamelist = JsonConvert.DeserializeObject<RaknetData>(res);
                //if (gamelist?.Title == null)
                //    return null;
                //gamelist.Poster = await NadekoBot.Google.ShortenUrl(gamelist.Poster);
                return gamelist;
            }
        }
    }

    public class RaknetData
    {
        public List<BZ2Game> GET { get; set; }

        public EmbedBuilder GetTopEmbed()
        {
            bool isMatesFamily = GET.Any(game => game.IsMatesFamilyMarker());
            bool isKebbzNet = GET.Any(game => game.IsKebbzNetMarker());
            bool isIonDriver = GET.Any(game => game.IsIonDriverMarker());

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(new Color(255, 255, 255))
                .WithTitle("Battlezone II Game List")
                //.WithUrl()
                .WithDescription($"List of games currently on Battlezone II matchmaking servers\n`{GET.Where(game => !game.IsMarker()).Count()} Game(s)`")
                .WithThumbnailUrl("http://vignette1.wikia.nocookie.net/battlezone/images/3/30/Isdf_logo.png/revision/latest/scale-to-width-down/80")
                .WithFooter(efb => efb.WithText("Brought to you by Nielk1's Raknet Bot"));

            if (isMatesFamily)
            {
                embed.AddField(efb => efb.WithName("MatesFamily").WithValue("✅ Online (Primary)").WithIsInline(true));
            } else
            {
                embed.AddField(efb => efb.WithName("MatesFamily").WithValue("⚠ Unknown (Primary)").WithIsInline(true));
            }

            embed.AddField(efb => efb.WithName("Raknet").WithValue("⛔ Dead").WithIsInline(true));

            if (isKebbzNet)
            {
                embed.AddField(efb => efb.WithName("Kebbznet").WithValue("✅ Online").WithIsInline(true));
            }
            else
            {
                embed.AddField(efb => efb.WithName("Kebbznet").WithValue("⚠ Unknown").WithIsInline(true));
            }

            if (isIonDriver)
            {
                embed.AddField(efb => efb.WithName("IonDriver").WithValue("✅ Online").WithIsInline(true));
            }
            else
            {
                embed.AddField(efb => efb.WithName("IonDriver").WithValue("⚠ Unknown").WithIsInline(true));
            }

            return embed;
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

        public bool IsMarker()
        {
            return IsMatesFamilyMarker() || IsKebbzNetMarker() || IsIonDriverMarker();
        }

        public EmbedBuilder GetEmbed(int idx, int total)
        {
            string footer = $"[{idx}/{total}] ({m}.bzn)";
            if (pong != null && pong.CompressedData != null && pong.CompressedData.Mods.Length > 0)
            {
                footer += "\n" + Format.Sanitize(pong.CompressedData.Mods);
            }

            EmbedBuilder embed = new EmbedBuilder()
                .WithDescription(ToString())
                .WithFooter(efb => efb.WithText(footer));

            string prop = Battlezone.GetBZ2GameProperty("shell", Format.Sanitize(m));
            embed.WithThumbnailUrl(prop ?? "http://vignette1.wikia.nocookie.net/battlezone/images/e/ef/Nomapbz1.png/revision/latest/scale-to-width-down/80");

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
            else if(t == "5" && pong == null)
            {
                embed.WithColor(new Color(0xff, 0xff, 0x00))
                     .WithTitle("⚠ " + Format.Sanitize(n) + playerCountData);
            }
            else
            {
                embed.WithOkColor()
                     .WithTitle(Format.Sanitize(n) + playerCountData);
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

        public override string ToString()
        {
            string name = Battlezone.GetBZ2GameProperty("name", m);
            string version = Battlezone.GetBZ2GameProperty("version", v);
            string mod = Battlezone.GetBZ2GameProperty("mod", d);


            StringBuilder builder = new StringBuilder();

            if (string.IsNullOrWhiteSpace(name))
            {
                builder.AppendLine($@"Map     | [{Format.Sanitize(m)}]");
            }
            else
            {
                builder.AppendLine($@"Map     | {Format.Sanitize(name)}");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                builder.AppendLine($@"Version | [{Format.Sanitize(v)}]");
            }
            else
            {
                builder.AppendLine($@"Version | {Format.Sanitize(version)}");
            }

            if (string.IsNullOrWhiteSpace(mod))
            {
                builder.AppendLine($@"Mod     | [{Format.Sanitize(d)}]");
            }
            else
            {
                builder.AppendLine($@"Mod     | {Format.Sanitize(mod)}");
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
                        }
                        builder.AppendLine(@"Type    | DM");
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
                        builder.AppendLine($"Time    | lobby ({pong.GameTimeMinutes} minutes)");
                        break;
                    case 3:
                        builder.AppendLine($"Time    | game ({pong.GameTimeMinutes} minutes)");
                        break;
                }

                builder.AppendLine($"TPS     | {pong.TPS}");

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

            Players.ForEach(dr =>
            {
                k = Math.Max(k, dr.Kills.ToString().Length);
                d = Math.Max(d, dr.Deaths.ToString().Length);
                s = Math.Max(s, dr.Score.ToString().Length);
            });

            Players.ForEach(dr =>
            {
                builder.AppendLine($"`{dr.Kills.ToString().PadLeft(k)}/{dr.Deaths.ToString().PadLeft(d)}/{dr.Score.ToString().PadLeft(s)}` {Format.Sanitize(dr.UserName)}");
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
