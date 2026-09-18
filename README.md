<div>

# Distributed Spinning Cubes Demo - Unity P2P Plugin Tutorial

<div id="hide_on_load">

## Interactive README

<b><a href="https://blainebell.org/P2PSpinningCubesDemo/README.html" target="_blank">An interactive version of this page</a></b> is available online.  Code blocks in the implementation section are interactive and show the related code from the psuedo-code in a split screen on the left.
</div>

## Overview

The Spinning Cubes Demo is a simple yet comprehensive example that
demonstrates the core functionality of the <a href="https://blainebell.org/P2PPlugin/README.html" target="_blank">Unity Peer-to-Peer Networking Plugin</a>. This interactive demo showcases distributed object creation, deletion, synchronization, and real-time manipulation across multiple connected devices.

In this demo, each connected player can:

- Create spinning cube objects that appear on all devices
- Delete cubes in the scene that were created by that same player
- Move cubes around by clicking and dragging
- See color-coded cubes indicating which player created each object
- Experience real-time synchronization across all connected devices

This tutorial will guide you through understanding and implementing the demo, serving as a foundation for building more complex peer-to-peer multiplayer experiences.

### Demo Features

#### Core Functionality

- **Distributed Object Creation**: Create cubes that instantly appear on all connected devices
- **Player Color Coding**: Each player has a unique color, and their created cubes reflect this color
- **Interactive Manipulation**: Click and drag any cube to move it around in 3D space
- **Real-time Synchronization**: All movements and changes are synchronized across devices instantly
- **Cross-platform Support**: Works seamlessly across desktop, mobile, and XR platforms

#### Visual Elements

- **Spinning Animation**: All cubes continuously rotate
- **Player Identification**: Each player's session has a colored outline related to the cube colors for easy identification
- **Smooth Movement**: Dragging provides immediate visual feedback across all devices
- **Clean UI**: Simple user clicking for creating, deleting, and moving objects

---

## Prerequisites

Before starting this demo, ensure you have:

1. **Unity P2P Plugin Installed**: Follow the Getting Started guide in the main documentation
2. **Development Environment**: Either Multiplayer Play Mode or multiple Unity instances/builds
3. **Basic Unity Knowledge**: Familiarity with GameObjects, components, and basic scripting

To use this demo project, you can download or clone it, load it into Unity (make sure you ignore the errors) before you install the plugin from the Package Manager.

## Project Setup

### 1. Scene Preparation

The demo project has a very simple setup:

```
SpinningCubesDemo (Empty GameObject)
├── Directional Light
└── Main Camera
    └── screenCanvasParent
         └── screenCanvas
              └── OutlineShowsColor
```

### 2. Main Scripts

The demo consists of three main scripts:

1. <a href="Assets/DistributedObjects/SharedCube.cs" id="linktop2pdemo" class="viewer-link language-csharp">**`DistributedObjects/SharedCube.cs`**</a> - Defines the distributed cube object
2. <a href="Assets/Scripts/P2PSharedCubeInteractionHandler.cs" id="linktop2pdemo" class="viewer-link language-csharp">**`Scripts/P2PSharedCubeInteractionHandler.cs`**</a> - Implements the user interactions that add, delete, and move cubes
3. <a href="Assets/Scripts/AssignSharedCubeColorsTo.cs" id="linktop2pdemo" class="viewer-link language-csharp">**`Scripts/AssignSharedCubeColorsTo.cs`**</a> - Responsible for assigning the color of the player and setting the cube colors

#### Script Setup

Scripts should be setup and added as components to the scene:

1. **P2P Plugin** - The main P2P plugin script needs to be added somewhere in the scene, such as on the **Main Camera**.  This node should be configured appropriately, as the <a href="https://blainebell.org/P2PPlugin/README.html?scrollToHighlight=pluginconfig" target="_blank" rel="noreferrer noopener">documentation</a> suggests, with the multicast parameters.
2. **P2P Shared Cube Interaction Handler** - Should be added somewhere globally, such as the **Main Camera**.
3. **Assign Shared Cube Colors To** - Add globally, such as on the **Main Camera**.
4. **Screen Canvas Script** - Added to **screenCanvas**, sets both transforms for **screenCanvasParent** and **screenCanvas** for screen stabilized coordinate systems, including the **screenCanvas** which is in pixel scale. This script should be configured with the Main Camera.

