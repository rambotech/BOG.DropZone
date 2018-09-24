# BOG.DropZone
![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/DropZone.png "They just keep coming and going, and going and coming!")

A very simple volatile aspnetcore webapi site for inter-application dropoff and 
pickup of payloads (queue), and key/value pair storage (references).  It is inspired 
by [BOG.Pathways.Server](https://github.com/rambotech/BOG.Pathways.Server), but uses 
optional an access and admin tokens for authentication, and auto-creates a dropzone 
as needed.

BOG.DropZone is a pull-only approach: no data is pushed to any user.

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

### Operational

The drop zone supports seven operational actions, and two admin actions.

*Dropoff* :: places a new string (as a payload) onto the queue of other payloads.

*Pickup* :: removes a string payload from the queue and provides it to the calling client.

*Set Reference* :: creates a key/value pair in the drop zone.

*Get Reference* :: returns the refence value for the specified key within the drop zone.  Returns an empty string if the key doesn't exist.

*List References* :: returns the refence key names available within the drop zone as a string array.

*Get Statistics* :: returns the metrics for the given drop zone.

*Get Security* :: returns information on client attempts to access the site with an invalid access token value.

NOTE: The reference key "info" is reserved for internal use.  When *Get Reference* is called with this key, a json blob of usage and state statistics for the drop zone is the value.

### Admin
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

Create a console application, and add a reference to BOG.DropZone.Client from either the project here, or from the Nuget package [here](https://www.nuget.org/packages/BOG.DropZone.Client/).  Copy the code below into the main method.

```C#
using System;
using System.IO;
using System.Threading.Tasks;
using BOG.DropZone.Client;

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
            var useConfig = new DropZoneConfig
            {
                BaseUrl = "http://localhost:5000",
                Password = "YourPassword",
                Salt = "YourSalt",
                UseEncryption = true,
                AccessToken = "YourAccessValueHere",
                AdminToken = "YourAdminValueHere",
                TimeoutSeconds = 10
            };
            RestApiCalls restApi = new RestApiCalls(useConfig);

            try
            {
                Console.Write("Reset()... ");
                var result = await restApi.Reset();
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");

                Console.Write("GetReference()... ");
                result = await restApi.GetReference("CityData", "Ref1");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                Console.Write("SetReference()... ");
                result = await restApi.SetReference("CityData", "Ref1", "test ref 1");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine();

                Console.Write("GetReference()... ");
                result = await restApi.GetReference("CityData", "Ref1");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                Console.Write("GetStatistics()... ");
                result = await restApi.GetStatistics("CityData");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                Console.Write("GetSecurityInfo()... ");
                result = await restApi.GetSecurity();
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

                Console.WriteLine("*** Drop off some data ***");
                for (int index = 0; index < 501; index++)
                {
                    Console.Write($"Add {index}: {set[index % 3]}... ");
                    // result = await restApi.DropOff("CityData", set[index % 3]);
                    result = await restApi.DropOff("CityData", bigData);
                    Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                    Console.WriteLine($"has: {result.Content}");
                    Console.WriteLine();
                }
                Console.WriteLine("GetStatistics()...");
                result = await restApi.GetStatistics("CityData");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                Console.WriteLine("*** Pickup some data ***");
                for (int index = 0; index < 502; index++)
                {
                    var dropZoneName = index == 6 ? "MysteryPath" : "CityData";
                    Console.WriteLine($"Retrieve {index} from {dropZoneName}: ");
                    result = await restApi.Pickup(dropZoneName);
                    Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                    if (result.HandleAs == Client.Model.Result.State.OK)
                    {
                        Console.WriteLine($"has: {result.Content.Length}");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("*** List references ***");
                result = await restApi.ListReferences("CityData");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine();

                Console.WriteLine("Overload references...");
                for (int index = 0; index < 51; index++)
                {
                    Console.Write($"SetReference(Ref-{index.ToString()})... ");
                    result = await restApi.SetReference("CityData", $"Ref-{index.ToString()}", $"test ref {index}");
                    Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                    Console.WriteLine();
                }

                Console.WriteLine("*** List references after overload ***");
                result = await restApi.ListReferences("CityData");
                Console.WriteLine($"{result.StatusCode.ToString()}: {result.HandleAs.ToString()}");
                Console.WriteLine($"has: {result.Content}");
                Console.WriteLine();

                //await restApi.Reset();

                //await restApi.Shutdown();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Untrapped: {err.Message.ToString()}");
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
```
