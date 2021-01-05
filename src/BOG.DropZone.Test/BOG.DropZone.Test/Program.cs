using System;
using System.Threading;
using System.Threading.Tasks;
using BOG.DropZone.Client;
using BOG.DropZone.Client.Model;
using Newtonsoft.Json;

namespace BOG.DropZone.Test
{
	public class WorkerComm
	{
		public string RecipientID { get; set; }
		public string Message { get; set; }
	}

	class Program
	{

		static void Main()
		{
			RunAsync().GetAwaiter().GetResult();
		}

		static async Task RunAsync()
		{
			const string Access = "YourAccessTokenValueHere";
			const string Admin = "YourAdminTokenValueHere";

			var zone = new DropZoneConfig
			{
				BaseUrl = "http://localhost:5000",
				ZoneName = "Update",
				Password = string.Empty,
				Salt = string.Empty,
				UseEncryption = false,
				AccessToken = Access,
				AdminToken = Admin,
				TimeoutSeconds = 10
			};

			var restApiUpdateMaster = new RestApiCalls(zone);

			try
			{
				Console.WriteLine("CheckHeartbeat()...");
				Result result = await restApiUpdateMaster.CheckHeartbeat();
				DisplayResult(result, 100);
				if (result.HandleAs == Result.State.InvalidAuthentication)
				{
					Console.WriteLine("Invalid auth token, testing halted... ");
					return;
				}

				Console.WriteLine("Reset()...");
				result = await restApiUpdateMaster.Reset();
				DisplayResult(result, 100);
				if (result.HandleAs == Result.State.InvalidAuthentication)
				{
					Console.WriteLine("Invalid admin token, testing halted... ");
					return;
				}

				Console.WriteLine("ListReferences()... ");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("GetReference()... ");
				result = await restApiUpdateMaster.GetReference("Test-Ref01");
				DisplayResult(result, 1000);

				Console.WriteLine("ListReferences()... ");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("SetReference()... ");
				result = await restApiUpdateMaster.SetReference("Test-Ref01", "test ref 1");
				DisplayResult(result, 1000);

				Console.WriteLine("ListReferences()... ");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("GetReference()... ");
				result = await restApiUpdateMaster.GetReference("Test-Ref01");
				DisplayResult(result, 1000);

				Console.WriteLine("SetReference()... Perishable");
				result = await restApiUpdateMaster.SetReference("Test-Ref02-Perish-5-Sec", "this will go way", DateTime.Now.AddSeconds(5));
				DisplayResult(result, 1000);

				Console.WriteLine("ListReferences()... shows static and perishable");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("GetReference()... Perishable ... < 1 sec (finds it)");
				result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
				DisplayResult(result, 1000);

				Console.WriteLine("Wait 5 seconds for reference to expire...");
				Thread.Sleep(5000);

				Console.WriteLine("ListReferences()... shows static only... perishable has expired");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("GetReference()... Perishable ... > 5 sec (perished)");
				result = await restApiUpdateMaster.GetReference("RefTimed-Good");
				DisplayResult(result, -1);

				Console.WriteLine("SetReference()");
				result = await restApiUpdateMaster.SetReference("Test-Ref03", "this will go way");

				Console.WriteLine("GetReference()");
				result = await restApiUpdateMaster.GetReference("Test-Ref03");

				Console.WriteLine("DropReference()");
				result = await restApiUpdateMaster.DropReference("Test-Ref03");

				Console.WriteLine("GetReference()");
				result = await restApiUpdateMaster.GetReference("Test-Ref03");
				DisplayResult(result, -1);

				Console.WriteLine("SetReference()... BIG...");
				var bigData = new string('X', 524288);
				Console.WriteLine($"{bigData.Length}");
				result = await restApiUpdateMaster.SetReference("BIG", bigData);
				DisplayResult(result, 1000);

				Console.WriteLine("GetReference()... BIG...");
				result = await restApiUpdateMaster.GetReference("BIG");
				Console.WriteLine($"{result.Message.Length}");
				result.Message = string.Empty;
				DisplayResult(result, 1000);

				Console.WriteLine("SetReference()... HUGE... rejected due to size");
				var hugeData = new string('X', 41000000);
				Console.WriteLine($"{hugeData.Length}");
				result = await restApiUpdateMaster.SetReference("HUGE", hugeData);
				DisplayResult(result, 1000);

				Console.WriteLine("GetReference()... HUGE...");
				result = await restApiUpdateMaster.GetReference("HUGE");
				Console.WriteLine($"{result.Message.Length}");
				result.Message = string.Empty;
				DisplayResult(result, -1);

				Console.WriteLine("GetStatistics()... ");
				result = await restApiUpdateMaster.GetStatistics();
				DisplayResult(result, -1);
				Console.WriteLine("GetSecurityInfo()... ");
				result = await restApiUpdateMaster.GetSecurity();
				DisplayResult(result, -1);

				// Payloads and recipients

				// Test a payload with and without a specific recipient

				Console.WriteLine("*** Drop off to global (1) and to a specific recipient (1)");
				Console.WriteLine($"Add \"Global payload\" for global queue in the zone ... ");
				result = await restApiUpdateMaster.DropOff("Global payload");
				DisplayResult(result, 0);

				var recipient = "Tim";
				Console.WriteLine($"Add \"Tim's payload\" for {recipient} queue in the zone ... ");
				result = await restApiUpdateMaster.DropOff("Tim's payload", recipient);
				DisplayResult(result, 0);

				Console.WriteLine($"Retrieve payload for recipient {recipient} ...");
				result = await restApiUpdateMaster.Pickup(recipient);
				DisplayResult(result, 0);

				Console.WriteLine($"Retrieve payload for recipient {recipient} ... should have nothing");
				result = await restApiUpdateMaster.Pickup(recipient);
				DisplayResult(result, 0);

				Console.WriteLine($"Retrieve payload for global use ...");
				result = await restApiUpdateMaster.Pickup();
				DisplayResult(result, 0);

				Console.WriteLine($"Retrieve payload for global use ... should have nothing");
				result = await restApiUpdateMaster.Pickup();
				DisplayResult(result, -1);

				// Other payloads tests.

				string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

				Console.WriteLine("*** Drop off some data .. which expires ***");
				for (int index = 0; index < 5; index++)
				{
					Console.WriteLine($"Add {index}: {set[index % 3]}... ");
					result = await restApiUpdateMaster.DropOff(set[index % 3] + $"{index}", DateTime.Now.AddSeconds(3 + index));
					DisplayResult(result, 0);
				}

				Console.WriteLine("Wait 5 for reference to expire...");
				Thread.Sleep(5000);

				Console.WriteLine("*** Pickup some data .. some of which has expired ***");
				for (int index = 0; index < 5; index++)
				{
					Console.WriteLine($"Retrieve {index} from {set[index % 3]}: ");
					result = await restApiUpdateMaster.Pickup();
					DisplayResult(result, 0);
				}

				Console.WriteLine("*** Clear ***");
				result = await restApiUpdateMaster.Clear();
				DisplayResult(result, -1);

				Console.WriteLine("*** Drop off some data.. places 501 items when 500 is the max ***");
				//for (int index = 0; index < 501; index++)
				for (int index = 0; index < 101; index++)
				{
					Console.WriteLine($"Add {index}: {set[index % 3]}... ");
					result = await restApiUpdateMaster.DropOff(set[index % 3]);
					DisplayResult(result, index < 5 ? 1000 : 0);
				}
				Console.WriteLine("GetStatistics()...");
				result = await restApiUpdateMaster.GetStatistics();
				DisplayResult(result, -1);

				Console.WriteLine("*** Pickup some data ***");
				//for (int index = 0; index < 502; index++)
				for (int index = 0; index < 102; index++)
				{
					Console.WriteLine($"Retrieve {index} from {set[index % 3]}: ");
					result = await restApiUpdateMaster.Pickup();
					DisplayResult(result, index == 2 ? -1 : (index < 5 ? 1000 : 0));
				}

				Console.WriteLine("*** List references ***");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("Overload references...");
				for (int index = 0; index < 48; index++)
				{
					Console.WriteLine($"SetReference(Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}... ");
					result = await restApiUpdateMaster.SetReference($"Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}", $"test ref {index}");
					DisplayResult(result, 0);
				}

				Console.WriteLine("*** List references after overload ***");
				result = await restApiUpdateMaster.ListReferences();
				DisplayResult(result, -1);

				Console.WriteLine("GetStatistics()... ");
				result = await restApiUpdateMaster.GetStatistics();
				DisplayResult(result, -1);

				Console.WriteLine("*** Reset ***");
				result = await restApiUpdateMaster.Reset();
				DisplayResult(result, 2000);

				Console.WriteLine("*** Shutdown ***");
				result = await restApiUpdateMaster.Shutdown();
				DisplayResult(result, 2000);

				Console.WriteLine("*** Shutdown (with no answer) ***");
				result = await restApiUpdateMaster.Shutdown();
				DisplayResult(result, 2000);
			}
			catch (Exception err)
			{
				Console.WriteLine($"Untrapped: {err.Message}");
			}

			Console.WriteLine("Done");
			Console.ReadLine();
		}

