# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

* Added APIs to `TinyAnimation` to allow selecting clips by hash or by name. To take advantage of it, please use `TinyAnimation.StringToHash`.

### Changed


### Deprecated

* `TinyAnimation.SelectClip(World, Entity, int)` was deprecated and renamed `TinyAnimation.SelectClipAtIndex(World, Entity, int)` to more easily differentiate it from the newly introduced `TinyAnimation.SelectClip(World, Entity, uint)`.
* `TinyAnimation.GetDuration(World, Entity, int)` was deprecated and renamed `TinyAnimation.GetDurationAtIndex(World, Entity, int)` to more easily differentiate it from the newly introduced `TinyAnimation.GetDuration(World, Entity, uint)`.

### Fixed

* TinyAnimation no longer causes crashes when instantiating an animated entity from a prefab, if the animation is set to "Play Automatically".
* TinyAnimation now ensures that if a default clip is set on the `Animation` component, it will be the one playing by default.
* Fixed a crash in `TinyAnimation` that happens when animating references and float values on the same entity.
* Fixed an issue in `TinyAnimation` where the `CreateAnimationBinding` attribute would not be able to bind an asset field to an entity field.

### Security




## [0.29.0] - 2020-07-10

### Added


### Changed


### Deprecated


### Removed


### Fixed

* ScreenSpaceToWorldSpace now also works correctly when rendering direct to framebuffer
* ScreenSpaceToWorldSpaceRay is no longer inverted

### Security




## [0.28.0] - 2020-07-10

### Added

* Stats collection for audio
* Stats collection for textures
* Sync scene view to game view in Tiny by pressing Shift + F

### Changed

* Updated minimum Unity Editor version to 2020.1.0b15 (40d9420e7de8)
* TinyAnimation is now allowed to run in play mode
* Change fov of debug camera with Shift + L

### Deprecated


### Removed


### Fixed


### Security




## [0.27.0] - 2020-05-27

### Added

* Add "DisplayText" component for rendering text at runtime.  Functionality is limited and will be expanded in future
  releases.

### Changed
* Updated minimum Unity Editor version to 2020.1.0b9 (9c0aec301c8d)


## [0.26.0] - 2020-05-15

### Changed

* Fixes issue where `ICustomBootstrap` instances would not be used while setting up the `UnityInstance`.
* Removed expired type `DefaultTinyWorldInitialization`.

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
