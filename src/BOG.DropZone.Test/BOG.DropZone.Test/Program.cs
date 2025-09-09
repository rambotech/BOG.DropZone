using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
        static StreamWriter sw;
        static string fileName = Path.GetTempFileName();

        static void LogFileWrite(string str)
        {
            Console.Write(str);
            sw.Write(str);
        }

        static void LogFileWriteLine(string str)
        {
            Console.WriteLine(str);
            sw.WriteLine(str);
        }

        static void LogFileWriteLine()
        {
            Console.WriteLine();
            sw.WriteLine("");
        }

        static void Main()
        {
            //RunAsync().GetAwaiter().GetResult();
            UnitTestAsync().GetAwaiter().GetResult();
        }

        static async Task UnitTestAsync()
        {
            var fileName = Path.GetTempFileName();
            Console.WriteLine($"Output logged to: {fileName}");
            using (sw = new StreamWriter(fileName))
            {
                //const string Access = "YourAccessTokenValueHere";
                //const string Admin = "YourAdminTokenValueHere";

                var zone = new DropZoneConfig
                {
                    BaseUrl = "http://localhost:5000",
                    ZoneName = "TestPlace",
                    Password = string.Empty,
                    Salt = string.Empty,
                    UseEncryption = false,
                    AccessToken = "YourAccessTokenValueHere",
                    AdminToken = "YourAdminTokenValueHere",
                    TimeoutSeconds = 600
                };

                LogFileWriteLine($"Target host: {zone.BaseUrl}");

                LogFileWriteLine("Setup()...");

                var restApiUpdateMaster = new RestApiCalls(zone);

                LogFileWriteLine($"{restApiUpdateMaster}");

                LogFileWriteLine("get Json Test data from typicode.com ...");
                var wc = new HttpClient();
                wc.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
                LogFileWrite("  posts ...");
                var postsData = wc.GetStringAsync("posts").GetAwaiter().GetResult();
                LogFileWriteLine($": {postsData.Length} bytes");

                LogFileWrite("  comments ...");
                var commentsData = wc.GetStringAsync("comments").GetAwaiter().GetResult();
                LogFileWriteLine($": {commentsData.Length} bytes");

                LogFileWrite("  photos ...");
                var photosData = wc.GetStringAsync("photos").GetAwaiter().GetResult();
                LogFileWriteLine($": {photosData.Length} bytes");

                LogFileWriteLine();
                bool testPassed = true;

                try
                {
                    LogFileWriteLine("### TESTING BEGINS ###");
                    LogFileWriteLine();
                    LogFileWriteLine("---------------------------");
                    LogFileWrite($"CheckHeartbeat(): ");
                    Result result = await restApiUpdateMaster.CheckHeartbeat();
                    LogFileWriteLine($"{result.HandleAs}");
                    if (result.HandleAs != Result.State.OK)
                    {
                        LogFileWriteLine();
                        UnexpectedResult("No response to heartbeat call, testing halted... ");
                        System.Environment.Exit(1);
                    }

                    LogFileWriteLine();
                    LogFileWriteLine("---------------------------");
                    LogFileWrite("Reset(): ");
                    result = await restApiUpdateMaster.Reset();
                    LogFileWriteLine($"{result.HandleAs}");
                    if (result.HandleAs != Result.State.OK)
                    {
                        UnexpectedResult("Reset failed, testing halted... ");
                        return;
                    }

                    LogFileWriteLine();
                    LogFileWriteLine("---------------------------");
                    LogFileWrite("SetMetrics(): ");
                    var metrics = new DropZoneMetrics
                    {
                        MaxPayloadCount = 500,
                        MaxPayloadSize = 5L * 1024L * 1024L,
                        MaxReferencesCount = 100,
                        MaxReferenceSize = 5L * 1024L * 1024L
                    };
                    result = await restApiUpdateMaster.SetMetrics(metrics);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected : NoDataAvailable, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("GetMetrics(): ");
                    result.Message = string.Empty;
                    result = await restApiUpdateMaster.GetStatistics();
                    LogFileWriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var obj = JsonConvert.DeserializeObject<DropZoneInfo>(result.Content).Metrics;
                        // DropZoneInfo obj = (DropZoneInfo) JsonConvert.DeserializeObject(result.Content, Type.GetType(result.CastType)); 
                        LogFileWrite("  MaxPayloadCount: ");
                        LogFileWriteLine(obj.MaxPayloadCount == metrics.MaxPayloadCount ? "pass" : $"FAIL, expected: 0, got {obj.MaxPayloadCount}");
                        testPassed &= obj.MaxPayloadCount == metrics.MaxPayloadCount;
                        LogFileWrite("  MaxPayloadSize: ");
                        LogFileWriteLine(obj.MaxPayloadSize == metrics.MaxPayloadSize ? "pass" : $"FAIL, expected: 0, got {obj.MaxPayloadSize}");
                        testPassed &= obj.MaxPayloadSize == metrics.MaxPayloadSize;
                        LogFileWrite("  MaxReferencesCount: ");
                        LogFileWriteLine(obj.MaxReferencesCount == metrics.MaxReferencesCount ? "pass" : $"FAIL, expected: 0, got {obj.MaxReferencesCount}");
                        testPassed &= obj.MaxReferencesCount == metrics.MaxReferencesCount;
                        LogFileWrite("  MaxReferenceSize: ");
                        LogFileWriteLine(obj.MaxReferenceSize == metrics.MaxReferenceSize ? "pass" : $"FAIL, expected: 0, got {obj.MaxReferenceSize}");
                        testPassed &= obj.MaxReferenceSize == metrics.MaxReferenceSize;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine();
                    LogFileWriteLine("--------------------------- Reference Storage");

                    LogFileWrite("ListReferences() ...: ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == 0;
                        LogFileWriteLine(list.Count == 0 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("SetReference(): ");
                    result = await restApiUpdateMaster.SetReference("Test-Ref01", "test ref 1");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("ListReferences() ...: ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == 1;
                        LogFileWriteLine(list.Count == 1 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("GetReference() [found]:  ");
                    result = await restApiUpdateMaster.GetReference("Test-Ref01");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Value check... ");
                        testPassed &= result.Content == "test ref 1";
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: test ref 1, got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("SetReference()... Perishable (5 second life span): ");
                    result = await restApiUpdateMaster.SetReference("Test-Ref02-Perish-5-Sec", "this will go away", DateTime.Now.AddSeconds(3));
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("ListReferences() ...: ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == 2;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("GetReference()... Perishable ... < 1 sec (finds it): ");
                    result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Value check... ");
                        testPassed &= result.Content == "this will go away";
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: this will go way, got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine("Wait 5 seconds for reference to expire...");
                    Thread.Sleep(5000);

                    LogFileWrite("ListReferences()... : ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()...: ");
                        testPassed &= list.Count == 1;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: 1, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("GetReference()... Perishable ... > 5 sec (perished): ");
                    result = await restApiUpdateMaster.GetReference("Test-Ref02-Perish-5-Sec");
                    testPassed = result.HandleAs == Result.State.NoDataAvailable;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Value from perishable reference is empty... ");
                        testPassed &= string.IsNullOrEmpty(result.Content);
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: empty string, got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    {
                        var bigData = new string('X', 524288);   // 512K of X's... not over limit.
                        LogFileWrite($"SetReference()... BIG...{bigData.Length}: ");
                        result = await restApiUpdateMaster.SetReference("BIG", bigData, DateTime.Now.AddSeconds(2));
                        testPassed = result.HandleAs == Result.State.OK;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        if (!testPassed) UnexpectedResult();
                        testPassed = true;

                        LogFileWrite($"GetReference()... BIG...:");
                        result = await restApiUpdateMaster.GetReference("BIG");
                        LogFileWrite($"{result.Message.Length}: ");
                        testPassed = result.HandleAs == Result.State.OK;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        if (testPassed)
                        {
                            LogFileWrite("  Value check... ");
                            testPassed &= result.Content == bigData;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: test ref 1, got: {result.Content}");
                        }
                        if (!testPassed) UnexpectedResult();
                        testPassed = true;
                    }

                    result.Message = string.Empty;
                    {
                        var hugeData = new string('X', 41000000);
                        LogFileWrite($"SetReference()... HUGE... rejected due to size {hugeData.Length}: ");
                        result = await restApiUpdateMaster.SetReference("HUGE", hugeData, DateTime.Now.AddSeconds(2));
                        testPassed = result.HandleAs == Result.State.OverLimit;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: UnexpectedResponse, got: {result.HandleAs}");
                        if (!testPassed) UnexpectedResult();
                        testPassed = true;

                        LogFileWriteLine("Wait 2 seconds for potential cleanup backlog...");
                        Thread.Sleep(2000);

                        LogFileWrite("GetReference()... HUGE...");
                        result = await restApiUpdateMaster.GetReference("HUGE");
                        testPassed = result.HandleAs == Result.State.NoDataAvailable;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                        if (!testPassed) UnexpectedResult();
                        testPassed = true;
                        result.Message = string.Empty;
                    }

                    LogFileWrite("DropReference() Test-Ref01 ...:  ");
                    result = await restApiUpdateMaster.DropReference("Test-Ref01");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("ListReferences() ...: ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == 0;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine();
                    LogFileWriteLine("--------------------------- Statistics");

                    LogFileWrite("GetStatistics(): ");
                    result = await restApiUpdateMaster.GetStatistics();
                    LogFileWriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var obj = JsonConvert.DeserializeObject<DropZoneInfo>(result.Content);
                        // DropZoneInfo obj = (DropZoneInfo) JsonConvert.DeserializeObject(result.Content, Type.GetType(result.CastType)); 
                        LogFileWrite("  PayloadCount: ");
                        LogFileWriteLine(obj.PayloadCount == 0 ? "pass" : $"FAIL, expected: 0, got {obj.PayloadCount}");
                        testPassed &= obj.PayloadCount == 0;
                        LogFileWrite("  ReferenceCount: ");
                        LogFileWriteLine(obj.ReferenceCount == 0 ? "pass" : $"FAIL, expected: 0, got {obj.ReferenceCount}");
                        testPassed &= obj.ReferenceCount == 0;
                        LogFileWrite("  PayloadExpiredCount: ");
                        LogFileWriteLine(obj.PayloadExpiredCount == 0 ? "pass" : $"FAIL, expected: 1, got {obj.PayloadExpiredCount}");
                        testPassed &= obj.PayloadExpiredCount == 0;
                        LogFileWrite("  ReferenceExpiredCount: ");
                        LogFileWriteLine(obj.ReferenceExpiredCount == 1 ? "pass" : $"FAIL, expected: 1, got {obj.ReferenceExpiredCount}");
                        testPassed &= obj.ReferenceExpiredCount == 1;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine();
                    LogFileWriteLine("--------------------------- Security Info");

                    LogFileWriteLine("GetSecurityInfo(): ");
                    result = await restApiUpdateMaster.GetSecurity();
                    LogFileWriteLine(result.HandleAs == Result.State.OK ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var obj = JsonConvert.DeserializeObject<List<ClientWatch>>(result.Content);
                        LogFileWrite("  TotalClients: ");
                        LogFileWriteLine(obj.Count == 1 ? "pass" : $"FAIL, expected: 0, got {obj.Count}");
                        testPassed &= obj.Count == 1;

                        LogFileWrite("  AccessAttemptsTotalCount: ");
                        LogFileWriteLine(obj[0].AccessAttemptsTotalCount == 16 ? "pass" : $"FAIL, expected: 0, got {obj[0].AccessAttemptsTotalCount}");
                        testPassed &= obj[0].AccessAttemptsTotalCount == 16;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine();
                    LogFileWriteLine("--------------------------- Payloads and recipients");

                    // Test a payload with and without a specific recipient

                    var globalPayloadValue = "GlobalPayloadData";
                    var globalPayloadMetadata = new PayloadMetadata();
                    var TimsPayloadValue = "TimsPayloadData";
                    var TimsPayloadMetadata = new PayloadMetadata
                    {
                        Recipient = "Tim",
                        Tracking = "ABC123"
                    };

                    LogFileWriteLine("*** Drop off to global (1) and to a specific recipient (1)");
                    LogFileWrite($"Add Global payload to \"{globalPayloadMetadata.Recipient}\"global queue in the zone: ");
                    result = await restApiUpdateMaster.DropOff(globalPayloadValue, globalPayloadMetadata);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Add Tim's payload to \"{TimsPayloadMetadata.Recipient}\" queue in the zone ");
                    result = await restApiUpdateMaster.DropOff(TimsPayloadValue, TimsPayloadMetadata);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Query payload tracking number for recipient {TimsPayloadMetadata.Recipient}:");
                    result = await restApiUpdateMaster.Inquiry(TimsPayloadMetadata.Tracking, TimsPayloadMetadata.Recipient, null);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite($"Payload found: ");
                        var obj = JsonConvert.DeserializeObject<PayloadInquiry>(result.Content);
                        testPassed = obj.Found;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: found == true, got: {result.HandleAs}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Query payload bad tracking number for recipient {TimsPayloadMetadata.Recipient}:");
                    result = await restApiUpdateMaster.Inquiry(TimsPayloadMetadata.Tracking + "X", TimsPayloadMetadata.Recipient, null);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite($"Payload found: ");
                        var obj = JsonConvert.DeserializeObject<PayloadInquiry>(result.Content);
                        testPassed = !obj.Found;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: found == false, got: {result.HandleAs}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Retrieve payload for recipient {TimsPayloadMetadata.Recipient}: ");
                    result = await restApiUpdateMaster.Pickup(TimsPayloadMetadata.Recipient);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Retrieve payload for recipient {TimsPayloadMetadata.Recipient} (nothing): ");
                    result = await restApiUpdateMaster.Pickup(TimsPayloadMetadata.Recipient);
                    testPassed = result.HandleAs == Result.State.NoDataAvailable;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite($"Retrieve payload for global use: ");
                    result = await restApiUpdateMaster.Pickup(globalPayloadMetadata.Recipient);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine($"Retrieve payload for global use ... should have nothing: ");
                    result = await restApiUpdateMaster.Pickup(globalPayloadMetadata.Recipient);
                    testPassed = result.HandleAs == Result.State.NoDataAvailable;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Other payloads tests.

                    string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

                    LogFileWriteLine("*** Drop off 5 payloads .. which expire after 5 seconds ***");
                    for (int index = 0; index < 5; index++)
                    {
                        var thisTest = true;
                        var payload = set[index % 3] + $"{index}";
                        LogFileWrite($"Add {index}: {payload}... ");
                        result = await restApiUpdateMaster.DropOff(payload, new PayloadMetadata
                        {
                            ExpiresOn = DateTime.Now.AddSeconds(index > 0 ? 600 : 1)
                        });
                        thisTest = result.HandleAs == Result.State.OK;
                        LogFileWriteLine(thisTest ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        if (!thisTest) UnexpectedResult();
                        testPassed &= thisTest;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine("Wait 2 seconds for reference to expire...");
                    Thread.Sleep(2000);

                    LogFileWriteLine("*** Pickup five items .. index 0 has expired: ***");
                    for (int index = 1; index < 5; index++)
                    {
                        var thisTest = true;
                        var payload = set[index % 3] + $"{index}";
                        LogFileWrite($"Retrieve {index} from {payload}: ");
                        result = await restApiUpdateMaster.Pickup();
                        thisTest = result.HandleAs == Result.State.OK;
                        LogFileWrite(thisTest ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        if (thisTest)
                        {
                            thisTest = (result.Content == payload);
                            LogFileWrite($"  payload as expected: ");
                            LogFileWrite(thisTest ? "pass" : $"FAIL, expected: {payload}, got: {result.Content}");
                        }
                        LogFileWriteLine();
                        testPassed &= thisTest;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Item 6 shoud not be available: the five non-perishing payloads have been picked up.
                    LogFileWrite($"Check for no availabile payloads: ");
                    result = await restApiUpdateMaster.Pickup();
                    testPassed = result.HandleAs == Result.State.NoDataAvailable;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailableK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();

                    LogFileWriteLine("*** Drop off some data.. places 501 items when 500 is the max ***");
                    for (int index = 0; index <= metrics.MaxPayloadCount; index++)
                    {
                        var thisPassed = true;
                        var payload = set[index % 3] + $"{index}";
                        LogFileWrite($"Add {index}: {payload}... ");
                        result = await restApiUpdateMaster.DropOff(payload);
                        if (index == metrics.MaxPayloadCount)
                        {
                            thisPassed = result.HandleAs == Result.State.OverLimit;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OverLimit, got: {result.HandleAs}");
                        }
                        else
                        {
                            thisPassed = result.HandleAs == Result.State.OK;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        }
                        if (!thisPassed)
                            UnexpectedResult();
                        testPassed &= thisPassed;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("GetStatistics(): ");
                    result = await restApiUpdateMaster.GetStatistics();
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var value = Serializer<DropZoneInfo>.FromJson(result.Content);
                        LogFileWrite("  value.Name == {zone.ZoneName}: ");
                        var thisTest = value.Name == zone.ZoneName;
                        testPassed &= thisTest;
                        LogFileWriteLine(thisTest ? "pass" : $"FAIL, got: {value.Name}");

                        LogFileWrite("  value.PayloadCount == 0: ");
                        thisTest = value.PayloadCount == 500;
                        testPassed &= thisTest;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, got: {value.PayloadCount}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine("*** Pickup some data ***");
                    for (int index = 0; index <= metrics.MaxPayloadCount; index++)
                    {
                        var thisPassed = true;
                        LogFileWrite($"Retrieve {index}: ");
                        result = await restApiUpdateMaster.Pickup();
                        if (index == metrics.MaxPayloadCount)
                        {
                            thisPassed = result.HandleAs == Result.State.NoDataAvailable;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: NoDataAvailable, got: {result.HandleAs}");
                        }
                        else
                        {
                            thisPassed = result.HandleAs == Result.State.OK;
                            LogFileWrite(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                            if (thisPassed)
                            {
                                var payload = set[index % 3] + $"{index}";
                                LogFileWrite($", payload == {payload}: ");
                                thisPassed = result.Content == payload;
                                LogFileWrite(testPassed ? " pass" : $" FAIL, expected: OK, got: {result.Content}");
                            }
                        }
                        LogFileWriteLine();
                        testPassed &= thisPassed;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("ListReferences()... : ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? " pass" : $" FAIL, expected: OK, got: {result.Content}");
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == 0;
                        LogFileWriteLine(list.Count == 0 ? "pass" : $"FAIL, expected: 0, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine("Overload references...");
                    for (int index = 0; index <= metrics.MaxReferencesCount; index++)
                    {
                        var thisPassed = true;
                        var key = $"Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}";
                        var value = $"test ref {index}";
                        LogFileWrite($"SetReference({key}): ");
                        result = await restApiUpdateMaster.SetReference(key, value);
                        if (index == metrics.MaxReferencesCount)
                        {
                            thisPassed = result.HandleAs == Result.State.OverLimit;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OverLimit, got: {result.HandleAs}");
                        }
                        else
                        {
                            thisPassed = result.HandleAs == Result.State.OK;
                            LogFileWrite(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        }
                        LogFileWriteLine();
                        testPassed &= thisPassed;
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("ListReferences()... : ");
                    result = await restApiUpdateMaster.ListReferences();
                    testPassed = result.HandleAs == Result.State.OK;
                    if (testPassed)
                    {
                        var list = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content);
                        LogFileWrite("  list.Count()... ");
                        testPassed &= list.Count == metrics.MaxReferencesCount;
                        LogFileWriteLine(list.Count == metrics.MaxReferencesCount ? "pass" : $"FAIL, expected: {metrics.MaxReferencesCount}, got: {list.Count}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine();
                    LogFileWriteLine("--------------------------- Blob Storage ");

                    const string TestItem01Value = "The quick brown fox jumped over the lazy dog's back.";

                    LogFileWriteLine("Delete any existing blobs...");
                    LogFileWrite("Get blob list...: ");
                    result = await restApiUpdateMaster.ListBlobs();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        foreach (var blobKey in Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content))
                        {
                            LogFileWrite($"  Delete {blobKey} ...: ");
                            result = await restApiUpdateMaster.DropBlob(blobKey);
                            testPassed = result.HandleAs == Result.State.OK;
                            LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                        }
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("Read blob list ...: ");
                    result = await restApiUpdateMaster.ListBlobs();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite($"  Blob list is empty ...: ");
                        var blobCount = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content).Count;
                        testPassed = (blobCount == 0);
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Read a non-existent blob item key from storage: test for no content.
                    LogFileWrite("  Read Blob TestItem01 ...: ");
                    result = await restApiUpdateMaster.GetBlob("TestItem01");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Valid Blob Content TestItem01 ...: ");
                        testPassed |= string.IsNullOrEmpty(result.Content);
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Commit a blob item to storage: 
                    LogFileWrite("  Write Blob TestItem01 ...: ");
                    result = await restApiUpdateMaster.SetBlob("TestItem01", TestItem01Value);
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Read the existent blob item key from storage: test for content stored above..
                    LogFileWrite("  Read Blob TestItem01 ...: ");
                    result = await restApiUpdateMaster.GetBlob("TestItem01");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Valid Blob Content TestItem01 ...: ");
                        testPassed |= string.Compare(result.Content, TestItem01Value, false) == 0;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: {TestItem01Value}\r\ngot: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Commit a large blob item to storage: 
                    LogFileWrite("  Write large Blob TestItem02: ");
                    // This is just a file containing a large amount of text data (100K or more).
                    var bigFish = @"Testing\TextFile1.txt";
                    string bigFishContent = null;
                    using (var t = new StreamReader(bigFish))
                    {
                        bigFishContent = t.ReadToEnd();
                        result = await restApiUpdateMaster.SetBlob("TestItem02", bigFishContent);
                    }
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWrite("Clear DropZone ...: ");
                    result = await restApiUpdateMaster.Clear();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    LogFileWriteLine("Read blob list ...: ");
                    result = await restApiUpdateMaster.ListBlobs();
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite($"  Blob list has previous items ...: ");
                        var blobCount = Serializer<System.Collections.Generic.List<System.String>>.FromJson(result.Content).Count;
                        testPassed = (blobCount == 2);
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: 2, got: {blobCount}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Read the blob item TestItem01: test for proper content.
                    LogFileWrite("Read Blob TestItem01 ...: ");
                    result = await restApiUpdateMaster.GetBlob("TestItem01");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Valid Blob Content TestItem01 ...: ");
                        testPassed |= string.Compare(result.Content, TestItem01Value, false) == 0;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;

                    // Read the blob item TestItem02: test for proper content.
                    LogFileWrite("Read Blob TestItem02 ...: ");
                    result = await restApiUpdateMaster.GetBlob("TestItem02");
                    testPassed = result.HandleAs == Result.State.OK;
                    LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: OK, got: {result.HandleAs}");
                    if (testPassed)
                    {
                        LogFileWrite("  Valid Blob Content TestItem02 ...: ");
                        testPassed |= string.Compare(result.Content, bigFishContent, false) == 0;
                        LogFileWriteLine(testPassed ? "pass" : $"FAIL, expected: (empty string), got: {result.Content}");
                    }
                    if (!testPassed) UnexpectedResult();
                    testPassed = true;
                }
                catch (Exception err)
                {
                    LogFileWriteLine($"Untrapped: {err.Message}");
                }

                LogFileWriteLine("---------------------------");
                LogFileWriteLine("### TESTING ENDS ###");
                LogFileWriteLine("---------------------------");
                LogFileWriteLine();

            }
            Console.WriteLine($"Output logged to: {fileName}");
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });

            Console.WriteLine("Press ENTER to finish");
            Console.ReadLine();
        }

        static void UnexpectedResult()
        {
            UnexpectedResult(null);
        }

        static void UnexpectedResult(string message)
        {
            LogFileWriteLine(!string.IsNullOrWhiteSpace(message) ? message : "Non-pass result: press Enter to continue tests");
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
        public static readonly JsonSerializerSettings Config = new JsonSerializerSettings()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.DateTime,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };
    }
}