The **P2P Shared Cube Interaction Handler** should also be configured appropriately:

<div style="width: 30%; margin: auto;">
<center>

![P2P Shared Cube Interaction Handler Configuration](images/P2PSharedCubeInteractionHandlerConfig.png "P2P Shared Cube Interaction Handler Configuration")

</center>
</div>

- **Main Camera** - used for screen to world computations to determine creation, deletion, updates from interaction positions
- **Prefab to Spawn** - For remote SharedCubes, this prefab SmallRotatedCube is created
- **Outline for Color** -  The OutlineShowsColor is used to show the outline around the outside of the view, which is colored by the local player's color


---

### 3. Implementation

This demo is mainly driven by one script <b><a href="Assets/Scripts/P2PSharedCubeInteractionHandler.cs" id="linktop2pdemo" class="viewer-link language-csharp">P2PSharedCubeInteractionHandler.cs</a></b> that implements the user interaction and distribution.  Pseudo-code for this script implements the three main mouse/touch events:

<style>


mark {
  display: block;
  margin: 0;
  padding: 0;
 background-color: white !important;
}

</style>

<div id="presection" style="width: fit-content;">
<mark style="margin: 0;" data-id="onPress">
<pre class="onPress hl" href="Assets/Scripts/P2PSharedCubeInteractionHandler.cs" highlight="OnPress"><code class="language-csharp">public void OnPress(Vector2 mouseTouchPos) {
    if User Presses a cube:
        Set dragging cube to pressed cube
}
</code></pre></mark><mark style="margin: 0;" data-id="onRelease"><pre class="onRelease hl" href="Assets/Scripts/P2PSharedCubeInteractionHandler.cs" highlight="OnRelease"><code class="language-csharp">public void OnRelease(Vector2 mouseTouchPos){
    if no dragging cube:
        Create new cube at mouse/touch position
    else if dragging cube is set and User did not move it:
        Delete dragging cube
}
</code></pre></mark><mark style="margin: 0;" data-id="onMove"><pre class="onMove hl" href="Assets/Scripts/P2PSharedCubeInteractionHandler.cs" highlight="OnMove"><code class="language-csharp">public void OnMove(Vector2 mouseTouchPos){
    if dragging cube is set:
        move dragging cube to mouseTouchPos
}
</code></pre>
</div>

This class is a subclass of <a href="Assets/Scripts/MouseAndTouchMonoBehaviour.cs" id="linktop2pdemo" class="viewer-link language-csharp">MouseAndTouchMonoBehaviour.cs</a> to support both desktop and mobile platforms.

Each cube is distributed to all other remote devices:

<table style="border: none;width:90%; margin: auto;">
<tr style="border: none;">
<td style="border: none;">

![Cubes are distributed to all other instances](images/SimpleP2POneRow.png "Cubes are distributed to all other instances")</td><td style="border: none;">

![Cubes are distributed to all other instances](images/SimpleP2PAll.png "Cubes are distributed to all other instances")

</td>
</tr>
<tr style="border: none; text-align: center;">
<td style="border: none;">Cubes are distributed to all other players</td>
<td style="border: none;">All players have local and remote cubes</td>
</tr>
</table>

<p></p>

Inserts, deletes and updates are replicated using a **one-way distribution mechanism**.  For this demo, we limit these actions to the source player who created the shape.


#### Defining the Distributed SharedCube

The distributed component of this demo is defined in the <a href="Assets/DistributedObjects/SharedCube.cs" id="linktop2pdemo" class="viewer-link language-csharp">**SharedCube**</a> class with 4 main sections:


