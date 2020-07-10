#!/bin/bash

MONO=mono
if [ "$WINDIR" != "" ] ; then
    MONO=
fi

${MONO} ../../../com.unity.dots.runtime/bee~/bee.exe $*

