# DiscordPlaysSwitch/TwitchPlaysSwitch

My attempt at making TwitchPlays type of thing with homebrew on my nintendo switch.

I'm not responsible for any ban that may happen to your account or console!

NOTE: I learned C# as i was doing this, therefore the code might not be of high quality

## Thing required

* [A non-ipatched switch](https://gbatemp.net/threads/switch-informations-by-serial-number-read-the-first-post-before-asking-questions.481215/) with [Atmosphere cfw](https://github.com/Atmosphere-NX/Atmosphere/) and [sys-botbase](https://github.com/olliz0r/sys-botbase)
* [SysBot.NET](https://github.com/kwsch/SysBot.NET) ( Included as a submodule in this repo )
* [SysDVR](https://github.com/exelix11/SysDVR/) ( Only for DiscordPlaysSwitch )
* [ffmpeg](https://ffmpeg.org/) ( Only for DiscordPlaysSwitch )
* [ldn_mitm](https://github.com/spacemeowx2/ldn_mitm) ( If playing a game that use local play like pokémon sword/shield )

**Don't forget to clone the submodule too!**

## DiscordPlaysSwitch

Currently DiscordPlaysSwitch also require SysDVR but it could be easily modified to use something else like a capture card.

This version might not be up-to-date compared to TwitchPlaysSwitch.

###  config.json example

```json
{
  "token": "Discord bot token",
  "prefix": "Discord bot prefix ",
  "nSwitch": {
    "IP": "Nintendo switch IP",
    "sysbotPORT": "6000",
    "sysDVRPORT": "6666"
  }
} 
```

## TwitchPlaysSwitch 

The capture is obviously done via another software so no change needed whether you use a capture card or sys-DVR.

### config.json example

```json
{
  "username": "Twitch username",
  "OAuth": "Twitch OAuth token",
  "ClientID": "Twitch Client ID",
  "AccessToken": "Twitch Access Token",
  "DiscordWebhook": "Discord webhook to send messages to",
  "nSwitch": {
    "IP": "Nintendo Switch IP",
    "sysbotPORT": "6000"
  }
} 
```

Thanks to [Jetbrains](https://www.jetbrains.com/?from=Hahayesdiscordbot) for providing their IDE free of charges! 

<img src="https://its.gamingti.me/XT8F.svg" width=20%></img>
