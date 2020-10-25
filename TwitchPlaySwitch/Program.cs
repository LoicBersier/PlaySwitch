using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

using Newtonsoft.Json.Linq;
using SysBot.Base;
using TwitchLib.Api;
using User = TwitchLib.Api.V5.Models.Users.User;

namespace TwitchPlaySwitch
{
    class Program
    {
        private static SwitchConnectionAsync _connection;

        private static readonly Queue StickQueue = new Queue();
        private static readonly Queue ButtonQueue = new Queue();

        private static bool _broadcasterOnly = false;

        private class StickMovement
        {
            public SwitchStick Stick;
            public short X = -1;
            public short Y = -1;
            public int Hold = 1000;
        }

        private static readonly JObject Config = JObject.Parse(File.ReadAllText("config.json"));
        private static readonly JObject NSwitch = JObject.Parse(Config.GetValue("nSwitch").ToString());

        private static readonly string[] Stick =
        {
            "LSU", "LSUR", "LSUL", "LSDR", "LSDL", "LSD", "LSL", "LSR", "RSU", "RSD", "RSL", "RSR"
        };

        private static readonly string[] Button =
        {
            "DLEFT", "DRIGHT", "DUP", "DDOWN", "A", "B", "X", "Y", "+", "-", "ZL", "ZR", "L", "R", "LSP", "RSP"
        };
        
        private static void Log(string msg, ConsoleColor color = default)
        {
            string log = $"{DateTime.Now}: {msg}";

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            File.AppendAllText($"logs/{Convert.ToDateTime(DateTime.Today).ToString("dd.MM.yy")}.txt",
                log + Environment.NewLine);

            Console.ForegroundColor = color;
            Console.WriteLine(log);
            Console.ResetColor();
        }

        private class WebhookContent
        {
            public string username = "Twitch Chat";
            public string content;
            public string avatar_url = "https://brand.twitch.tv/assets/logos/svg/glitch/purple.svg";
        }

        private static async Task SendWebhook(WebhookContent content)
        {
            if (_broadcasterOnly) return;
            
            string json = JsonConvert.SerializeObject(content);
            StringContent data = new StringContent(json, Encoding.UTF8, "application/json");

            var url = (string) Config.GetValue("DiscordWebhook");
            var client = new HttpClient();

            await client.PostAsync(url, data);
            client.Dispose();
        }

        private static void ConnectSwitch(string ip, int port = 6000)
        {
            _connection = new SwitchConnectionAsync(ip, port);
            _connection.Connect();

            if (_connection.Connected)
            {
                Log($"Connected to nintendo switch on {_connection.IP}:{_connection.Port}!", ConsoleColor.Green);
                Log($"Say \"exit\" in twitch or press any key to quit the application!", ConsoleColor.Green);
            }
        }

