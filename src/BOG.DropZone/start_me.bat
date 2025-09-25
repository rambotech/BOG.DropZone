PUSHD %~d0%~p0

BOG.DropZone.exe ^
--AccessToken YourAccessTokenValueHere ^
--AdminToken YourAdminTokenValueHere ^
--MaxDropzones 5 ^
--MaximumFailedAttemptsBeforeLockout 3 ^
--LockoutSeconds 300 ^
--HttpPort 5005 ^
--HttpsPort 5445 ^
--UseReverseProxy true ^
--KnownProxies 192.168.175.1,192.168.144.1

POPD

