# SDF Surface Nets Terrain – ECS Implementation Overview

## Title + Context
- The project represents terrain as signed distance fields (SDF) and converts them into meshes via a Surface Nets meshing pass.
- All processing is organized with Unity DOTS/ECS: chunks, sampling, meshing, and rendering are data-oriented systems and components.
- The pipeline flows from SDF sampling per chunk → Surface Nets vertex/index generation → blob-backed mesh data uploaded to ECS mesh/render components.

## High-Level Conceptual Overview
### Signed Distance Field Representation
- The terrain uses a grid-based SDF per chunk. Each chunk has a resolution (voxel counts) and voxel size defining world-space sampling positions.
- SDF values encode signed distance to the terrain surface: negative values are “inside” terrain, positive values are “outside,” and the iso-surface is at 0.
- Runtime edits are modeled as additive or subtractive spherical brushes applied on top of a base heightfield SDF.

### Surface Nets in This Project
- The density grid is interpreted as a lattice of cells (Resolution - 1 in each dimension). Each cell examines its 8 corner densities.
- If the cell contains a sign change (min < 0 and max > 0), a single Surface Net vertex is placed for that cell.
- The vertex position is computed by weighting corner positions inversely by absolute density (a simple centroid approximation); fallback is the cell center if weights vanish.
- Chunk world-space span/stride is `(ChunkGridResolution - 1) * VoxelSize` because neighboring chunks share border samples.
- Watertight seams across chunks require the mesher to “see” one extra layer of density samples on the +X/+Z borders (ghost samples). This project provides that by sampling a padded density grid and tracking its true resolution via `TerrainChunkDensityGridInfo`.

### ECS Mapping
- Terrain chunks, grid settings, density data, mesh blobs, and rendering flags are ECS components.
- Systems handle: sampling SDF into blob-backed density arrays, running Surface Nets to produce mesh blobs, preparing render bounds/components, and uploading meshes to Unity `Mesh` objects.
- Chunk entities are tagged when they need meshing or render upload, driving the frame-order execution of systems.

### Data Flow
1. SDF sources (base ground + edit buffer) define a continuous field.
2. Density sampling system evaluates the SDF on a per-chunk voxel grid and stores results in a blob.
3. Surface Nets job consumes the density grid to generate vertices/indices and stores them in a mesh blob.
4. Render prep ensures ECS render components/bounds exist; upload system pushes mesh data to Unity renderable assets and clears the upload tag.

## ECS Data Model (Components & Data Types)
### Terrain / Chunk Configuration
- **TerrainChunk** (`IComponentData`): Identifies a chunk by integer grid coordinates.
- **TerrainChunkBounds** (`IComponentData`): World origin of the chunk for sampling and vertex positioning.
- **TerrainChunkGridInfo** (`IComponentData`): Per-chunk resolution and voxel size; provides voxel count helper.

### SDF Parameters and Shapes
- **SDFTerrainFieldSettings** (`IComponentData`): Singleton holding base heightfield parameters (base height, amplitude, frequency, noise value).
- **SDFTerrainField** (Burst struct): Runtime sampler applying ground function plus buffered edits to compute densities.
- **SDFEdit** (`IBufferElementData`): Brush instances with center, radius, and operation (add/subtract) composing the runtime edit list.

### Meshing Buffers and Settings
- **TerrainChunkDensity** (`IComponentData`): Blob reference storing sampled density values per chunk; includes disposal helpers.
- **TerrainChunkDensityGridInfo** (`IComponentData`): The actual density sample grid resolution stored in `TerrainChunkDensity` (may be padded on +X/+Z for seam stitching).
- **TerrainChunkMeshData** (`IComponentData`): Blob reference holding generated vertices and indices for a chunk mesh.
- **TerrainChunkNeedsMeshBuild** (`IComponentData` tag): Marks chunks that should rebuild their mesh from density data.
- **TerrainChunkNeedsRenderUpload** (`IComponentData` tag): Marks chunks whose mesh blob must be uploaded to Unity mesh/render components.

