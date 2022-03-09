PUSHD %~d0%~p0
BOG.DropZone.exe --AccessToken YourAccessTokenValueHere --AdminToken YourAdminTokenValueHere --MaxDropzones 5 --MaximumFailedAttemptsBeforeLockout 3 --LockoutSeconds 300
POPD