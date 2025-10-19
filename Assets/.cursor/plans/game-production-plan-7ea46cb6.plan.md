<!-- 7ea46cb6-d292-4a0e-81ab-62bfc39e86b2 6a431e08-afa8-4107-aa8a-e5d07c15a3f1 -->
# 16-Bit ProcGen Game Production Plan

## Current State Assessment

### ✅ Completed Foundation Systems

- **DOTS Core Infrastructure**: Entity management, component systems, blob assets
- **Terrain Generation**: Compute shader-based noise generation, biome system, WFC dungeon generation
- **Terrain Destruction**: Glob removal system with physics for terrain chunks
- **Weather System**: Dynamic environmental effects (rain, sandstorms)
- **Basic Player Controller**: Standard FPS movement (needs replacement with magic hand mechanics)

### 🔨 Prototype/Incomplete Systems

- **WFC Dungeons**: Working but using placeholder prefabs (needs proper art integration)
- **Player Movement**: Basic FPS controls (needs slingshot mechanic)
- **Input System**: Defined but not fully integrated with gameplay

### ❌ Missing Core Systems

- **Magic Hand System**: Core interaction mechanic (destroy, manipulate, cast)
- **Inventory & Resource System**: Collection and storage of terrain globs/resources
- **Crafting System**: Combining resources to create items/tools
- **World Persistence**: Save/load system for modified terrain
- **Procedural Structures**: Points of interest, ruins, caves beyond dungeons
- **Progression System**: Unlocks, upgrades, abilities
- **Player Character Model**: Visual representation with hand animations
- **UI System**: HUD, inventory screen, crafting interface
- **Audio System**: SFX and ambient audio

---

## Game Design Pillars

1. **Exploration**: Rewarding discovery in a vast procedural world with varied biomes
2. **Destruction**: Satisfying terrain manipulation with the magic hand
3. **Movement**: Fun, skill-based slingshot traversal system
4. **Crafting**: Meaningful resource gathering and item creation
5. **Minimalism**: Low-poly aesthetic with clear visual communication

---

## Production Phases

### Phase 1: Core Player Experience (Priority: CRITICAL)

**Goal**: Establish the feel of the game - movement, destruction, and basic interaction loop

#### 1.1 Magic Hand System

- **Components**:
- `MagicHandComponent` (DOTS): Hand state, charge level, mode (destroy/grip/cast)
- `MagicHandAuthoring`: Configuration for hand properties
- `MagicHandInputSystem`: Capture input for hand actions
- `MagicHandVisualizationSystem`: Visual feedback (hand glow, charge indicator)
- **Features**:
- Raycast-based targeting system
- Charge-up mechanic for destruction power
- Visual indicator showing target area and charge level
- Integration with existing `TerrainModificationSystem`
- **Files**: `Scripts/Player/MagicHand/`, integrate with `Scripts/DOTS/Modification/`

#### 1.2 Slingshot Movement System

- **Components**:
- `SlingshotMovementComponent`: Grip state, pull distance, trajectory calculation
- `SlingshotInputSystem`: Detect grip + pull-back gesture
- `SlingshotTrajectorySystem`: Calculate and preview launch trajectory
- `SlingshotLaunchSystem`: Apply physics force on release
- **Features**:
- Hold button to "grip" (fist animation)
- Pull back to charge (visual rubber band effect)
- Release to launch player in trajectory arc
- Cooldown system to prevent spam
- Air control during flight
- Landing impact effects
- **Integration**: Replace `PlayerController.cs` with DOTS-based system
- **Files**: `Scripts/Player/Movement/`

#### 1.3 Resource Collection System

- **Components**:
- `ResourceComponent`: Resource type, quantity, properties
- `InventoryComponent`: Player's resource storage
- `ResourceCollectionSystem`: Detect and collect terrain globs
- `ResourceVisualizationSystem`: Show collectible resources
- **Features**:
- Automatic collection when player touches terrain globs
- Visual feedback (particle effect, sound)
- Resource types match terrain types (stone, dirt, sand, etc.)
- Stack limits and inventory capacity
- **Integration**: Extend existing `TerrainGlobComponent` with collection logic
- **Files**: `Scripts/Resources/`, integrate with `Scripts/DOTS/Modification/TerrainGlobPhysicsSystem.cs`

