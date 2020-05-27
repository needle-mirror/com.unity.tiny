# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.26.0] - 2020-05-15
* Fixes issue where `ICustomBootstrap` instances would not be used while setting up the `UnityInstance`.
* Removed expired type `DefaultTinyWorldInitialization`.
* Updated minimum Unity Editor version to 2019.3.12f1 (84b23722532d)

## [0.25.0] - 2020-04-30
* Added profiler marker integration to Tiny renderer and enable support in other DOTS packages through ENABLE_PROFILER define.
* Fix entity leak with particle emitters with non-attached particles

## [0.24.0] - 2020-04-09
* Changed all public fields of components in Tiny Animation to be PascalCase instead of camelCase. This is a breaking change and no automatic upgrade has been provided.
* Removed the `TinyAnimationAuthoring` component; Tiny Animation now uses the classic `Animation` component for conversion. This is a breaking change and no automatic upgrade has been provided.
* Added `TinyAnimationMecanimSupport` and `TinyAnimationScalePatcher` to re-introduce features lost by removing `TinyAnimationAuthoring`.
* Removed Animation menu entry: it's incomprehensible and no longer needed.

## [0.23.0] - 2020-03-20
* Fixed an issue where stopping audio would cancel all in-flight web requests as reported https://forum.unity.com/threads/tiny-3rd-party-api-requests-iframes-bug.819057/ (Thank you, Maras!)
* Renamed `DefaultTinyWorldInitialization` to `DefaultWorldInitialization`. The original type is deprecated and will be removed in the next release.
* The interface `Unity.Entities.IJobForEach` is deprecated for Project Tiny. Any jobs using this interface should be changed to use `IJobChunk` and `Entities.ForEach`. Support for `IJobForEach` in Project Tiny will be removed in the next release.


## [0.22.0] - 2020-02-05
* Update package dependencies
* TinyAnimation now has a player with a list of clips + added corresponding APIs

## [0.21.0] - 2020-01-21
* Added Tiny Animation features
* Added volume changes affect audio
* Added pan to audio

## [0.20.0] - 2019-12-10
* Update the package to use Unity '2019.3.0f1' or later
* This is the first release of Project Tiny with conversion workflow
