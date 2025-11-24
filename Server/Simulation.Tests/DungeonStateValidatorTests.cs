using System;
using System.Collections.Generic;
using Adventure.Server.Generation;
using Adventure.Shared.Network.Messages;
using FluentAssertions;
using Xunit;

namespace Adventure.Server.Simulation.Tests
{
    public class DungeonStateValidatorTests
    {
        [Fact]
        public void TryOpenDoor_Denies_When_EnemyRoom_Uncleared()
        {
            var enemyTemplate = new RoomTemplate { TemplateId = "enemy", Features = RoomFeature.None };
            var safeTemplate = new RoomTemplate { TemplateId = "safe", Features = RoomFeature.None };
            var enemyRoom = new GeneratedRoom("enemy-1", enemyTemplate, 0, RoomArchetype.Enemy, RoomFeature.None);
            var safeRoom = new GeneratedRoom("safe-1", safeTemplate, 1, RoomArchetype.Safe, RoomFeature.None);
            var door = new GeneratedDoor
            {
                DoorId = "door-1",
                FromRoomId = enemyRoom.RoomId,
                ToRoomId = safeRoom.RoomId,
                State = DoorState.Locked
            };

            enemyRoom.Doors.Add(door);
            var dungeon = new GeneratedDungeon(
                "dng",
                new List<GeneratedRoom> { enemyRoom, safeRoom },
                new List<GeneratedDoor> { door },
                new List<GeneratedInteractive>(),
                new List<GeneratedEnvironmentState>(),
                new List<DoorConfig>());

            var validator = new DungeonStateValidator(dungeon);

            var result = validator.TryOpenDoor("player", door.DoorId, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be("room_uncleared");
        }

        [Fact]
        public void TryOpenDoor_Allows_When_EnemyRoom_Cleared()
        {
            var enemyTemplate = new RoomTemplate { TemplateId = "enemy", Features = RoomFeature.None };
            var safeTemplate = new RoomTemplate { TemplateId = "safe", Features = RoomFeature.None };
            var enemyRoom = new GeneratedRoom("enemy-1", enemyTemplate, 0, RoomArchetype.Enemy, RoomFeature.None);
            var safeRoom = new GeneratedRoom("safe-1", safeTemplate, 1, RoomArchetype.Safe, RoomFeature.None);
            var door = new GeneratedDoor
            {
                DoorId = "door-1",
                FromRoomId = enemyRoom.RoomId,
                ToRoomId = safeRoom.RoomId,
                State = DoorState.Locked
            };

            enemyRoom.Doors.Add(door);
            var dungeon = new GeneratedDungeon(
                "dng",
                new List<GeneratedRoom> { enemyRoom, safeRoom },
                new List<GeneratedDoor> { door },
                new List<GeneratedInteractive>(),
                new List<GeneratedEnvironmentState>(),
                new List<DoorConfig>());

            var validator = new DungeonStateValidator(dungeon);

            validator.MarkRoomCleared(enemyRoom.RoomId).Accepted.Should().BeTrue();
            var result = validator.TryOpenDoor("player", door.DoorId, null);

            result.Accepted.Should().BeTrue();
        }
    }
}
