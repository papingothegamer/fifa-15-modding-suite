# FIFA 15 Modding Suite

A modern, comprehensive modding toolsuite for FIFA 15 (Windows), designed to streamline modifications for the Ignite Engine.

## Architecture

This toolsuite utilizes a hybrid architecture:

*   **C# (.NET 10.0 WPF):** The core manager application. Handles `.big` unpacking, `.bh` regeneration, database parsing, and the user interface.
    *   **FIFA15.Core**: Class library for reading and manipulating proprietary EA binary formats and databases.
    *   **FIFA15.Manager**: The primary WPF frontend containing the Competition Editor, Accessory Manager, and Scoreboard creator.
*   **Python:** Dedicated scripts for handling `.rx3` graphical assets, specifically tailored for importing/exporting textures and 3D meshes to and from Blender.
    *   **FIFA15.PyTools**: Standalone scripts that can be integrated directly into Blender.

## Key Features

- **Stadium Converter:** Convert stadium meshes from FIFA 14/16 formats.
- **Accessory Manager:** Dynamically assign custom boots and gloves, including batch randomization and automated assignments.
- *[PURGATORY]* **Competition Manager:** Easily modify rosters and tournament structures (`compdata`). Moved to `/purgatory/` due to hardcoded executable constraints.
- *[PURGATORY]* **Scoreboard Creator:** Generate custom generic scoreboards and popup sets using templates. Moved to `/purgatory/`.

## Initialization
Open `FIFA15.ModdingSuite.sln` in Visual Studio or your preferred IDE to compile the C# components. 
Python scripts are located in `src/FIFA15.PyTools`.
