using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RLNET;
using RogueSharp;

namespace Roguelike.Core
{
    public class DungeonMap : Map
    {
        public List<Rectangle> Rooms;

        public List<Door> Doors { get; set; }

        public Stairs StairsUp { get; set; }

        public Stairs StairsDown { get; set; }

        private readonly List<Monster> _monsters;

        public DungeonMap()
        {
            Game.SchedulingSystem.Clear();
            //Initialize the monster list
            _monsters = new List<Monster>();

            //Initialize list of rooms when creating a new DungeonMap
            Rooms = new List<Rectangle>();

            Doors = new List<Door>();
        }

        //The Draw method fo rendering to map console
        public void Draw(RLConsole mapConsole, RLConsole statConsole)
        {
            foreach (Cell cell in GetAllCells())
            {
                SetConsoleSymbolForCell(mapConsole, cell);
            }
            // Keep an index so we know which position to draw monster stats at
            int i = 0;

            // Iterate through each monster on the map and draw it after drawing the Cells
            foreach (Monster monster in _monsters)
            {
                monster.Draw(mapConsole, this);
                // When the monster is in the field-of-view also draw their stats
                if (IsInFov(monster.X, monster.Y))
                {

                    // Pass in the index to DrawStats and increment it afterwards
                    monster.DrawStats(statConsole, i);
                    i++;
                }
            }

            foreach (Door door in Doors)
            {
                door.Draw(mapConsole, this);
            }

            StairsUp.Draw(mapConsole, this);
            StairsDown.Draw(mapConsole, this);
        }

        private void SetConsoleSymbolForCell(RLConsole console, Cell cell)
        {
            //Dont draw anything unless cell is explored
            if (!cell.IsExplored)
            {
                return;
            }

            //when cell is in FOV, draw with lighter colors
            if (IsInFov(cell.X, cell.Y))
            {
                //choose symbol to draw based on if it's walkable or not
                if (cell.IsWalkable)
                {
                    console.Set(cell.X, cell.Y, Colors.FloorFov, Colors.FloorBackgroundFov, '.');
                }
                else
                {
                    console.Set(cell.X, cell.Y, Colors.WallFov, Colors.WallBackgroundFov, '#');
                }
            }
            //when a cell is outside the FOV, draw w/ darker colors
            else
            {
                if (cell.IsWalkable)
                {
                    console.Set(cell.X, cell.Y, Colors.Floor, Colors.FloorBackground, '.');
                }
                else
                {
                    console.Set(cell.X, cell.Y, Colors.Wall, Colors.WallBackground, '#');
                }
            }
        }

        public void AddPlayer(Player player)
        {
            Game.Player = player;
            SetIsWalkable(player.X, player.Y, false);
            UpdatePlayerFieldOfView();
            Game.SchedulingSystem.Add(player);
        }

        //Called every time we update the player
        public void UpdatePlayerFieldOfView()
        {
            Player player = Game.Player;
            //update FOV based on player's location and awareness
            ComputeFov(player.X, player.Y, player.Awareness, true);
            //mark all cells in FOV as having been explored
            foreach (Cell cell in GetAllCells())
            {
                if (IsInFov(cell.X, cell.Y))
                {
                    SetCellProperties(cell.X, cell.Y, cell.IsTransparent, cell.IsWalkable, true);
                }
            }
        }

        //return true when able to place the Actor on the cell or false otherwise
        public bool SetActorPosition(Actor actor, int x, int y)
        {
            //only allow actor placement if the cell is walkable
            if (GetCell(x, y).IsWalkable)
            {
                // the cell the actor was previously on is now walkable
                SetIsWalkable(actor.X, actor.Y, true);
                //update actor's position
                actor.X = x;
                actor.Y = y;
                //new cell actor is on is no longer walkable
                SetIsWalkable(actor.X, actor.Y, false);
                //Update FOV is player is repositioned
                    if (actor is Player)
                    {
                        UpdatePlayerFieldOfView();
                    }
                    return true;
             }
                return false;

            OpenDoor(actor, x, y);
        }
        //helper method to set IsWalkable on a cell
        public void SetIsWalkable(int x, int y, bool isWalkable)
        {
            Cell cell = GetCell(x, y);
            SetCellProperties(cell.X, cell.Y, cell.IsTransparent, isWalkable, cell.IsExplored);
        }

        public void AddMonster(Monster monster)
        {
            _monsters.Add(monster);
            SetIsWalkable(monster.X, monster.Y, false);
            Game.SchedulingSystem.Add(monster);
        }

        public void RemoveMonster(Monster monster)
        {
            _monsters.Remove(monster);
            // After removing the monster from the map, make sure the cell is walkable again
            SetIsWalkable(monster.X, monster.Y, true);
            Game.SchedulingSystem.Remove(monster);
        }

        public Monster GetMonsterAt(int x, int y)
        {
            return _monsters.FirstOrDefault(m => m.X == x && m.Y == y);
        }

        //look for random location in the room that is walkable
        public Point GetRandomWalkableLocationInRoom(Rectangle room)
        {
            if (DoesRoomHaveWalkableSpace(room))
            {
                for (int i = 0; i < 100; i++)
                {
                    int x = Game.Random.Next(1, room.Width - 2) + room.X;
                    int y = Game.Random.Next(1, room.Height - 2) + room.Y;
                    if (IsWalkable(x, y))
                    {
                        return new Point(x, y);
                    }
                }
            }
            //If we don't find walkable space, return null
            return null;
        }

        //iterate through each cell in the room and return true if there are any walkable tiles
        public bool DoesRoomHaveWalkableSpace(Rectangle room)
        {
            for (int x = 1; x <= room.Width - 2; x++)
            {
                for (int y = 1; y <= room.Height - 2; y++)
                {
                    if (IsWalkable(x + room.X, y + room.Y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Return the door at the x,y position or null if one is not found.
        public Door GetDoor(int x, int y)
        {
            return Doors.SingleOrDefault(d => d.X == x && d.Y == y);
        }

        // The actor opens the door located at the x,y position
        private void OpenDoor(Actor actor, int x, int y)
        {
            Door door = GetDoor(x, y);
            if (door != null && !door.IsOpen)
            {
                door.IsOpen = true;
                var cell = GetCell(x, y);
                // Once the door is opened it should be marked as transparent and no longer block field-of-view
                SetCellProperties(x, y, true, cell.IsWalkable, cell.IsExplored);

                Game.MessageLog.Add($"{actor.Name} opened a door");
            }
        }

        public bool CanMoveDownToNextLevel()
        {
            Player player = Game.Player;
            return StairsDown.X == player.X && StairsDown.Y == player.Y;
        }
    }
}
