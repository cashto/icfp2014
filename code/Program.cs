using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sim
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("[");

            var map = new Map(
                args[0],
                args[1],
                args.Skip(2).ToList());

            map.Pacman.CallMain();

            var tick = 1;
            var endOfLives = 127 * map.Width * map.Height * 16;
            //endOfLives = 30000;
            for (; tick < endOfLives; ++tick)
            {
                map.Pacman.Tick(tick);
                foreach (var ghost in map.Ghosts)
                {
                    ghost.Tick(tick);
                }
                map.Tick(tick);

                if (map.IsGameOver)
                {
                    break;
                }
            }

            Console.WriteLine("   {{ \"tick\":{0}, \"event\":\"end\", \"data\":{1} }}",
                tick,
                map.Points * (map.Pacman.Lives + 1));
            Console.WriteLine("]");
        }

        public static IEnumerable<List<string>> TokenizeStream(StreamReader reader, char splitChar)
        {
            var regex = new Regex(@"^\s*(\w+)\s*(.*)$");
            var splitCharArray = new char[] { splitChar };

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine().Trim();
                var item = new List<string>();
                
                var semicolonIdx = line.IndexOf(';');
                if (semicolonIdx >= 0)
                {
                    line = line.Remove(semicolonIdx);
                }

                if (line == "")
                {
                    continue;
                }

                var items = regex.Match(line);
                item.Add(items.Groups[1].Value);
                item.AddRange(items.Groups[2].Value.Split(splitCharArray, StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()));

                yield return item;
            }
        }

        public static Direction OppositeDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                case Direction.Right: return Direction.Left;
                default: throw new Exception();
            }
        }

        public static void LogDebug(int tick, string format, params object[] items)
        {
            Console.WriteLine("   {{ \"tick\":{0}, \"event\":\"debug\", \"data\":\"{1}\" }},",
                tick, 
                string.Format(format, items));
        }

        public static void LogMove(int tick, string item, Point position)
        {
            Console.WriteLine("   {{ \"tick\":{0}, \"event\":\"move\", \"data\":\"{1}\", \"x\":{2}, \"y\":{3} }},",
                tick,
                item,
                position.x,
                position.y);
        }

        public static void LogPut(int tick, string item, Point position)
        {
            Console.WriteLine("   {{ \"tick\":{0}, \"event\":\"put\", \"data\":\"{1}\", \"x\":{2}, \"y\":{3} }},",
                tick,
                item,
                position.x,
                position.y);
        }

        public static T Pop<T>(this List<T> stack)
        {
            T ans = stack.Last();
            stack.RemoveAt(stack.Count - 1);
            return ans;
        }

        private static readonly string mapCells = @"# .o%\=";

        public static char MapCellToChar(MapCell cell)
        {
            return mapCells[(int)cell];
        }

        public static MapCell CharToMapCell(char ch)
        {
            return (MapCell)mapCells.IndexOf(ch);
        }
    }


    class Map
    {
        public Map(
            string mapFilename,
            string pacmanFilename,
            List<string> ghostFilenames)
        {
            var file = new StreamReader(mapFilename);
            var fileLines = new List<string>();
            while (!file.EndOfStream)
            {
                fileLines.Add(file.ReadLine());
            }

            Console.WriteLine("   {{ \"tick\":0, \"event\":\"begin\", \"data\":[{0}] }},",
                string.Join(", ", fileLines.Select(i => string.Format("\"{0}\"", i.Replace(@"\", @"\\")))));

            this.Width = fileLines.First().Length;
            this.Height = fileLines.Count;
            this.Ghosts = new List<Ghost>();
            this.data = new MapCell[this.Width, this.Height];

            byte ghostIndex = 0;
            Point point;
            for (point.y = 0; point.y < this.Height; ++point.y)
            {
                for (point.x = 0; point.x < this.Width; ++point.x)
                {
                    var cell = Program.CharToMapCell(fileLines[point.y][point.x]);
                    switch (cell)
                    {
                        case MapCell.Pacman:
                            this.Pacman = new Pacman(pacmanFilename);
                            this.Pacman.InitActor(this, point);
                            Program.LogMove(0, @"\\", point);
                            cell = MapCell.Empty;
                            break;
                        case MapCell.Ghost:
                            var ghost = new Ghost(ghostFilenames[ghostIndex % ghostFilenames.Count], ghostIndex);
                            ghost.InitActor(this, point);
                            Program.LogMove(0, string.Format("={0}", ghostIndex), point);
                            this.Ghosts.Add(ghost);
                            ++ghostIndex;
                            cell = MapCell.Empty;
                            break;
                        case MapCell.Fruit:
                            this.fruitLocation = point;
                            cell = MapCell.Empty;
                            break;
                        case MapCell.Pill:
                            ++this.pillsTotal;
                            break;
                    }
                    this[point] = cell;
                }
            }
        }

        public void Tick(int tick)
        {
            if (tick == this.frightModeDeactivateTime)
            {
                Program.LogDebug(tick, "Fright deactivated");
                foreach (var ghost in this.Ghosts)
                {
                    ghost.Vitality = GhostVitality.Standard;
                }
            }

            if (fruitShowTimes.Contains(tick))
            {
                Program.LogPut(tick, "%", fruitLocation);
                this[fruitLocation] = MapCell.Fruit;
            }

            if (fruitHideTimes.Contains(tick))
            {
                Program.LogPut(tick, " ", fruitLocation);
                this[fruitLocation] = MapCell.Empty;
            }

            switch (this[this.Pacman.CurrentPosition])
            {
                case MapCell.Pill:
                    Program.LogPut(tick, " ", this.Pacman.CurrentPosition);
                    Program.LogDebug(tick, "{0} points", 10);
                    this.Points += 10;
                    ++this.pillsEaten;
                    break;
                case MapCell.PowerPill:
                    Program.LogPut(tick, " ", this.Pacman.CurrentPosition);
                    Program.LogDebug(tick, "Fright activated");
                    Program.LogDebug(tick, "{0} points", 50);
                    this.Points += 50;
                    this.frightModeDeactivateTime = tick + 127 * 20;
                    this.ghostsEaten = 0;
                    foreach (var ghost in this.Ghosts)
                    {
                        ghost.Vitality = GhostVitality.Fright;
                        ghost.Direction = Program.OppositeDirection(ghost.Direction);
                    }
                    break;
                case MapCell.Fruit:
                    Program.LogPut(tick, " ", this.Pacman.CurrentPosition);
                    Program.LogDebug(tick, "Pacman ate fruit");
                    Program.LogDebug(tick, "{0} points", FruitPoints());
                    this.Points += FruitPoints();
                    break;
            }
            this[this.Pacman.CurrentPosition] = MapCell.Empty;

            foreach (var ghost in this.Ghosts)
            {
                if (ghost.CurrentPosition.Equals(this.Pacman.CurrentPosition))
                {
                    switch (ghost.Vitality)
                    {
                        case GhostVitality.Standard:
                            Program.LogDebug(tick, "Pacman died");
                            --this.Pacman.Lives;
                            this.Pacman.InitActor(this, this.Pacman.StartingPosition);
                            Program.LogMove(tick, @"\\", this.Pacman.StartingPosition);
                            foreach (var ghost2 in this.Ghosts)
                            {
                                ghost2.InitActor(this, ghost2.StartingPosition);
                                Program.LogMove(tick, string.Format("={0}", ghost2.GhostIndex), ghost2.StartingPosition);
                            }
                            return;
                        case GhostVitality.Fright:
                            var points = ghostPoints[Math.Min(this.ghostsEaten, ghostPoints.Length - 1)];
                            this.Points += points;
                            ++this.ghostsEaten;
                            ghost.Vitality = GhostVitality.Invisible;
                            ghost.InitActor(this, ghost.StartingPosition);
                            Program.LogDebug(tick, "Pacman ate ghost");
                            Program.LogDebug(tick, "{0} points", points);
                            Program.LogMove(tick, string.Format("={0}", ghost.GhostIndex), ghost.StartingPosition);
                            break;
                    }
                }
            }
        }

        public bool IsGameOver { get { return this.pillsEaten == this.pillsTotal || this.Pacman.Lives == 0;  } }
        public int Level { get { return Width * Height / 100 + 1; } }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Pacman Pacman { get; private set; }
        public List<Ghost> Ghosts { get; private set; }
        public int Points { get; set; }

        public MapCell this[int x, int y]
        {
            get { return (x < this.Width && y < this.Height ? this.data[x, y] : MapCell.Wall); }
            set { this.data[x, y] = value; }
        }

        public MapCell this[Point point]
        {
            get { return this[point.x, point.y]; }
            set { this[point.x, point.y] = value; }
        }

        private Point fruitLocation;
        private MapCell[,] data;
        private int frightModeDeactivateTime;
        private int ghostsEaten;
        private int pillsEaten;
        private int pillsTotal;

        private static readonly int[] fruitShowTimes = new int[] { 127 * 200, 127 * 400 };
        private static readonly int[] fruitHideTimes = new int[] { 127 * 280, 127 * 480 };
        private static readonly int[] fruitPoints = new int[] { 0, 100, 300, 500, 500, 700, 700, 1000, 1000, 2000, 2000, 3000, 3000, 5000 };
        private static readonly int[] ghostPoints = new int[] { 200, 400, 800, 1600, 1600 };

        private int FruitPoints()
        {
            return Map.fruitPoints[Math.Min(this.Level, Map.fruitPoints.Length - 1)];
        }
    }


    abstract class Actor
    {
        public void InitActor(Map map, Point position)
        {
            Map = map;
            StartingPosition = CurrentPosition = position;
            Direction = Direction.Down;
        }

        public void Tick(int tick)
        {
            if (tick == this.ScheduledTick)
            {
                this.ScheduledTick = tick + TickImpl();
            }
        }

        protected abstract int TickImpl();

        public Map Map { get; private set; }
        public Point CurrentPosition { get; set; }
        public Point StartingPosition { get; set; }
        public Direction Direction { get; set; }
        public int ScheduledTick { get; set; }
    }

    
    enum MapCell
    {
        Wall = 0,
        Empty = 1,
        Pill = 2,
        PowerPill = 3,
        Fruit = 4,
        Pacman = 5,
        Ghost = 6
    }


    enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2, 
        Left = 3
    }


    struct Point
    {
        public Point(byte x, byte y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(Point other)
        {
            return x == other.x && y == other.y;
        }

        public Point Move(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new Point(x, (byte)(y - 1));
                case Direction.Down: return new Point(x, (byte)(y + 1));
                case Direction.Left: return new Point((byte)(x - 1), y);
                case Direction.Right: return new Point((byte)(x + 1), y);
                default: throw new Exception();
            }
        }

        public byte x;
        public byte y;
    }
}
