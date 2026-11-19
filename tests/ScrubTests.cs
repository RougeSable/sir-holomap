using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SirHolomap.Tests
{
    public class ScrubTests
    {
        private readonly ITestOutputHelper m_output;

        public ScrubTests(ITestOutputHelper output)
        {
            m_output = output;
        }

        [Fact]
        public void RealGameLogLeavesNoPlayerData()
        {
            var scrubber = new ReportScrubber();
            // Ce que le greffon sait du joueur, tel qu'il l'apprend en jeu.
            scrubber.AddPrivate(GameLogOf20261027.Nickname);
            scrubber.AddPrivate(GameLogOf20261027.GridName);
            scrubber.AddPrivate(GameLogOf20261027.WindowsUser);
            scrubber.AddPrivate(GameLogOf20261027.ServerName);
            scrubber.AddPrivate(GameLogOf20261027.ServerAddress);

            var log = GameLog.Split(GameLogOf20261027.Text());
            var kept = scrubber.CleanLines(log);
            foreach (var line in kept)
                m_output.WriteLine(line);

            // Les lignes qui portaient quelque chose du joueur ont disparu.
            Assert.True(kept.Count > 0 && kept.Count < log.Count);
            var all = string.Join("\n", kept.ToArray());
            Assert.DoesNotContain(GameLogOf20261027.SteamId, all);
            Assert.DoesNotContain(GameLogOf20261027.Nickname, all);
            Assert.DoesNotContain(GameLogOf20261027.GridName, all);
            Assert.DoesNotContain("203.0.113.7", all);
            Assert.DoesNotContain(GameLogOf20261027.ServerName, all);
            Assert.DoesNotContain(GameLogOf20261027.WindowsUser, all);
            Assert.DoesNotContain("Users\\", all);

            // Le dossier de l'utilisateur Windows devient %USERPROFILE%.
            Assert.Contains(kept, l => l.EndsWith("Log file: %USERPROFILE%\\AppData\\Roaming\\SpaceEngineers\\SpaceEngineers_20261027_210311_405.log"));
            Assert.Contains(kept, l => l.EndsWith("Profile folder: %USERPROFILE%\\AppData\\Roaming\\SpaceEngineers"));

            // Ce qui ne concerne pas le joueur reste.
            Assert.Contains(kept, l => l.Contains("cache de forme de la planète EarthLike pas prêt"));
            Assert.Contains(kept, l => l.Contains("DXGI_ERROR_DEVICE_REMOVED"));
            Assert.Contains(kept, l => l.Contains("at VRageRender.MyRender11.Present()"));
        }

        [Fact]
        public void EachKindOfPlayerDataIsCaughtWithoutHelp()
        {
            // On ne dit rien au nettoyeur : la forme de la ligne suffit.
            var scrubber = new ReportScrubber();
            var lines = new[]
            {
                "Steam user: someone (76561198012345678)",
                "Connecting to server 198.51.100.4:27016",
                "Connecting to my.example.org:27020",
                "Lobby joined at 198.51.100.4",
                "Player Someone joined the world",
                "Grid 'Big Ship 1' loaded",
                "Display name: Someone",
            };
            foreach (var line in lines)
                Assert.Null(scrubber.Clean(line));

            // Un numéro de version n'est pas une adresse.
            Assert.Equal("Assembly mscorlib, Version=4.0.0.0 loaded", scrubber.Clean("Assembly mscorlib, Version=4.0.0.0 loaded"));
            Assert.Equal("at A.B() in %USERPROFILE%\\source\\A.cs:line 12", scrubber.Clean("at A.B() in D:\\Users\\Someone Else\\source\\A.cs:line 12"));
            Assert.Equal("%USERPROFILE%/AppData/x.log", scrubber.Clean("c:/users/Jane/AppData/x.log"));
        }

        [Fact]
        public void WordsToHideAreWholeWordsOnly()
        {
            var scrubber = new ReportScrubber();
            scrubber.AddPrivate("Bob");
            Assert.Null(scrubber.Clean("hello bob!"));
            Assert.Equal("Bobsled loaded", scrubber.Clean("Bobsled loaded"));
            scrubber.AddUserFolder("E:\\Profiles\\Jane");
            Assert.Equal("opened %USERPROFILE%\\Documents", scrubber.Clean("opened E:\\Profiles\\Jane\\Documents"));
        }
    }
}
