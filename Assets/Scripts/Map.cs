using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Algorithms;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using System.Drawing;

[System.Serializable]
public enum TileType
{
    Empty = 0,
    Block = 1,
    OneWay = 2
}

[System.Serializable]
public partial class Map : MonoBehaviour 
{
	/// The map's position in world space. Bottom left corner.
	Vector3 position;
	
	/// The path finder.
	public PathFinderFast mPathFinder;
	
	/// The nodes that are fed to pathfinder.
	[HideInInspector]
	public byte[,] mGrid;
	
	/// The map's tile data.
	[HideInInspector]
	private TileType[,] tiles;
	
	/// A parent for all the sprites. Assigned from the inspector.
	public Transform mSpritesContainer;
	
	
	/// The length/height of the map in tiles.
	public int Size = 20;

    public Bot player;

    int lastMouseTileX = -1;
    int lastMouseTileY = -1;

    public Tilemap tilemap;
    public Tile tileBlock, tilePlatform;

	public TileType GetTile(int x, int y) 
	{
        if (x < 0 || x >= Size
            || y < 0 || y >= Size)
            return TileType.Block;

		return tiles[x, y]; 
	}

    public bool IsOneWayPlatform(int x, int y)
    {
        if (x < 0 || x >= Size
            || y < 0 || y >= Size)
            return false;

        return (tiles[x, y] == TileType.OneWay);
    }

    public bool IsGround(int x, int y)
    {
        return (tiles[x, y] == TileType.OneWay || tiles[x, y] == TileType.Block);
    }

    public bool IsObstacle(int x, int y)
    {
        if (x < 0 || x >= Size
            || y < 0 || y >= Size)
            return true;

        return (tiles[x, y] == TileType.Block);
    }

    public bool IsNotEmpty(int x, int y)
    {
        if (x < 0 || x >= Size
            || y < 0 || y >= Size)
            return true;

        return (tiles[x, y] != TileType.Empty);
    }

	public void InitPathFinder()
	{
		mPathFinder = new PathFinderFast(mGrid, this);
		
		mPathFinder.Formula                 = HeuristicFormula.Manhattan;
		//if false then diagonal movement will be prohibited
        mPathFinder.Diagonals               = false;
		//if true then diagonal movement will have higher cost
        mPathFinder.HeavyDiagonals          = false;
		//estimate of path length
        mPathFinder.HeuristicEstimate       = 6;
        mPathFinder.PunishChangeDirection   = false;
        mPathFinder.TieBreaker              = false;
        mPathFinder.SearchLimit             = 1000000;
        mPathFinder.DebugProgress           = false;
        mPathFinder.DebugFoundPath          = false;
	}
	
	public void GetMapTileAtPoint(Vector2 point, out int tileIndexX, out int tileIndexY)
	{
		tileIndexY =(int)((point.y - position.y /2.0f));
		tileIndexX =(int)((point.x - position.x /2.0f));
	}
	
	public Vector2i GetMapTileAtPoint(Vector2 point)
	{
		return new Vector2i((int)((point.x - position.x/2.0f)),
					(int)((point.y - position.y /2.0f)));
	}
	
	public Vector2 GetMapTilePosition(int tileIndexX, int tileIndexY)
	{
		return new Vector2(
				(float) (tileIndexX ) + position.x,
				(float) (tileIndexY ) + position.y
			);
	}

	public Vector2 GetMapTilePosition(Vector2i tileCoords)
	{
		return new Vector2(
			(float) (tileCoords.x ) + position.x,
			(float) (tileCoords.y ) + position.y
			);
	}
	
	public bool CollidesWithMapTile(AABB aabb, int tileIndexX, int tileIndexY)
	{
		var tilePos = GetMapTilePosition (tileIndexX, tileIndexY);
		
		return aabb.Overlaps(tilePos, new Vector2( 0.5f, 0.5f));
	}

    public bool AnySolidBlockInRectangle(Vector2 start, Vector2 end)
    {
        return AnySolidBlockInRectangle(GetMapTileAtPoint(start), GetMapTileAtPoint(end));
    }

