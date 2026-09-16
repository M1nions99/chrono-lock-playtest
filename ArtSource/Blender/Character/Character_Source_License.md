# 주인공 인체 원본 출처

- MPFB2 source: https://github.com/makehumancommunity/mpfb2
- Pinned commit: `437dd513888a92399d1d3200d2e80859fae55abc`; source version 2.0.17.
- Application code: GPLv3. Bundled character base mesh, modeling targets, rigs and other graphical assets: CC0, as specified in the pinned repository's LICENSE.md.
- Eyes, brows, short hair, teeth and skin: official MakeHuman system assets CC0 pack, https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html . Each chosen asset is listed there as CC0.

This project keeps the original MPFB source body and helper topology hidden for future fitting. The visible body is a separate helper-free copy, using the same GameEngine humanoid bone topology. No standalone procedural hand geometry is substituted.

MPFB was loaded from the pinned project-local source through Blender MCP. During registration only, its extension-user-directory lookup was directed to `work/character-tools/mpfb-user`; Blender's API function was immediately restored. No global preferences file was deliberately saved. MPFB's own initialization also writes its standard log configuration under the Blender user directory.
