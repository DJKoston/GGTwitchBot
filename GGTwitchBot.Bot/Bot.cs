using GGTwitchBot.Core.Services;
using GGTwitchBot.DAL.Models;
using Newtonsoft.Json.Linq;
using PokeApiNet;
using System.Diagnostics.Eventing.Reader;
using TwitchLib.Api;
using SimplifiedSearch;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Api.Services;
using System.Timers;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;

namespace GGTwitchBot.Bot
{
    public class Bot
    {
        PokeApiClient pokeClient;
        TwitchAPI TwitchAPI;
        TwitchClient GGTwitch;
        LiveStreamMonitorService Monitor;

        List<PCG> allPokemon;

        public ConsoleColor twitchColor;
        public ConsoleColor fail;

        public PCG pcgSpawn = null;
        public bool newPCGSpawn = false;

        public string pokeName = null;
        public bool pokeNameSet = false;

        public string pokeBotUsername = "pokemoncommunitygame";

        public string environmentName = null;

        public int rawrCount = 0;

        public bool newQuickBallTimer = true;
        public bool newTimerBallTimer = true;
        public System.Timers.Timer quickBallTimer = new();
        public System.Timers.Timer timerBallTimer = new();

        public Bot(IServiceProvider services, IConfiguration configuration)
        {
            environmentName = "Beta";

            if(environmentName =="Development") 
            {
                pokeBotUsername = "djkoston";
            }

            twitchColor = ConsoleColor.DarkMagenta;
            fail = ConsoleColor.Red;

            var botVersion = typeof(Bot).Assembly.GetName().Version.ToString();
            Log("-----------------------------");
            Log("Logging Started.");
            Log($"Bot Version: {botVersion}");
            Log($"Environment Name: {environmentName}");

            Log("Starting PokeApi Client...");
            pokeClient = new PokeApiClient();
            Log("New PokeApi Client Created...");

            Log("Loading Core Services...");
            _streamService = services.GetService<IStreamerService>();
            _pokecatchService = services.GetService<IPokecatchService>();
            _pcgService = services.GetService<IPCGService>();
            Log("Loaded Core Services.");

            Log("Creating Twitch API Access...");
            TwitchAPI = new TwitchAPI();

            TwitchAPI.Settings.ClientId = configuration["twitch-clientid"];
            TwitchAPI.Settings.AccessToken = configuration["twitch-accesstoken"];
            
            Log("Twitch API Access Created.");

            Log("Creating Bot Credentials...");
            ConnectionCredentials creds1 = new ConnectionCredentials(configuration["ggusername"], configuration["ggaccesstoken"]);
            Log("Created Bot Credentials...");

            Log("Creating Client Options...");
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
            };
            Log("Created Client Options...");

            Log("Creating WebSocket Client...");
            WebSocketClient webSocketClient1 = new(clientOptions);
            Log("Created WebSocket Client...");

            Log("Creating Twitch Client...", twitchColor);
            GGTwitch = new TwitchClient(webSocketClient1);
            Log("Created Twitch Client...", twitchColor);

            Log("Initialising Bot...");
            GGTwitch.Initialize(creds1, "generationgamersttv");
            GGTwitch.AddChatCommandIdentifier('!');
            Log("Bot Initialised...");

            Log("Starting Twitch Live Monitor...");
            Monitor = new(TwitchAPI, 60);
            Monitor.OnStreamOnline += OnStreamOnline;
            Monitor.OnStreamOffline += OnStreamOffline;

            var lst = new List<string>();

            if(environmentName == "Beta")
            {
                lst = _streamService.GetStreamsToMonitor(true);
            }
            if(environmentName == "Live")
            {
                lst = _streamService.GetStreamsToMonitor(false);
            }

            if(lst.Count != 0)
            {
                Monitor.SetChannelsByName(lst);
                Monitor.Start();
                if(Monitor.Enabled)
                {
                    Log($"Live Monitoring has started. Monitoring {lst.Count} channels.", twitchColor);
                    foreach (var channel in lst)
                    {
                        Log($"Monitoring: {channel}");
                    }
                }
                else
                {
                    Log("Live Monitor has failed to start.", fail);
                }
            }
            else
            {
                Log("No Channels have been submitted to the monitor.", fail);
            }

            Log("Subscribing to GG Client Events...");
            GGTwitch.OnConnected += OnGGClientConnected;
            GGTwitch.OnJoinedChannel += OnJoinedChannel;
            GGTwitch.OnMessageReceived += OnMessageReceived;
            GGTwitch.OnChatCommandReceived += OnCommandReceived;
            GGTwitch.OnLeftChannel += OnLeftChannel;
            Log("Subscribed to GG Client Events...");

            Log("Connecting to Twitch", twitchColor);
            GGTwitch.Connect();
            Log("Connected to Twitch!", twitchColor);
            
            if(environmentName == "Beta") { Log("GG-Bot Beta is Ready.", ConsoleColor.Green); }
            else if(environmentName == "Production") { Log("GG-Bot is Ready.", ConsoleColor.Green); }
            else if(environmentName == "Development") { Log("GG-Bot Dev is Ready.", ConsoleColor.Green); }
        }

        private readonly IStreamerService _streamService;
        private readonly IPokecatchService _pokecatchService;
        private readonly IPCGService _pcgService;

