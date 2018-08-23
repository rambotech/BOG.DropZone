# BOG.DropZone
![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/DropZone.png "They just keep coming and going, and going and coming!")

A very simple volatile aspnetcore webapi site for inter-application dropoff and pickup of payloads.  It is inspired by [BOG.Pathways.Server](https://github.com/rambotech/BOG.Pathways.Server), but uses an optional access token for authentication, and auto-creates a dropzone as needed.

## How it works
A drop zone is a named location for one or more applications to put payloads (strings) into it, and one or more other applications to remove them from it.  The name designates the role and type of traffic for sender's and receivers.  Many drop zones can be created to faciliate many different types of data transfers.

**Important**: one payload from a sender will go to ONLY one receiver.  Observer patterns are not supported.

A drop zone also has a set of key/value pairs as a dictionary.  Unlike payloads, they are not removed when read.  They can be used to hold static reference content, or state.

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

If you need any level of security or anti-tampering, check out the SecureGram class in [BOG.SwissArmyKnife](https://www.nuget.org/packages/BOG.SwissArmyKnife/).  It's source repo is a sister project in my repo collection here.

## Example usage

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
            // adjust the port to the port used by BOG.DropZone
            // var restApi = new RestApiCalls("http://localhost:54771");
            // var restApi = new RestApiCalls("http://localhost:54771", "YourAccessTokenValue");
            // var restApi = new RestApiCalls("http://localhost:54771", "optional_password", "optional_salt");
            // var restApi = new RestApiCalls("http://localhost:54771", "YourAccessTokenValue", "optional_password", "optional_salt");

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
