#!/bin/bash
set -ev

pwd

echo NUGET_API_KEY ... contents hidden
echo NUGET_SOURCE == ${NUGET_SOURCE} 
echo TRAVIS_PULL_REQUEST == ${TRAVIS_PULL_REQUEST}
echo BUILD_CONFIG == ${BUILD_CONFIG}

echo BUILD_DIR == ${BUILD_DIR}

dotnet build -c $BUILD_CONFIG ./src/BOG.DropZone

if [ "${TRAVIS_PULL_REQUEST}" = "false" ] && [ "${TRAVIS_BRANCH}" = "master" ]; then
		dotnet nuget push ./src/BOG.DropZone.Common/bin/$BUILD_DIR/BOG.DropZone.Common.*.nupkg --api-key ${NUGET_API_KEY} --source ${NUGET_SOURCE} --skip-duplicate
		dotnet nuget push ./src/BOG.DropZone.Client/bin/$BUILD_DIR/BOG.DropZone.Client.*.nupkg --api-key ${NUGET_API_KEY} --source ${NUGET_SOURCE} --skip-duplicate
fi
