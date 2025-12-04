# Voronoi Fracture Pedadogical Aid
## Team Members
Serena Akpoyibo, Issac Lee, Galen Sagarin

## Link to Project:
issacl891.github.io/voronoi-fracture-716/

## Project Description
This is a application tool that implements Voronoi fractures on a 2D plane in C# and visualizes them using Unity game engine. The application is designed to show the effect of a shape colliding with the surface or another shape.

## Background Information 

## User Interaction
* Wait for collision :- When enabled, it would not display fracture immediately
* Impact Threshold :- Measure of force needed to create fracture
* Break Depth:- Number of times recursively the object can fracture after initial fracture
* Time Rewinder:- Playback of the fracture

**The break depth is capped at 3 because more depth was too computationally heavy.* 

## Implementation and Optimizations
Some objects are predefined (circle, triangle, and square for now) which have the sprite(tells Unity how to render it), fracturable component (tells Unity object can break), and a collider(gives physics properties to Object). The user click spawns in an instance of the object.
<br>
* **1st Step:** Generate Voronoi diagram (that is a square) based off random seed points selected inside the polygon. The Voronoi implementation uses the Delaunay Triangulation to Voronoi fractures implementation. For Delaunay's triangulation, the random points are passed and a super triangle that would be broken into smaller, legal triangles is made. 
Bad triangles are triangles that contain a new point in their circumcircle and these are removed. Only boundary edges (appear only once) kept and used to make triangles.
The list of triangles is then passed to the Voronoi fractures generation. For each triangle, we associate the vertex with the circumcenter (the cells). The cells of each vertex are sorted counterclockwise and then returned as a mapping of vertex to its cells. 

* **2nd Step:** Clip the diagram into the actual polygon.
* **3rd Step:** The fragments (boundary of Voronoi cells) are passed to Unity which creates a mesh (the object represented for the Physics engine) and a sprite (the object represented for the rendering engine) for each cell.

#### Object Pooling
One of the optimizations we did to make things run more smoothly was using object pooling. 

