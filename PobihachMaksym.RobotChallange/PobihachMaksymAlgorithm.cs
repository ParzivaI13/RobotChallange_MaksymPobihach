using System;
using System.Collections.Generic;
using System.Linq;
using Robot.Common;

namespace PobihachMaksym.RobotChallange
{
    public class RobotActionEventArgs : EventArgs
    {
        public string RobotOwner { get; set; }
        public string Action { get; set; }
        public Position Position { get; set; }
        public int Energy { get; set; }
        public string Reason { get; set; }
    }

    public class PobihachMaksymAlgorithm : IRobotAlgorithm
    {
        public string Author => "Pobihach Maksym";

        public static event EventHandler<RobotActionEventArgs> RobotActionTaken;

        private const int COLLECTING_DISTANCE = 2;
        private const int MIN_ENERGY_FOR_SURVIVAL = 30;
        private const int MIN_ENERGY_FOR_CREATE = 150;
        private const int NEW_ROBOT_ENERGY = 80;
        private const int MAX_ROBOTS = 100;

        private const int EXCELLENT_POSITION = 4;
        private const int GOOD_POSITION = 3;
        private const int DECENT_POSITION = 2;

        private const int EXPLORATION_RADIUS = 15;
        private const int MAX_MOVE_DISTANCE = 8;

        private static readonly Random Random = new Random();

        public RobotCommand DoStep(IList<Robot.Common.Robot> robots, int robotToMoveIndex, Map map)
        {
            var robot = robots[robotToMoveIndex];
            var myRobots = GetMyRobots(robots, robot.OwnerName);
            var context = new RobotContext(robot, myRobots, robots.ToList(), map);

            var command = DecideOptimalAction(context);

            // Викликаємо подію для логування
            OnRobotActionTaken(robot, GetActionType(command), GetActionReason(command, context));

            return command;
        }

        private RobotCommand DecideOptimalAction(RobotContext context)
        {
            var nearbyStations = GetNearbyStations(context.Robot, context.Map);
            int stationCount = nearbyStations.Count;

            if (context.Robot.Energy < MIN_ENERGY_FOR_SURVIVAL)
                return new CollectEnergyCommand();

            if (stationCount >= EXCELLENT_POSITION)
            {
                if (ShouldCreateRobotAggressively(context, nearbyStations))
                    return new CreateNewRobotCommand { NewRobotEnergy = NEW_ROBOT_ENERGY };
                return new CollectEnergyCommand();
            }

            if (stationCount >= GOOD_POSITION)
            {
                if (ShouldCreateRobotModerately(context, nearbyStations))
                    return new CreateNewRobotCommand { NewRobotEnergy = NEW_ROBOT_ENERGY };

                if (!IsAreaOvercrowded(context, nearbyStations))
                    return new CollectEnergyCommand();
            }

            // Пошук кращих можливостей
            return FindOptimalMovement(context) ?? new CollectEnergyCommand();
        }

        private bool ShouldCreateRobotAggressively(RobotContext context, List<EnergyStation> stations)
        {
            if (context.MyRobots.Count >= MAX_ROBOTS || context.Robot.Energy < MIN_ENERGY_FOR_CREATE)
                return false;

            var nearbyMyRobots = CountNearbyMyRobots(context);
            var totalStationEnergy = stations.Sum(s => s.Energy);

            return nearbyMyRobots < 2 ||
                   totalStationEnergy > 500 ||
                   context.Robot.Energy > 300;
        }

        private bool ShouldCreateRobotModerately(RobotContext context, List<EnergyStation> stations)
        {
            if (context.MyRobots.Count >= MAX_ROBOTS || context.Robot.Energy < MIN_ENERGY_FOR_CREATE)
                return false;

            var nearbyMyRobots = CountNearbyMyRobots(context);

            return nearbyMyRobots == 0 ||
                   (nearbyMyRobots == 1 && context.Robot.Energy > 250);
        }

        private RobotCommand FindOptimalMovement(RobotContext context)
        {
            var attackTarget = FindPriorityAttackTarget(context);
            if (attackTarget != null)
                return new MoveCommand { NewPosition = attackTarget.Position };

            var bestPosition = FindBestAvailablePosition(context);
            if (bestPosition != null)
            {
                var nextMove = GetOptimalMoveTowards(context, bestPosition);
                if (nextMove != null)
                    return new MoveCommand { NewPosition = nextMove };
            }

            var explorationMove = ExploreNewAreas(context);
            if (explorationMove != null)
                return new MoveCommand { NewPosition = explorationMove };

            return null;
        }

