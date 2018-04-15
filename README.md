# BOG.DropZone
![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/DropZone.png "They just keep coming and going, and going and coming!")

A very simple non-secure, volatile aspnetcore webapi site for inter-application dropoff and pickup of payloads.  It is inspired by BOG.Pathways.Server, but removes security tokens and other auth, and auto-creates a dropzone as needed.

## How it works
A drop zone is a named location for one or more applications to put payloads (strings) into it, and one or more other applications to remove them from it.  The name designates the role and type of traffic for sender's and receivers.  Many drop zones can be created to faciliate many different types of data transfers.

**Important**: one payload from a sender will go to ONLY one receiver.  Observer patterns are not supported.

A drop zone also has a set of key/value pairs as a dictionary.  Unlike payloads, they are not removed when read.  They can be used to hold static reference content, or state.

### Operational

The drop zone supports four operational actions, and two admin actions.

*Dropoff* :: places a new string (as a payload) onto the queue of other payloads.

*Pickup* :: removes a string payload from the queue and provides it to the calling client.

*Set Reference* :: creates a key/value pair in the pathway.

*Get Reference* :: returns the refence value for the specified key within the pathway.  Returns an empty string if the key doesn't exist.

NOTE: The reference key "info" is reserved for internal use.  When *Get Reference* is called with this key, a json blob of usage and state statistics for the drop zone is the value.

### Admin
*Reset* :: wipes out all pathways, including their payloads and references.

*Shutdown* :: kills the web server operation.  It must be retarted from the command line.

## Why only strings
The drop zone is only concerned with a payload, not what is inside it.  The content is known to both the sender and receiver, so it can be cast by them.

## Best Practice
The drop zone is intended to be cheap to install and run, and is designed for multiple web servers to spread load and provide redundancy.  As such, it is good for distributing work among various agents without the overhead and frustration of extensive security and routing.

Also, like its inpiration project (BOG.Pathways.Server), it makes no guarantees of delivery and the sender and receiver take all responsibility for resending missing or dropped work.  BOG.DropZone was designed for simplicity, and as such is a good tool for coordinating data among various processes.  If you need guaranteed delivery, look at another project.

If you need any level of security or anti-tampering, consider BOG.Pathways.Server instead.

## Example usage

Create a console application, and add a reference to BOG.DropZone.Client from either the project here, or from the Nuget package [here](https://www.nuget.org/packages/BOG.DropZone.Client/).  Copy the code below into the main method.

```C#
using System;
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
            var restApi = new RestApi("http://localhost:54771");  // adjust the port to the port used by BOG.DropZone

            try
            {
                Console.WriteLine("State 1");
                Console.WriteLine(await restApi.GetReference("CityData", "info"));
                string[] set = new string[] { "Tallahunda", "Kookamunga", "Whatever" };

                for (int index = 0; index < 20; index++)
                {
                    Console.WriteLine($"Add {index}");
                    await restApi.DropOff("CityData", set[index % 3]);
                }
                Console.WriteLine("State 2");
                Console.WriteLine(await restApi.GetReference("Questions", "info"));

                for (int index = 0; index < 25; index++)
                {
                    try
                    {
                        Console.WriteLine($"Retrieve {index}");
                        Console.WriteLine(await restApi.Pickup("CityData"));
                    }
                    catch (RestApiNonSuccessException err)
                    {
                        Console.WriteLine($"Oops #{index}: {err.StatusCode.ToString()}, {err.Message}");
                    }
                    catch (Exception errGeneral)
                    {
                        Console.WriteLine($"Crash #{index}: this was unexpected: {errGeneral.Message}");
                    }
                }

                Console.WriteLine("Final");
                Console.WriteLine(await restApi.GetReference("Questions", "info"));
                Console.WriteLine("Set 1");
                await restApi.SetReference("Questions", "T1", "The kitty from down the street");
                Console.WriteLine("Get 1");
                Console.WriteLine(await restApi.GetReference("Questions", "T1"));

                // await restApi.Reset();
                // await restApi.Shutdown();
            }
            catch (RestApiNonSuccessException err)
            {
                Console.WriteLine($"Oops (main): {err.StatusCode.ToString()}, {err.Message}");
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