        private async void OnCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            var userName = e.Command.ChatMessage.Username;
            var userDisplayName = e.Command.ChatMessage.DisplayName;
            var streamerUserName = e.Command.ChatMessage.Channel.ToLower();
            var isMod = e.Command.ChatMessage.IsModerator;
            var isBroadcaster = e.Command.ChatMessage.IsBroadcaster;
            var isSub = e.Command.ChatMessage.IsSubscriber;
            var command = e.Command.CommandText.ToLower();
            var argumentsAsList = e.Command.ArgumentsAsList;
            var argumentsCount = argumentsAsList.Count();
            var argumentsAsString = e.Command.ArgumentsAsString;
            var targetUserName = e.Command.ArgumentsAsString.ToLower().Replace("@", "");

            //Whitelist
            var whitelistFile = File.ReadAllLines("/home/container/whitelist.txt");
            var whitelist = new List<string>(whitelistFile);

            if (command == "ggcommands")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                var commandList = "GG-Bot Commands: !affirmation, !dadjoke, !flipacoin, !ping";
                var modCommandList = "GG-Bot Commands: !affirmation, !dadjoke, !flipacoin, !ggleave, !ping";
                var ownerCommandList = "GG-Bot Commands: !affirmation, !announce, !betaannounce, !dadjoke, !flipacoin, !ggleave, !ping";

                if (userName == "djkoston")
                {
                    GGSendMessage(streamerUserName, ownerCommandList);
                }

                else if (isMod || isBroadcaster)
                {
                    GGSendMessage(streamerUserName, modCommandList);

                    return;
                }

