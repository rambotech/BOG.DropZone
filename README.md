# BOG.DropZone
![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/DropZone.png "They just keep coming and going, and going and coming!")

A very simple volatile aspnetcore webapi site for inter-application dropoff and 
pickup of payloads (queue), and key/value pair storage (references).  It is inspired 
by [BOG.Pathways.Server](https://github.com/rambotech/BOG.Pathways.Server), but uses 
optional an access and admin tokens for authentication, and auto-creates a dropzone 
as needed.

BOG.DropZone is a pull-only approach: no data is pushed to any user.  The site 
has a Swagger interface for testing.

## How it works
A drop zone is a named location for one or more applications to put payloads (strings) 
into it, and one or more other applications to remove them from it.  Many drop zones 
can be created to faciliate many different types of data transfers.

**Important**: one payload from a sender will go to ONLY one receiver. Observer patterns 
are not supported.  Both payloads and references support an optional expiration date.

![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/flow.png)

In the example above, a server posts work items (payloads) into the dropzone 
named Queries, and the clients poll the dropzone named Questions to retrieve them
(one-to-many). The clients process the work items, then post the resulting items
(payloads) to the dropzone named Answers.  The server polls the dropzone named
Answers for those payloads to collect the processed work.

Also, the server can also use the key/value settings (References) in a dropzone
to semaphore the clients with state information to manage work flow.  And the 
clients can do likewise to semaphore the server.

The drop zone supports seven operational actions, and two admin actions.

### Operational

These actions require the access token, if one is defined for the drop zone site.

*Dropoff* :: places a new string (as a payload) onto the queue of other payloads.

*Pickup* :: removes a string payload from the queue and provides it to the calling client.

*Set Reference* :: creates a key/value pair in the drop zone.

*Get Reference* :: returns the refence value for the specified key within the drop zone.  Returns an empty string if the key doesn't exist.

*List References* :: returns the refence key names available within the drop zone as a string array.

*Get Statistics* :: returns the metrics for the given drop zone.

### Admin

These actions require the admin token, if one is defined for the drop zone site.

*Get Security* :: returns information on client attempts to access the site with an invalid access token value.

*Reset* :: wipes out all drop zones, including their payloads and references.

*Shutdown* :: kills the web server operation.  It must be retarted from the command line.

## Why only strings
The drop zone is only concerned with a payload, not what is inside it.  The content is known to both the sender and receiver, so it can be cast by them.

## Best Practice
The drop zone is intended to be cheap to install and run, and is designed for multiple web servers to spread load and provide redundancy.  As such, it is good for distributing work among various agents without the overhead and frustration of extensive security and routing.

Also, like its inspiration project (BOG.Pathways.Server), it makes no guarantees of delivery and the sender and receiver take all responsibility for resending missing or dropped work.  BOG.DropZone was designed for simplicity, and as such is a good tool for coordinating data among various processes.  If you need guaranteed delivery, look at another project.

The project BOG.DropZone.Client is for applications using BOG.DropZone, and contains support
for encryption.

## Example usage

Build the BOG.DropZone project and run it locally.

Create a console application, and add a reference to BOG.DropZone.Client from either the project here, or v1.8.1 from the Nuget package repository [here](https://www.nuget.org/packages/BOG.DropZone.Client/).  Copy the code below into the main method.

```C#
using BOG.DropZone.Client;
using BOG.DropZone.Client.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BOG.DropZone.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        static async Task RunAsync()
        {
            DropZoneConfig[] dropZoneConfigs = new DropZoneConfig[]
            {
                new DropZoneConfig
                {
                    BaseUrl = "http://localhost:5000",
                    ZoneName = "CityData",
                    Password = "YourPassword",
                    Salt = "YourSalt",
                    UseEncryption = false,
                    AccessToken = "YourAccessToken",
                    AdminToken = "YourAdminToken",
                    TimeoutSeconds = 10
                },
                new DropZoneConfig
                {
                    BaseUrl = "http://localhost:5000",
                    ZoneName = "MysteryPath",
                    Password = "YourPassword1",
                    Salt = "YourSalt1",
                    UseEncryption = false,
                    AccessToken = "YourAccessToken",
                    AdminToken = "YourAdminToken",
                    TimeoutSeconds = 10
                }
            };
            RestApiCalls[] restApi = new RestApiCalls[]
            {
                new RestApiCalls(dropZoneConfigs[0]),
                new RestApiCalls(dropZoneConfigs[1])
            };

            try
            {
                Console.WriteLine("CheckHeartbeat()...");
                var result = await restApi[0].CheckHeartbeat();
                DisplayResult(result, 1000);
                if (result.HandleAs == Result.State.InvalidAuthentication)
                {
                    Console.WriteLine("Invalid access token, testing halted... ");
                    return;
                }

                Console.WriteLine("Reset()... ");
                result = await restApi[0].Reset();
                DisplayResult(result, 100);

                Console.WriteLine("ListReferences()... ");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... ");
                result = await restApi[0].GetReference("Test-Ref01");
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... ");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 1000);

                Console.WriteLine("SetReference()... ");
                result = await restApi[0].SetReference("Test-Ref01", "test ref 1");
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... ");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... ");
                result = await restApi[0].GetReference("Test-Ref01");
                DisplayResult(result, 1000);

                Console.WriteLine("SetReference()... Perishable");
                result = await restApi[0].SetReference("Test-Ref02-Perish-5-Sec", "this will go way", DateTime.Now.AddSeconds(5));
                DisplayResult(result, 1000);

                Console.WriteLine("ListReferences()... shows static and perishable");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... Perishable ... < 1 sec (finds it)");
                result = await restApi[0].GetReference("Test-Ref02-Perish-5-Sec");
                DisplayResult(result, 1000);

                Console.WriteLine("Wait 5 seconds for reference to expire...");
                Thread.Sleep(5000);

                Console.WriteLine("ListReferences()... shows static only... perishable has expired");
                result = await restApi[0].ListReferences();
                DisplayResult(result, -1);

                Console.WriteLine("GetReference()... Perishable ... > 5 sec (perished)");
                result = await restApi[0].GetReference("RefTimed-Good");
                DisplayResult(result, -1);

                Console.WriteLine("SetReference()... BIG...");
                var bigData = new string('X', 524288);
                Console.WriteLine($"{bigData.Length}");
                result = await restApi[0].SetReference("BIG", bigData);
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... BIG...");
                result = await restApi[0].GetReference("BIG");
                Console.WriteLine($"{result.Message.Length}");
                result.Message = string.Empty;
                DisplayResult(result, 1000);

                Console.WriteLine("SetReference()... HUGE... rejected due to size");
                var hugeData = new string('X', 41000000);
                Console.WriteLine($"{hugeData.Length}");
                result = await restApi[0].SetReference("HUGE", hugeData);
                DisplayResult(result, 1000);

                Console.WriteLine("GetReference()... HUGE...");
                result = await restApi[0].GetReference("HUGE");
                Console.WriteLine($"{result.Message.Length}");
                result.Message = string.Empty;
                DisplayResult(result, -1);

                Console.WriteLine("GetStatistics()... ");
                result = await restApi[0].GetStatistics();
                DisplayResult(result, -1);

                Console.WriteLine("GetSecurityInfo()... ");
                result = await restApi[0].GetSecurity();
                DisplayResult(result, -1);

                string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

                Console.WriteLine("*** Drop off some data .. which expires ***");
                for (int index = 0; index < 5; index++)
                {
                    Console.WriteLine($"Add {index}: {set[index % 3]}... ");
                    result = await restApi[0].DropOff(set[index % 3] + $"{index}", DateTime.Now.AddSeconds(3 + index));
                    DisplayResult(result, 0);
                }

                Console.WriteLine("Wait 5 for reference to expire...");
                Thread.Sleep(5000);

                Console.WriteLine("*** Pickup some data .. some of which has expired ***");
                for (int index = 0; index < 5; index++)
                {
                    var target = index == 2 ? 1 : 0;
                    Console.WriteLine($"Retrieve {index} from {dropZoneConfigs[target].ZoneName}: ");
                    result = await restApi[target].Pickup();
                    DisplayResult(result, 0);
                }

                Console.WriteLine("*** Clear ***");
                result = await restApi[0].Clear();
                DisplayResult(result, -1);

                Console.WriteLine("*** Drop off some data.. places 501 items when 500 is the max ***");
                for (int index = 0; index < 501; index++)
                {
                    Console.WriteLine($"Add {index}: {set[index % 3]}... ");
                    result = await restApi[0].DropOff(set[index % 3]);
                    DisplayResult(result, index < 5 ? 1000 : 0);
                }
                Console.WriteLine("GetStatistics()...");
                result = await restApi[0].GetStatistics();
                DisplayResult(result, -1);

                Console.WriteLine("*** Pickup some data ***");
                for (int index = 0; index < 502; index++)
                {
                    var target = index == 2 ? 1 : 0;
                    Console.WriteLine($"Retrieve {index} from {dropZoneConfigs[target].ZoneName}: ");
                    result = await restApi[target].Pickup();
                    DisplayResult(result, index == 2 ? -1 : (index < 5 ? 1000 : 0));
                }

                Console.WriteLine("*** List references ***");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 1000);

                Console.WriteLine("Overload references...");
                for (int index = 0; index < 48; index++)
                {
                    Console.WriteLine($"SetReference(Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}... ");
                    result = await restApi[0].SetReference($"Ref-{BOG.SwissArmyKnife.Formatting.RJLZ(index, 2)}", $"test ref {index}");
                    DisplayResult(result, 0);
                }

                Console.WriteLine("*** List references after overload ***");
                result = await restApi[0].ListReferences();
                DisplayResult(result, 2000);

                Console.WriteLine("GetStatistics()... ");
                result = await restApi[0].GetStatistics();
                DisplayResult(result, -1);

                Console.WriteLine("*** Reset ***");
                result = await restApi[0].Reset();
                DisplayResult(result, 2000);

                Console.WriteLine("*** Shutdown ***");
                result = await restApi[0].Shutdown();
                DisplayResult(result, 2000);

                Console.WriteLine("*** Shutdown (with no answer) ***");
                result = await restApi[1].Shutdown();
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
}
```
