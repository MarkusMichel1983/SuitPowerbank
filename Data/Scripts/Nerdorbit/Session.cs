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
         skippedTicks = 100;
         UpdateAfterSimulation();
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
			if (skippedTicks++ < 100)
			{
				return;
			}
			else
			{
				skippedTicks = 0;
            
            if (players != null)
            {
               int sessionPlayerCount = (int)MyAPIGateway.Players?.Count;
               if (sessionPlayerCount != (int)players.Count)
               {
                  Log.WriteLine($"[SuitPowerbank.Session] currently has {(players != null ? players.Count : 0 )} players registered but {sessionPlayerCount} are in the session");
                  players.Clear();
                  charactersStats.Clear();
                  MyAPIGateway.Players?.GetPlayers(players, p => !p.IsBot && p.Character != null);
               }
               Log.WriteLine($"[SuitPowerbank.Session] currently has {(players != null ? players.Count : 0 )} players registered ");
               if (players.Count == 0)
               {
                  return;
               }
               foreach (var player in players)
               {
                  MyEntityStatComponent statComp = player.Character?.Components?.Get<MyEntityStatComponent>();
                  
                  if (statComp == null)
                  {
                     Log.WriteLine("[SuitPowerbank.Session] StatComp is null");
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

                  Log.WriteLine($"[SuitPowerbank.Session] Checking player {player.DisplayName}");
                  CheckAndUpdatePlayer(player);
               }
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
               CanHandlePowerbank(itm)
               );
            float pbCount = 0;
            foreach (var item in items)
            {
               pbCount += (float) (int) item.Amount;
            }
            Log.WriteLine($"[SuitPowerbank.Session] {player.DisplayName} has {pbCount} active powerbanks");
            return pbCount;
         }
         return 0;
      }
      
      private void CheckAndUpdatePlayer(IMyPlayer player)
      {           
         var playerid = player.Character.ControllerInfo.ControllingIdentityId;
         if (player.Character.IsDead)
            return;
         var elevel = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(playerid);
         var inventory = player.Character.GetInventory();
         if (inventory != null && elevel <= 0.05)
         {
            var items = inventory.GetItems().Where(itm => itm.Content.SubtypeName.Contains("SuitPowerbank"));
            foreach (var item in items) 
            {
               if (!CanHandlePowerbank(item))
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

      private bool CanHandlePowerbank(IMyInventoryItem item)
      {
         var suitPowerbank = item.Content as MyObjectBuilder_GasContainerObject;
         if (suitPowerbank != null)
         {
            float fillAmount = GetFillAmountForPowerbank(item);
            return suitPowerbank.GasLevel >= fillAmount;
         }
         return false;
      }

      

      private void HandlePowerbank(IMyInventoryItem item, IMyPlayer player)
      {
         var inventory = player.Character.GetInventory();
         var suitPowerbank = item.Content as MyObjectBuilder_GasContainerObject;
         if (suitPowerbank != null)
         {
            float fillAmount = GetFillAmountForPowerbank(item);
            if (suitPowerbank.GasLevel >= fillAmount)
            {
               MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.Character.ControllerInfo.ControllingIdentityId,1);
               
               // Item is part of a stack
               if (item.Amount > 1)
               {
                  Log.WriteLine($"[SuitPowerbank.Session] {item.Content.SubtypeName} Amount is {item.Amount}");
                  inventory.RemoveItemAmount(item, 1);
                  
                  var newItem = new MyObjectBuilder_InventoryItem { 
                     Content = (MyObjectBuilder_PhysicalObject)item.Content.Clone(), 
                     Amount = 1
                  };
                  // Add the new item back into the inventory
                  suitPowerbank = newItem.Content as MyObjectBuilder_GasContainerObject;
                  suitPowerbank.GasLevel -= fillAmount;
                  inventory.AddItems(1, newItem.Content);
               }
               else
               {
                  suitPowerbank.GasLevel -= fillAmount;
               }
               CheckIfDepleted(player, suitPowerbank);
            }
         }
      }
            
      private void CheckIfDepleted(IMyPlayer player, MyObjectBuilder_GasContainerObject powerbank)
      {
         if (powerbank.GasLevel <= 0.01f)
         {                       
            powerbank.GasLevel = 0.0f;
            
            var playerInv = player.Character.GetInventory();
            if (playerInv != null)
            {
               playerInv.TransferItemTo(playerInv, 0,0);
            }
            Networking.SendToPlayer(new NotifyPowerbankDepletedPacket(player.Character.ControllerInfo.ControllingIdentityId), player.SteamUserId);
         }
      }
      
      private float GetFillAmountForPowerbank(IMyInventoryItem item)
      {
         switch(item.Content.SubtypeName)
         {
            case "SuitPowerbank": 
               return 1.0f;
            case "SuitPowerbank_1":
               return 0.5f;
            case "SuitPowerbank_2":
               return 0.33f;
            case "SuitPowerbank_3":
               return 0.25f;
            default: 
               return 1.0f;
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