#### 1.4 Basic HUD

- **UI Elements**:
- Resource counters (top-right corner)
- Hand charge indicator (crosshair area)
- Health/energy bar (if applicable)
- Slingshot charge indicator
- **Implementation**: Unity UI Toolkit or TextMeshPro
- **Files**: `Scripts/UI/HUD/`

**Deliverables**:

- Player can slingshot through the world with satisfying physics
- Player can destroy terrain with magic hand and collect resources
- Basic resource inventory displayed on HUD
- Core game loop feels fun and responsive

---

### Phase 2: World Generation & Biomes (Priority: HIGH)

**Goal**: Create a rich, varied world worth exploring

#### 2.1 Enhanced Biome System

- **Biomes to Implement**:
- Grasslands (starting area, gentle terrain)
- Desert (sand dunes, sparse resources)
- Mountains (rocky, vertical challenges)
- Forest (dense vegetation, hidden areas)
- Snow/Tundra (harsh environment, rare resources)
- Corrupted/Magical (late-game, unique materials)
- **Features**:
- Smooth biome transitions with blending
- Biome-specific terrain types and colors
- Biome-specific resource distribution
- Biome-specific weather patterns
- **Integration**: Extend existing `BiomeComponent` and `TerrainGenerationSystem`
- **Files**: `Scripts/DOTS/Generation/BiomeSystem/`

#### 2.2 Procedural Structures & POIs

- **Structure Types**:
- Ruins (WFC-generated, loot containers)
- Caves (underground networks, rare resources)
- Towers (vertical challenges, vantage points)
- Shrines (ability unlocks, lore)
- Resource nodes (concentrated material deposits)
- **Implementation**:
- Extend WFC system for structure generation
- Structure placement based on biome and terrain
- Interior generation for explorable structures
- **Files**: `Scripts/DOTS/Structures/`

#### 2.3 World Streaming & LOD

- **Features**:
- Chunk-based world streaming around player
- LOD system for distant terrain
- Unload distant chunks to manage memory
- Seamless chunk loading/unloading
- **Integration**: Extend existing `TerrainSystem` with streaming logic
- **Files**: `Scripts/DOTS/Core/WorldStreaming/`

**Deliverables**:

- Multiple distinct biomes with smooth transitions
- Interesting structures and POIs scattered throughout world
- Performant world streaming system
- World feels large and worth exploring

---

### Phase 3: Crafting & Progression (Priority: HIGH)

**Goal**: Give meaning to resource collection and provide progression hooks

#### 3.1 Crafting System

- **Components**:
- `CraftingRecipeData`: Recipe definitions (inputs → outputs)
- `CraftingStationComponent`: Crafting interface entity
- `CraftingSystem`: Process crafting requests
- **Features**:
- Recipe discovery system (find recipes in world)
- Crafting categories (tools, upgrades, consumables, building)
- Resource requirements and crafting time
- Crafting UI (recipe list, resource availability)
- **Items to Craft**:
- **Tools**: Enhanced destruction tools, terrain shaping tools
- **Upgrades**: Hand power upgrades, movement upgrades
- **Consumables**: Health/energy items, temporary buffs
- **Building**: Placeable structures, decorative items
- **Files**: `Scripts/Crafting/`

#### 3.2 Item & Tool System

- **Components**:
- `ItemComponent`: Item type, properties, durability
- `ToolComponent`: Tool-specific properties (power, range, speed)
- `EquipmentSystem`: Manage equipped tools
- **Tool Types**:
- Basic Hand (default, low power)
- Drill Hand (fast, precise destruction)
- Explosive Hand (large area, high power)
- Shaping Hand (terrain sculpting, not just destruction)
- Grapple Hand (enhanced slingshot, attach to surfaces)
- **Files**: `Scripts/Items/`

#### 3.3 Progression System

