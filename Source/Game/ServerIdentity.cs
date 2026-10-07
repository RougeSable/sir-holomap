using System;
using System.Reflection;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI;
using VRage.GameServices;

namespace SirHolomap
{
    // Which server the player is on: the key of its memory, its name and the
    // address the galaxy places it with.
    internal sealed class ServerIdentity
    {
        public string Key = "";
        public string Name = "";

        // As the game writes it ("steam://1.2.3.4:27016"): what the game
        // needs to ping and join it again.
        public string ConnectionString = "";

        // Dedicated servers only can be joined again from the galaxy.
        public bool IsDedicated;

        public string Normalized
        {
            get { return ServerAddress.Normalize(ConnectionString); }
        }

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
                identity.Key = "lobby:" + game.ServerId;
                try
                {
                    if (!string.IsNullOrEmpty(game.HostName))
                        identity.Name = game.HostName + " - " + worldName;
                }
                catch (Exception)
                {
                }
                return identity;
            }

            identity.Key = "unknown:" + worldName;
            return identity;
        }

        // The client of a dedicated server keeps the server item it joined in
        // an internal class: found by its type, never by a name that could
        // change.
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
            catch (Exception)
            {
            }
            return null;
        }
    }
}
