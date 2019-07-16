using BOG.DropZone.Client;
using BOG.DropZone.Client.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BOG.DropZone.Test
{
    class Program
    {
        static void Main()
        {
            RunAsync().GetAwaiter().GetResult();
        }

        static async Task RunAsync()
        {
            int testIndex = 0;

            // Add your own test location to this array, and set the testIndex value to run against that location.
            DropZoneConfig[] dropZoneConfigs = new DropZoneConfig[]
            {
                new DropZoneConfig
                {
                    BaseUrl = "http://localhost:5000",
                    ZoneName = "CityData",
                    Password = "YourPassword",
                    Salt = "YourSalt",
                    UseEncryption = false,
                    AccessToken = "YourAccessValueHere",
                    AdminToken = "YourAdminValueHere ",
                    TimeoutSeconds = 10
                }
            };
            RestApiCalls restApi = new RestApiCalls(dropZoneConfigs[testIndex]);

            try
            {
                Console.WriteLine("CheckHeartbeat()...");
                Result result = await restApi.CheckHeartbeat();
                DisplayResult(result, 1000);
                if (result.HandleAs == Result.State.InvalidAuthentication)
                {
                    Console.WriteLine("Invalid auth token, testing halted... ");
                    return;
                }

                Console.WriteLine("Reset()... ");
                result = await restApi.Reset();
                DisplayResult(result, 100);

                Console.WriteLine("ListReferences()... ");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetReference()... ");
                result = await restApi.GetReference("Test-Ref01");
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... ");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("SetReference()... ");
                result = await restApi.SetReference("Test-Ref01", "test ref 1");
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... ");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetReference()... ");
                result = await restApi.GetReference("Test-Ref01");
                DisplayResult(result, 1000);

                Console.WriteLine("SetReference()... Perishable");
                result = await restApi.SetReference("Test-Ref02-Perish-5-Sec", "this will go way", DateTime.Now.AddSeconds(5));
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... shows static and perishable");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetReference()... Perishable ... < 1 sec (finds it)");
                result = await restApi.GetReference("Test-Ref02-Perish-5-Sec");
                DisplayResult(result, 1000);

                Console.WriteLine("Wait 5 seconds for reference to expire...");
                Thread.Sleep(5000);

                Console.WriteLine("ListReferences()... shows static only... perishable has expired");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetReference()... Perishable ... > 5 sec (perished)");
                result = await restApi.GetReference("RefTimed-Good");
                DisplayResult(result, -1);

                Console.WriteLine("SetReference()... BIG...");
                var bigData = new string('X', 524288);
                Console.WriteLine($"{bigData.Length}");
                result = await restApi.SetReference("BIG", bigData);
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... BIG...");
                result = await restApi.GetReference("BIG");
                Console.WriteLine($"{result.Message.Length}");
                result.Message = string.Empty;
                DisplayResult(result, 1000);

                Console.WriteLine("SetReference()... HUGE... rejected due to size");
                var hugeData = new string('X', 41000000);
                Console.WriteLine($"{hugeData.Length}");
                result = await restApi.SetReference("HUGE", hugeData);
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... HUGE...");
                result = await restApi.GetReference("HUGE");
                Console.WriteLine($"{result.Message.Length}");
                result.Message = string.Empty;
                DisplayResult(result, -1);

                Console.WriteLine("GetStatistics()... ");
                result = await restApi.GetStatistics();
                DisplayResult(result, -1);
                Console.WriteLine("GetSecurityInfo()... ");
                result = await restApi.GetSecurity();
                DisplayResult(result, -1);

                string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

                Console.WriteLine("*** Drop off some data .. which expires ***");
                for (int index = 0; index < 5; index++)
                {
                    Console.WriteLine($"Add {index}: {set[index % 3]}... ");
                    result = await restApi.DropOff(set[index % 3] + $"{index}", DateTime.Now.AddSeconds(3 + index));
                    DisplayResult(result, 0);
                }

                Console.WriteLine("Wait 5 for reference to expire...");
                Thread.Sleep(5000);

                Console.WriteLine("*** Pickup some data .. some of which has expired ***");
                for (int index = 0; index < 5; index++)
                {
                    Console.WriteLine($"Retrieve {index} from {dropZoneConfigs[testIndex].ZoneName}: ");
                    result = await restApi.Pickup();
                    DisplayResult(result, 0);
                }

                Console.WriteLine("*** Clear ***");
                result = await restApi.Clear();
                DisplayResult(result, -1);

                Console.WriteLine("*** Drop off some data.. places 501 items when 500 is the max ***");
                for (int index = 0; index < 501; index++)
                {
                    Console.WriteLine($"Add {index}: {set[index % 3]}... ");
                    result = await restApi.DropOff(set[index % 3]);
                    DisplayResult(result, index < 5 ? 1000 : 0);
                }
                Console.WriteLine("GetStatistics()...");
                result = await restApi.GetStatistics();
                DisplayResult(result, -1);

                Console.WriteLine("*** Pickup some data ***");
                for (int index = 0; index < 502; index++)
                {
                    Console.WriteLine($"Retrieve {index} from {dropZoneConfigs[testIndex].ZoneName}: ");
                    result = await restApi.Pickup();
                    DisplayResult(result, index == 2 ? -1 : (index < 5 ? 1000 : 0));
                }

                Console.WriteLine("*** List references ***");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("Overload references...");
                for (int index = 0; index < 48; index++)
                {
                    Console.WriteLine($"SetReference(Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}... ");
                    result = await restApi.SetReference($"Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}", $"test ref {index}");
                    DisplayResult(result, 0);
                }

                Console.WriteLine("*** List references after overload ***");
                result = await restApi.ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetStatistics()... ");
                result = await restApi.GetStatistics();
                DisplayResult(result, -1);

                Console.WriteLine("*** Reset ***");
                result = await restApi.Reset();
                DisplayResult(result, 2000);

                Console.WriteLine("*** Shutdown ***");
                result = await restApi.Shutdown();
                DisplayResult(result, 2000);

                Console.WriteLine("*** Shutdown (with no answer) ***");
                result = await restApi.Shutdown();
                DisplayResult(result, 2000);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Untrapped: {err.Message.ToString()}");
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
