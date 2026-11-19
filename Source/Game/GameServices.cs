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
    // Le navigateur de serveurs du jeu et sa connexion, atteints par leur nom à
    // l'exécution : ces parties du jeu changent d'une version à l'autre, et un nom qui
    // n'y est plus doit seulement éteindre la fonction correspondante (avec une
    // ligne dans le journal), jamais empêcher le greffon de se charger.
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
                HolomapPlugin.LogCaught("découverte des serveurs introuvable : " + e.Message, e);
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.Flag", ex);
                return false;
            }
        }

        // La découverte du jeu est souvent une interface sur plusieurs services :
        // les membres sont cherchés sur la classe et sur chaque interface qu'elle a.
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

        // Abonne un gestionnaire « void (object sender, int index) » à un événement de
        // la découverte. Rend le délégué avec lequel se désabonner.
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
                HolomapPlugin.LogCaught("impossible d'écouter " + eventName + " : " + e.Message, e);
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.Unsubscribe", ex);
            }
        }

        // Demande une liste avec un filtre de recherche vide.
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
                HolomapPlugin.LogCaught(methodName + " failed: " + e.Message, e);
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.Call", ex);
            }
        }

        public static MyGameServerItem Details(object discovery, string methodName, int index)
        {
            try
            {
                var method = FindMethod(discovery.GetType(), methodName, 1);
                return method != null ? method.Invoke(discovery, new object[] { index }) as MyGameServerItem : null;
            }
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.Details", ex);
                return null;
            }
        }

        // Sonder un serveur, comme le fait le jeu pour « +connect » : la réponse arrive
        // sous forme d'événement.
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
                HolomapPlugin.LogCaught("sondage échoué : " + e.Message, e);
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.StopPing", ex);
            }
        }

        // Quitte le serveur actuel pour le menu principal, puis rejoint l'autre,
        // comme le fait le navigateur de serveurs du jeu.
        // leaveFirst : faux quand on est déjà au menu principal (le chemin du retour).
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
                // La surcharge qui demande d'abord ses règles au serveur.
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
                HolomapPlugin.LogError("la connexion du jeu est introuvable");
                return false;
            }
            if (leaveFirst)
                unload.Invoke(null, null);
            join.Invoke(null, arguments);
            return true;
        }

        // Le jeu est-il occupé à rejoindre ou à charger un monde ? Lu d'après les écrans
        // qu'il montre (ses écrans de progression et de chargement). Null quand la liste
        // des écrans du jeu ne peut pas être lue.
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("GameServices.JoinScreensOpen", ex);
                return null;
            }
        }

        // La connexion du jeu demande le mot de passe d'un serveur protégé dans
        // un écran à elle (MyGuiScreenServerPassword). Quand le joueur l'a déjà
        // tapé dans la confirmation de la carte, il est remis à cet écran
        // comme le fait son bouton de connexion, et l'écran se ferme.
        // Vrai une fois répondu ; faux tant que l'écran n'est pas là (ou ne peut pas
        // être atteint : l'écran du jeu reste alors simplement au joueur).
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
                        HolomapPlugin.LogError("l'écran de mot de passe du jeu n'a pas pu être rempli : le joueur le tape là");
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
                HolomapPlugin.LogCaught("mot de passe non remis au jeu : " + e.Message, e);
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
                    HolomapPlugin.LogError("la liste des écrans du jeu est introuvable : pas de retour après une connexion échouée");
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
