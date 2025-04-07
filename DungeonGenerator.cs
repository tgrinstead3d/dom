using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class DungeonGenerator : MonoBehaviour
{
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase exitTile;
    public TileBase rockTile;
    public int width = 20;
    public int height = 20;
    public int minRoomSize = 4;
    public int maxRoomSize = 8;
    public int roomCount = 6;
    public int corridorWidth = 2;
    [Range(0, 100)]
    public int rockDensity = 5;  // Reduced from 10 to 5

    private int[,] map;
    private List<Rect> rooms = new List<Rect>();
    private HashSet<Vector2Int> rockPositions = new HashSet<Vector2Int>();

    void Start()
    {
        GenerateDungeon();
    }
    void GenerateDungeon()
    {
        map = new int[width, height];
        rooms.Clear();
        rockPositions.Clear();

        // Try to place rooms (more attempts to get more rooms)
        int attempts = 0;
        int maxAttempts = roomCount * 5; // Increased attempts for better room placement
        
        while (rooms.Count < roomCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random room dimensions and position
            int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
            int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);
            int roomX = Random.Range(1, width - roomWidth - 1);
            int roomY = Random.Range(1, height - roomHeight - 1);
            
            Rect newRoom = new Rect(roomX, roomY, roomWidth, roomHeight);
            
            // Check if this room overlaps with any other rooms
            bool overlaps = false;
            foreach (Rect room in rooms)
            {
                if (RoomsOverlap(room, newRoom, 1))  // 1 tile spacing between rooms
                {
                    overlaps = true;
                    break;
                }
            }
            
            if (!overlaps)
            {
                // Add the room
                rooms.Add(newRoom);
                
                // Create the room (set floor tiles)
                for (int x = roomX; x < roomX + roomWidth; x++)
                {
                    for (int y = roomY; y < roomY + roomHeight; y++)
                    {
                        map[x, y] = 1; // Floor
                    }
                }
            }
        }

        // Connect rooms with corridors
        ConnectAllRooms();
        
        // Add some additional random corridors for more exploration paths
        AddExtraCorridors();

        // Make a secondary pass to widen corridors a bit
        WidenCorridors();
        
        // Place exit in a valid floor tile at the "end" of the dungeon
        PlaceExitAtEnd();
        
        // Place rocks on the floor
        PlaceRocks();

        // Render the map
        RenderMap();
    }

    bool RoomsOverlap(Rect a, Rect b, int spacing)
    {
        return (a.x - spacing < b.x + b.width + spacing && 
                a.x + a.width + spacing > b.x - spacing &&
                a.y - spacing < b.y + b.height + spacing && 
                a.y + a.height + spacing > b.y - spacing);
    }

    void ConnectAllRooms()
    {
        // Create a minimum spanning tree of corridors to connect all rooms
        if (rooms.Count <= 1) return;
        
        // Simple approach: connect each room to the next one in the list
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Vector2 start = GetRoomCenter(rooms[i]);
            Vector2 end = GetRoomCenter(rooms[i + 1]);
            CreateCorridor(start, end);
        }
        
        // Make a few more connections to create loops
        int extraConnections = rooms.Count / 2 + 1; // Increase extra connections
        for (int i = 0; i < extraConnections; i++)
        {
            int roomA = Random.Range(0, rooms.Count);
            int roomB = Random.Range(0, rooms.Count);
            
            // Avoid connecting a room to itself
            if (roomA != roomB)
            {
                Vector2 start = GetRoomCenter(rooms[roomA]);
                Vector2 end = GetRoomCenter(rooms[roomB]);
                CreateCorridor(start, end);
            }
        }
    }

    Vector2 GetRoomCenter(Rect room)
    {
        return new Vector2(room.x + room.width / 2, room.y + room.height / 2);
    }

    void CreateCorridor(Vector2 start, Vector2 end)
    {
        int x = Mathf.RoundToInt(start.x);
        int y = Mathf.RoundToInt(start.y);
        int targetX = Mathf.RoundToInt(end.x);
        int targetY = Mathf.RoundToInt(end.y);

        // Choose whether to go horizontal first or vertical first
        bool horizontalFirst = Random.value < 0.5f;

        if (horizontalFirst)
        {
            // Go horizontal first
            while (x != targetX)
            {
                x += (targetX > x) ? 1 : -1;
                CreateWiderCorridor(x, y);
            }
            
            // Then go vertical
            while (y != targetY)
            {
                y += (targetY > y) ? 1 : -1;
                CreateWiderCorridor(x, y);
            }
        }
        else
        {
            // Go vertical first
            while (y != targetY)
            {
                y += (targetY > y) ? 1 : -1;
                CreateWiderCorridor(x, y);
            }
            
            // Then go horizontal
            while (x != targetX)
            {
                x += (targetX > x) ? 1 : -1;
                CreateWiderCorridor(x, y);
            }
        }
    }

    void CreateWiderCorridor(int x, int y)
    {
        // Create a corridor of specified width
        for (int dx = 0; dx < corridorWidth; dx++)
        {
            for (int dy = 0; dy < corridorWidth; dy++)
            {
                int nx = x + dx - corridorWidth/2;
                int ny = y + dy - corridorWidth/2;
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    map[nx, ny] = 1; // Floor
                }
            }
        }
    }

    void WidenCorridors()
    {
        // Make a copy of the map to reference while we modify
        int[,] mapCopy = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                mapCopy[x, y] = map[x, y];
            }
        }
        
        // Look for narrow corridors and widen them
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (mapCopy[x, y] == 1) // If this is a floor
                {
                    // Count adjacent floor tiles
                    int adjacentFloors = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            
                            if (mapCopy[x + dx, y + dy] == 1)
                            {
                                adjacentFloors++;
                            }
                        }
                    }
                    
                    // If this has few adjacent floors, it might be a narrow corridor
                    // Let's expand it a bit by making adjacent walls into floors
                    if (adjacentFloors <= 3)
                    {
                        // Add an extra floor adjacent to this one
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                
                                int nx = x + dx;
                                int ny = y + dy;
                                
                                if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1)
                                {
                                    // Convert this wall to a floor with a random chance
                                    if (mapCopy[nx, ny] == 0 && Random.value < 0.5f)
                                    {
                                        map[nx, ny] = 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void AddExtraCorridors()
    {
        // Add some random corridors for more exploration options
        int extraPaths = width / 4; // Increased extra paths
        
        for (int i = 0; i < extraPaths; i++)
        {
            // Find two random floor tiles
            List<Vector2Int> floorTiles = GetFloorTiles();
            
            if (floorTiles.Count >= 2)
            {
                int idxA = Random.Range(0, floorTiles.Count);
                int idxB = Random.Range(0, floorTiles.Count);
                
                if (idxA != idxB && Vector2Int.Distance(floorTiles[idxA], floorTiles[idxB]) > 8)
                {
                    Vector2 startPos = floorTiles[idxA];
                    Vector2 endPos = floorTiles[idxB];
                    CreateCorridor(startPos, endPos);
                }
            }
        }
    }

    List<Vector2Int> GetFloorTiles()
    {
        List<Vector2Int> floorTiles = new List<Vector2Int>();
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y] == 1)
                {
                    floorTiles.Add(new Vector2Int(x, y));
                }
            }
        }
        
        return floorTiles;
    }

    void PlaceExitAtEnd()
    {
        // Find a floor tile that is at the "end" of a path
        if (rooms.Count == 0) return;
        
        // First, identify potential "end" tiles - floor tiles with only 1-2 adjacent floors
        List<Vector2Int> endTiles = new List<Vector2Int>();
        List<float> distancesFromStart = new List<float>();
        
        // Get a starting point (we'll use the center of the first room)
        Vector2 startPoint = GetRoomCenter(rooms[0]);
        
        // Find floor tiles that have few adjacent floors (dead ends or near dead ends)
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (map[x, y] == 1) // If this is a floor
                {
                    // Count adjacent floor tiles
                    int adjacentFloors = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            
                            int nx = x + dx;
                            int ny = y + dy;
                            
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height && map[nx, ny] == 1)
                            {
                                adjacentFloors++;
                            }
                        }
                    }
                    
                    // If this tile has 1-2 adjacent floors, it's a possible end
                    if (adjacentFloors <= 2)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        endTiles.Add(pos);
                        
                        // Calculate distance from start
                        float dist = Vector2.Distance(startPoint, pos);
                        distancesFromStart.Add(dist);
                    }
                }
            }
        }
        
        // If we found possible end tiles, choose the one furthest from start
        if (endTiles.Count > 0)
        {
            // Find the furthest tile
            int furthestIndex = 0;
            float maxDistance = 0;
            
            for (int i = 0; i < distancesFromStart.Count; i++)
            {
                if (distancesFromStart[i] > maxDistance)
                {
                    maxDistance = distancesFromStart[i];
                    furthestIndex = i;
                }
            }
            
            // Check if this tile is inside a room
            Vector2Int exitPos = endTiles[furthestIndex];
            bool isInRoom = false;
            
            foreach (Rect room in rooms)
            {
                if (exitPos.x > room.x && exitPos.x < room.x + room.width - 1 &&
                    exitPos.y > room.y && exitPos.y < room.y + room.height - 1)
                {
                    isInRoom = true;
                    break;
                }
            }
            
            // If it's in a room, try to find a non-room end tile
            if (isInRoom && endTiles.Count > 1)
            {
                // Sort by distance (descending)
                for (int i = 0; i < endTiles.Count - 1; i++)
                {
                    for (int j = i + 1; j < endTiles.Count; j++)
                    {
                        if (distancesFromStart[j] > distancesFromStart[i])
                        {
                            // Swap distances
                            float tempDist = distancesFromStart[i];
                            distancesFromStart[i] = distancesFromStart[j];
                            distancesFromStart[j] = tempDist;
                            
                            // Swap tiles
                            Vector2Int tempTile = endTiles[i];
                            endTiles[i] = endTiles[j];
                            endTiles[j] = tempTile;
                        }
                    }
                }
                
                // Try to find the first non-room end tile
                for (int i = 0; i < endTiles.Count; i++)
                {
                    exitPos = endTiles[i];
                    isInRoom = false;
                    
                    foreach (Rect room in rooms)
                    {
                        if (exitPos.x > room.x && exitPos.x < room.x + room.width - 1 &&
                            exitPos.y > room.y && exitPos.y < room.y + room.height - 1)
                        {
                            isInRoom = true;
                            break;
                        }
                    }
                    
                    if (!isInRoom)
                    {
                        break; // Found a suitable non-room end tile
                    }
                }
            }
            
            // Place the exit
            map[exitPos.x, exitPos.y] = 2; // Mark as exit
        }
        else
        {
            // Fallback: if no suitable end tiles, just place the exit at the furthest floor tile
            float maxDistance = 0;
            Vector2Int exitPos = Vector2Int.zero;
            
            List<Vector2Int> floorTiles = GetFloorTiles();
            
            foreach (Vector2Int pos in floorTiles)
            {
                float dist = Vector2.Distance(startPoint, pos);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    exitPos = pos;
                }
            }
            
            if (maxDistance > 0)
            {
                map[exitPos.x, exitPos.y] = 2; // Exit
            }
            else
            {
                // Last resort fallback
                if (floorTiles.Count > 0)
                {
                    Vector2Int randomPos = floorTiles[Random.Range(0, floorTiles.Count)];
                    map[randomPos.x, randomPos.y] = 2; // Exit
                }
            }
        }
    }

    void PlaceRocks()
    {
        // Get all floor tiles
        List<Vector2Int> floorTiles = GetFloorTiles();
        
        // Randomly place rocks on floor tiles based on rock density
        foreach (Vector2Int floorPos in floorTiles)
        {
            // Skip tiles where the exit is
            if (map[floorPos.x, floorPos.y] == 2) continue;
            
            // Random chance to place a rock based on density
            if (Random.Range(0, 100) < rockDensity)
            {
                // Check to ensure we don't block any paths
                if (IsValidRockPosition(floorPos))
                {
                    rockPositions.Add(floorPos); // Store the rock position separately
                    // The map array still shows this as a floor (1), since rocks are "on top" of floors
                }
            }
        }
        
        // Make sure we don't have too many rocks in a single room
        foreach (Rect room in rooms)
        {
            int rocksInRoom = 0;
            List<Vector2Int> roomRocks = new List<Vector2Int>();
            
            // Count rocks in this room
            foreach (Vector2Int rockPos in rockPositions)
            {
                if (rockPos.x >= room.x && rockPos.x < room.x + room.width &&
                    rockPos.y >= room.y && rockPos.y < room.y + room.height)
                {
                    rocksInRoom++;
                    roomRocks.Add(rockPos);
                }
            }
            
            // If too many rocks, remove some
            int maxRocksPerRoom = Mathf.Max(1, (int)(room.width * room.height * 0.08f)); // 8% of room area
            if (rocksInRoom > maxRocksPerRoom)
            {
                // Shuffle the list of rocks
                for (int i = 0; i < roomRocks.Count; i++)
                {
                    int swapIndex = Random.Range(i, roomRocks.Count);
                    Vector2Int temp = roomRocks[i];
                    roomRocks[i] = roomRocks[swapIndex];
                    roomRocks[swapIndex] = temp;
                }
                
                // Remove excess rocks
                for (int i = 0; i < rocksInRoom - maxRocksPerRoom; i++)
                {
                    if (i < roomRocks.Count)
                    {
                        rockPositions.Remove(roomRocks[i]);
                    }
                }
            }
        }
    }

    bool IsValidRockPosition(Vector2Int pos)
    {
        // Make sure placing a rock here doesn't completely block a path
        
        int adjacentFloors = 0;
        bool isInCorridor = true;
        
        // Check if this is in a room (less strict rules for rooms)
        foreach (Rect room in rooms)
        {
            if (pos.x > room.x && pos.x < room.x + room.width - 1 &&
                pos.y > room.y && pos.y < room.y + room.height - 1)
            {
                isInCorridor = false;
                break;
            }
        }
        
        // Count adjacent floor tiles
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip the center position
                
                int nx = pos.x + dx;
                int ny = pos.y + dy;
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && map[nx, ny] == 1)
                {
                    adjacentFloors++;
                }
            }
        }
        
        // In corridors we need to be more careful about placement
        if (isInCorridor)
        {
            // Ensure at least 3 adjacent floors in corridors (don't block the path)
            return adjacentFloors >= 3;
        }
        else
        {
            // In rooms, just make sure there are at least 2 adjacent floors
            return adjacentFloors >= 2;
        }
    }

    void RenderMap()
    {
        tilemap.ClearAllTiles();
        
        // First, render all the basic tiles (floor, walls)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                
                switch (map[x, y])
                {
                    case 0: // Wall
                        tilemap.SetTile(pos, wallTile);
                        break;
                    case 1: // Floor
                    case 2: // Exit is on a floor
                        tilemap.SetTile(pos, floorTile);
                        break;
                }
            }
        }
        
        // Then render objects on top (exit, rocks)
        // First the exit
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y] == 2) // Exit
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    // Using SetTile will replace the floor with the exit
                    // Instead, we can combine tiles if your tilemap supports it
                    // Or just assume the exit tile has transparent parts showing floor underneath
                    tilemap.SetTile(pos, exitTile);
                }
            }
        }
        
        // Then render rocks
        foreach (Vector2Int rockPos in rockPositions)
        {
            Vector3Int pos = new Vector3Int(rockPos.x, rockPos.y, 0);
            
            // Assuming rock tiles have transparent backgrounds
            tilemap.SetTile(pos, rockTile);
        }
    }
}