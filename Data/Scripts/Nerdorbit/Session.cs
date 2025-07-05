using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils; 
using VRageMath;

namespace Nerdorbit.SuitPowerbank
{
   public struct CharacterStats
   {
      public long playerId;
		public IMyCharacter character;
      public MyEntityStat energyBottles;
   }

   [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
   public class Session : MySessionComponentBase
   {
      public NetworkProtobuf Networking = new NetworkProtobuf(17278);// mod id: 3365172783
      public static Session Instance;
      private bool isServer;
      private static int skippedTicks = 0;
      private static readonly MyLog Log = MyLog.Default;
      List<IMyPlayer> players;
      private static List<CharacterStats> charactersStats = new List<CharacterStats>();
      
      public override void BeforeStart()
      {
         Networking.Register();
      }

      public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
      {
         isServer = MyAPIGateway.Multiplayer.IsServer;
			if (!isServer)
			{
				return;
			}
         Instance = this;
         players = new List<IMyPlayer>();
         UpdateAfterSimulation100();
      }
      
      protected override void UnloadData()
      {
         // executed when world is exited to unregister events and stuff
         Networking?.Unregister();
         Networking = null;
         Instance = null; // important for avoiding this object to remain allocated in memory
      }

      public override void UpdateAfterSimulation()
		{
			if (!isServer)
			{
				return;
			}
			if (skippedTicks++ > 100)
			{
            skippedTicks = 0;
				UpdateAfterSimulation100();
			}
		}
     
      public void UpdateAfterSimulation100()
		{      
         players.Clear();
         charactersStats.Clear();
         MyAPIGateway.Players?.GetPlayers(players);   
         if (players != null)
         {
            Debug.Log($"[SuitPowerbank] UpdateAfterSimulation100: Players count: {players.Count}");
            foreach (var player in players)
            {
               if (player.IsBot || player.Character == null || player.Character.IsDead)
               {
                  continue;
               }
               MyEntityStatComponent statComp = player.Character?.Components?.Get<MyEntityStatComponent>();
               if (statComp == null)
               {
                  continue;
               }
               MyEntityStat energyBottles;
               statComp.TryGetStat(MyStringHash.GetOrCompute("EnergyBottles"), out energyBottles);

               charactersStats.Add(new CharacterStats
               {
                  playerId = player.IdentityId,
                  character = player.Character,
                  energyBottles = energyBottles
               });
               CheckAndUpdatePlayer(player);
            }
         }
		}

      private float GetActivePowerbankCount(IMyPlayer player)
      {
         if (player.Character.IsDead)
         {
            return 0;
         }
         var inventory = player.Character.GetInventory();
         if (inventory != null)
         {
            var items = inventory.GetItems().Where(
               itm => itm.Content.SubtypeName.Contains("SuitPowerbank") &&
               PowerbankUtils.CanHandlePowerbank(itm)
               );
            float pbCount = 0;
            foreach (var item in items)
            {
               pbCount += (float) (int) item.Amount;
            }
            return pbCount;
         }
         return 0;
      }
      
      private void CheckAndUpdatePlayer(IMyPlayer player)
      {           
         var playerid = player.Character.ControllerInfo.ControllingIdentityId;
         if (player.Character.IsDead)
         {
            Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is dead");
            return;
         }

         if(IsInCockpitOrBed(player))
         {
            // no need to check for powerbanks when player is in a cockpit/bed
            Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is in a cockpit/bed");
            return;
         }
         
         var elevel = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(playerid);
         var inventory = player.Character.GetInventory();
         if (inventory != null && elevel <= Config.suitPowerbankConfig.ENERGY_THRESHOLD)
         {
            var items = inventory.GetItems().Where(itm => itm.Content.SubtypeName.Contains("SuitPowerbank"));
            foreach (var item in items) 
            {
               if (!PowerbankUtils.CanHandlePowerbank(item))
               {
                  continue;
               }
               else
               {
                  HandlePowerbank(item, player);
                  Networking.SendToPlayer(new UpdatePlayerChargePacket(playerid), player.SteamUserId);
                  return;
               }
            }
         }
      }

      private bool IsInCockpitOrBed(IMyPlayer player)
      {  
         Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is {player.Character.CurrentMovementState.ToString()}");
         if (player.Character.CurrentMovementState == MyCharacterMovementEnum.Sitting)
         {
            if (player.Controller.ControlledEntity is Sandbox.ModAPI.IMyShipController)
            {
               Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is in a ShipController");
               return true;
            }
            else if (player.Controller.ControlledEntity is Sandbox.ModAPI.IMyCockpit)
            {
               Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is in a Cockpit");
               return true;
            }
            else if (player.Controller.ControlledEntity is Sandbox.ModAPI.IMyCryoChamber)
            {
               Debug.Log($"[SuitPowerbank] Player {player.DisplayName} is in a CryoChamber");
               return true;
            }
         }
         return false;
      }

      private void HandlePowerbank(IMyInventoryItem item, IMyPlayer player)
      {
         var inventory = player.Character.GetInventory();
         if (inventory == null)
         {
            return;
         }
         var suitPowerbank = item.Content as MyObjectBuilder_GasContainerObject;
         if (suitPowerbank != null)
         {
            float fillAmount = PowerbankUtils.GetFillAmountForPowerbank(item);
            if (suitPowerbank.GasLevel >= fillAmount)
            {
               MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.Character.ControllerInfo.ControllingIdentityId,1);
               bool deleteOldItem = item.Amount == 1;
               Debug.Log($"[SuitPowerbank] {item.Content.SubtypeName} Amount is {item.Amount}");
               inventory.RemoveItemAmount(item, 1);
               
               var newItem = new MyObjectBuilder_InventoryItem { 
                  Content = (MyObjectBuilder_PhysicalObject)item.Content.Clone(), 
                  Amount = 1
               };
               // Add the new item back into the inventory
               suitPowerbank = newItem.Content as MyObjectBuilder_GasContainerObject;
               suitPowerbank.GasLevel -= fillAmount;
               inventory.AddItems(1, newItem.Content);
               Debug.Log($"[SuitPowerbank] New item GasLevel is {suitPowerbank.GasLevel}");
               if (deleteOldItem)
               {
                  inventory.RemoveItems(item.ItemId, sendEvent: true);
               }
               CheckIfDepleted(player, suitPowerbank);
            }
         }
      }
            
      private void CheckIfDepleted(IMyPlayer player, MyObjectBuilder_GasContainerObject powerbank)
      {
         if (powerbank.GasLevel <= 0.01f)
         {    
            Debug.Log($"[SuitPowerbank] Powerbank depleted for {player.DisplayName}");                   
            powerbank.GasLevel = 0.0f;
            var playerInv = player.Character.GetInventory();
            if (playerInv != null)
            {
               playerInv.TransferItemTo(playerInv, 0,0);
            }
            Networking.SendToPlayer(new NotifyPowerbankDepletedPacket(player.Character.ControllerInfo.ControllingIdentityId, Config.suitPowerbankConfig.NO_WARNING_SOUND), player.SteamUserId);
         }
      }
      
      public override MyObjectBuilder_SessionComponent GetObjectBuilder()
      {
         // executed during world save, most likely before entities.
         return base.GetObjectBuilder(); // leave as-is.
      }

      public override void UpdatingStopped()
      {
         // executed when game is paused
      }
   }
}