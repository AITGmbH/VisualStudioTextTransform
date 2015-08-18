#!/bin/bash
if [ -f "bootstrap.sh" ];
then
  ./bootstrap.sh
fi

build="packages/AIT.Build/content/build.sh"
chmod +x "$build"
. $build
do_build $@