    // The AnySolidBlockInStripe function checks whether there are any solid tiles between
    // two given points on the map. The points need to have the same x-coordinate. The x-coordinate
    // we are checking is the tile we'd like the character to move into, but we're not sure
    // if we can, as explained above. 
    public bool AnySolidBlockInStripe(int x, int y0, int y1)
    {
        int startY, endY;

        if (y0 <= y1)
        {
            startY = y0;
            endY = y1;
        }
        else
        {
            startY = y1;
            endY = y0;
        }

        for (int y = startY; y <= endY; ++y)
        {
            if (GetTile(x, y) == TileType.Block)
                return true;
        }

        return false;
    }

    public bool AnySolidBlockInRectangle(Vector2i start, Vector2i end)
    {
        int startX, startY, endX, endY;

        if (start.x <= end.x)
        {
            startX = start.x;
            endX = end.x;
        }
        else
        {
            startX = end.x;
            endX = start.x;
        }

        if (start.y <= end.y)
        {
            startY = start.y;
            endY = end.y;
        }
        else
        {
            startY = end.y;
            endY = start.y;
        }

        for (int y = startY; y <= endY; ++y)
        {
            for (int x = startX; x <= endX; ++x)
            {
                if (GetTile(x, y) == TileType.Block)
                    return true;
            }
        }

        return false;
    }

    public void SetTile(int x, int y, TileType type)
    {
        if (x <= 1 || x >= Size - 2 || y <= 1 || y >= Size - 2)
            return;

        tiles[x, y] = type;

        if (type == TileType.Block)
        {
            mGrid[x, y] = 0;
            tilemap.SetTile(new Vector3Int(x, y), tileBlock);
        }
        else if (type == TileType.OneWay)
        {
            mGrid[x, y] = 1;
            tilemap.SetTile(new Vector3Int(x, y), tilePlatform); 
        }
        else
        {
            mGrid[x, y] = 1;
            tilemap.SetTile(new Vector3Int(x, y), null);
        }

    }

    public void Start()
    {
        Application.targetFrameRate = 60;
        
        //set the position
        position = transform.position;
        tiles = new TileType[Size, Size];

        mGrid = new byte[Mathf.NextPowerOfTwo((int)Size), Mathf.NextPowerOfTwo((int)Size)];
        InitPathFinder();

        // Set Tiles
        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                SetTile(x, y, TileType.Empty);

                // border 2 blocks thick
                if (x <= 1 || x >= Size - 2 || y <= 1 || y >= Size - 2)
                {
                    tiles[x, y] = TileType.Block;
                    tilemap.SetTile(new Vector3Int(x,y), tileBlock);
                }
            }
        }

        player.mMap = this;
        
        InvokeRepeating("UpdateBot", 0f, 1f);

    }

    void UpdateBot()
    {
        if (!player.Updating)
            StartCoroutine(player.BotUpdate());
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Mouse0))
            lastMouseTileX = lastMouseTileY = -1;

        Vector2 mousePos = Input.mousePosition;
        var mousePosInWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);//cameraPos + mousePos -

        int mouseTileX, mouseTileY;
        GetMapTileAtPoint(mousePosInWorld, out mouseTileX, out mouseTileY);

        Vector2 offsetMouse = (Vector2)(Input.mousePosition) - new Vector2(Camera.main.pixelWidth/2, Camera.main.pixelHeight/2);
        
        // MOVE BOT TO POSITION
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            player.TappedOnTile(new Vector2i(mouseTileX, mouseTileY));
        }

        // PLACE / REMOVE TILE
        if (Input.GetKey(KeyCode.Mouse1) || Input.GetKey(KeyCode.Mouse2))
        {
            if (mouseTileX != lastMouseTileX || mouseTileY != lastMouseTileY || Input.GetKeyDown(KeyCode.Mouse1) || Input.GetKeyDown(KeyCode.Mouse2))
            {
                if (!IsNotEmpty(mouseTileX, mouseTileY))
                    SetTile(mouseTileX, mouseTileY, Input.GetKey(KeyCode.Mouse1) ? TileType.Block : TileType.OneWay);
                else
                    SetTile(mouseTileX, mouseTileY, TileType.Empty);

                lastMouseTileX = mouseTileX;
                lastMouseTileY = mouseTileY;

            }
        }
    }

}
