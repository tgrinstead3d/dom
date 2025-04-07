using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tilemap")] public Tilemap tilemap;

    [Header("Tiles")] public TileBase floorTile;
    public TileBase wallTile;
    public TileBase spawnTile;
    public TileBase exitTile;
    public TileBase rockTile;
    public TileBase chestTile;

    [Header("Generation Settings")] [Range(3, 5)]
    public int roomCount = 3;

    public int roomSize = 10;
    [Range(1, 4)] public int deadEndCount = 2;
    [Range(3, 7)] public int rockCount = 3;
    [Range(0, 3)] public int chestCount = 1;

    // Debug visualization
    public bool showDeadEnds = true;
    public Color deadEndColor = Color.red;

    // Internal representation of the dungeon
    private enum CellType
    {
        Empty,
        Floor,
        Wall,
        Spawn,
        Exit,
        Rock,
        Chest,
        DeadEnd
    }

    private CellType[,] dungeonGrid;
    private int dungeonWidth;
    private int dungeonHeight;
    private Vector2Int spawnPosition;
    private Vector2Int exitPosition;
    private List<Rect> rooms = new List<Rect>();
    private List<Vector2Int> corridors = new List<Vector2Int>();
    private List<List<Vector2Int>> deadEnds = new List<List<Vector2Int>>();

    void Start()
    {
        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        // Clear existing tilemap
        tilemap.ClearAllTiles();

        // Reset containers
        rooms.Clear();
        corridors.Clear();
        deadEnds.Clear();

        // Determine dungeon size based on room count and size
        dungeonWidth = roomCount * (roomSize + 10);
        dungeonHeight = roomCount * (roomSize + 10);

        // Initialize dungeon grid
        dungeonGrid = new CellType[dungeonWidth, dungeonHeight];

        // Generate rooms
        GenerateRooms();

        // Connect rooms with corridors
        ConnectRooms();

        // Add dead ends
        AddDeadEnds();

        // Place spawn and exit
        PlaceSpawnAndExit();

        // Place obstacles and rewards
        PlaceRocks();
        PlaceChests();

        // Create walls around floors
        CreateWalls();

        // Ensure all floor tiles have wall borders where needed
        EnsureWallBorders();

        // Render the dungeon
        RenderDungeon();

        // Debug visualization
        if (showDeadEnds)
        {
            VisualizeDeadEnds();
        }
    }

    private void GenerateRooms()
    {
        int actualRoomCount = Random.Range(3, roomCount + 1);

        for (int i = 0; i < actualRoomCount; i++)
        {
            bool roomPlaced = false;
            int attempts = 0;

            while (!roomPlaced && attempts < 50)
            {
                // Random room width and height around roomSize
                int roomWidth = Random.Range(roomSize - 2, roomSize + 2);
                int roomHeight = Random.Range(roomSize - 2, roomSize + 2);

                // Random position, with safety margin from edge
                int roomX = Random.Range(5, dungeonWidth - roomWidth - 5);
                int roomY = Random.Range(5, dungeonHeight - roomHeight - 5);

                Rect newRoom = new Rect(roomX, roomY, roomWidth, roomHeight);

                // Check if room overlaps with existing rooms
                bool overlaps = false;
                foreach (Rect room in rooms)
                {
                    // Add padding around rooms
                    Rect paddedRoom = new Rect(
                        room.x - 3, room.y - 3,
                        room.width + 6, room.height + 6
                    );

                    if (newRoom.Overlaps(paddedRoom))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    // Place room
                    rooms.Add(newRoom);

                    // Fill room with floor tiles
                    for (int x = (int)newRoom.x; x < (int)(newRoom.x + newRoom.width); x++)
                    {
                        for (int y = (int)newRoom.y; y < (int)(newRoom.y + newRoom.height); y++)
                        {
                            dungeonGrid[x, y] = CellType.Floor;
                        }
                    }

                    roomPlaced = true;
                }

                attempts++;
            }
        }
    }

    private void ConnectRooms()
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            // Find center points of rooms
            Vector2Int currentRoom = new Vector2Int(
                (int)(rooms[i].x + rooms[i].width / 2),
                (int)(rooms[i].y + rooms[i].height / 2)
            );

            Vector2Int nextRoom = new Vector2Int(
                (int)(rooms[i + 1].x + rooms[i + 1].width / 2),
                (int)(rooms[i + 1].y + rooms[i + 1].height / 2)
            );

            // Randomly decide whether to go horizontal or vertical first
            if (Random.value < 0.5f)
            {
                CreateHorizontalCorridor(currentRoom.x, nextRoom.x, currentRoom.y);
                CreateVerticalCorridor(currentRoom.y, nextRoom.y, nextRoom.x);
            }
            else
            {
                CreateVerticalCorridor(currentRoom.y, nextRoom.y, currentRoom.x);
                CreateHorizontalCorridor(currentRoom.x, nextRoom.x, nextRoom.y);
            }
        }
    }

    private void CreateHorizontalCorridor(int startX, int endX, int y)
    {
        int corridorWidth = Random.Range(2, 5); // Corridor width between 2-4 cells
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);

        for (int x = minX; x <= maxX; x++)
        {
            for (int i = 0; i < corridorWidth; i++)
            {
                int yPos = y - corridorWidth / 2 + i;

                if (IsInBounds(x, yPos) && dungeonGrid[x, yPos] == CellType.Empty)
                {
                    dungeonGrid[x, yPos] = CellType.Floor;
                    corridors.Add(new Vector2Int(x, yPos));
                }
            }
        }
    }

    private void CreateVerticalCorridor(int startY, int endY, int x)
    {
        int corridorWidth = Random.Range(2, 5); // Corridor width between 2-4 cells
        int minY = Mathf.Min(startY, endY);
        int maxY = Mathf.Max(startY, endY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int i = 0; i < corridorWidth; i++)
            {
                int xPos = x - corridorWidth / 2 + i;

                if (IsInBounds(xPos, y) && dungeonGrid[xPos, y] == CellType.Empty)
                {
                    dungeonGrid[xPos, y] = CellType.Floor;
                    corridors.Add(new Vector2Int(xPos, y));
                }
            }
        }
    }

    private void AddDeadEnds()
    {
        int deadEndsToAdd = Mathf.Max(1, deadEndCount); // Ensure at least one dead end
        int attemptsPerDeadEnd = 50; // Max attempts per dead end to avoid infinite loops

        for (int i = 0; i < deadEndsToAdd; i++)
        {
            CreateDeadEndCorridor();
        }
    }

    private void CreateDeadEndCorridor()
    {
        // Find a random floor tile to start from - preferring corridor tiles
        List<Vector2Int> possibleStartPoints = new List<Vector2Int>();

        // First try to use corridor tiles
        foreach (Vector2Int corridor in corridors)
        {
            if (IsGoodDeadEndStart(corridor))
            {
                possibleStartPoints.Add(corridor);
            }
        }

        // If no good corridor cells, look at any floor tile
        if (possibleStartPoints.Count == 0)
        {
            for (int x = 0; x < dungeonWidth; x++)
            {
                for (int y = 0; y < dungeonHeight; y++)
                {
                    if (dungeonGrid[x, y] == CellType.Floor && IsGoodDeadEndStart(new Vector2Int(x, y)))
                    {
                        possibleStartPoints.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        // If no start points found, we can't create a dead end
        if (possibleStartPoints.Count == 0)
        {
            return;
        }

        // Choose a random start point
        Vector2Int startPoint = possibleStartPoints[Random.Range(0, possibleStartPoints.Count)];

        // Find valid directions (toward empty space)
        List<Vector2Int> validDirections = new List<Vector2Int>();
        Vector2Int[] directions = new Vector2Int[]
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (Vector2Int dir in directions)
        {
            if (CanGrowDeadEndInDirection(startPoint, dir))
            {
                validDirections.Add(dir);
            }
        }

        if (validDirections.Count == 0)
        {
            return; // No valid direction to grow
        }

        // Choose a random direction and create the dead end
        Vector2Int direction = validDirections[Random.Range(0, validDirections.Count)];

        // Create a dead end corridor with random length between 8-12 cells
        int corridorLength = Random.Range(8, 13);
        int corridorWidth = Random.Range(2, 4); // Width between 2-3 cells

        List<Vector2Int> deadEndCells = new List<Vector2Int>();
        bool success = false;

        // Try to create the dead end
        for (int dist = 1; dist <= corridorLength; dist++)
        {
            List<Vector2Int> cellsAtCurrentDistance = new List<Vector2Int>();

            // The main line in the direction
            Vector2Int basePos = startPoint + (direction * dist);

            if (!IsInBounds(basePos.x, basePos.y))
            {
                break; // Out of bounds, stop growing
            }

            // For width, add cells perpendicular to main direction
            Vector2Int perpendicular;

            if (direction.x != 0) // Horizontal corridor
            {
                perpendicular = new Vector2Int(0, 1);
            }
            else // Vertical corridor
            {
                perpendicular = new Vector2Int(1, 0);
            }

            bool canContinue = true;

            // Place the corridor cells for current distance
            for (int w = -corridorWidth / 2; w <= corridorWidth / 2; w++)
            {
                Vector2Int currentPos = basePos + (perpendicular * w);

                if (!IsInBounds(currentPos.x, currentPos.y))
                {
                    continue; // Skip this position if out of bounds
                }

                // If we would hit a floor cell that's not at the start, cancel
                if (dist > 1 && dungeonGrid[currentPos.x, currentPos.y] == CellType.Floor)
                {
                    canContinue = false;
                    break;
                }

                // Otherwise, mark this as a position we want to add
                cellsAtCurrentDistance.Add(currentPos);
            }

            // If we can't continue, stop the corridor growth
            if (!canContinue)
            {
                break;
            }

            // Add all cells at this distance to our dead end
            foreach (Vector2Int cell in cellsAtCurrentDistance)
            {
                deadEndCells.Add(cell);
            }

            // If we've reached our desired length, mark as success
            if (dist == corridorLength)
            {
                success = true;
            }
        }

        // Only set cells if we created a valid dead end
        if (deadEndCells.Count > 2)
        {
            deadEnds.Add(deadEndCells);

            foreach (Vector2Int cell in deadEndCells)
            {
                dungeonGrid[cell.x, cell.y] = CellType.DeadEnd; // Mark as dead end (will be rendered as floor)
            }
        }
    }

    // Check if a position is a good starting point for a dead end
    private bool IsGoodDeadEndStart(Vector2Int pos)
    {
        // Must be a floor tile
        if (dungeonGrid[pos.x, pos.y] != CellType.Floor)
        {
            return false;
        }

        // Check if it has at least one direction with empty space
        bool hasEmptyDirection = false;
        Vector2Int[] directions = new Vector2Int[]
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (Vector2Int dir in directions)
        {
            if (CanGrowDeadEndInDirection(pos, dir))
            {
                hasEmptyDirection = true;
                break;
            }
        }

        return hasEmptyDirection;
    }

    // Check if we can grow a dead end from pos in direction dir
    private bool CanGrowDeadEndInDirection(Vector2Int pos, Vector2Int dir)
    {
        // Check if the adjacent cell in this direction is empty
        Vector2Int nextPos = pos + dir;

        if (!IsInBounds(nextPos.x, nextPos.y) || dungeonGrid[nextPos.x, nextPos.y] != CellType.Empty)
        {
            return false;
        }

        // Check if we have space to grow the corridor a few cells in this direction
        int minGrowth = 5; // Require at least 5 cells of growth space

        for (int dist = 1; dist <= minGrowth; dist++)
        {
            Vector2Int checkPos = pos + (dir * dist);

            if (!IsInBounds(checkPos.x, checkPos.y))
            {
                return false; // Not enough room to grow
            }

            // The main corridor path needs to be empty
            if (dungeonGrid[checkPos.x, checkPos.y] != CellType.Empty)
            {
                return false;
            }

            // Also check width space (1 tile on each side for corridor width)
            Vector2Int perpendicular = (dir.x != 0) ? new Vector2Int(0, 1) : new Vector2Int(1, 0);

            for (int w = -1; w <= 1; w++)
            {
                if (w == 0) continue; // Already checked the main position

                Vector2Int widthPos = checkPos + (perpendicular * w);

                if (!IsInBounds(widthPos.x, widthPos.y))
                {
                    continue; // Edge of map is fine
                }

                // If there's a floor tile along the width, that's not good
                if (dungeonGrid[widthPos.x, widthPos.y] == CellType.Floor)
                {
                    return false;
                }
            }
        }

        return true; // Enough space to grow in this direction
    }

    private void VisualizeDeadEnds()
    {
        foreach (List<Vector2Int> deadEnd in deadEnds)
        {
            foreach (Vector2Int cell in deadEnd)
            {
                // Visual debugging in the scene view
                Debug.DrawLine(
                    new Vector3(cell.x, cell.y, 0),
                    new Vector3(cell.x + 1, cell.y + 1, 0),
                    deadEndColor,
                    100f
                );
                Debug.DrawLine(
                    new Vector3(cell.x + 1, cell.y, 0),
                    new Vector3(cell.x, cell.y + 1, 0),
                    deadEndColor,
                    100f
                );
            }
        }
    }

    private void PlaceSpawnAndExit()
    {
        // Find possible spawn points (in corridors, not in rooms)
        List<Vector2Int> possibleSpawnPoints = new List<Vector2Int>();

        foreach (Vector2Int corridorCell in corridors)
        {
            if (!IsInAnyRoom(corridorCell))
            {
                possibleSpawnPoints.Add(corridorCell);
            }
        }

        // Place spawn point
        if (possibleSpawnPoints.Count > 0)
        {
            int spawnIndex = Random.Range(0, possibleSpawnPoints.Count);
            spawnPosition = possibleSpawnPoints[spawnIndex];
            dungeonGrid[spawnPosition.x, spawnPosition.y] = CellType.Spawn;

            // Find the farthest point from spawn for exit
            Vector2Int farthestPoint = FindFarthestPoint(spawnPosition);
            exitPosition = farthestPoint;
            dungeonGrid[exitPosition.x, exitPosition.y] = CellType.Exit;
        }
        else
        {
            // Fallback: place spawn and exit in opposite corners of the dungeon
            spawnPosition = new Vector2Int(5, 5);
            exitPosition = new Vector2Int(dungeonWidth - 5, dungeonHeight - 5);

            // Make sure these positions are floor tiles
            if (IsInBounds(spawnPosition.x, spawnPosition.y))
            {
                dungeonGrid[spawnPosition.x, spawnPosition.y] = CellType.Spawn;
            }

            if (IsInBounds(exitPosition.x, exitPosition.y))
            {
                dungeonGrid[exitPosition.x, exitPosition.y] = CellType.Exit;
            }
        }
    }

    private Vector2Int FindFarthestPoint(Vector2Int from)
    {
        Vector2Int farthestPoint = from;
        int maxDistance = 0;

        // Check all floor tiles
        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                if (dungeonGrid[x, y] == CellType.Floor || dungeonGrid[x, y] == CellType.DeadEnd)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    int distance = Mathf.Abs(point.x - from.x) + Mathf.Abs(point.y - from.y);

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthestPoint = point;
                    }
                }
            }
        }

        return farthestPoint;
    }

    private void PlaceRocks()
    {
        int rocksToPlace = Random.Range(3, rockCount + 1);
        int rockPlaced = 0;

        while (rockPlaced < rocksToPlace && rockPlaced < 50) // Avoid infinite loops
        {
            int x = Random.Range(0, dungeonWidth);
            int y = Random.Range(0, dungeonHeight);

            if (dungeonGrid[x, y] == CellType.Floor)
            {
                // Make sure there are no rocks adjacent
                bool hasAdjacentRock = false;

                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        if (IsInBounds(nx, ny) && dungeonGrid[nx, ny] == CellType.Rock)
                        {
                            hasAdjacentRock = true;
                            break;
                        }
                    }
                }

                // Check if there are at least 2 adjacent floor tiles
                int adjacentFloorCount = 0;
                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        if ((nx != x || ny != y) && IsInBounds(nx, ny) &&
                            (dungeonGrid[nx, ny] == CellType.Floor || dungeonGrid[nx, ny] == CellType.DeadEnd))
                        {
                            adjacentFloorCount++;
                        }
                    }
                }

                if (!hasAdjacentRock && adjacentFloorCount >= 2)
                {
                    dungeonGrid[x, y] = CellType.Rock;
                    rockPlaced++;
                }
            }
        }
    }

    private void PlaceChests()
    {
        int chestsToPlace = Random.Range(0, chestCount + 1);
        int chestsPlaced = 0;

        // Try to place some chests at the end of dead-end corridors
        foreach (List<Vector2Int> deadEnd in deadEnds)
        {
            if (chestsPlaced >= chestsToPlace) break;

            // Get the last cell of the dead end
            if (deadEnd.Count > 0)
            {
                Vector2Int cell = deadEnd[deadEnd.Count - 1];
                dungeonGrid[cell.x, cell.y] = CellType.Chest;
                chestsPlaced++;
            }
        }

        // Place remaining chests randomly
        while (chestsPlaced < chestsToPlace && chestsPlaced < 50) // Avoid infinite loops
        {
            int x = Random.Range(0, dungeonWidth);
            int y = Random.Range(0, dungeonHeight);

            if (dungeonGrid[x, y] == CellType.Floor || dungeonGrid[x, y] == CellType.DeadEnd)
            {
                // Check if there are at least 2 adjacent floor tiles
                int adjacentFloorCount = 0;
                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        if ((nx != x || ny != y) && IsInBounds(nx, ny) &&
                            (dungeonGrid[nx, ny] == CellType.Floor || dungeonGrid[nx, ny] == CellType.DeadEnd))
                        {
                            adjacentFloorCount++;
                        }
                    }
                }

                if (adjacentFloorCount >= 2)
                {
                    dungeonGrid[x, y] = CellType.Chest;
                    chestsPlaced++;
                }
            }
        }
    }

    private void CreateWalls()
    {
        CellType[,] tempGrid = new CellType[dungeonWidth, dungeonHeight];

        // Copy current grid
        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                tempGrid[x, y] = dungeonGrid[x, y];
            }
        }

        // Add walls around floor tiles
        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                if (dungeonGrid[x, y] == CellType.Empty)
                {
                    // Check if any neighbor is a floor or special tile
                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        for (int ny = y - 1; ny <= y + 1; ny++)
                        {
                            if (IsInBounds(nx, ny) && IsFloorLikeCell(nx, ny))
                            {
                                tempGrid[x, y] = CellType.Wall;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Update grid
        dungeonGrid = tempGrid;
    }

    // Ensure all floor tiles that are at the edge of the dungeon have wall tiles around them
    private void EnsureWallBorders()
    {
        // First pass: find floor tiles that are at the edge
        List<Vector2Int> edgeFloors = new List<Vector2Int>();

        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                if (IsFloorLikeCell(x, y))
                {
                    // Check if this floor is at the edge (adjacent to out of bounds)
                    if (x == 0 || y == 0 || x == dungeonWidth - 1 || y == dungeonHeight - 1)
                    {
                        edgeFloors.Add(new Vector2Int(x, y));
                    }
                    else
                    {
                        // Check in the four cardinal directions
                        Vector2Int[] directions = new Vector2Int[]
                        {
                            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
                        };

                        foreach (Vector2Int dir in directions)
                        {
                            Vector2Int checkPos = new Vector2Int(x, y) + dir;

                            if (!IsInBounds(checkPos.x, checkPos.y) ||
                                (dungeonGrid[checkPos.x, checkPos.y] == CellType.Empty &&
                                 !HasWallAdjacent(checkPos.x, checkPos.y)))
                            {
                                edgeFloors.Add(new Vector2Int(x, y));
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Second pass: create walls around edge floors
        foreach (Vector2Int edgeFloor in edgeFloors)
        {
            // Check in all 8 directions
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip the cell itself

                    int nx = edgeFloor.x + dx;
                    int ny = edgeFloor.y + dy;

                    // If out of bounds or empty, make sure there's a wall wherever possible
                    if (!IsInBounds(nx, ny))
                    {
                        // If at edge, can't place a wall outside the grid
                        continue;
                    }

                    if (dungeonGrid[nx, ny] == CellType.Empty)
                    {
                        dungeonGrid[nx, ny] = CellType.Wall;
                    }
                }
            }
        }
    }

    private bool HasWallAdjacent(int x, int y)
    {
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if ((nx != x || ny != y) && IsInBounds(nx, ny) && dungeonGrid[nx, ny] == CellType.Wall)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsFloorLikeCell(int x, int y)
    {
        return dungeonGrid[x, y] == CellType.Floor ||
               dungeonGrid[x, y] == CellType.DeadEnd ||
               dungeonGrid[x, y] == CellType.Spawn ||
               dungeonGrid[x, y] == CellType.Exit ||
               dungeonGrid[x, y] == CellType.Chest;
    }

    private void RenderDungeon()
    {
        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);

                switch (dungeonGrid[x, y])
                {
                    case CellType.Floor:
                    case CellType.DeadEnd: // Dead ends use floor tiles too
                        tilemap.SetTile(position, floorTile);
                        break;

                    case CellType.Wall:
                        tilemap.SetTile(position, wallTile);
                        break;

                    case CellType.Spawn:
                        tilemap.SetTile(position, spawnTile);
                        break;

                    case CellType.Exit:
                        tilemap.SetTile(position, exitTile);
                        break;

                    case CellType.Rock:
                        tilemap.SetTile(position, rockTile);
                        break;

                    case CellType.Chest:
                        tilemap.SetTile(position, chestTile);
                        break;
                }
            }
        }
    }

    // Utility functions
    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < dungeonWidth && y >= 0 && y < dungeonHeight;
    }

    private bool IsInAnyRoom(Vector2Int position)
    {
        foreach (Rect room in rooms)
        {
            if (position.x >= room.x && position.x < room.x + room.width &&
                position.y >= room.y && position.y < room.y + room.height)
            {
                return true;
            }
        }

        return false;
    }
}