## System Pipeline (Systems, Jobs, and Bakers)
### Sampling
- **TerrainChunkDensitySamplingSystem** (`ISystem`): Requires SDF settings; iterates chunk entities, samples SDF into a temporary array via `TerrainChunkDensitySamplingJob`, and stores results in a blob component on each chunk. Samples a padded density grid (+1 on +X and +Z) for seam stitching and writes `TerrainChunkDensityGridInfo`. Copies buffered `SDFEdit` entries into temp memory before sampling.

### Meshing
- **TerrainChunkMeshBuildSystem** (`ISystem`): Runs in the Simulation group for chunks tagged `TerrainChunkNeedsMeshBuild`. Invokes `TerrainChunkMeshBuilder.BuildMeshBlob`, which uses `TerrainChunkDensityGridInfo` (padded resolution) when running `SurfaceNetsJob`, attaches `TerrainChunkMeshData`, adds render-upload tag, and removes the mesh-build tag.
- **SurfaceNetsJob** (`IJob`): Executes the Surface Nets algorithm over the density grid, generating one vertex per sign-changing cell and constructing triangles across faces based on cell signs.

### Rendering / Upload
- **TerrainChunkRenderPrepSystem** (`ISystem`): Ensures `RenderBounds`, `LocalTransform`, and placeholder `MaterialMeshInfo` exist for chunk entities with mesh data; computes bounds from mesh vertices.
- **TerrainChunkMeshUploadSystem** (`ISystem`): After mesh build, loads render settings, allocates or reuses a Unity `Mesh`, uploads vertices/indices from the blob, sets `RenderMeshArray`/`MaterialMeshInfo`, and clears the upload tag.

### Authoring / Bootstrap
- **TerrainBootstrapAuthoring** (`MonoBehaviour`): Scene helper that spawns a grid of chunk entities with grid/bounds data, seeds SDF field settings, and tags chunks for mesh build to kick off the pipeline.

**Pipeline order (conceptual):**
- Sample SDF grid → Build mesh (Surface Nets) → Prepare render components/bounds → Upload mesh to Unity renderer

## File-by-File Walkthrough (Implementation Detail)
### Assets/Scripts/DOTS/Terrain/SDF/SDFMath.cs
- Burst-safe SDF primitives: sphere, box, ground heightfield, union/subtraction ops. Ground SDF combines sine waves and noise; subtraction respects exterior distances.

### Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainField.cs
- Encapsulates SDF sampling using `SDFMath.SdGround` and applies each `SDFEdit` brush (sphere union/subtraction) to produce a final density value per sample.

### Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainFieldSettings.cs
- ECS singleton storing base SDF parameters (heightfield amplitude/frequency/noise).

### Assets/Scripts/DOTS/Terrain/SDF/SDFEdit.cs
- Defines brush buffer element with center/radius/operation and helpers to create add/subtract edits.

### Assets/Scripts/DOTS/Terrain/SDF/Chunks/TerrainChunk*.cs
- **TerrainChunk**: chunk coordinate identifier.
- **TerrainChunkBounds**: world origin used when sampling and positioning vertices.
- **TerrainChunkGridInfo**: resolution/voxel size and voxel count helper.
- **TerrainChunkDensity/TerrainChunkDensityBlob**: blob-backed density buffer with disposal helpers.
- **TerrainChunkDensityGridInfo**: density sample grid resolution stored in `TerrainChunkDensity` (may be padded on +X/+Z for seam stitching).
- **TerrainChunkMeshData/TerrainChunkMeshBlob**: blob-backed mesh geometry (vertices/indices); includes tag `TerrainChunkNeedsMeshBuild` and render upload tag `TerrainChunkNeedsRenderUpload`.

### Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs
- Requires `SDFTerrainFieldSettings`; copies any `SDFEdit` buffer to a temp array.
- For each chunk, schedules/runs `TerrainChunkDensitySamplingJob` to fill a temp density array across a padded density resolution `(res.x+1, res.y, res.z+1)` using chunk origin and voxel size.
- Converts sampled densities into a persistent blob (`TerrainChunkDensity`) on the chunk (disposing previous blob if present) and writes `TerrainChunkDensityGridInfo` so meshing can interpret the blob correctly.

### Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuilder.cs
- Converts density blob to a mesh blob: copies densities into a temp `NativeArray`, allocates working arrays (cell signs and vertex indices), and runs `SurfaceNetsJob` using the resolution from `TerrainChunkDensityGridInfo` (which may be padded for seam stitching).
- After job completion, serializes the generated vertices/indices into a persistent `TerrainChunkMeshBlob` for ECS storage; disposes temp allocations.

### Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs
- Implements the Surface Nets job:
  - Iterates cells (Resolution-1 per axis), sampling the 8 corner densities.
  - Tracks min/max/sign sum; if a sign change exists, computes weighted average of corner positions (inverse abs density) as vertex, otherwise skips.
  - Records per-cell sign and vertex index arrays for neighbor stitching.
        - Builds triangles on XY/XZ/YZ faces when adjacent cell signs differ; outputs triangles with consistent winding for backface culling.
        - Uses base-vs-padded cell resolution to emit boundary faces against a +X/+Z ghost layer (seam stitching) without double-emitting the overlap region.
  - Includes fallback triangle fan if indices are empty but vertices exist (degenerate case handling).

### Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs
- For chunks marked `TerrainChunkNeedsMeshBuild`, invokes mesh builder to regenerate geometry, writes/updates `TerrainChunkMeshData`, tags for render upload, and clears the mesh build tag.

### Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkRenderPrepSystem.cs
- Ensures render-oriented ECS components exist for entities with mesh data, computing `RenderBounds` from mesh vertices and adding default transforms/material info placeholders.

### Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs
- After mesh build, gathers entities needing upload, creates/reuses Unity `Mesh` objects, uploads blob vertices/indices to Unity mesh data, assigns `RenderMeshArray` + `MaterialMeshInfo`, and removes the upload tag.

### Assets/Scripts/DOTS/Terrain/Bootstrap/TerrainBootstrapAuthoring.cs
- Editor/runtime helper to seed the world: creates SDF settings singleton, spawns a grid of chunk entities with grid info/bounds, tags them for mesh build, and ensures camera/light are present.

## Data Flow Summary
- **Chunk creation**: Bootstrap spawns chunk entities with coordinates, grid resolution/voxel size, world origin, and a `TerrainChunkNeedsMeshBuild` tag; SDF settings singleton is ensured.
- **SDF definition**: Ground parameters plus optional `SDFEdit` buffer define the signed distance field sampled per chunk.
- **Sampling → Mesh**: Density sampling system fills a blob with voxel densities; mesh build system runs Surface Nets over those densities to produce mesh blobs and marks chunks for render upload.
- **Rendering**: Render prep adds bounds/transform/material placeholders; mesh upload system pushes blob geometry into Unity `Mesh` assets, sets render components, and clears the upload flag.

```
SDF Settings + SDFEdit buffer
        │
        ▼
DensitySamplingJob per chunk → TerrainChunkDensity blob
        │
        ▼
SurfaceNetsJob → TerrainChunkMeshBlob
        │
        ▼
RenderPrep (bounds/components) → MeshUpload (Unity Mesh + Material)
```

## Future Extension Notes
- Multiple SDF primitives or noise sources could be composed in `SDFTerrainField` for richer terrain without altering the meshing pipeline.
- LOD or streaming: chunk tags (`NeedsMeshBuild`/`NeedsRenderUpload`) provide a hook for conditional rebuilds when resolutions or edits change.
- Additional Surface Nets optimizations (Burst parallel jobs, dual grids) could reduce allocation and improve performance for large worlds.
