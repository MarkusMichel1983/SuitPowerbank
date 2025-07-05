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

namespace Nerdorbit.SuitPowerbank
{
   [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false, "SmallBlockPowerbankCharger", "LargeBlockPowerbankCharger")]
   public class SuitPowerbankCharger : MyGameLogicComponent
   {
      public static readonly MyDefinitionId EnergyId = new MyDefinitionId((MyObjectBuilderType) typeof (VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Energy");
      Sandbox.ModAPI.IMyCargoContainer charger;
      private static readonly MyLog Log = MyLog.Default;

      private const string EMISSIVE_MATERIAL_NAME = "Emissive";
      private Color GREEN = new Color(0, 255, 0);
      private Color LIGHTBLUE = new Color(0, 255, 255, 255);
      private Color RED = new Color(255, 0, 0);
      private Color WHITE = new Color(255, 255, 255);
      private bool isCharging = false;
      
      public override void Init(MyObjectBuilder_EntityBase objectBuilder)
      {
         if (Entity is Sandbox.ModAPI.IMyCargoContainer)
         {
            charger = Entity as Sandbox.ModAPI.IMyCargoContainer;
            charger?.SetEmissiveParts(EMISSIVE_MATERIAL_NAME, WHITE, 0.75f);
         }
         NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
         base.Init(objectBuilder);
      }
        
      public override void UpdateAfterSimulation100()
      {
         base.UpdateAfterSimulation100();
         if (charger == null)
         {
            return;
         }
         bool isPowered = CheckIfGridIsPowered(charger?.CubeGrid);
         if(charger?.CubeGrid?.Physics == null || isPowered == false) 
         {
            Debug.Log($"[SuitPowerbank] Grid is not powered, skipping charging");
            return;
         }
         if (charger != null)
         {
            var inv = charger.GetInventory(0);
            if (inv != null)
            {
               var items = inv.GetItems();
               if (items != null && items.Count() > 0)
               {
                  isCharging = CheckIfChargeableItems(items);
                  Debug.Log($"[SuitPowerbank] is charging: {isCharging}, items count is: {items.Count()}");
                  foreach (var item in items)
                  {
                     var powerbankCell = item.Content as MyObjectBuilder_GasContainerObject;
                     if (powerbankCell == null || !item.Content.SubtypeName.Contains("SuitPowerbank"))
                     {
                        // those are not the powerbanks we're looking for
                        continue;
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
               else
               {
                  isCharging = false;
               }
            }
         }
      }

      private bool CheckIfChargeableItems(List<IMyInventoryItem> items)
      {
         return items.Any(item => item.Content.SubtypeName.Contains("SuitPowerbank") && (item.Content as MyObjectBuilder_GasContainerObject).GasLevel < 1.0f);
      }
      
      private bool CheckIfGridIsPowered(IMyCubeGrid cubeGrid)
      {
         try 
         {
            if (CheckGridForPowerProducers(cubeGrid))
            {
               charger?.SetEmissiveParts(EMISSIVE_MATERIAL_NAME, (isCharging ? LIGHTBLUE : GREEN), 0.75f);
               return true;
            }   
         }
         catch (Exception ex)
         {
            Debug.Log($"[SuitPowerbank] Exception when crawling connected grids: {ex}, {ex.Message}");
         }
         charger?.SetEmissiveParts(EMISSIVE_MATERIAL_NAME, RED, 0.75f);
         return false;
      }

      private bool CheckGridForPowerProducers(IMyCubeGrid cubeGrid, Sandbox.ModAPI.IMyShipConnector excludeConnector = null, 
      Sandbox.ModAPI.IMyMechanicalConnectionBlock excludeMechanicalBlock = null,
      Sandbox.ModAPI.IMyAttachableTopBlock excludeAttachableTopBlock = null)
      {
         if (cubeGrid == null)
         {
            return false;
         }
         List<IMySlimBlock> blocks = new List<IMySlimBlock>();
         cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock is Sandbox.ModAPI.IMyPowerProducer);
         // Check all blocks on the current grid for active power producers
         foreach (var block in blocks) 
         { 
            Sandbox.ModAPI.IMyPowerProducer powerProducer = block.FatBlock as Sandbox.ModAPI.IMyPowerProducer;
            if (powerProducer != null && powerProducer.Enabled && powerProducer.IsWorking) 
            {
               return true; 
            }
         }
         cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock is Sandbox.ModAPI.IMyShipConnector);
         foreach (var block in blocks)
         {
            Sandbox.ModAPI.IMyShipConnector connector = block.FatBlock as Sandbox.ModAPI.IMyShipConnector;
            if (connector != null && connector.Status == MyShipConnectorStatus.Connected) 
            {
               // exclude checking the connector that is connected to the current grid
               if (excludeConnector != null && connector.EntityId == excludeConnector.EntityId)
               {
                  continue;
               }
               Sandbox.ModAPI.IMyShipConnector otherConnector = connector.OtherConnector;
               if (otherConnector != null && otherConnector.Status == MyShipConnectorStatus.Connected && otherConnector.CubeGrid != null) 
               {
                  return CheckGridForPowerProducers(otherConnector.CubeGrid, excludeConnector: otherConnector);
               }
            }
         }
         // Check for connected grids via rotors, pistons, and hinges (lower part)
         cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock is Sandbox.ModAPI.IMyMechanicalConnectionBlock);
         foreach (var block in blocks)
         {
            if (excludeMechanicalBlock != null && block.FatBlock.EntityId == excludeMechanicalBlock.EntityId)
            {
               continue;
            }
            Sandbox.ModAPI.IMyMechanicalConnectionBlock mechanicalBlock = block.FatBlock as Sandbox.ModAPI.IMyMechanicalConnectionBlock;
            if (mechanicalBlock != null && mechanicalBlock.TopGrid != null)
            {
               return CheckGridForPowerProducers(mechanicalBlock.TopGrid, excludeAttachableTopBlock: mechanicalBlock.Top);
            }
         }
         // Check for connected grids via rotors, pistons, and hinges (attachable part)
         cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock is Sandbox.ModAPI.IMyAttachableTopBlock );
         foreach (var block in blocks)
         {
            if (excludeAttachableTopBlock != null && block.FatBlock.EntityId == excludeAttachableTopBlock.EntityId)
            {
               continue;
            }
            Sandbox.ModAPI.IMyAttachableTopBlock  attachableTopBlock = block.FatBlock as Sandbox.ModAPI.IMyAttachableTopBlock;
            if (attachableTopBlock != null && attachableTopBlock.Base != null)
            {
               return CheckGridForPowerProducers(attachableTopBlock.Base.CubeGrid, excludeMechanicalBlock: attachableTopBlock.Base);
            }
         }
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