                else
                {
                    GGSendMessage(streamerUserName, commandList);

                    return;
                }
            }
            if (command == "affirmation")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                HttpClient client = new();

                using (Stream dataStream = await client.GetStreamAsync("https://api.koston.eu/affirmation"))
                {
                    StreamReader reader = new(dataStream);

                    string responseFromServer = reader.ReadToEnd();

                    GGSendMessage(streamerUserName, $"Cute and Positive Affirmation: {responseFromServer}");
                }

                client.Dispose();

                return;
            }
            if (command == "announce" && userName == "djkoston" && streamerUserName == "generationgamersttv")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                var channels = GGTwitch.JoinedChannels;

                foreach (var channel in channels)
                {
                    GGSendMessage(channel.Channel, argumentsAsString);
                    Log($"Announcement sent to: {channel.Channel}");
                }

                return;
            }
            if (command == "betaannounce" && userName == "djkoston" && streamerUserName == "generationgamersttv" && environmentName == "Beta")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                var botVersion = typeof(Bot).Assembly.GetName().Version.ToString();
                var arguments = argumentsAsString.Replace("$version", $"(Version: {botVersion})");

                var channels = GGTwitch.JoinedChannels;

                foreach (var channel in channels)
                {
                    GGSendMessage(channel.Channel, arguments);
                    Log($"Announcement sent to: {channel.Channel}");
                }

                return;
            }
            if (command == "dadjoke")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                GGSendMessage(streamerUserName, "djkostWarning DAD JOKE ALERT djkostWarning");

                HttpClient client = new();

                using (Stream dataStream = await client.GetStreamAsync("https://api.scorpstuff.com/dadjokes.php"))
                {
                    StreamReader reader = new(dataStream);

                    string responseFromServer = reader.ReadToEnd();

                    GGSendMessage(streamerUserName, responseFromServer);
                }

                client.Dispose();

                return;
            }
            if (command == "flipacoin")
            {
                var resultString = String.Empty;

                var resultNumber = new Random().Next(1, 3);

                if(resultNumber == 1)
                {
                    resultString = "Tails";
                }
                else if(resultNumber == 2)
                {
                    resultString = "Heads";
                }

                GGSendMessage(streamerUserName, $"Everybody looks in awe as the coin does several flips in the air before it rests on @{userDisplayName}'s hand. The crowd stands and waits as the result is called out: It's {resultString}");
            }
            if (command == "ggjoin" && streamerUserName == "generationgamersttv" && userName == "generationgamerttv" && environmentName != "Beta")
            {
                if (argumentsCount == 1)
                {
                    GGTwitch.JoinChannel(targetUserName);

                    var lst = _streamService.GetStreamsToMonitor(true);

                    Monitor.SetChannelsByName(lst);
                }
            }
            if (command == "ggleave")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if(userName == "generationgamersttv" && streamerUserName == "generationgamersttv" && environmentName != "Beta")
                {
                    GGTwitch.LeaveChannel(targetUserName);

                    return;
                }

                if (userName != "generationgamersttv" && streamerUserName == "generationgamersttv" && environmentName != "Beta")
                {
                    GGSendMessage(streamerUserName, "To get the bot to leave your channel, run this command in your own chat.");

                    return;
                }

                else if (isMod || isBroadcaster || userName == "djkoston")
                {
                    GGSendMessage(streamerUserName, $"Hi there @{userDisplayName}, I am now leaving this channel! If you want me to join again, just type !join in my Twitch Chat.");

                    _streamService.DeleteStreamAsync(streamerUserName);
                    GGTwitch.LeaveChannel(streamerUserName);

                    return;
                }
            }
            if (command == "join" && streamerUserName == "generationgamersttv" && environmentName != "Beta")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    var isStream = _streamService.GetAllStreams().FirstOrDefault(x => x.StreamerUsername == userName);

                    if (whitelist.Contains(userName))
                    {
                        if (isStream == null)
                        {
                            _streamService.NewStream(userName);
                            GGTwitch.JoinChannel(userName);

                            GGSendMessage(streamerUserName, $"Hi there @{userDisplayName}, I am now connected to your channel! If you want me to leave, just type !ggleave in your channel.");
                            GGSendMessage(userName, $"Hi there @{userDisplayName}, Just wanted to let you know, i'm here and waiting <3");

                            var lst = _streamService.GetStreamsToMonitor(false);

                            Monitor.SetChannelsByName(lst);

                            return;
                        }

                        else
                        {
                            GGSendMessage(streamerUserName, "You already have GG-Bot in your channel. If you are experiencing issues, use !rejoin");

                            return;
                        }
                    }

                    else
                    {
                        if(isStream == null)
                        {
                            GGSendMessage(streamerUserName, "You are not currently whitelisted. Please contact DJKoston#0001 on Discord to be whitelisted.");

                            return;
                        }

                        else
                        {
                            GGSendMessage(streamerUserName, "You already have GG-Bot in your channel. You are not on our whitelist so please contact DJKoston#0001 on Discord to be whitelisted then use !rejoin");

                            return;
                        }
                    }
                }

                else if (argumentsCount == 1)
                {
                    var isStream = _streamService.GetAllStreams().FirstOrDefault(x => x.StreamerUsername == targetUserName);

                    if (whitelist.Contains(targetUserName))
                    {
                        if (isStream == null)
                        { 
                            _streamService.NewStream(targetUserName.ToLower());
                            GGTwitch.JoinChannel(targetUserName);

                            GGSendMessage(streamerUserName, $"Hi there @{userDisplayName}, I am now connected to {targetUserName}! If you want me to leave, just type !ggleave in their chat.");
                            GGSendMessage(targetUserName, $"Hi there @{targetUserName}, {userDisplayName} just added me to your channel, If you don't want me here, you or a mod can do !ggleave and i'll go away. <3");

                            return;
                        }

                        else
                        {
                            GGSendMessage(streamerUserName, "GG-Bot is already in this channel. If they are experiencing issues, use !rejoin @username");

                            return;
                        }
                    }

                    else
                    {
                        if (isStream == null)
                        {
                            GGSendMessage(streamerUserName, "The channel you are trying to add is not currently whitelisted. Please contact DJKoston#0001 on Discord to be whitelisted.");

                            return;
                        }

                        else
                        {
                            GGSendMessage(streamerUserName, $"This channel already has GG-Bot. They are not on our whitelist so please contact DJKoston#0001 on Discord to be whitelisted then use !rejoin @{targetUserName}");

                            return;
                        }
                    }
                }
            }
            if (command == "joinbeta" && (userName == "djkoston" || userName == "tactlessturtleplays" || userName == "wolfyeon") && streamerUserName == "generationgamersttv" && environmentName == "Beta")
            {
                if (argumentsCount == 1)
                {
                    var stream = _streamService.GetAllStreams().FirstOrDefault(x => x.StreamerUsername == targetUserName);

                    if (whitelist.Contains(targetUserName))
                    {
                        if (stream == null)
                        {
                            _streamService.NewBetaStream(targetUserName);
                            GGTwitch.JoinChannel(targetUserName);
                            GGSendMessage(streamerUserName, $"!ggleave {targetUserName}");

                            GGSendMessage(streamerUserName, $"Added {targetUserName} to the beta.");
                        }

                        else
                        {
                            _streamService.AddUserToBeta(targetUserName);

                            GGTwitch.JoinChannel(targetUserName);
                            GGSendMessage(streamerUserName, $"!ggleave {targetUserName}");

                            GGSendMessage(streamerUserName, $"Added {targetUserName} to the beta.");
                        }

                        var lst = _streamService.GetStreamsToMonitor(true);

                        Monitor.SetChannelsByName(lst);
                    }
                    else
                    {
                        GGSendMessage(streamerUserName, "Target is not whitelisted, please add them to the whitelist to add them to the beta.");
                    }
                }
                else if(argumentsCount == 0)
                {
                    GGSendMessage(streamerUserName, "You are already a part of the beta.");
                }
            }
            if (command == "leavebeta" && (userName == "djkoston" || userName == "tactlessturtleplays" || userName == "wolfyeon") && streamerUserName == "generationgamersttv" && environmentName == "Beta")
            {
                if (argumentsCount == 1)
                {
                    var stream = _streamService.GetAllStreams().FirstOrDefault(x => x.StreamerUsername == targetUserName);

                    if (stream.BetaTester == true)
                    {
                        _streamService.RemoveUserFromBeta(targetUserName);

                        GGTwitch.LeaveChannel(targetUserName);
                        GGSendMessage(streamerUserName, $"!ggjoin {targetUserName}");

                        GGSendMessage(streamerUserName, $"Removed {targetUserName} from the beta.");

                        var lst = _streamService.GetStreamsToMonitor(true);

                        Monitor.SetChannelsByName(lst);
                    }
                    else
                    {
                        GGSendMessage(streamerUserName, "Target is not whitelisted, please add them to the whitelist to add them to the beta.");
                    }
                }
                else if (argumentsCount == 0)
                {
                    GGSendMessage(streamerUserName, "You are already a part of the beta.");
                }
            }
            if (command == "ping")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                GGSendMessage(streamerUserName, "<3 I am Groo... I mean, I am here! <3");

                return;
            }

            //PCG Based Commands

            if (command == "pokecatch")
            {
                var pokeCheck = _pokecatchService.GetPokecatchersListAsync(streamerUserName);

                if (!pokeCheck.Contains(userName))
                {
                    await _pokecatchService.AddCatchAsync(streamerUserName, userName);
                    Log($"{userDisplayName} tried to catch a pokemon in {streamerUserName}");
                }
                
                return;
            }
            if ((command == "spawned" || command == "lastspawn") && environmentName == "Beta")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                HttpClient client = new();

                if (pcgSpawn == null)
                {
                    using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                    {
                        StreamReader reader = new(dataStream);

                        string responseFromServer = reader.ReadToEnd();

                        var pcgAPI = JObject.Parse(responseFromServer);
                        var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                        var dexNumber = pcgSpawnDex.ToString("000");

                        pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                        if (pcgSpawn == null)
                        {
                            if (pokeName == null)
                            {
                                GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                return;
                            }
                            else
                            {
                                pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                            }
                        }
                    }
                }

                GGSendMessage(streamerUserName, $"[#{pcgSpawn.DexNumber} {pcgSpawn.Name}] -> [Type] {pcgSpawn.Type} [Tier] {pcgSpawn.Tier} [Gen] {pcgSpawn.Generation} [Dex] {pcgSpawn.DexInfo} [Ball] {pcgSpawn.SuggestedBalls} [BST] {pcgSpawn.BST}");

                client.Dispose();

                return;
            }
            if (command == "dex" && environmentName == "Development")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    HttpClient client = new();

                    if (pcgSpawn == null)
                    {
                        using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                        {
                            StreamReader reader = new(dataStream);

                            string responseFromServer = reader.ReadToEnd();

                            var pcgAPI = JObject.Parse(responseFromServer);
                            var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                            var dexNumber = pcgSpawnDex.ToString("000");

                            pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                            if (pcgSpawn == null)
                            {
                                if (pokeName == null)
                                {
                                    GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                    return;
                                }
                                else
                                {
                                    pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                                }
                            }
                        }
                    }

                    GGSendMessage(streamerUserName, $"[#{pcgSpawn.DexNumber} {pcgSpawn.Name} - {pcgSpawn.Tier} Tier] -> [Type] {pcgSpawn.Type} [Gen] {pcgSpawn.Generation} [Dex] {pcgSpawn.DexInfo} [Ball] {pcgSpawn.SuggestedBalls} [BST] {pcgSpawn.BST}");

                }

                else
                {
                    var argumentIsNumber = Int32.TryParse(argumentsAsString, out int result);

                    IList<PCG> searchedMon = null;
                    PCG matchedMon = null;

                    if (!argumentIsNumber)
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(argumentsAsString, x => x.Name);
                    }

                    else
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(result.ToString("000"), x => x.DexNumber);
                    }

                    matchedMon = searchedMon.FirstOrDefault();

                    GGSendMessage(streamerUserName, $"[#{matchedMon.DexNumber} {matchedMon.Name} - {matchedMon.Tier} Tier] -> [Type] {matchedMon.Type} [Gen] {matchedMon.Generation} [Dex] {matchedMon.DexInfo} [Ball] {matchedMon.SuggestedBalls} [BST] {matchedMon.BST}");
                }
            }
            if (command == "tier" && environmentName == "Development")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    HttpClient client = new();

                    if (pcgSpawn == null)
                    {
                        using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                        {
                            StreamReader reader = new(dataStream);

                            string responseFromServer = reader.ReadToEnd();

                            var pcgAPI = JObject.Parse(responseFromServer);
                            var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                            var dexNumber = pcgSpawnDex.ToString("000");

                            pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                            if (pcgSpawn == null)
                            {
                                if (pokeName == null)
                                {
                                    GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                    return;
                                }
                                else
                                {
                                    pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                                }
                            }
                        }
                    }

                    GGSendMessage(streamerUserName, $"#{pcgSpawn.DexNumber} {pcgSpawn.Name} is an {pcgSpawn.Tier} Tier.");

                }

                else
                {
                    var argumentIsNumber = Int32.TryParse(argumentsAsString, out int result);

                    IList<PCG> searchedMon = null;
                    PCG matchedMon = null;

                    if (!argumentIsNumber)
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(argumentsAsString, x => x.Name);
                    }

                    else
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(result.ToString("000"), x => x.DexNumber);
                    }

                    matchedMon = searchedMon.FirstOrDefault();

                    GGSendMessage(streamerUserName, $"#{matchedMon.DexNumber} {matchedMon.Name} is an {matchedMon.Tier} Tier.");
                }
            }
            if (command == "weight" && environmentName == "Development")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    HttpClient client = new();

                    if (pcgSpawn == null)
                    {
                        using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                        {
                            StreamReader reader = new(dataStream);

                            string responseFromServer = reader.ReadToEnd();

                            var pcgAPI = JObject.Parse(responseFromServer);
                            var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                            var dexNumber = pcgSpawnDex.ToString("000");

                            pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                            if (pcgSpawn == null)
                            {
                                if (pokeName == null)
                                {
                                    GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                    return;
                                }
                                else
                                {
                                    pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                                }
                            }
                        }
                    }

                    GGSendMessage(streamerUserName, $"#{pcgSpawn.DexNumber} {pcgSpawn.Name} weighs {pcgSpawn.Weight}.");

                }

                else
                {
                    var argumentIsNumber = Int32.TryParse(argumentsAsString, out int result);

                    IList<PCG> searchedMon = null;
                    PCG matchedMon = null;

                    if (!argumentIsNumber)
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(argumentsAsString, x => x.Name);
                    }

                    else
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(result.ToString("000"), x => x.DexNumber);
                    }

                    matchedMon = searchedMon.FirstOrDefault();

                    GGSendMessage(streamerUserName, $"#{matchedMon.DexNumber} {matchedMon.Name} weighs {matchedMon.Weight}.");
                }
            }
            if (command == "type" && environmentName == "Development")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    HttpClient client = new();

                    if (pcgSpawn == null)
                    {
                        using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                        {
                            StreamReader reader = new(dataStream);

                            string responseFromServer = reader.ReadToEnd();

                            var pcgAPI = JObject.Parse(responseFromServer);
                            var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                            var dexNumber = pcgSpawnDex.ToString("000");

                            pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                            if (pcgSpawn == null)
                            {
                                if (pokeName == null)
                                {
                                    GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                    return;
                                }
                                else
                                {
                                    pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                                }
                            }
                        }
                    }

                    GGSendMessage(streamerUserName, $"#{pcgSpawn.DexNumber} {pcgSpawn.Name} is a {pcgSpawn.Type} Type Pokemon.");

                }

                else
                {
                    var argumentIsNumber = Int32.TryParse(argumentsAsString, out int result);

                    IList<PCG> searchedMon = null;
                    PCG matchedMon = null;

                    if (!argumentIsNumber)
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(argumentsAsString, x => x.Name);
                    }

                    else
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(result.ToString("000"), x => x.DexNumber);
                    }

                    matchedMon = searchedMon.FirstOrDefault();

                    GGSendMessage(streamerUserName, $"#{matchedMon.DexNumber} {matchedMon.Name} is a {matchedMon.Type} Type.");
                }
            }
            if (command == "ball" && environmentName == "Development")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                if (argumentsCount == 0)
                {
                    HttpClient client = new();

                    if (pcgSpawn == null)
                    {
                        using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                        {
                            StreamReader reader = new(dataStream);

                            string responseFromServer = reader.ReadToEnd();

                            var pcgAPI = JObject.Parse(responseFromServer);
                            var pcgSpawnDex = Convert.ToInt32(pcgAPI["pokedex_id"]);
                            var dexNumber = pcgSpawnDex.ToString("000");

                            pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                            if (pcgSpawn == null)
                            {
                                if (pokeName == null)
                                {
                                    GGSendMessage(streamerUserName, "I haven't tracked a spawn yet. I probably have just restarted.");

                                    return;
                                }
                                else
                                {
                                    pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                                }
                            }
                        }
                    }

                    GGSendMessage(streamerUserName, $"Steve suggests you throw: {pcgSpawn.SuggestedBalls} at #{pcgSpawn.DexNumber} {pcgSpawn.Name}");

                }

                else
                {
                    var argumentIsNumber = Int32.TryParse(argumentsAsString, out int result);

                    IList<PCG> searchedMon = null;
                    PCG matchedMon = null;

                    if (!argumentIsNumber)
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(argumentsAsString, x => x.Name);
                    }

                    else
                    {
                        searchedMon = await allPokemon.SimplifiedSearchAsync(result.ToString("000"), x => x.DexNumber);
                    }

                    matchedMon = searchedMon.FirstOrDefault();

                    GGSendMessage(streamerUserName, $"Steve suggests you throw: {matchedMon.SuggestedBalls} at #{matchedMon.DexNumber} {matchedMon.Name}");
                }
            }
            if (command == "weak" && environmentName == "Development")
            {
                GGSendMessage(streamerUserName, "We are currently working on this feature, and it's not currently available.");
            }

            //Wolfy private commands

            if (command == "rawr")
            {
                Log($"{userDisplayName} used command '{e.Command.CommandText}' in {streamerUserName}");

                rawrCount++;

                GGSendMessage(streamerUserName, $"*rawrs back* - I have rawred {rawrCount} times since I've been alived");
            }
        }

        private async void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.ToLower().Contains("catch it using !pokecatch (winners revealed in 90s)"))
            {
                if (pokeNameSet == false)
                {
                    if (e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3] == "Mr." ||
                        e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3] == "Type:" ||
                        e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3] == "Tapu" ||
                        e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3] == "Mime")
                    {
                        pokeName = $"{e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3]} {e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[4]}";
                    }

                    else
                    {
                        pokeName = e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[3];
                    }
                    pokeNameSet = true;
                }

                await _pokecatchService.RemoveAllCatchesAsync(e.ChatMessage.Channel);
                Log($"Pokecatch started in {e.ChatMessage.Channel}");

                HttpClient client = new();

                if (newPCGSpawn == false)
                {
                    using (Stream dataStream = await client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/"))
                    {
                        StreamReader reader = new(dataStream);

                        string responseFromServer = reader.ReadToEnd();

                        var pcgAPI = JObject.Parse(responseFromServer);
                        var dexNumber = Convert.ToInt32(pcgAPI["pokedex_id"]).ToString("000");

                        pcgSpawn = await _pcgService.GetPokemonByDexNumberAsync(dexNumber);

                        if (pcgSpawn == null)
                        {
                            pcgSpawn = await _pcgService.GetPokemonByNameAsync(pokeName);
                        }
                    }

                    newPCGSpawn = true;
                }

                if (environmentName == "Beta")
                {
                    GGSendMessage(e.ChatMessage.Channel, $"[#{pcgSpawn.DexNumber} {pcgSpawn.Name}] -> [Type] {pcgSpawn.Type} [Tier] {pcgSpawn.Tier} [Gen] {pcgSpawn.Generation} [Dex] {pcgSpawn.DexInfo} [Ball] {pcgSpawn.SuggestedBalls} [BST] {pcgSpawn.BST}");

                    var weeklyFile = File.ReadAllLines("/home/container/weekly.txt");
                    var weeklys = new List<string>(weeklyFile);
                    weeklys.RemoveRange(0, 13);

                    if (weeklys.Count != 0)
                    {
                        foreach (var weekly in weeklys)
                        {
                            var weeklyCheck = weekly.Split(" ", StringSplitOptions.None).ToList<string>();
                            var category = weeklyCheck[0];

                            if (category.ToLower() == "types" && pcgSpawn.Type.Contains(weeklyCheck[2]))
                            {
                                GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob Weekly Mon! - {weeklyCheck[1]} {weeklyCheck[2]} Types! djkostRGBBlob");
                            }

                            if (category.ToLower() == "bst")
                            {
                                var bstNumber = Convert.ToInt32(weeklyCheck[3]);

                                if (weeklyCheck[2] == ">" && pcgSpawn.BST >= bstNumber)
                                {
                                    GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob Weekly Mon! - {weeklyCheck[1]} Pokemon with {bstNumber} BST or Higher! djkostRGBBlob");
                                }

                                if (weeklyCheck[2] == "<" && pcgSpawn.BST <= bstNumber)
                                {
                                    GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob Weekly Mon! - {weeklyCheck[1]} Pokemon with {bstNumber} BST or Lower! djkostRGBBlob");
                                }
                            }

                            if (category.ToLower() == "weight")
                            {
                                var weightNumber = Convert.ToDouble(weeklyCheck[3]);
                                var pcgWeight = Convert.ToDouble(pcgSpawn.Weight.Replace("Lbs", ""));

                                if (weeklyCheck[2] == ">" && pcgWeight > weightNumber)
                                {
                                    GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob Weekly Mon! - {weeklyCheck[1]} Pokemon Heavier than {weightNumber}lbs! djkostRGBBlob");
                                }

                                if (weeklyCheck[2] == "<" && pcgWeight < weightNumber)
                                {
                                    GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob Weekly Mon! - {weeklyCheck[1]} Pokemon Lighter than {weightNumber}lbs! djkostRGBBlob");
                                }
                            }
                        }
                    }

                    if (pcgSpawn.Tier == "A" || pcgSpawn.Tier == "S")
                    {
                        GGSendMessage(e.ChatMessage.Channel, $"djkostRGBBlob djkostRGBBlob {pcgSpawn.Tier} Tier Hype!!! djkostRGBBlob djkostRGBBlob");
                    }

                    client.Dispose();

                    return;
                }

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.ToLower().Contains("you don't own that ball. check the extension to see your items"))
            {
                var username = e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[0].Replace("@", "");

                await _pokecatchService.RemoveCatchAsync(e.ChatMessage.Channel, username);
                Log($"{username}'s ball was returned to them in {e.ChatMessage.Channel}");

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.ToLower().Contains("oops, the pokémon already escaped!"))
            {
                var username = e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[0].Replace("@", "");

                await _pokecatchService.RemoveCatchAsync(e.ChatMessage.Channel, username);
                Log($"{username}'s ball was returned to them in {e.ChatMessage.Channel}");

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.Contains("ball type doesn't exist"))
            {
                var username = e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[0].Replace("@", "");

                await _pokecatchService.RemoveCatchAsync(e.ChatMessage.Channel, username);
                Log($"{username}'s ball was returned to them in {e.ChatMessage.Channel}");

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.ToLower().Contains("has been caught by:"))
            {
                var messageParse1 = e.ChatMessage.Message.Replace($"{pcgSpawn.Name} ", "").Replace(" (SHINY✨)", "").Replace(" (🪨)", "");
                var messageParse3 = messageParse1.Replace("has been caught by: ", "");

                var catchList = messageParse3.Split(", ").ToList();
                var throwersList = _pokecatchService.GetPokecatchersListAsync(e.ChatMessage.Channel);
                var catchersCount = await _pokecatchService.GetPokecatchersCountAsync(e.ChatMessage.Channel);

                var misserList = throwersList.Except(catchList).ToList();

                GGSendMessage(e.ChatMessage.Channel, $"{catchList.Count}/{catchersCount} people caught the {pcgSpawn.Name} this time around!");
                Log($"Pokecatch ended in {e.ChatMessage.Channel}, {catchList.Count}/{catchersCount} people caught the {pcgSpawn.Name}!");

                if (catchList.Count < catchersCount)
                {
                    var missers = String.Join(", ", misserList);

                    GGSendMessage(e.ChatMessage.Channel, $"Sorry to the following throwers who didn't catch the {pcgSpawn.Name}: {missers}");
                }

                if (catchList.Count == catchersCount && catchersCount > 2)
                {
                    await Task.Delay(500);
                    GGSendMessage(e.ChatMessage.Channel, "FULL CATCH LIST HYPE");
                    await Task.Delay(500);
                    GGSendMessage(e.ChatMessage.Channel, "FULL CATCH LIST HYPE");
                    await Task.Delay(500);
                    GGSendMessage(e.ChatMessage.Channel, "FULL CATCH LIST HYPE");
                    Log($"Pokecatch ended in {e.ChatMessage.Channel}, {catchList.Count}/{catchersCount} people caught the {pcgSpawn.Name}!");
                }

                await _pokecatchService.RemoveAllCatchesAsync(e.ChatMessage.Channel);
                pokeNameSet = false;
                newPCGSpawn = false;

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.Contains("escaped. No one caught it."))
            {
                var catchersCount = await _pokecatchService.GetPokecatchersCountAsync(e.ChatMessage.Channel);

                if(catchersCount == 0)
                {
                    GGSendMessage(e.ChatMessage.Channel, $"Unfortunately nobody attempted to catch the {pcgSpawn.Name} this time around :(");
                    Log($"Pokecatch ended in {e.ChatMessage.Channel}, nobody tried to catch the {pcgSpawn.Name}!");
                }

                else
                {
                    GGSendMessage(e.ChatMessage.Channel, $"Unfortunately 0/{catchersCount} people caught the {pcgSpawn.Name} this time around :(");
                    Log($"Pokecatch ended in {e.ChatMessage.Channel}, 0/{catchersCount} people caught the {pcgSpawn.Name}!");
                }

                List<string> catcherListDb = _pokecatchService.GetPokecatchersListAsync(e.ChatMessage.Channel);

                var missers = String.Join(", ", catcherListDb);

                if (missers.Count() != 0)
                {
                    GGSendMessage(e.ChatMessage.Channel, $"Sorry to everyone who didn't catch the {pcgSpawn.Name}: {missers}");
                }

                await _pokecatchService.RemoveAllCatchesAsync(e.ChatMessage.Channel);
                pokeNameSet = false;
                newPCGSpawn = false;

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.Contains("An unknown error occured while executing the command. Please try again in a few seconds."))
            {
                var username = e.ChatMessage.Message.Split(" ", StringSplitOptions.None)[0].Replace("@", "");

                await _pokecatchService.RemoveCatchAsync(e.ChatMessage.Channel, username);
                Log($"{username}'s ball was returned to them in {e.ChatMessage.Channel}");

                return;
            }

            if (e.ChatMessage.Username == pokeBotUsername && e.ChatMessage.Message.Contains("National-Dex:") && e.ChatMessage.Message.Contains("Spawnable-Dex:"))
            {
                var message = e.ChatMessage.Message.Split(" ").ToList();

                var nationalDex = new List<string>();
                var spawnableDex = new List<string>();
                var nationalPercent = "";
                var spawnablePercent = "";

                int nationalCaught = 0;
                int nationalTotal = 0;
                double nationalCaughtPercent = 0;
                int spawnableCaught = 0;
                int spawnableTotal = 0;
                double spawnableCaughtPercent = 0;

                int nationalLeft = 0;
                int spawnableLeft = 0;
                string nationalPercentLeft = "";
                string spawnablePercentLeft = "";

                if(message.Count == 8)
                {
                    nationalDex = message[2].Split("/").ToList();
                    spawnableDex = message[6].Split("/").ToList();
                    nationalPercent = message[3].Replace("(", "").Replace("%)", "");
                    spawnablePercent = message[7].Replace("(", "").Replace("%)", "");

                    nationalCaught = Int32.Parse(nationalDex[0]);
                    nationalTotal = Int32.Parse(nationalDex[1]);
                    nationalCaughtPercent = Double.Parse(nationalPercent);
                    spawnableCaught = Int32.Parse(spawnableDex[0]);
                    spawnableTotal = Int32.Parse(spawnableDex[1]);
                    spawnableCaughtPercent = Double.Parse(spawnablePercent);

                    nationalLeft = nationalTotal - nationalCaught;
                    spawnableLeft = spawnableTotal - spawnableCaught;
                    nationalPercentLeft = (100 - nationalCaughtPercent).ToString("0.##");
                    spawnablePercentLeft = (100 - spawnableCaughtPercent).ToString("0.##");
                }
                if (message.Count == 7)
                {
                    nationalDex = message[2].Split("/").ToList();
                    spawnableDex = message[5].Split("/").ToList();
                    nationalPercent = message[3].Replace("(", "").Replace("%)", "");
                    spawnablePercent = message[6].Replace("(", "").Replace("%)", "");

                    nationalCaught = Int32.Parse(nationalDex[0]);
                    nationalTotal = Int32.Parse(nationalDex[1]);
                    nationalCaughtPercent = Double.Parse(nationalPercent);
                    spawnableCaught = Int32.Parse(spawnableDex[0]);
                    spawnableTotal = Int32.Parse(spawnableDex[1]);
                    spawnableCaughtPercent = Double.Parse(spawnablePercent);

                    nationalLeft = nationalTotal - nationalCaught;
                    spawnableLeft = spawnableTotal - spawnableCaught;
                    nationalPercentLeft = (100 - nationalCaughtPercent).ToString("0.##");
                    spawnablePercentLeft = (100 - spawnableCaughtPercent).ToString("0.##");
                }

                GGSendMessage(e.ChatMessage.Channel, $"{message[0]} - Remaining National-Dex: {nationalLeft}/{nationalTotal} ({nationalPercentLeft}%) - Remaining Spawnable-Dex: {spawnableLeft}/{spawnableTotal} ({spawnablePercentLeft}%)");
            }

            if (e.ChatMessage.Username == "gordopokebot" && e.ChatMessage.Message == "<3 Connected in chat! <3")
            {
                GGSendMessage(e.ChatMessage.Channel, "^^ <3 And I'm also here! <3 ^^");

                return;
            }

            if (e.ChatMessage.Message == "djkostNaughtySpray")
            {
                Log($"{e.ChatMessage.DisplayName} used command 'djkostNaughtySpray' in {e.ChatMessage.Channel}");
                GGSendMessage(e.ChatMessage.Channel, $"It looks like DJKoston is being bad again! Bad Koston! {e.ChatMessage.DisplayName} sprays you down!");

                return;
            }

            if (e.ChatMessage.Message.StartsWith("djkostNaughtySpray") && e.ChatMessage.Message.Contains('@'))
            {
                Log($"{e.ChatMessage.DisplayName} used command 'djkostNaughtySpray' in {e.ChatMessage.Channel}");

                var parsedUser = e.ChatMessage.Message.Replace("djkostNaughtySpray", "");

                GGSendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName} thinks {parsedUser} is being bad again! Stop being naughty or {e.ChatMessage.DisplayName} will keep spraying you down!");

                return;
            }
        }

        private void OnLeftChannel(object sender, OnLeftChannelArgs e)
        {
            Log($"{e.BotUsername} disconnected from {e.Channel}");
        }

        private void OnGGClientConnected(object sender, OnConnectedArgs e)
        {
            allPokemon = _pcgService.GetAllPokemon();

            if (environmentName == "Beta")
            {
                var betaStreams = _streamService.GetBetaStreamsToConnect();

                if (betaStreams.Count == 0) { Log("No Beta Streams to connect to.", fail); return; }

                foreach (Streams betaStream in betaStreams)
                {
                    GGTwitch.JoinChannel(betaStream.StreamerUsername);
                }

                HttpClient client = new();

                using (Stream dataStream = client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/").Result)
                {
                    StreamReader reader = new(dataStream);

                    string responseFromServer = reader.ReadToEnd();

                    var pcgAPI = JObject.Parse(responseFromServer);
                    var quickBallCountdown = (Convert.ToInt32(pcgAPI["next_spawn"]) * 1000) - 10000;
                    var timerBallCountdown = (Convert.ToInt32(pcgAPI["next_spawn"]) * 1000) + 80000;

                    //Initialise QuickBall Timer
                    quickBallTimer.Enabled = true;
                    quickBallTimer.Elapsed += QuickBallTimerElapsed;
                    quickBallTimer.Interval = quickBallCountdown;
                    quickBallTimer.AutoReset = true;
                    quickBallTimer.Start();

                    //Initialise TimerBall Timer
                    timerBallTimer.Enabled = true;
                    timerBallTimer.Elapsed += TimerBallTimerElapsed;
                    timerBallTimer.Interval = timerBallCountdown;
                    timerBallTimer.AutoReset = true;
                    timerBallTimer.Start();
                }
            }

            else if(environmentName == "Development")
            {
                Log("Bot Running in Development Mode, no streams will be connected to at this time.");

                HttpClient client = new();

                using (Stream dataStream = client.GetStreamAsync("https://poketwitch.bframework.de/info/events/last_spawn/").Result)
                {
                    StreamReader reader = new(dataStream);

                    string responseFromServer = reader.ReadToEnd();

                    var pcgAPI = JObject.Parse(responseFromServer);
                    var quickBallCountdown = (Convert.ToInt32(pcgAPI["next_spawn"]) * 1000) - 10000;
                    var timerBallCountdown = (Convert.ToInt32(pcgAPI["next_spawn"]) * 1000) + 80000;

                    //Initialise QuickBall Timer
                    quickBallTimer.Enabled = true;
                    quickBallTimer.Elapsed += QuickBallTimerElapsed;
                    quickBallTimer.Interval = quickBallCountdown;
                    quickBallTimer.AutoReset = true;
                    quickBallTimer.Start();

                    //Initialise TimerBall Timer
                    timerBallTimer.Enabled = true;
                    timerBallTimer.Elapsed += TimerBallTimerElapsed;
                    timerBallTimer.Interval = timerBallCountdown;
                    timerBallTimer.AutoReset = true;
                    timerBallTimer.Start();
                }
            }

            else
            {
                var streams = _streamService.GetNonBetaStreamsToConnect();

                if (streams.Count == 0) { Log("No Streams to connect to.", fail); return; }

                foreach (Streams stream in streams)
                {
                    GGTwitch.JoinChannel(stream.StreamerUsername);
                }
            }
        }

        private void TimerBallTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (newTimerBallTimer)
            {
                timerBallTimer.Interval = 900000;

                newTimerBallTimer = false;
            }

            var liveStreams = Monitor.LiveStreams.Values;

            foreach (var stream in liveStreams)
            {
                GGSendMessage(stream.UserName, $"CurseLit Throw those Timer Balls! The Pokemon is about to disappear! CurseLit");
            }

            Log($"Timer Ball Timer: Notification sent to {liveStreams.Count} channels.");
        }

        private void QuickBallTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (newQuickBallTimer)
            {
                quickBallTimer.Interval = 900000;

                newQuickBallTimer = false;
            }

            var liveStreams = Monitor.LiveStreams.Values;

            foreach (var stream in liveStreams)
            {
                GGSendMessage(stream.UserName, $"CurseLit Prepare those Quick Balls! A Pokemon rustles in the distance! CurseLit");
            }

            Log($"Quick Ball Timer: Notification sent to {liveStreams.Count} channels.");
        }

        private void OnStreamOffline(object sender, OnStreamOfflineArgs e)
        {
            Console.WriteLine($"{e.Stream.UserName} is now Offline!"); 
        }

        private void OnStreamOnline(object sender, OnStreamOnlineArgs e)
        {
            Console.WriteLine($"{e.Stream.UserName} is now Live!");
        }

        public void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Log($"{e.BotUsername} Connected to {e.Channel}");
        }

        public void GGSendMessage(string channel, string message)
        {
            GGTwitch.SendMessage(channel, message);
        }

        public static void ConsoleLog(string logLine, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(logLine);
            Console.ResetColor();
        }

        public static void Log(string logItem, ConsoleColor color = ConsoleColor.White)
        {
            var directory = $"/home/container/Logs/{DateTime.Now.Year}/{DateTime.Now.Month:d2}";

            // logging strings
            var date = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}] ";
            var dateFileName = $"{DateTime.Now:dd MMMM yyyy}";
            var header = "[Log ] ";
            var log = $"{logItem}\n";

            // log to console
            ConsoleLog(date);
            ConsoleLog(header, ConsoleColor.DarkYellow);
            ConsoleLog(log, color);

            // ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // log to file
            using StreamWriter w = File.AppendText($"{directory}/{dateFileName}.txt");
            w.WriteLine($"{date}: {logItem}");

            w.Close();
            w.Dispose();
        }
    }
}
