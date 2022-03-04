using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BOG.DropZone.Client;
using BOG.DropZone.Client.Entity;
using BOG.DropZone.Client.Model;
using BOG.DropZone.Common.Dto;
using Newtonsoft.Json;

namespace BOG.DropZone.Test
{

	// Functions as both an example, and a functional test.
	// - Make this the default startup project
	// - Open a command prompt and start BOG.DropZone externally on the host with start_me.bat/.sh
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

		static async Task UnitTestAsync()
		{
			Console.WriteLine("Setup()...");
			const string Access = "YourAccessTokenValueHere";
			const string Admin = "YourAdminTokenValueHere";

			var zone = new DropZoneConfig
			{
				BaseUrl = "http://localhost:5000",
				ZoneName = "TestPlace",
				Password = string.Empty,
				Salt = string.Empty,
				UseEncryption = false,
				AccessToken = Access,
				AdminToken = Admin,
				TimeoutSeconds = 600
			};

			var restApiUpdateMaster = new RestApiCalls(zone);
			bool testPassed = true;

			try
			{
				Console.WriteLine("### TESTING BEGINS ###");
				Console.WriteLine();
				Console.WriteLine("---------------------------");
				Console.Write($"CheckHeartbeat(): ");
				Result result = await restApiUpdateMaster.CheckHeartbeat();
				Console.WriteLine($"{result.HandleAs}");
				if (result.HandleAs != Result.State.OK)
				{
					Console.WriteLine();
					UnexpectedResult("No response to heartbeat call, testing halted... ");
					System.Environment.Exit(1);
				}

				Console.WriteLine();
				Console.WriteLine("---------------------------");
				Console.Write("Reset(): ");
				result = await restApiUpdateMaster.Reset();
				Console.WriteLine($"{result.HandleAs}");
				if (result.HandleAs != Result.State.OK)
				{
					UnexpectedResult("Reset failed, testing halted... ");
					return;
				}

				Console.WriteLine();
				Console.WriteLine("---------------------------");
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

				Console.WriteLine();
				Console.WriteLine("--------------------------- Reference Storage");

				Console.Write("ListReferences() ...: ");
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

				Console.Write("SetReference(): ");
				result = await restApiUpdateMaster.SetReference("Test-Ref01", "test ref 1");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences() ...: ");
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

				Console.Write("SetReference()... Perishable (5 second life span): ");
				result = await restApiUpdateMaster.SetReference("Test-Ref02-Perish-5-Sec", "this will go away", DateTime.Now.AddSeconds(5));
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences() ...: ");
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
					testPassed &= result.Content == "this will go away";
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: this will go way, got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Wait 5 seconds for reference to expire...");
				Thread.Sleep(5100);

				Console.Write("ListReferences()... : ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()...: ");
					testPassed &= list.Count == 1;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: 1, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetReference()... Perishable ... > 5 sec (perished): ");
				result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Value from perishable reference is empty... ");
					testPassed &= string.IsNullOrEmpty(result.Content);
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: empty string, got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				{
					var bigData = new string('X', 524288);   // 512K of X's... not over limit.
					Console.Write($"SetReference()... BIG...{bigData.Length}: ");
					result = await restApiUpdateMaster.SetReference("BIG", bigData, DateTime.Now.AddSeconds(2));
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
					var hugeData = new string('X', 41000000);
					Console.Write($"SetReference()... HUGE... rejected due to size {hugeData.Length}: ");
					result = await restApiUpdateMaster.SetReference("HUGE", hugeData, DateTime.Now.AddSeconds(2));
					testPassed = result.HandleAs == Result.State.OverLimit;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: UnexpectedResponse, got: {result.HandleAs}");
					if (!testPassed) UnexpectedResult();
					testPassed = true;

					Console.WriteLine("Wait 2 seconds for potential cleanup backlog...");
					Thread.Sleep(2000);

					Console.Write("GetReference()... HUGE...");
					result = await restApiUpdateMaster.GetReference("HUGE");
					testPassed = result.HandleAs == Result.State.NoDataAvailable;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
					if (!testPassed) UnexpectedResult();
					testPassed = true;
					result.Message = string.Empty;
				}

				Console.Write("DropReference() Test-Ref01 ...:  ");
				result = await restApiUpdateMaster.DropReference("Test-Ref01");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences() ...: ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 0;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine();
				Console.WriteLine("--------------------------- Statistics");

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
					Console.WriteLine(obj.ReferenceCount == 0 ? "pass" : $"FAIL, expected: 0, got {obj.ReferenceCount}");
					testPassed &= obj.ReferenceCount == 0;
					Console.Write("  PayloadExpiredCount: ");
					Console.WriteLine(obj.PayloadExpiredCount == 0 ? "pass" : $"FAIL, expected: 1, got {obj.PayloadExpiredCount}");
					testPassed &= obj.PayloadExpiredCount == 0;
					Console.Write("  ReferenceExpiredCount: ");
					Console.WriteLine(obj.ReferenceExpiredCount == 1 ? "pass" : $"FAIL, expected: 1, got {obj.ReferenceExpiredCount}");
					testPassed &= obj.ReferenceExpiredCount == 1;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine();
				Console.WriteLine("--------------------------- Security Info");

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
					Console.WriteLine(obj[0].AccessAttemptsTotalCount == 16 ? "pass" : $"FAIL, expected: 0, got {obj[0].AccessAttemptsTotalCount}");
					testPassed &= obj[0].AccessAttemptsTotalCount == 16;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine();
				Console.WriteLine("--------------------------- Payloads and recipients");

				// Test a payload with and without a specific recipient

				var globalPayloadValue = "GlobalPayloadData";
				var globalPayloadMetadata = new PayloadMetadata();
				var TimsPayloadValue = "TimsPayloadData";
				var TimsPayloadMetadata = new PayloadMetadata
				{
					Recipient = "Tim",
					Tracking = "ABC123"
				};

				Console.WriteLine("*** Drop off to global (1) and to a specific recipient (1)");
				Console.Write($"Add Global payload to \"{globalPayloadMetadata.Recipient}\"global queue in the zone: ");
				result = await restApiUpdateMaster.DropOff(globalPayloadValue, globalPayloadMetadata);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Add Tim's payload to \"{TimsPayloadMetadata.Recipient}\" queue in the zone ");
				result = await restApiUpdateMaster.DropOff(TimsPayloadValue, TimsPayloadMetadata);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Query payload tracking number for recipient {TimsPayloadMetadata.Recipient}:");
				result = await restApiUpdateMaster.Inquiry(TimsPayloadMetadata.Tracking, TimsPayloadMetadata.Recipient, null);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write($"Payload found: ");
					var obj = JsonConvert.DeserializeObject<PayloadInquiry>(result.Content);
					testPassed = obj.Found;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: found == true, got: {result.HandleAs}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Query payload bad tracking number for recipient {TimsPayloadMetadata.Recipient}:");
				result = await restApiUpdateMaster.Inquiry(TimsPayloadMetadata.Tracking + "X", TimsPayloadMetadata.Recipient, null);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write($"Payload found: ");
					var obj = JsonConvert.DeserializeObject<PayloadInquiry>(result.Content);
					testPassed = !obj.Found;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: found == false, got: {result.HandleAs}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Retrieve payload for recipient {TimsPayloadMetadata.Recipient}: ");
				result = await restApiUpdateMaster.Pickup(TimsPayloadMetadata.Recipient);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Retrieve payload for recipient {TimsPayloadMetadata.Recipient} (nothing): ");
				result = await restApiUpdateMaster.Pickup(TimsPayloadMetadata.Recipient);
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write($"Retrieve payload for global use: ");
				result = await restApiUpdateMaster.Pickup(globalPayloadMetadata.Recipient);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine($"Retrieve payload for global use ... should have nothing: ");
				result = await restApiUpdateMaster.Pickup(globalPayloadMetadata.Recipient);
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Other payloads tests.

				string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

				Console.WriteLine("*** Drop off 5 payloads .. which expire after 5 seconds ***");
				for (int index = 0; index < 5; index++)
				{
					var thisTest = true;
					var payload = set[index % 3] + $"{index}";
					Console.Write($"Add {index}: {payload}... ");
					result = await restApiUpdateMaster.DropOff(payload, new PayloadMetadata
					{
						ExpiresOn = DateTime.Now.AddSeconds(index > 0 ? 600 : 1)
					});
					thisTest = result.HandleAs == Result.State.OK;
					Console.WriteLine(thisTest ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					if (!thisTest) UnexpectedResult();
					testPassed &= thisTest;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Wait 2 seconds for reference to expire...");
				Thread.Sleep(2000);

				Console.WriteLine("*** Pickup five items .. index 0 has expired: ***");
				for (int index = 1; index < 5; index++)
				{
					var thisTest = true;
					var payload = set[index % 3] + $"{index}";
					Console.Write($"Retrieve {index} from {payload}: ");
					result = await restApiUpdateMaster.Pickup();
					thisTest = result.HandleAs == Result.State.OK;
					Console.Write(thisTest ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					if (thisTest)
					{
						thisTest = (result.Content == payload);
						Console.Write($"  payload as expected: ");
						Console.Write(thisTest ? "pass" : $"FAIL, expected: {payload}, got: {result.Content}");
					}
					Console.WriteLine();
					testPassed &= thisTest;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Item 6 shoud not be available: the five non-perishing payloads have been picked up.
				Console.Write($"Check for no availabile payloads: ");
				result = await restApiUpdateMaster.Pickup();
				testPassed = result.HandleAs == Result.State.NoDataAvailable;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailableK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();

				Console.WriteLine("*** Drop off some data.. places 501 items when 500 is the max ***");
				for (int index = 0; index <= metrics.MaxPayloadCount; index++)
				{
					var thisPassed = true;
					var payload = set[index % 3] + $"{index}";
					Console.Write($"Add {index}: {payload}... ");
					result = await restApiUpdateMaster.DropOff(payload);
					if (index == metrics.MaxPayloadCount)
					{
						thisPassed = result.HandleAs == Result.State.OverLimit;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OverLimit, got: {result.HandleAs}");
					}
					else
					{
						thisPassed = result.HandleAs == Result.State.OK;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					}
					if (!thisPassed)
						UnexpectedResult();
					testPassed &= thisPassed;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("GetStatistics(): ");
				result = await restApiUpdateMaster.GetStatistics();
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var value = Serializer<DropZoneInfo>.FromJson(result.Content);
					Console.Write("  value.Name == {zone.ZoneName}: ");
					var thisTest = value.Name == zone.ZoneName;
					testPassed &= thisTest;
					Console.WriteLine(thisTest ? "pass" : $"FAIL, got: {value.Name}");

					Console.Write("  value.PayloadCount == 0: ");
					thisTest = value.PayloadCount == 500;
					testPassed &= thisTest;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, got: {value.PayloadCount}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("*** Pickup some data ***");
				for (int index = 0; index <= metrics.MaxPayloadCount; index++)
				{
					var thisPassed = true;
					Console.Write($"Retrieve {index}: ");
					result = await restApiUpdateMaster.Pickup();
					if (index == metrics.MaxPayloadCount)
					{
						thisPassed = result.HandleAs == Result.State.NoDataAvailable;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
					}
					else
					{
						thisPassed = result.HandleAs == Result.State.OK;
						Console.Write(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
						if (thisPassed)
						{
							var payload = set[index % 3] + $"{index}";
							Console.Write($", payload == {payload}: ");
							thisPassed = result.Content == payload;
							Console.Write(testPassed ? " pass" : $" FAIL, expected: OK, got: {result.Content}");
						}
					}
					Console.WriteLine();
					testPassed &= thisPassed;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences()... : ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? " pass" : $" FAIL, expected: OK, got: {result.Content}");
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == 0;
					Console.WriteLine(list.Count == 0 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Overload references...");
				for (int index = 0; index <= metrics.MaxReferencesCount; index++)
				{
					var thisPassed = true;
					var key = $"Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}";
					var value = $"test ref {index}";
					Console.Write($"SetReference({key}): ");
					result = await restApiUpdateMaster.SetReference(key, value);
					if (index == metrics.MaxReferencesCount)
					{
						thisPassed = result.HandleAs == Result.State.OverLimit;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OverLimit, got: {result.HandleAs}");
					}
					else
					{
						thisPassed = result.HandleAs == Result.State.OK;
						Console.Write(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					}
					Console.WriteLine();
					testPassed &= thisPassed;
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("ListReferences()... : ");
				result = await restApiUpdateMaster.ListReferences();
				testPassed = result.HandleAs == Result.State.OK;
				if (testPassed)
				{
					var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
					Console.Write("  list.Count()... ");
					testPassed &= list.Count == metrics.MaxReferencesCount;
					Console.WriteLine(list.Count == metrics.MaxReferencesCount ? "pass" : $"FAIL, expected: {metrics.MaxReferencesCount}, got: {list.Count}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine();
				Console.WriteLine("--------------------------- Blob Storage ");

				const string TestItem01Value = "The quick brown fox jumped over the lazy dog's back.";

				Console.WriteLine("Delete any existing blobs...");
				Console.Write("Get blob list...: ");
				result = await restApiUpdateMaster.ListBlobs();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					foreach (var blobKey in Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content))
					{
						Console.Write($"  Delete {blobKey} ...: ");
						result = await restApiUpdateMaster.DropBlob(blobKey);
						testPassed = result.HandleAs == Result.State.OK;
						Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
					}
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Read blob list ...: ");
				result = await restApiUpdateMaster.ListBlobs();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write($"  Blob list is empty ...: ");
					var blobCount = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content).Count;
					testPassed = (blobCount == 0);
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Read a non-existent blob item key from storage: test for no content.
				Console.Write("Read Blob TestItem01 ...: ");
				result = await restApiUpdateMaster.GetBlob("TestItem01");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Valid Blob Content TestItem01 ...: ");
					testPassed |= string.IsNullOrEmpty(result.Content);
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Commit a blob item to storage: 
				Console.Write("Write Blob TestItem01 ...: ");
				result = await restApiUpdateMaster.SetBlob("TestItem01", TestItem01Value);
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Read the existent blob item key from storage: test for content stored above..
				Console.Write("Read Blob TestItem01 ...: ");
				result = await restApiUpdateMaster.GetBlob("TestItem01");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Valid Blob Content TestItem01 ...: ");
					testPassed |= string.Compare(result.Content, TestItem01Value, false) == 0;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: {TestItem01Value}\r\ngot: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Commit a large blob item to storage: 
				Console.Write("Write large Blob TestItem02");
				// This is just a file containing a large amount of text data (100K or more).
				var bigFish = @"C:\Users\johnm\research\RR-Dynamic-20201216-C.result.json";
				string bigFishContent = null;
				using (StreamReader t = new(bigFish))
				{
					bigFishContent = t.ReadToEnd();
					result = await restApiUpdateMaster.SetBlob("TestItem02", bigFishContent);
				}
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.Write("Clear DropZone ...: ");
				result = await restApiUpdateMaster.Clear();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				Console.WriteLine("Read blob list ...: ");
				result = await restApiUpdateMaster.ListBlobs();
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write($"  Blob list has previous items ...: ");
					var blobCount = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content).Count;
					testPassed = (blobCount == 2);
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: 2, got: {blobCount}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Read the blob item TestItem01: test for proper content.
				Console.Write("Read Blob TestItem01 ...: ");
				result = await restApiUpdateMaster.GetBlob("TestItem01");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Valid Blob Content TestItem01 ...: ");
					testPassed |= string.Compare(result.Content, TestItem01Value, false) == 0;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;

				// Read the blob item TestItem01: test for proper content.
				Console.Write("Read Blob TestItem02 ...: ");
				result = await restApiUpdateMaster.GetBlob("TestItem02");
				testPassed = result.HandleAs == Result.State.OK;
				Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
				if (testPassed)
				{
					Console.Write("  Valid Blob Content TestItem02 ...: ");
					testPassed |= string.Compare(result.Content, bigFishContent, false) == 0;
					Console.WriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
				}
				if (!testPassed) UnexpectedResult();
				testPassed = true;
			}
			catch (Exception err)
			{
				Console.WriteLine($"Untrapped: {err.Message}");
			}

			Console.WriteLine("---------------------------");
			Console.WriteLine("### TESTING ENDS ###");
			Console.WriteLine("---------------------------");
#if DEBUG
			Console.WriteLine();
			Console.WriteLine("Press ENTER to finish");
			Console.ReadLine();
#endif
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