- **Progression Mechanics**:
- Ability unlocks (new hand modes, movement options)
- Stat upgrades (inventory size, hand power, movement speed)
- Recipe unlocks (discover new crafting recipes)
- Biome access (certain biomes require upgrades to survive)
- **Implementation**:
- `ProgressionComponent`: Track unlocks and upgrades
- `ProgressionSystem`: Handle unlock logic
- Save/load progression state
- **Files**: `Scripts/Progression/`

**Deliverables**:

- Functional crafting system with meaningful recipes
- Multiple tools/upgrades that change gameplay
- Clear progression path that guides exploration
- Sense of growth and increasing power

---

### Phase 4: World Persistence & Polish (Priority: MEDIUM)

**Goal**: Make player actions meaningful and persistent

#### 4.1 Save/Load System

- **Features**:
- Save modified terrain chunks
- Save player inventory and progression
- Save discovered structures and POIs
- Multiple save slots
- Auto-save system
- **Implementation**:
- Serialize DOTS entities to disk
- Chunk-based saving (only save modified chunks)
- Compression for save file size
- **Files**: `Scripts/Persistence/`

#### 4.2 Building System (Optional)

- **Features**:
- Place crafted structures in world
- Simple base building (platforms, walls, storage)
- Persistent placed objects
- Building mode UI
- **Integration**: Extend crafting system with placeable items
- **Files**: `Scripts/Building/`

#### 4.3 Visual Polish

- **Art Integration**:
- Replace all placeholder prefabs with proper low-poly models
- Consistent material palette across all assets
- Particle effects (destruction, collection, weather)
- Hand model with animations
- Player character model
- **Post-Processing**:
- Color grading for 16-bit aesthetic
- Optional pixelation shader
- Dithering for fog/shadows
- **Files**: `Art/Models/`, `Art/Materials/`, `Art/VFX/`

#### 4.4 Audio System

- **Audio Elements**:
- Terrain destruction SFX (per terrain type)
- Resource collection SFX
- Slingshot launch/landing SFX
- Ambient biome sounds
- UI interaction sounds
- Music system (ambient, exploration tracks)
- **Implementation**: Unity Audio with DOTS integration
- **Files**: `Audio/`, `Scripts/Audio/`

**Deliverables**:

- Player progress persists between sessions
- World modifications are saved and loaded
- Game has cohesive visual style with proper art
- Audio feedback enhances all interactions

---

### Phase 5: Content & Balancing (Priority: MEDIUM)

**Goal**: Fill the world with content and tune the experience

#### 5.1 Content Creation

- **Recipes**: 30-50 crafting recipes across all categories
- **Structures**: 20+ structure types with variations
- **Biomes**: 6+ fully realized biomes with unique features
- **Tools**: 8-10 distinct tools with different use cases
- **Resources**: 15-20 resource types with clear purposes

#### 5.2 Balancing Pass

- **Tuning**:
- Resource spawn rates and distribution
- Crafting costs and progression pacing
- Movement feel (slingshot power, cooldowns)
- Destruction power and terrain regeneration (if any)
- Biome difficulty progression
- **Testing**: Playtest and iterate on feel and pacing

#### 5.3 Tutorial & Onboarding

- **Features**:
- In-game tutorial for core mechanics
- Contextual hints for new features
- Optional tutorial area/starting zone
- Clear visual communication of mechanics

**Deliverables**:

- Rich content that provides 5-10 hours of gameplay
- Balanced progression that feels rewarding
- New players can understand core mechanics quickly

---

### Phase 6: Optimization & Release Prep (Priority: LOW)

**Goal**: Prepare for release

#### 6.1 Performance Optimization

- **Targets**:
- 60 FPS on mid-range hardware
- Minimal GC allocations (DOTS advantage)
- Efficient compute shader usage
- Memory optimization for large worlds
- **Profiling**: Use Unity Profiler and DOTS profiling tools

#### 6.2 Bug Fixing & Stability

- **Testing**: Comprehensive playthrough testing
- **Edge Cases**: Handle all error states gracefully
- **Crash Prevention**: Robust error handling

#### 6.3 Release Features

- **Settings Menu**: Graphics, audio, controls configuration
- **Main Menu**: New game, load game, settings, quit
- **Credits & About**: Team credits and game info

