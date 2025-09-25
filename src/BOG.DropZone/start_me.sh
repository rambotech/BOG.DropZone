#!/bin/sh

# Start for Linux machines
./BOG.DropZone \
		--AccessToken YourAccessTokenValueHere \
		--AdminToken YourAdminTokenValueHere \
		--MaxDropzones 5 \
		--MaximumFailedAttemptsBeforeLockout 3 \
		--LockoutSeconds 300 \
		--HttpPort 5005 \
		--HttpsPort 5445 \
		--UseReverseProxy true \
		--KnownProxies 172.25.175.1,172.18.144.1

