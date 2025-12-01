Purpose

Define the next concrete steps to bring SDF + Surface Nets + destructible terrain into the main Unity ECS/DOTS 16-bit crafting game, using the existing SDF lab work as the math/algorithm reference.

This SPEC is written for any AI coding assistant (e.g., ChatGPT, Copilot, Claude, Cursor, etc.) to implement in small, verifiable, review-gated steps following the SPEC → TEST → CODE workflow.

This document describes a minimal vertical slice:

A small grid of chunks

SDF-based terrain

Surface Nets meshing

Live editing (add/subtract sphere)

1. High-Level Goals

Integrate an SDF terrain backend that:

Evaluates terrain density at world positions

Supports additive and subtractive edits

Implement Surface Nets meshing with Burst jobs.

Generate chunk meshes via Mesh.MeshDataArray and display them through Entities Graphics.

Support simple interactive editing to modify terrain live.

Keep the system structured so future extensions can drop in cleanly:

Biomes

Chunk streaming

More complex shapes

Caves

Noise layering

2. Tech Context & Constraints

Unity Version: Unity 6+ or 2022 LTS+
Render Pipeline: Built-in or URP
Dependencies (ECS stack):

com.unity.entities

com.unity.entities.graphics

com.unity.mathematics

com.unity.collections

com.unity.jobs

com.unity.burst

Guiding principles:

Prefer small, isolated changes you can review.

Follow SPEC → TEST → CODE strictly.

Avoid refactoring unrelated systems unless required.

3. Roadmap / Phase Overview
Phase 1 — SDF Backend & Edit Data

Define Burst-compatible SDF sampling logic and edit structures.

Phase 2 — Chunk Density Sampling Job

Sample SDF values into a per-chunk 3D density grid.

Phase 3 — Surface Nets Meshing Job

Convert the density grid into a mesh using Surface Nets.

Phase 4 — ECS Terrain Chunk Rendering

Render chunks using Entities Graphics.

Phase 5 — Simple Interactive Editing

Add/subtract terrain via player input and rebuild affected chunks.

Each phase requires small debug helpers and/or minimal tests.

4. Phase 1 — SDF Backend & Edit Data
4.1 Files / Structure

Create/update:

Assets/Scripts/Terrain/SDF/SDFMath.cs
Assets/Scripts/Terrain/SDF/SDFTerrainField.cs
Assets/Scripts/Terrain/SDF/SDFEdit.cs

4.2 SDFMath.cs

A static, Burst-safe math class.

Implement:

float SdSphere(float3 p, float radius)

float SdBox(float3 p, float3 halfExtents)

Terrain-style height SDF:
float SdGround(float3 p, float amplitude, float frequency, float baseHeight, float noiseValue)

Boolean ops:

OpUnion(a, b) → math.min(a, b)

OpSubtraction(a, b) → math.max(a, -b)

Smooth union helper (optional for now).

No UnityEngine types.

4.3 SDFEdit.cs
struct SDFEdit : IBufferElementData {
    float3 center;
    float radius;
    int op; // +1 add, -1 subtract
}


Use global DynamicBuffer<SDFEdit> for now.

4.4 SDFTerrainField.cs

A Burst-safe struct containing:

Terrain noise parameters

Base height/amplitude/frequency

Public method:

float Sample(float3 worldPos, NativeArray<SDFEdit> edits)


Steps:

Evaluate ground SDF.

Loop through edits: apply union/subtraction with spheres.

4.5 Phase 1 Tests

Validate:

Sphere SDF: negative/zero/positive behavior

TerrainField:

No edits → smooth ground

Subtraction edit → hollow

Addition edit → bump

Minimal tests only.

5. Phase 2 — Chunk Density Sampling Job
5.1 Components

TerrainChunk : IComponentData

int3 ChunkCoord

TerrainChunkGridInfo : IComponentData

int3 Resolution

float VoxelSize

