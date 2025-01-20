using System;
using VRage.Game.ModAPI;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;

namespace Nerdorbit.SuitPowerbank
{
    public static class PowerbankUtils
    {
        public static float GetFillAmountForPowerbank(IMyInventoryItem item)
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

        public static bool CanHandlePowerbank(IMyInventoryItem item)
        {
            var suitPowerbank = item.Content as MyObjectBuilder_GasContainerObject;
            if (suitPowerbank != null)
            {
                float fillAmount = GetFillAmountForPowerbank(item);
                return suitPowerbank.GasLevel >= fillAmount;
            }
            return false;
        }
    }
}