        private Robot.Common.Robot FindPriorityAttackTarget(RobotContext context)
        {
            return context.AllRobots
                .Where(r => r.OwnerName != context.Robot.OwnerName)
                .Where(r => GetNearbyStations(r, context.Map).Count >= EXCELLENT_POSITION)
                .Where(r => CanReachEfficiently(context.Robot, r.Position))
                .OrderByDescending(r => GetNearbyStations(r, context.Map).Count)
                .ThenBy(r => GetDistance(context.Robot.Position, r.Position))
                .FirstOrDefault();
        }

        private Position FindBestAvailablePosition(RobotContext context)
        {
            var bestPositions = new List<(Position pos, int score)>();

            for (int x = 0; x < 100; x += 3)
            {
                for (int y = 0; y < 100; y += 3)
                {
                    var pos = new Position(x, y);
                    var stations = GetNearbyStations(new Robot.Common.Robot { Position = pos }, context.Map);

                    if (stations.Count >= DECENT_POSITION &&
                        !IsPositionOccupied(pos, context.AllRobots) &&
                        CanReachEfficiently(context.Robot, pos))
                    {
                        int score = CalculatePositionValue(pos, stations, context);
                        bestPositions.Add((pos, score));
                    }
                }
            }

            if (bestPositions.Count == 0) return null;

            return bestPositions
                .OrderByDescending(p => p.score)
                .Take(5)
                .OrderBy(p => GetDistance(context.Robot.Position, p.pos))
                .First().pos;
        }

        private int CalculatePositionValue(Position pos, List<EnergyStation> stations, RobotContext context)
        {
            int baseScore = stations.Count * 100;

            if (stations.Count >= EXCELLENT_POSITION)
                baseScore += 300;
            else if (stations.Count >= GOOD_POSITION)
                baseScore += 150;

            int totalEnergy = stations.Sum(s => s.Energy);
            baseScore += totalEnergy / 10;

            double distance = GetDistance(context.Robot.Position, pos);
            baseScore -= (int)(distance * 5);

            var nearbyRobots = context.AllRobots.Count(r =>
                IsWithinDistance(r.Position, pos, COLLECTING_DISTANCE * 2));
            baseScore -= nearbyRobots * 50;

            return baseScore;
        }

        private Position GetOptimalMoveTowards(RobotContext context, Position target)
        {
            var robot = context.Robot;
            var bestMove = robot.Position;
            double bestScore = -1000;

            for (int distance = 1; distance <= Math.Min(MAX_MOVE_DISTANCE, (int)Math.Sqrt(robot.Energy / 2)); distance++)
            {
                var moves = GenerateMovesInDirection(robot.Position, target, distance);

                foreach (var move in moves)
                {
                    if (!context.Map.IsValid(move)) continue;

                    int cost = GetMoveCost(robot.Position, move);
                    if (IsPositionOccupied(move, context.AllRobots)) cost += 20;
                    if (robot.Energy < cost + MIN_ENERGY_FOR_SURVIVAL) continue;

                    double progress = GetDistance(robot.Position, target) - GetDistance(move, target);
                    double stationBonus = GetNearbyStations(new Robot.Common.Robot { Position = move }, context.Map).Count * 20;
                    double score = progress * 100 + stationBonus - cost;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
            }

            return bestMove.Equals(robot.Position) ? null : bestMove;
        }

        private Position ExploreNewAreas(RobotContext context)
        {
            var sectors = DivideMapIntoSectors(4);
            var emptySector = sectors
                .OrderBy(sector => CountMyRobotsInSector(sector, context.MyRobots))
                .First();

            var targetInSector = FindGoodPositionInSector(emptySector, context);
            if (targetInSector != null)
            {
                return GetOptimalMoveTowards(context, targetInSector);
            }

            return null;
        }

        private List<Position> GenerateMovesInDirection(Position from, Position target, int maxDistance)
        {
            var moves = new List<Position>();
            int dx = target.X - from.X;
            int dy = target.Y - from.Y;

            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0) return moves;

            dx = (int)Math.Round(dx / length * maxDistance);
            dy = (int)Math.Round(dy / length * maxDistance);

            moves.Add(new Position(from.X + dx, from.Y + dy));

            if (Math.Abs(dx) > 1) moves.Add(new Position(from.X + dx / 2, from.Y + dy));
            if (Math.Abs(dy) > 1) moves.Add(new Position(from.X + dx, from.Y + dy / 2));
            if (dx != 0) moves.Add(new Position(from.X + dx, from.Y));
            if (dy != 0) moves.Add(new Position(from.X, from.Y + dy));

            return moves.Where(p => p.X >= 0 && p.X < 100 && p.Y >= 0 && p.Y < 100).ToList();
        }

        private List<EnergyStation> GetNearbyStations(Robot.Common.Robot robot, Map map) =>
            map.GetNearbyResources(robot.Position, COLLECTING_DISTANCE);

