using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BOG.DropZone.Client;
using BOG.DropZone.Client.Model;
using BOG.DropZone.Common.Dto;
using Newtonsoft.Json;

namespace BOG.DropZone.Test
{

	// Functions as both an example, and a functional test.
	// - Make this the default startup project
	// - open a command prompt and start BOG.DropZone externally on the host with start_me.bat/.sh
	public class WorkerComm
	{
		public string RecipientID { get; set; }
		public string Message { get; set; }
	}

	class Program
	{

		static void Main()
		{
			//RunAsync().GetAwaiter().GetResult();
			UnitTestAsync().GetAwaiter().GetResult();
		}

#if FALSE
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
				TimeoutSeconds = 600
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

				Console.WriteLine("SetMetrics()...");
				result = await restApiUpdateMaster.SetMetrics(new Common.Dto.DropZoneMetrics
				{
					MaxPayloadCount = 500,
					MaxPayloadSize = 50L * 1024L * 1024L,
					MaxReferencesCount = 100,
					MaxReferenceSize = 50L * 1024L * 1024L
				});
				DisplayResult(result, -1);

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
				var tracking = "ABC123";
				Console.WriteLine($"Add \"Tim's payload\" for {recipient} queue in the zone ... ");
				result = await restApiUpdateMaster.DropOff("Tim's payload", new PayloadMetadata { 
					Recipient = recipient,
					Tracking = tracking
				});
				DisplayResult(result, 0);

				Console.WriteLine($"Query payload tracking number for recipient {recipient} ...");
				result = await restApiUpdateMaster.Inquiry(tracking, recipient, null);
				DisplayResult(result, 0);

				Console.WriteLine($"Query payload bad tracking number for recipient {recipient} ...");
				result = await restApiUpdateMaster.Inquiry(tracking+"X", recipient, null);
				DisplayResult(result, -1);

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
					result = await restApiUpdateMaster.DropOff(set[index % 3] + $"{index}",
						new PayloadMetadata
						{
							ExpiresOn = DateTime.Now.AddSeconds(3 + index)
						});
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
#endif

		static async Task UnitTestAsync()
		{
			Console.WriteLine("Setup()...");
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
				TimeoutSeconds = 600
			};

			var restApiUpdateMaster = new RestApiCalls(zone);
			var testPassed = true;

