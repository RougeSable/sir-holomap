using System;
using System.Collections.Generic;
using System.Text;
using Sandbox;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.GameServices;

namespace SirHolomap
{
    // A server shown on the galaxy.
    internal sealed class GalaxyServer
    {
        public string Address = "";
        public string Name = "";
        public string ConnectionString = "";
        public bool Favorite;
        public VisitedServer Visit;
        public MyGameServerItem Item;
        public GalaxyPoint Place;
        public bool IsCurrent;

        public bool Visited
        {
            get { return Visit != null; }
        }
    }

    // The servers the galaxy knows: those the player visited (kept by the
    // map), the game's favorites, and the game's server list when no filter
    // is checked, asked to the game the way its own server browser does.
    internal sealed class ServerDirectory
    {
        private const int MaximumServers = 6000;
        private const string FavoriteEvent = "OnFavoritesServerListResponded";
        private const string InternetEvent = "OnDedicatedServerListResponded";

        private readonly Dictionary<string, GalaxyServer> m_servers = new Dictionary<string, GalaxyServer>();
        private object m_discovery;
        private Delegate m_favoriteHandler;
        private Delegate m_internetHandler;
        private bool m_favoritesAsked;
        private bool m_internetAsked;
        private DateTime m_loadingUntil;

        public bool Loading
        {
            get { return DateTime.UtcNow < m_loadingUntil; }
        }

        public IEnumerable<GalaxyServer> Servers
        {
            get { return m_servers.Values; }
        }

        public int Count
        {
            get { return m_servers.Count; }
        }

        public void Update(MapWorld world, MapSettings settings)
        {
            if (world.History != null)
            {
                foreach (var visit in world.History.Servers)
                {
                    var server = Get(visit.Address);
                    if (server == null)
                        continue;
                    server.Visit = visit;
                    if (string.IsNullOrEmpty(server.Name))
                        server.Name = visit.Name;
                }
            }

            // Where the player is: a dedicated server by its address; a local
            // world or a hosted game by its key, so that it has its place too.
            var current = "";
            var identity = world.Identity;
            if (identity != null)
            {
                var dedicated = identity.IsDedicated;
                current = dedicated ? identity.Normalized : ServerAddress.Normalize(identity.Key);
                var here = Get(dedicated ? identity.ConnectionString : identity.Key);
                if (here != null)
                {
                    if (string.IsNullOrEmpty(here.Name))
                        here.Name = identity.Name;
                    if (!dedicated)
                        here.ConnectionString = "";
                }
            }
            foreach (var server in m_servers.Values)
                server.IsCurrent = current.Length > 0 && server.Address == current;

            if (!m_favoritesAsked)
                AskFavorites();
            if (!m_internetAsked && !settings.GalaxyFavoritesOnly && !settings.GalaxyVisitedOnly)
                AskInternet();
        }

        public void Refresh()
        {
            Stop();
            m_favoritesAsked = false;
            m_internetAsked = false;
        }

        private GalaxyServer Get(string address)
        {
            var key = ServerAddress.Normalize(address);
            if (key.Length == 0)
                return null;
            GalaxyServer server;
            if (!m_servers.TryGetValue(key, out server))
            {
                if (m_servers.Count >= MaximumServers)
                    return null;
                server = new GalaxyServer { Address = key, ConnectionString = address, Place = GalaxyPlacement.PositionOf(key) };
                m_servers[key] = server;
            }
            return server;
        }

        private void Add(MyGameServerItem item, bool favorite)
        {
            if (item == null || string.IsNullOrEmpty(item.ConnectionString))
                return;
            var server = Get(item.ConnectionString);
            if (server == null)
                return;
            server.Item = item;
            server.ConnectionString = item.ConnectionString;
            if (!string.IsNullOrEmpty(item.Name))
                server.Name = item.Name;
            if (favorite)
                server.Favorite = true;
        }

        private object Discovery()
        {
            if (m_discovery == null)
                m_discovery = GameServices.Discovery();
            return m_discovery;
        }

        private void AskFavorites()
        {
            m_favoritesAsked = true;
            var discovery = Discovery();
            if (discovery == null || !GameServices.Flag(discovery, "FavoritesSupport"))
                return;
            if (m_favoriteHandler == null)
                m_favoriteHandler = GameServices.Subscribe(discovery, FavoriteEvent,
                    (sender, index) => Add(GameServices.Details(discovery, "GetFavoritesServerDetails", index), true));
            if (m_favoriteHandler != null && GameServices.Request(discovery, "RequestFavoritesServerList"))
                m_loadingUntil = DateTime.UtcNow.AddSeconds(8);
        }

        private void AskInternet()
        {
            m_internetAsked = true;
            var discovery = Discovery();
            if (discovery == null || !GameServices.Flag(discovery, "DedicatedSupport"))
                return;
            if (m_internetHandler == null)
                m_internetHandler = GameServices.Subscribe(discovery, InternetEvent,
                    (sender, index) => Add(GameServices.Details(discovery, "GetDedicatedServerDetails", index), false));
            if (m_internetHandler != null && GameServices.Request(discovery, "RequestInternetServerList"))
                m_loadingUntil = DateTime.UtcNow.AddSeconds(15);
        }

