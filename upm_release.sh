#!/bin/bash

absolute_path() {
  DIR="$(echo "$(cd "$(dirname "$1")"; pwd)")"
  case $(basename $1) in
      ..) echo "$(dirname $DIR)";;
      .)  echo "$DIR";;
      *)  echo "$DIR/$(basename $1)";;
  esac
}

# checking if a directory is provided
if [ -z "$1" ] ; then
  echo "Please provide a directory to setup the sandbox. The directory should be different than analytics-csharp's directory"
  echo "Usage: $0 <directory to setup sandbox>"
  exit 1
fi
if ! [ -d "$1" ]; then
    echo "$1 does not exist."
    exit 1
fi
if [[ "$(absolute_path "$1")" = $PWD* ]]; then
    echo "Please provide a directory different than analytics-csharp's directory"
    exit 1
fi
cd "$1" || exit 


echo "checking required tools..."

# checking required tools
if ! command -v git &> /dev/null
then
    echo "git could not be found"
    exit 1
fi
if ! command -v nuget &> /dev/null
then
    echo "nuget could not be found"
    exit 1
fi
if ! command -v jq &> /dev/null
then
    echo "jq could not be found"
    exit 1
fi

echo "looking for unity executable path..."
UNITY=$(find /Applications/Unity -type f -name 'Unity' | head -n 1)
echo "Unity executable found at $UNITY"
if [ -z "$UNITY" ]
then
      echo "unity executable is not found. make sure you have installed unity"
      exit 
else
  echo "Unity executable found at $UNITY"
fi

echo "setting up release sandbox ..."
mkdir sandbox
cd sandbox

# download analytics-csharp, so it's isolated
git clone https://github.com/segmentio/Analytics-CSharp.git
cd Analytics-CSharp || exit 

echo "fetching the current version of project ..."
VERSION=$(grep '<Version>' Analytics-CSharp/Analytics-CSharp.csproj | sed "s@.*<Version>\(.*\)</Version>.*@\1@")
echo "releasing version $VERSION ..."

git checkout upm
cd ..

echo "packing ..."
if [ "$(jq -r '.version' Analytics-CSharp/package.json)" == $VERSION ]
then
  echo "$VERSION is the same as the current package version"
  exit 
fi
# update version in package.json
echo "$(jq --arg VERSION "$VERSION" '.version=$VERSION' Analytics-CSharp/package.json)" > Analytics-CSharp/package.json
# remove all files in Plugins folder recursively
rm -rf Analytics-CSharp/Plugins/*
# download analytics-csharp and its dependencies from nuget
nuget install Segment.Analytics.CSharp -Version "$VERSION" -OutputDirectory Analytics-CSharp/Plugins
# loop over all the libs and remove any non-netstandard2.0 libs
for dir in Analytics-CSharp/Plugins/*; do
  if [ -d "$dir" ]; then
    for lib in "$dir"/lib/*; do
      if [ "$lib" != "$dir/lib/netstandard2.0" ]; then
        echo $lib
        rm -rf "$lib"
      fi
    done
  fi
done

echo "generating meta files ..."
# launch unity to create a dummy head project
"$UNITY" -batchmode -quit -createProject dummy
# update the manifest of dummy head to import the package
echo "$(jq '.dependencies += {"com.segment.analytics.csharp": "file:../../Analytics-CSharp"}' dummy/Packages/manifest.json)" > dummy/Packages/manifest.json
# launch unity in quit mode to generate meta files
"$UNITY" -batchmode -quit -projectPath dummy

echo "releasing ..."
# commit all the changes
cd Analytics-CSharp || exit 
git add .
git commit -m "prepare release $VERSION"
# create and push a new tag, openupm will pick up this new tag and release it automatically
git tag unity/"$VERSION"
git push && git push --tags
cd ..

echo "cleaning up"
# clean up sandbox
cd ..
rm -rf sandbox

echo "done!"