			try
			{
				Console.Write($"CheckHeartbeat(): ");
				Result result = await restApiUpdateMaster.CheckHeartbeat();
				Console.WriteLine($"{result.HandleAs}");
				if (result.HandleAs != Result.State.OK)
				{
					Console.WriteLine();
					UnexpectedResult("No response to heartbeat call, testing halted... ");
					return;
				}

				Console.Write("Reset(): ");
				result = await restApiUpdateMaster.Reset();
				Console.WriteLine($"{result.HandleAs}");
				if (result.HandleAs != Result.State.OK)
				{
					UnexpectedResult("Reset failed, testing halted... ");
					return;
				}

				Console.Write("SetMetrics(): ");
				var metrics = new Common.Dto.DropZoneMetrics
				{
					MaxPayloadCount = 500,
					MaxPayloadSize = 5L * 1024L * 1024L,
					MaxReferencesCount = 100,
					MaxReferenceSize = 5L * 1024L * 1024L
				};
				result = await restApiUpdateMaster.SetMetrics(metrics);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected : NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetMetrics(): ");
				result.Message = string.Empty;
				result = await restApiUpdateMaster.GetStatistics();
				Console.WriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var obj = JsonConvert.DeserializeObject<DropZoneInfo>(result.Content).Metrics;
					// DropZoneInfo obj = (DropZoneInfo) JsonConvert.DeserializeObject(result.Content, Type.GetType(result.CastType)); 
					Console.Write("  MaxPayloadCount: ");
					Console.WriteLine(obj.MaxPayloadCount == metrics.MaxPayloadCount ? "pass" : $"FAIL, expected: 0, got {obj.MaxPayloadCount}");
					testPassed &= obj.MaxPayloadCount == metrics.MaxPayloadCount;
					Console.Write("  MaxPayloadSize: ");
					Console.WriteLine(obj.MaxPayloadSize == metrics.MaxPayloadSize ? "pass" : $"FAIL, expected: 0, got {obj.MaxPayloadSize}");
					testPassed &= obj.MaxPayloadSize == metrics.MaxPayloadSize;
					Console.Write("  MaxReferencesCount: ");
					Console.WriteLine(obj.MaxReferencesCount == metrics.MaxReferencesCount ? "pass" : $"FAIL, expected: 0, got {obj.MaxReferencesCount}");
					testPassed &= obj.MaxReferencesCount == metrics.MaxReferencesCount;
					Console.Write("  MaxReferenceSize: ");
					Console.WriteLine(obj.MaxReferenceSize == metrics.MaxReferenceSize ? "pass" : $"FAIL, expected: 0, got {obj.MaxReferenceSize}");
					testPassed &= obj.MaxReferenceSize == metrics.MaxReferenceSize;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences(): ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 0;
					Console.WriteLine(list.Count == 0 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetReference() [empty]: ");
				result = await restApiUpdateMaster.GetReference("Test-Ref01");
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("SetReference(): ");
				result = await restApiUpdateMaster.SetReference("Test-Ref01", "test ref 1");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences(): ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 1;
					Console.WriteLine(list.Count == 1 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetReference() [found]:  ");
				result = await restApiUpdateMaster.GetReference("Test-Ref01");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Value check... ");
					testPassed &= result.Content == "test ref 1";
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: test ref 1, got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("SetReference()... Perishable: ");
				result = await restApiUpdateMaster.SetReference("Test-Ref02-Perish-5-Sec", "this will go way", DateTime.Now.AddSeconds(5));
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences()... shows static and perishable: ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 2;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetReference()... Perishable ... < 1 sec (finds it): ");
				result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Value check... ");
					testPassed &= result.Content == "this will go way";
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: this will go way, got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Wait 5 seconds for reference to expire...");
				Thread.Sleep(5000);

				Console.Write("ListReferences()... shows static only... perishable has expired: ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 1;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetReference()... Perishable ... > 5 sec (perished): ");
				result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				{
					var bigData = new string('X', 524288);   // 512K of X's... not over limit.
					Console.Write($"SetReference()... BIG...{bigData.Length}: ");
					result = await restApiUpdateMaster.SetReference("BIG", bigData);
					testPassed = result.HandleAs == Result.State.OK;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					if (!testPassed) UnexpectedResult();
					testPassed = true;

					Console.Write($"GetReference()... BIG...:");
					result = await restApiUpdateMaster.GetReference("BIG");
					Console.Write($"{result.Message.Length}: ");
					testPassed = result.HandleAs == Result.State.OK;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					if (testPassed)
					{
						Console.Write("  Value check... ");
						testPassed &= result.Content == bigData;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: test ref 1, got: {result.Content}");
					}
					if (!testPassed) UnexpectedResult();
					testPassed = true;
				}

				result.Message = string.Empty;
				{
					var messageTest = "Payload Too Large";
					var hugeData = new string('X', 41000000);
					Console.Write($"SetReference()... HUGE... rejected due to size {hugeData.Length}: ");
					result = await restApiUpdateMaster.SetReference("HUGE", hugeData);
					testPassed = result.HandleAs == Result.State.UnexpectedResponse;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: UnexpectedResponse, got: {result.HandleAs}");
					if (!testPassed) UnexpectedResult();
					testPassed = true;

					Console.Write("GetReference()... HUGE...");
					result = await restApiUpdateMaster.GetReference("HUGE");
					testPassed = result.HandleAs == Result.State.NoDataAvailable;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
					if (!testPassed) UnexpectedResult();
					testPassed = true;

					result.Message = string.Empty;
				}

				Console.Write("GetStatistics(): ");
				result = await restApiUpdateMaster.GetStatistics();
				Console.WriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var obj = JsonConvert.DeserializeObject<DropZoneInfo>(result.Content);
					// DropZoneInfo obj = (DropZoneInfo) JsonConvert.DeserializeObject(result.Content, Type.GetType(result.CastType)); 
					Console.Write("  PayloadCount: ");
					Console.WriteLine(obj.PayloadCount == 0 ? "pass" : $"FAIL, expected: 0, got {obj.PayloadCount}");
					testPassed &= obj.PayloadCount == 0;
					Console.Write("  ReferenceCount: ");
					Console.WriteLine(obj.ReferenceCount == 2 ? "pass" : $"FAIL, expected: 2, got {obj.PayloadCount}");
					testPassed &= obj.ReferenceCount == 2;
					Console.Write("  PayloadExpiredCount: ");
					Console.WriteLine(obj.PayloadExpiredCount == 0 ? "pass" : $"FAIL, expected: 1, got {obj.PayloadExpiredCount}");
					testPassed &= obj.PayloadExpiredCount == 0;
					Console.Write("  ReferenceExpiredCount: ");
					Console.WriteLine(obj.ReferenceExpiredCount == 1 ? "pass" : $"FAIL, expected: 1, got {obj.ReferenceExpiredCount}");
					testPassed &= obj.ReferenceExpiredCount == 1;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("GetSecurityInfo(): ");
				result = await restApiUpdateMaster.GetSecurity();
				Console.WriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var obj = JsonConvert.DeserializeObject<List<ClientWatch>>(result.Content);
					Console.Write("  TotalClients: ");
					Console.WriteLine(obj.Count == 1 ? "pass" : $"FAIL, expected: 0, got {obj.Count}");
					testPassed &= obj.Count == 1;

					Console.Write("  AccessAttemptsTotalCount: ");
					Console.WriteLine(obj[0].AccessAttemptsTotalCount == 17 ? "pass" : $"FAIL, expected: 0, got {obj[0].AccessAttemptsTotalCount}");
					testPassed &= obj[0].AccessAttemptsTotalCount == 17;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// DONE THROUGH HERE for testPassed changes.

				// Payloads and recipients

				// Test a payload with and without a specific recipient

				Console.WriteLine("*** Drop off to global (1) and to a specific recipient (1)");
				Console.Write($"Add \"Global payload\" for global queue in the zone: ");
				result = await restApiUpdateMaster.DropOff("Global payload");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				var recipient = "Tim";
				var tracking = "ABC123";
				Console.WriteLine($"Add \"Tim's payload\" for {recipient} queue in the zone ... ");
				result = await restApiUpdateMaster.DropOff("Tim's payload", new PayloadMetadata
				{
					Recipient = recipient,
					Tracking = tracking
				});
				DisplayResult(result, 0);

				Console.WriteLine($"Query payload tracking number for recipient {recipient} ...");
				result = await restApiUpdateMaster.Inquiry(tracking, recipient, null);
				DisplayResult(result, 0);

				Console.WriteLine($"Query payload bad tracking number for recipient {recipient} ...");
				result = await restApiUpdateMaster.Inquiry(tracking + "X", recipient, null);
				DisplayResult(result, -1);

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
					result = await restApiUpdateMaster.DropOff(set[index % 3] + $"{index}",
						new PayloadMetadata
						{
							ExpiresOn = DateTime.Now.AddSeconds(3 + index)
						});
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

			//Console.ReadLine();
			//return;
		}

		static void UnexpectedResult()
		{
			UnexpectedResult(null);
		}

		static void UnexpectedResult(string message)
		{
			Console.WriteLine(!string.IsNullOrWhiteSpace(message) ? message : "Non-pass result: press Enter to continue tests");
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
		public static readonly JsonSerializerSettings Config = new()
		{
			MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
			DateParseHandling = DateParseHandling.DateTime,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented
		};
	}
}