		static void DisplayResult(Result result, int waitMS)
		{
			Console.WriteLine(Serializer<Result>.ToJson(result));
			if (result.CastType != null && result.CastType != "System.String")
			{
				DisplayString("* * * JSON PAYLOAD * * *", 0);
				var work = JsonConvert.DeserializeObject(result.Content, Type.GetType(result.CastType));
				var visual = JsonConvert.SerializeObject(
						work,
						Type.GetType(result.CastType),
						Formatting.Indented, BOG.DropZone.Test.Converter.Config);
				DisplayString(visual, 0);
			}
			Console.WriteLine();
			switch (waitMS)
			{
				case -1:
					Console.WriteLine("Press ENTER to clear");
					Console.ReadLine();
					break;

				case 0:
					break;

				default:
					System.Threading.Thread.Sleep(waitMS);
					break;
			}
		}

		static void DisplayString(string result, int waitMS)
		{
			Console.WriteLine(result);
			Console.WriteLine();
			switch (waitMS)
			{
				case -1:
					Console.WriteLine("Press ENTER to clear");
					Console.ReadLine();
					break;

				case 0:
					break;

				default:
					System.Threading.Thread.Sleep(waitMS);
					break;
			}
		}
	}

	public static class Serializer<T> where T : class
	{
		public static T FromJson(string json) => JsonConvert.DeserializeObject<T>(json, Converter.Config);

		public static string ToJson(T obj) => JsonConvert.SerializeObject(obj, typeof(T), Converter.Config);
	}

	/// <summary>
	/// Standard serializtion settings for JSON.
	/// </summary>
	public static class Converter
	{
		public static readonly JsonSerializerSettings Config = new JsonSerializerSettings
		{
			MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
			DateParseHandling = DateParseHandling.DateTime,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented
		};
	}
}