**Deliverables**:

- Stable, performant build ready for release
- Complete game experience from menu to gameplay
- Professional presentation

---

## Implementation Priority

### Immediate Next Steps (Weeks 1-4)

1. **Magic Hand System** - Core interaction mechanic
2. **Slingshot Movement** - Core traversal mechanic
3. **Resource Collection** - Complete the gameplay loop
4. **Basic HUD** - Show player state

### Short Term (Weeks 5-8)

5. **Enhanced Biomes** - Make world interesting
6. **Procedural Structures** - Add exploration goals
7. **Crafting System** - Give resources purpose

### Medium Term (Weeks 9-16)

8. **Tool System** - Expand player options
9. **Progression System** - Provide direction
10. **Save/Load** - Make progress persistent

### Long Term (Weeks 17-24)

11. **Visual Polish** - Cohesive art style
12. **Audio System** - Enhance feel
13. **Content Creation** - Fill the world
14. **Balancing & Testing** - Tune experience

---

## Technical Considerations

### DOTS Integration

- Continue hybrid DOTS + MonoBehaviour approach where appropriate
- Player systems can be DOTS-based for performance
- UI can remain MonoBehaviour for ease of development
- Maintain existing DOTS patterns (partial classes, one per file, etc.)

### Art Pipeline

- Follow established 16-bit art direction (low-poly, flat shading, small textures)
- Use Blender for modeling, export to FBX
- Create prefabs with Authoring + Baker pattern
- Bake to Entity prefabs in SubScenes

### Performance Budget

- Maintain 60 FPS target on mid-range hardware
- Leverage compute shaders for heavy computation
- Use DOTS Jobs for parallelizable work
- Implement aggressive LOD and culling

---

## Risk Assessment

### High Risk

- **Slingshot movement feel**: Requires significant iteration to feel good
- **World persistence**: Complex system with potential for bugs
- **Performance at scale**: Large worlds may strain streaming system

### Medium Risk

- **Crafting balance**: Requires extensive playtesting
- **Biome variety**: Needs strong art direction to feel distinct
- **Progression pacing**: Easy to make too grindy or too fast

### Low Risk

- **Magic hand destruction**: Core tech already exists
- **Resource collection**: Simple extension of existing systems
- **UI implementation**: Well-understood problem space

---

## Success Metrics

### Core Loop

- Player spends 70%+ time in exploration/movement
- Destruction feels satisfying (player feedback)
- Resource collection feels rewarding, not tedious

### Engagement

- Average session length: 30-60 minutes
- Player reaches mid-game progression: 80%+ of players
- Player completes game: 40%+ of players

### Technical

- Maintains 60 FPS: 95%+ of time
- Zero crashes: 99.9%+ uptime
- Save/load success rate: 100%

---

## Next Action

**Recommendation**: Start with Phase 1.1 (Magic Hand System) and 1.2 (Slingshot Movement) in parallel, as these define the core feel of the game. Once movement feels good, the rest of the game can be built around it.

### To-dos

- [ ] Implement Magic Hand System with raycast targeting, charge mechanic, and visual feedback
- [ ] Implement Slingshot Movement System with grip, pull-back, trajectory preview, and launch mechanics
- [ ] Implement Resource Collection System extending TerrainGlobComponent with automatic pickup
- [ ] Create Basic HUD showing resources, hand charge, and slingshot charge indicators
- [ ] Expand biome system with 6+ distinct biomes including grasslands, desert, mountains, forest, snow, and corrupted areas
- [ ] Implement procedural structure generation for ruins, caves, towers, shrines, and resource nodes
- [ ] Create crafting system with recipes, crafting stations, and UI for tools, upgrades, and consumables
- [ ] Implement tool system with multiple hand types (drill, explosive, shaping, grapple) and equipment management
- [ ] Create progression system tracking ability unlocks, stat upgrades, and recipe discoveries
- [ ] Implement world persistence system for terrain modifications, inventory, and progression state
- [ ] Replace placeholder art with proper low-poly models, add particle effects, and implement post-processing
- [ ] Implement audio system with SFX for all interactions and ambient biome sounds