        private int CountNearbyMyRobots(RobotContext context) =>
            context.MyRobots.Count(r => r != context.Robot &&
                IsWithinDistance(r.Position, context.Robot.Position, COLLECTING_DISTANCE * 2));

        private bool IsAreaOvercrowded(RobotContext context, List<EnergyStation> stations)
        {
            if (stations.Count == 0) return true;

            var nearbyRobots = context.AllRobots.Count(r =>
                IsWithinDistance(r.Position, context.Robot.Position, COLLECTING_DISTANCE));

            return nearbyRobots > stations.Count;
        }

        private bool CanReachEfficiently(Robot.Common.Robot robot, Position target)
        {
            int cost = GetMoveCost(robot.Position, target);
            return robot.Energy >= cost + MIN_ENERGY_FOR_SURVIVAL && cost <= robot.Energy / 2;
        }

        private bool IsPositionOccupied(Position pos, List<Robot.Common.Robot> allRobots) =>
            allRobots.Any(r => r.Position.Equals(pos));

        private List<Robot.Common.Robot> GetMyRobots(IList<Robot.Common.Robot> robots, string ownerName) =>
            robots.Where(r => r.OwnerName == ownerName).ToList();

        private int GetMoveCost(Position from, Position to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return dx * dx + dy * dy;
        }

        private double GetDistance(Position from, Position to) =>
            Math.Sqrt(GetMoveCost(from, to));

        private bool IsWithinDistance(Position pos1, Position pos2, int distance) =>
            Math.Abs(pos1.X - pos2.X) <= distance && Math.Abs(pos1.Y - pos2.Y) <= distance;

        private List<(int x1, int y1, int x2, int y2)> DivideMapIntoSectors(int sectorsPerSide)
        {
            var sectors = new List<(int, int, int, int)>();
            int sectorSize = 100 / sectorsPerSide;

            for (int i = 0; i < sectorsPerSide; i++)
            {
                for (int j = 0; j < sectorsPerSide; j++)
                {
                    sectors.Add((i * sectorSize, j * sectorSize,
                                (i + 1) * sectorSize, (j + 1) * sectorSize));
                }
            }
            return sectors;
        }

        private int CountMyRobotsInSector((int x1, int y1, int x2, int y2) sector, List<Robot.Common.Robot> myRobots)
        {
            return myRobots.Count(r => r.Position.X >= sector.x1 && r.Position.X < sector.x2 &&
                                      r.Position.Y >= sector.y1 && r.Position.Y < sector.y2);
        }

        private Position FindGoodPositionInSector((int x1, int y1, int x2, int y2) sector, RobotContext context)
        {
            for (int x = sector.x1; x < sector.x2; x += 5)
            {
                for (int y = sector.y1; y < sector.y2; y += 5)
                {
                    var pos = new Position(x, y);
                    var stations = GetNearbyStations(new Robot.Common.Robot { Position = pos }, context.Map);

                    if (stations.Count >= DECENT_POSITION &&
                        !IsPositionOccupied(pos, context.AllRobots))
                    {
                        return pos;
                    }
                }
            }
            return null;
        }

        private void OnRobotActionTaken(Robot.Common.Robot robot, string action, string reason)
        {
            RobotActionTaken?.Invoke(this, new RobotActionEventArgs
            {
                RobotOwner = robot.OwnerName,
                Action = action,
                Position = robot.Position,
                Energy = robot.Energy,
                Reason = reason
            });
        }

        private string GetActionType(RobotCommand command)
        {
            if (command is CollectEnergyCommand) return "Collect";
            if (command is CreateNewRobotCommand) return "Create";
            if (command is MoveCommand) return "Move";
            return "Unknown";
        }

        private string GetActionReason(RobotCommand command, RobotContext context)
        {
            var stations = GetNearbyStations(context.Robot, context.Map);

            if (command is CollectEnergyCommand)
            {
                if (context.Robot.Energy < MIN_ENERGY_FOR_SURVIVAL)
                    return "Critical energy";
                return $"Farming at {stations.Count} stations";
            }
            if (command is CreateNewRobotCommand)
                return $"Rapid expansion ({context.MyRobots.Count} robots)";
            if (command is MoveCommand moveCmd)
                return $"Seeking optimal position";
            return "Fallback action";
        }
    }

    public class RobotContext
    {
        public Robot.Common.Robot Robot { get; }
        public List<Robot.Common.Robot> MyRobots { get; }
        public List<Robot.Common.Robot> AllRobots { get; }
        public Map Map { get; }

        public RobotContext(Robot.Common.Robot robot, List<Robot.Common.Robot> myRobots,
                          List<Robot.Common.Robot> allRobots, Map map)
        {
            Robot = robot;
            MyRobots = myRobots;
            AllRobots = allRobots;
            Map = map;
        }
    }
}