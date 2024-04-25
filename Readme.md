### I dont own the game on steam, so feel free to release it on workshop under your name if you like.



# RimWorld ResearchPal - Forkd

[![Version](https://img.shields.io/badge/Rimworld-1.2-green.svg)](http://rimworldgame.com/)
[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-blue.svg)](http://creativecommons.org/licenses/by-nc-sa/4.0/)

Smooth painless research

# Features

## ResearchPal

- automatically generated to (hopefully) maximize readability. 
- shows research projects, buildings, plants and recipes unlocked by each research project.
- projects can be queued, and colonists will automatically start the next project when the current research project completes.
- search functionality to quickly find research projects.

### Settings

- **Group Research by Tech-Level**: Whether or not explicitly separate techs by their tech-levels (neolithic, medival, industrial etc.) (Will result in a MUCH larger and sparser graph, and persumably slower generation). Default is `false`.

## ResearchPal - Forkd

This mod complete rewrites the ResearchPal's graph layout algorithm. The new algorithm is mainly based on the [Sugiyama's original work](https://ieeexplore.ieee.org/abstract/document/4308636) with some personal tweaks. Since Fluffy said:

> The final step is the hardest, but also the most important to create a visually pleasing tree. Sadly, I've been unable to implement two of the most well known algorithms for this purpose ... *[referring to two work of Sugiyama's algorithm]*

I thought the resulting layout would be much more pleasant than the original with the magical algorithm, but unfortunately it is not true, which I realized after I implemented the core algorithm. So I added a few features with a few other planned. The forked version:

- Eliminates most of the strange behaviors of original ResearchPal. (e.g. Some arrows make U-turns when there's literally nothing blocking their ways, arrows sometimes go through other nodes etc.). And hopefully it's better-looking in general.
- Guarantees that separate trees are placed separately, instead of relying on heuristics.
- Guarantees that techs of different mods could be placed together (See below).

### Settings

- **Align Nodes Closer to Prerequites**: The heuristic will place nodes closer to their prerequisites instead of children (This affects the layout heuristic which does not guarantee anything, could result in some drastic change). Default is `false`.
- **Group Techs from the Same Mod**: Put techs from mods separately from the vanilla techs. Currently group all vanilla expanded mods together. Default is `true`.
    + Currently this mod groups all vanilla expanded series techs (based on the name of the mod "Vanilla XXX Expanded - YYY) together if this feature's turned on. It is only a temporary solution for mod grouping.
- **Minimum Separate Mod Techs Count**: With the option above enabled, it determines the minimal amount of the techs of a certain mod for it to be placed separately from vanilla tech tree (so that mods adding very few techs will still be placed along with the main tree). Default is `5` (So a mod with 5 techs or more will be placed separately).

### Planned Features

- Configurable modded techs grouping. ("I want EPOE and A-dog-said placed together!" etc.)
- Not limiting the position of techs to be integer coordinate inside the grid.
- Less-messy and more-readable arrows (Not exactly sure how to do that though).
- Legacy code cleanup.
 
# FAQ

### *Can I add/remove this from an existing save?*

You can add it to existing saves without problems. Removing this mod will lead to some errors when loading, but these should not affect gameplay - and will go away after saving.

### *Why is research X in position Y?*

Mostly it's determined by a heuristic-based algorithm, which means that in general we try our best to guess the better position of a tech to be at, but not actually know why they are eventually there. Details see below "Technical Details".

### *Can I use this with mod X*

Should be incompatible with original ResearchPal and Research Tree, obviously, but I don't actually know.

Other than that, should be compatible with everything the original mod is compatible with.

### *This looks very similar to ResearchTree and ResearchPal*

There was first Fluffy's research tree, then NotFood and Skyarkangel's ResearchPal is
a fork of it, and this mod is a fork of the latter. We're all basically using the same UI framework (supposely created by Fluffy) so of course.

# Technical Details

Let's start with a quote of Fluffy:

> Why is research X in position Y? Honestly, I have no idea. 

The core algorithm is based on Sugiyama's original algorithm (as I simply don't understand the optimized version) which:

1. Separates nodes in a graph to layers.
2. Applies heuristics trying to minimize the total amount of arrow (edge) crossings between layers and determines the order of nodes in layers.
3. With the order of nodes fixed, applies heuristics to place nodes in each layer near their (ancestors or children).

You may not know what I'm talking about, but the core algorithm has two important implications:

- **It does NOT guarantee the absolute minimization of crossings (even worse, it doesn't say at all how far away from the optimal the result would be). Nodes _could_ still be placed at obviously-suboptimal positions**
- **It says (almost) nothing about the total length of edges. So some edges may travel a bit of detour in order to get the destination.**

It alone actually performs worse than the originally implemented algorithm in general, so I added an simple additional step after step 2 to further
tune down the total edge length and number of crossings, but I still don't believe the result is anywhere near optimal.
And after step 3, I added an additional step to adjust the position of some nodes to strictly better positions without compromising the core idea of the algorithm.

[github repository of this mod](https://github.com/VinaLx/RimWorld-ResearchPal). Data structure and algorithm mostly under `Graph/NodeLayers.cs`.

Please feel free to make _technical_ suggestions to the algorithm if you have any idea how to improve it.

# About Me

I'm a computer science student who have almost no idea of rimworld modding and hardcore c# programming. So should there be bugs or compatibility issues besides
the layout algorithm, I will try my best to resolve them but I can't really promise anything.

# License

All original code in this mod is licensed under the [MIT license](https://opensource.org/licenses/MIT). Do what you want, but give me credit. 
All original content (e.g. text, imagery, sounds) in this mod is licensed under the [CC-BY-SA 4.0 license](http://creativecommons.org/licenses/by-sa/4.0/).

Parts of the code in this mod, and some content may be licensed by their original authors. If this is the case, the original author & license will either be given in the source code, or be in a LICENSE file next to the content. Please do not decompile my mods, but use the original source code available on [GitHub](https://github.com/FluffierThanThou/ResearchTree/), so license information in the source code is preserved.

## Credits:

Thanks Fluffy, NotFood and Skyarkangel for this awesome research panel UI framework.
Thanks Fluffy for mentioning the Sugiyama's algorithm for me to learn.
