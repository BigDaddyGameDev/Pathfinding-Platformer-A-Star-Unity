# A* Pathfinding for a 2D Tile-Based Platformer

I've stripped down the original code to the bare minimum.

Changes (for better / worse):
- Uses tilemaps/tiles rather than instantiating an object per block
- No finicking with the frames
- Lerp to next node
- Bot will clip through corners
- Removed one-way platforms and some of the logic like checking if an obstacle is between you and the next node.

![Example](https://i.imgur.com/WvX68rQ.gif)

Forked from: Daniel Branicki. [See his tutorial](http://gamedevelopment.tutsplus.com/tutorials/how-to-adapt-a-pathfinding-to-a-2d-grid-based-platformer-theory--cms-24662)
