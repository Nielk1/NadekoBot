using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Http;
using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;

namespace NadekoBot.Core.Services
{
    public class SteamService : INService
    {
        private readonly IBotCredentials _creds;
        private readonly IBotConfigProvider _config;

        /// <summary>
        /// Workshop name cache
        /// </summary>
        private ConcurrentDictionary<string, Tuple<DateTime, string>> steamWorkshopNameCache;

        /// <summary>
        /// Steam PlayerSummary cache
        /// </summary>
        static ConcurrentDictionary<ulong, Tuple<DateTime, PlayerSummaryModel>> steamPlayerCache = new ConcurrentDictionary<ulong, Tuple<DateTime, PlayerSummaryModel>>();

        /// <summary>
        /// SteamUser WebAPI Interface
        /// </summary>
        static SteamUser steamInterface;

        public SteamService(IBotCredentials creds, IBotConfigProvider config)
        {
            _creds = creds;
            _config = config;
            steamWorkshopNameCache = new ConcurrentDictionary<string, Tuple<DateTime, string>>();

            // Build SteamUser SteamWebAPI interface
            if (!string.IsNullOrWhiteSpace(_creds.SteamApiKey))
                steamInterface = new SteamUser(_creds.SteamApiKey);
        }

        public async Task<PlayerSummaryModel> GetSteamUserData(ulong id)
        {
            if (steamInterface == null) return null;
            Tuple<DateTime, PlayerSummaryModel> newPlayerData = null;
            if (steamPlayerCache.TryGetValue(id, out newPlayerData))
            {
                if (newPlayerData.Item1 > DateTime.UtcNow)
                {
                    newPlayerData = null;
                }
            }
            if (newPlayerData == null)
            {
                ISteamWebResponse<PlayerSummaryModel> playerData = await Task.Run(async () =>
                {
                    ISteamWebResponse<PlayerSummaryModel> msg = await steamInterface.GetPlayerSummaryAsync(id);
                    return msg;
                });

                if (playerData != null)
                {
                    newPlayerData = new Tuple<DateTime, PlayerSummaryModel>(DateTime.UtcNow.AddHours(1), playerData.Data);

                    steamPlayerCache.AddOrUpdate(id, newPlayerData,
                        (key, existingVal) =>
                        {
                            return newPlayerData;
                        });
                }
            }
            return newPlayerData?.Item2;
        }

        public async Task<string> GetSteamWorkshopName(string workshopId)
        {
            if (string.IsNullOrWhiteSpace(workshopId)) return null;

            Tuple<DateTime, string> newWorkshopName = null;
            if (steamWorkshopNameCache.TryGetValue(workshopId, out newWorkshopName))
            {
                if (newWorkshopName.Item1 > DateTime.UtcNow)
                {
                    newWorkshopName = null;
                }
            }

            if (newWorkshopName == null)
            {
                try
                {
                    using (var http = new HttpClient())
                    {
                        var reqString = $"http://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}";
                        var rawText = (await http.GetStringAsync(reqString).ConfigureAwait(false));

                        var matches = System.Text.RegularExpressions.Regex.Matches(rawText, "<\\s*div\\s+class\\s*=\\s*\"workshopItemTitle\"\\s*>(.*)<\\s*/\\s*div\\s*>");
                        string found = null;
                        if (matches.Count > 0)
                        {
                            if (matches[0].Groups.Count > 1)
                            {
                                found = matches[0].Groups[1].Value.Trim();
                            }
                        }

                        newWorkshopName = new Tuple<DateTime, string>(DateTime.UtcNow.AddHours(24), found);
                        steamWorkshopNameCache.AddOrUpdate(workshopId, newWorkshopName,
                         (key, existingVal) =>
                         {
                             return newWorkshopName;
                         });
                    }
                }
                catch (Exception ex)
                {
                    //_log.Warn(ex, "Steam Workshop scan failed");
                }
            }

            return newWorkshopName.Item2;
        }

    }
}
