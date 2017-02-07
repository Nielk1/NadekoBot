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
                .WithDescription("List of games currently on Battlezone II Raknet matchmaking servers")
                .WithThumbnailUrl("http://vignette2.wikia.nocookie.net/battlezone/images/4/41/Chronicles_icon.png/revision/latest/scale-to-width-down/80")
                .WithFooter(efb => efb.WithText("Brought to you by Nielk1's Raknet Bot").WithIconUrl("http://vignette2.wikia.nocookie.net/battlezone/images/4/41/Chronicles_icon.png/revision/latest/scale-to-width-down/32"));

            if (isMatesFamily)
            {
                embed.AddField(efb => efb.WithName("MatesFamily").WithValue("✅ Online (Primary)").WithIsInline(true));
            }else
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

    public class BZ2Game
    {
        public string __addr { get; set; }
        public string proxySource { get; set; }

        public string n { get; set; } // varchar(256) | Name of client game session.
        public string m { get; set; } // varchar(68)  | Name of client map, no bzn extension.
        public string k { get; set; } // tinyint      | Password Flag.
        public string d { get; set; } // varchar(16)  | MODSLISTCRC_KEY
        public string t { get; set; } // tinyint      | NATTYPE_KEY
        public string r { get; set; } // varchar(16)  | PRIVATEADDRESS_KEY
        public string v { get; set; } // varchar(8)   | GAMEVERSION_KEY
        public string p { get; set; } // varchar(16)  | GAMEPORT_KEY
        public string l { get; set; }

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

            if (k == "1")
            {
                embed.WithColor(new Color(0xffac33))
                     .WithTitle("⛔ " + n);
            }
            if (k == "2")
            {
                embed.WithColor(new Color(0xbe1931))
                     .WithTitle("🔐 " + n);
            }

            return embed;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(@"```");
            builder.AppendLine(@"Map:     {" + m + ".bzn}");
            builder.AppendLine(@"Version: {" + v + "}");
            builder.AppendLine(@"Mod:     {" + d + "}");
            builder.AppendLine(@"NAT:     {" + t + "}");
            switch (proxySource)
            {
                case "masterserver.matesfamily.org":
                    builder.AppendLine(@"List:    MatesFamily");
                    break;
                case "gamelist.kebbz.com":
                    builder.AppendLine(@"List:    KebbzNet");
                    break;
                default:
                    builder.AppendLine(@"List:    IonDriver");
                    break;
            }
            builder.AppendLine(@"```");
            return builder.ToString();
        }
    }
}
