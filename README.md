# BOG.DropZone
A very simple non-secure, volatile aspnetcore webapi site for inter-application dropoff and pickup of payloads.  It is inspired by BOG.Pathways.Server, but removes security tokens and other auth, and auto-creates a dropzone as needed.

## How it works
The drop zone supports four operational actions, and two admin actions.

### Operational
*Dropoff* :: places a new string (as a payload) onto the queue of other payloads.

*Pickup* :: removes a string payload from the queue and provides it to the calling client.

*Set Reference* :: creates a key/value pair in the pathway.

*Get Reference* :: returns the refence value for the specified key within the pathway.  Returns an empty string if the key doesn't exist.


### Admin
*Reset* :: wipes out all pathways, including their payloads and references.

*Shutdown* :: kills the web server operation.  It must be retarted from the command line.

## Why only strings
The drop zone is only concerned with a payload, not what is inside it.  The content is known to both the sender and receiver, so it can be cast by them.

## Best Practice
The drop zone is intended to be cheap to install and run, and is designed for multiple web servers to spread load and provide redundancy.  As such, it is good for distributing work among various agents without the overhead and frustration of extensive security and routing.

Also, like its inpiration project (BOG.Pathways.Server), it makes no guarantees of delivery and the sender and receiver take all responsibility for resending missing or dropped work.  BOG.DropZone was designed for simplicity, and as such is a good tool for coordinating data among various processes.  If you need guaranteed delivery, look at another project.

If you need any level of security or anti-tampering, consider BOG.Pathways.Server instead.


