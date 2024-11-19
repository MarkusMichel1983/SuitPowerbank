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

namespace SuitPowerbank.Charger
{
   [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false, "SmallBlockPowerbankCharger", "LargeBlockPowerbankCharger")]
   public class SuitPowerbankCharger : MyGameLogicComponent
   {
      public static readonly MyDefinitionId EnergyId = new MyDefinitionId((MyObjectBuilderType) typeof (VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Energy");
      Sandbox.ModAPI.IMyCargoContainer charger;
      private static readonly MyLog Log = MyLog.Default;
      
      public override void Init(MyObjectBuilder_EntityBase objectBuilder)
      {
         //Log.WriteLine("[SuitPowerbank] Init");
         if (Entity is Sandbox.ModAPI.IMyCargoContainer)
         {
            charger = Entity as Sandbox.ModAPI.IMyCargoContainer;
         }
         
         //Log.WriteLine("[SuitPowerbank] was found");
         NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
      }
        
      public override void UpdateAfterSimulation100()
      {
         //Log.WriteLine("[SuitPowerbank] UpdateAfterSimulation100");
         base.UpdateAfterSimulation100();
         if (charger == null)
         {
            //Log.WriteLine("[SuitPowerbank] Charger is null");
            return;
         }
         if(charger?.CubeGrid?.Physics == null || CheckIfGridIsPowered(charger?.CubeGrid) == false) 
         {
            //Log.WriteLine("[SuitPowerbank] Grid is not powered");
            return;
         }
         if (charger != null)
         {
            //Log.WriteLine("[SuitPowerbank] Charger UpdateAfterSimulation100");
            var inv = charger.GetInventory(0);
            if (inv != null)
            {
               var items = inv.GetItems();
               //Log.WriteLine($"[SuitPowerbank] Items in inventory {items.Count()}");
               if (items != null && items.Count() > 0)
               {
                  foreach (var item in items)
                  {
                     var powerbankCell = item.Content as MyObjectBuilder_GasContainerObject;
                     if (powerbankCell == null || !item.Content.SubtypeName.Contains("SuitPowerbank"))
                     {
                        // those are not the powerbanks we're looking for
                        continue;
                     }
                        

                     MyOxygenContainerDefinition physicalItem = MyDefinitionManager.Static.GetPhysicalItemDefinition((MyObjectBuilder_Base) powerbankCell) as MyOxygenContainerDefinition;
                     if (physicalItem != null)
                     {
                        //Log.WriteLine($"[SuitPowerbank.Item] Injecting Energy ID into Item in order to prevent content being used as oxygen");
                        physicalItem.StoredGasId = EnergyId;
                     }

                     float previousGasLevel = powerbankCell.GasLevel;
                     powerbankCell.GasLevel += 0.05f;
                     if (powerbankCell.GasLevel > 1.0f)
                     {
                        powerbankCell.GasLevel = 1.0f;
                     }
                     if (powerbankCell.GasLevel != previousGasLevel) 
                     { 
                        charger.RefreshCustomInfo();
                        inv.TransferItemFrom(inv,0,0);
                        charger.UpdateVisual();
                     }
                  }               
               }
            }
         }
      }
      
      private bool CheckIfGridIsPowered(IMyCubeGrid cubeGrid)
      {
         //Log.WriteLine("[SuitPowerbank] CheckIfGridIsPowered");
         List<IMySlimBlock> blocks = new List<IMySlimBlock>();
         cubeGrid.GetBlocks(blocks, block => block.FatBlock is Sandbox.ModAPI.IMyPowerProducer);
         if (cubeGrid == null)
         {
            //Log.WriteLine("[SuitPowerbank] CubeGrid is null");
            return false;
         }
         //Log.WriteLine($"[SuitPowerbank] Blocks has {blocks.Count} blocks");  
         foreach (var block in blocks) 
         { 
            Sandbox.ModAPI.IMyPowerProducer powerProducer = block.FatBlock as Sandbox.ModAPI.IMyPowerProducer;
            if (powerProducer != null && powerProducer.Enabled && powerProducer.IsWorking) 
            {
               //Log.WriteLine("[SuitPowerbank] Grid is powered");
               return true; 
            } 
         }
         //Log.WriteLine("[SuitPowerbank] Grid is NOT powered");
         return false;
      }
   
      public override void MarkForClose()
      {
         base.MarkForClose();
      }
      
      public override void UpdatingStopped()
      {
         base.UpdatingStopped();
      }
      
      public override void Close() 
      { 
      }
   }
}