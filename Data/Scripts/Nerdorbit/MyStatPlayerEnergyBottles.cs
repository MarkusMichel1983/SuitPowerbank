using System.Linq;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Library.Utils;
using VRage.Game.ModAPI;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;

namespace Nerdorbit.SuitPowerbank
{
    public class MyStatPlayerEnergyBottles : IMyHudStat
    {
        private static readonly double CHECK_INTERVAL_MS = 1000.0;
        private static readonly MyGameTimer TIMER = new MyGameTimer();
        private double m_lastCheck = 0.0;

        private float m_currentValue;
        private string m_valueStringCache;

        public MyStringHash Id { get; protected set; }
        private static readonly MyLog Log = MyLog.Default;

        public float CurrentValue
        {
            get { return m_currentValue; }
            protected set
            {
                if (m_currentValue == value)
                {
                    return;
                }
                m_currentValue = value;
                m_valueStringCache = null;
            }
        }

        public virtual float MaxValue => 100.0f;
        public virtual float MinValue => 0.0f;

        public string GetValueString()
        {
            if (m_valueStringCache == null)
            {
                m_valueStringCache = ToString();
            }
            return m_valueStringCache;
        }

        public MyStatPlayerEnergyBottles()
        {
            this.Id = MyStringHash.GetOrCompute("player_energy_bottles");
            this.m_lastCheck = 0.0;
        }

        public void Update()
        {
            if (MyStatPlayerEnergyBottles.TIMER.ElapsedTimeSpan.TotalMilliseconds - MyStatPlayerEnergyBottles.CHECK_INTERVAL_MS < this.m_lastCheck)
                return;
            this.m_lastCheck = MyStatPlayerEnergyBottles.TIMER.ElapsedTimeSpan.TotalMilliseconds;
            IMyCharacter localCharacter = MyAPIGateway.Session.Player?.Character;
            if (localCharacter != null && !localCharacter.IsDead)
            {
                MyEntityStatComponent statComp = localCharacter.Components.Get<MyEntityStatComponent>();

                if (statComp == null)
                {
                    return;
                }
                IMyInventory inventory = localCharacter.GetInventory();
                if (inventory != null)
                {
                    this.CurrentValue = 0.0f;
                    foreach (var inventoryItem in inventory.GetItems().Where(
                        itm => itm.Content.SubtypeName.Contains("SuitPowerbank") &&
                        PowerbankUtils.CanHandlePowerbank(itm)
                        ))
                    {
                        // Multiply with 100 to make it work with the HUD
                        this.CurrentValue += (float) ((int) inventoryItem.Amount)*100;
                    }
                }
            } else
            {
                this.CurrentValue = 0.0f;
            }
        }
        public override string ToString() => string.Format("{0:0.00}", (float)(CurrentValue));
    }
}