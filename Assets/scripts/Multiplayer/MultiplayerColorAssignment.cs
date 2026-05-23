using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using Photon.Realtime;

namespace Multiplayer
{
    // Server-authoritative color pairing: master gets room hostColor; joiner gets the opposite.
    // Claims myFramework as soon as the local player enters the room (lobby), so peers cannot pick the same color.
    public static class MultiplayerColorAssignment
    {
        public const string MyFrameworkProperty = MultiplayerSpawner.MyFrameworkProperty;

        public static Framework AssignForLocalPlayer(Framework hostColor)
        {
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.IsLocal) continue;
                if (player.CustomProperties.TryGetValue(MyFrameworkProperty, out object raw) && raw is int frameworkInt)
                    return Opposite((Framework)frameworkInt);
            }

            return PhotonNetwork.IsMasterClient ? hostColor : Opposite(hostColor);
        }

        public static Framework ClaimForLocalPlayer()
        {
            if (!TryGetHostColor(out Framework hostColor))
                return Framework.BLACK;

            Framework assigned = AssignForLocalPlayer(hostColor);
            if (TryGetLocalClaimedColor(out Framework existing) && existing == assigned)
                return assigned;

            var props = new Hashtable { { MyFrameworkProperty, (int)assigned } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return assigned;
        }

        public static bool TryGetLocalAssignedColor(out Framework color)
        {
            if (TryGetLocalClaimedColor(out color))
                return true;

            if (!TryGetHostColor(out Framework hostColor))
            {
                color = Framework.BLACK;
                return false;
            }

            color = AssignForLocalPlayer(hostColor);
            return true;
        }

        public static bool TryGetLocalClaimedColor(out Framework color)
        {
            color = Framework.BLACK;
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(MyFrameworkProperty, out object raw))
                return false;
            if (!(raw is int frameworkInt)) return false;
            color = (Framework)frameworkInt;
            return true;
        }

        public static string ColorLabel(Framework color)
        {
            return color == Framework.WHITE ? "White" : "Black";
        }

        private static bool TryGetHostColor(out Framework framework)
        {
            framework = Framework.BLACK;
            var props = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (props == null || !props.TryGetValue(MultiplayerSpawner.HostColorProperty, out object raw))
                return false;
            if (!(raw is int frameworkInt)) return false;
            framework = (Framework)frameworkInt;
            return true;
        }

        private static Framework Opposite(Framework f)
        {
            return f == Framework.BLACK ? Framework.WHITE : Framework.BLACK;
        }
    }
}