TerrainChunkBounds : IComponentData

float3 WorldOrigin

TerrainChunkDensity : IComponentData

Reference to grid data (NativeArray or Blob for now)

5.2 Sampling Job

Create:

TerrainChunkDensitySamplingSystem

For each chunk:

Allocate NativeArray<float> of size resolution³

Convert each index → (ix, iy, iz)

Compute worldPos

Call terrainField.Sample(worldPos, edits)

Store density

Pass density grid to Phase 3

Read edits once into a temporary NativeArray<SDFEdit>.

5.3 Debugging

Log min/max density

Visualize chunk bounds

Ensure job completes without errors

6. Phase 3 — Surface Nets Meshing Job
6.1 Mesh Builder Job

Create:

Assets/Scripts/Terrain/Meshing/SurfaceNets.cs

Implement:

public struct SurfaceNetsJob : IJob {
    [ReadOnly] public NativeArray<float> Densities;
    public int3 Resolution;
    public float VoxelSize;
    public float3 ChunkOrigin;

    public NativeList<float3> Vertices;
    public NativeList<int> Indices;

    public void Execute() {
        // Surface Nets implementation
    }
}


Use the same interpolation logic from the SDF lab.

6.2 Chunk Mesh Build System

TerrainChunkMeshBuildSystem

Steps:

For each chunk needing a mesh or marked dirty:

Create NativeList<float3> + NativeList<int>

Run SurfaceNetsJob

Allocate a Mesh.MeshData

Write vertices/indices

Assign mesh to chunk entity

Dispose temporary data

6.3 Debug

Test single chunk creation end-to-end.

7. Phase 4 — ECS Terrain Chunk Rendering
7.1 Required Components

Each chunk must have:

LocalTransform

MaterialMeshInfo

RenderBounds

Other Entities Graphics components (based on your project template)

7.2 Bootstrap

Create a TerrainBootstrapAuthoring MonoBehaviour to:

Spawn a small grid (e.g., 3×3 chunks)

Add required ECS components

Ensure camera + lighting exist

7.3 Validate

Multiple chunks appear

No Burst/job exceptions

Reasonable performance (24³ or 32³ resolution)

8. Phase 5 — Simple Interactive Editing
8.1 Input → Edit

On click/key:

Raycast (or fixed forward position)

Find editCenter

Add:

SDFEdit { center = ..., radius = ..., op = ±1 }


…to global buffer.

8.2 Dirty Flag

Add TerrainChunkDirty : IComponentData

Mark:

All chunks dirty (initial version), or

Only intersecting chunks (advanced)

8.3 Rebuild Path

DensitySamplingSystem regenerates density for dirty chunks

MeshBuildSystem rebuilds mesh and clears dirty flag

8.4 Test

Add/subtract sphere

Mesh updates correctly

No leaks

No rebuild spam

9. Implementation Order for AI (Strict Review-Gated Sequence)

AI coding assistants must follow these rules exactly:

Execution Rules

Implement changes in small, isolated, reviewable steps.
Each step should correspond to a single phase or sub-phase listed in this SPEC.

After each step or phase, STOP.
Present:

What files were added/modified

Summary of logic

Any tests or debug tools added

Do not proceed until the human approves.

Do not combine phases or skip ahead.
Example:

Do not mesh chunks and sample densities in one step.

Do not build systems before components.

Do not integrate rendering while still defining SDF math.

Follow SPEC → TEST → CODE consistently.

Define types

Add minimal tests or debug scaffolding

Implement logic

Wait for human sign-off

Use AI-agnostic conventions.
This SPEC must work for any AI tool—no tool-specific assumptions.

10. Prompt to Use With AI

After adding this file to your repo at:

AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md

Use a prompt like:

Read AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md.
Begin with Phase 1.
Implement only the next small, reviewable step.
Follow SPEC → TEST → CODE.
Stop after completing the step and wait for my review before continuing.