using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Level
{
    /// <summary>
    /// Pure-data description of a tile-based dungeon, parsed from an ASCII map (see <c>Assets/Level/Catacombs.txt</c>).
    /// Contains no UnityEngine objects besides vectors, so parsing and validation are unit-testable.
    ///
    /// Legend: <c>#</c> wall · <c>.</c> floor · <c>S</c> player start · <c>R</c> relic shrine · <c>A</c> altar centre ·
    /// <c>E</c> exit portal · <c>M</c> monster spawn · <c>T</c> ragdoll trap · <c>P</c> pillar.
    /// Lines that are empty or start with <c>;</c> are ignored. The first map row is the NORTH edge.
    /// </summary>
    public sealed class DungeonGrid
    {
        public const char Wall = '#';
        public const char Floor = '.';
        public const char Start = 'S';
        public const char Relic = 'R';
        public const char Altar = 'A';
        public const char Exit = 'E';
        public const char Monster = 'M';
        public const char Trap = 'T';
        public const char Pillar = 'P';

        private const string ValidChars = "#.SRAEMTP";
        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        private readonly char[,] _cells;

        /// <summary>Number of columns.</summary>
        public int Width { get; }

        /// <summary>Number of rows.</summary>
        public int Height { get; }

        private DungeonGrid(char[,] cells, int width, int height)
        {
            _cells = cells;
            Width = width;
            Height = height;
        }

        /// <summary>Cell character at column <paramref name="x"/>, row <paramref name="y"/> (row 0 is the north edge).</summary>
        public char this[int x, int y] => _cells[x, y];

        /// <summary>True when the position is inside the map.</summary>
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>True for every cell a player can stand on (everything except walls).</summary>
        public bool IsWalkable(int x, int y) => InBounds(x, y) && _cells[x, y] != Wall;

        /// <summary>Parses an ASCII map. Throws <see cref="FormatException"/> for ragged rows or unknown characters.</summary>
        public static DungeonGrid Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var rows = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';') continue;
                rows.Add(line);
            }

            if (rows.Count == 0) throw new FormatException("The dungeon map is empty.");

            int width = rows[0].Length;
            var cells = new char[width, rows.Count];
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new FormatException($"Row {y + 1} has {rows[y].Length} columns, expected {width}.");
                }

                for (int x = 0; x < width; x++)
                {
                    char c = rows[y][x];
                    if (ValidChars.IndexOf(c) < 0)
                    {
                        throw new FormatException($"Unknown map character '{c}' at column {x + 1}, row {y + 1}.");
                    }
                    cells[x, y] = c;
                }
            }

            return new DungeonGrid(cells, width, rows.Count);
        }

        /// <summary>Every cell holding <paramref name="symbol"/>, in row-major order.</summary>
        public List<Vector2Int> Find(char symbol)
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, y] == symbol) result.Add(new Vector2Int(x, y));
            }
            return result;
        }

        /// <summary>
        /// World position of a cell centre. The map is centred on the origin, columns grow along +X and the
        /// first row is the north edge (+Z).
        /// </summary>
        public Vector3 CellToWorld(int x, int y, float cellSize)
        {
            return new Vector3((x - (Width - 1) * 0.5f) * cellSize, 0f, ((Height - 1) * 0.5f - y) * cellSize);
        }

        /// <summary>
        /// Splits the wall cells of a row into horizontal runs so the builder creates a few long boxes
        /// instead of one box per wall cell. Each run is (first column, length).
        /// </summary>
        public List<Vector2Int> WallRuns(int y)
        {
            var runs = new List<Vector2Int>();
            int x = 0;
            while (x < Width)
            {
                if (_cells[x, y] != Wall) { x++; continue; }
                int start = x;
                while (x < Width && _cells[x, y] == Wall) x++;
                runs.Add(new Vector2Int(start, x - start));
            }
            return runs;
        }

        /// <summary>
        /// Checks that the map can host a full match: one start, one altar, three relic shrines, an exit, a monster,
        /// a sealed border and every point of interest reachable from the start on foot.
        /// Returns a list of human-readable problems (empty when the map is valid).
        /// </summary>
        public List<string> Validate(int requiredRelics = 3)
        {
            var errors = new List<string>();

            ExpectCount(errors, Start, 1, exact: true, "player start");
            ExpectCount(errors, Altar, 1, exact: true, "altar");
            ExpectCount(errors, Relic, requiredRelics, exact: true, "relic shrine");
            ExpectCount(errors, Exit, 1, exact: false, "exit portal");
            ExpectCount(errors, Monster, 1, exact: false, "monster spawn");

            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, 0] != Wall || _cells[x, Height - 1] != Wall) { errors.Add("The map border must be sealed with walls."); break; }
            }
            for (int y = 0; y < Height; y++)
            {
                if (_cells[0, y] != Wall || _cells[Width - 1, y] != Wall) { errors.Add("The map border must be sealed with walls."); break; }
            }

            List<Vector2Int> starts = Find(Start);
            if (starts.Count == 1)
            {
                bool[,] reachable = FloodFill(starts[0]);
                foreach (char symbol in "RAEMT")
                {
                    foreach (Vector2Int cell in Find(symbol))
                    {
                        if (!reachable[cell.x, cell.y])
                        {
                            errors.Add($"'{symbol}' at column {cell.x + 1}, row {cell.y + 1} cannot be reached from the player start.");
                        }
                    }
                }
            }

            return errors;
        }

        private void ExpectCount(List<string> errors, char symbol, int expected, bool exact, string label)
        {
            int count = Find(symbol).Count;
            if (exact && count != expected) errors.Add($"Expected exactly {expected} {label}(s), found {count}.");
            else if (!exact && count < expected) errors.Add($"Expected at least {expected} {label}, found {count}.");
        }

        private bool[,] FloodFill(Vector2Int origin)
        {
            var visited = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            visited[origin.x, origin.y] = true;
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                foreach (Vector2Int step in Neighbours)
                {
                    int nx = cell.x + step.x, ny = cell.y + step.y;
                    if (!IsWalkable(nx, ny) || visited[nx, ny]) continue;
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return visited;
        }
    }
}
