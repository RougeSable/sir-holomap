using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Engine.Networking;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.GameServices;

namespace SirHolomap
{
    // The game's server browser and its join flow, reached by name at run
    // time: these parts of the game change between versions, and a name that
    // is no longer there must only switch off the matching feature (with a
    // line in the log), never stop the plugin from loading.
    internal static class GameServices
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static object Discovery()
        {
            try
            {
                var property = typeof(MyGameService).GetProperty("ServerDiscovery", Static);
                return property != null ? property.GetValue(null, null) : null;
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("server discovery not found: " + e.Message);
                return null;
            }
        }

        public static bool Flag(object discovery, string name)
        {
            try
            {
                var property = FindProperty(discovery.GetType(), name);
                return property != null && property.GetValue(discovery, null) is bool && (bool)property.GetValue(discovery, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The game's discovery is often an interface over several services:
        // members are looked up on the class and on every interface it has.
        private static PropertyInfo FindProperty(Type type, string name)
        {
            var property = type.GetProperty(name, Instance);
            if (property != null)
                return property;
            foreach (var face in type.GetInterfaces())
            {
                property = face.GetProperty(name, Instance);
                if (property != null)
                    return property;
            }
            return null;
        }

        private static MethodInfo FindMethod(Type type, string name, int parameters)
        {
            foreach (var method in type.GetMethods(Instance))
            {
                if (method.Name == name && method.GetParameters().Length == parameters)
                    return method;
            }
            foreach (var face in type.GetInterfaces())
            {
                foreach (var method in face.GetMethods())
                {
                    if (method.Name == name && method.GetParameters().Length == parameters)
                        return method;
                }
            }
            return null;
        }

        private static EventInfo FindEvent(Type type, string name)
        {
            var info = type.GetEvent(name, Instance);
            if (info != null)
                return info;
            foreach (var face in type.GetInterfaces())
            {
                info = face.GetEvent(name);
                if (info != null)
                    return info;
            }
            return null;
        }

        // Subscribes a handler "void (object sender, int index)" to an event of
        // the discovery. Returns the delegate to unsubscribe with.
        public static Delegate Subscribe(object discovery, string eventName, Action<object, int> handler)
        {
            try
            {
                var info = FindEvent(discovery.GetType(), eventName);
                if (info == null || info.EventHandlerType != typeof(EventHandler<int>))
                    return null;
                EventHandler<int> typed = (sender, index) => handler(sender, index);
                info.AddEventHandler(discovery, typed);
                return typed;
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("could not listen to " + eventName + ": " + e.Message);
                return null;
            }
        }

        public static void Unsubscribe(object discovery, string eventName, Delegate handler)
        {
            if (discovery == null || handler == null)
                return;
            try
            {
                var info = FindEvent(discovery.GetType(), eventName);
                if (info != null)
                    info.RemoveEventHandler(discovery, handler);
            }
            catch (Exception)
            {
            }
        }

        // Asks for a list with an empty search filter.
        public static bool Request(object discovery, string methodName)
        {
            try
            {
                var method = FindMethod(discovery.GetType(), methodName, 1);
                if (method == null)
                    return false;
                var filter = Activator.CreateInstance(method.GetParameters()[0].ParameterType);
                method.Invoke(discovery, new[] { filter });
                return true;
            }
            catch (Exception e)
            {
                HolomapPlugin.Log(methodName + " failed: " + e.Message);
                return false;
            }
        }

        public static void Call(object discovery, string methodName)
        {
            if (discovery == null)
                return;
            try
            {
                var method = FindMethod(discovery.GetType(), methodName, 0);
                if (method != null)
                    method.Invoke(discovery, null);
            }
            catch (Exception)
            {
            }
        }

        public static MyGameServerItem Details(object discovery, string methodName, int index)
        {
            try
            {
                var method = FindMethod(discovery.GetType(), methodName, 1);
                return method != null ? method.Invoke(discovery, new object[] { index }) as MyGameServerItem : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Pinging a server, as the game does for "+connect": the answer comes
        // as an event.
        public static bool Ping(string connectionString, EventHandler<MyGameServerItem> responded, EventHandler failed)
        {
            try
            {
                var type = typeof(MyGameService);
                var ok = type.GetEvent("OnPingServerResponded", Static);
                var ko = type.GetEvent("OnPingServerFailedToRespond", Static);
                var ping = type.GetMethod("PingServer", Static, null, new[] { typeof(string) }, null);
                if (ok == null || ko == null || ping == null || ok.EventHandlerType != typeof(EventHandler<MyGameServerItem>))
                    return false;
                ok.AddEventHandler(null, responded);
                if (ko.EventHandlerType == typeof(EventHandler))
                    ko.AddEventHandler(null, failed);
                ping.Invoke(null, new object[] { connectionString });
                return true;
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("ping failed: " + e.Message);
                return false;
            }
        }

        public static void StopPing(EventHandler<MyGameServerItem> responded, EventHandler failed)
        {
            try
            {
                var type = typeof(MyGameService);
                var ok = type.GetEvent("OnPingServerResponded", Static);
                var ko = type.GetEvent("OnPingServerFailedToRespond", Static);
                if (ok != null && ok.EventHandlerType == typeof(EventHandler<MyGameServerItem>))
                    ok.RemoveEventHandler(null, responded);
                if (ko != null && ko.EventHandlerType == typeof(EventHandler))
                    ko.RemoveEventHandler(null, failed);
            }
            catch (Exception)
            {
            }
        }

        // Leaves the current server for the main menu, then joins the other
        // one, the way the game's own server browser does.
        // leaveFirst: false when already at the main menu (the way back).
        public static bool Join(MyGameServerItem server, bool leaveFirst = true)
        {
            var unload = typeof(MySessionLoader).GetMethod("UnloadAndExitToMenu", Static, null, Type.EmptyTypes, null);
            MethodInfo join = null;
            object[] arguments = null;
            foreach (var method in typeof(MyJoinGameHelper).GetMethods(Static).Where(m => m.Name == "JoinGame"))
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(MyGameServerItem))
                    continue;
                // The overload that first asks the server for its rules.
                if (parameters.Length >= 2 && parameters[1].ParameterType == typeof(bool))
                {
                    join = method;
                    arguments = Defaults(parameters, server);
                    arguments[1] = true;
                    break;
                }
                if (join == null && parameters.Length >= 2 && parameters[1].ParameterType == typeof(Dictionary<string, string>))
                {
                    join = method;
                    arguments = Defaults(parameters, server);
                    arguments[1] = new Dictionary<string, string>();
                }
                if (join == null && parameters.Length == 1)
                {
                    join = method;
                    arguments = new object[] { server };
                }
            }
            if ((leaveFirst && unload == null) || join == null)
            {
                HolomapPlugin.Log("the game's join flow was not found");
                return false;
            }
            if (leaveFirst)
                unload.Invoke(null, null);
            join.Invoke(null, arguments);
            return true;
        }

        // Is the game busy joining or loading a world? Read from the screens
        // it shows (its progress and loading screens). Null when the game's
        // screen list cannot be read.
        public static bool? JoinScreensOpen()
        {
            try
            {
                var screens = ScreenList();
                if (screens == null)
                    return null;
                foreach (var screen in screens)
                {
                    if (screen == null)
                        continue;
                    var name = screen.GetType().Name;
                    if (name.IndexOf("Progress", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Loading", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Join", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Download", StringComparison.Ordinal) >= 0)
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The game's own join asks for the password of a protected server in
        // a screen of its own (MyGuiScreenServerPassword). When the player
        // already typed it in the map's confirmation, it is handed to that
        // screen the way its connect button does, and the screen closes.
        // True once answered; false while the screen is not there (or cannot
        // be reached: the game's screen then simply stays for the player).
        public static bool AnswerPasswordPrompt(string password)
        {
            try
            {
                var screens = ScreenList();
                if (screens == null)
                    return false;
                foreach (var screen in screens)
                {
                    if (screen == null || screen.GetType().Name != "MyGuiScreenServerPassword")
                        continue;
                    var field = screen.GetType().GetField("m_connectAction", Instance);
                    var connect = field != null ? field.GetValue(screen) as Action<string> : null;
                    if (connect == null)
                    {
                        HolomapPlugin.Log("the game's password screen could not be answered: the player types it there");
                        return true;
                    }
                    var gui = screen as MyGuiScreenBase;
                    if (gui != null)
                        gui.CloseScreen();
                    connect(password);
                    return true;
                }
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("password not handed to the game: " + e.Message);
                return true;
            }
            return false;
        }

        private static FieldInfo s_screens;
        private static bool s_screensLooked;

        private static System.Collections.IEnumerable ScreenList()
        {
            if (!s_screensLooked)
            {
                s_screensLooked = true;
                const string managerName = "Sandbox.Graphics.GUI.MyScreenManager";
                var manager = typeof(MyGuiSandbox).Assembly.GetType(managerName);
                if (manager == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        manager = assembly.GetType(managerName);
                        if (manager != null)
                            break;
                    }
                }
                if (manager != null)
                {
                    foreach (var field in manager.GetFields(Static))
                    {
                        if (field.Name == "m_screens" && typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType))
                        {
                            s_screens = field;
                            break;
                        }
                    }
                }
                if (s_screens == null)
                    HolomapPlugin.Log("the game's screen list was not found: no way back after a failed join");
            }
            return s_screens != null ? s_screens.GetValue(null) as System.Collections.IEnumerable : null;
        }

        private static object[] Defaults(ParameterInfo[] parameters, MyGameServerItem server)
        {
            var arguments = new object[parameters.Length];
            arguments[0] = server;
            for (var i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].HasDefaultValue)
                    arguments[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType.IsValueType)
                    arguments[i] = Activator.CreateInstance(parameters[i].ParameterType);
            }
            return arguments;
        }
    }
}
