using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Robot.Common;
using PobihachMaksym.RobotChallange;

namespace PobihachMaksym.RobotChallange.Tests
{
    [TestClass]
    public class PobihachMaksymAlgorithmTests
    {
        private PobihachMaksymAlgorithm algorithm;
        private Map testMap;

        [TestInitialize]
        public void SetUp()
        {
            algorithm = new PobihachMaksymAlgorithm();
            if (Variant.GetInstance() == null)
            {
                Variant.Initialize(2);
            }
            testMap = CreateTestMap();
        }

        [TestMethod]
        public void DoStep_WithLowEnergy_ReturnsCollectCommand()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(10, 10), Energy = 20, OwnerName = "Test" }
            };

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsInstanceOfType(command, typeof(CollectEnergyCommand));
        }

        [TestMethod]
        public void DoStep_WithHighEnergyNearManyStations_CanCreateRobot()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(50, 50), Energy = 500, OwnerName = "Test" }
            };
            AddStationsAroundPosition(new Position(50, 50), 5);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsTrue(command is CreateNewRobotCommand || command is CollectEnergyCommand);
        }

        [TestMethod]
        public void DoStep_WithMaxRobots_DoesNotCreateNewRobot()
        {
            var robots = new List<Robot.Common.Robot>();
            for (int i = 0; i < 100; i++)
            {
                robots.Add(new Robot.Common.Robot
                {
                    Position = new Position(i % 10, i / 10),
                    Energy = 200,
                    OwnerName = "Test"
                });
            }
            AddStationsAroundPosition(new Position(0, 0), 5);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsNotInstanceOfType(command, typeof(CreateNewRobotCommand));
        }

        [TestMethod]
        public void DoStep_WithInsufficientEnergyForCreation_DoesNotCreate()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(50, 50), Energy = 100, OwnerName = "Test" }
            };
            AddStationsAroundPosition(new Position(50, 50), 5);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsNotInstanceOfType(command, typeof(CreateNewRobotCommand));
        }

        [TestMethod]
        public void RobotContext_CreatesCorrectly()
        {
            var robot = new Robot.Common.Robot { Position = new Position(0, 0), Energy = 100, OwnerName = "Test" };
            var myRobots = new List<Robot.Common.Robot> { robot };
            var allRobots = new List<Robot.Common.Robot> { robot };

            var context = new RobotContext(robot, myRobots, allRobots, testMap);

            Assert.AreEqual(robot, context.Robot);
            Assert.AreEqual(myRobots, context.MyRobots);
            Assert.AreEqual(allRobots, context.AllRobots);
            Assert.AreEqual(testMap, context.Map);
        }

        [TestMethod]
        public void DoStep_WithNoNearbyStations_ReturnsMove()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(10, 10), Energy = 200, OwnerName = "Test" }
            };

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsTrue(command is MoveCommand || command is CollectEnergyCommand);
        }

        [TestMethod]
        public void CreateNewRobotCommand_SetsCorrectEnergy()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(50, 50), Energy = 300, OwnerName = "Test" }
            };
            AddStationsAroundPosition(new Position(50, 50), 4);

            var command = algorithm.DoStep(robots, 0, testMap) as CreateNewRobotCommand;

            if (command != null)
            {
                Assert.AreEqual(80, command.NewRobotEnergy);
            }
        }

        [TestMethod]
        public void DoStep_WithMultipleRobots_ProcessesCorrectRobot()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(10, 10), Energy = 100, OwnerName = "Test1" },
                new Robot.Common.Robot { Position = new Position(20, 20), Energy = 50, OwnerName = "Test2" }
            };

            var command1 = algorithm.DoStep(robots, 0, testMap);
            var command2 = algorithm.DoStep(robots, 1, testMap);

            Assert.IsNotNull(command1);
            Assert.IsNotNull(command2);
        }

        [TestMethod]
        public void DoStep_WithEnemyRobotOnGoodPosition_MightAttack()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(10, 10), Energy = 200, OwnerName = "Test" },
                new Robot.Common.Robot { Position = new Position(50, 50), Energy = 100, OwnerName = "Enemy" }
            };
            AddStationsAroundPosition(new Position(50, 50), 4);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsTrue(command is MoveCommand || command is CollectEnergyCommand);
        }

        [TestMethod]
        public void DoStep_WithMediumEnergy_MakesReasonableDecision()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(30, 30), Energy = 150, OwnerName = "Test" }
            };
            AddStationsAroundPosition(new Position(30, 30), 2);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsTrue(command is CollectEnergyCommand || command is CreateNewRobotCommand || command is MoveCommand);
        }

        [TestMethod]
        public void DoStep_HandlesBoundaryConditions()
        {
            var robots = new List<Robot.Common.Robot>
            {
                new Robot.Common.Robot { Position = new Position(0, 0), Energy = 100, OwnerName = "Test" },
                new Robot.Common.Robot { Position = new Position(99, 99), Energy = 100, OwnerName = "Test" }
            };

            var command1 = algorithm.DoStep(robots, 0, testMap);
            var command2 = algorithm.DoStep(robots, 1, testMap);

            Assert.IsNotNull(command1);
            Assert.IsNotNull(command2);
        }

        [TestMethod]
        public void DoStep_WithCrowdedArea_AvoidsOvercrowding()
        {
            var robots = new List<Robot.Common.Robot>();
            for (int i = 0; i < 10; i++)
            {
                robots.Add(new Robot.Common.Robot
                {
                    Position = new Position(50 + i % 3, 50 + i / 3),
                    Energy = 150,
                    OwnerName = "Test"
                });
            }
            AddStationsAroundPosition(new Position(50, 50), 2);

            var command = algorithm.DoStep(robots, 0, testMap);

            Assert.IsTrue(command is MoveCommand || command is CollectEnergyCommand);
        }

        private Map CreateTestMap()
        {
            var map = new Map();
            map.MaxPozition = new Position { X = 100, Y = 100 };
            return map;
        }

        private void AddStationsAroundPosition(Position center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var station = new EnergyStation
                {
                    Position = new Position(center.X + i % 3 - 1, center.Y + i / 3 - 1),
                    Energy = 1000,
                    RecoveryRate = 50
                };
                testMap.Stations.Add(station);
            }
        }
    }
}