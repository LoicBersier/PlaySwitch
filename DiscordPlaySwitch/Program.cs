using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SysBot.Base;

namespace DiscordPlaySwitch
{
    class Program
    {
        private DiscordSocketClient _client;
        private SwitchConnectionAsync _connection;

        private static readonly JObject Config = JObject.Parse(File.ReadAllText("config.json"));
        private readonly JObject _nSwitch = JObject.Parse(Config.GetValue("nSwitch").ToString());

        private readonly string[] _control =
        {
            "DLEFT", "DRIGHT", "DUP", "DDOWN", "A", "B", "X", "Y", "+", "-", "ZL", "ZR", "L", "R", "LSP", "RSP", "LSU",
            "LSUR", "LSUL",  "LSDR", "LSDL", "LSD", "LSL", "LSR", "RSU", "RSD", "RSL", "RSR", "SCREENSHOT"
        };

        private readonly string _prefix = ((string) Config.GetValue("prefix")).ToUpper();

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            _client = new DiscordSocketClient();

            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, (string) Config.GetValue("token"));
            await _client.StartAsync();

            _client.Ready += () =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==================================================================================");
                Console.WriteLine($"Connected to discord as {_client.CurrentUser.Username}#{_client.CurrentUser.DiscriminatorValue} ({_client.CurrentUser.Id})");
                Console.WriteLine($"Discord bot prefix is set as: {_prefix}");
                Console.WriteLine("Attempting to connect to the Nintendo switch");
                try
                {
                    _connection = new SwitchConnectionAsync((string) _nSwitch.GetValue("IP"),
                        int.Parse((string) _nSwitch.GetValue("sysbotPORT")));
                    _connection.Connect();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ResetColor();
                }


                if (_connection.Connected)
                {
                    Console.WriteLine($"Connected to nintendo switch on {_connection.IP}:{_connection.Port}!");
                    Console.WriteLine($"Say \"{_prefix}exit\" in discord to quit the application!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to connect to the console!");
                    Console.WriteLine("Press any key to exit the app.");
                    Console.ResetColor();
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                Console.WriteLine("==================================================================================");
                Console.ResetColor();
                return Task.CompletedTask;
            };

            _client.MessageReceived += async message =>
            {
                if (message.Author.IsBot) return;
                if (message.Author.Id == 595703186816499772) return;

                if (_connection.Connected)
                    if (message.Content.Length > _prefix.Length)
                        await HandleInput(message);
            };
            