        public static void Main()
        {
            Log(
                $@"{Environment.NewLine} _____          _ _       _     ____  _                 ____          _ _       _
|_   ___      _(_| |_ ___| |__ |  _ \| | __ _ _   _ ___/ _____      _(_| |_ ___| |__
  | | \ \ /\ / | | __/ __| '_ \| |_) | |/ _` | | | / __\___ \ \ /\ / | | __/ __| '_ \
  | |  \ V  V /| | || (__| | | |  __/| | (_| | |_| \__ \___) \ V  V /| | || (__| | | |
  |_|   \_/\_/ |_|\__\___|_| |_|_|   |_|\__,_|\__, |___|____/ \_/\_/ |_|\__\___|_| |_|
                                              |___/", ConsoleColor.Green);
            
            ConnectSwitch((string) NSwitch.GetValue("IP"));
            
            Bot bot = new Bot();
            Console.ReadLine();
        }

        class Bot
        {
            TwitchClient _client;
            TwitchAPI api;


            public Bot()
            {
                api = new TwitchAPI();
                
                api.Settings.ClientId = (string) Config.GetValue("ClientID");
                api.Settings.AccessToken = (string) Config.GetValue("AccessToken");
                
                ConnectionCredentials credentials =
                    new ConnectionCredentials((string) Config.GetValue("username"), (string) Config.GetValue("OAuth"));
                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };
                WebSocketClient customClient = new WebSocketClient(clientOptions);
                
                _client = new TwitchClient(customClient);
                _client.Initialize(credentials, (string) Config.GetValue("username"));

                _client.OnLog += Client_OnLog;
                _client.OnMessageReceived += Client_OnMessageReceived;
                _client.OnConnected += Client_OnConnected;

                _client.Connect();
            }

            private void Client_OnLog(object sender, OnLogArgs e)
            {
                Log($"{e.BotUsername} - {e.Data}");
            }

            private void Client_OnConnected(object sender, OnConnectedArgs e)
            {
                Log($"Connected to {e.AutoJoinChannel}", ConsoleColor.Green);
            }

            private async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
            {
                User user = await api.V5.Users.GetUserByIDAsync(e.ChatMessage.UserId);
                WebhookContent webhookContent = new WebhookContent();

                if (user.CreatedAt > DateTime.Now.AddDays(-7))
                {
                    Log($"User {e.ChatMessage.DisplayName} account is too new: {user.CreatedAt}", ConsoleColor.DarkRed);
                    _client.SendMessage(e.ChatMessage.Channel, "Your account is too new... Wait a little more...");
                    
                    webhookContent.username = "!WARNING!";
                    webhookContent.avatar_url = "https://upload.wikimedia.org/wikipedia/en/thumb/1/15/Ambox_warning_pn.svg/1178px-Ambox_warning_pn.svg.png";
                    webhookContent.content = $"User `{e.ChatMessage.DisplayName}` account is too new... Created at: `{user.CreatedAt}`";
                    
                
                    SendWebhook(webhookContent);
                }
                else if (_broadcasterOnly && !e.ChatMessage.IsBroadcaster)
                    _client.SendMessage(e.ChatMessage.Channel, "Bot is in Broadcaster only mode.");
                else
                    HandleInput(e, _client);

                webhookContent.username = e.ChatMessage.DisplayName;
                webhookContent.avatar_url = user.Logo;
                webhookContent.content = e.ChatMessage.Message;

                SendWebhook(webhookContent);
            }
        }
        
        private static async Task HandleInput(OnMessageReceivedArgs message, TwitchClient client)
        {
            string msg = message.ChatMessage.Message;
            string args = msg.ToUpper();
            string[] words = args.Split(' ');

            switch (args)
            {
                case "HELP":
                    client.SendMessage(message.ChatMessage.Channel, "You get to control the Nintendo Switch!");
                    client.SendMessage(message.ChatMessage.Channel, "You can say any of the following and they will execute on the console! They can also be chained together");
                    client.SendMessage(message.ChatMessage.Channel, $"{string.Join(" ", Button)} {string.Join(" ", Stick)}");
                    client.SendMessage(message.ChatMessage.Channel, "You can specify \"slower\", \"slow\" or \"medium\" to control the speed of the joystick");
                    client.SendMessage(message.ChatMessage.Channel, "You can also add \"short\" or \"long\" to control the duration of how long the joystick is held");
                    return;
                case "EXIT GAME":
                {
                    if (message.ChatMessage.IsBroadcaster)
                    {
                        CancellationTokenSource cancellationToken = new CancellationTokenSource();
                        await _connection.SendAsync(SwitchCommand.Click(SwitchButton.HOME),
                            cancellationToken.Token);
                        cancellationToken.Cancel();
                        Thread.Sleep(500);
                        cancellationToken = new CancellationTokenSource();
                        await _connection.SendAsync(SwitchCommand.Click(SwitchButton.X),
                            cancellationToken.Token);
                        cancellationToken.Cancel();
                        Thread.Sleep(500);
                        cancellationToken = new CancellationTokenSource();
                        await _connection.SendAsync(SwitchCommand.Click(SwitchButton.A),
                            cancellationToken.Token);
                        cancellationToken.Cancel();

                        Log("Game exited", ConsoleColor.Red);
                        client.SendMessage(message.ChatMessage.Channel, "Game exited");
                    }
                    else
                    {
                        client.SendMessage(message.ChatMessage.Channel,
                            "Command only available for the broadcaster.");
                    }

                    break;
                }
                case "EXIT":
                {
                    if (message.ChatMessage.IsBroadcaster)
                    {
                        _connection.Disconnect();
                        Log("Bye bye!", ConsoleColor.Red);
                        client.SendMessage(message.ChatMessage.Channel, "Bye bye!");
                        Environment.Exit(1);
                    }
                    else
                    {
                        client.SendMessage(message.ChatMessage.Channel,
                            "Command only available for the broadcaster.");
                    }

                    break;
                }
                case "RESTART":
                {
                    if (message.ChatMessage.IsBroadcaster)
                    {
                        _connection.Disconnect();
                        Log("Restarting!!!", ConsoleColor.DarkRed);
                        client.SendMessage(message.ChatMessage.Channel, "Restarting");
                        
                        ConnectSwitch((string) NSwitch.GetValue("IP"));
                    }
                    else
                    {
                        client.SendMessage(message.ChatMessage.Channel,
                            "Command only available for the broadcaster.");
                    }

                    break;
                }
                case "HOME":
                {
                    if (message.ChatMessage.IsBroadcaster)
                    {
                        CancellationTokenSource cancellationToken = new CancellationTokenSource();
                        await _connection.SendAsync(SwitchCommand.Click(SwitchButton.HOME),
                            cancellationToken.Token);
                        cancellationToken.Cancel();
                    }
                    else
                    {
                        client.SendMessage(message.ChatMessage.Channel,
                            "Command only available for the broadcaster.");
                    }

                    break;
                }
                case "BROADCASTERONLY":
                    if (message.ChatMessage.IsBroadcaster)
                    {
                        if (_broadcasterOnly)
                        {
                            _broadcasterOnly = false;
                            Log($"BroadcasterOnly mode turned off: {_broadcasterOnly}", ConsoleColor.Green);
                            client.SendMessage(message.ChatMessage.Channel,
                                "Bot is no longer in Broadcaster only mode.");
                        }
                        else
                        {
                            _broadcasterOnly = true;
                            Log($"BroadcasterOnly mode turned on: {_broadcasterOnly}", ConsoleColor.Red);
                            client.SendMessage(message.ChatMessage.Channel, "Bot is now in Broadcaster only mode.");
                        }
                    }
                    else
                    {
                        client.SendMessage(message.ChatMessage.Channel,
                            "Command only available for the broadcaster.");
                    }

                    break;
            }

            if (Stick.Any(args.Contains))
            {
                short speed = short.MaxValue;
                int hold = 1000;

                if (args.Contains("SLOWER"))
                {
                    speed = 8000;
                    //speed = 10000;
                }
                else if (args.Contains("SLOW"))
                {
                    speed = 10000;
                    //speed = 16382;
                }
                else if (args.Contains("MEDIUM"))
                {
                    speed = 16382;
                    //speed = 20000;
                }

                if (args.Contains("SHORT"))
                {
                    hold = 500;
                }
                else if (args.Contains("LONG"))
                {
                    hold = 5000;
                }

                foreach (var word in words)
                {
                    if (Array.Exists(Stick, e => e == word))
                    {
                        short x = 0;
                        short y = 0;

                        SwitchStick stick;
                        StickMovement stickMovement = new StickMovement {Hold = hold};

                        Log($"{message.ChatMessage.DisplayName} moved stick {word}", ConsoleColor.Green);
                        
                        switch (word)
                        {
                            case "LSU":
                                stick = SwitchStick.LEFT;
                                x = 0;
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, 0, speed);
                                break;
                            case "LSUL":
                                stick = SwitchStick.LEFT;
                                x = short.Parse((speed * -1).ToString());
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), speed);
                                break;
                            case "LSUR":
                                stick = SwitchStick.LEFT;
                                x = speed;
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, speed, speed);
                                break;
                            case "LSD":
                                stick = SwitchStick.LEFT;
                                x = 0;
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, 0, short.Parse((speed * -1).ToString()));
                                break;
                            case "LSDL":
                                stick = SwitchStick.LEFT;
                                x = short.Parse((speed * -1).ToString());
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), short.Parse((speed * -1).ToString()));
                                break;
                            case "LSDR":
                                stick = SwitchStick.LEFT;
                                x = speed;
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, speed, short.Parse((speed * -1).ToString()));
                                break;
                            case "LSL":
                                stick = SwitchStick.LEFT;
                                x = short.Parse((speed * -1).ToString());
                                y = 0;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), 0);
                                break;
                            case "LSR":
                                stick = SwitchStick.LEFT;
                                x = speed;
                                y = 0;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.LEFT, speed, 0);
                                break;
                            case "RSU":
                                stick = SwitchStick.RIGHT;
                                x = 0;
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, 0, speed);
                                break;
                            case "RSD":
                                stick = SwitchStick.RIGHT;
                                x = 0;
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, 0, short.Parse((speed * -1).ToString()));
                                break;
                            case "RSL":
                                stick = SwitchStick.RIGHT;
                                x = short.Parse((speed * -1).ToString());
                                y = 0;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, short.Parse((speed * -1).ToString()), 0);
                                break;
                            case "RSR":
                                stick = SwitchStick.RIGHT;
                                x = speed;
                                y = 0;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, speed, 0);
                                break;
                            case "RSUL":
                                stick = SwitchStick.RIGHT;
                                x = short.Parse((speed * -1).ToString());
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, short.Parse((speed * -1).ToString()), speed);
                                break;
                            case "RSUR":
                                stick = SwitchStick.RIGHT;
                                x = speed;
                                y = speed;

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, speed, speed);
                                break;
                            case "RSDL":
                                stick = SwitchStick.RIGHT;
                                x = short.Parse((speed * -1).ToString());
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, short.Parse((speed * -1).ToString()), short.Parse((speed * -1).ToString()));
                                break;
                            case "RSDR":
                                stick = SwitchStick.RIGHT;
                                x = speed;
                                y = short.Parse((speed * -1).ToString());

                                stickMovement.Stick = stick;
                                stickMovement.X = x;
                                stickMovement.Y = y;
                                //MoveStick(SwitchStick.RIGHT, speed, short.Parse((speed * -1).ToString()));
                                break;
                        }

                        Log("QUEUED", ConsoleColor.Yellow);
                        StickQueue.Enqueue(stickMovement);
                    }
                }


                foreach (StickMovement stick in StickQueue)
                {
                    Log($"Moving {stick.Stick} stick x: {stick.X} y: {stick.Y} hold: {stick.Hold}", ConsoleColor.Red);
                    await MoveStick(stick.Stick, stick.X, stick.Y, stick.Hold);
                }

                StickQueue.Clear();
            }

            if (Button.Any(args.Contains))
            {
                foreach (var word in words)
                {
                    if (Array.Exists(Button, e => e == word))
                    {
                        SwitchButton button = SwitchButton.CAPTURE; // Capture button is never used, let's use it as a "null" value

                        Log($"{message.ChatMessage.DisplayName} Pressed button {word}", ConsoleColor.Green);

                        switch (word)
                        {
                            case "DLEFT":
                                button = SwitchButton.DLEFT;
                                //PressButton(SwitchButton.DLEFT);
                                break;
                            case "DRIGHT":
                                button = SwitchButton.DRIGHT;
                                //PressButton(SwitchButton.DRIGHT);
                                break;
                            case "DUP":
                                button = SwitchButton.DUP;
                                //PressButton(SwitchButton.DUP);
                                break;
                            case "DDOWN":
                                button = SwitchButton.DDOWN;
                                //PressButton(SwitchButton.DDOWN);
                                break;
                            case "A":
                                button = SwitchButton.A;
                                //PressButton(SwitchButton.A);
                                break;
                            case "B":
                                button = SwitchButton.B;
                                //PressButton(SwitchButton.B);
                                break;
                            case "X":
                                button = SwitchButton.X;
                                //PressButton(SwitchButton.X);
                                break;
                            case "Y":
                                button = SwitchButton.Y;
                                //PressButton(SwitchButton.Y);
                                break;
                            case "+":
                                button = SwitchButton.PLUS;
                                //PressButton(SwitchButton.PLUS);
                                break;
                            case "-":
                                button = SwitchButton.MINUS;
                                //PressButton(SwitchButton.MINUS);
                                break;
                            case "ZL":
                                button = SwitchButton.ZL;
                                //PressButton(SwitchButton.ZL);
                                break;
                            case "ZR":
                                button = SwitchButton.ZR;
                                //PressButton(SwitchButton.ZR);
                                break;
                            case "L":
                                button = SwitchButton.L;
                                //PressButton(SwitchButton.L);
                                break;
                            case "R":
                                button = SwitchButton.R;
                                //PressButton(SwitchButton.R);
                                break;
                            case "LSP":
                                button = SwitchButton.LSTICK;
                                //PressButton(SwitchButton.LSTICK);
                                break;
                            case "RSP":
                                button = SwitchButton.RSTICK;
                                //PressButton(SwitchButton.RSTICK);
                                break;
                        }

                        Log("QUEUED", ConsoleColor.Yellow);

                        if (button != SwitchButton.CAPTURE
                        ) // Capture button is never used, let's use it as a "null" value
                            ButtonQueue.Enqueue(button);
                    }
                }

                foreach (SwitchButton button in ButtonQueue)
                {
                    Log($"Pressed {button}", ConsoleColor.Red);
                    await PressButton(button);
                }

                ButtonQueue.Clear();
            }
        }

        private static async Task PressButton(SwitchButton button)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            if (button == SwitchButton.ZL || button == SwitchButton.ZR)
            {
                await _connection.SendAsync(SwitchCommand.Hold(button), cancellationToken.Token);
                Thread.Sleep(2000);
                await _connection.SendAsync(SwitchCommand.Release(button), cancellationToken.Token);
            }
            else
            {
                await _connection.SendAsync(SwitchCommand.Click(button), cancellationToken.Token);
            }

            Thread.Sleep(500);


            cancellationToken.Cancel();
            //StickQueue.Clear();
        }

        private static async Task MoveStick(SwitchStick stick, short x, short y, int hold)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            await _connection.SendAsync(SwitchCommand.SetStick(stick, x, y), cancellationToken.Token);

            //.Sleep(stick == SwitchStick.RIGHT ? 500 : 1000);
            Thread.Sleep(hold);

            await _connection.SendAsync(SwitchCommand.ResetStick(stick), cancellationToken.Token);

            cancellationToken.Cancel();
            //ButtonQueue.Clear();
        }
    }
}