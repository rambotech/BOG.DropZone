| BOG.DropZone  | BOG.DropZone.Client   | BOG.DropZone.Common |
|---    |---    |---    |
| master    | [![Build status](https://api.travis-ci.com/rambotech/BOG.DropZone.svg?branch=master)](https://travis-ci.com/rambotech/BOG.DropZone){target="_blank"} |  [![Nuget](https://img.shields.io/nuget/v/BOG.DropZone.Client)](https://www.nuget.org/packages/BOG.DropZone.Client/){target="_blank"}  | [![Nuget](https://img.shields.io/nuget/v/BOG.DropZone.Client)](https://www.nuget.org/packages/BOG.DropZone.Common/){target="_blank"} |
| release   | [![Build status](https://api.travis-ci.com/rambotech/BOG.DropZone.svg?branch=release)](https://travis-ci.com/rambotech/BOG.DropZone){target="_blank"} | (n/a)  |  (n/a) |
| develop   | [![Build status](https://api.travis-ci.com/rambotech/BOG.DropZone.svg?branch=develop)](https://travis-ci.com/rambotech/BOG.DropZone){target="_blank"} | (n/a)  |  (n/a) |

# BOG.DropZone
![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/DropZone.png "They just keep coming and going, and going and coming!")

A very simple volatile aspnetcore webapi site for inter-application dropoff and pickup of payloads (queue), and key/value pair storage (references).
It is inspired by [BOG.Pathways.Server](https://github.com/rambotech/BOG.Pathways.Server), but uses access and admin tokens for
authentication, and auto-creates a dropzone as needed.

BOG.DropZone is a pull-only approach: no data is pushed to any user.  The site has a Swagger interface for testing.

## How it works
A drop zone is a named location for one or more applications to put payloads (strings) 
into it, and one or more other applications to remove them from it.  Many drop zones 
can be created to faciliate many different types of data transfers.

**Important**: one payload from a sender will go to ONLY one receiver. Observer patterns 
are not supported.  Both payloads and references support an optional expiration date.

**Change to v2.0**: Prior to v2.0, all payloads were enqueued into the same single queue, 
and the payloads were dequeued on a first-come, first-server basis, regardless of the request 
for pickup.

*v2.0* now supports multiple queues for a targeted audience defined by the applications.  If 
no recipient is specified, the behavior is to queue the payload in a general area (as before).
The payloads in the general queue are dequeued and given to a caller picking up from the dropzone 
with no recipient specified.

*If a recipient is specified for a payload when it is dropped off, it is only dequeued and given 
to a caller during a pickup call when the caller specifies that same recipient value*. It can be 
thought of as a mailbag name. The intended recipient is specified as an optional recipient query 
argument when using the endpoint.  The RestApiCalls client assembly for C# (available on Nuget) 
now has an optional argument on the Dropoff() and Pickup() methods for this.

Note that this change to allow routing does not change the one payload to one receiver rule: it 
only allows directing a payload to a specific receiver, or one or a subset of receivers which can 
pick it up (i.e. using the recipient identifier as a group identifier). The applications will have 
to manage discovery and knowledge of the receivers.

![alt text](https://github.com/rambotech/BOG.DropZone/blob/master/assets/flow.png)

In the example above, a server posts work items (payloads) into the dropzone 
named Questions, and the clients poll the dropzone named Questions to retrieve them
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
The recipient can be specific, or global.

*Pickup* :: removes a string payload from the queue and provides it to the calling client.

*Set Reference* :: creates a key/value pair in the drop zone.

*Get Reference* :: returns the refence value for the specified key within the drop zone.
Returns an empty string if the key doesn't exist.

*List References* :: returns the refence key names available within the drop zone as a string array.

*Get Statistics* :: returns the metrics for the given drop zone.

### Admin

These actions require the admin token, if one is defined for the drop zone site.

*Get Security Info* :: returns information on client attempts to access the site with an 
invalid access token value.

*Reset* :: wipes out all drop zones, including their payloads and references.

*Shutdown* :: stops the web server operation.  It must be retarted from the command line.

## Why only strings
The drop zone is only concerned with a payload, not what is inside it.  The content is known to both 
the sender and receiver, so it can be cast by them.

## Best Practice
The drop zone is intended to be cheap to install and run, and can run on multiple web servers to spread 
the load and provide redundancy. There is no intercommunication among drop zones.

Also, like its inspiration project (BOG.Pathways.Server), it makes no guarantees of delivery and the 
sender and receiver take all responsibility for resending missing or dropped work.  BOG.DropZone was 
designed for simplicity, and as such is a good tool for coordinating data among various processes. If 
you need guaranteed delivery, look at another project.

The project BOG.DropZone.Client (written in .NET Standard) is for .NET applications using BOG.DropZone, 
and contains support for encryption of payloads.

## Example usage

The project BOG.DropZone.Test demonstrates all the functionality of the DropZone. This project can be demonstrated as follows.

- Build the entire solution as debug.
- Right click on the project BOG.DropZone.Test, and select "Set as Startup Project" from the menu.
- Right click on the project BOG.DropZone, and select "Open in Terminal" from the menu.
- Enter these commands in the terminal window:
  - ```cd .\bin\Debug\net5.0\```
  - ```.\start_me.bat```
- The rerminal window will display the following:

```
AccessToken: YourAccessTokenValueHere
AdminToken: YourAdminTokenValueHere
MaxDropzones: 5
MaximumFailedAttemptsBeforeLockout: 3
LockoutSeconds: 300
Hosting environment: Development
Content root path: C:\src\BOG\Public\BOG.DropZone\src\BOG.DropZone\bin\Debug\net5.0
Now listening on: http://[::]:5000
Application started. Press Ctrl+C to shut down.
```

- Now run the app BOG.DropZone.Test by pressing the play button in the toolbar, or by pressing ```Ctrl+F5```
- If you would like to set breakpoints in BOG.DropZone.Test and run in Debug, pressing ```F5``` after setting your breakpoints.
- When complete, close the Developer terminal window.




