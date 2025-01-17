using ProtoBuf;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRageMath;

namespace Nerdorbit.SuitPowerbank
{
    [ProtoContract]
    public class NotifyPowerbankDepletedPacket : PacketBase
    {
        [ProtoMember(1)]
        public long PlayerId;

        private MySoundPair soundPair = new MySoundPair("SuitPowerbankDepleted");

        public NotifyPowerbankDepletedPacket() { } // Empty constructor required for deserialization

        public NotifyPowerbankDepletedPacket(long playerId)
        {
            this.PlayerId = playerId;
        }

        public override bool Received()
        {
            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer)
            {
            }
            IMyPlayer player = MyAPIGateway.Players.TryGetIdentityId(PlayerId);
            if (player != null)
            {
               MyAPIGateway.Utilities.ShowNotification("Powerbank depleted", 2000, MyFontEnum.Green);
               MyEntity3DSoundEmitter soundEmitter = new MyEntity3DSoundEmitter(player.Controller.ControlledEntity as MyEntity);
               soundEmitter.CustomVolume = 0.5f;
               soundEmitter.PlaySound(soundPair);
            }

            return true; // relay packet to other clients (only works if server receives it)
        }
    }
}