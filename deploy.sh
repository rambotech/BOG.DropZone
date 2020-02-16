#!/bin/bash
set -ev

pwd

echo NUGET_API_KEY
echo NUGET_SOURCE == ${NUGET_SOURCE} 
echo TRAVIS_PULL_REQUEST == ${TRAVIS_PULL_REQUEST}

if [ "${BUILD_CONFIG}" = "release" ]; then
		BUILD_DIR="release"
else
		BUILD_DIR=$BUILD_CONFIG
fi

dotnet build -c $BUILD_CONFIG ./src/BOG.DropZone.sln

if [ "${TRAVIS_PULL_REQUEST}" = "false" ] && [ "${TRAVIS_BRANCH}" = "master" ]; then
		dotnet nuget push ./src/BOG.DropZone.Common/bin/$BUILD_DIR/BOG.DropZone.Common.*.nupkg --api-key ${NUGET_API_KEY} --source ${NUGET_SOURCE} --skip-duplicate
		dotnet nuget push ./src/BOG.DropZone.Client/bin/$BUILD_DIR/BOG.DropZone.Client.*.nupkg --api-key ${NUGET_API_KEY} --source ${NUGET_SOURCE} --skip-duplicate
fi
