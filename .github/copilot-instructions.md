# Copilot Instructions for voronoi-fracture-716

## Project Overview
This Unity project implements 2D Voronoi-based fracture simulation. The core logic is split between a geometry library (for Delaunay triangulation and Voronoi cell generation) and Unity MonoBehaviours for scene setup, mesh generation, and interactive physics-based fracturing.

## Key Components
- **Geometry-Lib/Geometry/**: Pure C# geometry algorithms (DelaunayTriangulation, VoronoiGenerator, Point, etc.). No Unity dependencies.
- **Assets/GeometryLib/Scripts/**: Unity MonoBehaviours for fracture logic 
- **Assets/GeometryLib/Editor/**: Custom Unity Editor inspectors for fracture components.

## Conventions & Patterns
- All geometry code is in `Geometry-Lib/Geometry/` and is Unity-agnostic.
- Unity scripts are in `Assets/GeometryLib/Scripts/` and use the geometry library for computation.
- Fracture logic is 2D-only .

## Integration Points
- Geometry library is imported via `using Geometry;` in Unity scripts.
- No external Unity packages required beyond standard 2D physics and rendering.

## Example: Adding a New Fracture Type
1. Add new geometry logic to `Geometry-Lib/Geometry/`.
2. Create a new MonoBehaviour in `Assets/GeometryLib/Scripts/` that uses the geometry logic.
3. Add custom inspector/editor scripts in `Assets/GeometryLib/Editor/` as needed.

---
If any section is unclear or missing, please provide feedback for further refinement.
