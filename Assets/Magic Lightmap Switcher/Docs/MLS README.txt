Magic Lightmap Switcher

  What is it?
  -----------

  Magic Lightmap Switcher is a Unity editor extension that will allow you 
  to store baked lightmap data and then switch between its instantly or interpolate smoothly. 
  
  The following data is involved in switching and blending:
    * Lightmaps
    * Lightprobes
    * Reflection probes
    * Light sources settings
    * Skybox texture
    * Scene fog settings
    * Any custom data such as post-processing settings, Volume component settings in SRP, 
      or variable values of any of your script
  
  System features:
    * Switching and blending all available baked data in the scene at runtime
    * Using shaders and multithreading for system operation, which allows you to maintain high performance
    * Support for multiple lighting scenarios for one scene
    * Automatic baking and storing process of lightmaps in turn order

  The Latest Version
  ------------------
    
  The latest version is always available in the asset store. Interim updates 
  not published in the asset store can be obtained by contacting the developer. 
  
  Documentation
  -------------
    
  Up-to-date documentation is located at
  https://motiongamesstudio.gitbook.io/magic-lightmap-switcher/

  Additional Information
  -------------

  About error "Failed to reserve memory for scene-based lightmaps"
  https://forum.unity.com/threads/lightmaps-always-bake-but-unity-says-its-out-of-memory-to-store-them.644632/

  CHANGELOG

  v 1.5.0
    -------------

    Bug Fixes:
      - Core: Fixed an issue where texture arrays could not initialize on the Android platform
      - Core: Fixed an issue where lightmaps on the terrain were not displayed if Texture2DArrays mode was not selected
      - Core: Fixed a bug where objects and light sources could receive the same GUID.
      - Core: Fixed a bug where objects outside the camera's view did not receive correct lightmaps
      - Core: Fixed a bug that caused the lightmaps to look incorrect immediately after loading the scene in the editor
      - Core: Fixed a bug where mixing modules were incorrectly taken into account when executing the switching or mixing code
      - Core: Fixed an error where the path to the SRP folder was not determined correctly during the patch, which caused the shader patch to run with errors
      - Preset Manager: Fixed a bug where the values in the preset were equated to the values in the inspector as soon as the cursor was set to the inspector when the preset was active
      - Preset Manager: Fixed a bug where the ranges of the luminaires in the Area/Rectangle mode were not saved correctly
      - Preset Manager: Fixed an error that caused endless initialization when moving to a stage that did not contain an MLS instance and the preset manager window was open
      - Preset Manager (Bakery): Fixed a bug where the Skylight parameters were broken after viewing the component in the preset manager
      - Lighting Scenario: Fixed a bug where the lightmap and reflection mixing ranges were clamped immediately after adding a preset to the script
      - Lighting Scenario: Fixed a bug where the value of Custom Blendable was mixed incorrectly
      - Lighting Scenario: Fixed an error where the Custom Blendable value was not set correctly after adding a new preset to the mixing queue
      - Lighting Scenario: Fixed a bug where renaming the script did not work correctly      

    Improvements:
      - Core: Added the ability to view lightmap mixing in debugging mode
      - Core: Added support for Skinned Mesh
      - Core: Added the MLS Baked Skinned Mesh component, which allows you to copy lightmap data from a similar NON-Skinned Mesh
      - Core: Added support for Panoramic Skybox for URP
      - Core: Added the Blending callback.currentBlendingValue (float)
      - Support Packages: Added support for URP 16 - 17
      - Support Packages: Added support for HDRP 17
      - Lighting Scenario: Changed the behavior of the "Cyclic Blend" option
      - Lighting Scenario: In the "Custom Blendable" option, the Instant Switching option has been added (switches values without interpolation)
      - Lighting Scenario: The option to select the script that will be loaded at default startup has been added
      - Lighting Scenario: Added a script check that reveals inconsistencies in the number of baked objects
      - Lighting Preset: The "Select On Scene" option has been added to the game objects section
      - MLS Manager: Added "Select/Deselect All" option for presets
      - MLS Manager: Added "Remove Selected" option for presets
      - Tools: Added a stage cleaning module (MLS Component Cleaner). It may be useful if unexpected errors occur

    Changes:
      - Core: Event management mechanics disabled due to constantly occurring errors in the module
      - Lighting Scenario UI: The "Global Blend" area display style has been changed      

  v 1.2.2
    -------------
          
    Bug Fixes:   
      - Preset Manager: Fixed the behavior of the Preset Manager window in Mac OS. 
        It no longer goes into the background when switching focus to scene objects
      
    Improvements: 
      - Preset Manager: Added the ability to manage any parameters of any Skybox material
      - Preset Manager: Added the ability to control any parameters of the object's material
      - Support Packages: Added support for SRP 15+
      - Lighting Scenario: Added the option to ignore the need for a camera with the "Main" tag on the scene

  v 1.2.1
    -------------
          
    Bug Fixes:   
      - Preset Manager: Fixed a bug that caused Bakery Sky 
        to be saved incorrectly, which led to actual duplication 
        of settings and incorrect baking    
      - Data Storing: Fixed an error saving Environment settings
      - Core: Fixed a bug when light probes were removed from the scene
      - Core: Fixed a bug where UV was broken in Play Mode or Build
      - Core: Fixed a bug due to which light probe data was not loaded 
        in "Switching Only" mode
      - Core: Fixed a bug due to which the reflection probe data was 
        not loaded in the "Switching Only" mode
      - Core: Fixed a bug that caused lightmaps not to load in the build
      
    Improvements: 
      - Support Packages: Added support for SRP 14+
      
    Changes:
      - The scene with the demonstration of Bakery Volumes support 
        has been moved to the Bakery Support Package
  
  v 1.2.0
    -------------
          
    Bug Fixes:   
      - Core: Fixed a bug that caused UpdateStoredArray to 
        be called immediately after adding the MLS instance to the scene    
      - Core: UV bug in the build when using Unlit materials 
      - Shader Code Modifier: Special characters in the path cause errors
      - Preset Manager: Fixed bug that caused Shadow Strength to be 
        reset to 0 when duplicating a preset
      
    Improvements: 
      - Shader Code Modifier: Changed Shader Patch Algorithm
      - Preset Manager: Added ability to multi-edit objects
      - Preset Manager: Added scroll bar to object groups
      - Core: Automatic reloading of lighting data after changing texture parameters
      - Shader Code Modifier: Added support for SRP 13,14
      
    Changes:
      - Core: Changed the code of the main blending cycle. Added 
        processing of objects excluded from the previous cycle/cycles. 
        Static Batching now works properly.
      - Support Packages: Updated SRP Support Packages
  
  v 1.1.1
  -------------
        
  Bug Fixes:   
    - Core: Shadow mask does not work in URP and HDRP    
    - Custom Blendable: When changing the order of the blending 
      queue, the skybox textures are displayed incorrectly     
  
  v 1.1.0
  -------------
      
  Bug Fixes:   
      - Preset Manager: Fixed an error due to which the "_Tint" 
        property of the skybox could not be assigned correctly.
      - Preset Manager: Fixed a bug where the Shadows Type 
        property of the light source was reset to None. 
      
  Improvements:
      - Core: Added support for Texture2DArrays and TextureCubeArrays, 
        which significantly saves FPS.
      - Lighting Scenario: Added options to disable mixing cycle modules.
      - Core: Added support for Bakery Volumes 
        (operation mode switches between Lightprobes and Bakery Volumes).
  
  v 1.0.0
  -------------
    
  Bug Fixes:   
    - Lighting Scenario: Fixed Events serialization errors.
    
  Improvements:
     - Core: Bakery RNM + SH for SRP added.
     - Core: Added support for Bakery Shader Tweaks.
     - Core: Added support for Deferred mode in HDRP.
  
  v 0.94.5b
  -------------
  
  Bug Fixes:   
     - Core: Lighting Scenario events are reset after editor restart.
  
  Improvements:
     - Lightmap Data Storing: Data is now saved in a folder based on 
       the location of the scene or in a specified custom folder.

  v 0.94.4b
  -------------

  Improvements:
    - SRP 12 support added. (URP + HDRP)

  v 0.94.3b
  -------------

  Bug Fixes:   
    - Custom Blendable: Minor fixes.
    - Preset Manager: Game Objects settings reset during baking with Bakery.
    - Lightmap Data Storing: Fixed a bug due to which Terrain data was saved with errors
    - Lighting Scenario: Fixed a bug that caused Events to work incorrectly

  Improvements:
    - Preset Manager: Added support for saving Environment Lighting settings.
    - Preset Manager: Added options to save light source shadow type (None / Hard / Soft) and Baked Shadow Angle.

  v 0.94.2b
  -------------

  Bug Fixes:   
    - Fixed a bug due to which, immediately after importing a package, an error 
      occurred when determining the SRP version.
    - Fixed a bug due to which presets for Bakery Light Mesh worked incorrectly.

  v 0.94.1b
  -------------

  Bug Fixes:   
    - Fixed a bug due to which it was not possible to correctly create a System Properties asset.
    - Fixed a bug due to which the building of the project failed.

  v 0.94b
  -------------

  Bug Fixes:   
    - Fixed a bug due to which preset data was reset during baking with Bakery.
    - Fixed a bug due to which the instance of the main component was not deleted 
      after calling the "Clear All Data" option, which led to many errors.

  Improvements:
    - Added support for callbacks during blending.
    - Now the plugin can work in the lightmap switching only mode without the need for a shader patch.

  v 0.93.1b
  -------------

  Bug Fixes:   
    - Fixed a bug in the shader code that led to the overexposure of the scene in HDRP.
    - Fixed a bug that occurred when duplicating objects and repeated baking.

  Improvements:
    - Enviro support.

  v 0.93b
  -------------

  Bug Fixes:   
    - Dynamic and static component on the same object caused "Null Reference Exeption" error
    - Deleted Custom Blendable objects cause errors in the preset manager
    - Terrain was incorrectly accounted for by the system
    - Fog settings are overwritten for all presets
    - Fixed other bugs in the preset manager
    - Fixed a bug when working with a few lighting scenarios
    - Missing light probes in the scene causes an error in UpdateOperationalData
    - An error occurred while rewriting a scenario asset if the previous option did not contain Reflection Probes
    - Fixed bug when working with meshes with multiple materials

  Improvements:
    - New Stable Shader Patch System for SRP

  v 0.92b
  -------------

  Bug Fixes:
    - Preset parameters break if baking starts with the preset manager window opened.
    - Rotation values ​​for light sources are sometimes incorrectly assigned.
    - The SRP patch does not execute correctly.
    - Fixed bugs in test scenes.

  Improvements:
    - Access to settings and presets is now locked during baking.
    - New versions of the plugin can be downloaded immediately after release through the "About MLS..." window.

  v 0.91b
  -------------

  Bug Fixes:
    - Fixed a rare bug that caused the lightmap data asset to lose all settings.
    - Fixed a bug due to which objects were sometimes assigned different UIDs when changing 
      presets, resulting in an error when mixing.
    - Fixed a bug that occurred if the Terrain was not marked as static.
    - Fixed bugs in test scenes.

  Improvements:
    - The ability to save transforms of game objects to presets and, as a result, 
      synchronize them with the Global Blend value.
    - Blending of the tint color for the skybox shader is now also supported.
  
  v 0.9b
  -------------

  First release.

  Contacts
  -------------

  e-mail: evb45@bk.ru
  telegram: https://t.me/EVB45
  forum: https://forum.unity.com/threads/magic-lightmap-switcher-storing-switching-and-blending-lightmaps-in-real-time.966461/
  discord channel: https://discord.gg/p94azzE