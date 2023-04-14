using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Algorithms;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.ParticleSystem;
using Unity.VisualScripting;
using UnityEngine.UIElements;
using System.ComponentModel;
using Unity.Mathematics;
using TMPro;

public class Bot : MonoBehaviour
{
    public static readonly float[] cHalfSizes = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };

    public enum BotAction
    {
        None = 0,
        MoveTo,
    }

    public List<Vector2i> mPath = new List<Vector2i>();

    /// The current position.
    Vector2 mPosition;

    public Vector2 mAABBOffset;

    /// The AABB for collision queries.
    public AABB mAABB;

    /// The tile map.
    public Map mMap;

    /// True if the instance is on the ground.
    public bool mOnGround = false;

    public bool mOnOneWayPlatform = false;

    int mFramesOfJumping = 0;

    /// If the object is colliding with one way platform tile and the distance to the tile's top is less
    /// than this threshold, then the object will be aligned to the one way platform.
    public float OneWayPlatformThreshold = 2.0f;

    public bool mIgnoresOneWayPlatforms = false;

    public BotAction mCurrentAction = BotAction.None;

    public Vector2 mDestination;

    int mCurrentNodeId = -1;

    public int mMaxJumpHeight = 5;

    [Range(1, 3)]
    public int PlayerTileWidth = 1;

    [Range(1, 3)]
    public int PlayerTileHeight = 1;

    public float BotMaxPositionError = 0.99f;

    [HideInInspector]
    public bool Updating = false;

    private void Awake()
    {
        mPosition = transform.position;
    }


    public void TappedOnTile(Vector2i mapPos)
    {
        while (!(mMap.IsGround(mapPos.x, mapPos.y)))
            --mapPos.y;

        // START PATH FIND
        MoveTo(new Vector2i(mapPos.x, mapPos.y + 1));
    }



    public IEnumerator BotUpdate()
    {
        if (Updating)
            yield break;

        Updating = true;

        mOnGround = PlayerIsTouchingGround(transform.position);

        int dir;

        int tileX, tileY;

        Vector3 startPosition, destination;
        float startTime, totalDistance;

        mMap.GetMapTileAtPoint(transform.position, out tileX, out tileY);

        switch (mCurrentAction)
        {
            default:
            case BotAction.None:

                TestJumpValues();

                if (!mOnGround)
                {
                    startPosition = transform.position;
                    startTime = Time.time;
                    destination = transform.position -= Vector3.up;
                    totalDistance = Vector3.Distance(transform.position, destination);

                    while (transform.position != destination)
                    {
                        float newPercentageBetweenVectors = (Time.time - startTime) * Speed / totalDistance;
                        transform.position = Vector3.Lerp(startPosition, destination, newPercentageBetweenVectors);

                        yield return 0;

                    }
                }

                break;

            case BotAction.MoveTo:

                Vector2 prevDest, currentDest, nextDest;
                bool destOnGround, reachedY, reachedX;

                GetContext(out prevDest, out currentDest, out nextDest, out destOnGround, out reachedX, out reachedY);

                // Lerp to new position
                startPosition = transform.position;
                startTime = Time.time;
                totalDistance = Vector3.Distance(transform.position, currentDest);
                destination = currentDest;

                while (transform.position != destination)
                {
                    float newPercentageBetweenVectors = (Time.time - startTime) * Speed / totalDistance;
                    transform.position = Vector3.Lerp(startPosition, currentDest, newPercentageBetweenVectors);

                    yield return 0;

                }
                //yield return new WaitForSeconds(Speed * dist * Time.deltaTime);
                mCurrentNodeId += 1;

                if (mCurrentNodeId < mPath.Count)
                {
                    if (mOnGround)
                    {
                        // CHECK IF JUMP REQUIRED
                        mFramesOfJumping = GetJumpFramesForNode(mCurrentNodeId - 1);
                    }

                    goto case BotAction.MoveTo;

                }
                else
                {
                    // the character has reached the goal
                    mCurrentNodeId = -1;
                    ChangeAction(BotAction.None);
                    mPath.Clear();
                    break;

                }
        }

        Updating = false;

        //yield return new WaitForSeconds(1f);
    }


    bool IsOnGroundAndFitsPos(Vector2i pos)
    {
        for (int y = pos.y; y < pos.y + PlayerTileHeight; ++y)
        {
            for (int x = pos.x; x < pos.x + PlayerTileWidth; ++x)
            {
                if (mMap.IsObstacle(x, y))
                    return false;
            }
        }

        for (int x = pos.x; x < pos.x + PlayerTileWidth; ++x)
        {
            if (mMap.IsGround(x, pos.y - 1))
                return true;
        }

        return false;
    }

    // INIT PATH FIND
    public void MoveTo(Vector2i destination)
    {
       
        // SET START TILE
        Vector2i startTile = mMap.GetMapTileAtPoint(new Vector3((float)((int)Mathf.Round(transform.position.x)), (float)((int)Mathf.Round(transform.position.y)), -1f));

        // CHECK PLAYER WILL FIT IN THE END POSITION
        if (mOnGround && !IsOnGroundAndFitsPos(startTile))
        {
            if (IsOnGroundAndFitsPos(new Vector2i(startTile.x + 1, startTile.y)))
                startTile.x += 1;
            else
                startTile.x -= 1;
        }

        var path = mMap.mPathFinder.FindPath(
                        startTile,
                        destination,
                        PlayerTileWidth,
                        PlayerTileHeight,
                        (short)mMaxJumpHeight);


        mPath.Clear();

        if (path != null && path.Count > 1)
        {
            for (var i = path.Count - 1; i >= 0; --i)
                mPath.Add(path[i]);

            mCurrentNodeId = 1;

            // START UPDATE FX
            ChangeAction(BotAction.MoveTo);

            mFramesOfJumping = GetJumpFramesForNode(0);
        }
        else
        {
            mCurrentNodeId = -1;

            if (mCurrentAction == BotAction.MoveTo)
                mCurrentAction = BotAction.None;
        }

        if (!Debug.isDebugBuild)
            DrawPathLines();
    }

    public void MoveTo(Vector2 destination)
    {
        MoveTo(mMap.GetMapTileAtPoint(destination));
    }


    public void ChangeAction(BotAction newAction)
    {
        mCurrentAction = newAction;
    }

    int GetJumpFrameCount(int deltaY)
    {
        if (deltaY <= 0)
            return 0;
        else
        {
            switch (deltaY)
            {
                case 1:
                    return 1;
                case 2:
                    return 2;
                case 3:
                    return 6;
                case 4:
                    return 9;
                case 5:
                    return 15;
                case 6:
                    return 21;
                default:
                    return 30;
            }
        }
    }

    // check whether the character reached the next goal before it reached the current one.
    public bool ReachedNodeOnXAxis(Vector2 pathPosition, Vector2 prevDest, Vector2 currentDest)
    {
        return (prevDest.x <= currentDest.x && pathPosition.x >= currentDest.x)
            || (prevDest.x >= currentDest.x && pathPosition.x <= currentDest.x)
            || Mathf.Abs(pathPosition.x - currentDest.x) <= BotMaxPositionError;

    }

    // check whether the character reached the next goal before it reached the current one.
    public bool ReachedNodeOnYAxis(Vector2 pathPosition, Vector2 prevDest, Vector2 currentDest)
    {
        return (prevDest.y <= currentDest.y && pathPosition.y >= currentDest.y)
            || (prevDest.y >= currentDest.y && pathPosition.y <= currentDest.y)
            || (Mathf.Abs(pathPosition.y - currentDest.y) <= BotMaxPositionError);
    }

    public void GetContext(out Vector2 prevDest, out Vector2 currentDest, out Vector2 nextDest, out bool destOnGround, out bool reachedX, out bool reachedY)
    {

        prevDest = new Vector2(mPath[mCurrentNodeId - 1].x +
            mMap.transform.position.x,
            mPath[mCurrentNodeId - 1].y + mMap.transform.position.y);

        currentDest = new Vector2(mPath[mCurrentNodeId].x +
            mMap.transform.position.x,
            mPath[mCurrentNodeId].y + mMap.transform.position.y);

        nextDest = currentDest;

        if (mPath.Count > mCurrentNodeId + 1)
        {
            nextDest = new Vector2(mPath[mCurrentNodeId + 1].x + mMap.transform.position.x,
                mPath[mCurrentNodeId + 1].y + mMap.transform.position.y);
        }

        destOnGround = false;

        for (int x = mPath[mCurrentNodeId].x; x < mPath[mCurrentNodeId].x + PlayerTileWidth; ++x)
        {
            if (mMap.IsGround(x, mPath[mCurrentNodeId].y - 1))
            {
                destOnGround = true;
                break;
            }
        }

        Vector2 pathPosition = mAABB.Center - mAABB.HalfSize + Vector2.one * 0.5f;

        // check whether the character reached the next goal before it reached the current one
        reachedX = ReachedNodeOnXAxis(pathPosition, prevDest, currentDest);
        reachedY = ReachedNodeOnYAxis(pathPosition, prevDest, currentDest);

        //snap the character if it reached the goal but overshot it by more than cBotMaxPositionError
        if (reachedX && Mathf.Abs(pathPosition.x - currentDest.x) > BotMaxPositionError && Mathf.Abs(pathPosition.x - currentDest.x) < BotMaxPositionError * 3.0f)
        {
            pathPosition.x = currentDest.x;
            mPosition.x = pathPosition.x * 0.5f + mAABB.HalfSizeX + mAABBOffset.x;
        }

        if (destOnGround && !mOnGround)
            reachedY = false;
    }

    public void TestJumpValues()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            mFramesOfJumping = GetJumpFrameCount(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            mFramesOfJumping = GetJumpFrameCount(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            mFramesOfJumping = GetJumpFrameCount(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            mFramesOfJumping = GetJumpFrameCount(4);
        else if (Input.GetKeyDown(KeyCode.Alpha5))
            mFramesOfJumping = GetJumpFrameCount(5);
        else if (Input.GetKeyDown(KeyCode.Alpha6))
            mFramesOfJumping = GetJumpFrameCount(6);
    }

    public int GetJumpFramesForNode(int prevNodeId)
    {
        int currentNodeId = prevNodeId + 1;

        // jump if the new node is higher than the previous one and the character is on the ground
        if (mPath[currentNodeId].y - mPath[prevNodeId].y > 0 && mOnGround)
        {
            // To find out how many tiles we'll need to jump, we're going to iterate through nodes
            // for as long as they go higher and higher. When we get to a node that is at a lower height,
            // or a node that has ground under it, we can stop, since we know that there will be no need
            // to go higher than that.

            int jumpHeight = 1;

            for (int i = currentNodeId; i < mPath.Count; ++i)
            {
                if (mPath[i].y - mPath[prevNodeId].y >= jumpHeight)
                {
                    // If the next node is higher than the jumpHeight, and it's not on the ground, then let's set the new jump height
                    jumpHeight = mPath[i].y - mPath[prevNodeId].y;
                }

                if (mPath[i].y - mPath[prevNodeId].y < jumpHeight || mMap.IsGround(mPath[i].x, mPath[i].y - 1))
                {
                    // If the new node height is lower than the previous, or it's on the ground, then we return the number of frames of jump needed for the found height.
                    return GetJumpFrameCount(jumpHeight);
                }
            }
        }

        // there's no need to jump. return 0.
        return 0;
    }

    public bool PlayerIsTouchingGround(Vector3 pos)
    {
        Vector3 groundPos = new Vector3((int)Mathf.Round(pos.x), (int)Mathf.Floor(pos.y - 0.5f), 0f);

        if (mMap.IsGround((int)groundPos.x, (int)groundPos.y))
        {
            return true;
        }

        return false;
    }



    #region DRAW GIZMOS

    void OnDrawGizmos()
    {
        DrawMovingObjectGizmos();
        //draw the path

        if (mPath != null && mPath.Count > 0)
        {
            var start = mPath[0];

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(mMap.transform.position + new Vector3(start.x, start.y, -5.0f), 0.5f);

            for (var i = 1; i < mPath.Count; ++i)
            {
                var end = mPath[i];
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(mMap.transform.position + new Vector3(end.x, end.y, -5.0f), 0.5f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(mMap.transform.position + new Vector3(start.x, start.y, -1f),
                                mMap.transform.position + new Vector3(end.x, end.y, -1));
                start = end;
            }
        }
    }

    /// <summary>
    /// Draws the aabb and ceiling, ground and wall sensors .
    /// </summary>
    protected void DrawMovingObjectGizmos()
    {
        //calculate the position of the aabb's center
        var aabbPos = transform.position + (Vector3)mAABBOffset;

        //draw the aabb rectangle
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(aabbPos, mAABB.HalfSize * 2.0f);

        //draw the ground checking sensor
        Vector2 bottomLeft = aabbPos - new Vector3(mAABB.HalfSizeX, mAABB.HalfSizeY, 0.0f) - Vector3.up + Vector3.right;
        var bottomRight = new Vector2(bottomLeft.x + mAABB.HalfSizeX * 2.0f - 2.0f, bottomLeft.y);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bottomLeft, bottomRight);

        //draw the ceiling checking sensor
        Vector2 topRight = aabbPos + new Vector3(mAABB.HalfSize.x, mAABB.HalfSize.y, 0.0f) + Vector3.up - Vector3.right;
        var topLeft = new Vector2(topRight.x - mAABB.HalfSize.x * 2.0f + 2.0f, topRight.y);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(topLeft, topRight);

        //draw left wall checking sensor
        bottomLeft = aabbPos - new Vector3(mAABB.HalfSize.x, mAABB.HalfSize.y, 0.0f) - Vector3.right;
        topLeft = bottomLeft;
        topLeft.y += mAABB.HalfSize.y * 2.0f;

        Gizmos.DrawLine(topLeft, bottomLeft);

        bottomRight = aabbPos + new Vector3(mAABB.HalfSize.x, -mAABB.HalfSize.y, 0.0f) + Vector3.right;
        topRight = bottomRight;
        topRight.y += mAABB.HalfSize.y * 2.0f;

        //Gizmos.color = Color.green;
        Gizmos.DrawLine(topRight, bottomRight);
    }

    public LineRenderer lineRenderer;

    [Range(0.01f,10)]
    public float Speed;

    protected void DrawPathLines()
    {
        if (mPath != null && mPath.Count > 0)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetVertexCount(mPath.Count);
            lineRenderer.SetWidth(4.0f, 4.0f);

            for (var i = 0; i < mPath.Count; ++i)
            {
                lineRenderer.SetColors(Color.red, Color.red);
                lineRenderer.SetPosition(i, mMap.transform.position + new Vector3(mPath[i].x, mPath[i].y, -1.0f));
            }
        }
        else
            lineRenderer.enabled = false;
    }

    #endregion

}