<div id="presection2" style="width: fit-content;">
<mark style="margin: 0;" data-id="begClassDef">
<pre class=""><code class="language-csharp">public class SharedCube : P2PNetworkComponent {
</code></pre>
<mark style="margin: 0;" data-id="fieldsandprops">
<pre class="hl" href="Assets/DistributedObjects/SharedCube.cs" highlight="range-8-28"><code class="language-csharp"><div class="hljs-comment"><b>  /* 1. Define fields/properties */</b></div>
  public Vector3 translation;
</code></pre></mark><mark style="margin: 0;" data-id="triggerfuncs"><pre class="onRelease hl" href="Assets/DistributedObjects/SharedCube.cs" highlight="range-30-54"><code class="language-csharp"><div class="hljs-comment"><b>  /* 2. Add Trigger Functions for Remote */
  /*     Insert/Delete of SharedCube     */</b></div>
  public void AfterInsertRemote() {
    Initialize GameObject and populate data structures
  }
  public void AfterDeleteRemote() {
    Remove from data structures and delete GameObject
  }
  static public GameObject spawnNewRemoteObject() {
    Create new GameObject from prefab
    with SharedCube component and return
  }</code></pre></mark><mark style="margin: 0;" data-id="dsandhelpers"><pre class="onMove hl" href="Assets/DistributedObjects/SharedCube.cs" highlight="range-55-78"><code class="language-csharp">  <div class="hljs-comment"><b>&sol;&ast; 3. Static data structures &ast;&sol;</b></div>
  <div class="hljs-comment"><b>/&ast;   and helper functions to setAssignedColor &ast;/</b></div>
  allSharedCubes = new Dictionary&lt;long, SharedCube&gt;();
  assignedColors = new Dictionary&lt;long, Color&gt;();
  void setColorToRenderer(Renderer rend, long peerID) {
    Lookup and set assigned color to renderer
  }
  void setColorToCube(SharedCube sc) {
    Lookup and set assigned color of
	  source computer to SharedCube
  }
  void setColorToGameObject(GameObject go,
                            long peerID) {
    Lookup and set assigned color of
      source computer to GameObject
  }
</code></pre></mark><mark style="margin: 0;" data-id="dsandhelpers"><pre class="onMove hl" href="Assets/DistributedObjects/SharedCube.cs" highlight="range-80-83"><code class="language-csharp">  <div class="hljs-comment"><b>&sol;&ast; 4. Need public constructor for remote instantiation &ast;&sol;</b></div>
  public SharedCube()
  {
  }
</code></pre>

</div>

<pre class="hln"><code class="language-csharp">}
</code></pre>

This SharedCube class enables the Peer-to-Peer distribution and hooks the data into the Unity GameObjects for smooth distributed multi-player interactive functionality.

#### Supporting Scripts

There are some scripts that support the demo but do not have demo-specific logic:

1. <a href="Assets/Scripts/MouseAndTouchMonoBehaviour.cs" id="linktop2pdemo" class="viewer-link language-csharp"> **`Scripts/MouseAndTouchMonoBehaviour.cs`**</a> - A super class of P2PSharedCubeInteractionHandler.cs and allows support of mobile and desktop interaction using the same callbacks.
2. <a href="Assets/Scripts/RotateCube.cs" id="linktop2pdemo" class="viewer-link language-csharp"> **`Scripts/RotateCube.cs`**</a> - A very simple (1-liner) script that rotates cubes on an axis.
3. <a href="Assets/Scripts/ScreenCanvasScript.cs" id="linktop2pdemo" class="viewer-link language-csharp"> **`Scripts/ScreenCanvasScript.cs`**</a> - A script that sets the transforms of both screenCanvas and screenCanvasParent to support screen stabilized coordinate system, which is used for the OutlineShowsColor GameObject.
4. <a href="Assets/Scripts/ScreenOutline.cs" id="linktop2pdemo" class="viewer-link language-csharp"> **`Scripts/ScreenOutline.cs`**</a> - A script that generates the geometry and sets it to the Mesh of the GameObject.


## Building and Exporting to Supported Platforms

