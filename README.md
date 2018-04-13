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

*Shutdown* :: kilsl the web server operation.  It must be retarted from the command line.


