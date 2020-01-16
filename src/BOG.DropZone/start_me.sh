#!/bin/sh

# Start for Linux machines
./BOG.DropZone --AccessToken YourAccessValueHere --AdminToken YourAdminValueHere --MaxDropzones 5 --MaximumFailedAttemptsBeforeLockout 3 --LockoutSeconds 300 --HttpsPort 5001
