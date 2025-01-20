using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.Components;

namespace Nerdorbit.SuitPowerbank
{
	public struct SuitPowerbankConfig
	{
		public float ENERGY_THRESHOLD;
		public bool DEBUG;
	}

	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Config : MySessionComponentBase
	{
		public static SuitPowerbankConfig suitPowerbankConfig = new SuitPowerbankConfig()
		{
            ENERGY_THRESHOLD = 0.05f,
            DEBUG = false
		};

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				return;
			}
			try
			{
				string configFileName = "SuitPowerbankConfig.xml";
				if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFileName, typeof(SuitPowerbankConfig)))
				{
					var textReader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFileName, typeof(SuitPowerbankConfig));
					var configXml = textReader.ReadToEnd();
					textReader.Close();
					suitPowerbankConfig = MyAPIGateway.Utilities.SerializeFromXML<SuitPowerbankConfig>(configXml);
				}
				else
				{
					var textWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName, typeof(SuitPowerbankConfig));
					textWriter.Write(MyAPIGateway.Utilities.SerializeToXML(suitPowerbankConfig));
					textWriter.Flush();
					textWriter.Close();
				}
			}
			catch (Exception e)
			{
				MyAPIGateway.Utilities.ShowMessage("EDSR", "Exception: " + e);
			}
		}
	}
}
