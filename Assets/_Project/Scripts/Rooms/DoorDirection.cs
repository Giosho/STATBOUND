using UnityEngine;

public enum DoorDirection
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class DoorDirectionExtensions
{
    public static DoorDirection Opposite(this DoorDirection d)
    {
        return (DoorDirection)(((int)d + 2) % 4);
    }

    public static Vector2Int ToGrid(this DoorDirection d)
    {
        switch (d)
        {
            case DoorDirection.North: return Vector2Int.up;
            case DoorDirection.East: return Vector2Int.right;
            case DoorDirection.South: return Vector2Int.down;
            default: return Vector2Int.left;
        }
    }
}