            await Task.Delay(-1);
        }

        private async Task HandleInput(SocketMessage message)
        {
            string msg = message.Content;
            string args = msg.ToUpper().Substring(_prefix.Length, msg.Length - _prefix.Length);
            string[] words = args.Split(' ');
            
            if (msg.ToUpper().StartsWith(_prefix))
            {
                switch (args)
                {
                    case "HELP":
                        await message.Channel.SendMessageAsync(
                            $"You can input one of those command and they will execute on my nintendo switch ( please don't destroy it ):\n{string.Join(" ", _control)}");
                        break;
                    case "EXIT GAME":
                    {
                        break;

                        if (message.Author.Id == 267065637183029248)
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

                            Console.WriteLine("Game exited");
                            await message.Channel.SendMessageAsync("Game exited");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync("Only for supositware");
                        }
                        break;
                    }
                    case "EXIT":
                    {
                        break;

                        if (message.Author.Id == 267065637183029248)
                        {
                            _connection.Disconnect();
                            Console.WriteLine("Bye bye!");
                            await message.Channel.SendMessageAsync("Bye bye!");
                            Environment.Exit(1);
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync("Only for supositware");
                        }
                        break;
                    }
                    case "SCREENSHOT":
                        SendScreenshot(message);
                        break;
                }


                if (_control.Any(args.Contains))
                {
                    short speed = short.MaxValue;
                    
                    if (args.Contains("SLOWER"))
                    {
                        speed = 8000;
                        //i++;
                    } 
                    else if (args.Contains("SLOW"))
                    {
                        speed = 10000;
                        //i++;
                    } 
                    else if (args.Contains("MEDIUM"))
                    {
                        speed = 16382;
                        //i++;
                    }
                    
                    foreach (var word in words)
                    {
                        if (Array.Exists(_control, e => e == word))
                        {
                            switch (word)
                            {
                                case "DLEFT":
                                    PressButton(SwitchButton.DLEFT);
                                    break;
                                case "DRIGHT":
                                    PressButton(SwitchButton.DRIGHT);
                                    break;
                                case "DUP":
                                    PressButton(SwitchButton.DUP);
                                    break;
                                case "DDOWN":
                                    PressButton(SwitchButton.DDOWN);
                                    break;
                                case "A":
                                    PressButton(SwitchButton.A);
                                    break;
                                case "B":
                                    PressButton(SwitchButton.B);
                                    break;
                                case "X":
                                    PressButton(SwitchButton.X);
                                    break;
                                case "Y":
                                    PressButton(SwitchButton.Y);
                                    break;
                                case "+":
                                    PressButton(SwitchButton.PLUS);
                                    break;
                                case "-":
                                    PressButton(SwitchButton.MINUS);
                                    break;
                                case "ZL":
                                    PressButton(SwitchButton.ZL);
                                    break;
                                case "ZR":
                                    PressButton(SwitchButton.ZR);
                                    break;
                                case "L":
                                    PressButton(SwitchButton.L);
                                    break;
                                case "R":
                                    PressButton(SwitchButton.R);
                                    break;
                                case "LSP":
                                    PressButton(SwitchButton.LSTICK);
                                    break;
                                case "RSP":
                                    PressButton(SwitchButton.RSTICK);
                                    break;
                                case "LSU":
                                    MoveStick(SwitchStick.LEFT, 0, speed);
                                    break;
                                case "LSUL":
                                    MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), speed);
                                    break;
                                case "LSUR":
                                    MoveStick(SwitchStick.LEFT, speed, speed);
                                    break;
                                case "LSD":
                                    MoveStick(SwitchStick.LEFT, 0, short.Parse((speed * -1).ToString()));
                                    break;
                                case "LSDL":
                                    MoveStick(SwitchStick.LEFT, speed, short.Parse((speed * -1).ToString()));
                                    break;
                                case "LSDR":
                                    MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), short.Parse((speed * -1).ToString()));
                                    break;
                                case "LSL":
                                    MoveStick(SwitchStick.LEFT, short.Parse((speed * -1).ToString()), 0);
                                    break;
                                case "LSR":
                                    MoveStick(SwitchStick.LEFT, speed, 0);
                                    break;
                                case "RSU":
                                    MoveStick(SwitchStick.RIGHT, 0, speed);
                                    break;
                                case "RSD":
                                    MoveStick(SwitchStick.RIGHT, 0, short.Parse((speed * -1).ToString()));
                                    break;
                                case "RSL":
                                    MoveStick(SwitchStick.RIGHT, short.Parse((speed * -1).ToString()), 0);
                                    break;
                                case "RSR":
                                    MoveStick(SwitchStick.RIGHT, speed, 0);
                                    break;
                            }
                            
                            SendScreenshot(message);
                        }
                    }
                }
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async void PressButton(SwitchButton button)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            if (button == SwitchButton.ZL || button == SwitchButton.ZR)
            {
                await _connection.SendAsync(SwitchCommand.Hold(button), cancellationToken.Token);
                Thread.Sleep(2000);
                //SendScreenshot(message);
                await _connection.SendAsync(SwitchCommand.Release(button), cancellationToken.Token);
            }
            else
            {
                await _connection.SendAsync(SwitchCommand.Click(button), cancellationToken.Token);
                //SendScreenshot(message);
            }


            cancellationToken.Cancel();
        }


        private async void MoveStick(SwitchStick stick, short x, short y)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            await _connection.SendAsync(SwitchCommand.SetStick(stick, x, y), cancellationToken.Token);

            Thread.Sleep(stick == SwitchStick.RIGHT ? 500 : 1000);

            await _connection.SendAsync(SwitchCommand.ResetStick(stick), cancellationToken.Token);
            //SendScreenshot(message);

            cancellationToken.Cancel();
        }

        private void SendScreenshot(SocketMessage message)
        {
            using (Process pProcess = new Process())
            {
                pProcess.StartInfo.FileName = "ffmpeg";
                pProcess.StartInfo.Arguments =
                    $"-hide_banner -loglevel panic -y -i rtsp://{(string) _nSwitch.GetValue("IP")}:{(string) _nSwitch.GetValue("sysDVRPORT")} -vframes 1 NintendoSwitch.jpg"; //argument
                //pProcess.StartInfo.Arguments = $"-hide_banner -loglevel panic -y -i rtsp://127.0.0.1:6666 -vframes 1 NintendoSwitch.jpg"; //argument
                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pProcess.StartInfo.CreateNoWindow = true; //not diplay a windows
                pProcess.Start();
                pProcess.WaitForExit();
            }

            message.Channel.SendFileAsync("NintendoSwitch.jpg");
        }
    }
}