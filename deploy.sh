ApiKey=$1
Source=$2

nuget push ./src/BOG.DropZone.Client/bin/Release/BOG.DropZone.Common.*.nupkg -Verbosity detailed -ApiKey $ApiKey -Source $Source
nuget push ./src/BOG.DropZone.Client/bin/Release/BOG.DropZone.Client.*.nupkg -Verbosity detailed -ApiKey $ApiKey -Source $Source
