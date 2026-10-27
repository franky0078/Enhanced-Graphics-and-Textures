Enhanced Graphics and Textures adds extended graphics controls and an in-game surface texture editor

**Graphics controls**

- Tree LOD distance and global object detail scale
- Geometry cache size
- Terrain tessellation level and patch size
- Shadow resolution and distance
- Small shadow caster threshold
- TAA sharpening strength
- Mipmap bias for all surfaces
- 16× anisotropic texture filtering for modified surface textures

Higher settings can increase GPU load and memory use. Adjust one setting at a time and compare both image quality and frame rate.


**Texture editor**

Adjust these surfaces independently:

- Road asphalt
- Sidewalks
- Independently placed paved footpaths
- Alley / one-way road sidewalks
- Sidewalk grass
- Gravel roads
- Gravel sidewalks / road edges
- Railway ballast

The editor provides original, generated and custom texture modes, with surface-dependent controls for texture size, depth, offset and rotation.
Asphalt and sidewalk controls also include procedural cracks with adjustable count, size, width, opacity and randomness.
Depth controls change the material's normal-map appearance.
Parking spaces use the selected road asphalt texture, while outer sidewalks use the sidewalk texture settings.
Color Filter controls for all surfaces.
Adjustable sidewalk texture tiling at road ends and junctions.


**How to use**

1. Enable the mod and load a city.
2. Open Options and select Enhanced Graphics and Textures to adjust graphics settings and surface options.
3. Click the in-game texture editor button to open the surface panel. Its tooltip identifies the editor.
4. Select the surface tab and enable the corresponding surface override.
5. Select the texture mode: 0 = Original, 1 = Generated, 2 = Custom.
6. Adjust texture size, depth, offset and rotation. Use the crack controls where available.
7. Parking surface factor in the road tab adjusts the asphalt scale on parking spaces without changing the carriageway or sidewalk scale.
8. Use the reset controls to return the corresponding surface or graphics settings to their defaults.

Launcher visibility for the surface panels can be configured in the mod options.


**Custom textures**

Custom textures are optional and must be provided manually by the user. The mod creates empty texture folders at startup.
It does not automatically copy PNGs from previous installations or create sample custom PNG files.

Use the folder-opening controls in the mod options to find the correct directory. The texture root is:

%USERPROFILE%/AppData/LocalLow/Colossal Order/Cities Skylines II/ModsData/Enhanced_Graphics_and_Textures/Textures

Use these exact filenames:

- Road asphalt: Road/Road_BaseColor.png and Road/Road_Normal.png
- Sidewalk: Sidewalk/Sidewalk_BaseColor.png and Sidewalk/Sidewalk_Normal.png
- Footpath: Footpath/Footpath_BaseColor.png and Footpath/Footpath_Normal.png
- Alley / one-way sidewalk: AlleySidewalk/AlleySidewalk_BaseColor.png and AlleySidewalk/AlleySidewalk_Normal.png
- Sidewalk grass: SidewalkGrass/Grass_BaseColor.png and SidewalkGrass/Grass_Normal.png
- Gravel road: Gravel/RoadGravel_BaseColor.png and Gravel/RoadGravel_Normal.png
- Gravel sidewalk / edge: GravelSidewalk/GravelShoulder_BaseColor.png and GravelSidewalk/GravelShoulder_Normal.png
- Railway ballast: Rail/RailGravel_BaseColor.png and Rail/RailGravel_Normal.png

1. Prepare a square, seamlessly tiling PNG, at most 4096 × 4096 pixels and no larger than 32 MiB.
2. Place it in the corresponding subfolder using the exact filename above.
3. Select Custom mode and use the surface's reload control.
4. Enable the surface override and adjust its appearance.

Only use textures you own or have permission to use.
Third-party custom texture files are not included in this update.


**Optional normal maps**

Use unpacked RGB tangent-space normal maps in OpenGL / +Y format. They must be square PNG files, at most 4096 × 4096 pixels and no larger than 32 MiB.
Place the normal map beside its matching color texture. It should represent the same pattern and alignment.
Enable Use custom normal map for the corresponding surface tab. This option is off by default. When disabled, the mod uses its existing automatic relief behavior.
Color textures and custom normal maps follow the same texture transformations. Surface depth controls the strength of the relief.


**Language support**

English, German, Spanish, French and Italian are included. The interface follows the game's language setting.


**Compatibility and limitations**

No additional mod dependency or DLC requirement is declared by this project. Other mods that change the same graphics settings or surface materials may override these changes. Avoid applying competing overrides to the same setting or material.

The mod modifies runtime rendering and materials. Keep the game's own graphics options in mind when comparing results.

Changing textures or adjusting texture settings may cause brief stutters while the mod rebuilds the textures and updates all affected surfaces.


**Transparency note**

- Some parts of the code are built with AI assistance.
- Some UI elements are built with AI assistance, which is also used for debugging and troubleshooting.


**Feedback and bug reports**

Please include:

- The affected road or surface type
- Original, Generated or Custom mode and the relevant slider values
- Steps to reproduce the issue