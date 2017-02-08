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
        private const string queryUrl = "http://raknetsrv2.iondriver.com/testServer?__gameId=BZ2&__excludeCols=__rowId,__city,__cityLon,__cityLat,__timeoutSec,__geoIP,__gameId&__pluginShowSource=true";

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
                .WithDescription($"List of games currently on Battlezone II Raknet matchmaking servers\n`{GET.Where(game => !game.IsMarker()).Count()} Game(s)`")
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
            EmbedBuilder embed = new EmbedBuilder()
                .WithDescription(ToString())
                .WithFooter(efb => efb.WithText($"[{idx}/{total}] ({m}.bzn)"));

            string prop = Battlezone.GetBZ2GameProperty("shell", m);
            embed.WithThumbnailUrl(prop ?? "http://vignette1.wikia.nocookie.net/battlezone/images/e/ef/Nomapbz1.png/revision/latest/scale-to-width-down/80");

            if (l == "1")
            {
                embed.WithColor(new Color(0xbe, 0x19, 0x31))
                     .WithTitle("⛔ " + n);
            }
            else if (k == "1")
            {
                embed.WithColor(new Color(0xff, 0xac, 0x33))
                     .WithTitle("🔐 " + n);
            }
            //else if(t == "5" && !string.IsNullOrWhiteSpace(r))
            //{
            //    embed.WithColor(new Color(0xff, 0xff, 0x00))
            //         .WithTitle("⚠ " + n);
            //}
            else
            {
                embed.WithOkColor()
                     .WithTitle(n);
            }

            return embed;
        }

        public override string ToString()
        {
            string name = Battlezone.GetBZ2GameProperty("name", m);
            string version = Battlezone.GetBZ2GameProperty("version", m);
            string mod = Battlezone.GetBZ2GameProperty("mod", m);


            StringBuilder builder = new StringBuilder();

            if (string.IsNullOrWhiteSpace(name))
            {
                builder.AppendLine($@"Map      | [{m}]");
            }
            else
            {
                builder.AppendLine($@"Map      | {name}");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                builder.AppendLine($@"Version  | [{v}]");
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

            return Format.Code(builder.ToString(), "css");
        }
    }
}
