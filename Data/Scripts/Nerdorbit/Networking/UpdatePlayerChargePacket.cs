using ProtoBuf;
using Sandbox.ModAPI;
using Sandbox.Game;
using VRageMath;

namespace Nerdorbit.SuitPowerbank
{
    [ProtoContract]
    public class UpdatePlayerChargePacket : PacketBase
    {
        [ProtoMember(1)]
        public long PlayerId;

        public UpdatePlayerChargePacket() { } // Empty constructor required for deserialization

        public UpdatePlayerChargePacket(long playerId)
        {
            this.PlayerId = playerId;
        }

        public override bool Received()
        {
            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer)
            {
            }

            if (MyAPIGateway.Session.Player != null)
            {
                MyVisualScriptLogicProvider.SetPlayersEnergyLevel(this.PlayerId,1);
            }

            return true; // relay packet to other clients (only works if server receives it)
        }
    }
}