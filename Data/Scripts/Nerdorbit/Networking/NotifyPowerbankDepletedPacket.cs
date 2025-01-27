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
        [ProtoMember(2)]
        public bool NoSound;

        private MySoundPair soundPair = new MySoundPair("SuitPowerbankDepleted");

        public NotifyPowerbankDepletedPacket() { } // Empty constructor required for deserialization

        public NotifyPowerbankDepletedPacket(long playerId, bool noSound)
        {
            this.PlayerId = playerId;
            this.NoSound = noSound;
        }

        public override bool Received()
        {
            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer)
            {
            }
            IMyPlayer player = MyAPIGateway.Players.TryGetIdentityId(PlayerId);
            if (player != null)
            {
                var playerInv = player.Character.GetInventory();
                if (playerInv != null)
                {
                    playerInv.TransferItemTo(playerInv, 0,0);
                }
                MyAPIGateway.Utilities.ShowNotification("Powerbank depleted", 2000, MyFontEnum.Green);
                if (!NoSound)
                {
                    MyEntity3DSoundEmitter soundEmitter = new MyEntity3DSoundEmitter(player.Controller.ControlledEntity as MyEntity);
                    soundEmitter.CustomVolume = 0.5f;
                    soundEmitter.PlaySound(soundPair);
                }
            }

            return true; // relay packet to other clients (only works if server receives it)
        }
    }
}