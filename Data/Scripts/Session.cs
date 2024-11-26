using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils; 
using VRageMath;

namespace SuitPowerbank
{
   [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
   public class Session : MySessionComponentBase
   {
      public Session Instance;
      private bool isServer;
      private static int skippedTicks = 0;
      private static readonly MyLog Log = MyLog.Default;
      //private MyEntity3DSoundEmitter soundEmitter;
      private MySoundPair soundPair;
      List<IMyPlayer> players;
      
      public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
      {
         
         //Log.WriteLine("[SuitPowerbank.Item] Init");
         isServer = MyAPIGateway.Multiplayer.IsServer;
			if (!isServer)
			{
				return;
			}
         Instance = this;
         players = new List<IMyPlayer>();
         soundPair = new MySoundPair("SuitPowerbankDepleted");
      }
      
      protected override void UnloadData()
      {
         // executed when world is exited to unregister events and stuff

         Instance = null; // important for avoiding this object to remain allocated in memory
      }
     
      public override void UpdateAfterSimulation()
		{
			if (!isServer)
			{
				return;
			}
			if (skippedTicks++ <= 100)
			{
				return;
			}
			else
			{
				skippedTicks = 0;
            
            if (players != null)
            {
               int sessionPlayerCount = (int)MyAPIGateway.Players.Count;
               if (sessionPlayerCount != (int)players.Count)
               {
                  Log.WriteLine($"[SuitPowerbank.Session] currently has {(players != null ? players.Count : 0 )} players registered but {sessionPlayerCount} are in the session");
                  MyAPIGateway.Players.GetPlayers(players, p => !p.IsBot && p.Character != null);
               }
               Log.WriteLine($"[SuitPowerbank.Session] currently has {(players != null ? players.Count : 0 )} players registered ");
               foreach (var player in players)
               {
                  Log.WriteLine($"[SuitPowerbank.Session] Checking player {player.DisplayName}");
                  CheckAndUpdatePlayer(player.Character);
               }
            }
			}
		}
      
      private void CheckAndUpdatePlayer(IMyCharacter character)
      {           
         var playerid = character.ControllerInfo.ControllingIdentityId;
         if (character.IsDead)
            return;
         var elevel = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(playerid);
         var inventory = character.GetInventory();
         if (inventory != null && elevel <= 0.05)
         {
            var items = inventory.GetItems().Where(itm => itm.Content.SubtypeName.Contains("SuitPowerbank"));
            foreach (var item in items) 
            {
               if (HandlePowerbank(item, character))
               {

               }
               else
               {
                  continue;
               }
            }
         }
      }

      private bool HandlePowerbank(IMyInventoryItem item, IMyCharacter character)
      {
         var inventory = character.GetInventory();
         var suitPowerbank = item.Content as MyObjectBuilder_GasContainerObject;
         if (suitPowerbank != null)
         {
            //Log.WriteLine($"[SuitPowerbank.Session] Using suitPowerbank {item.Content.SubtypeName}");
            float fillAmount = GetFillAmountForPowerbank(item);
            if (suitPowerbank.GasLevel >= fillAmount)
            {
               //Log.WriteLine($"[SuitPowerbank.Session] {item.Content.SubtypeName} Has enough Gas to fill: {suitPowerbank.GasLevel}");
               //Log.WriteLine($"[SuitPowerbank.Session] Item Amount is: {item.Amount}");
               MyVisualScriptLogicProvider.SetPlayersEnergyLevel(character.ControllerInfo.ControllingIdentityId,1);
               
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
               //Log.WriteLine("[SuitPowerbank.Session] {item.Content.SubtypeName} gas remaining: " + suitPowerbank.GasLevel);
               CheckIfDepleted(character, suitPowerbank);
               return true;
            }
         }
         return false;
      }
            
      private void CheckIfDepleted(IMyCharacter player, MyObjectBuilder_GasContainerObject powerbank)
      {
         if (powerbank.GasLevel <= 0.01f)
         {                       
            powerbank.GasLevel = 0.0f;
            
            var playerInv = player.GetInventory();
            if (playerInv != null)
            {
               playerInv.TransferItemTo(playerInv, 0,0);
            }
            if (IsLocalPlayer(player))
            {
               MyAPIGateway.Utilities.ShowNotification("Powerbank depleted", 2000, MyFontEnum.Green);
               MyEntity3DSoundEmitter soundEmitter = new MyEntity3DSoundEmitter(MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity as MyEntity);
               soundEmitter.CustomVolume = 0.7f;
               soundEmitter.PlaySound(soundPair);
            }
         }
      }

      private bool IsLocalPlayer(IMyCharacter character)
      {
         var localPlayer = MyAPIGateway.Session?.Player?.Character;
         return localPlayer != null && localPlayer.EntityId == character.EntityId;
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