        public void Stop()
        {
            var discovery = m_discovery;
            if (discovery != null)
            {
                GameServices.Unsubscribe(discovery, FavoriteEvent, m_favoriteHandler);
                GameServices.Unsubscribe(discovery, InternetEvent, m_internetHandler);
                if (m_favoriteHandler != null)
                    GameServices.Call(discovery, "CancelFavoritesServersRequest");
                if (m_internetHandler != null)
                    GameServices.Call(discovery, "CancelInternetServersRequest");
            }
            m_favoriteHandler = null;
            m_internetHandler = null;
            m_loadingUntil = DateTime.MinValue;
        }

        // Which servers the filters let through: favorites and visited are
        // boxes, both unchecked shows all; the search matches name or address.
        public static bool Passes(GalaxyServer server, MapSettings settings)
        {
            if (server.IsCurrent)
                return true;
            if (settings.GalaxyFavoritesOnly || settings.GalaxyVisitedOnly)
            {
                var wanted = (settings.GalaxyFavoritesOnly && server.Favorite) || (settings.GalaxyVisitedOnly && server.Visited);
                if (!wanted)
                    return false;
            }
            var search = settings.GalaxySearch;
            if (!string.IsNullOrEmpty(search))
            {
                var name = server.Name ?? "";
                if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && server.Address.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            return true;
        }
    }

    // Joining another server from the galaxy. The double click first asks
    // the server whether it answers, has room and runs this version of the
    // game; then a confirmation names it, with a box for the password when
    // the server asks for one. Only then does the game leave the current
    // server. On any failure the player stays where they are, and is told
    // why. The password typed is handed to the game when its own join asks
    // for it, and forgotten at once.
    internal static class ServerJoiner
    {
        private static GalaxyServer s_pending;
        private static bool s_pendingReturn;
        private static DateTime s_deadline;
        private static string s_password;
        private static DateTime s_passwordUntil;
        private static readonly WayBack s_wayBack = new WayBack();
        private static readonly EventHandler<MyGameServerItem> Responded = OnResponded;
        private static readonly EventHandler Failed = OnFailed;

        public static bool Busy
        {
            get { return s_pending != null; }
        }

        private static string NameOf(GalaxyServer server)
        {
            return string.IsNullOrEmpty(server.Name) ? server.Address : server.Name;
        }

        public static void Ask(GalaxyServer server)
        {
            if (server == null || Busy)
                return;
            var name = NameOf(server);
            if (server.IsCurrent)
            {
                Show(string.Format(Texts.JoinAlreadyHere, name), Texts.JoinCaption);
                return;
            }
            if (string.IsNullOrEmpty(server.ConnectionString))
            {
                Show(string.Format(Texts.JoinNoAddress, name), Texts.JoinFailedCaption);
                return;
            }
            Check(server);
        }

        // returning: going back to the server just left, from the main menu.
        private static void Check(GalaxyServer server, bool returning = false)
        {
            if (Busy)
                return;
            s_pending = server;
            s_pendingReturn = returning;
            s_deadline = DateTime.UtcNow.AddSeconds(12);
            if (MyAPIGateway.Utilities != null && MyAPIGateway.Session != null)
                MyAPIGateway.Utilities.ShowNotification(string.Format(Texts.JoinChecking, NameOf(server)), 3000);
            if (!GameServices.Ping(server.ConnectionString, Responded, Failed))
                Fail(returning ? Texts.ReturnOffline : Texts.JoinOffline);
        }

        // Called every update, in a world or not: a server that never answers
        // is offline, a password typed in the confirmation goes to the game's
        // own password prompt, and a join that failed after leaving offers
        // the way back.
        public static void Update(bool inWorld)
        {
            if (s_pending != null && DateTime.UtcNow > s_deadline)
                Fail(s_pendingReturn ? Texts.ReturnOffline : Texts.JoinOffline);

            if (s_password != null)
            {
                if (DateTime.UtcNow > s_passwordUntil)
                    s_password = null;
                else if (GameServices.AnswerPasswordPrompt(s_password))
                    s_password = null;
            }

            if (!s_wayBack.Armed)
                return;
            var busy = inWorld ? false : GameServices.JoinScreensOpen();
            if (s_wayBack.Update(DateTime.UtcNow, inWorld, busy))
                OfferWayBack();
        }