The **P2P Spinning Cubes Demo** can be built for desktop, mobile, and XR platforms. The P2P Plugin itself does not require any platform-specific dependencies, but some platforms require additional Unity packages or project configuration.

Before building for a platform, make sure the appropriate scene is the **only scene included in the Build scene list**.

### Desktop — macOS / Windows / Linux

Desktop builds do not require any additional packages or platform-specific configuration.

1. Select the desired desktop **Build Profile**.
2. Make sure **P2PSpinningCubesDemo** is the only scene included in the Build scene list.
3. Build the application normally.

### Mobile: Android or IOS

The standard Android or iOS build does not require additional packages.

1. Switch the **Build Profile** to **Android** or **iOS**.
2. Make sure **P2PSpinningCubesDemo** is the only scene included in the Build scene list.
3. Build the project.

Unity will export the project in the format appropriate for the
selected platform. For iOS, Unity exports an Xcode project that can be
built and deployed to an iPhone or iPad using Xcode. For Android,
Unity can either export an Android project or build an APK that can be
installed directly on an Android device. Exported Android projects can
be opened, built, and run using Android Studio.

### Meta Quest / Android XR

Meta Quest uses the XR version of the demo scene and requires Unity XR support and the Meta XR SDK.

1. Switch the **Build Profile** to **Android Meta Quest**.
2. Open **Edit → Project Settings → XR Plug-in Management**.
3. Install **XR Plug-in Management** if it is not already
   installed. (reboot to see the configurations in the Project Settings)
4. Check **Initialize XR on Startup** and enable **OpenXR** as the Plug-in Provider.
5. Install the <b><a
   href="https://assetstore.unity.com/packages/p/meta-xr-sdk-9022845" target="_blank">**Meta XR All-in-One SDK**</a></b>
6. Change the active scene to **MetaP2PSpinningCubesDemo**.
7. Make sure **MetaP2PSpinningCubesDemo** is the only scene included in the build Scene List.
8. Use Android Studio to build and run the project on the headset.

### Apple Vision Pro

Apple Vision Pro uses the visionOS version of the project and requires Apple's Unity XR and PolySpatial packages.

1. Switch the **Build Profile** to **visionOS**.
3. Open:
   **Edit → Project Settings → XR Plug-in Management → visionOS settings**
4. Make sure **Initialize XR on Startup** is checked and **Apple visionOS** is enabled as a Plug-in Provider.
2. Open: **Window → Package Manager**, install:
   - **Apple visionOS XR Plugin**
   - **PolySpatial visionOS**
5. Select the **VisionProP2PSpinningCubesDemo** scene in the workspace, and make sure it
   is the only scene included in the build Scene List, which is on the top of the **Build Profiles** dialog.
6. Build the project.

Unity will export an Xcode project that can then be built and deployed to Apple Vision Pro using Xcode.

**Note**: Because the plugin uses multicast networking for peer
  discovery, the project must be signed with Apple's Multicast
  Networking Entitlement. This entitlement may need to be requested
  from Apple before the application can use multicast networking on
  the device.

### Switching Between Platforms

When switching between the standard desktop/mobile demo and an XR platform, remember to verify both the **Build Profile** and the **scene included in the build**.

| Platform | Build Profile / Target | Demo Scene | Additional Packages |
|---|---|---|---|
| macOS / Windows / Linux | Desktop | `P2PSpinningCubesDemo` | None |
| Android | Android™ | `P2PSpinningCubesDemo` | None |
| iOS | iOS | `P2PSpinningCubesDemo` | None |
| Meta Quest / Android XR | Android Meta Quest | `MetaP2PSpinningCubesDemo` | XR Plug-in Management, OpenXR, Meta XR All-in-One SDK |
| Apple Vision Pro | visionOS | `VisionProP2PSpinningCubesDemo` | Apple visionOS XR Plugin, PolySpatial visionOS |

The same P2P networking code is used across these platforms; the XR-specific scenes primarily provide the platform-appropriate camera, input, and interaction setup.

</div>
