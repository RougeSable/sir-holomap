using System;
using System.Reflection;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI;
using VRage.GameServices;

namespace SirHolomap
{
    // Sur quel serveur le joueur se trouve : la clé de sa mémoire, son nom et
    // l'adresse avec laquelle la galaxie le place.
    internal sealed class ServerIdentity
    {
        public string Key = "";
        public string Name = "";

        // Tel que le jeu l'écrit (« steam://1.2.3.4:27016 ») : ce dont le jeu
        // a besoin pour le sonder et le rejoindre de nouveau.
        public string ConnectionString = "";

        // Seuls les serveurs dédiés peuvent être rejoints de nouveau depuis la galaxie.
        public bool IsDedicated;

        public string Normalized
        {
            get { return ServerAddress.Normalize(ConnectionString); }
        }

        // Un serveur où l'on a joué, gardé dans l'historique et sur la galaxie : un
        // serveur dédié, ou une partie rejointe par le salon du jeu.
        public bool IsRemembered
        {
            get { return IsDedicated || Key.StartsWith(LobbyPrefix, StringComparison.Ordinal); }
        }

        public const string LobbyPrefix = "lobby:";

        public static ServerIdentity Current()
        {
            var identity = new ServerIdentity();
            var session = MyAPIGateway.Session;
            var worldName = session != null ? (session.Name ?? "") : "";
            identity.Name = worldName;

            var multiplayer = MyAPIGateway.Multiplayer;
            if (multiplayer == null || !multiplayer.MultiplayerActive)
            {
                var path = session != null ? (session.CurrentPath ?? "") : "";
                identity.Key = "world:" + worldName + ":" + Hashing.Fnv1a64(path).ToString("x16");
                return identity;
            }

            if (multiplayer.IsServer)
            {
                identity.Key = "host:" + worldName;
                return identity;
            }

            var game = MyMultiplayer.Static;
            var server = DedicatedServerOf(game);
            if (server != null && !string.IsNullOrEmpty(server.ConnectionString))
            {
                identity.ConnectionString = server.ConnectionString;
                identity.IsDedicated = true;
                if (!string.IsNullOrEmpty(server.Name))
                    identity.Name = server.Name;
                identity.Key = "ds:" + ServerAddress.Normalize(server.ConnectionString);
                return identity;
            }

            if (game != null)
            {
                identity.Key = LobbyPrefix + game.ServerId;
                try
                {
                    if (!string.IsNullOrEmpty(game.HostName))
                        identity.Name = game.HostName + " - " + worldName;
                }
                catch (Exception ex)
                {
                    HolomapPlugin.Caught("ServerIdentity.Current", ex);
                }
                return identity;
            }

            identity.Key = "unknown:" + worldName;
            return identity;
        }

        // Le client d'un serveur dédié garde l'élément de serveur qu'il a rejoint dans
        // une classe interne : retrouvé par son type, jamais par un nom qui pourrait
        // changer.
        private static MyGameServerItem DedicatedServerOf(object game)
        {
            if (game == null)
                return null;
            try
            {
                foreach (var property in game.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (property.PropertyType == typeof(MyGameServerItem) && property.GetIndexParameters().Length == 0)
                        return property.GetValue(game, null) as MyGameServerItem;
                }
                foreach (var field in game.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(MyGameServerItem))
                        return field.GetValue(game) as MyGameServerItem;
                }
            }
            catch (Exception ex)
            {
                HolomapPlugin.Caught("ServerIdentity.DedicatedServerOf", ex);
            }
            return null;
        }
    }
}