        private static void OfferWayBack()
        {
            var back = new GalaxyServer
            {
                ConnectionString = s_wayBack.BackConnection,
                Address = ServerAddress.Normalize(s_wayBack.BackConnection),
                Name = s_wayBack.BackName,
            };
            HolomapPlugin.Log("the join of " + s_wayBack.TargetName + " failed: offering the way back to " + back.ConnectionString);
            var box = MyGuiSandbox.CreateMessageBox(
                styleEnum: MyMessageBoxStyleEnum.Info,
                buttonType: MyMessageBoxButtonsType.YES_NO,
                messageText: new StringBuilder(string.Format(Texts.ReturnQuestion, s_wayBack.TargetName, NameOf(back))),
                messageCaption: new StringBuilder(Texts.ReturnCaption),
                callback: result =>
                {
                    if (result == MyGuiScreenMessageBox.ResultEnum.YES)
                        Check(back, true);
                },
                focusedResult: MyGuiScreenMessageBox.ResultEnum.YES,
                canHideOthers: false);
            MyGuiSandbox.AddScreen(box);
        }

        private static void OnResponded(object sender, MyGameServerItem item)
        {
            var server = s_pending;
            if (server == null || item == null)
                return;
            // Another ping of the game's own: not ours.
            if (ServerAddress.Normalize(item.ConnectionString) != server.Address)
                return;
            GameServices.StopPing(Responded, Failed);
            s_pending = null;
            var returning = s_pendingReturn;
            s_pendingReturn = false;

            var name = string.IsNullOrEmpty(item.Name) ? NameOf(server) : item.Name;
            if (item.MaxPlayers > 0 && item.Players >= item.MaxPlayers)
            {
                Show(string.Format(Texts.JoinFull, name, item.Players, item.MaxPlayers), Texts.JoinFailedCaption);
                return;
            }
            if (item.ServerVersion != 0 && item.ServerVersion != (int)MyFinalBuildConstants.APP_VERSION)
            {
                Show(string.Format(Texts.JoinVersion, name), Texts.JoinFailedCaption);
                return;
            }

            if (returning)
            {
                HolomapPlugin.Log("going back to " + item.ConnectionString);
                MySandboxGame.Static.Invoke(() =>
                {
                    if (!GameServices.Join(item, false))
                        Show(string.Format(Texts.JoinNoAddress, name), Texts.JoinFailedCaption);
                }, "SirHolomapReturn");
                return;
            }

            // The confirmation, naming the server.
            var lines = new List<string>();
            lines.Add(string.Format(Texts.JoinQuestion, name));
            lines.Add("");
            lines.Add(Texts.Address + ": " + item.ConnectionString);
            if (item.MaxPlayers > 0)
                lines.Add(Texts.Players + ": " + item.Players + " / " + item.MaxPlayers);
            if (item.Ping > 0)
                lines.Add(Texts.Ping + ": " + item.Ping + " ms");
            var needsPassword = item.Password || (server.Item != null && server.Item.Password);
            if (needsPassword)
                lines.Add(Texts.JoinPasswordNeeded);
            var question = string.Join(Environment.NewLine, lines.ToArray());
            MyGuiSandbox.AddScreen(new JoinDialog(question, needsPassword, password => Leave(item, name, password)));
        }

        private static void Leave(MyGameServerItem item, string name, string password)
        {
            HolomapPlugin.Log("leaving for " + item.ConnectionString);
            MySandboxGame.Static.Invoke(() =>
            {
                // The way back, should the other server refuse once left.
                var plugin = HolomapPlugin.Instance;
                var here = plugin != null ? plugin.CurrentServer : null;
                if (here != null && here.IsDedicated)
                    s_wayBack.Leave(here.ConnectionString, here.Name, name, DateTime.UtcNow);
                else
                    s_wayBack.Disarm();
                s_password = string.IsNullOrEmpty(password) ? null : password;
                s_passwordUntil = DateTime.UtcNow.AddMinutes(3);
                if (plugin != null)
                    plugin.BeforeLeaving();
                if (!GameServices.Join(item))
                {
                    s_wayBack.Disarm();
                    s_password = null;
                    Show(string.Format(Texts.JoinNoAddress, name), Texts.JoinFailedCaption);
                }
            }, "SirHolomapJoin");
        }

        private static void OnFailed(object sender, EventArgs e)
        {
            if (s_pending != null)
                Fail(s_pendingReturn ? Texts.ReturnOffline : Texts.JoinOffline);
        }

        private static void Fail(string format)
        {
            var server = s_pending;
            GameServices.StopPing(Responded, Failed);
            s_pending = null;
            s_pendingReturn = false;
            if (server != null)
                Show(string.Format(format, NameOf(server)), Texts.JoinFailedCaption);
        }

        private static void Show(string text, string caption)
        {
            MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                styleEnum: MyMessageBoxStyleEnum.Error,
                buttonType: MyMessageBoxButtonsType.OK,
                messageText: new StringBuilder(text),
                messageCaption: new StringBuilder(caption),
                canHideOthers: false));
        }

        public static void Reset()
        {
            GameServices.StopPing(Responded, Failed);
            s_pending = null;
            s_pendingReturn = false;
            s_password = null;
            s_wayBack.Disarm();
        }
    }
}
