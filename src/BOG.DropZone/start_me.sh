#!/bin/sh

# Start for Linux machines
./BOG.DropZone \
		--AccessToken YourAccessTokenValueHere \
		--AdminToken YourAdminTokenValueHere \
		--MaxDropzones 5 \
		--MaximumFailedAttemptsBeforeLockout 3 \
		--LockoutSeconds 300 \
		--HttpPort 5000 \
		--HttpsPort 5001 \
		--UseReverseProxy true \
		--KnownProxies 172.25.175.1,172.18.144.1

